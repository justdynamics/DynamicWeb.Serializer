---
phase: 29-sqltable-field-filtering
plan: 01
subsystem: serialization
tags: [field-filtering, sqltable, excludeFields, excludeXmlElements, deserialize-guard]
dependency_graph:
  requires: [Phase 28 ExcludeFields/ExcludeXmlElements on ProviderPredicateDefinition, Phase 27 xmlColumns config, XmlFormatter.RemoveElements]
  provides: [SqlTable serialize-side excludeFields filtering, SqlTable serialize-side excludeXmlElements filtering, deserialize skip guard verification]
  affects: [SqlTableProvider, FlatFileStore YAML output]
tech_stack:
  added: []
  patterns: [HashSet excludeFields filtering before WriteRow, XmlFormatter.RemoveElements on xmlColumns for excludeXmlElements]
key_files:
  created:
    - tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/SqlTableProviderSerializeTests.cs
  modified:
    - src/DynamicWeb.Serializer/Providers/SqlTable/SqlTableProvider.cs
    - tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/SqlTableProviderDeserializeTests.cs
decisions:
  - excludeFields filtering applied after XML pretty-print and XML element stripping, before WriteRow
  - Deserialize skip guard verified via YAML round-trip + BuildMergeCommand SQL test (provider-level test skipped due to pre-existing SqlTableProviderDeserialize test failures)
metrics:
  duration: 5min
  completed: 2026-04-07
  tasks: 2
  files: 3
---

# Phase 29 Plan 01: SqlTable Field Filtering Summary

ExcludeFields and excludeXmlElements filtering in SqlTableProvider.Serialize with HashSet column removal and XmlFormatter.RemoveElements, plus deserialize skip guard verification via YAML round-trip and BuildMergeCommand SQL assertion.

## Performance

- **Duration:** 5 min
- **Started:** 2026-04-07T20:09:50Z
- **Completed:** 2026-04-07T20:14:49Z
- **Tasks:** 2
- **Files modified:** 3

## Accomplishments

### Task 1: Serialize-side excludeFields + excludeXmlElements
- Added `excludeFields` HashSet built from `predicate.ExcludeFields` (case-insensitive)
- Added Step 2 in serialize loop: `XmlFormatter.RemoveElements` on xmlColumns when `ExcludeXmlElements` configured
- Added Step 3 in serialize loop: `row.Remove(field)` for each excluded field before `WriteRow`
- Order preserved: (1) PrettyPrint, (2) RemoveElements, (3) excludeFields removal
- 3 new tests: ExcludeFields_OmitsColumns, ExcludeXmlElements_StripsElements, NoExcludeFields_AllColumnsPresent
- **Commit:** 9249571

### Task 2: Deserialize skip guard verification
- YAML round-trip test: writes row with 2 of 3 columns, reads back, confirms absent column stays absent
- BuildMergeCommand SQL test: row missing column produces MERGE SQL without that column name
- These two tests prove the full skip guard chain: serialize excludes column from YAML, deserialize reads partial row, MERGE SQL omits absent columns
- **Commit:** 637ff5c

## Verification Results

| Check | Result |
|-------|--------|
| `dotnet build --no-restore` | 0 errors (pre-existing warnings only) |
| Phase29 tests (5 total) | All pass |
| excludeFields in SqlTableProvider.cs | 3 references |
| RemoveElements in SqlTableProvider.cs | 1 reference |
| ExcludeFields in SerializeTests | 4 references |

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Provider-level deserialize test replaced with YAML round-trip + SQL-level tests**
- **Found during:** Task 2
- **Issue:** SqlTableProviderDeserializeTests.CreateProviderWithFiles has pre-existing failures (TableExists triggers CreateTableFromMetadata which fails on empty ColumnDefinitions -- 4 pre-existing test failures documented in Phase 27 summary)
- **Fix:** Replaced the provider-level Deserialize_ExcludedFields_NotPassedToWriter test with two targeted tests: (1) YAML round-trip via FlatFileStore proving absent columns stay absent, (2) BuildMergeCommand SQL assertion proving absent columns excluded from MERGE. Together these prove the same skip guard without depending on the broken provider-level test infrastructure.
- **Files modified:** tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/SqlTableProviderDeserializeTests.cs

## Self-Check: PASSED
