# Phase 33: SqlTable Column Pickers - Research

**Researched:** 2026-04-10
**Domain:** DW10 CheckboxList editors, SQL schema introspection, admin UI binding
**Confidence:** HIGH

## Summary

Phase 33 replaces two free-text `Textarea` editors (`ExcludeFields` and `XmlColumns`) on the SqlTable predicate edit screen with `CheckboxList` controls populated from `INFORMATION_SCHEMA.COLUMNS`. The existing codebase already has all the infrastructure needed: `DataGroupMetadataReader` already queries column schemas via `INFORMATION_SCHEMA.COLUMNS`, and the DW10 `CheckboxList` component is a trivial subclass of `ListBase` that renders checkboxes with `Options` of `ListOption { Value, Label }`.

The main implementation challenge is the model property type change. Currently `PredicateEditModel.ExcludeFields` and `.XmlColumns` are `string` (newline-separated), but CheckboxList binds to collection types like `IList<string>`. The model properties need to change from `string` to `IList<string>`, and `SavePredicateCommand`'s manual newline-splitting must be replaced with direct list usage. `PredicateByIndexQuery`'s `string.Join("\n", ...)` mappings must also change to pass lists directly.

**Primary recommendation:** Change `ExcludeFields` and `XmlColumns` model properties from `string` to `IList<string>`, add a column schema query to the query/screen load path, and create `CheckboxList` editors populated from the schema results.

<user_constraints>

## User Constraints (from CONTEXT.md)

### Locked Decisions
- **D-01:** Column schema fetched on screen load via `INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @table`. Single query when predicate edit screen opens.
- **D-02:** When table doesn't exist (no columns returned), show empty CheckboxList with warning message. Do not fall back to Textarea.
- **D-03:** Checked = excluded. Existing exclusions pre-checked on load. Unchecking removes from list.
- **D-04:** Column names only -- no data type information displayed.
- **D-05:** Model properties stay `List<string>`. Only editor control changes. DW framework handles CheckboxList value binding.

### Claude's Discretion
- Where to place the SQL schema query logic (model load vs screen constructor vs dedicated helper)
- Whether to cache the column list within a single request lifecycle
- Test approach for the schema query integration

### Deferred Ideas (OUT OF SCOPE)
None

</user_constraints>

<phase_requirements>

## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| PRED-04 | SqlTable predicate excludeFields uses CheckboxList populated from table schema instead of textarea | CheckboxList component verified in DW10 source; `DataGroupMetadataReader.GetColumnTypes()` already queries INFORMATION_SCHEMA.COLUMNS; model property type change from `string` to `IList<string>` enables framework binding |
| PRED-05 | SqlTable predicate xmlColumns uses CheckboxList populated from table schema instead of textarea | Same infrastructure as PRED-04; both fields share the same column options list |

</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Dynamicweb.CoreUI | DW10 | CheckboxList, ListBase, ListOption, EditorBase | Framework-provided UI components [VERIFIED: DW10 source] |
| Dynamicweb.Data | DW10 | CommandBuilder, Database for SQL queries | Already used by DataGroupMetadataReader [VERIFIED: codebase] |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| INFORMATION_SCHEMA.COLUMNS | SQL Server | Column name discovery | Query on screen load to populate CheckboxList options [VERIFIED: already used in DataGroupMetadataReader] |

No new packages needed. This phase uses only existing framework components and infrastructure.

## Architecture Patterns

### Recommended Changes

```
src/DynamicWeb.Serializer/AdminUI/
  Models/PredicateEditModel.cs        # Change ExcludeFields, XmlColumns from string to IList<string>; add AvailableColumns property
  Screens/PredicateEditScreen.cs      # Swap Textarea -> CheckboxList for ExcludeFields/XmlColumns in SqlTable section
  Queries/PredicateByIndexQuery.cs    # Pass lists directly instead of string.Join
  Commands/SavePredicateCommand.cs    # Remove newline-splitting for ExcludeFields/XmlColumns; use list directly
```

### Pattern 1: CheckboxList with Options from SQL Schema
**What:** DW10 CheckboxList extends ListBase. Set `Options` with `ListOption` entries where `Value` and `Label` are both the column name. Framework binds checked values to `IList<string>` model property. [VERIFIED: DW10 source GridRowDefinitionModel uses `IList<string> Features` with CheckboxList]
**When to use:** When user selects from a known set of string values
**Example:**
```csharp
// Source: DW10 GridRowDefinitionEditScreen.cs pattern + DataGroupMetadataReader.GetColumnTypes()
var columns = GetColumnsForTable(Model?.Table);
var editor = new CheckboxList
{
    SortOrder = OrderBy.Default,
    Options = columns.Select(col => new ListOption
    {
        Value = col,
        Label = col
    }).ToList()
};
// Value is set automatically by DW framework from model property binding
```

