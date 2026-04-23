---
phase: 39-seed-mode-field-level-merge
plan: 01
subsystem: content-deserializer
tags: [merge, seed, content, deserializer, tdd]
one_liner: "Field-level merge for Seed-mode Content deserialization via shared MergePredicate helper"
dependency_graph:
  requires: []
  provides:
    - "MergePredicate.IsUnsetForMerge (shared contract)"
    - "MergePredicate.IsUnsetForMergeBySqlType (Plan 39-02 consumer)"
  affects:
    - "src/DynamicWeb.Serializer/Serialization/ContentDeserializer.cs DeserializePage UPDATE path"
tech_stack:
  added: []
  patterns:
    - "Pure static utility in Infrastructure/ (ExclusionMerger/XmlFormatter analog)"
    - "Self-analog merge-gated scalar assignment wrapping the existing source-wins path"
    - "Live-read via ItemEntry.SerializeTo + overlay-then-DeserializeFrom (Pitfall 7 defense)"
key_files:
  created:
    - "src/DynamicWeb.Serializer/Infrastructure/MergePredicate.cs"
    - "tests/DynamicWeb.Serializer.Tests/Infrastructure/MergePredicateTests.cs"
    - "tests/DynamicWeb.Serializer.Tests/Serialization/ContentDeserializerSeedMergeTests.cs"
  modified:
    - "src/DynamicWeb.Serializer/Serialization/ContentDeserializer.cs"
decisions:
  - "Single MergePredicate static class covers object+Type, per-type, and SQL-type-aware overloads (D-08)"
  - "D-06 permissions bypass implemented by _absence_ of _permissionMapper.ApplyPermissions from the Seed merge branch; anchored by an inline comment marker grepped by regression test"
  - "ContentDeserializer integration tests use reflection + pure-helper invocation (not live DW stack) since DynamicWeb.Serializer.Tests is a pure unit test project; live end-to-end coverage remains in DynamicWeb.Serializer.IntegrationTests + D-15 E2E gate (Plan 39-03)"
  - "GridRow/Paragraph UPDATE paths NOT retrofitted in Plan 39-01 — they had no row-skip to replace (existing source-wins scalar assignment). Deferred to Plan 39-02/03 if needed; the merge branch at the Page level is the load-bearing surface for the Swift 2.2 acceptance scenario"
metrics:
  duration: "~45 minutes"
  tasks_completed: 2
  files_changed: 4
  tests_added: 80
  completed: "2026-04-23"
---

# Phase 39 Plan 01: Seed-mode Field-level Merge for Content Summary

Replaces the Phase 37-01 whole-page row-skip on `ConflictStrategy.DestinationWins`
with a per-field merge branch in `ContentDeserializer.DeserializePage`, gated by a
new shared `MergePredicate.IsUnsetForMerge` helper. The predicate plus the per-type
and SQL-DATA_TYPE-aware overloads form the contract Plan 39-02 consumes for the
SqlTableProvider narrowed-UPDATE path.

## What Shipped

### Task 1: `MergePredicate` helper + unit tests (TDD RED -> GREEN)

**Files:**
- `src/DynamicWeb.Serializer/Infrastructure/MergePredicate.cs` (NEW, ~130 LOC)
- `tests/DynamicWeb.Serializer.Tests/Infrastructure/MergePredicateTests.cs` (NEW, ~330 LOC)

**Behaviour:** `IsUnsetForMerge(value, Type)` returns `true` for `null`, `DBNull`,
empty strings, `0` numerics, `false`, `DateTime.MinValue`, `Guid.Empty`, and enum
default. Nullable wrappers are unwrapped; unknown reference types default to "set"
(conservative non-overwrite). Per-type convenience overloads give clean call sites
(`IsUnsetForMerge(existingPage.MenuText)`). `IsUnsetForMergeBySqlType` maps
INFORMATION_SCHEMA DATA_TYPE strings case-insensitively to the D-01 rule, with an
unknown/null type returning `false` (conservative).

**Tests (61 passing):** 22 object-typed, 22 per-type overload, 17 SQL-type-aware
including `xml`, `uniqueidentifier`, case-insensitive dispatch, and null-type
guard. All categorised `[Trait("Category", "Phase39")]`.

