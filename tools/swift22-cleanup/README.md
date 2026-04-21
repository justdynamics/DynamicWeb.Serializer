# Swift 2.2 Data Cleanup Scripts

Re-runnable SQL scripts to clean up "obviously wrong" data in a Swift 2.2 DynamicWeb database before serializing it as a deployment baseline.

**Author:** 2026-04-21 autonomous session
**Target DB:** Any Swift 2.2 instance on SQL Server (tested on `localhost\SQLEXPRESS`, DB `Swift-2.2`)
**Source findings:** `.planning/sessions/2026-04-20-e2e-baseline-roundtrip/REPORT.md` — Phase 37 autonomous E2E round-trip surfaced these issues via `BaselineLinkSweeper`, `SqlIdentifierValidator`, and direct DB analysis.

## What these scripts do

| # | Script | Fixes |
|---|--------|-------|
| 00 | `00-backup.sql` | Snapshots every table mutated by 01-04 into `*_BAK_YYYYMMDD` tables in the same DB. Safe to re-run (overwrites prior backup). |
| 01 | `01-null-orphan-page-refs.sql` | Null out 77 paragraph/item-field references to 5 known-broken page IDs (8308, 149, 15717, 295, 140). Result: `BaselineLinkSweeper` no longer needs `AcknowledgedOrphanPageIds` to serialize this baseline. |
| 02 | `02-delete-test-page.sql` | Hard-delete page 8451 ("New Serialized Page") + its subtree — test artifact from prior serializer development. |
| 03 | `03-delete-orphan-areas.sql` | Delete 267 pages across 5 deleted areas (AreaIds 11, 12, 13, 25, 27) that no longer exist in the Area table. Non-renderable, invisible in admin UI. |
| 04 | `04-delete-soft-deleted-pages.sql` | Hard-delete all pages where `PageDeleted=1` along with their paragraph/grid-row children. Usually overlaps with 03; run both to be safe. |
| 05 | `05-null-stale-template-refs.sql` | Nulls paragraph/item-field references to 3 orphan template names (1ColumnEmail, 2ColumnsEmail, Swift-v2_PageNoLayout.cshtml) that no longer ship with upstream Swift. Closes Phase 38 D-38-06 (B.1/B.2). |
| 06 | `06-delete-orphan-ecomshopgrouprelation.sql` | Deletes the orphan `(ShopGroupShopId='SHOP19', ShopGroupGroupId='GROUP253')` row from `[EcomShopGroupRelation]`. SHOP19 is not a valid ShopId (only 9 shops exist: SHOP1/5/6/7/8/9/14/27/28). Transaction-wrapped; asserts `@before=1` and `@after=0`. Closes Phase 38 B.4 / Phase 38.1 B.4.1. |
| 07 | `07-delete-stale-email-gridrows.sql` | Deletes 142 stale GridRow rows whose `GridRowDefinitionId` was nulled to empty string by script 05 (references to removed templates `1ColumnEmail` / `2ColumnsEmail`). Transaction-wrapped; asserts `@before=142` and `@after=0`. Closes Phase 38.1 GRID-01. **Must run after 05.** |
| 08 | `08-null-orphan-page-link-refs.sql` | Nullifies/clears the 47 link-field occurrences of 20 orphan page IDs (1, 2, 4, 16, 19, 21, 23, 33, 34, 37, 40, 41, 42, 44, 48, 60, 97, 98, 104, 113) across `ItemType_Swift-v2_*` tables. These IDs point to non-existent pages and surface as `Unresolvable page ID <N>` strict-mode escalations during Deserialize Deploy. Dynamic-SQL sweep over `INFORMATION_SCHEMA.COLUMNS` (same pattern as script 01's ID-15717 cleanup) updates string columns via digit-boundary-guarded REPLACE, clears whole-value raw-numeric string matches to `''`, and sets nullable integer columns to `NULL`. Transaction-wrapped; asserts pre-count in `[1..200]` (with zero-count no-op branch) and `@after=0`. Closes Phase 38.1 VERIFICATION gap `truth[0]` (47 unresolvable page-ID link occurrences). **Must run after the baseline is restored from bacpac (or on a Swift-2.2 DB where the orphan rows still exist). Must run after 01 (overlap-safe but 01 clears a different 5-ID set first).** Investigation: `.planning/phases/38.1-close-phase-38-deferrals/38.1-02-orphan-investigation.md`. |
| 09 | `09-fix-misconfigured-property-pages.sql` | Clears dangling `PagePropertyItemId` on 10 misconfigured Page rows where `PagePropertyItemId` is set but `PagePropertyItemType` is empty — causing DW's PropertyItem loader to emit "Could not load PropertyItem for page <GUID>" on Deserialize Deploy. PageIDs: 88 (Navigation), 103 (Secondary Navigation), 106 (Contact), 107 (About), 108 (Terms), 109 (Delivery), 111 (Sign in), 116 (About us), 121 (Desktop Header), 122 (Mobile Header). Plan 38.1-02 Task 1 investigation discovered the misconfiguration extends from the plan's original 2-PageID scope (88, 103) to all 10 PropertyItem-warning pages (Rule-2 deviation D-38.1-02-01). UPDATE-only (pages are legitimate, only the property-item reference is bad); transaction-wrapped; asserts `@before=10` and `@after=0`. Closes Phase 38.1 VERIFICATION gap `truth[0]` structural-remnant portion. **Must run after the baseline is restored from bacpac.** Investigation: `.planning/phases/38.1-close-phase-38-deferrals/38.1-02-orphan-investigation.md`. |
| 99 | `99-verify.sql` | Row counts + re-scan for remaining orphan refs. Run after 01-09 to confirm clean state. |

## Expected Swift 2.2 "before" state

| Metric | Value |
|--------|-------|
| Orphan Default.aspx?ID refs | 77 occurrences across 8 ItemType_Swift-v2_* tables |
| Test-only page `/New Serialized Page` (8451) | 1 page + descendants |
| Pages in deleted areas (11,12,13,25,27) | ~267 |
| Soft-deleted pages (PageDeleted=1) | ~238 |
| Empty-name product translations (NOT cleaned) | 1091 — legitimate DW "not localized yet" state |
| Stale-email GridRow rows (`GridRowDefinitionId` IN '', '1ColumnEmail', '2ColumnsEmail') | 142 (after 05 runs) |
| Orphan page-ID link-field occurrences (20 IDs × varied rows) | ~47 occurrences across `ItemType_Swift-v2_*` (see `.planning/phases/38.1-close-phase-38-deferrals/38.1-02-orphan-investigation.md`) |

## Run order

```bash
# From a shell with sqlcmd on PATH
DB='Swift-2.2'                            # Target DB name
SERVER='localhost\SQLEXPRESS'              # SQL Server instance

sqlcmd -S "$SERVER" -E -d "$DB" -i tools/swift22-cleanup/00-backup.sql
sqlcmd -S "$SERVER" -E -d "$DB" -i tools/swift22-cleanup/01-null-orphan-page-refs.sql
sqlcmd -S "$SERVER" -E -d "$DB" -i tools/swift22-cleanup/02-delete-test-page.sql
sqlcmd -S "$SERVER" -E -d "$DB" -i tools/swift22-cleanup/03-delete-orphan-areas.sql
sqlcmd -S "$SERVER" -E -d "$DB" -i tools/swift22-cleanup/04-delete-soft-deleted-pages.sql
sqlcmd -S "$SERVER" -E -d "$DB" -i tools/swift22-cleanup/05-null-stale-template-refs.sql
sqlcmd -S "$SERVER" -E -d "$DB" -i tools/swift22-cleanup/06-delete-orphan-ecomshopgrouprelation.sql
sqlcmd -S "$SERVER" -E -d "$DB" -i tools/swift22-cleanup/07-delete-stale-email-gridrows.sql
sqlcmd -S "$SERVER" -E -d "$DB" -i tools/swift22-cleanup/08-null-orphan-page-link-refs.sql
sqlcmd -S "$SERVER" -E -d "$DB" -i tools/swift22-cleanup/09-fix-misconfigured-property-pages.sql
sqlcmd -S "$SERVER" -E -d "$DB" -i tools/swift22-cleanup/99-verify.sql
```

## Rollback

If anything goes wrong, restore from the `*_BAK_YYYYMMDD` tables created by `00-backup.sql`:

```sql
-- Example: rollback Paragraph table (find backup suffix from 00-backup output)
TRUNCATE TABLE Paragraph;
INSERT INTO Paragraph SELECT * FROM Paragraph_BAK_20260421;
```

## Not covered (intentional)

- **Empty-name product translations** (1091 rows) — real DW "not localized yet" state, NOT garbage
- **Area 26 "Digital Assets Portal"** — separate future baseline per user decision 2026-04-21
- **Duplicate page MenuTexts** — not true duplicates, just similar names in different subtrees
- **Env-specific config** (AreaDomain, GTM ID, CDN host, payment credentials) — handled by the env-bucket split, not this cleanup
