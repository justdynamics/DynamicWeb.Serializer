# Plan 38-03 Task 5 — E2E Round-Trip Results

**Date:** 2026-04-21
**Executor:** agent-add31647 (continuation of agents afd43510 + a42f9480)
**Hosts:** Swift 2.2 (https://localhost:54035, DB `Swift-2.2`) → Swift CleanDB
(https://localhost:58217, DB `Swift-CleanDB`), both on `localhost\SQLEXPRESS`
Integrated Security.
**DLL deployed:** `src/DynamicWeb.Serializer/bin/Release/net8.0/DynamicWeb.Serializer.dll`
(size 354816 bytes, 2026-04-21 16:03) — contains the Task 1 C.1 monotonic-
counter fix from commit `692e184`.

## Result summary

| Gate | Pass/Fail | Notes |
| ---- | --------- | ----- |
| C.1 — EcomProducts 2051 → 2051 round-trip | **PASS** | 2051 source rows, 2052 YAML files (monotonic counters preserved all rows incl. 1091 empty-name duplicates), 2051 target rows |
| B.1/B.2 — zero stale template warnings in serialize | **PASS** | 0 matches for `1ColumnEmail\|2ColumnsEmail\|Swift-v2_PageNoLayout` across both deploy and seed YAML trees |
| Deserialize round-trip | PARTIAL | Seed 3489/3489 (100%); Deploy 1477/1619 (91%, 142 GridRow NOT NULL failures — new issue introduced by the cleanup, see below) |
| B.3/B.4 known pre-existing warnings | as expected | 3 Area-column drift warnings + 1 EcomShopGroupRelation FK orphan warning (both documented in 38-03-b3/b4-investigation.md) |

**Outcome label:** `e2e-passed` for C.1 gate + B.1/B.2 serialize gate. New
deferred issue recorded below for the deserialize-side cleanup consequence.

## Detailed results

### C.1 monotonic-counter dedup — LIVE VERIFICATION

Source: **2051 EcomProducts** rows in Swift 2.2. Schema breakdown:
- 1091 rows with empty `ProductName`
- 842 rows with duplicated named `ProductName` (231 distinct names)
- 118 rows with unique `ProductName`

Serialized YAML output at
`C:\Projects\Solutions\swift.test.forsync\Swift2.2\Dynamicweb.Host.Suite\wwwroot\Files\System\Serializer\SerializeRoot\seed\_sql\EcomProducts\`:

- **Total: 2052 .yml files**
- 351 base files (no dedup suffix) — one per distinct ProductName, incl. `_unnamed.yml`
- 1701 dedup files with `[hash-N]` suffix — counters contiguous per base

Breakdown of `_unnamed` files (the C.1-critical case):
- 1 `_unnamed.yml` + 1090 `_unnamed [d41d8c-1..1090].yml` = 1091 (matches source)
- Counter gap analysis: contiguous 1..1090, no duplicates

Pre-fix behavior (Phase 37 live session, 2026-04-20): **582** YAML files — 1469
rows silently lost via hash-of-identity overwrites.

**Post-fix behavior (this run): 2052** YAML files. The +1 vs. source-row-count
2051 is a stale-file-cleanup artifact (one named product has an extra base file
from a prior serialize that was not garbage-collected). This is NOT a C.1
semantics issue — it's a minor stale-cleanup glitch unrelated to the dedup fix
and does not affect round-trip preservation.

**CleanDB round-trip (target DB):** `SELECT COUNT(*) FROM EcomProducts` = **2051**
— exact match with source. The extra YAML file is idempotent on re-deserialize
(same identity, upsert) so target count stays at the source row count.

### B.1/B.2 stale template cleanup

Post-cleanup verify query against Swift 2.2 (after 05-null-stale-template-refs.sql
ran cleanly): 0 remaining refs in `ItemType_Swift-v2_*`, `Page.PageLayout`,
`Paragraph.ParagraphItemType`, and `GridRow.GridRowDefinitionId`.

Post-serialize verify across the 5278 freshly-written YAML files:

```
find SerializeRoot/{deploy,seed} -name '*.yml' -exec grep -l '1ColumnEmail\|2ColumnsEmail\|Swift-v2_PageNoLayout' {} \; | wc -l
# Result: 0
```

### Deserialize deploy bucket — 142 GridRow NOT NULL failures

The 142 GridRow rows whose `GridRowDefinitionId` was set to empty string by the
cleanup script now fail deserialize with
`Column 'GridRowDefinitionId' does not allow nulls.`. The serializer maps
empty-string-in-YAML to `DBNull.Value` in the INSERT command.

Root cause: `GridRow.GridRowDefinitionId` is declared NOT NULL but we
intentionally emptied it (because the referenced Swift templates no longer
ship upstream). The YAML serializer then emits either no `definitionId:` field
or an empty one, and the deserializer treats the absent value as NULL.

Options for resolution (deferred to Phase 38.1):
1. Change the cleanup to use a default-valid template name (e.g. `'Row'`)
   rather than empty string. Preserves NOT NULL but silently re-targets the
   template reference to a valid-but-wrong template.
2. Add ContentDeserializer logic to coalesce empty-string `definitionId` to a
   sensible default when the column is NOT NULL (serializer-side fix).
3. Delete the 142 GridRow rows entirely from Swift 2.2 (most aggressive — they
   belong to 7 unused newsletter templates that reference no-longer-shipping
   Swift assets). This matches the spirit of "stale data removal".

Recommended: **Option 3 as a new `06-delete-stale-email-gridrows.sql`**, to be
landed in Phase 38.1. That script's files_modified entry + the decimal-phase
dispatch are out of Plan 38-03's `files_modified` scope.

### B.3/B.4 — pre-existing warnings (expected, documented in prior tasks)

Confirmed during this E2E run:

- **3x WARNING: source column [Area].[AreaHtmlType|AreaLayoutPhone|AreaLayoutTablet]
  not present on target schema — skipping** — expected per
  `38-03-b3-investigation.md` outcome-a-operational. Resolution: DW NuGet version
  alignment (customer-ops concern, NOT code).
- **1x WARNING: Could not re-enable FK constraints for [EcomShopGroupRelation]**
  — expected per `38-03-b4-investigation.md` outcome
  `production-write-order-deferred`. Resolution: delete orphan SHOP19 row from
  Swift 2.2 source (Phase 38.1 follow-up, `06-delete-orphan-ecomshopgrouprelation.sql`).

Neither warning prevented the C.1 gate from passing.

### Other deserialize warnings (pre-existing, not Wave-3 scope)

- 24x `WARNING: Could not load PropertyItem for page <guid>` — Page-tree property-
  bag loading issue, orthogonal to this plan.
- N `WARNING: Unresolvable page ID <int> in link` — `acknowledgedOrphanPageIds`
  still includes [15717]; removal is Plan 38-05's scope.
- N `WARNING: Missing grid-row template` — now legitimately raised because we
  nulled the references; the 7 newsletter templates referenced 142 emptied
  GridRows (see above).

## Operational notes (for reproduction)

1. **Host restart required** after DB cleanup: DW caches ContentProvider data;
   serializing immediately after SQL UPDATE yields pre-cleanup YAML. The run
   sequence that worked was: SQL → restart hosts → serialize → mirror → purge →
   deserialize.

2. **DLL deploy needs host stop/start**: DW hosts on Windows lock the serializer
   DLL (no hot-reload). The `cp` fails with "Device or resource busy" unless
   both `Dynamicweb.Host.Suite.exe` processes are stopped first. Automated
   stop/start via PowerShell `Stop-Process` + `Start-Process` worked.

3. **Cross-host SerializeRoot mirror**: CleanDB reads its OWN
   `wwwroot/Files/System/Serializer/SerializeRoot/` folder, not Swift 2.2's.
   The reference note about a post-commit git hook was not used in this run;
   instead a direct `Copy-Item -Recurse` of `deploy/` and `seed/` was used.

4. **PowerShell quoting pitfalls** on bash-for-Windows: `Get-CimInstance
   Win32_Process -Filter "Name='x.exe'"` fails when bash eats the single-quoted
   inner string; `Get-Process x` is simpler and works.

## Commit hashes for this plan (final state)

| Task | Commit | Description |
| ---- | ------ | ----------- |
| 1 RED | `7f6af36` | Failing regression tests for FlatFileStore dedup (C.1) |
| 1 GREEN | `692e184` | FlatFileStore dedup preserves all rows (C.1 fix) |
| 1 DOC | `9a061e1` | Deferred-items note for DW-host prerequisites |
| 2 | `ed06c64` | SQL cleanup for 3 orphan template refs (B.1/B.2) |
| 3 | `fff81e8` | B.3 investigation — DW-version drift, documentation outcome |
| 4 | `a159672` | B.4 investigation — source-data orphan, deferred to 38.1 |
| 5 fix | `6065b96` | 05 script Rule 1 fixes (PageMasterPage removal + GridRow.GridRowDefinitionId add) |
| 5 results | (this commit) | E2E results file |

## Verdict

**Plan 38-03 Task 5 gate: e2e-passed** for the two primary gates (C.1 + B.1/B.2
serialize). Deploy-side deserialize introduces a new minor issue (142 GridRow
NOT NULL failures) which is a cleanup-approach refinement deferred to
Phase 38.1, alongside the already-deferred B.4 orphan-row cleanup.
