-- 02-delete-test-page.sql — Hard-delete Page 8451 "New Serialized Page" + its
-- subtree. Test artifact from prior serializer development (2026-04-03 per
-- docs/baselines/Swift2.2-baseline.md). Not part of the Swift 2.2 template.
--
-- This walks PageParentPageId recursively so any accidental children are caught.

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRAN;

-- Collect the subtree
DECLARE @victims TABLE (PageId INT PRIMARY KEY);
;WITH subtree AS (
    SELECT PageId FROM Page WHERE PageId = 8451
    UNION ALL
    SELECT p.PageId FROM Page p INNER JOIN subtree s ON p.PageParentPageId = s.PageId
)
INSERT INTO @victims SELECT PageId FROM subtree
OPTION (MAXRECURSION 1000);

PRINT 'Pages to delete:';
SELECT p.PageId, p.PageParentPageId, p.PageMenuText FROM Page p INNER JOIN @victims v ON p.PageId = v.PageId ORDER BY p.PageId;

-- Cascade delete dependents
DELETE p FROM Paragraph p INNER JOIN @victims v ON p.ParagraphPageId = v.PageId;
PRINT 'Paragraphs deleted: ' + CAST(@@ROWCOUNT AS NVARCHAR(10));

DELETE g FROM GridRow g INNER JOIN @victims v ON g.GridRowPageId = v.PageId;
PRINT 'GridRows deleted: ' + CAST(@@ROWCOUNT AS NVARCHAR(10));

DELETE p FROM Page p INNER JOIN @victims v ON p.PageId = v.PageId;
PRINT 'Pages deleted: ' + CAST(@@ROWCOUNT AS NVARCHAR(10));

-- Verify
IF EXISTS (SELECT 1 FROM Page WHERE PageId = 8451)
BEGIN
    RAISERROR('Page 8451 still exists after delete — rolling back', 16, 1);
    ROLLBACK TRAN;
    RETURN;
END

COMMIT TRAN;
PRINT 'Done — /New Serialized Page subtree removed.';
