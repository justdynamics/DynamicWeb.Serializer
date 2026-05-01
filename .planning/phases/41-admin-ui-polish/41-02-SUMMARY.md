---
phase: 41-admin-ui-polish
plan: 02
subsystem: admin-ui
tags: [admin-ui, rename, docs, mode-labels]

# Dependency graph
requires:
  - phase: 41-admin-ui-polish
    plan: 01
    provides: 14 RED tests across 4 AdminUI test files; the 4 GetScreenName + tree-rename + clean-labels facts that Plan 41-02 turns GREEN
provides:
  - "Item Type Excludes" surface text across tree node + list/edit screen titles (D-01)
  - Clean Mode option labels ("Deploy" / "Seed", no parens) (D-11)
  - Documented intent for empty `excludeFieldsByItemType` in post-Phase-40 baseline (D-03)
affects: [41-03-dual-list-merge-and-mode-string]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Tree node display label rename via `NavigationNode.Name` flows automatically into breadcrumb segment — no separate breadcrumb provider edit needed."
    - "Documenting absent-by-design config keys via `docs/baselines/Swift2.2-baseline.md` rather than restoring stale empty dicts that ConfigWriter.WhenWritingNull would re-omit on next save."

key-files:
  created: []
  modified:
    - src/DynamicWeb.Serializer/AdminUI/Tree/SerializerSettingsNodeProvider.cs
    - src/DynamicWeb.Serializer/AdminUI/Screens/ItemTypeListScreen.cs
    - src/DynamicWeb.Serializer/AdminUI/Screens/ItemTypeEditScreen.cs
    - src/DynamicWeb.Serializer/AdminUI/Screens/PredicateEditScreen.cs
    - docs/baselines/Swift2.2-baseline.md

key-decisions:
  - "Label-only edit on PredicateEditScreen; preserved `Value = nameof(DeploymentMode.Deploy/Seed)` so Plan 41-03's enum→string Model migration can round-trip through `Enum.Parse<DeploymentMode>(... ignoreCase: true)` without churn."
  - "ItemTypeEditScreen.CreateFieldSelector NOT modified — Plan 41-03 owns that body change for D-06. Only GetScreenName at line 130-132 was touched here."
  - "Documented the empty `excludeFieldsByItemType` rather than restoring `{}` to JSON. ConfigWriter.WhenWritingNull (ConfigWriter.cs line 19 + Save dto null-coalesce at lines 35-36) would strip the empty dict on next save, making any restore pointless."
  - "Const ItemTypesNodeId = \"Serializer_ItemTypes\" preserved per D-02 to avoid churning SerializerSettingsNodeProviderModeTreeTests."

requirements-completed: [D-01, D-02, D-03, D-04, D-11]

# Metrics
duration: 3min
completed: 2026-05-01
---

# Phase 41 Plan 02: Admin-UI Rename + Mode Label Cleanup + Baseline Doc Summary

**Five mechanical, low-risk surface-text fixes ship the user-visible polish for D-01 (rename), D-03 (baseline doc), and D-11 (Mode option-label cleanup) — landing before the riskier Plan 41-03 merge-logic rework.**

## Performance

- **Duration:** ~3 min
- **Started:** 2026-05-01T17:03:06Z
- **Completed:** 2026-05-01T17:06:34Z
- **Tasks:** 3
- **Files modified:** 5 (4 source-text changes + 1 docs section)

## Accomplishments

- **D-01 rename** landed across the three user-visible strings:
  - Tree node `Name = "Item Types"` → `"Item Type Excludes"` at `SerializerSettingsNodeProvider.cs:75`
  - List screen `GetScreenName() => "Item Types"` → `"Item Type Excludes"` at `ItemTypeListScreen.cs:13`
  - Edit screen `$"Item Type: {SystemName}"` → `$"Item Type Excludes - {SystemName}"` and `"Item Type"` → `"Item Type Excludes"` at `ItemTypeEditScreen.cs:130-132`
