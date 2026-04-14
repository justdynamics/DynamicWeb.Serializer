---
phase: 34-embedded-xml-screens
plan: 02
subsystem: admin-ui
tags: [xml-screens, tree-node, list-screen, edit-screen, selectmultidual, commands]
dependency_graph:
  requires: [XmlTypeDiscovery, XmlTypeListModel, XmlTypeEditModel, ConfigWriter, ConfigLoader]
  provides: [XmlTypeListScreen, XmlTypeEditScreen, XmlTypeListQuery, XmlTypeByNameQuery, ScanXmlTypesCommand, SaveXmlTypeCommand]
  affects: [SerializerSettingsNodeProvider]
tech_stack:
  added: [RunCommandAction, SelectMultiDual]
  patterns: [GetToolbarActions-for-scan, ModelIdentifier-SetKey-wiring, merge-not-overwrite]
key_files:
  created:
    - src/DynamicWeb.Serializer/AdminUI/Screens/XmlTypeListScreen.cs
    - src/DynamicWeb.Serializer/AdminUI/Screens/XmlTypeEditScreen.cs
    - src/DynamicWeb.Serializer/AdminUI/Queries/XmlTypeListQuery.cs
    - src/DynamicWeb.Serializer/AdminUI/Queries/XmlTypeByNameQuery.cs
    - src/DynamicWeb.Serializer/AdminUI/Commands/ScanXmlTypesCommand.cs
    - src/DynamicWeb.Serializer/AdminUI/Commands/SaveXmlTypeCommand.cs
    - tests/DynamicWeb.Serializer.Tests/AdminUI/XmlTypeCommandTests.cs
  modified:
    - src/DynamicWeb.Serializer/AdminUI/Tree/SerializerSettingsNodeProvider.cs
decisions:
  - Icon.BracketsCurly used for XML nodes (Icon.Code does not exist in DW10)
  - Text editor with Readonly=true for type name display (TextField does not exist, Readonly not IsReadonly)
  - RunCommandAction.For<ScanXmlTypesCommand>().WithReloadOnSuccess() via GetToolbarActions (not GetItemCreateAction)
metrics:
  duration: 6min
  completed: 2026-04-14T09:08:44Z
  tasks_completed: 2
  tasks_total: 2
  files_created: 7
  files_modified: 1
---

# Phase 34 Plan 02: Embedded XML Admin Screens Summary

Complete admin UI for XML type discovery and element exclusion: tree node with dynamic children, list screen with Scan toolbar, edit screen with SelectMultiDual, and config persistence under excludeXmlElementsByType.

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Tree node + list screen + scan command + list query | c7f3efa | SerializerSettingsNodeProvider.cs, XmlTypeListScreen.cs, XmlTypeListQuery.cs, ScanXmlTypesCommand.cs |
| 2 | Edit screen + save command + query + tests | 7cd4932 | XmlTypeEditScreen.cs, XmlTypeByNameQuery.cs, SaveXmlTypeCommand.cs, XmlTypeCommandTests.cs |

## Implementation Details

### Task 1: Tree Node + List Screen + Scan Command + List Query

**SerializerSettingsNodeProvider:** Added `EmbeddedXmlNodeId` constant and new "Embedded XML" node under Serialize (Sort=15, between Predicates=10 and LogViewer=20) with `HasSubNodes=true`. Added dynamic children block that reads `ExcludeXmlElementsByType` keys from config and creates per-type child nodes navigating to `XmlTypeEditScreen` via `XmlTypeByNameQuery { ModelIdentifier = typeName }`.

**XmlTypeListScreen:** Follows PredicateListScreen pattern. Two columns: TypeName, ExcludedElementCount. Primary action navigates to edit screen. Toolbar button "Scan for XML types" uses `RunCommandAction.For<ScanXmlTypesCommand>().WithReloadOnSuccess()` (verified against DW10 `ListScreenBase.GetToolbarActions()` virtual method).

**XmlTypeListQuery:** Reads `ExcludeXmlElementsByType` from config, maps to `XmlTypeListModel` list ordered case-insensitively by key.