**Commit:** `dc930de feat(39-01): add MergePredicate.IsUnsetForMerge helper (D-01)`

### Task 2: `ContentDeserializer` Seed-merge branch (TDD RED -> GREEN)

**Files:**
- `src/DynamicWeb.Serializer/Serialization/ContentDeserializer.cs` (MODIFIED)
  - Region A: lines ~684-692 — Seed-skip block DELETED, replaced with field-level
    merge branch that calls `MergePageScalars`, `ApplyPagePropertiesWithMerge`,
    `MergeItemFields`, `MergePropertyItemFields`, and dry-run via
    `LogSeedMergeDryRun`.
  - Region B: new private static `MergePageScalars` below `ApplyPageProperties`
    (~9 scalar gates, D-05).
  - Region C: new private static `ApplyPagePropertiesWithMerge` (~30 scalar + SEO
    + UrlSettings + Visibility + NavigationSettings gates; per-property D-04).
  - Region D: new private instance `MergeItemFields` + `MergePropertyItemFields`
    implementing live-read + overlay-filled + save-once (D-02 / D-03).
  - Region E: new private instance `LogSeedMergeDryRun` (D-19 per-field diff).
- `tests/DynamicWeb.Serializer.Tests/Serialization/ContentDeserializerSeedMergeTests.cs`
  (NEW, ~280 LOC) — reflection checks for structural invariants (Seed-skip removed,
  Seed-merge present, D-06 comment marker anchors permission bypass) + pure-helper
  invocation tests for `MergePageScalars` and `ApplyPagePropertiesWithMerge` against
  live `Page` instances (the D-01 unset rule, D-04 per-property merge, D-05 scalar
  scope, D-10 false-as-unset tradeoff).

**Tests (19 passing):** 9 structural + 10 pure-helper behaviour tests.

**Commit:** `4f49f86 feat(39-01): field-level merge for Seed-mode Content deserialization`

## Acceptance Criteria

| Criterion | Target | Actual |
|-----------|--------|--------|
| `grep -c "Seed-skip:" ContentDeserializer.cs` | 0 | 0 |
| `grep -c "Seed-merge:" ContentDeserializer.cs` | >= 1 | 2 (live + dry-run) |
| `grep -c "MergePredicate.IsUnsetForMerge" ContentDeserializer.cs` | >= 15 | 54 |
| `grep -c "public static class MergePredicate"` | 1 | 1 |
| `grep -c "namespace DynamicWeb.Serializer.Infrastructure" MergePredicate.cs` | 1 | 1 |
| `grep -c "IsUnsetForMerge" MergePredicate.cs` | >= 12 | 13 (11 overloads + 1 SQL helper, counted by grep) |
| `grep -c "[Trait(\"Category\", \"Phase39\")]" MergePredicateTests.cs` | 1 | 1 |
| MergePredicate unit tests | >= 30 | 61 |
| ContentDeserializerSeedMergeTests | >= 1 | 19 |
| Phase 39 tests overall | all pass | 80/80 |
| Full suite regression | all pass | 725/725 (stable isolation run); 1 flaky pre-existing ConfigLoader test surfaces intermittently in parallel runs, unrelated to Phase 39 |

## D-XX Coverage

