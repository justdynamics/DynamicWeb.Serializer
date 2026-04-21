-- 05-null-stale-template-refs.sql — Null out references to 3 stale template
-- names that no longer ship with upstream Swift (confirmed via
-- https://github.com/dynamicweb/Swift on 2026-04-21).
--
-- Targets:
--   1ColumnEmail                  — grid-row template, removed from upstream Swift
--   2ColumnsEmail                 — grid-row template, removed from upstream Swift
--   Swift-v2_PageNoLayout.cshtml  — page-layout template, removed from upstream Swift
--
-- These references in the Swift 2.2 source DB cause TemplateAssetManifest
-- validation warnings during serialize. Since upstream does NOT ship them,
-- we null out the references rather than expand TEMPLATE-01 scope.
-- Closes Phase 38 D-38-06 (B.1/B.2).
--
-- Re-runnable. Wraps in a transaction. Prints summary counts.
--
-- Safety posture (per RESEARCH §Security Domain / T-38-B12-01):
--   - Template names are HARDCODED (no user input)
--   - Bracket-escaped identifiers via [<col>]
--   - BEGIN TRAN / COMMIT TRAN with SET XACT_ABORT ON
--   - Excludes *_BAK_* backup tables (mirrors 99-verify.sql pattern)

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRAN;

PRINT '=== Before cleanup — reference counts (ItemType_Swift-v2_* columns) ===';
DECLARE @preCountSql NVARCHAR(MAX) = N'';
SELECT @preCountSql = @preCountSql
    + N'SELECT ''' + c.TABLE_NAME + N'.' + c.COLUMN_NAME + N''' AS location, COUNT(*) AS ref_count '
    + N'FROM [' + c.TABLE_NAME + N'] '
    + N'WHERE CAST([' + c.COLUMN_NAME + N'] AS NVARCHAR(MAX)) LIKE ''%1ColumnEmail%'' '
    + N'OR CAST([' + c.COLUMN_NAME + N'] AS NVARCHAR(MAX)) LIKE ''%2ColumnsEmail%'' '
    + N'OR CAST([' + c.COLUMN_NAME + N'] AS NVARCHAR(MAX)) LIKE ''%Swift-v2_PageNoLayout.cshtml%'' '
    + N'HAVING COUNT(*) > 0 '
    + N'UNION ALL ' + CHAR(10)
FROM INFORMATION_SCHEMA.COLUMNS c
WHERE c.TABLE_NAME LIKE 'ItemType_Swift-v2_%'
  AND c.TABLE_NAME NOT LIKE '%_BAK_%'
  AND c.DATA_TYPE IN ('nvarchar', 'ntext', 'varchar', 'nchar');

IF LEN(@preCountSql) > 10
BEGIN
    SET @preCountSql = LEFT(@preCountSql, LEN(@preCountSql) - LEN(N'UNION ALL ' + CHAR(10)));
    EXEC sp_executesql @preCountSql;
END
ELSE
    PRINT 'No ItemType_Swift-v2_* string columns found — skipping pre-count';

PRINT '';
PRINT '=== Running cleanup (null matching rows in ItemType_Swift-v2_*) ===';
DECLARE @sql NVARCHAR(MAX) = N'';
SELECT @sql = @sql
    + N'UPDATE [' + c.TABLE_NAME + N'] SET [' + c.COLUMN_NAME + N'] = '''' '
    + N'WHERE CAST([' + c.COLUMN_NAME + N'] AS NVARCHAR(MAX)) LIKE ''%1ColumnEmail%'' '
    + N'OR CAST([' + c.COLUMN_NAME + N'] AS NVARCHAR(MAX)) LIKE ''%2ColumnsEmail%'' '
    + N'OR CAST([' + c.COLUMN_NAME + N'] AS NVARCHAR(MAX)) LIKE ''%Swift-v2_PageNoLayout.cshtml%'';' + CHAR(10)
FROM INFORMATION_SCHEMA.COLUMNS c
WHERE c.TABLE_NAME LIKE 'ItemType_Swift-v2_%'
  AND c.TABLE_NAME NOT LIKE '%_BAK_%'
  AND c.DATA_TYPE IN ('nvarchar', 'ntext', 'varchar', 'nchar');

IF LEN(@sql) > 0
    EXEC sp_executesql @sql;
ELSE
    PRINT 'No ItemType_Swift-v2_* string columns to update — scan empty';

PRINT '';
PRINT '=== Extended scan — non-ItemType locations (Page.Layout, Paragraph.ItemType) ===';
-- Per RESEARCH Assumption A3, these names may also live in Paragraph.ItemType
-- or Page.Layout / Page.Master columns (not just ItemType_Swift-v2_*).
-- Targeted updates on well-known DW columns:

-- Page.PageLayout: string path to the layout .cshtml — this is where
-- `Swift-v2_PageNoLayout.cshtml` actually lives on Swift 2.2.
-- Dynamic SQL so SQL Server does not compile-time-validate missing column
-- references across hosts with different schema versions.
IF COL_LENGTH('Page', 'PageLayout') IS NOT NULL
    EXEC sp_executesql N'UPDATE [Page]
       SET PageLayout = NULL
     WHERE CAST(PageLayout AS NVARCHAR(MAX)) LIKE ''%Swift-v2_PageNoLayout.cshtml%''';

-- Note: DW's Page.MasterPage link is stored as an INT FK column
-- `PageMasterPageId` (not a string template path). No string-column scan is
-- meaningful here. The originally planned `PageMasterPage` UPDATE has been
-- removed (Phase 38-03 Task 5 fix: Rule 1 — column never existed on any
-- supported DW schema, so the UPDATE was invalid SQL).

-- Paragraph.ItemType references (grid-row templates point at ItemType records,
-- not raw cshtml, but scan as a safety net):
IF COL_LENGTH('Paragraph', 'ParagraphItemType') IS NOT NULL
    EXEC sp_executesql N'UPDATE [Paragraph]
       SET ParagraphItemType = NULL
     WHERE ParagraphItemType IN (''1ColumnEmail'', ''2ColumnsEmail'')';

-- GridRow.GridRowDefinitionId — discovered during Phase 38-03 Task 5 E2E:
-- the actual storage location for `1ColumnEmail` and `2ColumnsEmail` grid-row
-- template references on Swift 2.2 (142 rows as of 2026-04-21).
-- Plan 38-03 Task 2 originally scanned only ItemType_Swift-v2_* + Page.PageLayout
-- + Paragraph.ParagraphItemType, missing this GridRow column.
-- Rule 1 extension added during live E2E run.
-- Note: GridRowDefinitionId is NOT NULL, so we set to '' rather than NULL.
-- Empty string is a valid no-template value for this column.
IF COL_LENGTH('GridRow', 'GridRowDefinitionId') IS NOT NULL
    EXEC sp_executesql N'UPDATE [GridRow]
       SET GridRowDefinitionId = ''''
     WHERE GridRowDefinitionId IN (''1ColumnEmail'', ''2ColumnsEmail'')';

PRINT '';
PRINT '=== Verify — expected 0 rows across ItemType_Swift-v2_* for each template name ===';
DECLARE @verifySql NVARCHAR(MAX) = N'';
SELECT @verifySql = @verifySql
    + N'SELECT ''' + c.TABLE_NAME + N'.' + c.COLUMN_NAME + N''' AS location, COUNT(*) AS remaining '
    + N'FROM [' + c.TABLE_NAME + N'] '
    + N'WHERE CAST([' + c.COLUMN_NAME + N'] AS NVARCHAR(MAX)) LIKE ''%1ColumnEmail%'' '
    + N'OR CAST([' + c.COLUMN_NAME + N'] AS NVARCHAR(MAX)) LIKE ''%2ColumnsEmail%'' '
    + N'OR CAST([' + c.COLUMN_NAME + N'] AS NVARCHAR(MAX)) LIKE ''%Swift-v2_PageNoLayout.cshtml%'' '
    + N'HAVING COUNT(*) > 0 '
    + N'UNION ALL ' + CHAR(10)
FROM INFORMATION_SCHEMA.COLUMNS c
WHERE c.TABLE_NAME LIKE 'ItemType_Swift-v2_%'
  AND c.TABLE_NAME NOT LIKE '%_BAK_%'
  AND c.DATA_TYPE IN ('nvarchar', 'ntext', 'varchar', 'nchar');

IF LEN(@verifySql) > 10
BEGIN
    SET @verifySql = LEFT(@verifySql, LEN(@verifySql) - LEN(N'UNION ALL ' + CHAR(10)));
    EXEC sp_executesql @verifySql;
END
ELSE
    PRINT 'No ItemType_Swift-v2_* string columns to verify — scan empty (unexpected on Swift 2.2)';

PRINT '';
PRINT '=== Non-ItemType verify (expected 0 each) ===';
-- Dynamic SQL so SQL Server does not compile-time-validate column refs
-- against the wrong schema version.
IF COL_LENGTH('Page', 'PageLayout') IS NOT NULL
    EXEC sp_executesql N'SELECT ''Page.PageLayout'' AS Loc, COUNT(*) AS remaining
      FROM [Page] WHERE CAST(PageLayout AS NVARCHAR(MAX)) LIKE ''%Swift-v2_PageNoLayout.cshtml%''';

-- Page.MasterPage link is an INT FK (`PageMasterPageId`), never a cshtml path.
-- No string-column verify is meaningful.

IF COL_LENGTH('Paragraph', 'ParagraphItemType') IS NOT NULL
    EXEC sp_executesql N'SELECT ''Paragraph.ParagraphItemType'' AS Loc, COUNT(*) AS remaining
      FROM [Paragraph] WHERE ParagraphItemType IN (''1ColumnEmail'', ''2ColumnsEmail'')';

IF COL_LENGTH('GridRow', 'GridRowDefinitionId') IS NOT NULL
    EXEC sp_executesql N'SELECT ''GridRow.GridRowDefinitionId'' AS Loc, COUNT(*) AS remaining
      FROM [GridRow] WHERE GridRowDefinitionId IN (''1ColumnEmail'', ''2ColumnsEmail'')';

COMMIT TRAN;
PRINT '';
PRINT 'Done — 05-null-stale-template-refs.sql';
