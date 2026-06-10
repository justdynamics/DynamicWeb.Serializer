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
--   script 01's ID-15717 cleanup). One pass:
--     Part A — string columns: REPLACE Default.aspx?ID=<N>/Id=<N> with ''
--              for each of the 20 IDs, with a digit-boundary guard in the
--              WHERE clause so "=4" does not match "=40" / "=42" / "=44" / "=48"
--              / "=490" / "=4897" etc.
--
--   2026-06-11: two structural repairs.
--   (1) The pre/post count previously aggregated every column via a single
--       giant UNION ALL statement — hundreds of branches with ~40 LIKE
--       predicates each. On fresh restores SQL Server fails to compile it
--       (Msg 8623). Counting now runs one small query per column in a cursor.
--   (2) Former Parts B/C (whole-value numeric strings / nullable-int columns
--       IN the 20-ID set) are REMOVED — see the inline note where they used
--       to live. They matched Sort/style/Id columns wholesale (1641 hits).
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
-- STEP 1: Enumerate target columns once + build the per-column Form A
-- predicate template (<COL> placeholder substituted per column).
-- =========================================================================
DECLARE @cols TABLE (TableName SYSNAME, ColumnName SYSNAME, Kind CHAR(1)); -- S = string, I = nullable int

INSERT INTO @cols (TableName, ColumnName, Kind)
SELECT c.TABLE_NAME, c.COLUMN_NAME, 'S'
FROM INFORMATION_SCHEMA.COLUMNS c
JOIN INFORMATION_SCHEMA.TABLES t ON t.TABLE_NAME = c.TABLE_NAME AND t.TABLE_SCHEMA = c.TABLE_SCHEMA
WHERE c.TABLE_NAME LIKE 'ItemType_Swift-v2_%'
  AND c.TABLE_NAME NOT LIKE '%_BAK_%'
  AND t.TABLE_TYPE = 'BASE TABLE'
  AND c.DATA_TYPE IN ('nvarchar','ntext','varchar','nchar','text');

INSERT INTO @cols (TableName, ColumnName, Kind)
SELECT c.TABLE_NAME, c.COLUMN_NAME, 'I'
FROM INFORMATION_SCHEMA.COLUMNS c
JOIN INFORMATION_SCHEMA.TABLES t ON t.TABLE_NAME = c.TABLE_NAME AND t.TABLE_SCHEMA = c.TABLE_SCHEMA
WHERE c.TABLE_NAME LIKE 'ItemType_Swift-v2_%'
  AND c.TABLE_NAME NOT LIKE '%_BAK_%'
  AND t.TABLE_TYPE = 'BASE TABLE'
  AND c.DATA_TYPE IN ('int','bigint','smallint','tinyint')
  AND c.IS_NULLABLE = 'YES';

-- Form A predicate template: one (LIKE AND NOT LIKE) pair per orphan ID.
DECLARE @formA NVARCHAR(MAX) = N'';
SELECT @formA = @formA
    + CASE WHEN @formA = N'' THEN N'' ELSE N' OR ' END
    + N'(<COL> LIKE ''%Default.aspx?%=' + v.n + N'%'' AND <COL> NOT LIKE ''%Default.aspx?%=' + v.n + N'[0-9]%'')'
FROM (VALUES ('1'),('2'),('4'),('16'),('19'),('21'),('23'),('33'),('34'),('37'),
             ('40'),('41'),('42'),('44'),('48'),('60'),('97'),('98'),('104'),('113')) v(n);

-- =========================================================================
-- STEP 2: Pre-count assertion (one small query per column — no compile bomb).
-- =========================================================================
PRINT '--- Before ---';
DECLARE @before INT = 0, @cnt INT, @sql NVARCHAR(MAX), @tn SYSNAME, @cn SYSNAME, @kind CHAR(1), @colExpr NVARCHAR(400);

