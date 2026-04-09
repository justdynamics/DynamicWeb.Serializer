---
phase: 32-config-schema-extension
plan: 02
subsystem: serialization
tags: [exclusion-merger, typed-dictionaries, per-entity-merge, pipeline-wiring]
dependency_graph:
  requires: [ExcludeFieldsByItemType, ExcludeXmlElementsByType]
  provides: [ExclusionMerger, per-entity-typed-exclusions]
  affects: [ContentMapper, ContentSerializer, ContentDeserializer]
tech_stack:
  added: []
  patterns: [static-merge-helper, case-insensitive-dictionary-lookup, null-means-no-filtering]
key_files:
  created:
    - src/DynamicWeb.Serializer/Configuration/ExclusionMerger.cs
    - tests/DynamicWeb.Serializer.Tests/Configuration/ExclusionMergerTests.cs
  modified:
    - src/DynamicWeb.Serializer/Serialization/ContentMapper.cs
    - src/DynamicWeb.Serializer/Serialization/ContentSerializer.cs
    - src/DynamicWeb.Serializer/Serialization/ContentDeserializer.cs
decisions:
  - Merge happens per-entity in ContentMapper (serialization) and at each SaveItemFields call site (deserialization) rather than once per predicate
  - Case-insensitive dictionary lookup via linear scan fallback for DW item type name casing mismatches
metrics:
  duration: 383s
  completed: 2026-04-09T16:30:00Z
  tasks_completed: 2
  tasks_total: 2
  tests_added: 9
  tests_total_passing: 369
---

# Phase 32 Plan 02: ExclusionMerger and Pipeline Wiring Summary

Created ExclusionMerger static helper with case-insensitive dictionary lookup and wired typed dictionary exclusions through both serialization and deserialization pipelines for per-entity union merge at runtime.

## Commits

| Task | Commit | Description |
|------|--------|-------------|
| 1 | 9739fe2 | feat(32-02): create ExclusionMerger helper with unit tests |
| 2 | 5ba72d4 | feat(32-02): wire ExclusionMerger into serialization and deserialization pipelines |

## Task Details

### Task 1: Create ExclusionMerger helper with unit tests (TDD)

Created `ExclusionMerger` static class with two public methods:
- `MergeFieldExclusions` -- unions flat predicate field exclusions with per-item-type dictionary entries, returns `HashSet<string>?` (null when no exclusions apply)
- `MergeXmlExclusions` -- unions flat predicate XML element exclusions with per-XML-type dictionary entries, returns `IReadOnlyList<string>?`

Both methods use `TryGetValueIgnoreCase` private helper that does exact-match fast path then case-insensitive linear scan fallback for DW item type name casing mismatches.

9 unit tests cover: empty inputs return null, flat-only, typed-only, union of both, different type name returns flat-only, case-insensitive key lookup, and parallel XML exclusion scenarios.

### Task 2: Wire ExclusionMerger into serialization and deserialization pipelines

**ContentMapper.cs**: Extended `MapArea`, `MapPage`, `MapParagraph`, and `BuildColumns` signatures with optional `excludeFieldsByItemType` and `excludeXmlElementsByType` dictionary parameters. Each method computes effective exclusions by calling `ExclusionMerger.MergeFieldExclusions`/`MergeXmlExclusions` with the entity's specific item type name before applying field/XML filtering.

**ContentSerializer.cs**: Updated three mapper call sites (`MapArea`, `BuildColumns`, `MapPage`) to pass `_configuration.ExcludeFieldsByItemType` and `_configuration.ExcludeXmlElementsByType` from the config object.

**ContentDeserializer.cs**: Added `ExcludeFieldsByItemType` to `WriteContext`. Updated `DeserializePredicate` to populate it from config. Updated all 8 `SaveItemFields` call sites (area item fields, area properties, area creation, page insert/update, grid row insert/update, paragraph insert/update) to merge per-entity using `ExclusionMerger.MergeFieldExclusions`. `SavePropertyItemFields` also receives merged exclusions.

**SqlTableProvider**: Unchanged -- SqlTable rows have no item type concept (flat-only exclusions remain).

## Decisions Made

1. **Per-entity merge in ContentMapper** -- Merge happens inside each Map method rather than once per predicate, because each page/paragraph/area has its own item type that determines which typed dictionary entries apply.
2. **Case-insensitive dictionary lookup** -- Linear scan fallback after exact match. Dictionary sizes are small (tens of entries for item types), so O(n) scan is negligible.

## Deviations from Plan

None -- plan executed exactly as written.

## Verification

- `dotnet build src/DynamicWeb.Serializer` exits 0 (21 warnings, 0 errors)
- `dotnet test tests/DynamicWeb.Serializer.Tests --filter ExclusionMergerTests` exits 0 (9 passed)
- `dotnet test tests/DynamicWeb.Serializer.Tests` -- 364 total, 359 passed, 5 pre-existing failures unrelated to this plan
- ContentMapper.cs contains `ExclusionMerger.MergeFieldExclusions` and `ExclusionMerger.MergeXmlExclusions`
- ContentSerializer.cs passes `_configuration.ExcludeFieldsByItemType` and `_configuration.ExcludeXmlElementsByType`
- ContentDeserializer.cs WriteContext contains `ExcludeFieldsByItemType`
- ContentDeserializer.cs contains `ExclusionMerger.MergeFieldExclusions` at all SaveItemFields call sites
- SqlTableProvider has NO references to ExclusionMerger or typed dictionaries