- **D-02 invariant preserved:** Const `ItemTypesNodeId = "Serializer_ItemTypes"` is verifiably unchanged (line 27 of `SerializerSettingsNodeProvider.cs`).
- **D-11 Mode label cleanup:** dropped parenthetical suffixes from `PredicateEditScreen.cs:88-89`. Value strings (`nameof(DeploymentMode.Deploy)` / `nameof(DeploymentMode.Seed)`) preserved as Plan 41-03 dependency (round-trip through `Enum.Parse<DeploymentMode>` after the `Mode` model property is migrated to `string`).
- **D-03 / D-04 baseline doc:** new `## Exclusion sections` section appended after `## Per-predicate mode` in `docs/baselines/Swift2.2-baseline.md`. Documents the intentional absence of `excludeFieldsByItemType` from the post-Phase-40 baseline + the Phase 40 expansion of `excludeXmlElementsByType`. Audit conclusion: no content restoration needed.
- **4 RED tests from Plan 41-01 turned GREEN:**
  1. `SerializerSettingsNodeProviderModeTreeTests.GetSubNodes_UnderSerializeNode_ItemTypesNode_DisplayName_IsItemTypeExcludes`
  2. `ItemTypeEditScreenTests.GetScreenName_WithSystemName_StartsWithItemTypeExcludes`
  3. `ItemTypeEditScreenTests.GetScreenName_NoModel_IsItemTypeExcludes`
  4. `PredicateCommandTests.PredicateEditScreen_ModeOptions_HaveCleanLabels_PostPhase41`
- **7 RED tests stay RED** as planned — Plan 41-03 owns these (D-05/D-06/D-12/D-13 surface).
- **Zero pre-existing regressions:** full unit test suite goes 805 passed → 812 passed (was 808 → 812 over Plan 41-02 windows depending on slice). No previously-green test became red.

## Task Commits

1. **Task 1: Rename tree node + list/edit screen titles (D-01)** — `dd82470` (feat)
2. **Task 2: Drop parenthetical suffixes on Mode option labels (D-11)** — `b413cfe` (feat)
3. **Task 3: Document intentional empty excludeFieldsByItemType (D-03 + D-04)** — `16bfecf` (docs)

## Files Modified

- `src/DynamicWeb.Serializer/AdminUI/Tree/SerializerSettingsNodeProvider.cs` (1 line changed: line 75 `Name = "Item Types"` → `"Item Type Excludes"` with explanatory comment)
- `src/DynamicWeb.Serializer/AdminUI/Screens/ItemTypeListScreen.cs` (1 line changed: line 13 `GetScreenName()` literal)
- `src/DynamicWeb.Serializer/AdminUI/Screens/ItemTypeEditScreen.cs` (2 lines changed: ternary at lines 130-132. CreateFieldSelector at lines 82-128 verifiably untouched.)
- `src/DynamicWeb.Serializer/AdminUI/Screens/PredicateEditScreen.cs` (2 Label literals + 3 explanatory comment lines added at lines 83-95; Value literals preserved)
- `docs/baselines/Swift2.2-baseline.md` (+37 lines: new `## Exclusion sections` block with `### excludeFieldsByItemType — empty by design` and `### excludeXmlElementsByType — expanded during Phase 40` subsections)

## Audit Ground Truth (Task 3 step 1 — for Plan 41-03 inheritance)

Pre-execution audit confirmed RESEARCH.md expectations:

| Source | `excludeFieldsByItemType` | `excludeXmlElementsByType` |
|--------|---------------------------|----------------------------|
| Current `swift2.2-combined.json` | NOT PRESENT | present (line 7), populated (eCom_CartV2 has 21 elements) |
| `git show c5d9a8c~:swift2.2-baseline.json` | `"excludeFieldsByItemType": {}` (explicit empty) | present, sparse (smaller per-type lists than current) |
| `ConfigWriter.cs:19` | — | `DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull` (combined with `Save` dto coalesce at lines 35-36 that emits `null` when `Count == 0`) |

**Audit conclusion:** the absence of `excludeFieldsByItemType` from the post-Phase-40 baseline is the result of `ConfigWriter` correctly stripping empty dicts. The pre-Phase-40 explicit empty dict was an artifact of an older writer. No content was lost. The semantic state — no per-ItemType exclusions in Swift 2.2 — is unchanged. Documented in `docs/baselines/Swift2.2-baseline.md` per D-04.

