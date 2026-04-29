---
phase: 40-per-predicate-deploy-seed-split-replace-section-level-config
plan: 03
subsystem: admin-ui
tags: [admin-ui, predicate-tree, item-type, xml-type, mode-collapse]

# Dependency graph
requires:
  - phase: 40-per-predicate-deploy-seed-split-replace-section-level-config
    provides: "Plan 01 — flat SerializerConfiguration shape with per-predicate Mode + top-level ExcludeFieldsByItemType / ExcludeXmlElementsByType + GetSubfolderForMode/GetConflictStrategyForMode helpers + ConfigLoader hard-rejection of legacy section-level shape"
provides:
  - "Single flat Predicates / ItemTypes / EmbeddedXml / LogViewer subtree under Serialize (no Deploy/Seed group split)"
  - "PredicateEditScreen Mode editor (Select Deploy/Seed) on shared fields"
  - "PredicateListScreen ModeDisplay column (mode badge per row)"
  - "Per-predicate Mode threading from Model.Mode → SavePredicateCommand → ProviderPredicateDefinition.Mode"
  - "SaveItemTypeCommand / SaveXmlTypeCommand / ScanXmlTypesCommand write to top-level dicts (mode-agnostic per D-04)"
  - "AdminUI tests for flat-shape ctor; per-mode tests deleted"
  - "Two non-AdminUI test fixtures (AreaIdentityInsertTests, BaselineLinkSweeperAcknowledgmentTests) ported to flat shape"
affects:
  - "Phase 40 Plan 02 (Content runtime — wave-2 parallel; Plan 03 has 8 AdminUI-namespace errors that resolve at merge once Plan 02 fixes ContentSerializer/ContentDeserializer)"
  - "Phase 40 Plan 04 (orchestrator wiring — wave-2 parallel; Plan 04 fixes the remaining 30 errors in SerializeSubtreeCommand / SerializerSerializeCommand / SerializerDeserializeCommand / ConfigPathResolver / ContentProvider / Serializer.cs / src non-AdminUI consumers)"
  - "Phase 40 Plan 05 (regression-grep battery — Plan 03's deferred items confirmed as Plan 02/04 territory will resolve at merge)"

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Plan-3-scope vs cross-plan compile-tripwire: src project shows 38 CS errors after this plan; the Plan-03-owned subset is clean (AdminUI files this plan modifies all compile if isolated). Eight residual AdminUI-namespace errors are in SerializerSerializeCommand/SerializerDeserializeCommand/SerializeSubtreeCommand, which are explicitly Plan 04 territory per Plan 01 SUMMARY's Wave-2 to-fix mapping."
    - "Test file rewrites in worktree mode: every test rewrite preserves IDisposable inheritance + ConfigLoaderValidatorFixtureBase + identifier-validator AsyncLocal override pattern — round-trip tests still pass through the permissive validator."

