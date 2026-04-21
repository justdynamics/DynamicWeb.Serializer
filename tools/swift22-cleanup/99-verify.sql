-- 99-verify.sql — Post-cleanup state check. Run after 01-04.

SET NOCOUNT ON;

PRINT '=== Remaining Default.aspx?ID refs to the 5 known-broken IDs ===';
DECLARE @sql NVARCHAR(MAX) = N'';
SELECT @sql = @sql + N'SELECT ''' + c.TABLE_NAME + N'.' + c.COLUMN_NAME + N''' AS Loc, COUNT(*) AS N FROM [' + c.TABLE_NAME + N']'
    + N' WHERE ['  + c.COLUMN_NAME + N'] LIKE ''%Default.aspx?%=8308%'''
    + N' OR  ['    + c.COLUMN_NAME + N'] LIKE ''%Default.aspx?%=149%'''
    + N' OR  ['    + c.COLUMN_NAME + N'] LIKE ''%Default.aspx?%=15717%'''
    + N' OR  ['    + c.COLUMN_NAME + N'] LIKE ''%Default.aspx?%=295%'''
    + N' OR  ['    + c.COLUMN_NAME + N'] LIKE ''%Default.aspx?%=140%'''
    + N' OR  ['    + c.COLUMN_NAME + N'] LIKE ''%"SelectedValue":"15717"%'''
    + N' HAVING COUNT(*) > 0 UNION ALL '
FROM INFORMATION_SCHEMA.COLUMNS c
WHERE c.TABLE_NAME LIKE 'ItemType_Swift-v2_%'
  AND c.TABLE_NAME NOT LIKE '%_BAK_%'
  AND c.DATA_TYPE IN ('nvarchar','ntext','varchar','nchar');
IF LEN(@sql) > 10
BEGIN
    SET @sql = LEFT(@sql, LEN(@sql) - 10);
    EXEC sp_executesql @sql;
END

PRINT '';
PRINT '=== Test-only pages ===';
SELECT PageId, PageMenuText FROM Page WHERE PageMenuText LIKE '%New Serialized%' OR PageId = 8451;

PRINT '';
PRINT '=== Pages in orphan areas (no Area row) ===';
SELECT p.PageAreaId, COUNT(*) AS Orphans
  FROM Page p
 WHERE p.PageAreaId NOT IN (SELECT AreaId FROM Area)
 GROUP BY p.PageAreaId;

PRINT '';
PRINT '=== Soft-deleted pages still present ===';
SELECT COUNT(*) AS SoftDeleted FROM Page WHERE PageDeleted = 1;

PRINT '';
PRINT '=== Summary row counts ===';
SELECT 'Page'            AS T, COUNT(*) AS N FROM Page
UNION ALL SELECT 'Paragraph',       COUNT(*) FROM Paragraph
UNION ALL SELECT 'GridRow',         COUNT(*) FROM GridRow
UNION ALL SELECT 'Area',            COUNT(*) FROM Area
UNION ALL SELECT 'EcomShops',       COUNT(*) FROM EcomShops
UNION ALL SELECT 'EcomProducts',    COUNT(*) FROM EcomProducts
UNION ALL SELECT 'UrlPath',         COUNT(*) FROM UrlPath;