### Pattern 2: Model Property Type for CheckboxList Binding
**What:** DW framework binds CheckboxList selections to `IList<string>` (or `HashSet<int>` for numeric values). The model property type determines binding behavior. [VERIFIED: DW10 WorkflowStateModel uses `HashSet<int>`, GridRowDefinitionModel uses `IList<string>`]
**When to use:** Any multi-select editor
**Example:**
```csharp
// Source: DW10 GridRowDefinitionModel.cs
[ConfigurableProperty("Exclude Fields", explanation: "...")]
public IList<string> ExcludeFields { get; set; } = [];

[ConfigurableProperty("XML Columns", explanation: "...")]
public IList<string> XmlColumns { get; set; } = [];
```

### Pattern 3: Column Schema Query via DataGroupMetadataReader
**What:** Reuse existing `DataGroupMetadataReader.GetColumnTypes()` which already queries `INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @table`. Returns `Dictionary<string, string>` (column name -> data type). We only need the keys (column names per D-04). [VERIFIED: DataGroupMetadataReader.cs line 27-40]
**When to use:** When populating column pickers for a SqlTable predicate
**Recommendation for query placement:** Add a helper method on `PredicateEditScreen` that fetches columns when `Model.Table` is set. This keeps the query close to where it's consumed (the editor factory) and avoids polluting the model with infrastructure concerns.

### Anti-Patterns to Avoid
- **Don't query columns in SavePredicateCommand:** The save command should only validate and persist; column discovery belongs in the screen/query layer.
- **Don't change the config model (`ProviderPredicateDefinition`):** The `ExcludeFields` and `XmlColumns` are already `List<string>` there. Only the UI model (`PredicateEditModel`) needs type changes.
- **Don't create a new SQL query method:** `DataGroupMetadataReader.GetColumnTypes()` already returns all columns. Just use its `.Keys`.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Multi-select checkbox UI | Custom HTML/JS checkboxes | DW10 `CheckboxList` from `Dynamicweb.CoreUI.Editors.Lists` | Framework handles rendering, value collection, and model binding [VERIFIED: DW10 source] |
| SQL column discovery | Raw ADO.NET queries | `DataGroupMetadataReader.GetColumnTypes()` | Already tested, uses `ISqlExecutor` abstraction, handles `CommandBuilder` [VERIFIED: codebase] |
| List-to-string-to-list conversion | Manual split/join | Change model to `IList<string>` and let framework bind directly | Eliminates error-prone newline parsing [VERIFIED: DW10 pattern] |

## Common Pitfalls

### Pitfall 1: Model Property Type Mismatch
**What goes wrong:** CheckboxList posts array values but model property is `string`, causing binding failure or empty values.
**Why it happens:** Current `PredicateEditModel.ExcludeFields` and `.XmlColumns` are `string` type, designed for Textarea newline-separated input.
**How to avoid:** Change both properties from `string` to `IList<string>`. Update `PredicateByIndexQuery` to assign lists directly instead of `string.Join("\n", ...)`. Update `SavePredicateCommand` to read from lists directly instead of `.Split()`.
**Warning signs:** Saved config has empty arrays despite selections being visible in UI.

### Pitfall 2: SavePredicateCommand Still Splitting by Newline
**What goes wrong:** After changing model type to `IList<string>`, the save command's `.Split(new[] { '\r', '\n' }, ...)` throws or returns wrong results on an `IList<string>`.
**Why it happens:** SavePredicateCommand lines 69-85 manually split `ExcludeFields`, `XmlColumns`, and `ExcludeXmlElements` by newline characters. With `IList<string>` these splits are no longer valid.
**How to avoid:** For `ExcludeFields` and `XmlColumns` (now `IList<string>`), use `Model.ExcludeFields?.ToList() ?? []` directly. Keep `ExcludeXmlElements` as `string` with newline splitting since it stays as Textarea.
**Warning signs:** Runtime exceptions in SavePredicateCommand when saving SqlTable predicates.

### Pitfall 3: Empty Table Name on New Predicate
**What goes wrong:** On a new SqlTable predicate, `Model.Table` is empty, so column query returns nothing and CheckboxList is empty.
**Why it happens:** User hasn't entered a table name yet.
**How to avoid:** Per D-02, show empty CheckboxList with warning message when no columns found. The screen already conditionally builds SqlTable sections only when `ProviderType == "SqlTable"`. But Table may be set without being a real table. Handle gracefully.
**Warning signs:** Error on screen load for new predicates.