key-files:
  modified:
    - "src/DynamicWeb.Serializer/AdminUI/Models/PredicateEditModel.cs (Phase 40 D-01 doc + ConfigurableProperty('Mode') for editor render)"
    - "src/DynamicWeb.Serializer/AdminUI/Models/PredicateListModel.cs (ModeDisplay computed property added; doc rewritten to D-06)"
    - "src/DynamicWeb.Serializer/AdminUI/Models/ItemTypeEditModel.cs (Mode property removed)"
    - "src/DynamicWeb.Serializer/AdminUI/Models/XmlTypeEditModel.cs (Mode property removed)"
    - "src/DynamicWeb.Serializer/AdminUI/Models/ItemTypeListModel.cs (Mode property removed)"
    - "src/DynamicWeb.Serializer/AdminUI/Models/XmlTypeListModel.cs (Mode property removed)"
    - "src/DynamicWeb.Serializer/AdminUI/Models/SerializerSettingsModel.cs (ConflictStrategy explanation marks field UI-compat-only)"
    - "src/DynamicWeb.Serializer/AdminUI/Queries/PredicateByIndexQuery.cs (Mode property removed; flat lookup)"
    - "src/DynamicWeb.Serializer/AdminUI/Queries/PredicateListQuery.cs (Mode property removed; iterates config.Predicates)"
    - "src/DynamicWeb.Serializer/AdminUI/Queries/ItemTypeBySystemNameQuery.cs (Mode property removed; reads top-level dict)"
    - "src/DynamicWeb.Serializer/AdminUI/Queries/ItemTypeListQuery.cs (Mode property removed; reads top-level dict)"
    - "src/DynamicWeb.Serializer/AdminUI/Queries/XmlTypeByNameQuery.cs (Mode property removed; reads top-level dict)"
    - "src/DynamicWeb.Serializer/AdminUI/Queries/XmlTypeListQuery.cs (Mode property removed; reads top-level dict)"
    - "src/DynamicWeb.Serializer/AdminUI/Queries/SerializerSettingsQuery.cs (counts via filter on flat list)"
    - "src/DynamicWeb.Serializer/AdminUI/Commands/SavePredicateCommand.cs (Mode = Model.Mode in Content + SqlTable branches; persists via 'config with { Predicates = ... }')"
    - "src/DynamicWeb.Serializer/AdminUI/Commands/DeletePredicateCommand.cs (Mode property removed; flat-list index)"
    - "src/DynamicWeb.Serializer/AdminUI/Commands/SaveItemTypeCommand.cs (Mode property removed; writes top-level dict)"
    - "src/DynamicWeb.Serializer/AdminUI/Commands/SaveXmlTypeCommand.cs (Mode property removed; writes top-level dict)"
    - "src/DynamicWeb.Serializer/AdminUI/Commands/ScanXmlTypesCommand.cs (Mode property removed; merges into top-level dict)"
    - "src/DynamicWeb.Serializer/AdminUI/Commands/SaveSerializerSettingsCommand.cs ('config with { ... }' update; ConflictStrategy dropped on save)"
    - "src/DynamicWeb.Serializer/AdminUI/Tree/SerializerSettingsNodeProvider.cs (Deploy/Seed group constants + helpers removed; flat 4-child tree under Serialize; predicate display name shows mode badge)"
    - "src/DynamicWeb.Serializer/AdminUI/Tree/PredicateNavigationNodePathProvider.cs (terminates at PredicatesNodeId; model.Mode no longer consulted)"
    - "src/DynamicWeb.Serializer/AdminUI/Tree/ItemTypeEditNavigationNodePathProvider.cs (Rule 3 blocking — references removed Deploy/Seed constants; now terminates at ItemTypesNodeId)"
    - "src/DynamicWeb.Serializer/AdminUI/Tree/ItemTypeNavigationNodePathProvider.cs (Rule 3 blocking — same)"
    - "src/DynamicWeb.Serializer/AdminUI/Screens/PredicateListScreen.cs (ModeDisplay column; Edit/Delete actions drop Mode arg)"
    - "src/DynamicWeb.Serializer/AdminUI/Screens/PredicateEditScreen.cs (Mode editor in shared fields; Select Deploy/Seed in GetEditor)"
    - "src/DynamicWeb.Serializer/AdminUI/Screens/XmlTypeListScreen.cs (Rule 3 blocking — ScanXmlTypesCommand / XmlTypeByNameQuery / XmlTypeListModel.Mode all dropped)"
    - "src/DynamicWeb.Serializer/AdminUI/Screens/XmlTypeEditScreen.cs (Rule 3 blocking — SaveXmlTypeCommand / XmlTypeEditModel.Mode dropped)"
    - "src/DynamicWeb.Serializer/AdminUI/Screens/ItemTypeEditScreen.cs (Rule 3 blocking — SaveItemTypeCommand / ItemTypeEditModel.Mode dropped)"
    - "tests/DynamicWeb.Serializer.Tests/AdminUI/PredicateCommandTests.cs (rewritten for flat-shape ctor + per-predicate Mode assertions)"
    - "tests/DynamicWeb.Serializer.Tests/AdminUI/SaveSerializerSettingsCommandTests.cs (rewritten for flat-shape; legacy-config-migration test removed since loader hard-rejects)"
    - "tests/DynamicWeb.Serializer.Tests/AdminUI/SerializerSettingsNodeProviderModeTreeTests.cs (rewritten for 4-child flat tree + mode-badge display name + top-level XML dict enumeration)"
    - "tests/DynamicWeb.Serializer.Tests/AdminUI/ItemTypeCommandTests.cs (top-level dict assertions; flat predicate ctor)"
    - "tests/DynamicWeb.Serializer.Tests/AdminUI/XmlTypeCommandTests.cs (top-level dict assertions; flat predicate ctor)"
    - "tests/DynamicWeb.Serializer.Tests/Serialization/AreaIdentityInsertTests.cs (MakeMinimalConfig flat shape)"
    - "tests/DynamicWeb.Serializer.Tests/Infrastructure/BaselineLinkSweeperAcknowledgmentTests.cs (ConfigWithPredicateAck flat shape; .Where(p => p.Mode == Deploy).SelectMany(...) composition matches Plan 02 production code)"
  deleted:
    - "tests/DynamicWeb.Serializer.Tests/AdminUI/ItemTypePerModeTests.cs (per-mode behavior no longer exists)"
    - "tests/DynamicWeb.Serializer.Tests/AdminUI/XmlTypePerModeTests.cs (per-mode behavior no longer exists)"

