-- 04-delete-soft-deleted-pages.sql — Hard-delete all pages where PageDeleted=1,
-- with cascade to paragraphs and grid rows.
--
-- Swift 2.2 has ~238 soft-deleted pages that persist as tombstones. Most live
-- in Areas 25/26/27 and overlap with 03-delete-orphan-areas.sql; this script
-- catches any that remained after 03.

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRAN;

DECLARE @victims TABLE (PageId INT PRIMARY KEY);
INSERT INTO @victims SELECT PageId FROM Page WHERE PageDeleted = 1;

DECLARE @victim_count INT = (SELECT COUNT(*) FROM @victims);
PRINT 'Soft-deleted pages to hard-delete: ' + CAST(@victim_count AS NVARCHAR(10));

IF NOT EXISTS (SELECT 1 FROM @victims)
BEGIN
    PRINT 'No soft-deleted pages remain — nothing to do.';
    COMMIT TRAN;
    RETURN;
END

DELETE p FROM Paragraph p INNER JOIN @victims v ON p.ParagraphPageId = v.PageId;
PRINT 'Paragraphs deleted: ' + CAST(@@ROWCOUNT AS NVARCHAR(10));

DELETE g FROM GridRow g INNER JOIN @victims v ON g.GridRowPageId = v.PageId;
PRINT 'GridRows deleted: ' + CAST(@@ROWCOUNT AS NVARCHAR(10));

DELETE p FROM Page p INNER JOIN @victims v ON p.PageId = v.PageId;
PRINT 'Pages deleted: ' + CAST(@@ROWCOUNT AS NVARCHAR(10));

-- Verify
IF EXISTS (SELECT 1 FROM Page WHERE PageDeleted = 1)
BEGIN
    RAISERROR('Soft-deleted pages still present after delete — rolling back', 16, 1);
    ROLLBACK TRAN;
    RETURN;
END

COMMIT TRAN;
PRINT 'Done — soft-deleted pages removed.';
