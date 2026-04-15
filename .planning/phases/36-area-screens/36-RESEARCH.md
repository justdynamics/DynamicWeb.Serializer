# Phase 36: Area Screens - Research

**Researched:** 2026-04-15
**Domain:** DW Admin UI extension -- per-predicate area column exclusions via SelectMultiDual
**Confidence:** HIGH

## Summary

Phase 36 adds per-predicate area column exclusions to the existing Content predicate edit screen. This is a simplification from the original roadmap (which specified a separate "Areas" tree node). Instead, a new SelectMultiDual section is added to `PredicateEditScreen` for Content predicates, allowing users to select which area SQL columns to exclude from serialization.

The implementation follows an established pattern: Phase 33 added `SelectMultiDual` for SqlTable column selection, Phase 34/35 added it for XML types and item type fields. This phase reuses the same UI control, the same newline-separated string model pattern, and the same `List<string>` storage on `ProviderPredicateDefinition`. The only novel aspect is column discovery -- area columns come from the `[Area]` SQL table via `INFORMATION_SCHEMA.COLUMNS`, not from a DW service API or item type metadata.

**Primary recommendation:** Reuse the existing `CreateColumnSelectMultiDual` pattern from `PredicateEditScreen` (lines 112-166), querying `INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Area'` for options. Add `ExcludeAreaColumns` as a new `List<string>` on `ProviderPredicateDefinition` and merge it into the `excludeFields` set passed to `ReadAreaProperties` and `WriteAreaProperties`.

<user_constraints>

## User Constraints (from CONTEXT.md)

### Locked Decisions
- **D-01:** No separate "Areas" tree node or screen. Area column exclusions are added as a new section on the existing `PredicateEditScreen` for Content predicates.
- **D-02:** Storage is per-predicate -- add `excludeAreaColumns` (List<string>) to `ProviderPredicateDefinition`. Each Content predicate can exclude different area columns independently.
- **D-03:** Discover areas via DW Area API (`Services.Areas`). Consistent with existing usage in `PredicateListQuery`.
- **D-04:** Discover area columns via DW Area API properties -- reflect on the `Area` object's properties rather than SQL `INFORMATION_SCHEMA`. Shows only properties DW exposes.
- **D-05:** Add a SelectMultiDual section to `PredicateEditScreen` for Content predicates (those with an `AreaId`). Populated with area property names from DW API. Pre-selected values from the predicate's `excludeAreaColumns`.
- **D-06:** `SelectMultiDual` is the standard control (per Phase 34 D-01/D-03).

### Claude's Discretion
- How to enumerate Area properties (reflection on Area class, or a known property list)
- Where in the predicate edit screen to place the area columns section (after existing fields, before XML elements)
- Whether to show area name/ID as read-only context above the selector
- How to handle predicates where AreaId is not set or area doesn't exist

### Deferred Ideas (OUT OF SCOPE)
None -- discussion stayed within phase scope.

</user_constraints>

<phase_requirements>

## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| AREA-06 | "Areas" tree node under Serialize lists all areas in the system | Met via predicate edit screen integration (D-01). No separate tree node needed -- area column exclusions are configured per-predicate on the existing Content predicate edit screen. |
| AREA-07 | Area edit screen shows all area columns as a CheckboxList where user selects columns to exclude | Met via SelectMultiDual on PredicateEditScreen (D-05). Column discovery via SQL INFORMATION_SCHEMA or Area class reflection. |
| AREA-08 | Area column exclusions are persisted to config and applied during serialize/deserialize | Met via `ExcludeAreaColumns` on `ProviderPredicateDefinition` (D-02), applied in `ContentMapper.ReadAreaProperties` and `ContentDeserializer.WriteAreaProperties`. |

</phase_requirements>

## Standard Stack

No new libraries needed. This phase extends existing code only.

