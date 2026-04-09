# Feature Research: Structured UI Configuration (v0.6.0)

**Domain:** Structured UI controls replacing free-text fields for DynamicWeb serialization exclusion configuration
**Researched:** 2026-04-07
**Confidence:** HIGH (all CoreUI controls verified against DW10 source code)

## Feature Landscape

### Table Stakes (Users Expect These)

Features that replace existing free-text fields with structured alternatives. Without these, the v0.6 milestone delivers no value.

| Feature | Why Expected | Complexity | DW CoreUI Control | Notes |
|---------|--------------|------------|-------------------|-------|
| **Item Type field exclusion (Serialization tab)** | Currently free-text field names with no discoverability; users must guess field system names | MEDIUM | `CheckboxList` for <10 fields, `SelectMultiDual` for >10 | Inject tab via `EditScreenInjector<ItemTypeEditScreen, ItemTypeDataModel>`. Use `builder.AddComponents("Serialization", ...)`. Fields discovered from `ItemManager.Metadata.GetItemType(systemName).Fields`. Each field becomes a `ListOption` with `Value = field.SystemName, Label = field.Name`. Store excluded set in serializer config JSON keyed by ItemType systemName. |
| **Content page exclusion via multi-select** | Free-text page paths are error-prone and undiscoverable | MEDIUM | `SelectorBuilder.CreatePageSelector(multiselect: true, areaId: model.AreaId)` | DW page selector already supports `multiselect: true` natively. Returns comma-separated page IDs. Need to resolve IDs to paths for config storage. Replace `Textarea` in PredicateEditScreen with `Selector`. |
| **Read-only filtering summary on predicate screen** | Users need to see what is excluded without navigating away | LOW | `TextBlock` (read-only display) with `HtmlBlock` for formatted lists | Use `DisplayBase` components, not editors. `TextBlock` for labels, `Link` for clickable navigation to Item Edit / Embedded XML screens. Add as a new LayoutWrapper group "Active Filtering" below existing groups. |
| **SqlTable column picker (excludeFields)** | Free-text column names require DB knowledge | MEDIUM | `CheckboxList` or `SelectMultiDual` with options from schema query | Query `INFORMATION_SCHEMA.COLUMNS` for the selected table. Populate options dynamically. Requires `ReloadOnChange` on Table field to refresh column lists when table changes. |
| **SqlTable XML column picker** | Same discoverability problem as excludeFields | LOW | `CheckboxList` (typically <5 XML columns per table) | Subset of column picker: filter to columns likely containing XML content. Could auto-detect via content sampling or present full column list for manual selection. |

### Differentiators (Competitive Advantage)

Features beyond basic replacement that add real UX value.

| Feature | Value Proposition | Complexity | DW CoreUI Control | Notes |
|---------|-------------------|------------|-------------------|-------|
| **Embedded XML tree node with auto-discovery** | Zero-config discovery of XML element types across all predicates; no manual entry needed | HIGH | New `ListScreenBase` under Serialize tree + `CheckboxList` per XML type edit screen | Requires: (1) new tree node in `SerializerSettingsNodeProvider`, (2) XML type discovery by scanning serialized YAML or running sample serialization, (3) per-type element exclusion screen. Most complex feature in the milestone. |
| **Area column exclusion on Area Edit screen** | Area-level field filtering configured where area is managed, not buried in predicate screen | MEDIUM | `EditScreenInjector<AreaEditScreen, AreaDataModel>` with `CheckboxList` | Inject "Serialization" group into Area Edit screen. Discover area-level columns from area properties schema. Read-only summary on predicate screen links back to Area Edit. |
| **Clickable links from predicate read-only view** | Navigate directly to Item Type Edit or Embedded XML screen from predicate overview | LOW | `Link` display component + `NavigateScreenAction` | `Link` has `Text` and `Value` (URL target). Use `NavigateScreenAction.To<ItemTypeEditScreen>().With(query)` for internal navigation. Small effort, high polish. |
| **Auto-populated defaults for new predicates** | When adding SqlTable predicate, auto-fill xmlColumns and excludeFields from known table schemas | LOW | No special control; logic in query/model | Pre-populate model fields based on table selection. Reduces manual configuration for the ~74 SQL tables. |

