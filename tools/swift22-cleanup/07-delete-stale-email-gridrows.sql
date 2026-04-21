-- 07-delete-stale-email-gridrows.sql — Delete the 142 stale [GridRow] rows
-- whose GridRowDefinitionId references removed upstream Swift templates.
--
-- Context: script 05-null-stale-template-refs.sql set GridRowDefinitionId
-- to empty string for rows that previously pointed at '1ColumnEmail' and
-- '2ColumnsEmail'. GridRowDefinitionId is NOT NULL, so '' rows survive,
-- but on deserialize into CleanDB the NOT NULL constraint rejects them
-- (142 errors observed in Phase 38 E2E).
--
-- Fix: delete these stale rows from the source. They refer to templates
-- upstream Swift no longer ships; removing them is a source-data
-- correction, not a serializer behavior change.
--
-- Ordering: MUST run AFTER 05-null-stale-template-refs.sql (which nulls
-- the names to ''). If 05 has not run, the predicate won't match '' rows.
--
-- Closes Phase 38.1 GRID-01 (D-38.1-10..D-38.1-13).

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRAN;

PRINT '--- Before ---';
DECLARE @before INT = (
    SELECT COUNT(*) FROM [GridRow]
    WHERE GridRowDefinitionId IN ('', '1ColumnEmail', '2ColumnsEmail')
);
PRINT CONCAT('Stale-email GridRow rows before: ', @before);

IF @before <> 142
BEGIN
    PRINT CONCAT('ABORT: expected exactly 142 stale-email GridRow rows, found ', @before, '. Has script 05 run? Aborting without DELETE.');
    ROLLBACK TRAN;
    RETURN;
END

DELETE FROM [GridRow]
 WHERE GridRowDefinitionId IN ('', '1ColumnEmail', '2ColumnsEmail');

PRINT '--- After ---';
DECLARE @after INT = (
    SELECT COUNT(*) FROM [GridRow]
    WHERE GridRowDefinitionId IN ('', '1ColumnEmail', '2ColumnsEmail')
);
PRINT CONCAT('Stale-email GridRow rows after: ', @after);

IF @after <> 0
BEGIN
    PRINT 'ABORT: post-DELETE count should be 0; rolling back.';
    ROLLBACK TRAN;
    RETURN;
END

COMMIT TRAN;
PRINT 'Done — 07-delete-stale-email-gridrows.sql';
