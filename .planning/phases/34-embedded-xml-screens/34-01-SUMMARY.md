---
phase: 34-embedded-xml-screens
plan: 01
subsystem: admin-ui
tags: [xml-discovery, selectmultidual, admin-screens, sql-query]
dependency_graph:
  requires: [ISqlExecutor, DataGroupMetadataReader, PredicateEditScreen]
  provides: [XmlTypeDiscovery, XmlTypeListModel, XmlTypeEditModel]
  affects: [PredicateEditScreen]
tech_stack:
  added: [SelectMultiDual]
  patterns: [ISqlExecutor-injection, XDocument-parse-with-catch, regex-sql-guard]
key_files:
  created:
    - src/DynamicWeb.Serializer/AdminUI/Infrastructure/XmlTypeDiscovery.cs
    - src/DynamicWeb.Serializer/AdminUI/Models/XmlTypeListModel.cs
    - src/DynamicWeb.Serializer/AdminUI/Models/XmlTypeEditModel.cs
    - tests/DynamicWeb.Serializer.Tests/AdminUI/XmlTypeDiscoveryTests.cs
  modified:
    - src/DynamicWeb.Serializer/AdminUI/Screens/PredicateEditScreen.cs
decisions:
  - SelectMultiDual with .ToArray() for Value binding (per ScreenPresetEditScreen pattern)
  - Regex typeName validation as defense-in-depth before SQL embedding
metrics:
  duration: 4min
  completed: 2026-04-14T08:59:37Z
  tasks_completed: 2
  tasks_total: 2
  files_created: 4
  files_modified: 1
---

# Phase 34 Plan 01: XmlTypeDiscovery + Models + SelectMultiDual Retrofit Summary

XmlTypeDiscovery service queries Page/Paragraph tables for distinct XML type names and parses live XML blobs for root element discovery, with regex input validation and malformed-XML resilience; PredicateEditScreen CheckboxList controls replaced with SelectMultiDual.

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | XmlTypeDiscovery service + models + tests (TDD) | b580955, 5963f09 | XmlTypeDiscovery.cs, XmlTypeListModel.cs, XmlTypeEditModel.cs, XmlTypeDiscoveryTests.cs |
| 2 | Replace CheckboxList with SelectMultiDual (D-02) | 9e3edbf | PredicateEditScreen.cs |

## Implementation Details

### Task 1: XmlTypeDiscovery + Models

Created `XmlTypeDiscovery` class in `AdminUI.Infrastructure` namespace with constructor-injected `ISqlExecutor` (same pattern as `DataGroupMetadataReader`).

**DiscoverXmlTypes():** Queries `SELECT DISTINCT PageUrlDataProviderType FROM Page` and `SELECT DISTINCT ParagraphModuleSystemName FROM Paragraph`, returns case-insensitive `HashSet<string>` deduplicating across both tables.

**DiscoverElementsForType(typeName):** Validates typeName with `^[A-Za-z0-9_.]+$` regex (T-34-01 mitigation), queries `TOP 50` XML blobs from both tables, parses with `XDocument.Parse()`, extracts root child element local names into case-insensitive HashSet. Catches `XmlException` silently for malformed blobs (T-34-02 mitigation).

**Models:** `XmlTypeListModel` (TypeName, ExcludedElementCount) and `XmlTypeEditModel` (TypeName, ExcludedElements as newline-separated string) follow existing PredicateListModel/PredicateEditModel patterns.

**Tests:** 9 xUnit facts using `FakeSqlExecutor` with `DataTable.CreateDataReader()` covering: type discovery from Page/Paragraph, deduplication, empty filtering, element extraction, malformed XML skip, case-insensitive deduplication, empty result, and SQL injection rejection.

### Task 2: SelectMultiDual Retrofit (D-02)

Replaced `CreateColumnCheckboxList` with `CreateColumnSelectMultiDual` on `PredicateEditScreen`. Key changes:
- `CheckboxList` -> `SelectMultiDual` control type
- `.ToList()` -> `.ToArray()` for Value binding (per ScreenPresetEditScreen pattern, Pitfall 3 from research)
- Both ExcludeFields and XmlColumns call sites updated
- All existing SQL injection validation and error handling preserved

## Deviations from Plan

None - plan executed exactly as written.

## Verification Results

- `dotnet build src/DynamicWeb.Serializer` -- 0 errors, 22 warnings (pre-existing)
- `dotnet test --filter "XmlTypeDiscovery"` -- 9/9 passed
- `dotnet test --filter "Predicate"` -- 74/74 passed
- `PredicateEditScreen.cs` contains zero references to `CheckboxList`

## Self-Check: PASSED

- [x] `src/DynamicWeb.Serializer/AdminUI/Infrastructure/XmlTypeDiscovery.cs` exists
- [x] `src/DynamicWeb.Serializer/AdminUI/Models/XmlTypeListModel.cs` exists
- [x] `src/DynamicWeb.Serializer/AdminUI/Models/XmlTypeEditModel.cs` exists
- [x] `tests/DynamicWeb.Serializer.Tests/AdminUI/XmlTypeDiscoveryTests.cs` exists
- [x] Commits b580955, 5963f09, 9e3edbf exist in log