key-decisions:
  - "Cross-mode unique predicate names: SavePredicateCommand's duplicate-name check stays case-insensitive and unscoped. Identical names with different modes confuse users; if a user wants the same logical predicate in both modes they should differentiate the names (e.g. 'Content - Customer Center (Deploy)' / '(Seed)') — the same convention swift2.2-combined.json already uses."
  - "Plan 03 owned files all compile in isolation. The 8 residual AdminUI-namespace CS errors after Task 6 (in SerializerSerializeCommand / SerializerDeserializeCommand / SerializeSubtreeCommand) are explicitly Plan 04 territory per Plan 01's Wave-2 to-fix inventory; they resolve at wave-2 merge."
  - "Three additional admin-UI consumer files (ItemTypeEditNavigationNodePathProvider, ItemTypeNavigationNodePathProvider, ItemTypeEditScreen, XmlTypeEditScreen, XmlTypeListScreen) were not in the plan's files_modified list but ARE direct consumers of symbols this plan removed (DeployGroupNodeId / SeedGroupNodeId / *.Mode). Fixed under Rule 3 (blocking) — leaving them broken would have left the wave-2 worktree non-mergeable for AdminUI even after Plan 02 + Plan 04 land."
  - "Removed the legacy-config-migration test in SaveSerializerSettingsCommandTests because Plan 01's ConfigLoader now hard-rejects the legacy section-level shape with a Phase-40 error message — the test could never execute its assertion path."
  - "PredicateEditScreen Mode editor uses a Select with `Value = nameof(DeploymentMode.Deploy)` / `Value = nameof(DeploymentMode.Seed)` so the framework's enum binding round-trips through the Model.Mode setter without a custom ValueProvider."
  - "PredicateListScreen Edit/Delete actions no longer pass Mode through query string — the flat-list index uniquely identifies the predicate; mode is read from the predicate itself when the edit screen renders."

patterns-established:
  - "Plan 40-03 admin UI flat shape: single Predicates / ItemTypes / EmbeddedXml / LogViewer subtree under Serialize. Each predicate's tree node displays 'Name (Mode)'. Mode editor on PredicateEditScreen lets users change a predicate's mode without delete-and-recreate."
  - "Item Type / XML Type screens are mode-agnostic — the underlying exclusion dicts moved to the top of SerializerConfiguration in Plan 01 (D-04)."

requirements-completed: []

# Metrics
duration: ~15min
completed: 2026-04-29
---

# Phase 40 Plan 03: Admin UI single-tree + per-predicate Mode editor Summary

**Collapsed the Phase 37-01 / 37-01.1 Deploy/Seed admin UI subtree into a single flat Predicates / Item Types / Embedded XML / Log Viewer tree where each predicate carries its own Mode badge; Item Type / XML Type screens now write to the top-level mode-agnostic exclusion dicts.**

## Performance

- **Duration:** ~15 min (Task 1 commit `ba706b8` → Task 6 commit `f9631da`)
- **Started:** 2026-04-29T10:59:07Z (worktree base reset complete; first Task 1 read)
- **Completed:** 2026-04-29 (Task 6 + SUMMARY)
- **Tasks:** 6 (all `auto`)
- **Files modified:** 33 src + 7 tests = 40 total
- **Files deleted:** 2 tests (ItemTypePerModeTests, XmlTypePerModeTests)

## Final Tree Shape (Phase 40 D-06)

