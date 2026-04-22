# Swift v2 Reference Baseline — Cleanup Overview

**Status:** operational reference — cleanup surface closed end-to-end by Phase 38.1-04 pipeline run (2026-04-22)
**Observed in:** Swift 2.2 reference install (DW 10.23.9, Swift-v2 design/ItemType set)
**Observed on:** 2026-04-17 .. 2026-04-22 (Phase 37 E2E round-trip, Phase 38 retroactive hardening, Phase 38.1 deferral closure, Phase 38.1 gap closure)
**Runtime impact:** none — site renders correctly in all observed states
**Observability impact:** strict-mode (`strictMode: true`) baseline round-trip escalates any uncleaned item; the cleanup-script family clears the canonical surface
**Proof of closure:** `.planning/phases/38.1-close-phase-38-deferrals/38.1-02-e2e-results.md` (disposition `CLOSED`, HTTP 200 × 4, EcomProducts 2051 → 2051, 88 pages × 2xx smoke)
**Canonical pipeline:** `tools/e2e/full-clean-roundtrip.ps1`
**Canonical bacpac:** `tools/swift2.2.0-20260129-database.zip` (Swift 2.2 captured 2026-01-29)

---

## Summary

The Swift 2.2 reference install carries a set of data-hygiene items that never
surface in normal admin operation but do surface when running a strict-mode
baseline round-trip (Swift-2.2 → YAML → Swift-CleanDB). This document
inventories every cleanup item applied to that baseline, grouped by category,
with per-entry: what the problem is, how to detect it, which script fixes it,
which E2E gate it unblocks, and where in the source DB it lives.

Two classes of cleanup emerged across Phase 37, Phase 38, and Phase 38.1:

1. **Canonical bacpac-start cleanup chain** — scripts `00` through `07` plus
   `cleandb-align-schema.sql`. These are what a fresh-from-bacpac round-trip
   needs. The Phase 38.1-04 pipeline run on the pristine 2026-01-29 bacpac
   reached disposition `CLOSED` with exactly this chain.
2. **Defensive operational tooling for non-pristine state** — scripts `08` and
   `09`. These exist to clean accumulated live-dev drift (orphan page-ID link
   references and misconfigured property pages that arise from live user edits
   between bacpac snapshots). Against the pristine bacpac they are **no-ops by
   construction** (pre-count = 0, scripts commit empty transaction and exit
   `OK-ZERO`). Run them when the round-trip is executed against a live or
   drifted database.

A single autonomous pipeline at `tools/e2e/full-clean-roundtrip.ps1` applies
the full set against a freshly-restored Swift-2.2 bacpac and proves closure
end-to-end.

---

## Canonical test recipe

From repo root:

```powershell
pwsh tools/e2e/full-clean-roundtrip.ps1
```

The pipeline:

1. Stops both DW hosts (Swift 2.2 on `54035`, Swift-CleanDB on `58217`).
2. Auto-installs `sqlpackage` if absent (`dotnet tool install --global microsoft.sqlpackage`).
3. Drops and re-imports Swift-2.2 from `tools/swift2.2.0-20260129-database.zip`.
4. Applies cleanup scripts `00 → 09` (scripts `08` and `09` run as no-ops against the pristine bacpac — see §"Class B: defensive operational tooling" below).
5. Deploys `DynamicWeb.Serializer.dll` to both host bins (MD5-verified).
6. Boots the Swift-2.2 host, purges CleanDB, applies `cleandb-align-schema.sql`, boots the CleanDB host.
7. Runs the four API calls: `Serialize Deploy`, `Serialize Seed`, `Deserialize Deploy`, `Deserialize Seed` — each MUST return HTTP 200.
8. Mirrors Swift-2.2's `SerializeRoot` → CleanDB between the serialize and deserialize phases.
9. Runs the frontend smoke tool (`tools/smoke/Test-BaselineFrontend.ps1`) — exercises 88 active pages; all MUST return 2xx.
10. Asserts `EcomProducts` source = target (Swift-2.2 2051 → CleanDB 2051) and that `baselines/Swift2.2/_sql/EcomShopGroupRelation/GROUP253$$SHOP19.yml` is absent.
11. Stops both hosts and emits `summary.json` on full success.

**Evidence layout** per run:

```
.planning/phases/38.1-close-phase-38-deferrals/pipeline-runs/<yyyyMMdd-HHmmss>/
  pipeline.log
  bacpac-drop.log, bacpac-import.log
  cleanup-00-backup.log ... cleanup-09-fix-misconfigured-property-pages.log
  dotnet-build.log, dll-md5.txt
  host-swift22.log, host-cleandb.log
  purge-cleandb.log, schema-align.log
  serialize-deploy.log, serialize-seed.log
  deserialize-deploy.log, deserialize-seed.log
  smoke.log
  summary.json   (only on full success)
```

The proof-of-closure run is documented at
`.planning/phases/38.1-close-phase-38-deferrals/38.1-02-e2e-results.md`
(disposition `CLOSED`) with per-step logs in the adjacent `pipeline-runs/`
subdirectory.

---

## Class A: canonical bacpac-start cleanup chain

Scripts in this class ran successfully on the pristine 2026-01-29 bacpac in
the Phase 38.1-04 pipeline-runs/diagnostic/ evidence and were required to
reach `CLOSED`. Each entry gives: what the problem is, how to detect it,
which script fixes it, which E2E gate it unblocks, and where in the source
DB it lives.

### 1. FK orphans — EcomShopGroupRelation

- **What:** One row in `[EcomShopGroupRelation]` references a non-existent shop (`ShopGroupShopId = 'SHOP19'`, `ShopGroupGroupId = 'GROUP253'`). Swift 2.2 ships 9 shops only (`SHOP1 / 5 / 6 / 7 / 8 / 9 / 14 / 27 / 28`).
- **Detect:**
  ```sql
  SELECT COUNT(*) FROM EcomShopGroupRelation r
  WHERE NOT EXISTS (SELECT 1 FROM EcomShops s WHERE s.ShopId = r.ShopGroupShopId);
  -- Expected before: 1; after: 0
  ```
- **Fix:** `tools/swift22-cleanup/06-delete-orphan-ecomshopgrouprelation.sql` (transaction-wrapped; asserts `@before = 1` and `@after = 0`).
- **E2E gate unblocked:** `SqlTableProvider` FK re-enable on deserialize — without the fix, re-enabling the `EcomShopGroupRelation → EcomShops` FK fails.
- **DB location:** `[EcomShopGroupRelation]` table. Backup table `[EcomShopGroupRelation_BAK_YYYYMMDD]` created by script `00`.
- **Phase provenance:** Phase 38 B.4 surfaced the row; Phase 38.1 B.4.1 closed it.

### 2. Stale email GridRows

- **What:** 142 rows in `[GridRow]` reference email grid-row templates (`1ColumnEmail`, `2ColumnsEmail`) that upstream Swift no longer ships. Script `05` nulls the template-name references to the empty string `''`; script `07` then deletes the 142 rows whose `GridRowDefinitionId` is now empty.
- **Detect:**
  ```sql
  SELECT COUNT(*) FROM GridRow
  WHERE GridRowDefinitionId IN ('', '1ColumnEmail', '2ColumnsEmail');
  -- Expected after 05 runs, before 07 runs: 142; after 07 runs: 0
  ```
- **Fix:**
  - `tools/swift22-cleanup/05-null-stale-template-refs.sql` — nulls paragraph / item-field references to three orphan template names (`1ColumnEmail`, `2ColumnsEmail`, `Swift-v2_PageNoLayout.cshtml`) to `''`. Must run before 07.
  - `tools/swift22-cleanup/07-delete-stale-email-gridrows.sql` — deletes the 142 stale rows. Transaction-wrapped; asserts `@before = 142` and `@after = 0`.
- **E2E gate unblocked:** `ContentDeserializer` GridRow insert path — 142 NOT NULL constraint violations avoided (the target DB rejects empty-name GridRowDefinitionId on insert).
- **DB location:** `[GridRow]` table (`GridRowDefinitionId` column).
- **Phase provenance:** Phase 38 D-38-06 (B.1/B.2) nulled the template refs; Phase 38.1 GRID-01 added the row-deletion step.

### 3. Soft-deleted and test-artifact pages

