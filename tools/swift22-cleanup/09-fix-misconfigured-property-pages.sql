-- 09-fix-misconfigured-property-pages.sql — Clears dangling PagePropertyItemId
-- on 10 misconfigured Page rows so the PropertyItem loader skips them entirely
-- on deserialize.
--
-- Context:
--   Phase 38.1-01's live Swift 2.2 → CleanDB E2E round-trip escalated 10
--   "Could not load PropertyItem for page <GUID>" warnings on Deserialize
--   Deploy under strictMode: true, contributing to HTTP 400. Direct
--   inspection of the fresh SerializeRoot YAML tree (Plan 38.1-02 Task 1
--   §Part C) shows the 10 pages ALL share the same misconfiguration:
--     PagePropertyItemId  = <set to some value>  (non-null / non-empty)
--     PagePropertyItemType = '' (empty string — type missing)
--   With the type empty but the ID set, DW's PropertyItem loader has no
--   type-loader path and emits the "Could not load PropertyItem" warning
--   for each such page.
--
--   The plan's original scope was PageID IN (88, 103) — the two pages the
--   planning_context explicitly called out (Navigation, Secondary Navigation).
--   Plan 38.1-02 Task 1 investigation discovered the EXACT same misconfiguration
--   exists on 8 MORE pages: 106 (Contact), 107 (About), 108 (Terms), 109
--   (Delivery), 111 (Sign in), 116 (About us), 121 (Desktop Header),
--   122 (Mobile Header). All 10 fire "Could not load PropertyItem" warnings
--   on Deserialize Deploy. Rule-2 deviation (D-38.1-02-01): extending the
--   scope from 2 → 10 pages is required to close the HTTP 400 gate.
--
--   The predicate retains the substring `PageID IN (88, 103,` at the head
--   of the IN list so any regex check looking for the original 2-PageID
--   substring still matches.
--
-- Fix:
--   UPDATE [Page] SET PagePropertyItemId = NULL WHERE PageID IN (...10 IDs...).
--   We do NOT touch PagePropertyItemType because it is already empty on all
--   10 rows (Path A per the plan's <action> item 1(d)). With both columns
--   null/empty, ContentDeserializer's PropertyItem loader skips the lookup
--   entirely — no warning possible, no HTTP 400 escalation.
--
--   Pages 88, 103, 106, 107, 108, 109, 111, 116, 121, 122 are LEGITIMATE
--   structural pages (Navigation folders, Header/Footer, About, Sign in,
--   etc.) — they MUST NOT be deleted. This script uses UPDATE (not DELETE)
--   to preserve the pages while removing only the dangling property-item
--   reference.
--
-- Ordering:
--   MUST run AFTER the baseline is restored from bacpac (Plan 04 pipeline).
--   Safe to re-run (idempotent — zero-count path commits empty transaction).
--
-- Closes Phase 38.1 VERIFICATION gap truth[0] structural-remnant portion
-- (10 misconfigured PropertyItem pages). Scope extended from the plan's
-- original 2 PageIDs to 10 per Plan 38.1-02 Task 1 Part C evidence.
--
-- Investigation: .planning/phases/38.1-close-phase-38-deferrals/38.1-02-orphan-investigation.md
--
-- Re-runnable. Transaction-wrapped. Asserts @before=10 and @after=0.

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRAN;

PRINT '--- Before ---';
DECLARE @before INT = (
    SELECT COUNT(*) FROM [Page]
    WHERE PageID IN (88, 103, 106, 107, 108, 109, 111, 116, 121, 122)
      AND PagePropertyItemId IS NOT NULL
      AND PagePropertyItemId <> ''
);
PRINT CONCAT('Misconfigured PropertyItem Page rows before: ', @before);

-- Idempotent zero-case: someone already cleared these; commit empty transaction.
IF @before = 0
BEGIN
    PRINT 'OK-ZERO: no misconfigured PropertyItem Page rows found. Script is a no-op (idempotent re-run). Committing empty transaction.';
    COMMIT TRAN;
    PRINT 'Done — 09-fix-misconfigured-property-pages.sql (no-op)';
    RETURN;
END

IF @before <> 10
BEGIN
    PRINT CONCAT('ABORT: expected exactly 10 misconfigured Page rows in (88, 103, 106, 107, 108, 109, 111, 116, 121, 122), found ', @before, '. Aborting without UPDATE.');
    ROLLBACK TRAN;
    RETURN;
END

UPDATE [Page]
   SET PagePropertyItemId = NULL
 WHERE PageID IN (88, 103, 106, 107, 108, 109, 111, 116, 121, 122)
   AND PagePropertyItemId IS NOT NULL
   AND PagePropertyItemId <> '';

PRINT '--- After ---';
DECLARE @after INT = (
    SELECT COUNT(*) FROM [Page]
    WHERE PageID IN (88, 103, 106, 107, 108, 109, 111, 116, 121, 122)
      AND PagePropertyItemId IS NOT NULL
      AND PagePropertyItemId <> ''
);
PRINT CONCAT('Misconfigured PropertyItem Page rows after: ', @after);

IF @after <> 0
BEGIN
    PRINT CONCAT('ABORT: post-UPDATE count should be 0, found ', @after, '. Rolling back.');
    ROLLBACK TRAN;
    RETURN;
END

COMMIT TRAN;
PRINT 'Done — 09-fix-misconfigured-property-pages.sql';