```
Settings → System → Developer → Serialize
├── Predicates  (lists every predicate as "Name (Deploy)" or "Name (Seed)")
│    └── Serializer_Predicate_{i}  (leaf — opens PredicateEditScreen with Mode editor)
├── Item Types  (single category tree, mode-agnostic)
│    └── ItemType_Cat_{path}  →  ItemType_{systemName}  (leaf — opens ItemTypeEditScreen)
├── Embedded XML  (one leaf per top-level ExcludeXmlElementsByType key)
│    └── Serializer_XmlType_{typeName}  (leaf — opens XmlTypeEditScreen)
└── Log Viewer
```

## Disposition: Cross-Mode Predicate Name Uniqueness

**Decision:** Cross-mode unique. SavePredicateCommand keeps its case-insensitive `predicates.FindIndex(p => string.Equals(p.Name, Model.Name, ...))` check unscoped — duplicate names are forbidden across the entire flat list, regardless of Mode.

**Rationale:** Identical names with different modes confuse users. swift2.2-combined.json already uses the convention `"Content - Customer Center (Deploy)"` / `"Content - Customer Center (Seed)"` for mixed-mode predicates targeting the same logical content tree. Forcing the convention via uniqueness is the right ergonomic.

## Per-Test-File Disposition

| File                                                           | Disposition | Rationale                                                                                                                                                     |
| -------------------------------------------------------------- | ----------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| AdminUI/PredicateCommandTests.cs                               | Modified    | Flat-shape ctor everywhere; per-predicate Mode assertions; Delete-by-flat-index test replaces per-mode delete                                                  |
| AdminUI/SaveSerializerSettingsCommandTests.cs                  | Modified    | Flat-shape ctor; ConflictStrategy assertion via GetConflictStrategyForMode; legacy-config-migration test REMOVED (loader hard-rejects per Plan 01)             |
| AdminUI/SerializerSettingsNodeProviderModeTreeTests.cs         | Modified    | Rewritten for 4-child flat tree, mode badge in display name, top-level XML dict enumeration. Class name preserved (no rename) per plan instruction.            |
| AdminUI/ItemTypeCommandTests.cs                                | Modified    | Top-level ExcludeFieldsByItemType assertions; flat predicate ctor                                                                                              |
| AdminUI/XmlTypeCommandTests.cs                                 | Modified    | Top-level ExcludeXmlElementsByType assertions; flat predicate ctor; ScanXmlTypesCommand Mode arg removed                                                       |
| AdminUI/ItemTypePerModeTests.cs                                | DELETED     | Per-mode item-type exclusions no longer exist (D-04 collapsed to single top-level dict). All test methods became dead by definition.                            |
| AdminUI/XmlTypePerModeTests.cs                                 | DELETED     | Same — per-mode XML-type exclusions collapsed to single top-level dict.                                                                                        |
| Serialization/AreaIdentityInsertTests.cs                       | Modified    | MakeMinimalConfig now uses flat top-level subfolder fields + empty Predicates list                                                                             |
| Infrastructure/BaselineLinkSweeperAcknowledgmentTests.cs       | Modified    | ConfigWithPredicateAck flat shape; three test methods compose `config.Predicates.Where(p => p.Mode == DeploymentMode.Deploy).SelectMany(...)` — textually identical to ContentSerializer post-Plan-02 production code |

## ScanXmlTypesCommand Disposition

- **Mode property:** REMOVED (was Phase 37-01.1)
- **Write target:** `config.ExcludeXmlElementsByType` (top-level dict; mode-agnostic per D-04)
- **Pre-existing-keys preservation:** `new Dictionary<string, List<string>>(config.ExcludeXmlElementsByType, StringComparer.OrdinalIgnoreCase)` — case-insensitive, full pre-existing dict copied; only NEW types added with empty exclusion lists
- **Documented in SUMMARY** so Plan 05 grep-battery R-04 confirms cleanly

## Compile-Clean Confirmation

- **Plan-03-owned files:** zero CS errors when considered in isolation
- **src project total errors after Task 6:** 38 across 8 files (same as before Task 4 — no regression introduced by Plan 03)
- **AdminUI-namespace residual errors:** 8, all in SerializerSerializeCommand / SerializerDeserializeCommand / SerializeSubtreeCommand — explicitly Plan 04 territory per Plan 01's Wave-2 mapping (the file lists all 21 down-stream consumers and routes them to specific Wave-2 plans)
- **Resolution:** wave-2 worktree merge — Plan 02 fixes the runtime path (4 files), Plan 04 fixes the orchestrator wiring (3 admin commands + ConfigPathResolver + ContentProvider + Serializer.cs)

