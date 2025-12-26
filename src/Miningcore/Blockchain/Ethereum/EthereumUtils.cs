namespace Miningcore.Blockchain.Ethereum;

public static class EthereumUtils
{
    public static void DetectNetworkAndChain(string netVersionResponse, string gethChainResponse,
        out EthereumNetworkType networkType, out GethChainType chainType)
    {
        // convert network
        if(int.TryParse(netVersionResponse, out var netWorkTypeInt))
        {
            networkType = (EthereumNetworkType) netWorkTypeInt;

            if(!Enum.IsDefined(typeof(EthereumNetworkType), networkType))
                networkType = EthereumNetworkType.Unknown;
        }

        else
            networkType = EthereumNetworkType.Unknown;

        // convert chain
        var chain = gethChainResponse;

        // Historically used by configs and other components as alias for Ethereum main chain
        if(string.Equals(chain, "Ethereum", StringComparison.OrdinalIgnoreCase))
            chain = nameof(GethChainType.Main);

        if(!Enum.TryParse(chain, true, out chainType))
        {
            chainType = GethChainType.Unknown;
        }

        if(chainType == GethChainType.Main)
            chainType = GethChainType.Main;

        if(chainType == GethChainType.Callisto)
            chainType = GethChainType.Callisto;
    }
}