**ScanXmlTypesCommand:** Uses injected `XmlTypeDiscovery` (or creates one with `DwSqlExecutor`). Merges discovered types into config without overwriting existing exclusion lists. New types get empty lists. `ConfigPath` and `Discovery` properties enable test injection.

### Task 2: Edit Screen + Save Command + Query + Tests

**XmlTypeByNameQuery:** Extends `DataQueryIdentifiableModelBase<XmlTypeEditModel, string>`. `SetKey(string key)` populates `TypeName`. `GetModel()` loads config, looks up type in `ExcludeXmlElementsByType`, returns model with newline-joined exclusions.

**XmlTypeEditScreen:** Two fields: read-only `Text` editor for TypeName, `SelectMultiDual` for element exclusions. `CreateElementSelector()` discovers all elements for the type via `XmlTypeDiscovery.DiscoverElementsForType()`, populates Options, pre-selects currently excluded elements using `.ToArray()` (per ScreenPresetEditScreen pattern).

**SaveXmlTypeCommand:** Validates Model and TypeName. Parses newline-separated exclusions, updates `ExcludeXmlElementsByType` dictionary entry, saves via `ConfigWriter.Save()`. `ConfigPath` property for test injection.

**Tests:** 8 xUnit facts: 3 for ScanXmlTypesCommand (merge new types preserving existing, empty config adds all, no new types unchanged) and 5 for SaveXmlTypeCommand (null model, empty type name, valid save, update existing, empty exclusions).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Icon.Code does not exist in DW10**
- **Found during:** Task 1
- **Issue:** Plan specified `Icon.Code` but DW10 `Icon.cs` has no `Code` property
- **Fix:** Used `Icon.BracketsCurly` (uil-brackets-curly) which semantically represents code/markup
- **Files modified:** SerializerSettingsNodeProvider.cs

**2. [Rule 1 - Bug] TextField does not exist, IsReadonly incorrect**
- **Found during:** Task 2
- **Issue:** Plan specified `Dynamicweb.CoreUI.Editors.Inputs.TextField` with `IsReadonly = true` but DW10 has `Text` class and the property is `Readonly`
- **Fix:** Used `Text` with `Readonly = true`
- **Files modified:** XmlTypeEditScreen.cs

## Verification Results

- `dotnet build src/DynamicWeb.Serializer` -- 0 errors, 25 warnings (pre-existing)
- `dotnet test --filter "XmlType"` -- 18/18 passed (9 discovery + 8 command + 1 overlap)
- `dotnet test` full suite -- 382/386 passed (4 pre-existing failures in SqlTableProviderDeserializeTests)
- Tree node has `EmbeddedXmlNodeId` with Sort=15, `HasSubNodes=true`
- Dynamic children from `config.ExcludeXmlElementsByType.Keys`
- List screen uses `RunCommandAction.For<ScanXmlTypesCommand>().WithReloadOnSuccess()` in `GetToolbarActions()`
- Edit screen uses `SelectMultiDual` with `.ToArray()` for Value binding
- Save command persists to `ExcludeXmlElementsByType` dictionary
- Scan merges without overwriting existing exclusions

## Self-Check: PASSED

- [x] `src/DynamicWeb.Serializer/AdminUI/Screens/XmlTypeListScreen.cs` exists
- [x] `src/DynamicWeb.Serializer/AdminUI/Screens/XmlTypeEditScreen.cs` exists
- [x] `src/DynamicWeb.Serializer/AdminUI/Queries/XmlTypeListQuery.cs` exists
- [x] `src/DynamicWeb.Serializer/AdminUI/Queries/XmlTypeByNameQuery.cs` exists
- [x] `src/DynamicWeb.Serializer/AdminUI/Commands/ScanXmlTypesCommand.cs` exists
- [x] `src/DynamicWeb.Serializer/AdminUI/Commands/SaveXmlTypeCommand.cs` exists
- [x] `tests/DynamicWeb.Serializer.Tests/AdminUI/XmlTypeCommandTests.cs` exists
- [x] Commits c7f3efa, 7cd4932 exist in log