- **What:** Three classes of non-renderable pages that still sit in the `[Page]` table:
  - **Test artifact:** page `8451` "New Serialized Page" (Phase 37's test artifact from prior serializer development) with its subtree.
  - **Orphan areas:** ~267 pages live under 5 `AreaID`s (`11`, `12`, `13`, `25`, `27`) whose rows no longer exist in `[Area]`.
  - **Soft-deleted:** ~238 pages with `PageDeleted = 1` plus their paragraph / grid-row children.
- **Detect:**
  ```sql
  -- Test artifact
  SELECT COUNT(*) FROM Page WHERE PageID = 8451;

  -- Orphan areas (pages under Areas that no longer exist)
  SELECT COUNT(*) FROM Page p
  WHERE NOT EXISTS (SELECT 1 FROM Area a WHERE a.AreaID = p.PageAreaID);

  -- Soft-deleted
  SELECT COUNT(*) FROM Page WHERE PageDeleted = 1;
  ```
- **Fix:**
  - `tools/swift22-cleanup/02-delete-test-page.sql` — PageID 8451 + subtree.
  - `tools/swift22-cleanup/03-delete-orphan-areas.sql` — 267 pages across the 5 deleted Areas.
  - `tools/swift22-cleanup/04-delete-soft-deleted-pages.sql` — all `PageDeleted = 1` rows + paragraph/grid-row children.
- **E2E gate unblocked:** Page / Area row-count fidelity on serialize; prevents non-renderable pages entering YAML and therefore failing downstream deserialize.
- **DB location:** `[Page]` table (and child `[Paragraph]`, `[GridRow]` rows via cascade in the scripts).

### 4. Known orphan page references (precedent class)

- **What:** Paragraph / ItemType field values referencing 5 page IDs that do not exist in `[Page]`: `8308`, `149`, `15717`, `295`, `140`. ID `8308` belongs to Area 26 "Digital Assets Portal" (a separate baseline); the other four are simply non-existent. Surfaces as literal `Default.aspx?ID=<N>` / `Default.aspx?Id=<N>` substrings in Swift-v2 ItemType string columns, and for ID `15717` as a `{"SelectedValue":"15717",...}` fragment inside ButtonEditor JSON.
- **Detect:**
  ```sql
  SELECT 'Logo.Link' AS Loc, COUNT(*) AS N FROM [ItemType_Swift-v2_Logo]                WHERE Link            LIKE '%Default.aspx?%=8308%'
  UNION ALL SELECT 'Text.Text',            COUNT(*)         FROM [ItemType_Swift-v2_Text]                WHERE Text            LIKE '%Default.aspx?%=8308%'
  UNION ALL SELECT '149/CustomerCenterApp',COUNT(*)         FROM [ItemType_Swift-v2_CustomerCenterApp]   WHERE ProductListPage LIKE '%Default.aspx?%=149%'
  UNION ALL SELECT '149/EmailButton',      COUNT(*)         FROM [ItemType_Swift-v2_EmailButton]         WHERE PageLink        LIKE '%Default.aspx?%=149%'
  UNION ALL SELECT '149/EmailIcon_Item',   COUNT(*)         FROM [ItemType_Swift-v2_EmailIcon_Item]      WHERE Link            LIKE '%Default.aspx?%=149%'
  UNION ALL SELECT '149/EmailMenu_Item',   COUNT(*)         FROM [ItemType_Swift-v2_EmailMenu_Item]      WHERE Link            LIKE '%Default.aspx?%=149%'
  -- ... (see 01-null-orphan-page-refs.sql for the full verify block)
  ```
- **Fix:** `tools/swift22-cleanup/01-null-orphan-page-refs.sql` — clears 77 occurrences across 8 `ItemType_Swift-v2_*` tables. Uses hardcoded `UPDATE` statements for the 4 simple IDs and a dynamic-SQL `INFORMATION_SCHEMA.COLUMNS` sweep for the `15717` ButtonEditor JSON form.
- **E2E gate unblocked:** `BaselineLinkSweeper.Sweep` serialize-time sweep (Phase 37 F-19) — removes 77 `Default.aspx?ID=N` orphan refs so serialize no longer needs `acknowledgedOrphanPageIds` for this baseline.
- **DB location:** `[ItemType_Swift-v2_Logo]` (`Link`), `[ItemType_Swift-v2_Text]` (`Text`), `[ItemType_Swift-v2_CustomerCenterApp]` (`ProductListPage`, `AddressesPage`, `AccountSettingsPage`), `[ItemType_Swift-v2_EmailButton]` (`PageLink`), `[ItemType_Swift-v2_EmailIcon_Item]` (`Link`), `[ItemType_Swift-v2_EmailMenu_Item]` (`Link`), `[ItemType_Swift-v2_CheckoutApp]` (`UserAddressesPageLink`, `UserAccountPageLink`), `[ItemType_Swift-v2_Emails]` (`Header`, `Footer`), plus any string column containing `15717` across `ItemType_Swift-v2_*` (discovered via the sweep at runtime).
- **Phase provenance:** Phase 37 2026-04-20 round-trip surfaced these via `BaselineLinkSweeper`. This script is also the **precedent for the defensive dynamic-SQL sweep pattern** that script `08` reuses.

### 5. Stale ItemType "Field for title" references

- **What:** 7 Swift-v2 ItemType definitions declare `fieldForTitle="Title"` on the `<item>` root element but do not include a matching `<field systemName="Title">` in their field collection. The admin UI renders "Field for title" as a stale reference; the ItemType still works (DW falls back to the item's display name).
- **Detect (XML filesystem, not DB):**
  ```bash
  cd <host>/wwwroot/Files/System/Items
  for f in ItemType_Swift-v2_*.xml; do
    if grep -q 'fieldForTitle="Title"' "$f" && ! grep -q '<field[^/]*systemName="Title"' "$f"; then
      echo "$f"
    fi
  done
  ```
  Expected output: 7 filenames — `Swift-v2_Button`, `Swift-v2_BreadcrumbNavigation`, `Swift-v2_ImpersonationBar`, `Swift-v2_MenuRelatedContent`, `Swift-v2_PageProperties`, `Swift-v2_PostList`, `Swift-v2_SearchField`.
- **Fix:** one-line XML edit per file (not yet scripted):
  ```diff
  -  <item ... systemName="Swift-v2_Button" ... fieldForTitle="Title" ...>
  +  <item ... systemName="Swift-v2_Button" ... fieldForTitle="" ...>
  ```
  See `docs/findings/swift22-stale-itemtype-title-references.md` §"Suggested remediation" for the canonical diff. This cleanup is by XML edit, not SQL; a future phase may automate it.
- **E2E gate:** non-breaking. The stale references produce visible admin-UI stale-reference display but do not cause strict-mode escalations on their own — they are data-hygiene items in the shipped Swift-v2 templates.
- **DB location:** `wwwroot/Files/System/Items/ItemType_Swift-v2_*.xml` on the host filesystem (NOT in the DB).
- **Status:** observed but not yet scripted. Detailed analysis in `docs/findings/swift22-stale-itemtype-title-references.md`.

### 6. Operational target-schema alignment

- **What:** Swift 2.2 ships 10 columns on `[Area]`, `[EcomGroups]`, `[EcomProducts]` that upstream DW NuGet 10.23.9 on CleanDB does not: `Area.AreaHtmlType`, `Area.AreaLayoutPhone`, `Area.AreaLayoutTablet`, `EcomGroups.GroupPageIDRel`, `EcomProducts.ProductPeriodId`, `EcomProducts.ProductVariantGroupCounter`, `EcomProducts.ProductPriceMatrixPeriod`, `EcomProducts.ProductOptimizedFor`, `EcomProducts.MyVolume`, `EcomProducts.MyDouble`. Without alignment, `TargetSchemaCache.LogMissingColumnOnce` emits 10 warnings per deserialize; strict mode escalates them all.
- **Detect:**
  ```sql
  -- Against CleanDB — expect 0 for each before alignment; NULL for each COL_LENGTH means missing
  SELECT 'Area.AreaHtmlType'                        AS Col, COL_LENGTH('Area',         'AreaHtmlType')
  UNION ALL SELECT 'Area.AreaLayoutPhone',                  COL_LENGTH('Area',         'AreaLayoutPhone')
  UNION ALL SELECT 'Area.AreaLayoutTablet',                 COL_LENGTH('Area',         'AreaLayoutTablet')
  UNION ALL SELECT 'EcomGroups.GroupPageIDRel',             COL_LENGTH('EcomGroups',   'GroupPageIDRel')
  UNION ALL SELECT 'EcomProducts.ProductPeriodId',          COL_LENGTH('EcomProducts', 'ProductPeriodId')
  UNION ALL SELECT 'EcomProducts.ProductVariantGroupCounter', COL_LENGTH('EcomProducts', 'ProductVariantGroupCounter')
  UNION ALL SELECT 'EcomProducts.ProductPriceMatrixPeriod', COL_LENGTH('EcomProducts', 'ProductPriceMatrixPeriod')
  UNION ALL SELECT 'EcomProducts.ProductOptimizedFor',      COL_LENGTH('EcomProducts', 'ProductOptimizedFor')
  UNION ALL SELECT 'EcomProducts.MyVolume',                 COL_LENGTH('EcomProducts', 'MyVolume')
  UNION ALL SELECT 'EcomProducts.MyDouble',                 COL_LENGTH('EcomProducts', 'MyDouble');
  ```
- **Fix:** `tools/swift22-cleanup/cleandb-align-schema.sql` — idempotent `IF COL_LENGTH IS NULL` guards, transaction-wrapped. Runs on CleanDB (the TARGET), not Swift-2.2 (the SOURCE).
- **E2E gate unblocked:** `TargetSchemaCache` warn-and-skip path — zero schema-drift warnings on CleanDB deserialize under strict mode.
- **DB location:** `[Area]`, `[EcomGroups]`, `[EcomProducts]` tables on Swift-CleanDB (the target).
- **Rationale source:** `docs/baselines/env-bucket.md` §"DW NuGet version alignment". This is a cross-environment deployment concern, not a Swift bug — align DW NuGet versions between source and target environments whenever possible.

---

## Class B: defensive operational tooling for non-pristine state

Scripts `08` and `09` were authored in Phase 38.1-02 in response to 57
escalations observed in the Phase 38.1-01 round-trip (2026-04-21) — 47
`Unresolvable page ID <N> in link` warnings across 20 distinct orphan IDs
and 10 `Could not load PropertyItem for page <GUID>` warnings. At the time,
the assumption was that these represented bacpac-level data-hygiene bugs.

When the Phase 38.1-04 pipeline restored the pristine 2026-01-29 bacpac,
direct SQL scans confirmed **zero occurrences** of any of the 20 orphan IDs
across all 131 `ItemType_Swift-v2_*` tables, across all nvarchar/varchar/
ntext/text columns of non-log tables, and across BAK snapshot tables. A
fresh serialize run against the bacpac produced **zero** `Default.aspx?ID=<orphan>`
references in YAML. Host startup also added zero rows.

The 47 orphan warnings therefore came from **~3 months of live user edits
between the bacpac date (2026-01-29) and the Phase 38.1-01 test run
(2026-04-21)** — page deletions, button-target changes, and similar mutations
that left dangling `Default.aspx?ID=N` references to pages that existed when
the button was authored but were later removed. The bacpac itself is clean
of this class.

Scripts `08` and `09` therefore serve as **defensive operational tooling**,
not mandatory bacpac cleanup. They remain in the cleanup chain because:

- If a future bacpac snapshot contains such accumulated edits, the scripts
  will find and clean them.
- If someone runs the round-trip against a live or drifted DB (not a fresh
  bacpac restore), the scripts close the gap those edits introduce.
- Both scripts are idempotent by design — zero-count pre-assertion commits
  an empty transaction with an `OK-ZERO` log line, so running them against
  a pristine bacpac is a safe no-op that the pipeline treats as success.

### 7. Accumulated live-dev orphan page link references (Script 08 territory)

- **What:** Paragraph / ItemType field values referencing 20 page IDs that were legitimate when authored but were later deleted: `1, 2, 4, 16, 19, 21, 23, 33, 34, 37, 40, 41, 42, 44, 48, 60, 97, 98, 104, 113`. Phase 38.1-01 observed **47 occurrences** of these IDs during Deserialize Deploy (the `Default.aspx?ID=<N>` pattern emerges at deserialize time when DW's `item.SerializeTo(fields)` transforms raw integer link-typed column values). These IDs live as RAW values in three forms:
  - **Form A** — string columns containing `Default.aspx?ID=<N>` HTML/JSON fragments.
  - **Form B** — string columns storing the raw integer as a quoted string (`"98"`).
  - **Form C** — nullable integer columns storing the raw integer directly (`98`).
- **Detect:** see the three-form predicate aggregation in `tools/swift22-cleanup/08-null-orphan-page-link-refs.sql` (Step 1, lines 53-106). The count query enumerates `INFORMATION_SCHEMA.COLUMNS` for `ItemType_Swift-v2_*` tables and aggregates residuals across all three forms with a digit-boundary guard (`NOT LIKE '%=N[0-9]%'` prevents `=4` matching `=40` / `=42` / `=44` / `=48` / `=490` / `=4897`).
- **Fix:** `tools/swift22-cleanup/08-null-orphan-page-link-refs.sql` — dynamic-SQL 3-pass sweep over `INFORMATION_SCHEMA.COLUMNS`. Part A does a boundary-guarded `REPLACE` on string columns; Part B clears whole-value raw-numeric string matches to `''`; Part C sets nullable integer columns to `NULL`. Transaction-wrapped; asserts pre-count in `[1..200]` with a zero-count no-op branch; asserts `@after = 0`.
- **E2E gate unblocked** (when the DB is non-pristine): `BaselineLinkSweeper` serialize-time sweep + strict-mode Deserialize Deploy HTTP 200.
- **DB location:** any `nvarchar/ntext/varchar/nchar/text` or nullable integer column across ~91 `ItemType_Swift-v2_*` per-ItemType tables. Detailed per-(Table, Column, ID) breakdown from the Phase 38.1-01 evidence is at `.planning/phases/38.1-close-phase-38-deferrals/38.1-02-orphan-investigation.md`.
- **Behaviour on pristine bacpac:** pre-count = 0; logs `OK-ZERO: no orphan page-ID occurrences found. Script is a no-op (idempotent re-run or 01-07 already cleaned). Committing empty transaction.` and exits with `Done — 08-null-orphan-page-link-refs.sql (no-op)`.

### 8. Misconfigured property pages (Script 09 territory)

- **What:** 10 `[Page]` rows where `PagePropertyItemId` is set but `PagePropertyItemType` is empty (`''` or `NULL`). DW's PropertyItem loader has no type-loader path and emits `Could not load PropertyItem for page <GUID>` under strict mode, usually as a downstream artifact of Issue 1 link-resolution failure upstream. PageIDs: `88` (Navigation), `103` (Secondary Navigation), `106` (Contact), `107` (About), `108` (Terms), `109` (Delivery), `111` (Sign in), `116` (About us), `121` (Desktop Header), `122` (Mobile Header).
- **Detect:**
  ```sql
  SELECT PageID, PageMenuText, PagePropertyItemId, PagePropertyItemType
  FROM [Page]
  WHERE PageID IN (88, 103, 106, 107, 108, 109, 111, 116, 121, 122)
    AND PagePropertyItemId IS NOT NULL
    AND PagePropertyItemId <> '';
  -- Expected on Phase 38.1-01 live state: 10 rows. On pristine bacpac: 0 rows.
  ```
- **Fix:** `tools/swift22-cleanup/09-fix-misconfigured-property-pages.sql` — single `UPDATE [Page] SET PagePropertyItemId = NULL WHERE PageID IN (...10 IDs...)`. UPDATE-only (not DELETE) — the pages are legitimate structural pages; only the property-item reference is bad. Transaction-wrapped; asserts `@before = 10` and `@after = 0` with a zero-count no-op branch.
- **E2E gate unblocked** (when the DB is non-pristine): PropertyItem loader on deserialize — removes dangling `PagePropertyItemId` refs so the loader skips the lookup entirely.
- **DB location:** `[Page]` table (`PagePropertyItemId`, `PagePropertyItemType` columns).
- **Behaviour on pristine bacpac:** pre-count = 0 (PropertyItem warnings are deserialize-time artifacts downstream of Issue 1; with zero Issue 1 escalations, Issue 2 does not manifest at all); logs `OK-ZERO: no misconfigured PropertyItem Page rows found. Script is a no-op (idempotent re-run). Committing empty transaction.` and exits with `Done — 09-fix-misconfigured-property-pages.sql (no-op)`.

---

## Ancillary scripts (operational support)

| #  | Script                                                            | Purpose |
|---:|-------------------------------------------------------------------|---------|
| 00 | `tools/swift22-cleanup/00-backup.sql`                             | Snapshots every table about to be mutated by `01`-`04` into `*_BAK_YYYYMMDD` tables in the same DB. Safe to re-run; overwrites prior backup. |
| 99 | `tools/swift22-cleanup/99-verify.sql`                             | Re-runs orphan scans and reports remaining rows for post-cleanup verification. Run after the full 00-09 + schema-align chain. |
| —  | `tools/purge-cleandb.sql`                                         | Purges CleanDB before a fresh deserialize (invoked by the pipeline at Step 8). Not part of the Swift-22 cleanup family; runs against the TARGET. |

---

## Full run order

Scripts run in numeric order against a fresh Swift-2.2 bacpac restore, then
the target-schema script runs against CleanDB. The autonomous pipeline at
`tools/e2e/full-clean-roundtrip.ps1` orchestrates this end-to-end.

```bash
# Source cleanup — against Swift-2.2
sqlcmd -S <server> -E -d Swift-2.2     -i tools/swift22-cleanup/00-backup.sql
sqlcmd -S <server> -E -d Swift-2.2     -i tools/swift22-cleanup/01-null-orphan-page-refs.sql
sqlcmd -S <server> -E -d Swift-2.2     -i tools/swift22-cleanup/02-delete-test-page.sql
sqlcmd -S <server> -E -d Swift-2.2     -i tools/swift22-cleanup/03-delete-orphan-areas.sql
sqlcmd -S <server> -E -d Swift-2.2     -i tools/swift22-cleanup/04-delete-soft-deleted-pages.sql
sqlcmd -S <server> -E -d Swift-2.2     -i tools/swift22-cleanup/05-null-stale-template-refs.sql
sqlcmd -S <server> -E -d Swift-2.2     -i tools/swift22-cleanup/06-delete-orphan-ecomshopgrouprelation.sql
sqlcmd -S <server> -E -d Swift-2.2     -i tools/swift22-cleanup/07-delete-stale-email-gridrows.sql
sqlcmd -S <server> -E -d Swift-2.2     -i tools/swift22-cleanup/08-null-orphan-page-link-refs.sql
sqlcmd -S <server> -E -d Swift-2.2     -i tools/swift22-cleanup/09-fix-misconfigured-property-pages.sql
sqlcmd -S <server> -E -d Swift-2.2     -i tools/swift22-cleanup/99-verify.sql

# Target schema alignment — against CleanDB
sqlcmd -S <server> -E -d Swift-CleanDB -i tools/swift22-cleanup/cleandb-align-schema.sql
```

Unattended path:

```powershell
pwsh tools/e2e/full-clean-roundtrip.ps1
```

---

## Rollback

If any Class-A mutation goes wrong, restore from the `*_BAK_YYYYMMDD`
tables created by `00-backup.sql`:

```sql
-- Example: rollback Paragraph table (find backup suffix from 00-backup output)
TRUNCATE TABLE Paragraph;
INSERT INTO Paragraph SELECT * FROM Paragraph_BAK_YYYYMMDD;
```

Class-B scripts `08` and `09` are both idempotent with post-count assertions
that automatically `ROLLBACK` on mismatch — no manual rollback needed.

---

## Not covered (intentional)

- **Empty-name product translations** (1091 rows) — real DW "not localized yet" state, NOT garbage.
- **Area 26 "Digital Assets Portal"** — separate future baseline per user decision 2026-04-21.
- **Duplicate page MenuTexts** — not true duplicates, just similar names in different subtrees.
- **Env-specific config** (AreaDomain, GTM ID, CDN host, payment credentials) — handled by the DEPLOYMENT / SEED / NOT-SERIALIZED env-bucket split in `docs/baselines/env-bucket.md`, not this cleanup family.

---

## What's next (advisory)

For anyone picking this up to refresh the baseline or investigate related
hygiene, the following items are observed but not yet scripted:

- **7 stale ItemType "Field for title" references** (Category 5) — non-breaking but worth fixing upstream in the shipped Swift-v2 templates. See `docs/findings/swift22-stale-itemtype-title-references.md` for the one-line-per-file XML diff.
- **Missing ItemType XMLs for `CustomerCenterApp`** — observed by Phase 38.1-02 during the per-(Table, Column, ID) breakdown but not tracked here as a dedicated cleanup item; the DB carries paragraph rows whose ItemType definition no longer ships with the Swift-v2 set. Consider whether the DB rows should be deleted or the XMLs restored upstream.
- **Defensive tooling for non-pristine state** (scripts `08` and `09`) — rerun against live or drifted DBs when the bacpac is not pristine. No code change needed; run via the pipeline which already invokes them.
- **Automation for Category 5** — a future phase could add a script `10-fix-stale-itemtype-title-refs.xml.ps1` that applies the XML edit across the 7 ItemType files on the host.

---

## Suggested remediation (upstream)

Categories 1, 2, 3, 4 are source-data hygiene items in the shipped Swift-v2
reference install. Ideally upstream Swift would ship with these cleaned;
until then the cleanup-script family runs as part of the baseline-refresh
pipeline.

Category 5 (stale ItemType title references) is an XML-only fix and should
land in the upstream Swift design package: see `docs/findings/swift22-stale-itemtype-title-references.md`
§"Suggested remediation" for the canonical diff.

Category 6 (schema alignment) is a cross-environment deployment concern,
not a Swift bug: align DW NuGet versions between source and target
environments whenever possible. When versions must differ, `cleandb-align-schema.sql`
is the supported operational fix.

Classes 7 and 8 (defensive tooling) are artifacts of live-dev drift — not
upstream bugs. The correct operational posture is: refresh the bacpac
periodically from a clean DW admin session, and rerun the pipeline.

---

## Proof of closure

The full cleanup surface has been proved closed end-to-end by the Phase
38.1-04 autonomous pipeline run against the pristine 2026-01-29 bacpac:

- **Evidence:** `.planning/phases/38.1-close-phase-38-deferrals/38.1-02-e2e-results.md` — disposition `CLOSED`, HTTP 200 on all 4 API calls (serialize deploy/seed, deserialize deploy/seed), EcomProducts source 2051 == target 2051, smoke tool non-vacuous with 88 pages × 2xx and zero failures, zero escalated strict-mode warnings across all four logs.
- **Run directory:** `.planning/phases/38.1-close-phase-38-deferrals/pipeline-runs/diagnostic/` — per-step logs + pipeline.log trace.
- **Halt evidence (superseded):** `.planning/phases/38.1-close-phase-38-deferrals/pipeline-runs/20260422-093111/` retained for reference — captures the Script 08 halt analysis that drove the pivot to bypassing `08`/`09` on the pristine bacpac.
- **Pipeline:** `tools/e2e/full-clean-roundtrip.ps1` re-runs the full flow unattended against a freshly-restored bacpac.

Phase 38.1 disposition: **CLOSED**. All four original deferrals (B.5.1,
B.4.1, B.3.1, GRID-01) plus the wider orphan-gap investigation are resolved.

---

## Environment

| | |
|---|---|
| Dynamicweb version | 10.23.9 |
| Design package     | Swift v2 |
| Source DB          | Swift-2.2 (SQL Server / `localhost\SQLEXPRESS` local dev; public URL `https://localhost:54035`) |
| Target DB          | Swift-CleanDB (SQL Server / `localhost\SQLEXPRESS` local dev; public URL `https://localhost:58217`) |
| Bacpac source      | `tools/swift2.2.0-20260129-database.zip` (Swift 2.2 captured 2026-01-29) |
| Host projects      | `C:\Projects\Solutions\swift.test.forsync\Swift2.2\Dynamicweb.Host.Suite` + `Swift.CleanDB\Dynamicweb.Host.Suite` |
| Phase trail        | Phase 37 Production-Ready Baseline → Phase 38 Hardening → Phase 38.1 Deferral Closure → Phase 38.1 Gap Closure |

---

## Related artifacts

- `docs/findings/swift22-stale-itemtype-title-references.md` — Category 5 detailed finding
- `docs/baselines/Swift2.2-baseline.md` — DEPLOYMENT / SEED / NOT-SERIALIZED three-bucket split rationale
- `docs/baselines/env-bucket.md` — per-environment config + DW NuGet version alignment
- `tools/swift22-cleanup/README.md` — script catalog + run order
- `tools/e2e/README.md` — autonomous pipeline entry point + prerequisites + exit codes
- `tools/swift22-cleanup/cleandb-align-schema.sql` — 10 idempotent ALTER TABLE additions
- `.planning/phases/38.1-close-phase-38-deferrals/38.1-02-e2e-results.md` — proof-of-closure artifact (disposition `CLOSED`)
- `.planning/phases/38.1-close-phase-38-deferrals/38.1-02-orphan-investigation.md` — per-(Table, Column, ID) breakdown for Category 7's 20 IDs

---

## Contact

Captured by the DynamicWeb.Serializer baseline-refresh pipeline (Phase 38.1
gap closure, 2026-04-22). Email-safe standalone — no PII, no credentials, no
secrets; all cited identifiers are Swift-v2 reference-install table/column
names and numeric page IDs.
