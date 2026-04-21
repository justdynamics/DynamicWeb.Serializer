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

IF COL_LENGTH('Page', 'PageLayout') IS NOT NULL
    UPDATE [Page]
       SET PageLayout = NULL
     WHERE CAST(PageLayout AS NVARCHAR(MAX)) LIKE '%Swift-v2_PageNoLayout.cshtml%';

IF COL_LENGTH('Page', 'PageMasterPage') IS NOT NULL
    UPDATE [Page]
       SET PageMasterPage = NULL
     WHERE CAST(PageMasterPage AS NVARCHAR(MAX)) LIKE '%Swift-v2_PageNoLayout.cshtml%';

-- Paragraph.ItemType references (grid-row templates point at ItemType records,
-- not raw cshtml, but scan as a safety net):
IF COL_LENGTH('Paragraph', 'ParagraphItemType') IS NOT NULL
    UPDATE [Paragraph]
       SET ParagraphItemType = NULL
     WHERE ParagraphItemType IN ('1ColumnEmail', '2ColumnsEmail');

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
IF COL_LENGTH('Page', 'PageLayout') IS NOT NULL
    SELECT 'Page.PageLayout' AS Loc, COUNT(*) AS remaining
      FROM [Page] WHERE CAST(PageLayout AS NVARCHAR(MAX)) LIKE '%Swift-v2_PageNoLayout.cshtml%';
IF COL_LENGTH('Page', 'PageMasterPage') IS NOT NULL
    SELECT 'Page.PageMasterPage' AS Loc, COUNT(*) AS remaining
      FROM [Page] WHERE CAST(PageMasterPage AS NVARCHAR(MAX)) LIKE '%Swift-v2_PageNoLayout.cshtml%';
IF COL_LENGTH('Paragraph', 'ParagraphItemType') IS NOT NULL
    SELECT 'Paragraph.ParagraphItemType' AS Loc, COUNT(*) AS remaining
      FROM [Paragraph] WHERE ParagraphItemType IN ('1ColumnEmail', '2ColumnsEmail');

COMMIT TRAN;
PRINT '';
PRINT 'Done — 05-null-stale-template-refs.sql';
