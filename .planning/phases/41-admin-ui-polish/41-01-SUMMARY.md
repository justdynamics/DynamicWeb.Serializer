---
phase: 41-admin-ui-polish
plan: 01
subsystem: testing
tags: [tdd, admin-ui, xunit, reflection, red-tests]

# Dependency graph
requires:
  - phase: 40-per-predicate-deploy-seed-split
    provides: per-predicate Mode property on PredicateEditModel + flat Predicates list + top-level ExcludeXmlElementsByType / ExcludeFieldsByItemType dicts (the screens whose bugs Wave 0 locks down)
provides:
  - 14 RED unit tests across 4 files locking down five admin-UI defects (D-01, D-05, D-06, D-11, D-12, D-13)
  - Reflection-based test pattern that compiles cleanly while production code still has the buggy shapes (Mode is enum, GetScreenName returns "Item Type:")
  - Executable spec for Plans 41-02 (rename) and 41-03 (state-load + binding fixes)
affects: [41-02-admin-ui-rename-and-tooltip, 41-03-dual-list-merge-and-mode-string]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "RED-test reflection pattern: when production type is about to change shape (enum→string), tests reach the property via PropertyInfo + runtime branch on PropertyType so the test file stays compilable both before and after the production change."
    - "TestOverridePath try/finally: tests that exercise PredicateByIndexQuery.GetModel set ConfigPathResolver.TestOverridePath to the per-test config path inside try/finally so the AsyncLocal override never leaks across tests."

key-files:
  created:
    - tests/DynamicWeb.Serializer.Tests/AdminUI/XmlTypeEditScreenTests.cs
    - tests/DynamicWeb.Serializer.Tests/AdminUI/ItemTypeEditScreenTests.cs
  modified:
    - tests/DynamicWeb.Serializer.Tests/AdminUI/PredicateCommandTests.cs
    - tests/DynamicWeb.Serializer.Tests/AdminUI/SerializerSettingsNodeProviderModeTreeTests.cs

key-decisions:
  - "Used reflection on PredicateEditModel.Mode property type to keep RED tests compilable while Mode is still DeploymentMode enum — avoids #if false gating and lets the test file evolve cleanly when Plan 41-03 lands the string-typed Mode."
  - "Selected Hint (not Explanation) as the assertion target on ConfigurablePropertyAttribute. Current code passes the copy as `explanation:`; D-12 wants it as `hint:` (second positional ctor arg per the 10.23.9 ctor signature). The test fails RED because attr.Hint is empty."
  - "Corrected the plan's namespace for the Select editor: it lives in Dynamicweb.CoreUI.Editors.Lists, not Dynamicweb.CoreUI.Editors.Inputs. Plan 41-01's task 3 specified the wrong namespace (.Inputs.Select); the .Inputs namespace contains ListBase but no public Select type."

patterns-established:
  - "RED-test reflection pattern: PropertyInfo + runtime PropertyType branch keeps tests compilable across the buggy → fixed transition for type-changing refactors (enum → string)."
  - "TestOverridePath try/finally wrapping for query-based round-trip tests."

requirements-completed: [D-01, D-05, D-06, D-11, D-12, D-13]

# Metrics
duration: 13min
completed: 2026-05-01
---

# Phase 41 Plan 01: Wave 0 RED Tests for Admin-UI Polish Summary

**14 RED unit tests across 4 files lock down five Phase 40 admin-UI defects (rename, dual-list merge for both XmlType and ItemType, Mode enum-vs-string binding, option-label parens cleanup, hint copy) before any production fix lands.**

## Performance

- **Duration:** ~13 min
- **Started:** 2026-05-01T16:45:00Z (worktree base reset)
- **Completed:** 2026-05-01T16:58:11Z
- **Tasks:** 3
- **Files modified:** 4 (2 created, 2 extended)

## Accomplishments

