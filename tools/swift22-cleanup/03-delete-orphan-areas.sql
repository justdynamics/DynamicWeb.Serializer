-- 03-delete-orphan-areas.sql — Delete pages + paragraphs + grid-rows whose
-- PageAreaId points to an Area that no longer exists in the Area table.
--
-- Swift 2.2 has 267 such pages across AreaIds 11, 12, 13, 25, 27 — the Areas
-- were deleted but their pages remained. Documented in
-- docs/baselines/Swift2.2-baseline.md "Swift 2.2 contamination problem".

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRAN;

-- Identify orphan Areas (referenced by Page rows but missing from Area table)
DECLARE @orphan_areas TABLE (AreaId INT PRIMARY KEY);
INSERT INTO @orphan_areas
SELECT DISTINCT p.PageAreaId
  FROM Page p
 WHERE p.PageAreaId NOT IN (SELECT AreaId FROM Area);

PRINT 'Orphan AreaIds (no row in Area table):';
SELECT AreaId FROM @orphan_areas ORDER BY AreaId;

IF NOT EXISTS (SELECT 1 FROM @orphan_areas)
BEGIN
    PRINT 'No orphan areas found — nothing to delete.';
    COMMIT TRAN;
    RETURN;
END

-- Collect victim pages (all rows in orphan areas)
DECLARE @victims TABLE (PageId INT PRIMARY KEY);
INSERT INTO @victims
SELECT p.PageId FROM Page p INNER JOIN @orphan_areas oa ON p.PageAreaId = oa.AreaId;

DECLARE @victim_count INT = (SELECT COUNT(*) FROM @victims);
PRINT 'Pages to delete: ' + CAST(@victim_count AS NVARCHAR(10));

DELETE p FROM Paragraph p INNER JOIN @victims v ON p.ParagraphPageId = v.PageId;
PRINT 'Paragraphs deleted: ' + CAST(@@ROWCOUNT AS NVARCHAR(10));

DELETE g FROM GridRow g INNER JOIN @victims v ON g.GridRowPageId = v.PageId;
PRINT 'GridRows deleted: ' + CAST(@@ROWCOUNT AS NVARCHAR(10));

DELETE p FROM Page p INNER JOIN @victims v ON p.PageId = v.PageId;
PRINT 'Pages deleted: ' + CAST(@@ROWCOUNT AS NVARCHAR(10));

-- Verify
IF EXISTS (SELECT 1 FROM Page p WHERE p.PageAreaId NOT IN (SELECT AreaId FROM Area))
BEGIN
    RAISERROR('Orphan pages still present after delete — rolling back', 16, 1);
    ROLLBACK TRAN;
    RETURN;
END

COMMIT TRAN;
PRINT 'Done — orphan-area pages removed.';