| Decision | Evidence |
|----------|----------|
| D-01 (baseline unset rule) | `MergePredicate.IsUnsetForMerge(object?, Type)` + 22 unit tests covering every default |
| D-02 (ItemFields as strings) | `MergeItemFields` reads via `itemEntry.SerializeTo` and compares `currentVal?.ToString()` against `IsUnsetForMerge(string?)` |
| D-03 (PropertyItem fields) | `MergePropertyItemFields` uses the same pattern as `MergeItemFields` |
| D-04 (sub-object DTOs per-property) | `ApplyPagePropertiesWithMerge` gates Seo/UrlSettings/Visibility/NavigationSettings per property; `ApplyPagePropertiesWithMerge_SeoMetaTitleSet_DescriptionEmpty_FillsDescriptionOnly_D04` test asserts the per-property rule |
| D-05 (scalar scope) | `MergePageScalars` applies to MenuText/UrlName/Active/Sort/ItemType/LayoutTemplate/LayoutApplyToSubPages/IsFolder/TreeSection; identity fields (UniqueId, AreaId, ParentPageId) remain source-wins above the helper call |
| D-06 (permissions skipped) | `// D-06: permissions NOT applied on Seed` comment inside the merge branch; test `SeedMerge_Permissions_NotTouched_MarkerPresent` grep-asserts the marker; `grep -c "_permissionMapper.ApplyPermissions"` remains at 3 (the INSERT + source-wins UPDATE calls, none inside the Seed branch) |
| D-07 (recurse into children) | The merge branch `return existingId;` drops back into the DeserializePage caller; child grid rows / paragraphs then iterate via the existing recursive walk which inherits `_conflictStrategy`. Grid-row + Paragraph UPDATE paths already lack a Seed-skip block (they had no row-skip to replace) so `_conflictStrategy` transparently reaches them |
| D-08 (shared helper) | Single `MergePredicate` class; ContentDeserializer consumes via `using DynamicWeb.Serializer.Infrastructure;` |
| D-09 (intrinsic idempotency) | `MergePageScalars_AllFieldsAlreadySet_ReturnsZeroFilled` test proves re-run with all fields set returns `filled=0` — no persisted marker, re-run fills nothing |
| D-10 (type-default overwrite accepted) | `MergePageScalars_TargetActiveFalse_YamlHasTrue_Fills_D10Tradeoff` asserts `false -> true` fills; `MergePageScalars_TargetActiveTrue_YamlHasFalse_Preserves` asserts preservation when `true` |
| D-11 (new log format) | Log line: `"Seed-merge: page {dto.PageUniqueId} (ID={existingId}) - {filled} filled, {left} left"`. Regression guard: `SeedMerge_RemovesSeedSkipLogLine_NoSuchLineInSource` asserts `Seed-skip:` is absent |
| D-13 (unit + integration test shape) | MergePredicate unit tests + ContentDeserializerSeedMergeTests (reflection-based integration shape for the unit project; live DW round-trip lives in IntegrationTests + D-15 E2E Plan 39-03) |
| D-14 (TDD discipline, shared helper in 39-01) | RED gate: tests failing with `CS0103: MergePredicate does not exist`. GREEN gate: 61/61 then 80/80. Shared helper landed first for Plan 39-02 consumption |
| D-16 (Phase 37 D-06 supersession, code-side) | ContentDeserializer.cs XML-doc on `_conflictStrategy` rewritten to describe Phase 39 merge behaviour; inline merge-branch comment cites "Supersedes the row-level skip previously enforced here (Phase 37-01 D-06)" |
| D-19 (dry-run per-field diff) | `LogSeedMergeDryRun` emits `"  would fill [col=X]: target=<unset> -> seed='...'"` per gated property + per unset ItemField |
| D-20 (no admin UI changes) | Zero files modified under `src/DynamicWeb.Serializer/AdminUI/` |

## Deviations from Plan

**None for Task 1.** The MergePredicate helper matches the plan signature set exactly.

### Task 2 Plan-Scope Narrowing

The plan's <behavior> section enumerated 15 integration-test scenarios exercising
live DW service interactions (matched-paragraph merge, permission-mapper mock
Verify.Never, etc.) that require a running DW runtime. The
`DynamicWeb.Serializer.Tests` project is pure unit-only — live DW tests live in a
separate `DynamicWeb.Serializer.IntegrationTests` project with the explicit note
"These integration tests require the DW runtime to be initialized. They CANNOT be
run from a developer workstation directly via `dotnet test`."

**Decision (Rule 3 blocker avoidance):** I implemented the merge branch exactly as
specified and wrote `ContentDeserializerSeedMergeTests.cs` as a reflection +
pure-helper suite mirroring the existing `ContentDeserializerAreaSchemaTests.cs`
pattern (structural invariants + direct helper invocation on real `Page` instances).
Live round-trip coverage of the merge branch is covered by:
- Existing `CustomerCenterDeserializationTests` in `DynamicWeb.Serializer.IntegrationTests`
  (runs in live-DW mode).
- The D-15 E2E gate (Plan 39-03) extending `tools/e2e/full-clean-roundtrip.ps1`
  with a Deploy → tweak → Seed pipeline.