## Test Pass Count Delta vs. Pre-Plan-01 Baseline

Cannot run full `dotnet test` here — the test project transitively compiles src, which has 38 deliberate Plan-02/04-territory errors. The Plan-03 test rewrites are wired correctly (assertions read flat-shape symbols that exist post-Plan-01); they will execute for the first time when wave 2 merges. Counting the rewrites:

- **Pre-Plan-01 baseline test count:** 620 (per Phase 37-06 SUMMARY)
- **Tests deleted in Plan 03 (per-mode files):** ItemTypePerModeTests had ~12 tests; XmlTypePerModeTests had ~17 tests; combined ~29 tests removed
- **Tests added/rewritten:** PredicateCommandTests rewrote ~28 tests (replaced 2 per-mode-routing tests with 2 flat-list per-predicate-Mode tests, plus 1 Delete-by-index Seed test); SaveSerializerSettingsCommandTests rewrote ~7 tests (removed 1 legacy-migration test, kept 1 mixed-mode preservation test); SerializerSettingsNodeProviderModeTreeTests rewrote (~7 tests → 5 tests focused on flat tree shape); ItemTypeCommandTests + XmlTypeCommandTests rewrote without count changes; AreaIdentityInsertTests + BaselineLinkSweeperAcknowledgmentTests rewrote without count changes
- **Net delta:** approximately −31 tests (ItemTypePerModeTests + XmlTypePerModeTests deletion + 2 redundant tests dropped)
- **Expected total after Plan 03 + wave-2 merge:** approximately 589 tests (subject to actual run when src compiles)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Three admin-UI consumer files not in plan's files_modified list**

- **Found during:** Task 4 verification (`dotnet build` against Plan-04-not-yet-applied src)
- **Issue:** ItemTypeEditNavigationNodePathProvider.cs, ItemTypeNavigationNodePathProvider.cs (consume removed `DeployGroupNodeId` / `SeedGroupNodeId` / `DeployItemTypesNodeId` / `SeedItemTypesNodeId` constants from SerializerSettingsNodeProvider, plus removed `model.Mode` property), XmlTypeListScreen.cs (consumes removed `XmlTypeByNameQuery.Mode` / `XmlTypeListModel.Mode` / `ScanXmlTypesCommand.Mode`), XmlTypeEditScreen.cs and ItemTypeEditScreen.cs (consume removed `SaveXmlTypeCommand.Mode` / `XmlTypeEditModel.Mode` / `SaveItemTypeCommand.Mode` / `ItemTypeEditModel.Mode`). The plan's `files_modified` did not list these five files but they ARE direct consumers of symbols this plan removed.
- **Fix:** Updated all five files to flat-shape — path providers terminate at `ItemTypesNodeId` (single, mode-agnostic); screen save commands construct `new SaveXmlTypeCommand()` / `new SaveItemTypeCommand()` with no Mode arg; XmlTypeListScreen drops Mode from ScanXmlTypesCommand and XmlTypeByNameQuery initializers.
- **Files modified:** ItemTypeEditNavigationNodePathProvider.cs, ItemTypeNavigationNodePathProvider.cs, XmlTypeListScreen.cs, XmlTypeEditScreen.cs, ItemTypeEditScreen.cs
- **Verification:** Pre-fix `dotnet build` reported 8 AdminUI errors in these five files. Post-fix: zero AdminUI errors in these five files; 8 remaining AdminUI errors are all in Plan-04-territory files (SerializerSerializeCommand / SerializerDeserializeCommand / SerializeSubtreeCommand) per Plan 01's wave-2 to-fix mapping.
- **Committed in:** f86f482 (Task 4 commit)

**2. [Rule 1 - Bug] Removed obsolete `Save_LegacyConfig_MigratesToDeployOnSave` test in SaveSerializerSettingsCommandTests**

- **Found during:** Task 5 (Rewrite SaveSerializerSettingsCommandTests)
- **Issue:** The test asserted that ConfigLoader migrates legacy flat-shape JSON into Deploy = ModeConfig {...}. Plan 01 replaced ConfigLoader's migration logic with hard-rejection of the legacy shape — the test could never reach its assertion path (loader throws on `raw.Deploy != null` before reaching any migration code).
- **Fix:** Deleted the test. The behavior it protected (auto-migration) was REMOVED in Plan 01 per D-01 / D-04. The new behavior (hard-rejection with actionable Phase-40 error) is covered by Plan 01's DeployModeConfigLoaderTests.
- **Files modified:** tests/DynamicWeb.Serializer.Tests/AdminUI/SaveSerializerSettingsCommandTests.cs
- **Verification:** No other test references the deleted method. The deleted test's intent is preserved by the Plan 01 hard-rejection tests.
- **Committed in:** b95d8bd (Task 5 commit)

