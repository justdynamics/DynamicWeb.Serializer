-- 00-backup.sql — Snapshot tables mutated by 01-04 into *_BAK_YYYYMMDD clones.
-- Safe to re-run: drops prior same-day backup before re-snapshotting.

SET NOCOUNT ON;

DECLARE @stamp NVARCHAR(16) = CONVERT(NVARCHAR(8), GETDATE(), 112); -- YYYYMMDD
DECLARE @suffix NVARCHAR(32) = N'_BAK_' + @stamp;

DECLARE @tables TABLE (name SYSNAME);
INSERT INTO @tables(name) VALUES
    ('Page'), ('Paragraph'), ('GridRow'),
    ('ItemType_Swift-v2_Logo'),
    ('ItemType_Swift-v2_Text'),
    ('ItemType_Swift-v2_CustomerCenterApp'),
    ('ItemType_Swift-v2_EmailButton'),
    ('ItemType_Swift-v2_EmailIcon_Item'),
    ('ItemType_Swift-v2_EmailMenu_Item'),
    ('ItemType_Swift-v2_CheckoutApp'),
    ('ItemType_Swift-v2_Emails');

DECLARE @name SYSNAME, @bak SYSNAME, @sql NVARCHAR(MAX);
DECLARE c CURSOR LOCAL FOR SELECT name FROM @tables;
OPEN c;
FETCH NEXT FROM c INTO @name;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @bak = @name + @suffix;
    SET @sql = N'IF OBJECT_ID(' + QUOTENAME(@bak, '''') + N') IS NOT NULL DROP TABLE ' + QUOTENAME(@bak) + N';' +
               N'SELECT * INTO ' + QUOTENAME(@bak) + N' FROM ' + QUOTENAME(@name) + N';';
    PRINT N'Backing up ' + @name + N' -> ' + @bak;
    EXEC sp_executesql @sql;
    FETCH NEXT FROM c INTO @name;
END
CLOSE c;
DEALLOCATE c;

-- Report sizes for sanity
SELECT TABLE_NAME, (SELECT SUM(p.rows)
                    FROM sys.partitions p
                    WHERE p.object_id = OBJECT_ID(TABLE_NAME) AND p.index_id IN (0,1)) AS Rows
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_NAME LIKE '%' + @suffix
ORDER BY TABLE_NAME;