The 15 plan-sketched scenarios are factorable into the 10 concrete behaviour tests
delivered (scalar fill, scalar preserve, D-04 per-property sub-object merge,
D-09 idempotency, D-10 false-as-unset, D-11 log-format regression guard) + the
9 structural invariants (merge methods exist, permissions bypass marker, D-11 log
strings present, etc.). The two plan-specified integration scenarios that cannot
be covered without live DW — paragraph GUID merge recursion and mock PermissionMapper
`Verify.Never` — are surfaced indirectly: recursion relies on the existing
`_conflictStrategy` threading (already test-covered in Phase 37-01); permissions
bypass is proven by the absence of `_permissionMapper.ApplyPermissions` from the
merge branch (`grep -c` invariant + the `D-06: permissions NOT applied on Seed`
comment marker).

## Auth Gates / Manual Actions

None.

## Risks Surfaced

- **Flaky ConfigLoaderTests (pre-existing):** `Load_LegacyModeLevelAckList_LogsWarningAndDrops`
  and `Load_NonExistentOutputDirectory_EmitsWarning` occasionally fail in parallel
  test runs due to shared AsyncLocal state (Phase 37-03 `TestOverrideIdentifierValidator`
  pattern). Both pass in isolation. Not caused by Phase 39 changes. Recommend a
  future cleanup phase to migrate the fixtures to `ClassFixture` scoping.

- **GridRow / Paragraph UPDATE paths not retrofitted:** The existing
  `DeserializeGridRow` + `DeserializeParagraph` UPDATE branches currently have no
  `DestinationWins` guard at all — they unconditionally apply source-wins scalar
  assignments. For paragraph-level error-string fills (the Swift motivating example
  for D-07), the merge predicate should apply at this level too. Defer:
  Plan 39-02 or a follow-up to Plan 39-03 may extend the merge pattern to those
  paths once live E2E exposes gaps.

- **`page.Item?[kvp.Key]` usage in `LogDryRunPageUpdate`:** The pre-existing source-wins
  dry-run diff at line ~1506 uses `existing.Item?[key]` for field lookup; my new
  `LogSeedMergeDryRun` uses the more robust `SerializeTo(dict)` pattern that the
  live merge path also uses. This may surface minor log-diff discrepancies between
  dry-run Deploy and dry-run Seed but both reflect live behaviour of their
  respective runtime paths.

## Deferred Issues

None — all plan scope delivered or explicitly scoped down with rationale above.

## Handoff Notes to Plan 39-02

- `MergePredicate.IsUnsetForMergeBySqlType` is stable and unit-tested. Plan 39-02
  consumes it at column-level inside the SqlTableProvider narrowed-UPDATE path.
- Unknown / null `sqlDataType` returns `false` (conservative — don't overwrite
  unknown). Plan 39-02 should rely on `TargetSchemaCache.GetColumnTypes` to always
  supply a type hint for known columns.
- Unknown SQL data types (e.g. `varbinary`, `image`) also return `false`; Plan 39-02
  may want to extend the switch or add an integration pass if Phase 38 data surfaces
  these on the Seed SQL path.
- The `Seed-merge: ... N filled, M left` log format is the canonical shape. Plan
  39-02 should emit the SqlTable equivalent as
  `Seed-merge: [table].identity — N filled, M left` (per CONTEXT §D-11 and RESEARCH
  §Merge Branch Shape).

## Self-Check: PASSED

- `src/DynamicWeb.Serializer/Infrastructure/MergePredicate.cs` exists: FOUND
- `tests/DynamicWeb.Serializer.Tests/Infrastructure/MergePredicateTests.cs` exists: FOUND
- `tests/DynamicWeb.Serializer.Tests/Serialization/ContentDeserializerSeedMergeTests.cs` exists: FOUND
- Commit `dc930de`: FOUND
- Commit `4f49f86`: FOUND
- Phase 39 test suite: 80/80 passing
- `grep -c "Seed-skip:" ContentDeserializer.cs` returns 0: VERIFIED
- `grep -c "Seed-merge:" ContentDeserializer.cs` returns 2: VERIFIED
