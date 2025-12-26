using Microsoft.Extensions.Caching.Memory;
using Miningcore.Api.Responses;
using Miningcore.Blockchain.Ethereum;
using Miningcore.Configuration;
using Miningcore.Extensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;
using System.Globalization;
using System.Net;
using System.Numerics;
using System.Text;

namespace Miningcore.Api.Enrichment;

public class EthereumBlockEnricher
{
    public EthereumBlockEnricher(IHttpClientFactory httpClientFactory, IMemoryCache cache)
    {
        this.httpClientFactory = httpClientFactory;
        this.cache = cache;
    }

    private readonly IHttpClientFactory httpClientFactory;
    private readonly IMemoryCache cache;

    private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

    private record EthereumBlockMeta(double? Difficulty, DateTime? Timestamp, int? TxCount, string Miner,
        long? GasUsed, long? GasLimit, long? BaseFeePerGas, long? SizeBytes);

    public async Task<ulong?> GetChainTipHeightAsync(PoolConfig pool, CancellationToken ct)
    {
        try
        {
            if(pool?.Template?.Family != CoinFamily.Ethereum)
                return null;

            var daemon = pool.Daemons?.FirstOrDefault(x => string.IsNullOrEmpty(x.Category)) ?? pool.Daemons?.FirstOrDefault();
            if(daemon == null)
                return null;

            if(!IsLoopbackHost(daemon.Host))
            {
                logger.Warn(() => $"Skipping eth_blockNumber because daemon host is not loopback: {daemon.Host}");
                return null;
            }

            var scheme = daemon.Ssl ? "https" : "http";
            var rpcUrl = new UriBuilder(scheme, daemon.Host, daemon.Port, daemon.HttpPath?.TrimStart('/') ?? string.Empty).Uri;

            var cacheKey = $"ethtip:{pool.Id}";

            return await cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(3);

                try
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts.CancelAfter(TimeSpan.FromSeconds(2));

                    var payload = new JObject
                    {
                        ["jsonrpc"] = "2.0",
                        ["id"] = 1,
                        ["method"] = EthCommands.GetBlockNumber,
                        ["params"] = new JArray(),
                    };

                    var client = httpClientFactory.CreateClient();
                    using var content = new StringContent(payload.ToString(Formatting.None), Encoding.UTF8, "application/json");
                    using var response = await client.PostAsync(rpcUrl, content, cts.Token);

                    if(!response.IsSuccessStatusCode)
                    {
                        logger.Warn(() => $"eth_blockNumber failed with HTTP {(int) response.StatusCode}");
                        return (ulong?) null;
                    }

                    var json = await response.Content.ReadAsStringAsync(cts.Token);
                    var root = JObject.Parse(json);
                    var resultHex = root["result"]?.Value<string>();

                    return TryParseHexUlong(resultHex);
                }

                catch(Exception ex)
                {
                    logger.Warn(ex, () => "Error fetching eth_blockNumber");
                    return (ulong?) null;
                }
            });
        }

        catch(Exception ex)
        {
            logger.Warn(ex, () => "Error getting eth chain tip height");
            return null;
        }
    }

    private static ulong? TryParseHexUlong(string value)
    {
        if(string.IsNullOrWhiteSpace(value))
            return null;

        value = value.Trim();

        if(value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            value = value[2..];

        if(string.IsNullOrWhiteSpace(value))
            return null;

        try
        {
            if(BigInteger.TryParse(value, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var big) &&
               big >= 0 &&
               big <= ulong.MaxValue)
            {
                return (ulong) big;
            }

            return null;
        }

        catch
        {
            return null;
        }
    }

    public async Task EnrichAsync(PoolConfig pool, Block[] blocks, CancellationToken ct)
    {
        try
        {
            if(pool?.Template?.Family != CoinFamily.Ethereum || blocks == null || blocks.Length == 0)
                return;

            var daemon = pool.Daemons?.FirstOrDefault(x => string.IsNullOrEmpty(x.Category)) ?? pool.Daemons?.FirstOrDefault();
            if(daemon == null)
                return;

            if(!IsLoopbackHost(daemon.Host))
            {
                logger.Warn(() => $"Skipping eth_getBlockByNumber enrichment because daemon host is not loopback: {daemon.Host}");
                return;
            }

            var scheme = daemon.Ssl ? "https" : "http";
            var rpcUrl = new UriBuilder(scheme, daemon.Host, daemon.Port, daemon.HttpPath?.TrimStart('/') ?? string.Empty).Uri;

            var heights = blocks.Select(x => x.BlockHeight).Distinct().ToArray();
            var neededHeights = new HashSet<ulong>(heights);

            foreach(var height in heights)
            {
                if(height > 0)
                    neededHeights.Add(height - 1);
            }

            var metaTasks = neededHeights.ToDictionary(height => height,
                height => GetBlockMetaCachedAsync(rpcUrl, height, ct));

            await Task.WhenAll(metaTasks.Values);

            foreach(var block in blocks)
            {
                if(metaTasks.TryGetValue(block.BlockHeight, out var metaTask))
                {
                    var meta = metaTask.Result;
                    block.BlockDifficulty = meta?.Difficulty;
                    block.BlockTimestamp = meta?.Timestamp;
                    block.TxCount = meta?.TxCount;
                    block.BlockMiner = meta?.Miner;
                    block.GasUsed = meta?.GasUsed;
                    block.GasLimit = meta?.GasLimit;
                    block.BaseFeePerGas = meta?.BaseFeePerGas;
                    block.BlockSizeBytes = meta?.SizeBytes;
                }

                if(block.BlockHeight > 0 &&
                   metaTasks.TryGetValue(block.BlockHeight, out var currentTask) &&
                   metaTasks.TryGetValue(block.BlockHeight - 1, out var prevTask))
                {
                    var currentTs = currentTask.Result?.Timestamp;
                    var prevTs = prevTask.Result?.Timestamp;

                    if(currentTs.HasValue && prevTs.HasValue)
                    {
                        var delta = (currentTs.Value - prevTs.Value).TotalSeconds;

                        if(delta >= 0 && double.IsFinite(delta))
                            block.BlockTimeSeconds = delta;
                    }
                }
            }
        }

        catch(Exception ex)
        {
            logger.Warn(ex, () => "Error enriching blocks with eth block meta");
        }
    }

    private static bool IsLoopbackHost(string host)
    {
        if(string.IsNullOrWhiteSpace(host))
            return false;

        if(host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            return true;

        return IPAddress.TryParse(host, out var ip) && IPAddress.IsLoopback(ip);
    }

    private async Task<EthereumBlockMeta> GetBlockMetaCachedAsync(Uri rpcUrl, ulong blockHeight, CancellationToken ct)
    {
        var cacheKey = $"ethblock:{blockHeight}";

        try
        {
            return await cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);

                try
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts.CancelAfter(TimeSpan.FromSeconds(3));

                    var payload = new JObject
                    {
                        ["jsonrpc"] = "2.0",
                        ["id"] = 1,
                        ["method"] = EthCommands.GetBlockByNumber,
                        ["params"] = new JArray(blockHeight.ToStringHexWithPrefix(), false),
                    };

                    var client = httpClientFactory.CreateClient();
                    using var content = new StringContent(payload.ToString(Formatting.None), Encoding.UTF8, "application/json");
                    using var response = await client.PostAsync(rpcUrl, content, cts.Token);

                    if(!response.IsSuccessStatusCode)
                    {
                        logger.Warn(() => $"eth_getBlockByNumber({blockHeight}) failed with HTTP {(int) response.StatusCode}");
                        return null;
                    }

                    var json = await response.Content.ReadAsStringAsync(cts.Token);
                    var root = JObject.Parse(json);

                    var result = root["result"] as JObject;
                    if(result == null)
                        return null;

                    static bool TryParseHexBigInteger(string value, out BigInteger result)
                    {
                        result = BigInteger.Zero;

                        if(string.IsNullOrWhiteSpace(value))
                            return false;

                        if(value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                            value = value[2..];

                        if(string.IsNullOrWhiteSpace(value))
                            return false;

                        try
                        {
                            if(value.Length % 2 == 1)
                                value = "0" + value;

                            // BigInteger.Parse expects a two's complement representation for hex strings and
                            // will treat the highest bit as sign. Prefix a leading 00 byte to force unsigned.
                            if(byte.TryParse(value.AsSpan(0, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var msb) && msb >= 0x80)
                                value = "00" + value;

                            return BigInteger.TryParse(value, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out result);
                        }

                        catch
                        {
                            return false;
                        }
                    }

                    static long? TryParseHexLong(string value)
                    {
                        if(!TryParseHexBigInteger(value, out var big))
                            return null;

                        if(big < 0 || big > long.MaxValue)
                            return null;

                        try
                        {
                            return (long) big;
                        }

                        catch
                        {
                            return null;
                        }
                    }

                    double? difficulty = null;
                    var difficultyHex = result["difficulty"]?.Value<string>();
                    if(TryParseHexBigInteger(difficultyHex, out var difficultyBig))
                    {
                        try
                        {
                            var difficultyDouble = (double) difficultyBig;
                            difficulty = double.IsFinite(difficultyDouble) ? difficultyDouble : null;
                        }

                        catch
                        {
                            difficulty = null;
                        }
                    }

                    DateTime? timestamp = null;
                    var timestampHex = result["timestamp"]?.Value<string>();
                    if(TryParseHexBigInteger(timestampHex, out var timestampBig) && timestampBig >= 0 && timestampBig <= long.MaxValue)
                    {
                        try
                        {
                            timestamp = DateTimeOffset.FromUnixTimeSeconds((long) timestampBig).UtcDateTime;
                        }

                        catch
                        {
                            // ignored
                        }
                    }

                    int? txCount = null;
                    if(result["transactions"] is JArray txs)
                        txCount = txs.Count;

                    var miner = result["miner"]?.Value<string>();
                    var gasUsed = TryParseHexLong(result["gasUsed"]?.Value<string>());
                    var gasLimit = TryParseHexLong(result["gasLimit"]?.Value<string>());
                    var baseFeePerGas = TryParseHexLong(result["baseFeePerGas"]?.Value<string>());
                    var sizeBytes = TryParseHexLong(result["size"]?.Value<string>());

                    return new EthereumBlockMeta(difficulty, timestamp, txCount, miner, gasUsed, gasLimit, baseFeePerGas, sizeBytes);
                }

                catch(Exception ex)
                {
                    logger.Warn(ex, () => $"Error fetching eth block meta for height {blockHeight}");
                    return null;
                }
            }) ?? new EthereumBlockMeta(null, null, null, null, null, null, null, null);
        }

        catch(Exception ex)
        {
            logger.Warn(ex, () => $"Error caching eth block meta for height {blockHeight}");
            return new EthereumBlockMeta(null, null, null, null, null, null, null, null);
        }
    }
}