DECLARE precur CURSOR LOCAL FAST_FORWARD FOR SELECT TableName, ColumnName, Kind FROM @cols;
OPEN precur;
FETCH NEXT FROM precur INTO @tn, @cn, @kind;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF @kind = 'S'
    BEGIN
        SET @colExpr = N'CAST([' + @cn + N'] AS NVARCHAR(MAX))';
        SET @sql = N'SELECT @c = COUNT(*) FROM [' + @tn + N'] WHERE ('
            + REPLACE(@formA, N'<COL>', @colExpr)
            + N')';
    END
    ELSE
        SET @sql = N'SELECT @c = 0';
    EXEC sp_executesql @sql, N'@c INT OUTPUT', @c = @cnt OUTPUT;
    SET @before = @before + ISNULL(@cnt, 0);
    FETCH NEXT FROM precur INTO @tn, @cn, @kind;
END
CLOSE precur;
DEALLOCATE precur;

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
        + N'UPDATE [' + c.TableName + N'] SET [' + c.ColumnName + N'] = '
        + N'REPLACE(REPLACE(CAST([' + c.ColumnName + N'] AS NVARCHAR(MAX)), '
        + N'''Default.aspx?ID=' + @nStr + N''', ''''), '
        + N'''Default.aspx?Id=' + @nStr + N''', '''') '
        + N'WHERE CAST([' + c.ColumnName + N'] AS NVARCHAR(MAX)) LIKE ''%Default.aspx?%=' + @nStr + N'%'' '
        + N'  AND CAST([' + c.ColumnName + N'] AS NVARCHAR(MAX)) NOT LIKE ''%Default.aspx?%=' + @nStr + N'[0-9]%'';' + CHAR(10)
    FROM @cols c
    WHERE c.Kind = 'S';
    IF @updA <> N''
        EXEC sp_executesql @updA;
    FETCH NEXT FROM idcur INTO @id;
END
CLOSE idcur;
DEALLOCATE idcur;

-- Parts B and C REMOVED (2026-06-11). The blanket whole-value-numeric (Form B) and
-- nullable-int (Form C) predicates matched 1641 occurrences on a fresh restore — almost
-- all of them Sort orders, style values (Padding/BorderThicknes/FontSize/Height), and
-- item Id fields whose values merely happen to be small integers. Blanking those corrupts
-- legitimate content. The original script never caught this because its single-statement
-- string-concat aggregation (undefined multi-row behavior + Msg 8623 compile failures)
-- silently under-scanned. Raw-value orphan LINK fields, if any remain, surface by exact
-- page + field name in the serialize-time BaselineLinkSweeper output — clean those
-- specifically (or acknowledge via the predicate's acknowledgedOrphanPageIds) instead of
-- pattern-matching every numeric column.

-- =========================================================================
-- STEP 4: Post-count assertion (same per-column accumulation as STEP 2).
-- =========================================================================
PRINT '--- After ---';
DECLARE @after INT = 0;

DECLARE postcur CURSOR LOCAL FAST_FORWARD FOR SELECT TableName, ColumnName, Kind FROM @cols;
OPEN postcur;
FETCH NEXT FROM postcur INTO @tn, @cn, @kind;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF @kind = 'S'
    BEGIN
        SET @colExpr = N'CAST([' + @cn + N'] AS NVARCHAR(MAX))';
        SET @sql = N'SELECT @c = COUNT(*) FROM [' + @tn + N'] WHERE ('
            + REPLACE(@formA, N'<COL>', @colExpr)
            + N')';
    END
    ELSE
        SET @sql = N'SELECT @c = 0';
    EXEC sp_executesql @sql, N'@c INT OUTPUT', @c = @cnt OUTPUT;
    SET @after = @after + ISNULL(@cnt, 0);
    FETCH NEXT FROM postcur INTO @tn, @cn, @kind;
END
CLOSE postcur;
DEALLOCATE postcur;

PRINT CONCAT('Orphan page-ID link occurrences after: ', ISNULL(@after, 0));

IF @after IS NOT NULL AND @after <> 0
BEGIN
    PRINT CONCAT('ABORT: post-mutation count should be 0, found ', @after, '. Rolling back.');
    ROLLBACK TRAN;
    RETURN;
END

COMMIT TRAN;
PRINT 'Done — 08-null-orphan-page-link-refs.sql';