### Anti-Features (Commonly Requested, Often Problematic)

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| **Inline editing of exclusions on predicate screen** | "Edit everything in one place" | Predicate screen becomes overloaded; ItemType exclusions are per-type (global), not per-predicate. Mixing scopes creates confusion about what applies where. | Read-only summary with links to dedicated edit screens |
| **Drag-and-drop field ordering** | "I want to control exclusion order" | Exclusions are a set (unordered); ordering adds false complexity and misleading UX | `CheckboxList` or `SelectMultiDual` without sorting |
| **Real-time preview of exclusion impact** | "Show me what gets excluded live" | Requires running full serialization preview; expensive, slow, and DW has no incremental serialize API | Dry-run button on settings screen (already exists from v0.5.0) |
| **Per-predicate ItemType exclusions** | "Different predicates need different field exclusions for the same ItemType" | ItemType is a global schema definition; per-predicate overrides create conflicting configs and confusing merge behavior | Global per-ItemType exclusion (matches Sitecore Unicorn model where fieldFilter is per-configuration, but field transforms apply per-include) |
| **Full CRUD for XML types in Embedded XML screen** | "Let me manually add XML type definitions" | Auto-discovery is the whole point; manual entry defeats the purpose and re-creates the free-text problem | Auto-discover only; manual override via config file for edge cases |

## DW CoreUI Control Reference (Verified Against Source)

### Multi-Select Controls

**CheckboxList** (`Dynamicweb.CoreUI.Editors.Lists.CheckboxList`)
- Extends `ListBase`. Doc says: "For selecting multiple options. Use this when you have less than 10 options."
- Value: array of selected option values (e.g., `new int[] { 1, 3 }` or `new string[] { "field1", "field2" }`).
- Used in DW10: `ItemTypeEditScreen.CreateEnabledForEditor()` for "Enabled for" structure context types.
- Best for: Item Type field exclusion (most types have <10 fields), XML column selection.

**SelectMultiDual** (`Dynamicweb.CoreUI.Editors.Lists.SelectMultiDual`)
- Dual-pane include/exclude with search. Properties: `EnableSorting`, `ForceEnableSearch`, `Groups`, `NoDataTextExcluded`, `NoDataTextIncluded`.
- `ReloadOnChangeBehavior.HorizontalMovement` fires reload when items move between panes.
- Best for: Large field lists (>10 fields), SQL table column selection where many columns exist.

**SelectMulti** (`Dynamicweb.CoreUI.Editors.Lists.SelectMulti`)
- Multi-select with all options visible. Supports `Groups` for categorized options.
- Best for: Grouped selections where categories matter (e.g., columns grouped by data type).

**Selector with multiselect** (`SelectorBuilder.CreatePageSelector(multiselect: true)`)
- Full tree-based dialog selector. Already used in project for single-select page/area.
- `SelectorBuilder.CreatePageSelector(multiselect: true, areaId: N)` returns multi-select page picker.
- Best for: Page exclusion (replaces free-text path list). Native DW tree-browsing UX.

### Read-Only Display Controls

**TextBlock** (`Dynamicweb.CoreUI.Displays.Information.TextBlock`)
- Read-only text display. Properties: `Bold`, `Italic`, `Alignment` (Left/Center/Right).
- Extends `TextDisplayBase` which extends `DisplayBase<string>`.
- Best for: Section labels and static text in read-only filtering summary.

**Link** (`Dynamicweb.CoreUI.Displays.Information.Link`)
- Clickable link display. Properties: `Text`, `Value` (URL target), `OpenInNewTab`.
- Extends `TextDisplayBase`. `Text` defaults to `Value` if not set.
- Best for: "Edit on Item Type screen" navigation links in predicate read-only view.

**HtmlBlock** (`Dynamicweb.CoreUI.Displays.Information.HtmlBlock`)
- Renders arbitrary HTML inline. Properties: `Inline`, `DisableValidation`.
- Extends `DisplayBase<string>`.
- Best for: Formatted exclusion summaries (bulleted lists of excluded fields/elements).

