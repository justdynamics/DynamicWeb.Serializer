---
phase: 36-area-screens
plan: 01
subsystem: admin-ui
tags: [predicate, area-columns, config, ui]
dependency_graph:
  requires: []
  provides: [ExcludeAreaColumns-property, area-column-selectmultidual]
  affects: [predicate-edit-screen, config-loader, save-predicate-command]
tech_stack:
  added: []
  patterns: [SelectMultiDual-for-column-exclusion, DTO-column-filtering]
key_files:
  created: []
  modified:
    - src/DynamicWeb.Serializer/Models/ProviderPredicateDefinition.cs
    - src/DynamicWeb.Serializer/Configuration/ConfigLoader.cs
    - src/DynamicWeb.Serializer/AdminUI/Models/PredicateEditModel.cs
    - src/DynamicWeb.Serializer/AdminUI/Queries/PredicateByIndexQuery.cs
    - src/DynamicWeb.Serializer/AdminUI/Commands/SavePredicateCommand.cs
    - src/DynamicWeb.Serializer/AdminUI/Screens/PredicateEditScreen.cs
    - tests/DynamicWeb.Serializer.Tests/AdminUI/PredicateCommandTests.cs
    - tests/DynamicWeb.Serializer.Tests/Configuration/ConfigLoaderTests.cs
decisions:
  - ExcludeAreaColumns added to Content branch only; SqlTable branch intentionally excluded
  - Six DTO-captured columns (AreaID, AreaName, AreaSort, AreaItemType, AreaItemId, AreaUniqueId) filtered from SelectMultiDual options
metrics:
  duration: 3min
  completed: "2026-04-15T12:04:00Z"
  tasks: 2
  files: 8
---

# Phase 36 Plan 01: Per-Predicate Area Column Exclusions Summary

ExcludeAreaColumns property added across model, config, UI, and command layers with SelectMultiDual populated from Area INFORMATION_SCHEMA minus 6 DTO-captured columns.

## Task Results

| Task | Name | Commit | Status |
|------|------|--------|--------|
| 1 | Add ExcludeAreaColumns to model, config, query, and save command | c1658ed | Done |
| 2 | Add SelectMultiDual for area columns on PredicateEditScreen + unit tests | 353de1d | Done |

## What Was Built

### Task 1: Model/Config/Command Layer
- Added `ExcludeAreaColumns` `List<string>` property to `ProviderPredicateDefinition`
- Added `ExcludeAreaColumns` to `RawPredicateDefinition` and `BuildPredicate()` in `ConfigLoader`
- Added `ExcludeAreaColumns` configurable property to `PredicateEditModel`
- Added mapping in `PredicateByIndexQuery.GetModel()`
- Added parsing in `SavePredicateCommand` Content branch only (SqlTable excluded per design)

### Task 2: UI + Tests
- Added "Area Column Filtering" group to Content predicate edit screen
- Added `CreateAreaColumnSelectMultiDual` method that queries `DataGroupMetadataReader.GetColumnTypes("Area")` and filters out 6 DTO-captured columns
- Added 3 PredicateCommand tests: round-trip, SqlTable exclusion, empty value
- Added 1 ConfigLoader test: JSON parsing of `excludeAreaColumns` array
- All 56 PredicateCommand + ConfigLoader tests pass

## Deviations from Plan

None - plan executed exactly as written.

## Verification

- `dotnet build` succeeds with 0 errors
- `dotnet test --filter "PredicateCommand|ConfigLoader"` passes all 56 tests
- ExcludeAreaColumns present on all 5 model/config/command layers
- SqlTable branch confirmed to NOT include ExcludeAreaColumns

## Self-Check: PASSED
