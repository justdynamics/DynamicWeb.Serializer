# Phase 34: Embedded XML Screens - Context

**Gathered:** 2026-04-14
**Status:** Ready for planning

<domain>
## Phase Boundary

Add an "Embedded XML" tree node under Serialize that lists auto-discovered XML types and provides per-type element-level exclusion editing via `SelectMultiDual` controls. Also retroactively replace Phase 33's `CheckboxList` editors with `SelectMultiDual` to establish a consistent multi-select pattern.

</domain>

<decisions>
## Implementation Decisions

### Control Type
- **D-01:** Use `SelectMultiDual` (from `Dynamicweb.CoreUI.Editors.Lists`) instead of `CheckboxList` for all multi-select fields. This is a dual-pane control with searchable available list on the left, selected items on the right, and add/remove buttons in the middle.
- **D-02:** Retroactively replace Phase 33's `CheckboxList` editors (SqlTable `excludeFields` and `xmlColumns` on `PredicateEditScreen`) with `SelectMultiDual`. Consistent control across all multi-select fields.
- **D-03:** `SelectMultiDual` is the standard for all multi-select fields going forward (Phases 35-37 included).

### XML Type Discovery
- **D-04:** Discover XML types by **parsing live XML blobs from DB rows** — query actual `UrlDataProviderParameters` (Page) and `ModuleSettings` (Paragraph) columns, parse XML to extract distinct type identifiers.
- **D-05:** **Persist + manual rescan** — Initial Scan saves discovered types to config JSON. List screen reads from config (fast). "Rescan" button re-runs discovery and updates the persisted list.

### Tree Node Structure
- **D-06:** **Flat list under one "Embedded XML" node** — all discovered type names appear as a flat list, no grouping by source (modules vs URL providers). Mirrors the existing Predicates list pattern.

### Element Discovery
- **D-07:** Element names for a given XML type are discovered by **parsing live XML from DB** when the edit screen loads. Query XML blobs matching that type, extract all distinct root element names across rows. Shows real elements present in the data.

### Claude's Discretion
- SQL query structure for XML blob extraction (which tables/columns to scan, how to identify XML type per row)
- How to store discovered XML types in config (new top-level property or section)
- Caching strategy for element discovery within a single screen load
- Error handling for malformed XML blobs

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### SelectMultiDual Control
- `C:/Projects/temp/dw10source/Dynamicweb.CoreUI/Editors/Lists/SelectMultiDual.cs` -- Dual-pane multiselect: Options, EnableSorting, ForceEnableSearch, Groups, NoDataTextExcluded/Included
- `C:/Projects/temp/dw10source/Dynamicweb.CoreUI/Editors/Inputs/ListBase.cs` -- Base class with Options (List<ListOption>), SortOrder, ListOption { Value, Label }
- `C:/Projects/temp/dw10source/Dynamicweb.Application.UI/Screens/ScreenPresetEditScreen.cs` (lines 36-45) -- Usage example: how to construct SelectMultiDual with Options, Value, EnableSorting

### Tree Node Registration
- `src/DynamicWeb.Serializer/AdminUI/Tree/SerializerSettingsNodeProvider.cs` -- Existing tree node provider: NavigationNodeProvider<SystemSection>, node registration pattern, NavigateScreenAction.To<TScreen>().With(TQuery)

### Existing Screen Patterns
- `src/DynamicWeb.Serializer/AdminUI/Screens/PredicateListScreen.cs` -- List screen pattern: ListScreenBase<T>, GetViewMappings(), GetListItemPrimaryAction(), GetItemCreateAction()
- `src/DynamicWeb.Serializer/AdminUI/Screens/PredicateEditScreen.cs` -- Edit screen pattern: EditScreenBase<T>, BuildEditScreen(), GetEditor(), CreateColumnCheckboxList() (to be replaced with SelectMultiDual)
- `src/DynamicWeb.Serializer/AdminUI/Models/PredicateEditModel.cs` -- Edit model pattern with [ConfigurableProperty]
- `src/DynamicWeb.Serializer/AdminUI/Commands/SavePredicateCommand.cs` -- Save command pattern
- `src/DynamicWeb.Serializer/AdminUI/Queries/PredicateByIndexQuery.cs` -- Query pattern for loading edit models

### Config & Exclusion Infrastructure
- `src/DynamicWeb.Serializer/Configuration/SerializerConfiguration.cs` -- Top-level config with ExcludeXmlElementsByType dictionary
- `src/DynamicWeb.Serializer/Configuration/ExclusionMerger.cs` -- MergeXmlExclusions() unions per-predicate + type-specific exclusions
- `src/DynamicWeb.Serializer/Configuration/ConfigLoader.cs` -- Config loading with RawSerializerConfiguration
- `src/DynamicWeb.Serializer/Configuration/ConfigWriter.cs` -- Atomic JSON save

### XML Infrastructure
- `src/DynamicWeb.Serializer/Infrastructure/XmlFormatter.cs` -- PrettyPrint, RemoveElements, CompactWithMerge
- `src/DynamicWeb.Serializer/Serialization/ContentMapper.cs` (lines 81-86) -- Where effectiveXmlExclusions are applied using page.UrlDataProviderTypeName

### Requirements
- `.planning/REQUIREMENTS.md` -- XMLUI-01 through XMLUI-04

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `SelectMultiDual` from `Dynamicweb.CoreUI.Editors.Lists` — dual-pane multiselect with search, exactly matches the desired UX
- `SerializerSettingsNodeProvider` — extend with new "Embedded XML" node and dynamic child nodes per discovered type
- `PredicateListScreen` / `PredicateEditScreen` — patterns to follow for list and edit screens
- `DataGroupMetadataReader` (Phase 33) — SQL schema queries via INFORMATION_SCHEMA; similar pattern for XML blob queries
- `ExclusionMerger.MergeXmlExclusions()` — already handles union of per-predicate + typed exclusions

### Established Patterns
- Tree nodes: `NavigationNode` with `HasSubNodes`, `NavigateScreenAction.To<TScreen>().With(TQuery)`
- Edit screens: `EditScreenBase<T>`, editor factory via `GetEditor()` switch, `GetSaveCommand()`
- List screens: `ListScreenBase<T>`, `GetViewMappings()`, `GetListItemPrimaryAction()`
- Config persistence: `ConfigWriter.Save()` / `ConfigLoader.Load()` with System.Text.Json

### Integration Points
- `SerializerSettingsNodeProvider` — add "Embedded XML" node with dynamic children from config
- `SerializerConfiguration` — may need a new property for discovered XML type list (or reuse `excludeXmlElementsByType` keys)
- `PredicateEditScreen.CreateColumnCheckboxList()` — replace with SelectMultiDual-based helper
- `ContentMapper` / `SqlTableProvider` — already consume exclusions, no changes needed if config shape stays the same

</code_context>

<specifics>
## Specific Ideas

- Control reference: `SelectMultiDual` as seen on DW's `ScreenPresetEditScreen` for "visible fields" — searchable left pane, selected items on right pane
- Phase 33 retroactive change: swap `CheckboxList` to `SelectMultiDual` in `PredicateEditScreen` for SqlTable `excludeFields` and `xmlColumns`

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 34-embedded-xml-screens*
*Context gathered: 2026-04-14*
