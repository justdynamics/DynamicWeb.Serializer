# Phase 33: SqlTable Column Pickers - Context

**Gathered:** 2026-04-10
**Status:** Ready for planning

<domain>
## Phase Boundary

Replace the free-text `Textarea` editors for `excludeFields` and `xmlColumns` on the SqlTable predicate edit screen with `CheckboxList` controls populated from the table's actual SQL column schema. Users select columns to exclude (or mark as XML) by checking boxes instead of typing column names manually.

</domain>

<decisions>
## Implementation Decisions

### Column Discovery
- **D-01:** Column schema is fetched **on screen load** via `INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @table`. Single query when the predicate edit screen opens. If `Table` property is set, columns are immediately available as CheckboxList options.
- **D-02:** When the table doesn't exist in the database (no columns returned), show an **empty CheckboxList with a warning message** (e.g., "Table not found in database"). Do not fall back to Textarea — force the user to correct the table name.

### CheckboxList UX
- **D-03:** **Checked = excluded.** Existing exclusions from config are pre-checked when the edit screen opens. Unchecking removes the column from the exclusion list.
- **D-04:** **Column names only** — no data type information displayed. Keep the list clean and simple.

### Save/Load Flow
- **D-05:** Model properties stay `List<string>` (`ExcludeFields`, `XmlColumns`). Only the editor control changes from `Textarea` to `CheckboxList`. The DW framework handles CheckboxList value binding to `List<string>`. SavePredicateCommand should need minimal or no changes.

### Claude's Discretion
- Where to place the SQL schema query logic (model load vs screen constructor vs dedicated helper)
- Whether to cache the column list within a single request lifecycle
- Test approach for the schema query integration

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### DW10 CheckboxList Component
- `C:/Projects/temp/dw10source/Dynamicweb.CoreUI/Editors/Lists/CheckboxList.cs` — DW10's CheckboxList extends ListBase, uses Options list of ListOption { Value, Label }
- `C:/Projects/temp/dw10source/Dynamicweb.CoreUI/Editors/Inputs/ListBase.cs` — Base class with Options, ListOption, SortOrder

### Predicate Edit Screen (current implementation)
- `src/DynamicWeb.Serializer/AdminUI/Screens/PredicateEditScreen.cs` — Current Textarea editors for ExcludeFields (line 87) and XmlColumns (line 92) in SqlTable section (lines 44-59)
- `src/DynamicWeb.Serializer/AdminUI/Models/PredicateEditModel.cs` — Model with ExcludeFields and XmlColumns as string properties
- `src/DynamicWeb.Serializer/AdminUI/Commands/SavePredicateCommand.cs` — Save command that splits textarea lines into List<string>
- `src/DynamicWeb.Serializer/AdminUI/Queries/PredicateByIndexQuery.cs` — Query that loads predicate data into model

### Config Infrastructure (from Phase 32)
- `src/DynamicWeb.Serializer/Configuration/ConfigLoader.cs` — Config loading with per-predicate ExcludeFields/XmlColumns
- `src/DynamicWeb.Serializer/Configuration/ConfigWriter.cs` — Atomic JSON save
- `src/DynamicWeb.Serializer/Models/ProviderPredicateDefinition.cs` — Per-predicate model with ExcludeFields and XmlColumns lists

### SQL Table Provider
- `src/DynamicWeb.Serializer/Providers/SqlTable/SqlTableProvider.cs` — Where ExcludeFields is applied during SqlTable serialization
- `src/DynamicWeb.Serializer/Providers/SqlTable/SqlTableReader.cs` — Reads SQL table data, has column handling logic

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- DW10 `CheckboxList` from `Dynamicweb.CoreUI.Editors.Lists` — empty subclass of `ListBase`, ready to use with `Options` list
- `ListOption` class with `Value` (object?) and `Label` (string) — maps directly to column names
- `SelectorBuilder` pattern in PredicateEditScreen for creating selector editors
- `WithReloadOnChange()` on AreaId field — same pattern available if dynamic reload is needed later

### Established Patterns
- Editor creation via `GetEditor()` switch expression returning `EditorBase` subtypes
- `Select` with `ListOption` used for ProviderType, LogLevel, ConflictStrategy dropdowns
- `Textarea` with `Label` and `Explanation` for text input fields
- Model properties use `[ConfigurableProperty]` attribute for DW admin binding

### Integration Points
- `PredicateEditScreen.GetEditor()` — swap Textarea → CheckboxList for ExcludeFields and XmlColumns when ProviderType == "SqlTable"
- `PredicateEditModel` — may need an `AvailableColumns` property to hold schema query results
- `PredicateByIndexQuery` or model constructor — where to run the INFORMATION_SCHEMA query
- `SavePredicateCommand` — verify DW framework handles CheckboxList → List<string> binding (may need adjustment)

</code_context>

<specifics>
## Specific Ideas

No specific requirements — standard DW10 CheckboxList usage with SQL schema query.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 33-sqltable-column-pickers*
*Context gathered: 2026-04-10*