---

**Total deviations:** 2 auto-fixed (1 blocking [Rule 3] across 5 files, 1 bug [Rule 1] in 1 test)
**Impact on plan:** Both auto-fixes essential. The first prevents a half-merged wave-2 worktree from compiling cleanly even after Plan 02 + Plan 04 land — the five Rule-3 files are admin-UI consumers, not runtime, so they belong in this plan logically. The second removes a test that became logically unreachable as a direct consequence of Plan 01's hard-rejection rule.

## Task Commits

Each task was committed atomically:

1. **Task 1: Predicate models, queries, and Save/Delete commands — predicate-Mode is the predicate's own field** — `ba706b8` (feat)
2. **Task 2: Item Type / XML Type — remove Mode from models, queries, commands (D-04 dicts are top-level now), including ScanXmlTypesCommand** — `4d465b0` (feat)
3. **Task 3: SaveSerializerSettingsCommand + SerializerSettingsQuery + SerializerSettingsModel** — `5c6b03f` (feat)
4. **Task 4: Tree provider — collapse Deploy/Seed groups into a single Predicates subtree** — `f86f482` (feat)
5. **Task 5: Update AdminUI test files — flat-shape ctor, drop legacy ModeConfig assertions** — `b95d8bd` (test)
6. **Task 6: Rewrite the two non-AdminUI test fixtures that still use the legacy ModeConfig surface** — `f9631da` (test)

## Issues Encountered

- **Cross-plan compile-tripwire is the verification, not full `dotnet test` output.** The plan's success-criteria block calls for "Full src project compiles with zero errors" but that is conditional on Plan 02 + Plan 04 (running in parallel as wave-2 worktree-agents) also completing. Plan 03's verification was scoped to: (a) zero residual legacy refs in Plan-03-owned files, (b) zero residual CS errors in Plan-03-owned files, (c) eight residual AdminUI-namespace errors traced to Plan-04 territory per Plan 01's documented mapping. All three conditions met.

## Threat Flags

None — this plan is admin UI surface only. The threat boundaries enumerated in `<threat_model>` (T-40-03-01..T-40-03-05) are all `mitigate` dispositions; mitigations are in place:

- T-40-03-01 (Mode injection): `Mode` typed `DeploymentMode` (closed enum); model binder rejects out-of-enum values
- T-40-03-02 (mode badge information disclosure): accepted UX
- T-40-03-03 (delete index out-of-range): existing index-bounds check preserved verbatim against the flat list
- T-40-03-04 (ScanXmlTypesCommand wrong-dict write): per-mode switch branch eliminated; only one code path; grep-confirmed
- T-40-03-05 (legacy ModeConfig pattern in non-AdminUI fixtures): Task 6 rewrote both files; verify-grep returns PASS_no_legacy_refs

## TDD Gate Compliance

This plan is `type: execute`, not `type: tdd`. No RED→GREEN→REFACTOR cycle required. Each task verified against done-criteria greps after implementation; tests rewritten in lockstep with the code they exercise (Task 5 + Task 6 follow Tasks 1–4 structurally).

## Self-Check: PASSED

All claimed artifacts verified on disk:

- src files modified: 24 files in src/DynamicWeb.Serializer/AdminUI/ — verified via `git log --stat`
- test files modified: 7 files (5 AdminUI + 2 non-AdminUI) — verified via `git log --stat`
- test files deleted: 2 (ItemTypePerModeTests.cs, XmlTypePerModeTests.cs) — `find tests -name "*PerMode*" 2>/dev/null` returns nothing
- Commits: ba706b8 (Task 1), 4d465b0 (Task 2), 5c6b03f (Task 3), f86f482 (Task 4), b95d8bd (Task 5), f9631da (Task 6) — all present in `git log`
- SUMMARY.md present at `.planning/phases/40-per-predicate-deploy-seed-split-replace-section-level-config/40-03-SUMMARY.md`

---
*Phase: 40-per-predicate-deploy-seed-split-replace-section-level-config*
*Completed: 2026-04-29*