## Test Results — RED→GREEN Transitions

Before Plan 41-02 (per `41-01-SUMMARY.md` baseline):
- AdminUI suite: 98 passing + 11 RED = 109 total

After Plan 41-02:
- AdminUI suite: 102 passing + 7 RED = 109 total

**Specifically GREEN now (4 transitions):**

| Test | Was | Now |
|------|-----|-----|
| `SerializerSettingsNodeProviderModeTreeTests.GetSubNodes_UnderSerializeNode_ItemTypesNode_DisplayName_IsItemTypeExcludes` | RED | GREEN |
| `ItemTypeEditScreenTests.GetScreenName_WithSystemName_StartsWithItemTypeExcludes` | RED | GREEN |
| `ItemTypeEditScreenTests.GetScreenName_NoModel_IsItemTypeExcludes` | RED | GREEN |
| `PredicateCommandTests.PredicateEditScreen_ModeOptions_HaveCleanLabels_PostPhase41` | RED | GREEN |

**Still RED — Plan 41-03 owns these:**

| Test | D-ID |
|------|------|
| `XmlTypeEditScreenTests.CreateElementSelector_DiscoveryEmpty_SavedNonEmpty_ShowsSavedAsOptions` | D-05 |
| `XmlTypeEditScreenTests.CreateElementSelector_DiscoveryAndSavedOverlap_UnionInOptions` | D-05 |
| `ItemTypeEditScreenTests.CreateFieldSelector_MetadataEmpty_SavedNonEmpty_ShowsSavedAsOptions` | D-06 |
| `PredicateCommandTests.ModeProperty_IsString_NotEnum_PostPhase41` | D-13 |
| `PredicateCommandTests.ModeProperty_HasHint_WithExplanatoryCopy_PostPhase41` | D-12 |
| `PredicateCommandTests.Save_ModeAsString_Deploy_RoundTripsViaQuery_PostPhase41` | D-13 |
| `PredicateCommandTests.Save_ModeAsString_Seed_RoundTripsViaQuery_PostPhase41` | D-13 |

Full unit-test suite: 812 passed + 7 failed (= the 7 above) of 819 total. **Zero regressions outside the AdminUI test set.**

## Critical Invariants Preserved

- **D-02:** `public const string ItemTypesNodeId = "Serializer_ItemTypes";` unchanged at line 27 of `SerializerSettingsNodeProvider.cs`.
- **Plan 41-03 dependency:** `Value = nameof(DeploymentMode.Deploy)` and `Value = nameof(DeploymentMode.Seed)` preserved at `PredicateEditScreen.cs:91-92` (each appears exactly once). When Plan 41-03 migrates `PredicateEditModel.Mode` to `string`, the Value strings round-trip through `Enum.Parse<DeploymentMode>(model.Mode, ignoreCase: true)` cleanly.
- **CreateFieldSelector untouched:** `ItemTypeEditScreen.cs` lines 82-128 verified unchanged via `git diff dd82470 HEAD -- src/DynamicWeb.Serializer/AdminUI/Screens/ItemTypeEditScreen.cs`. Plan 41-03 owns the D-06 short-circuit fix.

## Decisions Made

- **Comment placement:** added explanatory comments directly inline at each rename anchor (`// Phase 41 D-01: ...`) so future readers see the rationale without consulting plan files. Same shape used in 41-01 reflection-pattern comments.
- **`Item Type Excludes - {SystemName}` separator chosen as ` - ` (space-hyphen-space):** matches the Edit Predicate screen pattern at `PredicateEditScreen.cs:364` (`$"Edit Predicate: {Model.Name}"`) — though that uses `:`, the new screen-title intent is "exclusion-set view of <type>" so the dash conveys subordination better than a colon.
- **Inserted `## Exclusion sections` block AFTER `## Per-predicate mode` (and before `## The Swift 2.2 contamination problem`):** keeps the doc's reading flow logical — bucket split → mode mechanics → exclusion mechanics → cleanup notes.