### Injection Patterns

**EditScreenInjector** (`Dynamicweb.CoreUI.Screens.EditScreenInjector<TScreen, TModel>`)
- Injects UI into existing edit screens. Three override points:
  - `OnBuildEditScreen(builder)` -- add components/tabs via `builder.AddComponents(tabName, heading, components)` or `builder.AddComponent(tabName, heading, component)`.
  - `GetEditor(propertyName, model)` -- provide custom editors for model properties.
  - `GetScreenActions()` -- add action buttons.
- Auto-discovered by `AddInManager`. Zero registration required.
- DW10 references: `PageEditScreenInjector` adds "Ecommerce" tab with 7 fields, `AreaEditScreenInjector` adds "Ecommerce settings" group with 7 fields.

**NavigationNodeProvider** (existing: `SerializerSettingsNodeProvider`)
- Adds tree nodes under Settings. `GetSubNodes(parentNodePath)` yields child nodes when parent is expanded.
- Extend existing provider to add "Embedded XML" under "Serialize" node.

### Tab Creation Pattern

Tabs are created implicitly by using a new tab name in `AddComponents(tabName, ...)`. The `EditScreenBuilder` in injectors uses the same API. DW10 AreaEditScreen demonstrates 4 tabs: "General", "Domain and URL", "Layout", "Advanced" -- each a separate `AddComponents` call.

For injectors: `builder.AddComponents("Serialization", "Field Exclusions", editors)` creates a "Serialization" tab if it does not already exist.

### ReadOnly Patterns

Two approaches verified in DW10 source:
1. **Editor with `Readonly = true`**: via `GetEditorMappings()` returning `CreateMapping(m => m.Field) with { ReadOnly = true }`. Renders as grayed-out input.
2. **Display components**: `TextBlock`, `Link`, `HtmlBlock` added directly via `builder.AddComponent()`. Not editable by design -- correct choice for the filtering summary.

## Feature Dependencies

```
Page multi-select picker
    (standalone, no dependencies on other v0.6 features)

Item Type field exclusion (Serialization tab)
    └──enables──> Read-only filtering display on predicate screen
                      └──enhanced by──> Clickable links to Item Edit screen

Embedded XML tree node
    └──enables──> Read-only filtering display (XML exclusion section)
    └──requires──> XML type auto-discovery logic (scan YAML output)

Area column exclusion (on Area Edit screen)
    └──enables──> Read-only filtering display (Area exclusion section)

SqlTable column picker
    └──requires──> Table field ReloadOnChange wiring (partially exists)
    └──enables──> SqlTable XML column picker (filtered subset)
```

### Dependency Notes

- **Read-only filtering display requires edit screens to exist first**: It reads stored exclusion config and links to Item Type Edit, Embedded XML, and Area Edit screens. Those screens must be built before the summary can reference them.
- **SqlTable XML picker is a strict subset of column picker**: Build the general column picker first, then filter to XML-containing columns for the XML picker.
- **Embedded XML auto-discovery is independent of other features**: Can scan existing serialized YAML files for XML content without depending on other v0.6 features.
- **Page multi-select is fully standalone**: Direct replacement of an existing Textarea control in PredicateEditScreen.

## MVP Definition

### Phase 1: Foundation Controls (High Value, Independent)

- [ ] **Item Type field exclusion tab** -- highest user value, most-used free-text field, showcases structured approach
- [ ] **Page exclusion multi-select** -- direct replacement using native DW selector, minimal new code
- [ ] **SqlTable column/XML pickers** -- completes structured UI for SQL predicates, uses same control patterns

### Phase 2: Advanced Screens (Complex, Enable Polish)

- [ ] **Embedded XML tree node + edit screen** -- most complex feature; needs auto-discovery, new tree node, list + edit screens
- [ ] **Area column exclusion on Area Edit** -- injector pattern proven in Phase 1, apply to different screen

### Phase 3: Polish & Integration