- 14 new test facts added across the AdminUI test suite (3 + 4 + 6 + 1).
- 11 facts fail RED for the documented bug shape; 3 are GREEN regression baselines that lock existing-correct behavior so Plans 41-02 / 41-03 cannot break it.
- 0 pre-existing AdminUI tests regressed: baseline 95 passing → after this plan 98 passing + 11 RED + 0 broken.
- The RED set provides executable specs for both downstream plans:
  - Plan 41-02: `GetScreenName_*_StartsWith/Equals_ItemTypeExcludes` + `ItemTypesNode_DisplayName_IsItemTypeExcludes` + `ModeProperty_HasHint_WithExplanatoryCopy`.
  - Plan 41-03: `CreateElementSelector_*_ShowsSavedAsOptions` (D-05), `CreateFieldSelector_MetadataEmpty_*` (D-06), `ModeProperty_IsString_*` + `Save_ModeAsString_*_RoundTripsViaQuery` + `Save_ModeAsString_BogusValue_*` (D-13), `PredicateEditScreen_ModeOptions_HaveCleanLabels` (D-11).

## Task Commits

1. **Task 1: Create XmlTypeEditScreenTests.cs (D-05 RED tests)** — `4ff5b76` (test)
2. **Task 2: Create ItemTypeEditScreenTests.cs (D-06 RED + D-01 GetScreenName reflection)** — `d61ae84` (test)
3. **Task 3: Extend PredicateCommandTests.cs + SerializerSettingsNodeProviderModeTreeTests.cs** — `5cfe64e` (test)

_All commits use the `test(41-01):` conventional-commit type. No production code changed in this plan._

## Files Created/Modified

- `tests/DynamicWeb.Serializer.Tests/AdminUI/XmlTypeEditScreenTests.cs` (new, 108 LOC) — 3 facts covering D-05 dual-list merge for `XmlTypeEditScreen.CreateElementSelector`. Uses reflection on the private method since there is no DI seam yet (Plan 41-03 introduces one).
- `tests/DynamicWeb.Serializer.Tests/AdminUI/ItemTypeEditScreenTests.cs` (new, 105 LOC) — 4 facts covering D-06 (`CreateFieldSelector` short-circuit) + D-01 (`GetScreenName` rename to "Item Type Excludes").
- `tests/DynamicWeb.Serializer.Tests/AdminUI/PredicateCommandTests.cs` (extended, +173 LOC) — 6 new facts for D-13 (Mode→string + round-trip), D-12 (Hint copy), D-11 (clean Mode option labels). All use reflection so the test file stays compilable while Mode is still enum-typed.
- `tests/DynamicWeb.Serializer.Tests/AdminUI/SerializerSettingsNodeProviderModeTreeTests.cs` (extended, +18 LOC) — 1 new fact for D-01 tree-node display name rename.

## RED / GREEN Counts

| File | New facts | RED today | GREEN baseline | Notes |
|------|-----------|-----------|----------------|-------|
| `XmlTypeEditScreenTests.cs` | 3 | 2 | 1 | `CreateElementSelector_NoSaved_NoSelectionPreset` is the regression baseline. |
| `ItemTypeEditScreenTests.cs` | 4 | 3 | 1 | `CreateFieldSelector_NoSaved_NoSelectionPreset` is the regression baseline. |
| `PredicateCommandTests.cs` | 6 | 5 | 1 | `Save_ModeAsString_BogusValue_ReturnsInvalid_PostPhase41` short-circuits with `return` while Mode is enum-typed (acceptable per plan; becomes RED automatically once Plan 41-03 changes Mode to string). |
| `SerializerSettingsNodeProviderModeTreeTests.cs` | 1 | 1 | 0 | Tree-node rename. |
| **Totals** | **14** | **11** | **3** | |

**Pre-existing AdminUI tests:** 95 passing both before and after — no regressions.

**Final AdminUI suite:** 98 passing + 11 RED = 109 total.

## Decisions Made

