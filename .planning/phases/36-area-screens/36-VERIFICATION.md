---
phase: 36-area-screens
verified: 2026-04-15T12:30:00Z
status: gaps_found
score: 4/6 must-haves verified
overrides_applied: 0
gaps:
  - truth: "Users can browse all areas through a dedicated tree node"
    status: failed
    reason: "No area-specific tree node exists. SerializerSettingsNodeProvider provides only 'Predicates' and 'Log Viewer' subnodes under 'Serialize'. No AreaListScreen, AreaBrowseScreen, or area tree node was created in this phase."
    artifacts:
      - path: "src/DynamicWeb.Serializer/AdminUI/Tree/SerializerSettingsNodeProvider.cs"
        issue: "No 'Areas' node in GetSubNodes — only PredicatesNodeId and LogViewerNodeId are yielded"
    missing:
      - "A dedicated tree node (e.g. 'Areas') under the Serialize node"
      - "An AreaListScreen or AreaBrowseScreen that lists areas with their predicate configurations"
  - truth: "AREA-06, AREA-07, AREA-08 are defined in REQUIREMENTS.md with traceability entries"
    status: failed
    reason: "REQUIREMENTS.md contains no entries for AREA-06, AREA-07, or AREA-08. The plans reference these IDs but they are orphaned — no requirement definition, description, or traceability row exists for them."
    artifacts:
      - path: ".planning/REQUIREMENTS.md"
        issue: "Only defines AREA-01 and AREA-02. AREA-06/07/08 are absent entirely."
    missing:
      - "AREA-06 requirement entry in REQUIREMENTS.md (Area Column Filtering UI)"
      - "AREA-07 requirement entry in REQUIREMENTS.md (Area Column Exclusion Config Round-trip)"
      - "AREA-08 requirement entry in REQUIREMENTS.md (Area Column Exclusion Pipeline)"
      - "Traceability rows for all three IDs in the traceability table"
human_verification:
  - test: "Open Admin UI > Settings > Database > Serialize > Predicates > (create or edit a Content predicate with an AreaId set) > verify 'Area Column Filtering' group appears with SelectMultiDual populated from Area table columns minus DTO-captured columns"
    expected: "SelectMultiDual shows Area table columns (excluding AreaID, AreaName, AreaSort, AreaItemType, AreaItemId, AreaUniqueId). Selecting columns and saving persists them in config JSON. Reloading the predicate shows the previously selected columns pre-selected."
    why_human: "Requires live DW instance with a configured Area and predicate. Cannot verify SelectMultiDual population or UI reload behavior programmatically."
---

# Phase 36: Area Screens Verification Report

**Phase Goal:** Users can browse all areas and configure per-area column exclusions through a dedicated tree node
**Verified:** 2026-04-15T12:30:00Z
**Status:** gaps_found
**Re-verification:** No — initial verification

## Goal Achievement

The stated phase goal has two components: (1) browse all areas via a dedicated tree node, and (2) configure per-area column exclusions. Only component (2) was implemented. No area tree node or area browse screen was created. The ExcludeAreaColumns feature was implemented entirely within the existing predicate edit screen as a per-predicate property.

Additionally, the requirement IDs AREA-06, AREA-07, AREA-08 referenced in both plans do not exist in REQUIREMENTS.md, creating a traceability gap.

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Content predicate edit screen shows a SelectMultiDual for area column exclusions when AreaId is set | VERIFIED | `PredicateEditScreen.cs:40-43` — "Area Column Filtering" group added to Content branch; `GetEditor` switch at line 82 returns `CreateAreaColumnSelectMultiDual` |
| 2 | Area column selections round-trip through save and load (model -> config JSON -> model) | VERIFIED | `SavePredicateCommand.cs:91-95` parses `ExcludeAreaColumns` for Content branch; `ConfigLoader.cs:92` maps `raw.ExcludeAreaColumns`; 56 tests pass including `Save_Content_ExcludeAreaColumns_RoundTrips` |
| 3 | SelectMultiDual options come from INFORMATION_SCHEMA.COLUMNS for the Area table, excluding 6 DTO-captured columns | VERIFIED | `PredicateEditScreen.cs:126-145` — calls `metadataReader.GetColumnTypes("Area")`, filters out `AreaID, AreaName, AreaSort, AreaItemType, AreaItemId, AreaUniqueId` via `dtoColumns` HashSet |
| 4 | ExcludeAreaColumns from predicate config filters columns out of ReadAreaProperties during serialization | VERIFIED | `ContentSerializer.cs:94-96` builds `excludeAreaColumns` HashSet; `ContentMapper.cs:50` passes it to `ReadAreaProperties`; `ReadAreaProperties:307` applies `excludeAreaColumns?.Contains(name) != true` filter |
| 5 | ExcludeAreaColumns from predicate config filters columns out of WriteAreaProperties during deserialization | VERIFIED | `ContentDeserializer.cs:197-199` builds `excludeAreaColumnsSet`; `WriteAreaProperties:1196` skips excluded columns; `WriteContext` does NOT contain ExcludeAreaColumns |
| 6 | Users can browse all areas through a dedicated tree node | FAILED | `SerializerSettingsNodeProvider.cs` shows only `PredicatesNodeId` and `LogViewerNodeId` subnodes. No area node, AreaListScreen, or AreaBrowseScreen exists in the codebase. |