- [ ] **Read-only filtering display on predicate screen** -- requires Phase 1+2 screens to exist for links
- [ ] **Clickable navigation links** -- enhancement to read-only display
- [ ] **Auto-populated defaults for SqlTable predicates** -- quality-of-life optimization

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority | Phase |
|---------|------------|---------------------|----------|-------|
| Item Type field exclusion tab | HIGH | MEDIUM | P1 | 1 |
| Page exclusion multi-select | HIGH | LOW | P1 | 1 |
| SqlTable column picker | HIGH | MEDIUM | P1 | 1 |
| SqlTable XML column picker | MEDIUM | LOW | P1 | 1 |
| Embedded XML tree node | MEDIUM | HIGH | P2 | 2 |
| Area column exclusion | MEDIUM | MEDIUM | P2 | 2 |
| Read-only filtering display | MEDIUM | LOW | P2 | 3 |
| Clickable links from predicate | LOW | LOW | P3 | 3 |
| Auto-populated defaults | LOW | LOW | P3 | 3 |

## Implementation Sketches

### 1. Item Type Field Exclusion (Serialization Tab)

**New files:**
- `AdminUI/Injectors/SerializerItemTypeEditInjector.cs`
- `AdminUI/Commands/SaveItemTypeExclusionsCommand.cs` (or save via existing config mechanism)

**Pattern (modeled on DW10 `PageEditScreenInjector`):**
```csharp
public class SerializerItemTypeEditInjector : EditScreenInjector<ItemTypeEditScreen, ItemTypeDataModel>
{
    public override void OnBuildEditScreen(EditScreenBase<ItemTypeDataModel>.EditScreenBuilder builder)
    {
        var systemName = Screen?.Model?.SystemName;
        if (string.IsNullOrEmpty(systemName)) return;

        var fields = ItemManager.Metadata.GetItemType(systemName)?.Fields;
        if (fields == null || !fields.Any()) return;

        var checkboxList = new CheckboxList
        {
            Label = "Exclude from serialization",
            Name = "ExcludedFields",
            Value = GetCurrentExclusions(systemName),
            Explanation = "Checked fields will be omitted from serialized YAML output.",
            Options = fields.Select(f => new ListOption
            {
                Value = f.SystemName,
                Label = f.Name,
                Hint = f.SystemName
            }).ToList()
        };

        builder.AddComponent("Serialization", "Field Exclusions", checkboxList);
    }
}
```

**Storage:** Serializer config JSON, new `itemTypeExclusions` dictionary keyed by ItemType systemName.

### 2. Page Exclusion Multi-Select

**Change in `PredicateEditScreen.GetEditor`:**
```csharp
nameof(PredicateEditModel.Excludes) => SelectorBuilder.CreatePageSelector(
    multiselect: true,
    areaId: Model?.AreaId > 0 ? Model.AreaId : null,
    hint: "Select pages to exclude from serialization"
)
```

**Data conversion:** Selected page IDs must be converted to paths for portable config storage (IDs differ between environments). Resolve via `Services.Pages.GetPage(id)?.Path`.

### 3. SqlTable Column Pickers

**Change in `PredicateEditScreen.GetEditor`:**
```csharp
nameof(PredicateEditModel.ExcludeFields) => CreateColumnCheckboxList(Model?.Table, "Exclude Columns"),
nameof(PredicateEditModel.XmlColumns) => CreateColumnCheckboxList(Model?.Table, "XML Columns"),
```

Where `CreateColumnCheckboxList` queries `INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = table` and returns `CheckboxList` or `SelectMultiDual` depending on column count.

**Requires:** Table `Select` must use `ReloadOnChange = true` so column lists refresh when table changes.

### 4. Embedded XML Tree Node

**Extend `SerializerSettingsNodeProvider.GetSubNodes`:**
```csharp
yield return new NavigationNode
{
    Id = EmbeddedXmlNodeId,
    Name = "Embedded XML",
    Icon = Icon.Code,
    Sort = 15,
    HasSubNodes = false,
    NodeAction = NavigateScreenAction.To<EmbeddedXmlListScreen>()
        .With(new EmbeddedXmlListQuery())
};
```

