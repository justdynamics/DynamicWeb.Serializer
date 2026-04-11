---
phase: 33-sqltable-column-pickers
plan: 01
subsystem: AdminUI
tags: [checkbox-list, column-picker, predicate-edit, sqltable]
dependency_graph:
  requires: [DataGroupMetadataReader, DwSqlExecutor, PredicateEditScreen]
  provides: [CheckboxList editors for SqlTable ExcludeFields and XmlColumns]
  affects: [PredicateEditScreen.cs, PredicateCommandTests.cs]
tech_stack:
  added: []
  patterns: [conditional editor type based on ProviderType, INFORMATION_SCHEMA column introspection for UI]
key_files:
  created: []
  modified:
    - src/DynamicWeb.Serializer/AdminUI/Screens/PredicateEditScreen.cs
    - tests/DynamicWeb.Serializer.Tests/AdminUI/PredicateCommandTests.cs
decisions:
  - Model stays string (no type change) -- CheckboxList posts values that SavePredicateCommand parses via existing newline split
metrics:
  duration: 3min
  completed: "2026-04-11T00:07:10Z"
  tasks: 2
  files: 2
---

# Phase 33 Plan 01: SqlTable Column Pickers Summary

CheckboxList editors for SqlTable ExcludeFields and XmlColumns populated from INFORMATION_SCHEMA.COLUMNS via DataGroupMetadataReader, with pre-checked existing values and graceful handling of missing tables.

## What Was Done

### Task 1: Add CheckboxList editors for SqlTable ExcludeFields and XmlColumns
**Commit:** `c62ccbd`

Added `CreateColumnCheckboxList` private helper to `PredicateEditScreen` that:
- Instantiates `DataGroupMetadataReader(new DwSqlExecutor())` to query column names from INFORMATION_SCHEMA.COLUMNS
- Returns sorted `ListOption` items (column name as both Value and Label)
- Pre-parses existing model value (newline-separated string) and sets as `editor.Value` for pre-checked state
- Handles empty table name with "Enter a table name" message
- Handles table-not-found (0 columns) with "Table not found in database" warning
- Wraps DB call in try/catch with "Could not query database columns" fallback

Updated `GetEditor` switch to conditionally return CheckboxList for `ExcludeFields` and `XmlColumns` when `ProviderType == "SqlTable"`, preserving Textarea for Content predicates.

Added `using DynamicWeb.Serializer.Providers.SqlTable` import.

**Files modified:** `PredicateEditScreen.cs` (+66 lines, -10 lines)

### Task 2: Add round-trip tests for CheckboxList-style value persistence
**Commit:** `efda64a`

Added 5 new test methods to `PredicateCommandTests`:
1. `Save_SqlTable_ExcludeFields_RoundTrips` -- 3 column names persist correctly
2. `Save_SqlTable_XmlColumns_RoundTrips` -- 2 XML column names persist correctly
3. `Save_SqlTable_EmptyFilteringFields_PersistsAsEmptyLists` -- empty strings become empty lists
4. `Save_SqlTable_UpdateExisting_PreservesFilteringFields` -- update from 1 to 3 columns works
5. `Save_Content_ExcludeFields_StillWorksWithNewlines` -- Content predicate regression guard with \r\n

All 25 PredicateCommand tests pass. Full suite: 365 pass, 4 pre-existing failures (unrelated SqlTableProviderDeserialize and SaveSerializerSettings tests).

**Files modified:** `PredicateCommandTests.cs` (+156 lines)

## Deviations from Plan

None - plan executed exactly as written.

## Verification Results

1. `dotnet build src/DynamicWeb.Serializer` -- 0 errors, 21 warnings (all pre-existing)
2. `dotnet test --filter "PredicateCommand"` -- 25 passed, 0 failed
3. `dotnet test` (full suite) -- 365 passed, 4 failed (pre-existing, unrelated)

## Self-Check: PASSED

- [x] `PredicateEditScreen.cs` exists and contains `CreateColumnCheckboxList`
- [x] `PredicateEditScreen.cs` contains `new CheckboxList` and `GetColumnTypes`
- [x] `PredicateEditScreen.cs` contains `using DynamicWeb.Serializer.Providers.SqlTable`
- [x] `PredicateEditScreen.cs` still contains `new Textarea` for non-SqlTable branch
- [x] `PredicateEditScreen.cs` contains "Table not found in database" warning
- [x] `PredicateEditModel.cs` NOT modified
- [x] `SavePredicateCommand.cs` NOT modified
- [x] Commit c62ccbd exists
- [x] Commit efda64a exists
