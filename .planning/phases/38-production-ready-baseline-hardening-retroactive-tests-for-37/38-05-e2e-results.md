# Plan 38-05 Task 2 — Final Live E2E Round-Trip Results

**Date:** 2026-04-21
**Executor:** agent-a34e8bdb (continuation of agent-a2dec3f1 post worktree rotation)
**Config state:** `swift2.2-combined.json` — `strictMode: false` REMOVED, `acknowledgedOrphanPageIds: [15717]` REMOVED (Task 1 commit `2c70fc2`)
**Hosts:**
- Swift 2.2 `https://localhost:54035` (DB `Swift-2.2`)
- Swift CleanDB `https://localhost:58217` (DB `Swift-CleanDB`)
- Both on `localhost\SQLEXPRESS` Integrated Security
- Both hosts restarted 2026-04-21 18:48:14 with fresh Release DLL (md5 `49CCC3D6A19A12AFD6A9B7B0242588F2`, containing commits through `692e184` C.1 fix)
- Both hosts' `Serializer.config.json` replaced with post-Task-1 baseline (no strictMode, no acknowledgedOrphanPageIds)

## Round-trip step outcomes

| Step | HTTP | Rows | Notes |
| ---- | ---- | ---- | ----- |
| Serialize Deploy  | **400** | 916 across 16 predicates (Content aborted, 15 SqlTable OK) | Content predicate threw BaselineLinkSweeper error on 1 unresolvable ref (page 15717 in ButtonEditor SelectedValue) — see "New problem" below |
| Serialize Seed    | 200 | 3489 across 8 predicates | 3497 YAML files, 2052 EcomProducts YAML (matches prior 38-03 run — C.1 fix holds) |
| Deserialize Deploy | **400** | 916 created / 0 failed across 17 predicates | 1 warning escalated (B.4 FK re-enable) + 1 hard error (missing `_sql/area.yml` — downstream of serialize Content abort) |
| Deserialize Seed  | **400** | 3489 created / 0 failed across 8 predicates | 7 warnings escalated (schema-drift columns) — all rows wrote, escalation is post-write |

## Final CleanDB state

```
Area              0        (content aborted on serialize)
Page              0
Paragraph         0
GridRow           0
EcomShops         9        (deploy SqlTable wrote before FK escalation)
EcomCountries    96
EcomProducts   2051        (C.1 preservation holds: source 2051 → target 2051)
EcomGroups      316
UrlPath           1
```

## New problem surfaced (gating decision)

**ID 15717 is NOT a page — it is a paragraph ID referenced in ButtonEditor JSON `SelectedValue`.**

Pre-cleanup query on Swift-2.2 DB:

```sql
SELECT TOP 2 LEFT(SecondButton, 800) FROM [ItemType_Swift-v2_Poster] WHERE SecondButton LIKE '%15717%'
```
returns:
```json
{ "SelectedValue": "15717",
  "Label": "Become partner",
  "Link": "Default.aspx?ID=4897#15717",
  "LinkType": "paragraph",
  "Style": "outline-primary" }
```

`SELECT PageID FROM Page WHERE PageID = 15717` returns 0 rows — 15717 is not a page.

**Why B.5 (Plan 38-02) did not catch this:** Plan 02 commit `7ddd37c` intentionally left `SelectedValuePattern` loop unchanged (explicit Pitfall 3 in the B.5 plan), deferring ButtonEditor JSON handling. The BaselineLinkSweeper's `SelectedValuePattern` matches `"SelectedValue": "N"` and checks `N` against `validIds` (page IDs only) — so 15717 (a paragraph ID, not a page ID) is flagged as unresolvable.

**Impact:** Removing `acknowledgedOrphanPageIds: [15717]` in Task 1 surfaces this intentionally-deferred case. Without that ack entry, strict-mode Content serialize aborts on the first occurrence.

**Fix path (deferred to Phase 38.1):** Two viable options, neither in Phase 38 scope —
1. Extend `SelectedValuePattern` handling in `BaselineLinkSweeper.CheckField` to also accept paragraph IDs when a neighboring `"LinkType": "paragraph"` hint is present (or unconditionally check both sets — low false-positive risk since paragraphs and pages share no IDs).
2. Re-introduce acknowledgedOrphanPageIds mid-term but document that 15717 IS a paragraph ID (data is fine, sweeper is incomplete).

