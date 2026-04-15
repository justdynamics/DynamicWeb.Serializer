# Phase 35: Item Type Screens - Context

**Gathered:** 2026-04-15
**Status:** Ready for planning

<domain>
## Phase Boundary

Add an "Item Types" tree node under Serialize that lists all item types discovered via the DW ItemType API, organized by category path in the tree. Each item type edit screen shows type metadata and a `SelectMultiDual` for per-item-type field exclusions persisted to `excludeFieldsByItemType`.

</domain>

<decisions>
## Implementation Decisions

### Item Type Discovery
- **D-01:** Discover item types via **DW ItemType API** (`ItemType.GetAllItemTypes()` or equivalent service call). Not SQL — use the idiomatic DW API.
- **D-02:** **Live discovery on every screen load** — no scan/persist pattern. ItemType API is fast and always reflects the current system state. No scan button needed.

### Field Discovery
- **D-03:** Discover fields per item type via **DW ItemType field API** (the `Fields` collection on the `ItemType` object). Not SQL.

### Tree Node Structure
- **D-04:** Flat list screen + **expandable tree children organized by category path**. Item type categories use `/` as separator (e.g., `Swift-v2/Utilities`). The tree renders as nested folders: `Item Types > Swift-v2 > Utilities > CartApps`.
- **D-05:** Item types with **no category or empty category** are grouped under an **"Uncategorized"** folder in the tree.

### Edit Screen
- **D-06:** Edit screen shows **type name, category, and total field count as read-only info** plus `SelectMultiDual` for field exclusions.
- **D-07:** `SelectMultiDual` is the standard control (per Phase 34 D-01/D-03).

### Claude's Discretion
- Exact DW API calls for ItemType discovery and field enumeration (research which service/static accessor to use)
- How to extract category from ItemType (property name, parsing logic)
- How tree node IDs are structured for nested category folders
- Whether to show field data types alongside field names in the SelectMultiDual options

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### DW ItemType API (research needed)
- `C:/Projects/temp/dw10source/` — Search for `ItemType`, `GetAllItemTypes`, `ItemTypeField` to find the correct API surface

### SelectMultiDual Control
- `C:/Projects/temp/dw10source/Dynamicweb.CoreUI/Editors/Lists/SelectMultiDual.cs` — Dual-pane multiselect
- `C:/Projects/temp/dw10source/Dynamicweb.CoreUI/Editors/Inputs/ListBase.cs` — Base with Options, ListOption

### Phase 34 Patterns (mirror these)
- `src/DynamicWeb.Serializer/AdminUI/Tree/SerializerSettingsNodeProvider.cs` — Tree node registration with expandable children and category-based nesting (Predicates + Embedded XML patterns)
- `src/DynamicWeb.Serializer/AdminUI/Screens/XmlTypeListScreen.cs` — List screen with action menu and empty-state create action
- `src/DynamicWeb.Serializer/AdminUI/Screens/XmlTypeEditScreen.cs` — Edit screen with SelectMultiDual + read-only info + sample display
- `src/DynamicWeb.Serializer/AdminUI/Commands/ScanXmlTypesCommand.cs` — CommandBase (non-generic) pattern for model-less commands
- `src/DynamicWeb.Serializer/AdminUI/Queries/XmlTypeListQuery.cs` — List query from config
- `src/DynamicWeb.Serializer/AdminUI/Queries/XmlTypeByNameQuery.cs` — Edit query with SetKey wiring via DataQueryIdentifiableModelBase

### Config Infrastructure
- `src/DynamicWeb.Serializer/Configuration/SerializerConfiguration.cs` — Top-level config with ExcludeFieldsByItemType dictionary
- `src/DynamicWeb.Serializer/Configuration/ExclusionMerger.cs` — MergeFieldExclusions() unions per-predicate + type-specific exclusions
- `src/DynamicWeb.Serializer/Configuration/ConfigWriter.cs` — Atomic JSON save

### Requirements
- `.planning/REQUIREMENTS.md` — ITEM-01 through ITEM-03

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `SelectMultiDual` — established as standard multi-select control
- `SerializerSettingsNodeProvider` — extend with "Item Types" node and category-based nested children
- `XmlTypeListScreen` / `XmlTypeEditScreen` — patterns to follow for list and edit screens
- `ExclusionMerger.MergeFieldExclusions()` — already handles union of per-predicate + typed exclusions
- `ConfigWriter.Save()` / `ConfigLoader.Load()` — config persistence

### Established Patterns
- Tree nodes with `HasSubNodes = true` and category-based children (new for this phase)
- List screens: `ListScreenBase<T>`, `GetViewMappings()`, `GetListItemPrimaryAction()`
- Edit screens: `EditScreenBase<T>`, `GetEditor()`, `GetSaveCommand()`, read-only info fields
- Commands: `CommandBase` (non-generic) for model-less, `CommandBase<T>` for save commands

### Integration Points
- `SerializerSettingsNodeProvider` — add "Item Types" node with nested category children
- `SerializerConfiguration.ExcludeFieldsByItemType` — already exists from Phase 32, stores per-type field exclusions
- No scan command needed (live discovery) — simpler than Embedded XML pattern

</code_context>

<specifics>
## Specific Ideas

- Category path parsing: split on `/` to create nested tree folders (e.g., `Swift-v2/Utilities` → two tree levels)
- Tree node example: `Item Types > Swift-v2 > Utilities > Swift-v2-CartApps`
- Uncategorized items grouped under an "Uncategorized" folder node

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 35-item-type-screens*
*Context gathered: 2026-04-15*
