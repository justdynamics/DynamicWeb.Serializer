-- 08-null-orphan-page-link-refs.sql — Nullifies/clears the 47 link-field
-- occurrences of 20 orphan page IDs across ItemType_Swift-v2_* per-ItemType
-- tables.
--
-- Context:
--   Phase 38.1-01's live Swift 2.2 → CleanDB E2E round-trip escalated 47
--   "Unresolvable page ID <N> in link" warnings on Deserialize Deploy under
--   strictMode: true, causing HTTP 400. The 20 distinct orphan IDs:
--     1, 2, 4, 16, 19, 21, 23, 33, 34, 37, 40, 41, 42, 44, 48, 60, 97, 98, 104, 113
--   These IDs point to pages that do not exist in source.
--
--   Per InternalLinkResolver.cs:118, the "Unresolvable page ID" warning
--   fires from the Default.aspx?ID=<N> regex branch. DW's Item.SerializeTo()
--   transforms raw-integer link-typed column values into Default.aspx?ID=<N>
--   strings in-memory at deserialize time — which is why the baseline YAML
--   tree itself contains ZERO literal Default.aspx?ID=<orphan> matches. The
--   orphan IDs live as RAW values in ItemType_Swift-v2_* per-ItemType tables:
--     Form A — string columns containing "Default.aspx?ID=<N>" HTML/JSON fragments.
--     Form B — string columns storing the raw integer as a quoted string ("98").
--     Form C — nullable integer columns storing the raw integer directly (98).
--
-- Fix:
--   Dynamic-SQL sweep over INFORMATION_SCHEMA.COLUMNS (same pattern as
--   script 01's ID-15717 cleanup). Three passes:
--     Part A — string columns: REPLACE Default.aspx?ID=<N>/Id=<N> with ''
--              for each of the 20 IDs, with a digit-boundary guard in the
--              WHERE clause so "=4" does not match "=40" / "=42" / "=44" / "=48"
--              / "=490" / "=4897" etc.
--     Part B — string columns: SET column = '' when the whole value (after
--              NULLIF-empty) parses as an INT in the 20-ID set.
--     Part C — nullable integer columns: SET column = NULL when value IN 20-ID set.
--
-- Ordering:
--   MUST run AFTER the baseline is restored from bacpac (Plan 04 pipeline).
--   MUST run AFTER 01-null-orphan-page-refs.sql (different 5 orphan IDs, overlap-safe).
--   Safe to re-run (idempotent — zero-count path commits empty transaction).
--
-- Closes Phase 38.1 VERIFICATION gap truth[0] (47 unresolvable page-ID
-- occurrences escalation on Deserialize Deploy).
--
-- Investigation: .planning/phases/38.1-close-phase-38-deferrals/38.1-02-orphan-investigation.md
--
-- Re-runnable. Transaction-wrapped. Asserts count > 0 (with zero-no-op branch)
-- and <= 200 before mutation; asserts 0 after mutation.

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRAN;

DECLARE @idList NVARCHAR(MAX) = N'1, 2, 4, 16, 19, 21, 23, 33, 34, 37, 40, 41, 42, 44, 48, 60, 97, 98, 104, 113';

-- =========================================================================
-- STEP 1: Build the count query used for pre- and post-assertions.
-- The query aggregates residual orphan occurrences across all three forms.
-- =========================================================================
DECLARE @countSql NVARCHAR(MAX) = N'SELECT @total = SUM(n) FROM (SELECT 0 AS n ';

-- Form A + Form B aggregated on string columns (one SELECT per column)
SELECT @countSql = @countSql
    + N' UNION ALL SELECT COUNT(*) FROM [' + c.TABLE_NAME + N'] WHERE '
    -- Form A: Default.aspx?ID=<any of 20> with non-digit tail guard
    + N'('
        + N'(CAST([' + c.COLUMN_NAME + N'] AS NVARCHAR(MAX)) LIKE ''%Default.aspx?%=1%''    AND CAST([' + c.COLUMN_NAME + N'] AS NVARCHAR(MAX)) NOT LIKE ''%Default.aspx?%=1[0-9]%'')'
        + N' OR (CAST([' + c.COLUMN_NAME + N'] AS NVARCHAR(MAX)) LIKE ''%Default.aspx?%=2%''   AND CAST([' + c.COLUMN_NAME + N'] AS NVARCHAR(MAX)) NOT LIKE ''%Default.aspx?%=2[0-9]%'')'
        + N' OR (CAST([' + c.COLUMN_NAME + N'] AS NVARCHAR(MAX)) LIKE ''%Default.aspx?%=4%''   AND CAST([' + c.COLUMN_NAME + N'] AS NVARCHAR(MAX)) NOT LIKE ''%Default.aspx?%=4[0-9]%'')'
        + N' OR (CAST([' + c.COLUMN_NAME + N'] AS NVARCHAR(MAX)) LIKE ''%Default.aspx?%=16%''  AND CAST([' + c.COLUMN_NAME + N'] AS NVARCHAR(MAX)) NOT LIKE ''%Default.aspx?%=16[0-9]%'')'
        + N' OR (CAST([' + c.COLUMN_NAME + N'] AS NVARCHAR(MAX)) LIKE ''%Default.aspx?%=19%''  AND CAST([' + c.COLUMN_NAME + N'] AS NVARCHAR(MAX)) NOT LIKE ''%Default.aspx?%=19[0-9]%'')'
        + N' OR (CAST([' + c.COLUMN_NAME + N'] AS NVARCHAR(MAX)) LIKE ''%Default.aspx?%=21%''  AND CAST([' + c.COLUMN_NAME + N'] AS NVARCHAR(MAX)) NOT LIKE ''%Default.aspx?%=21[0-9]%'')'
        + N' OR (CAST([' + c.COLUMN_NAME + N'] AS NVARCHAR(MAX)) LIKE ''%Default.aspx?%=23%''  AND CAST([' + c.COLUMN_NAME + N'] AS NVARCHAR(MAX)) NOT LIKE ''%Default.aspx?%=23[0-9]%'')'
        + N' OR (CAST([' + c.COLUMN_NAME + N'] AS NVARCHAR(MAX)) LIKE ''%Default.aspx?%=33%''  AND CAST([' + c.COLUMN_NAME + N'] AS NVARCHAR(MAX)) NOT LIKE ''%Default.aspx?%=33[0-9]%'')'
        + N' OR (CAST([' + c.COLUMN_NAME + N'] AS NVARCHAR(MAX)) LIKE ''%Default.aspx?%=34%''  AND CAST([' + c.COLUMN_NAME + N'] AS NVARCHAR(MAX)) NOT LIKE ''%Default.aspx?%=34[0-9]%'')'
        + N' OR (CAST([' + c.COLUMN_NAME + N'] AS NVARCHAR(MAX)) LIKE ''%Default.aspx?%=37%''  AND CAST([' + c.COLUMN_NAME + N'] AS NVARCHAR(MAX)) NOT LIKE ''%Default.aspx?%=37[0-9]%'')'
        + N' OR (CAST([' + c.COLUMN_NAME + N'] AS NVARCHAR(MAX)) LIKE ''%Default.aspx?%=40%''  AND CAST([' + c.COLUMN_NAME + N'] AS NVARCHAR(MAX)) NOT LIKE ''%Default.aspx?%=40[0-9]%'')'
        + N' OR (CAST([' + c.COLUMN_NAME + N'] AS NVARCHAR(MAX)) LIKE ''%Default.aspx?%=41%''  AND CAST([' + c.COLUMN_NAME + N'] AS NVARCHAR(MAX)) NOT LIKE ''%Default.aspx?%=41[0-9]%'')'
        + N' OR (CAST([' + c.COLUMN_NAME + N'] AS NVARCHAR(MAX)) LIKE ''%Default.aspx?%=42%''  AND CAST([' + c.COLUMN_NAME + N'] AS NVARCHAR(MAX)) NOT LIKE ''%Default.aspx?%=42[0-9]%'')'
        + N' OR (CAST([' + c.COLUMN_NAME + N'] AS NVARCHAR(MAX)) LIKE ''%Default.aspx?%=44%''  AND CAST([' + c.COLUMN_NAME + N'] AS NVARCHAR(MAX)) NOT LIKE ''%Default.aspx?%=44[0-9]%'')'
        + N' OR (CAST([' + c.COLUMN_NAME + N'] AS NVARCHAR(MAX)) LIKE ''%Default.aspx?%=48%''  AND CAST([' + c.COLUMN_NAME + N'] AS NVARCHAR(MAX)) NOT LIKE ''%Default.aspx?%=48[0-9]%'')'
        + N' OR (CAST([' + c.COLUMN_NAME + N'] AS NVARCHAR(MAX)) LIKE ''%Default.aspx?%=60%''  AND CAST([' + c.COLUMN_NAME + N'] AS NVARCHAR(MAX)) NOT LIKE ''%Default.aspx?%=60[0-9]%'')'
        + N' OR (CAST([' + c.COLUMN_NAME + N'] AS NVARCHAR(MAX)) LIKE ''%Default.aspx?%=97%''  AND CAST([' + c.COLUMN_NAME + N'] AS NVARCHAR(MAX)) NOT LIKE ''%Default.aspx?%=97[0-9]%'')'
        + N' OR (CAST([' + c.COLUMN_NAME + N'] AS NVARCHAR(MAX)) LIKE ''%Default.aspx?%=98%''  AND CAST([' + c.COLUMN_NAME + N'] AS NVARCHAR(MAX)) NOT LIKE ''%Default.aspx?%=98[0-9]%'')'
        + N' OR (CAST([' + c.COLUMN_NAME + N'] AS NVARCHAR(MAX)) LIKE ''%Default.aspx?%=104%'' AND CAST([' + c.COLUMN_NAME + N'] AS NVARCHAR(MAX)) NOT LIKE ''%Default.aspx?%=104[0-9]%'')'
        + N' OR (CAST([' + c.COLUMN_NAME + N'] AS NVARCHAR(MAX)) LIKE ''%Default.aspx?%=113%'' AND CAST([' + c.COLUMN_NAME + N'] AS NVARCHAR(MAX)) NOT LIKE ''%Default.aspx?%=113[0-9]%'')'
    -- Form B: raw-numeric-string storage
    + N' OR TRY_CAST(NULLIF(CAST([' + c.COLUMN_NAME + N'] AS NVARCHAR(4000)), '''') AS INT) IN (' + @idList + N')'
    + N')'
FROM INFORMATION_SCHEMA.COLUMNS c
JOIN INFORMATION_SCHEMA.TABLES t ON t.TABLE_NAME = c.TABLE_NAME AND t.TABLE_SCHEMA = c.TABLE_SCHEMA
WHERE c.TABLE_NAME LIKE 'ItemType_Swift-v2_%'
  AND c.TABLE_NAME NOT LIKE '%_BAK_%'
  AND t.TABLE_TYPE = 'BASE TABLE'
  AND c.DATA_TYPE IN ('nvarchar','ntext','varchar','nchar','text');

-- Form C on nullable integer columns
SELECT @countSql = @countSql
    + N' UNION ALL SELECT COUNT(*) FROM [' + c.TABLE_NAME + N'] '
    + N'WHERE [' + c.COLUMN_NAME + N'] IN (' + @idList + N')'
FROM INFORMATION_SCHEMA.COLUMNS c
JOIN INFORMATION_SCHEMA.TABLES t ON t.TABLE_NAME = c.TABLE_NAME AND t.TABLE_SCHEMA = c.TABLE_SCHEMA
WHERE c.TABLE_NAME LIKE 'ItemType_Swift-v2_%'
  AND c.TABLE_NAME NOT LIKE '%_BAK_%'
  AND t.TABLE_TYPE = 'BASE TABLE'
  AND c.DATA_TYPE IN ('int','bigint','smallint','tinyint')
  AND c.IS_NULLABLE = 'YES';

SET @countSql = @countSql + N') q;';

-- =========================================================================
-- STEP 2: Pre-count assertion.
-- =========================================================================
PRINT '--- Before ---';
DECLARE @before INT;
EXEC sp_executesql @countSql, N'@total INT OUTPUT', @total = @before OUTPUT;
PRINT CONCAT('Orphan page-ID link occurrences before: ', ISNULL(@before, 0));

IF @before IS NULL OR @before = 0
BEGIN
    PRINT 'OK-ZERO: no orphan page-ID occurrences found. Script is a no-op (idempotent re-run or 01-07 already cleaned). Committing empty transaction.';
    COMMIT TRAN;
    PRINT 'Done — 08-null-orphan-page-link-refs.sql (no-op)';
    RETURN;
END

IF @before > 200
BEGIN
    PRINT CONCAT('ABORT: expected orphan-occurrence count in range [1..200], found ', @before, '. Predicate may be matching unintended columns; aborting without mutation.');
    ROLLBACK TRAN;
    RETURN;
END

-- =========================================================================
-- STEP 3: Execute per-column UPDATE statements for all three forms.
-- =========================================================================

-- Part A — string columns: per-ID REPLACE with digit-boundary guard in WHERE.
PRINT '--- Part A: string-column REPLACE for Default.aspx?ID=<N> forms ---';
DECLARE @updA NVARCHAR(MAX) = N'';
DECLARE @id INT;
DECLARE idcur CURSOR LOCAL FAST_FORWARD FOR
    SELECT v FROM (VALUES (1),(2),(4),(16),(19),(21),(23),(33),(34),(37),(40),(41),(42),(44),(48),(60),(97),(98),(104),(113)) x(v);
OPEN idcur;
FETCH NEXT FROM idcur INTO @id;
WHILE @@FETCH_STATUS = 0
BEGIN
    DECLARE @nStr NVARCHAR(10) = CAST(@id AS NVARCHAR(10));
    SET @updA = N'';
    SELECT @updA = @updA
        + N'UPDATE [' + c.TABLE_NAME + N'] SET [' + c.COLUMN_NAME + N'] = '
        + N'REPLACE(REPLACE(CAST([' + c.COLUMN_NAME + N'] AS NVARCHAR(MAX)), '
        + N'''Default.aspx?ID=' + @nStr + N''', ''''), '
        + N'''Default.aspx?Id=' + @nStr + N''', '''') '
        + N'WHERE CAST([' + c.COLUMN_NAME + N'] AS NVARCHAR(MAX)) LIKE ''%Default.aspx?%=' + @nStr + N'%'' '
        + N'  AND CAST([' + c.COLUMN_NAME + N'] AS NVARCHAR(MAX)) NOT LIKE ''%Default.aspx?%=' + @nStr + N'[0-9]%'';' + CHAR(10)
    FROM INFORMATION_SCHEMA.COLUMNS c
    JOIN INFORMATION_SCHEMA.TABLES t ON t.TABLE_NAME = c.TABLE_NAME AND t.TABLE_SCHEMA = c.TABLE_SCHEMA
    WHERE c.TABLE_NAME LIKE 'ItemType_Swift-v2_%'
      AND c.TABLE_NAME NOT LIKE '%_BAK_%'
      AND t.TABLE_TYPE = 'BASE TABLE'
      AND c.DATA_TYPE IN ('nvarchar','ntext','varchar','nchar','text');
    IF @updA <> N''
        EXEC sp_executesql @updA;
    FETCH NEXT FROM idcur INTO @id;
END
CLOSE idcur;
DEALLOCATE idcur;

-- Part B — string columns: clear whole-value raw-numeric matches to ''.
PRINT '--- Part B: string-column clear-to-empty for raw-numeric orphan IDs ---';
DECLARE @updB NVARCHAR(MAX) = N'';
SELECT @updB = @updB
    + N'UPDATE [' + c.TABLE_NAME + N'] SET [' + c.COLUMN_NAME + N'] = '''' '
    + N'WHERE TRY_CAST(NULLIF(CAST([' + c.COLUMN_NAME + N'] AS NVARCHAR(4000)), '''') AS INT) IN (' + @idList + N');' + CHAR(10)
FROM INFORMATION_SCHEMA.COLUMNS c
JOIN INFORMATION_SCHEMA.TABLES t ON t.TABLE_NAME = c.TABLE_NAME AND t.TABLE_SCHEMA = c.TABLE_SCHEMA
WHERE c.TABLE_NAME LIKE 'ItemType_Swift-v2_%'
  AND c.TABLE_NAME NOT LIKE '%_BAK_%'
  AND t.TABLE_TYPE = 'BASE TABLE'
  AND c.DATA_TYPE IN ('nvarchar','varchar','nchar','text','ntext');
IF @updB <> N''
    EXEC sp_executesql @updB;

-- Part C — nullable integer columns: SET NULL.
PRINT '--- Part C: integer-column SET NULL for orphan IDs ---';
DECLARE @updC NVARCHAR(MAX) = N'';
SELECT @updC = @updC
    + N'UPDATE [' + c.TABLE_NAME + N'] SET [' + c.COLUMN_NAME + N'] = NULL '
    + N'WHERE [' + c.COLUMN_NAME + N'] IN (' + @idList + N');' + CHAR(10)
FROM INFORMATION_SCHEMA.COLUMNS c
JOIN INFORMATION_SCHEMA.TABLES t ON t.TABLE_NAME = c.TABLE_NAME AND t.TABLE_SCHEMA = c.TABLE_SCHEMA
WHERE c.TABLE_NAME LIKE 'ItemType_Swift-v2_%'
  AND c.TABLE_NAME NOT LIKE '%_BAK_%'
  AND t.TABLE_TYPE = 'BASE TABLE'
  AND c.DATA_TYPE IN ('int','bigint','smallint','tinyint')
  AND c.IS_NULLABLE = 'YES';
IF @updC <> N''
    EXEC sp_executesql @updC;

-- =========================================================================
-- STEP 4: Post-count assertion.
-- =========================================================================
PRINT '--- After ---';
DECLARE @after INT;
EXEC sp_executesql @countSql, N'@total INT OUTPUT', @total = @after OUTPUT;
PRINT CONCAT('Orphan page-ID link occurrences after: ', ISNULL(@after, 0));

IF @after IS NOT NULL AND @after <> 0
BEGIN
    PRINT CONCAT('ABORT: post-mutation count should be 0, found ', @after, '. Rolling back.');
    ROLLBACK TRAN;
    RETURN;
END

COMMIT TRAN;
PRINT 'Done — 08-null-orphan-page-link-refs.sql';