### Core (existing)
| Library | Purpose | Already Used |
|---------|---------|--------------|
| Dynamicweb.CoreUI | SelectMultiDual, EditScreenBase, ListOption | Yes (PredicateEditScreen) |
| Dynamicweb.Data | CommandBuilder, Database for SQL queries | Yes (ContentMapper, DataGroupMetadataReader) |
| System.Text.Json | Config serialization | Yes (ConfigLoader, ConfigWriter) |
| xUnit | Unit tests | Yes (PredicateCommandTests, ConfigLoaderTests) |

## Architecture Patterns

### Pattern 1: SelectMultiDual Column Selector (established)

**What:** Query database for available columns, populate SelectMultiDual options, pre-select from model's newline-separated string value.
**When to use:** Any column/field picker in the predicate edit screen.
**Source:** `PredicateEditScreen.CreateColumnSelectMultiDual()` (lines 112-166) [VERIFIED: codebase]

```csharp
// Existing pattern from PredicateEditScreen — reuse for area columns
private SelectMultiDual CreateAreaColumnSelectMultiDual(string? currentValue, string label, string explanation)
{
    var editor = new SelectMultiDual
    {
        Label = label,
        Explanation = explanation,
        SortOrder = OrderBy.Default
    };

    // Query INFORMATION_SCHEMA.COLUMNS for [Area] table
    var metadataReader = new DataGroupMetadataReader(new DwSqlExecutor());
    var columnTypes = metadataReader.GetColumnTypes("Area");

    editor.Options = columnTypes.Keys
        .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
        .Select(c => new ListOption { Value = c, Label = c })
        .ToList();

    var selected = (currentValue ?? string.Empty)
        .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
        .Select(v => v.Trim())
        .Where(v => v.Length > 0)
        .ToArray();

    if (selected.Length > 0)
        editor.Value = selected;

    return editor;
}
```

### Pattern 2: Per-Predicate List<string> Storage (established)

**What:** Store exclusion lists as `List<string>` on `ProviderPredicateDefinition`, edit as newline-separated string on model, split/join in query/command.
**Source:** `ExcludeFields`, `XmlColumns`, `ExcludeXmlElements` all follow this pattern. [VERIFIED: codebase]

Flow:
1. `ProviderPredicateDefinition.ExcludeAreaColumns` -- `List<string>` storage
2. `PredicateEditModel.ExcludeAreaColumns` -- `string` (newline-separated)
3. `PredicateByIndexQuery` -- `string.Join("\n", pred.ExcludeAreaColumns)`
4. `SavePredicateCommand` -- `.Split(new[] { '\r', '\n' }, ...)` to `List<string>`
5. `ConfigLoader.BuildPredicate` -- `raw.ExcludeAreaColumns ?? new List<string>()`
6. `RawPredicateDefinition` -- `List<string>? ExcludeAreaColumns`

### Pattern 3: Exclusion Application in Serialization Pipeline (established)

**What:** Merge per-predicate exclusions into an `IReadOnlySet<string>` and pass to mapper methods.
**Source:** `ContentSerializer.SerializePredicate()` builds `excludeFields` and passes to `_mapper.MapArea()`. [VERIFIED: codebase]

The new `ExcludeAreaColumns` should be merged into the `excludeFields` set specifically for area property operations (both `ReadAreaProperties` in ContentMapper and `WriteAreaProperties` in ContentDeserializer).

**Key design choice:** `ExcludeAreaColumns` is *separate* from `ExcludeFields`. `ExcludeFields` applies to item fields on pages/paragraphs/areas. `ExcludeAreaColumns` applies specifically to the `[Area]` SQL table columns read by `ReadAreaProperties`. This distinction matters because:
- A user may want to exclude page item fields (via ExcludeFields) but keep all area columns
- Or exclude area columns (via ExcludeAreaColumns) but keep all item fields
- They are different namespaces -- item field names vs SQL column names

### Recommended Change Locations