- **Reflection-based property-type branching for RED tests:** Plan 41-01 originally proposed `#if false` gating for the round-trip tests that touch `PredicateEditModel.Mode`. Used the cleaner runtime branch (`if (modeProp.PropertyType == typeof(string)) ... else ...`) so the test file evolves automatically when Plan 41-03 lands the model change — no source edits required to switch the tests from "RED via short-circuit" to "RED via assertion failure."
- **Asserted Hint, not Explanation:** D-12 specifies the explanatory copy should move from `explanation:` to `hint:` so it renders as a label-adjacent tooltip. The test asserts `attr.Hint contains "Deploy ="`. Current code's `[ConfigurableProperty("Mode", explanation: "...")]` leaves `Hint` empty → RED for the documented reason.
- **Corrected Select namespace from plan's spec:** `Dynamicweb.CoreUI.Editors.Lists.Select`, not `Dynamicweb.CoreUI.Editors.Inputs.Select`. The `.Inputs` namespace contains `ListBase` (and the nested `ListOption`), but the public `Select` editor type ships in `.Lists`.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Corrected Select editor namespace in PredicateCommandTests.cs**
- **Found during:** Task 3 (PredicateEditScreen_ModeOptions_HaveCleanLabels_PostPhase41 fact)
- **Issue:** Plan specified `Assert.IsType<Dynamicweb.CoreUI.Editors.Inputs.Select>(editor)` and `using Dynamicweb.CoreUI.Editors.Inputs;`. The CoreUI 10.23.9 assembly places the public `Select` editor in `Dynamicweb.CoreUI.Editors.Lists` (verified via reflection-based DLL probe). The `.Inputs` namespace contains `ListBase` and nested `ListOption` only.
- **Fix:** Changed assertion target to `Assert.IsType<Select>(editor)` with `using Dynamicweb.CoreUI.Editors.Lists;`.
- **Files modified:** tests/DynamicWeb.Serializer.Tests/AdminUI/PredicateCommandTests.cs
- **Verification:** Build clean (0 errors), test runs and fails RED for the documented reason ("labels contain parens"), not for a type-cast error.
- **Committed in:** `5cfe64e` (Task 3 commit)

**2. [Rule 3 - Blocking] Wrapped query-based round-trip tests with TestOverridePath try/finally**
- **Found during:** Task 3 (Save_ModeAsString_*_RoundTripsViaQuery_PostPhase41 facts)
- **Issue:** `PredicateByIndexQuery.GetModel()` calls `ConfigPathResolver.FindConfigFile()` rather than using a per-test `ConfigPath` property. Without setting `ConfigPathResolver.TestOverridePath`, the query would not find the test config and would fail with a null/missing-file shape rather than the documented enum-vs-string round-trip mismatch.
- **Fix:** Each affected fact saves the existing override, sets `TestOverridePath = _configPath`, and restores in a `finally` block. Same pattern used by `SerializerSettingsNodeProviderModeTreeTests` (which sets it once in the fixture ctor; tests here scope it per-fact to avoid leaking across the rest of the class).
- **Files modified:** tests/DynamicWeb.Serializer.Tests/AdminUI/PredicateCommandTests.cs
- **Verification:** Save commands succeed with `CommandResult.ResultType.Ok`, queries return populated models, assertion failures cite the actual enum-vs-string shape (`Expected: Deploy / Actual: Deploy` with type mismatch), not "model is null."
- **Committed in:** `5cfe64e` (Task 3 commit)

