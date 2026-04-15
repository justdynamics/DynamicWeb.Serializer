---
phase: 36-area-screens
plan: 02
subsystem: serialization
tags: [area-columns, exclude, serialization, deserialization, sql]
dependency_graph:
  requires: [ExcludeAreaColumns-property]
  provides: [area-column-exclusion-pipeline, ReadAreaProperties, WriteAreaProperties]
  affects: [ContentSerializer, ContentMapper, ContentDeserializer, SerializedArea]
tech_stack:
  added: []
  patterns: [SQL-SELECT-star-with-exclude-filter, CommandBuilder-parameterized-UPDATE]
key_files:
  created: []
  modified:
    - src/DynamicWeb.Serializer/Models/SerializedArea.cs
    - src/DynamicWeb.Serializer/Serialization/ContentSerializer.cs
    - src/DynamicWeb.Serializer/Serialization/ContentMapper.cs
    - src/DynamicWeb.Serializer/Serialization/ContentDeserializer.cs
decisions:
  - ExcludeAreaColumns is separate from ExcludeFields; only applies to Area SQL table columns
  - ReadAreaProperties reads all Area columns via SELECT * and filters by excludeAreaColumns
  - WriteAreaProperties uses parameterized CommandBuilder UPDATE, skipping excluded columns
  - Properties dictionary added to SerializedArea for full Area SQL column round-trip
metrics:
  duration: 9min
  completed: "2026-04-15T12:16:00Z"
  tasks: 2
  files: 4
---

# Phase 36 Plan 02: Area Column Exclusion Pipeline Summary

ExcludeAreaColumns wired end-to-end: predicate config builds exclude set, ContentMapper.ReadAreaProperties filters columns during serialization, ContentDeserializer.WriteAreaProperties skips excluded columns during deserialization.

## Task Results

| Task | Name | Commit | Status |
|------|------|--------|--------|
| 1 | Wire ExcludeAreaColumns into serialization pipeline | 9a4c465 | Done |
| 2 | Wire ExcludeAreaColumns into deserialization pipeline | 451358f | Done |

## Implementation Details

### Task 1: Serialization Pipeline
- Added `Properties` dictionary to `SerializedArea` model for Area SQL column data
- Added `ReadAreaProperties` method to `ContentMapper` -- reads all columns from `[Area]` table via `SELECT *`, filters by `excludeAreaColumns`, removes 6 DTO-captured columns (AreaID, AreaName, AreaSort, AreaItemType, AreaItemId, AreaUniqueId)
- Added `excludeAreaColumns` parameter to `ContentMapper.MapArea`
- `ContentSerializer.SerializePredicate` builds `excludeAreaColumns` HashSet from `predicate.ExcludeAreaColumns` and passes to MapArea

### Task 2: Deserialization Pipeline
- Added `WriteAreaProperties` method to `ContentDeserializer` -- parameterized SQL UPDATE that skips excluded columns
- `DeserializePredicate` builds `excludeAreaColumnsSet` from predicate config, calls `WriteAreaProperties` before ItemType field processing
- `excludeAreaColumnsSet` is NOT added to `WriteContext` -- area column exclusions stay separate from page/paragraph/gridrow processing

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] ReadAreaProperties and WriteAreaProperties methods missing from codebase**
- **Found during:** Task 1 analysis
- **Issue:** Plan interfaces described `ReadAreaProperties`, `WriteAreaProperties`, `CreateAreaFromProperties`, `ExclusionMerger`, and exclude parameters on MapArea -- none of which exist in the current codebase. These were implemented in prior phases (30-32) but were lost during a merge.
- **Fix:** Created `ReadAreaProperties` in ContentMapper and `WriteAreaProperties` in ContentDeserializer with the ExcludeAreaColumns-specific filtering. Added `Properties` dictionary to `SerializedArea`. Did NOT restore the full ExcludeFields/ExclusionMerger infrastructure (out of scope for this plan).
- **Files modified:** All 4 modified files
- **Commits:** 9a4c465, 451358f

**2. [Rule 3 - Blocking] CreateAreaFromProperties not implemented**
- **Found during:** Task 2 analysis
- **Issue:** Plan references merging excludeAreaColumnsSet into `CreateAreaFromProperties` call, but this method doesn't exist in the current codebase and the area-not-found path returns early without creating areas.
- **Fix:** Skipped -- area creation from properties requires architectural decisions about when to auto-create areas. The core WriteAreaProperties path (updating existing areas) is fully implemented with ExcludeAreaColumns filtering.
- **Impact:** None for existing functionality; areas must pre-exist in the target DB.

## Verification

- `dotnet build` succeeds with 0 errors
- 86/86 config and predicate tests pass
- Pre-existing failures (6 SqlTableProvider, 2 SaveSerializerSettings) unrelated to changes

## Self-Check: PASSED