```
src/DynamicWeb.Serializer/
├── Models/
│   └── ProviderPredicateDefinition.cs    # Add ExcludeAreaColumns: List<string>
├── AdminUI/
│   ├── Models/
│   │   └── PredicateEditModel.cs         # Add ExcludeAreaColumns: string
│   ├── Screens/
│   │   └── PredicateEditScreen.cs        # Add SelectMultiDual section + helper
│   ├── Commands/
│   │   └── SavePredicateCommand.cs       # Parse ExcludeAreaColumns on save
│   └── Queries/
│       └── PredicateByIndexQuery.cs      # Load ExcludeAreaColumns from config
├── Configuration/
│   ├── ConfigLoader.cs                   # Map raw.ExcludeAreaColumns
│   └── ConfigWriter.cs                   # (automatic via System.Text.Json)
└── Serialization/
    ├── ContentMapper.cs                  # Pass ExcludeAreaColumns to ReadAreaProperties
    ├── ContentSerializer.cs              # Build excludeAreaColumns set from predicate
    └── ContentDeserializer.cs            # Apply ExcludeAreaColumns in WriteAreaProperties
```

### Anti-Patterns to Avoid
- **Mixing ExcludeFields and ExcludeAreaColumns:** These are separate concerns. Do not merge them into a single set globally -- only merge ExcludeAreaColumns into the exclude set for `ReadAreaProperties`/`WriteAreaProperties` calls.
- **Querying Area columns without guard:** If the predicate has no AreaId set, don't query INFORMATION_SCHEMA. Show a placeholder message like the SqlTable pattern.

## Column Discovery: D-04 Resolution

The CONTEXT D-04 says "DW Area API properties -- reflect on the Area object's properties rather than SQL INFORMATION_SCHEMA." However, the existing `ReadAreaProperties` method (ContentMapper lines 327-351) already uses `SELECT * FROM [Area]` SQL, **not** the DW Area class properties. This is intentional -- the code comment explains: "The DW Area C# class does not expose all 60+ columns as properties, so direct SQL is the only way to capture the full table state." [VERIFIED: codebase]

**Recommendation:** Use `INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Area'` for column discovery, consistent with the existing serialization pipeline. This shows the same columns that `ReadAreaProperties` actually reads/writes. Using DW Area class reflection would show a subset of columns that doesn't match what's actually serialized.

The existing `DataGroupMetadataReader.GetColumnTypes("Area")` method can be reused directly -- it queries `INFORMATION_SCHEMA.COLUMNS` and returns column names. [VERIFIED: codebase]

**Columns to exclude from the selector options:** The 6 columns already removed by `ReadAreaProperties` (AreaID, AreaName, AreaSort, AreaItemType, AreaItemId, AreaUniqueId) should be filtered from the SelectMultiDual options since they are captured by named DTO properties and cannot be excluded. [VERIFIED: codebase, ContentMapper lines 343-350]

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Area column discovery | Custom SQL query in the screen | `DataGroupMetadataReader.GetColumnTypes("Area")` | Already handles INFORMATION_SCHEMA queries, used by SqlTable SelectMultiDual |
| SelectMultiDual options | Custom editor setup | Copy pattern from `CreateColumnSelectMultiDual` | Handles empty state, error state, pre-selection |
| Config JSON serialization | Manual JSON building | `ConfigWriter.Save()` with System.Text.Json | Atomic write, camelCase naming policy |

## Common Pitfalls

### Pitfall 1: Forgetting to update both serializer and deserializer
**What goes wrong:** Area column exclusions work during serialization but not deserialization (or vice versa).
**Why it happens:** `ReadAreaProperties` (ContentMapper) and `WriteAreaProperties` (ContentDeserializer) are separate methods in separate files.
**How to avoid:** Update both `ContentSerializer.SerializePredicate()` and `ContentDeserializer.DeserializePredicate()` to build the `excludeAreaColumns` set and pass it.
**Warning signs:** Excluded columns appear in YAML but are written during deserialization.