**3. [Rule 1 - Bug] ConfigurablePropertyAttribute property name**
- **Found during:** Task 3 (ModeProperty_HasHint_WithExplanatoryCopy_PostPhase41 fact)
- **Issue:** Plan reference checks were ambiguous on whether `Hint` is a property or only a constructor parameter on `Dynamicweb.CoreUI.Data.ConfigurablePropertyAttribute`.
- **Fix:** Reflection probe of the 10.23.9 DLL confirmed `Hint` is a public property (declared on the attribute, not inherited). Test asserts `attr.Hint` directly. Current code passes the explanatory copy via the `explanation:` named arg, leaving `Hint` empty — fails RED for the documented reason.
- **Files modified:** none (test is correctly shaped)
- **Verification:** Test fails with `Sub-string not found / String: ""` — proves `Hint` is in fact empty under current code.
- **Committed in:** `5cfe64e` (Task 3 commit)

---

**Total deviations:** 3 auto-fixed (2 blocking + 1 bug). No Rule 4 architectural decisions needed.
**Impact on plan:** All three are micro-corrections to the plan's test code that were necessary for the tests to compile / fail-RED for the documented reason. No scope creep, no production code touched, no new packages.

## Issues Encountered

- **Worktree base mismatch on entry.** `git merge-base HEAD 24e294b...` returned the parent commit `347bdad...`, not the expected `24e294b...`. Resolved per `<worktree_branch_check>` step: `git reset --hard 24e294b` (safe in fresh worktree). HEAD now matches expected base.
- **No CLAUDE.md at project root.** Confirmed PROJECT.md is the canonical project doc per RESEARCH.md; followed PROJECT.md conventions (no backcompat, docstring nibble rule, DW patterns from memory).

## TDD Gate Compliance

This plan is the RED phase of the Phase 41 cycle. Plans 41-02 and 41-03 will land the GREEN gates. Per plan acceptance criteria, RED gate is now committed: 11 of 14 new facts fail RED in this plan's commits. The TDD discipline holds.

## Next Phase Readiness

- **Plan 41-02 ready** to land:
  - `Item Type Excludes` rename (3 source strings: `SerializerSettingsNodeProvider.cs:75`, `ItemTypeListScreen.cs:13`, `ItemTypeEditScreen.cs:131`)
  - Mode `[ConfigurableProperty]` hint copy (move from `explanation:` to `hint:` with the D-12 wording)
  - Will turn 4 tests GREEN: 2× GetScreenName facts, 1× tree-node-rename fact, 1× ModeProperty_HasHint fact.
- **Plan 41-03 ready** to land:
  - `XmlTypeDiscovery` DI seam + dual-list saved-exclusion merge
  - `ItemTypeEditScreen.CreateFieldSelector` saved-exclusion merge
  - `PredicateEditModel.Mode` typed as `string` + `SavePredicateCommand` validation gate + `PredicateByIndexQuery` enum→string conversion
  - Mode option-label cleanup (drop parens)
  - Will turn the remaining 7 RED facts GREEN.
- **No blockers, no architectural decisions deferred.**

---
*Phase: 41-admin-ui-polish*
*Completed: 2026-05-01*

## Self-Check: PASSED

Verification of claims in this SUMMARY:

- **Files exist:**
  - `tests/DynamicWeb.Serializer.Tests/AdminUI/XmlTypeEditScreenTests.cs` — FOUND
  - `tests/DynamicWeb.Serializer.Tests/AdminUI/ItemTypeEditScreenTests.cs` — FOUND
  - `tests/DynamicWeb.Serializer.Tests/AdminUI/PredicateCommandTests.cs` — FOUND (extended)
  - `tests/DynamicWeb.Serializer.Tests/AdminUI/SerializerSettingsNodeProviderModeTreeTests.cs` — FOUND (extended)
- **Commits exist (verified via `git log --oneline -4`):**
  - `4ff5b76` — FOUND
  - `d61ae84` — FOUND
  - `5cfe64e` — FOUND
- **Test counts (verified via `dotnet test --filter ~AdminUI`):**
  - Total: 109 (was 95 baseline + 14 new) ✓
  - Passed: 98 (95 baseline + 3 GREEN baselines) ✓
  - Failed: 11 (RED facts proving the documented bugs) ✓
  - Pre-existing AdminUI tests: still 95 passing — no regressions ✓