**Score:** 4/6 truths verified (but 5/5 plan must-haves verified — gap is against stated phase goal)

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/DynamicWeb.Serializer/Models/ProviderPredicateDefinition.cs` | ExcludeAreaColumns property | VERIFIED | Line 50: `public List<string> ExcludeAreaColumns { get; init; } = new();` |
| `src/DynamicWeb.Serializer/Configuration/ConfigLoader.cs` | ExcludeAreaColumns round-trip in RawPredicateDefinition and BuildPredicate | VERIFIED | Line 119 (Raw), line 92 (Build) |
| `src/DynamicWeb.Serializer/AdminUI/Screens/PredicateEditScreen.cs` | SelectMultiDual for area columns in Content predicate section | VERIFIED | Lines 40-43 (group), 82-84 (editor), 109-162 (method) |
| `tests/DynamicWeb.Serializer.Tests/AdminUI/PredicateCommandTests.cs` | Round-trip test for ExcludeAreaColumns | VERIFIED | Lines 478-561: 3 ExcludeAreaColumns tests all pass |
| `src/DynamicWeb.Serializer/Serialization/ContentSerializer.cs` | Builds excludeAreaColumns set and passes to MapArea | VERIFIED | Lines 94-96, 99 |
| `src/DynamicWeb.Serializer/Serialization/ContentMapper.cs` | MapArea accepts excludeAreaColumns and passes to ReadAreaProperties | VERIFIED | Line 24-25 (signature), line 50 (ReadAreaProperties call) |
| `src/DynamicWeb.Serializer/Serialization/ContentDeserializer.cs` | DeserializePredicate builds excludeAreaColumnsSet and passes to WriteAreaProperties | VERIFIED | Lines 197-199, 218 |
| Area tree node / AreaListScreen | Dedicated tree node for browsing areas | MISSING | Not created — no such screen or node provider exists |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `PredicateEditScreen.cs` | `DataGroupMetadataReader` | `GetColumnTypes("Area")` | VERIFIED | Line 127: `metadataReader.GetColumnTypes("Area")` |
| `SavePredicateCommand.cs` | `ProviderPredicateDefinition` | `ExcludeAreaColumns` parsing | VERIFIED | Lines 91-95: parsed in Content branch only; SqlTable branch confirmed absent |
| `ConfigLoader.cs` | `ProviderPredicateDefinition` | `BuildPredicate` mapping | VERIFIED | Line 92: `ExcludeAreaColumns = raw.ExcludeAreaColumns ?? new List<string>()` |
| `ContentSerializer.cs` | `ContentMapper.MapArea` | `excludeAreaColumns` parameter | VERIFIED | Line 99: `_mapper.MapArea(area, serializedPages, excludeAreaColumns)` |
| `ContentMapper.MapArea` | `ReadAreaProperties` | `excludeAreaColumns` directly passed | VERIFIED | Line 50: `Properties = ReadAreaProperties(area.ID, excludeAreaColumns)` — note: plan specified `areaPropsExclude` variable but implementation passes parameter directly (no ExcludeFieldsByItemType infrastructure existed to merge; functionally equivalent) |
| `ContentDeserializer.cs` | `WriteAreaProperties` | `excludeAreaColumnsSet` | VERIFIED | Line 218: `WriteAreaProperties(predicate.AreaId, area.Properties, excludeAreaColumnsSet)` |
| `ContentDeserializer.WriteContext` | (must NOT contain ExcludeAreaColumns) | — | VERIFIED | WriteContext (lines 46-57) has no ExcludeAreaColumns property |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|--------------------|--------|
| `ContentMapper.ReadAreaProperties` | `props` (area column dictionary) | `SELECT * FROM [Area] WHERE [AreaID] = {0}` | Yes — live SQL query | FLOWING |
| `ContentDeserializer.WriteAreaProperties` | `properties` dict | YAML deserialized `area.Properties` | Yes — parameterized UPDATE | FLOWING |
| `PredicateEditScreen.CreateAreaColumnSelectMultiDual` | `columnTypes` | `DataGroupMetadataReader.GetColumnTypes("Area")` | Yes — live INFORMATION_SCHEMA query (with DB guard) | FLOWING |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Build succeeds | `dotnet build src/DynamicWeb.Serializer --no-restore` | 0 errors, 17 warnings | PASS |
| ExcludeAreaColumns round-trip tests | `dotnet test --filter "PredicateCommand|ConfigLoader"` | 56/56 passed | PASS |
| Area tree node exists | File system check for AreaListScreen/node | No such file found | FAIL |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|-------------|--------|----------|
| AREA-06 | 36-01-PLAN.md | Not defined in REQUIREMENTS.md | ORPHANED | No entry in REQUIREMENTS.md; no traceability row |
| AREA-07 | 36-01-PLAN.md | Not defined in REQUIREMENTS.md | ORPHANED | No entry in REQUIREMENTS.md; no traceability row |
| AREA-08 | 36-02-PLAN.md | Not defined in REQUIREMENTS.md | ORPHANED | No entry in REQUIREMENTS.md; no traceability row |

All three requirement IDs referenced in the phase plans are absent from REQUIREMENTS.md. There are no definitions, descriptions, or traceability table rows for AREA-06, AREA-07, or AREA-08. This is an administrative gap — the implementation itself is sound, but the requirements were never formally added to the requirements register.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `ContentDeserializer.cs` | 1200-1205 | Column names from YAML interpolated into SQL string (WR-02 from REVIEW) | Warning | Low — values are parameterized, but key is string-interpolated; bracket-quoting does not escape `]` |
| `ContentDeserializer.cs` | 95-97 | `ReadTree` result not null-checked before `DeserializePredicate` (WR-03 from REVIEW) | Warning | NullReferenceException if area directory missing |
| `ConfigLoader.cs` | 76 | `ParseConflictStrategy` silently ignores unrecognized values (WR-01 from REVIEW) | Warning | Operator config errors silently swallowed |
| `PredicateByIndexQuery.cs` | 25 | `ConfigLoader.Load` not wrapped in try/catch (WR-04 from REVIEW) | Warning | Malformed config propagates as unhandled exception to DW admin UI |

Note: These anti-patterns were identified in the phase REVIEW.md (WR-01 through WR-04). They are pre-existing warnings carried into this verification. None of these block the ExcludeAreaColumns feature itself, but WR-03 is a latent crash path.

### Human Verification Required

#### 1. Area Column Filtering UI End-to-End

**Test:** In a live DW admin instance, navigate to Settings > Database > Serialize > Predicates, create or open a Content predicate with a valid AreaId. Confirm the "Area Column Filtering" group appears. Open the SelectMultiDual and verify it shows Area table columns (not including AreaID, AreaName, AreaSort, AreaItemType, AreaItemId, AreaUniqueId). Select 2-3 columns, save, reopen the predicate, and verify selections are pre-populated.

**Expected:** SelectMultiDual shows Area table columns minus 6 DTO-captured columns. Saved selections are preserved and re-displayed on reload. Predicate with no AreaId shows "Select an area to see available columns."

**Why human:** Requires live DW instance with a connected Area. Cannot verify SelectMultiDual population, reload behavior, or visual rendering programmatically.

### Gaps Summary

Two gaps block full goal achievement:

**Gap 1 — Missing area tree node (critical against stated phase goal):** The phase goal specifies "browse all areas... through a dedicated tree node." No such node, area list screen, or area browse screen was created. The ExcludeAreaColumns feature was embedded in the existing predicate edit screen as a per-predicate field, not as a separate area-centric browsing experience. This may reflect a mismatch between the stated phase goal and the actual planned scope (the plans do not mention a tree node either) — if the intent was simply to add ExcludeAreaColumns to the predicate edit screen, the phase goal was stated too broadly.

**Gap 2 — Orphaned requirement IDs (administrative):** AREA-06, AREA-07, and AREA-08 exist in plan frontmatter but not in REQUIREMENTS.md. The implementation substance is correct, but the traceability register is incomplete. These should be added to REQUIREMENTS.md with definitions and traceability rows.

---

_Verified: 2026-04-15T12:30:00Z_
_Verifier: Claude (gsd-verifier)_