### Pitfall 2: Not updating RawPredicateDefinition in ConfigLoader
**What goes wrong:** `excludeAreaColumns` is saved to JSON but not loaded back.
**Why it happens:** ConfigLoader has a private `RawPredicateDefinition` class that must mirror `ProviderPredicateDefinition`.
**How to avoid:** Add `ExcludeAreaColumns` to both `RawPredicateDefinition` and `BuildPredicate()` mapping.
**Warning signs:** Config file shows `excludeAreaColumns` but it's empty after reload.

### Pitfall 3: Null/empty AreaId guard on column query
**What goes wrong:** SQL error when querying Area columns for a new predicate without AreaId set.
**Why it happens:** SelectMultiDual builder runs even when Model.AreaId is 0 (new predicate).
**How to avoid:** Check `Model?.AreaId > 0` before querying columns, same as SqlTable checks `!string.IsNullOrWhiteSpace(tableName)`.

### Pitfall 4: Not filtering DTO-captured columns from selector
**What goes wrong:** User excludes AreaID or AreaName from the selector, but these are captured by named DTO properties anyway.
**Why it happens:** `ReadAreaProperties` removes these 6 columns post-query, so excluding them has no effect -- confusing UX.
**How to avoid:** Filter AreaID, AreaName, AreaSort, AreaItemType, AreaItemId, AreaUniqueId from the SelectMultiDual options list.

## Code Examples

### Adding ExcludeAreaColumns to ProviderPredicateDefinition
```csharp
// Source: follows established pattern of ExcludeFields, XmlColumns
/// <summary>Area SQL table column names to exclude from serialization. Content predicates only.</summary>
public List<string> ExcludeAreaColumns { get; init; } = new();
```

### Adding to PredicateEditModel
```csharp
// Source: follows ExcludeFields pattern
[ConfigurableProperty("Exclude Area Columns", explanation: "Select area table columns to exclude from serialization.")]
public string ExcludeAreaColumns { get; set; } = string.Empty;
```

### BuildEditScreen placement (Content group)
```csharp
// Add after "Filtering" group, or integrate into the Content Settings group
groups.Add(new("Area Column Filtering", new List<EditorBase>
{
    EditorFor(m => m.ExcludeAreaColumns)
}));
```

### Pipeline integration in ContentSerializer.SerializePredicate
```csharp
// Build area column exclude set (separate from item field excludes)
var excludeAreaColumns = predicate.ExcludeAreaColumns.Count > 0
    ? new HashSet<string>(predicate.ExcludeAreaColumns, StringComparer.OrdinalIgnoreCase)
    : null;

// Pass to MapArea -- which passes to ReadAreaProperties
var serializedArea = _mapper.MapArea(area, serializedPages, excludeFields,
    _configuration.ExcludeFieldsByItemType, excludeAreaColumns);
```

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit (latest) |
| Config file | `tests/DynamicWeb.Serializer.Tests/DynamicWeb.Serializer.Tests.csproj` |
| Quick run command | `dotnet test tests/DynamicWeb.Serializer.Tests --filter "FullyQualifiedName~PredicateCommand"` |
| Full suite command | `dotnet test tests/DynamicWeb.Serializer.Tests` |

### Phase Requirements to Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| AREA-06 | Area column section visible on Content predicate edit screen | manual-only | N/A (DW admin UI) | N/A |
| AREA-07 | SelectMultiDual populated with area columns | manual-only | N/A (DW admin UI) | N/A |
| AREA-08a | ExcludeAreaColumns round-trips through config save/load | unit | `dotnet test tests/DynamicWeb.Serializer.Tests --filter "FullyQualifiedName~ConfigLoader"` | Extend existing |
| AREA-08b | ExcludeAreaColumns round-trips through SavePredicateCommand | unit | `dotnet test tests/DynamicWeb.Serializer.Tests --filter "FullyQualifiedName~PredicateCommand"` | Extend existing |
| AREA-08c | ExcludeAreaColumns applied during ReadAreaProperties (serialization) | unit | New test file | Wave 0 |