### Pitfall 4: ISqlExecutor Not Available in Screen Context
**What goes wrong:** `DataGroupMetadataReader` requires `ISqlExecutor` which may not be directly available in the screen class.
**Why it happens:** Screen classes don't typically take constructor dependencies.
**How to avoid:** Instantiate `DataGroupMetadataReader` with `new DwSqlExecutor()` directly in the screen's helper method (same pattern as production provider code). Or use `new DataGroupMetadataReader(new DwSqlExecutor()).GetColumnTypes(tableName)`.
**Warning signs:** NullReferenceException or missing service.

### Pitfall 5: Dual-Type Model Properties Break Content Predicates
**What goes wrong:** Changing `ExcludeFields` from `string` to `IList<string>` on the shared model breaks Content predicates that still use Textarea for ExcludeFields.
**Why it happens:** `PredicateEditModel` is shared between Content and SqlTable. Content predicates also show ExcludeFields as Textarea (line 40 in PredicateEditScreen). Textarea expects `string`, not `IList<string>`.
**How to avoid:** Either (a) keep `ExcludeFields` as `string` and do manual conversion in the CheckboxList creation (set Value from parsed string), or (b) change to `IList<string>` and handle Textarea binding via a separate string property. Option (a) is simpler and lower risk -- create CheckboxList, set its Value to the parsed list, and in save handle both Textarea string and CheckboxList list inputs.
**Warning signs:** Content predicate edit screen breaks after model type change.

**CRITICAL:** This is the most important pitfall. The `ExcludeFields` property is used by BOTH Content (Textarea) and SqlTable (CheckboxList). The model property type must work for both editor types. Recommendation: Keep the model property as `string` and manually handle conversion in the screen's `GetEditor` method. Set CheckboxList.Value to the parsed list on load. In SavePredicateCommand, detect whether the value came as newline-separated string or as a list and handle accordingly.

## Code Examples

### Example 1: CheckboxList Editor Creation for ExcludeFields
```csharp
// Based on: DW10 CheckboxList patterns + DataGroupMetadataReader
private CheckboxList CreateColumnCheckboxList(string? tableName, string? currentValue)
{
    var editor = new CheckboxList
    {
        SortOrder = ListBase.OrderBy.Default,
        Label = "Exclude Fields",
        Explanation = "Select columns to exclude from serialization."
    };

    if (string.IsNullOrWhiteSpace(tableName))
        return editor;

    try
    {
        var metadataReader = new DataGroupMetadataReader(new DwSqlExecutor());
        var columns = metadataReader.GetColumnTypes(tableName);

        if (columns.Count == 0)
        {
            editor.Explanation = "Table not found in database. Verify the table name.";
            return editor;
        }

        editor.Options = columns.Keys
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .Select(col => new ListBase.ListOption { Value = col, Label = col })
            .ToList();

        // Pre-select currently excluded columns
        if (!string.IsNullOrEmpty(currentValue))
        {
            var selected = currentValue
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList();
            editor.Value = selected;
        }
    }
    catch
    {
        editor.Explanation = "Could not query database columns.";
    }

    return editor;
}
```

### Example 2: Updated GetEditor Switch for SqlTable CheckboxLists
```csharp
// In PredicateEditScreen.GetEditor(), replace Textarea cases conditionally
nameof(PredicateEditModel.ExcludeFields) => Model?.ProviderType == "SqlTable"
    ? CreateColumnCheckboxList(Model?.Table, Model?.ExcludeFields, "Exclude Fields",
        "Select columns to exclude from serialization.")
    : new Textarea
    {
        Label = "Exclude Fields",
        Explanation = "One field name per line. These fields will be omitted from serialization."
    },
```