Option 1 is code-correctness (matches B.5's direction); option 2 is a workaround.

## Serialize-side strict mode escalations (serialize deploy log)

Host log `final-serialize-deploy-hostlog.log` line 467:

```
[2026-04-21 18:54:23.179] ERROR: Content serialization failed: Baseline link sweep found 1 unresolvable reference(s):
  - ID 15717 in page 2504cbec-3276-407d-8a25-0c9bf0958285 (Home Machines)/paragraph b31a4a5d-953a-45a9-8eab-c348783b3981 / Fields.SecondButton: "SelectedValue": "15717"
Fix the source baseline: include the referenced pages in a predicate path, or remove the references. Known-broken source refs may be listed under AcknowledgedOrphanPageIds on the owning Content predicate.
```

Link sweep stats that run: `83 internal link(s) verified, 1 unresolvable (ack deploy=0, seed=0)`.

## Deserialize Deploy strict mode escalations (1 warning + 1 error)

Host log `final-deserialize-deploy-hostlog.log` strict errors block:

```
"Strict mode: 1 warning(s) escalated to failure:
  -   WARNING: Could not re-enable FK constraints for [EcomShopGroupRelation]: The ALTER TABLE statement conflicted with the FOREIGN KEY constraint 'DW_FK_EcomShopGroupRelation_EcomShops'. The conflict occurred in database 'Swift-CleanDB', table 'dbo.EcomShops', column 'ShopId'.",
"Error in Content: Could not find file '...\\SerializeRoot\\deploy\\_sql\\area.yml'."
```

The B.4 FK escalation is the **known pre-existing deferral** (see `38-03-b4-investigation.md` — orphan SHOP19 row in source data, outcome `production-write-order-deferred`). The missing `area.yml` is a secondary consequence of the serialize-side Content abort.

## Deserialize Seed strict mode escalations (7 warnings)

All 7 warnings are schema-drift: source has columns the target schema lacks. `TargetSchemaCache` (Phase 37-02) warns-and-skips the column — ALL row data writes successfully (3489/3489 created) — but strict mode then escalates the warning count to a failure status at the end.

Distinct columns flagged:

| Source Column | Warning |
| ------------- | ------- |
| EcomGroups.GroupPageIDRel                      | not present on target schema — skipping |
| EcomProducts.ProductPeriodId                   | not present on target schema — skipping |
| EcomProducts.ProductVariantGroupCounter        | not present on target schema — skipping |
| EcomProducts.ProductPriceMatrixPeriod          | not present on target schema — skipping |
| EcomProducts.ProductOptimizedFor               | not present on target schema — skipping |
| EcomProducts.MyVolume                          | not present on target schema — skipping |
| EcomProducts.MyDouble                          | not present on target schema — skipping |

**Scope relative to plan's stated B.3 expectation:** The Phase 38 CONTEXT identified only 3 Area columns (`AreaHtmlType`, `AreaLayoutPhone`, `AreaLayoutTablet`) for B.3. Those Area-column warnings did NOT fire in this run because Content serialization aborted before Area rows were written (so the target never saw them on deserialize). The 7 columns listed above are a **larger-than-expected schema-drift surface** that B.3 did not enumerate — effectively a superset. The fix pattern is the same (operational: align DW NuGet versions between source and target, OR extend `TargetSchemaCache` allowlist / `excludeFields`), but the scope is broader than Phase 38 anticipated.

## Smoke tool result

```
=== DynamicWeb.Serializer Frontend Smoke Test ===
Host:       https://localhost:58217
AreaID:     3
LangPrefix: /en-us
SqlServer:  localhost\SQLEXPRESS
Database:   Swift-CleanDB
Auth:       Integrated Security

No active pages found under area 3. Nothing to test.
```

Exit code: 0 (no pages to exercise — empty set vacuously passes). This is a **false-positive pass** caused by the content-empty state; a real smoke result requires Content deserialize to succeed.

## Success-gate assessment vs plan's <success_criteria>

| Gate | Plan expectation | Observed | Met? |
| ---- | --------------- | -------- | ---- |
| Config edited: no strictMode, no acknowledgedOrphanPageIds | pass | pass | YES |
| Zero escalated warnings in serialize deploy+seed | pass | 1 escalated (15717 ButtonEditor SelectedValue) | NO |
| EcomProducts source 2051 → target 2051 | 2051 → 2051 | 2051 → 2051 | YES |
| Zero escalated warnings in deserialize deploy+seed | pass | 1+7 = 8 escalations | NO |
| Smoke tool exits 0 | real pass on 2xx | exit 0 but "Nothing to test" — vacuous | PARTIAL |

## Phase 38 final disposition

**`gated-closed-on-38.1`** — D-38-16 code+config change is complete and correct. A fully clean strict-mode E2E is **gated on Phase 38.1 work** covering:

1. **B.5 extension (ButtonEditor `SelectedValue` paragraph-ID handling)** — NEW Phase 38.1 item. The 15717 case proves B.5's `SelectedValuePattern` loop (kept intentionally stable per Pitfall 3) needs paragraph-ID awareness. Tracked as **B.5.1** in deferred-items.
2. **B.4 FK re-enable order / SHOP19 orphan-row cleanup** — already deferred by Plan 38-03 to Phase 38.1 (`06-delete-orphan-ecomshopgrouprelation.sql`).
3. **Expanded B.3 schema-drift allowlist** — original B.3 named 3 Area columns; live E2E shows 7 more columns across EcomGroups + EcomProducts. Fix pattern (operational: DW version alignment OR config allowlist) is the same; scope is wider. Treated as a Phase 38.1 extension of B.3.
4. **142 GridRow GridRowDefinitionId NOT NULL** — carried forward from Plan 38-03 Task 5 (was not exercised here because Content serialize aborted before GridRow write).

The strict-mode code path, config flip (D-38-16), and the two Phase 37-03/04/05 validator families (SqlIdentifierValidator, SqlWhereClauseValidator, BaselineLinkSweeper + StrictModeResolver + TemplateAssetManifest) all functioned as designed. The gating issues are **source-data shape** (15717, SHOP19) and **target-schema alignment** (7 schema-drift columns) — both of which Phase 38 explicitly planned to defer to Phase 38.1 as operational/data-cleanup items.

## Repro recipe (for Phase 38.1)

1. Branch from this commit.
2. Fix B.5.1 in `BaselineLinkSweeper.CheckField.SelectedValuePattern` loop — check `validParagraphIds` in addition to `validIds`; add regression test on the 15717 fixture.
3. Add `tools/swift22-cleanup/06-delete-orphan-ecomshopgrouprelation.sql` deleting the SHOP19 orphan.
4. Extend Swift-2.2 cleanup OR CleanDB schema-alignment to close the 7 EcomGroups/EcomProducts schema-drift columns (operational; no code change if DW version alignment works).
5. Re-run this plan's Task 2 E2E recipe — all 4 HTTP codes should be 200, all counts should match, smoke should find pages.

## Artifacts

| File | Purpose |
| ---- | ------- |
| `task5-logs/final-serialize-deploy.log`         | HTTP 400 response body, 0 lines (API message only) |
| `task5-logs/final-serialize-deploy-hostlog.log` | 1368-line host log, line 467 has the 15717 error |
| `task5-logs/final-serialize-seed.log`           | HTTP 200 response body |
| `task5-logs/final-serialize-seed-hostlog.log`   | 113-line host log, 0 errors, 0 warnings |
| `task5-logs/final-deserialize-deploy.log`       | HTTP 400 response body with B.4 escalation |
| `task5-logs/final-deserialize-deploy-hostlog.log` | 1142-line host log, 3 error blocks |
| `task5-logs/final-deserialize-seed.log`         | HTTP 400 response body with 7 schema-drift escalations |
| `task5-logs/final-deserialize-seed-hostlog.log` | 3613-line host log, 7 WARNING + 1 ERROR block |