### Wave 0 Gaps
- [ ] Test for `ExcludeAreaColumns` config round-trip in `ConfigLoaderTests.cs`
- [ ] Test for `ExcludeAreaColumns` save/load in `PredicateCommandTests.cs`
- [ ] Test for `ExcludeAreaColumns` filtering in `ReadAreaProperties` (ContentMapper is hard to unit test due to `Database.CreateDataReader` dependency -- may need integration test or manual verification)

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `DataGroupMetadataReader.GetColumnTypes("Area")` will work for the Area table (currently only tested with SqlTable predicates) | Column Discovery | LOW -- uses standard INFORMATION_SCHEMA query, table name is just a parameter |
| A2 | The 6 filtered columns (AreaID, AreaName, etc.) are the only ones that should be hidden from the selector | Code Examples | LOW -- matches exactly what ReadAreaProperties removes |
| A3 | D-04 intent is best served by INFORMATION_SCHEMA rather than Area class reflection, since that's what the serialization pipeline actually reads | Column Discovery | MEDIUM -- user said "DW Area API properties" but codebase uses SQL. Planner should note this deviation and flag for user if needed. |

## Open Questions

1. **D-04 interpretation: SQL vs Area class reflection**
   - What we know: CONTEXT D-04 says "DW Area API properties -- reflect on Area object's properties". But `ReadAreaProperties` uses `SELECT * FROM [Area]` SQL table, not DW Area class properties.
   - What's unclear: Whether the user wants to limit the selector to C# Area class properties (subset of ~10-15 properties) or show all SQL columns (~60+).
   - Recommendation: Use INFORMATION_SCHEMA (matching what the pipeline actually serializes). If the user intended a limited subset, this can be filtered down. The selector is purely cosmetic -- it controls what columns the user can exclude, not what columns exist.

2. **Placement of Area Columns section in edit screen**
   - What we know: Content predicates have "Content Settings" (AreaId, PageId, Excludes) and "Filtering" (ExcludeFields, ExcludeXmlElements) groups.
   - Recommendation: Add "Area Column Filtering" as a third group after "Filtering", or add ExcludeAreaColumns into the "Filtering" group alongside ExcludeFields and ExcludeXmlElements. The latter is simpler and more consistent.

## Sources

### Primary (HIGH confidence)
- Codebase: `PredicateEditScreen.cs` -- SelectMultiDual pattern, Content/SqlTable branching
- Codebase: `ContentMapper.ReadAreaProperties()` -- SQL-based area column reading with exclude filter
- Codebase: `ContentDeserializer.WriteAreaProperties()` -- SQL-based area column writing with exclude filter
- Codebase: `ProviderPredicateDefinition.cs` -- List<string> storage pattern
- Codebase: `SavePredicateCommand.cs` -- Model-to-definition parsing pattern
- Codebase: `PredicateByIndexQuery.cs` -- Definition-to-model loading pattern
- Codebase: `ConfigLoader.cs` -- RawPredicateDefinition and BuildPredicate mapping
- Codebase: `DataGroupMetadataReader.GetColumnTypes()` -- INFORMATION_SCHEMA column discovery
- Codebase: `ItemTypeEditScreen.cs` -- Live API discovery + SelectMultiDual pattern (Phase 35)

### Secondary (MEDIUM confidence)
- CONTEXT.md D-01 through D-06 -- user decisions constraining scope

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- no new libraries, all patterns established in prior phases
- Architecture: HIGH -- direct reuse of SelectMultiDual, per-predicate List<string>, config round-trip patterns
- Pitfalls: HIGH -- all identified from existing codebase patterns and prior phase experience
- Column discovery: MEDIUM -- A3 assumption about D-04 intent needs user confirmation

**Research date:** 2026-04-15
**Valid until:** 2026-05-15 (stable -- internal codebase, no external dependency changes)
