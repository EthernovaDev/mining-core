using System.Data;
using AutoMapper;
using Dapper;
using Miningcore.Persistence.Model;
using Miningcore.Persistence.Repositories;

namespace Miningcore.Persistence.Postgres.Repositories;

public class BlockRepository : IBlockRepository
{
    public BlockRepository(IMapper mapper)
    {
        this.mapper = mapper;
    }

    private readonly IMapper mapper;

    public async Task InsertAsync(IDbConnection con, IDbTransaction tx, Block block)
    {
        var mapped = mapper.Map<Entities.Block>(block);

        const string query =
            @"INSERT INTO blocks(poolid, blockheight, networkdifficulty, status, type, transactionconfirmationdata,
                miner, reward, effort, confirmationprogress, source, hash, created)
            VALUES(@poolid, @blockheight, @networkdifficulty, @status, @type, @transactionconfirmationdata,
                @miner, @reward, @effort, @confirmationprogress, @source, @hash, @created)
            ON CONFLICT (poolid, blockheight) DO UPDATE SET
                networkdifficulty = EXCLUDED.networkdifficulty,
                status = CASE
                    WHEN blocks.status IN ('confirmed', 'orphaned') THEN blocks.status
                    ELSE EXCLUDED.status
                END,
                type = COALESCE(EXCLUDED.type, blocks.type),
                transactionconfirmationdata = CASE
                    WHEN EXCLUDED.transactionconfirmationdata <> '' THEN EXCLUDED.transactionconfirmationdata
                    ELSE blocks.transactionconfirmationdata
                END,
                miner = COALESCE(EXCLUDED.miner, blocks.miner),
                reward = COALESCE(EXCLUDED.reward, blocks.reward),
                effort = COALESCE(EXCLUDED.effort, blocks.effort),
                confirmationprogress = GREATEST(blocks.confirmationprogress, EXCLUDED.confirmationprogress),
                source = COALESCE(EXCLUDED.source, blocks.source),
                hash = COALESCE(NULLIF(EXCLUDED.hash, ''), blocks.hash),
                created = GREATEST(blocks.created, EXCLUDED.created)";