**New screens:** `EmbeddedXmlListScreen` (list of discovered XML types), `EmbeddedXmlEditScreen` (element exclusion per type).

### 5. Read-Only Filtering Display

**Add to `PredicateEditScreen.BuildEditScreen` after existing groups:**
```csharp
var filteringComponents = new List<UiComponentBase>();
filteringComponents.Add(new TextBlock { Value = "Excluded Fields:", Bold = true });
filteringComponents.Add(new HtmlBlock { Value = BuildExclusionHtml(Model) });
filteringComponents.Add(new Link { Text = "Edit on Item Type screen", Value = itemTypeEditUrl });

groups.Add(new("Active Filtering", filteringComponents));
```

### 6. Area Column Exclusion

**New file: `AdminUI/Injectors/SerializerAreaEditInjector.cs`**

Same pattern as Item Type injector, targeting `EditScreenInjector<AreaEditScreen, AreaDataModel>`. Area properties discovered from area schema. Injected as "Serialization" group under "Advanced" tab or as its own tab.

## Competitor Feature Analysis

| Feature | Sitecore Unicorn | Sitecore TDS | Our Approach |
|---------|------------------|--------------|--------------|
| Field exclusion UI | Config XML (no UI) | VS project checkboxes | CheckboxList on ItemType Edit screen -- discoverable and in-context |
| Path exclusion | Config XML predicates | Project item include/exclude | Multi-select page picker with tree browsing -- native DW UX |
| XML element control | Not supported (no embedded XML) | Not supported | Auto-discovered XML types with element-level CheckboxList -- unique |
| Configuration summary | Unicorn Control Panel (web page) | VS solution explorer | Read-only display with links embedded in predicate edit screen |
| Column exclusion | N/A (field-based) | N/A | CheckboxList populated from INFORMATION_SCHEMA -- schema-aware |

## Sources

- DW10 source: `C:\Projects\temp\dw10source\Dynamicweb.CoreUI\Editors\Lists\CheckboxList.cs` -- multi-select <10 options
- DW10 source: `C:\Projects\temp\dw10source\Dynamicweb.CoreUI\Editors\Lists\SelectMultiDual.cs` -- dual-pane include/exclude
- DW10 source: `C:\Projects\temp\dw10source\Dynamicweb.CoreUI\Editors\Lists\SelectMulti.cs` -- multi-select with groups
- DW10 source: `C:\Projects\temp\dw10source\Dynamicweb.CoreUI\Editors\Selectors\SelectorBuilder.cs` -- `CreatePageSelector(multiselect: true)`
- DW10 source: `C:\Projects\temp\dw10source\Dynamicweb.CoreUI\Screens\EditScreenInjector.cs` -- tab injection pattern
- DW10 source: `C:\Projects\temp\dw10source\Dynamicweb.CoreUI\Screens\EditScreenBase.cs` -- `EditScreenBuilder` API
- DW10 source: `C:\Projects\temp\dw10source\Dynamicweb.CoreUI\Displays\Information\TextBlock.cs` -- read-only text
- DW10 source: `C:\Projects\temp\dw10source\Dynamicweb.CoreUI\Displays\Information\Link.cs` -- clickable links
- DW10 source: `C:\Projects\temp\dw10source\Dynamicweb.CoreUI\Displays\Information\HtmlBlock.cs` -- HTML display
- DW10 source: `C:\Projects\temp\dw10source\Dynamicweb.Global.UI\Content\PageEditScreenInjector.cs` -- reference injector adding "Ecommerce" tab
- DW10 source: `C:\Projects\temp\dw10source\Dynamicweb.Global.UI\Content\AreaEditScreenInjector.cs` -- reference injector for area screen
- DW10 source: `C:\Projects\temp\dw10source\Dynamicweb.Content.UI\Screens\Settings\ItemTypes\ItemTypeEditScreen.cs` -- CheckboxList usage example

---
*Feature research for: DynamicWeb.Serializer v0.6.0 Structured UI Configuration*
*Researched: 2026-04-07*
