-- Deduplicate blocks by (poolid, blockheight) and prevent future duplicates.
--
-- Why: the default schema's UNIQUE(poolid, blockheight, type) allows duplicates when type is NULL
-- (Postgres treats NULLs as distinct for UNIQUE), which can break block/round calculations.
--
-- Safe to run multiple times.

-- 1) Delete duplicates, keeping the newest row by created (and id as tiebreaker)
WITH ranked AS (
    SELECT id,
           ROW_NUMBER() OVER (PARTITION BY poolid, blockheight ORDER BY created DESC, id DESC) AS rn
    FROM blocks
)
DELETE FROM blocks b
USING ranked r
WHERE b.id = r.id
  AND r.rn > 1;

-- 2) Add UNIQUE constraint to prevent future duplicates (required for INSERT .. ON CONFLICT on poolid+blockheight)
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conrelid = 'public.blocks'::regclass
          AND conname = 'uq_blocks_poolid_height'
    ) THEN
        ALTER TABLE public.blocks
            ADD CONSTRAINT uq_blocks_poolid_height UNIQUE (poolid, blockheight);
    END IF;
END $$;