        await con.ExecuteAsync(query, mapped, tx);
    }

    public async Task DeleteBlockAsync(IDbConnection con, IDbTransaction tx, Block block)
    {
        const string query = "DELETE FROM blocks WHERE id = @id";
        await con.ExecuteAsync(query, block, tx);
    }

    public async Task UpdateBlockAsync(IDbConnection con, IDbTransaction tx, Block block)
    {
        var mapped = mapper.Map<Entities.Block>(block);

        const string query = @"UPDATE blocks SET blockheight = @blockheight, status = @status, type = @type,
            reward = @reward, effort = COALESCE(@effort, effort), confirmationprogress = @confirmationprogress, hash = @hash WHERE id = @id";

        await con.ExecuteAsync(query, mapped, tx);
    }

    public async Task<Block[]> PageBlocksAsync(IDbConnection con, string poolId, BlockStatus[] status,
        int page, int pageSize, CancellationToken ct)
    {
        const string query = @"SELECT * FROM blocks WHERE poolid = @poolid AND status = ANY(@status)
            ORDER BY created DESC OFFSET @offset FETCH NEXT @pageSize ROWS ONLY";

        return (await con.QueryAsync<Entities.Block>(new CommandDefinition(query, new
        {
            poolId,
            status = status.Select(x => x.ToString().ToLower()).ToArray(),
            offset = page * pageSize,
            pageSize
        }, cancellationToken: ct)))
            .Select(mapper.Map<Block>)
            .ToArray();
    }

    public async Task<Block[]> PageBlocksWithEffortAsync(IDbConnection con, string poolId, BlockStatus[] status,
        int page, int pageSize, CancellationToken ct)
    {
        const string query = @"
WITH dedup AS (
    SELECT b.*,
           ROW_NUMBER() OVER (PARTITION BY b.poolid, b.blockheight ORDER BY b.created DESC, b.id DESC) AS rn
    FROM blocks b
    WHERE b.poolid = @poolId
      AND b.status = ANY(@status)
),
blocks_unique AS (
    SELECT *
    FROM dedup
    WHERE rn = 1
),
paged AS (
    SELECT *
    FROM blocks_unique
    ORDER BY created DESC, id DESC
    OFFSET @offset
    LIMIT @pageSizePlusOne
),
with_prev AS (
    SELECT p.*,
           LEAD(p.created) OVER (ORDER BY p.created DESC, p.id DESC) AS prev_created
    FROM paged p
)
SELECT
    w.id,
    w.poolid,
    w.blockheight,
    w.networkdifficulty,
    w.status,
    w.type,
    w.confirmationprogress,
    COALESCE(
        CASE
            WHEN w.prev_created IS NULL THEN NULL
            WHEN w.networkdifficulty IS NULL OR w.networkdifficulty <= 0 THEN NULL
            ELSE (
                SELECT SUM(s.difficulty)
                FROM shares s
                WHERE s.poolid = w.poolid
                  AND s.created > w.prev_created
                  AND s.created <= w.created
            ) / w.networkdifficulty
        END,
        w.effort,
        0
    ) AS effort,
    w.transactionconfirmationdata,
    w.miner,
    w.reward,
    w.source,
    w.hash,
    w.created
FROM with_prev w
ORDER BY w.created DESC, w.id DESC
LIMIT @pageSize;";

        return (await con.QueryAsync<Entities.Block>(new CommandDefinition(query, new
        {
            poolId,
            status = status.Select(x => x.ToString().ToLower()).ToArray(),
            offset = page * pageSize,
            pageSize,
            pageSizePlusOne = pageSize + 1,
        }, cancellationToken: ct)))
            .Select(mapper.Map<Block>)
            .ToArray();
    }

    public async Task<Block[]> PageBlocksAsync(IDbConnection con, BlockStatus[] status, int page, int pageSize, CancellationToken ct)
    {
        const string query = @"SELECT * FROM blocks WHERE status = ANY(@status)
            ORDER BY created DESC OFFSET @offset FETCH NEXT @pageSize ROWS ONLY";

        return (await con.QueryAsync<Entities.Block>(new CommandDefinition(query, new
        {
            status = status.Select(x => x.ToString().ToLower()).ToArray(),
            offset = page * pageSize,
            pageSize
        }, cancellationToken: ct)))
            .Select(mapper.Map<Block>)
            .ToArray();
    }

    public async Task<Block[]> GetPendingBlocksForPoolAsync(IDbConnection con, string poolId)
    {
        const string query = @"SELECT * FROM blocks WHERE poolid = @poolid AND status = @status";

        return (await con.QueryAsync<Entities.Block>(query, new { status = BlockStatus.Pending.ToString().ToLower(), poolid = poolId }))
            .Select(mapper.Map<Block>)
            .ToArray();
    }

    public async Task<Block> GetBlockBeforeAsync(IDbConnection con, string poolId, BlockStatus[] status, DateTime before)
    {
        const string query = @"SELECT * FROM blocks WHERE poolid = @poolid AND status = ANY(@status) AND created < @before
            ORDER BY created DESC FETCH NEXT 1 ROWS ONLY";

        return (await con.QueryAsync<Entities.Block>(query, new
        {
            poolId,
            before,
            status = status.Select(x => x.ToString().ToLower()).ToArray()
        }))
            .Select(mapper.Map<Block>)
            .FirstOrDefault();
    }

    public Task<uint> GetPoolBlockCountAsync(IDbConnection con, string poolId, CancellationToken ct)
    {
        const string query = @"SELECT COUNT(*) FROM blocks WHERE poolid = @poolId";

        return con.ExecuteScalarAsync<uint>(new CommandDefinition(query, new { poolId }, cancellationToken: ct));
    }

    public Task<DateTime?> GetLastPoolBlockTimeAsync(IDbConnection con, string poolId)
    {
        const string query = @"SELECT created FROM blocks WHERE poolid = @poolId ORDER BY created DESC LIMIT 1";

        return con.ExecuteScalarAsync<DateTime?>(query, new { poolId });
    }

    public async Task<Block> GetBlockByHeightAsync(IDbConnection con, string poolId, long height)
    {
        const string query = @"SELECT * FROM blocks WHERE poolid = @poolId AND blockheight = @height";

        var entity = await con.QuerySingleOrDefaultAsync<Entities.Block>(new CommandDefinition(query, new { poolId, height }));

        return entity == null ? null : mapper.Map<Block>(entity);
    }
}