## Deviations from Plan

None — plan executed exactly as written. Three Edit-tool calls fired the `READ-BEFORE-EDIT` hook reminder despite the files being read in the same session at the start of the run; the edits succeeded regardless and no rework was required.

## Issues Encountered

- **Worktree base mismatch on entry.** `git merge-base HEAD 5ef87af96fa5edf56b7a836a5245b7ae83a623c9` returned `347bdad...` rather than the expected base. Resolved per `<worktree_branch_check>`: `git reset --hard 5ef87af9...`. HEAD now matches.

## TDD Gate Compliance

This plan is the GREEN-gate companion to Plan 41-01's RED tests for D-01 + D-11. Sequence in git log:
- `test(41-01): ...` (RED gate, prior plan): `4ff5b76`, `d61ae84`, `5cfe64e`
- `feat(41-02): rename Item Types ...` (GREEN gate for D-01 / D-02): `dd82470`
- `feat(41-02): drop parens from Mode option labels` (GREEN gate for D-11): `b413cfe`
- `docs(41-02): document intentional empty excludeFieldsByItemType` (D-03 / D-04): `16bfecf`

The remaining D-05 / D-06 / D-12 / D-13 RED facts await Plan 41-03 GREEN coverage.

## Next Plan Readiness

- **Plan 41-03 ready** to land:
  - `XmlTypeEditScreen.CreateElementSelector` saved-exclusion merge (D-05) — turns 2 RED → GREEN
  - `ItemTypeEditScreen.CreateFieldSelector` saved-exclusion merge (D-06) — turns 1 RED → GREEN
  - `PredicateEditModel.Mode` typed as `string` + `[ConfigurableProperty(... hint:...)]` copy + `SavePredicateCommand` validation gate + `PredicateByIndexQuery` enum→string conversion (D-12 / D-13) — turns 4 RED → GREEN
- **No blockers, no architectural decisions deferred.**
- **Plan 41-03 inherits the audit ground truth** in this Summary's "Audit Ground Truth" section so it does not need to re-run the diff.

---
*Phase: 41-admin-ui-polish*
*Completed: 2026-05-01*

## Self-Check: PASSED

Verification of claims in this SUMMARY:

- **Files modified exist (and contain the documented strings):**
  - `src/DynamicWeb.Serializer/AdminUI/Tree/SerializerSettingsNodeProvider.cs` — FOUND, contains `Name = "Item Type Excludes"` at line 75, const `ItemTypesNodeId = "Serializer_ItemTypes"` preserved at line 27
  - `src/DynamicWeb.Serializer/AdminUI/Screens/ItemTypeListScreen.cs` — FOUND, contains `GetScreenName() => "Item Type Excludes"` at line 13
  - `src/DynamicWeb.Serializer/AdminUI/Screens/ItemTypeEditScreen.cs` — FOUND, contains `Item Type Excludes - {Model.SystemName}` at line 132
  - `src/DynamicWeb.Serializer/AdminUI/Screens/PredicateEditScreen.cs` — FOUND, contains exactly one `Label = "Deploy"` and one `Label = "Seed"`, plus one each `Value = nameof(DeploymentMode.Deploy/Seed)`
  - `docs/baselines/Swift2.2-baseline.md` — FOUND, contains `## Exclusion sections` heading (1×), `excludeFieldsByItemType` (4×), `excludeXmlElementsByType` (3×), `empty by design` (1×), `c5d9a8c` (1×), `Phase 41 D-03` (1×)
- **Commits exist (verified via `git log --oneline -5`):**
  - `dd82470` — FOUND
  - `b413cfe` — FOUND
  - `16bfecf` — FOUND
- **Build status:** `dotnet build DynamicWeb.Serializer.sln` exits 0 (62 pre-existing warnings, 0 errors)
- **Test counts:** AdminUI suite 102 passed + 7 failed (was 98 + 11) — exactly 4 RED→GREEN transitions, zero regressions; full unit suite 812 passed + 7 failed (the 7 are the same 7 still-RED AdminUI facts — Plan 41-03 owns them)