### Example 3: SavePredicateCommand Value Handling
```csharp
// SavePredicateCommand must handle both string (from Textarea) and IList (from CheckboxList)
// The framework may post CheckboxList values as a list or as newline-joined string
// depending on the model property type. Keeping string type means parsing stays the same.
var excludeFields = (Model.ExcludeFields ?? string.Empty)
    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
    .Select(e => e.Trim())
    .Where(e => e.Length > 0)
    .ToList();
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Free-text Textarea for column names | CheckboxList populated from SQL schema | This phase | Eliminates typos, shows actual available columns |

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | DW framework binds CheckboxList values back to a `string` model property as newline-joined or comma-joined string | Pitfall 5 / Code Examples | If framework doesn't do this, SavePredicateCommand parsing breaks. Must test empirically. |
| A2 | `DataGroupMetadataReader` can be instantiated directly in the screen via `new DataGroupMetadataReader(new DwSqlExecutor())` without DI | Pitfall 4 | If DW screens prevent direct DB access, need alternative approach |
| A3 | CheckboxList respects `.Value` set programmatically to pre-check items | Code Example 1 | If `.Value` is ignored, pre-selection won't work |

## Open Questions

1. **CheckboxList value binding with string model property**
   - What we know: DW10 uses `IList<string>` and `HashSet<int>` for CheckboxList model properties. Our model uses `string`.
   - What's unclear: How does CheckboxList value round-trip when the model property is `string`? Does the framework join selected values with a delimiter? Or does binding fail?
   - Recommendation: Keep `ExcludeFields` and `XmlColumns` as `string` properties (since they're shared with Content Textarea). Set `CheckboxList.Value` explicitly from parsed string on load. Test that save correctly receives the values. If framework joins them, the existing newline split may need adjustment to handle the actual delimiter (could be comma, newline, or pipe).

2. **Screen-level SQL access pattern**
   - What we know: Provider classes use `ISqlExecutor` via constructor injection. Screens use `EditorFor()` pattern.
   - What's unclear: Whether instantiating `DataGroupMetadataReader` directly in a screen method works in the DW admin runtime.
   - Recommendation: Use `new DataGroupMetadataReader(new DwSqlExecutor())` inline. This is the simplest approach and `DwSqlExecutor` just wraps `Database.CreateDataReader()` static calls. [ASSUMED]

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit (verified in existing tests) |
| Config file | tests/DynamicWeb.Serializer.Tests/ |
| Quick run command | `dotnet test tests/DynamicWeb.Serializer.Tests --filter "PredicateCommand"` |
| Full suite command | `dotnet test` |

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| PRED-04 | ExcludeFields CheckboxList populated from schema, selections persist | unit | `dotnet test tests/DynamicWeb.Serializer.Tests --filter "PredicateCommand"` | Partial (PredicateCommandTests exists, needs new tests) |
| PRED-05 | XmlColumns CheckboxList populated from schema, selections persist | unit | `dotnet test tests/DynamicWeb.Serializer.Tests --filter "PredicateCommand"` | Partial (same file) |

### Sampling Rate
- **Per task commit:** `dotnet test tests/DynamicWeb.Serializer.Tests --filter "PredicateCommand"`
- **Per wave merge:** `dotnet test`
- **Phase gate:** Full suite green before verification

### Wave 0 Gaps
- [ ] Test for ExcludeFields round-trip with CheckboxList-style input (list rather than newline string) in `PredicateCommandTests.cs`
- [ ] Test for XmlColumns round-trip similarly
- [ ] Test for empty table (no columns returned) behavior

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | N/A |
| V3 Session Management | no | N/A |
| V4 Access Control | no | N/A (admin-only screens) |
| V5 Input Validation | yes | Column names come from SQL schema, not user input; CheckboxList constrains to valid options |
| V6 Cryptography | no | N/A |

### Known Threat Patterns

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| SQL injection in table name | Tampering | Table name comes from existing config (user-entered in separate field, already validated). Column query uses table name in INFORMATION_SCHEMA query -- current code uses string interpolation (not parameterized). Existing pattern accepted in codebase. |

## Sources

### Primary (HIGH confidence)
- DW10 source: `Dynamicweb.CoreUI/Editors/Lists/CheckboxList.cs` - empty subclass of ListBase
- DW10 source: `Dynamicweb.CoreUI/Editors/Lists/ListBase.cs` - Options, ListOption, Value, SortOrder
- DW10 source: `Dynamicweb.Content.UI/Screens/Settings/GridRowDefinitionEditScreen.cs` - CheckboxList with IList<string> pattern
- DW10 source: `Dynamicweb.Products.UI/Models/Settings/WorkflowStateModel.cs` - HashSet<int> binding pattern
- Codebase: `DataGroupMetadataReader.GetColumnTypes()` - existing INFORMATION_SCHEMA query
- Codebase: `PredicateEditScreen.cs`, `PredicateEditModel.cs`, `SavePredicateCommand.cs`, `PredicateByIndexQuery.cs` - current implementation

### Secondary (MEDIUM confidence)
- DW10 source: `Dynamicweb.CoreUI/Editors/EditorBase.cs` - Value property is object?

### Tertiary (LOW confidence)
- None

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - all components verified in DW10 source and existing codebase
- Architecture: HIGH - clear pattern from DW10 examples and existing code structure
- Pitfalls: HIGH - identified through code analysis of shared model between Content/SqlTable

**Research date:** 2026-04-10
**Valid until:** 2026-05-10 (stable DW10 framework, no expected changes)
