# Architecture Patterns

**Domain:** DynamicWeb Serializer v0.6.0 — Structured UI Integration
**Researched:** 2026-04-07
**Confidence:** HIGH (verified against DW10 source at C:\Projects\temp\dw10source\)

## Recommended Architecture

The v0.6.0 UI features integrate into three distinct DW extension points, all auto-discovered by DW's AddInManager. No manual registration needed.

### Integration Point Summary

| Feature | DW Extension Point | Base Class | Target Screen | New/Modified |
|---------|-------------------|------------|---------------|-------------|
| Item Edit "Serialization" action | ListScreenInjector | `ListScreenInjector<ItemFieldListScreen, ItemFieldDataModel>` | Settings > Item Types > Fields | **NEW** injector |
| Area Edit "Serialization" action | EditScreenInjector | `EditScreenInjector<AreaEditScreen, AreaDataModel>` | Content > Area > Edit | **NEW** injector |
| Embedded XML tree node | NavigationNodeProvider | Extend existing `SerializerSettingsNodeProvider` | Settings > Database > Serialize | **MODIFY** existing |
| Predicate read-only filtering | EditScreenBase | Modify existing `PredicateEditScreen` | Settings > Database > Serialize > Predicates | **MODIFY** existing |
| SqlTable column pickers | EditScreenBase | Modify existing `PredicateEditScreen` | Settings > Database > Serialize > Predicates | **MODIFY** existing |
| Embedded XML edit screen | EditScreenBase | `EditScreenBase<EmbeddedXmlEditModel>` | (new tree node) | **NEW** screen |
| Item type serialization screen | EditScreenBase | `EditScreenBase<ItemTypeSerializationModel>` | (navigated from injector) | **NEW** screen |

## Critical Architectural Finding: ListScreenInjector vs EditScreenInjector

**`ItemFieldListScreen` extends `ListScreenBase<ItemFieldListDataModel, ItemFieldDataModel>`, NOT `EditScreenBase`.** This is the single most important finding. Using `EditScreenInjector` would fail at compile time due to generic constraints.

The correct injection point is:

```csharp
public sealed class SerializerItemFieldListInjector
    : ListScreenInjector<ItemFieldListScreen, ItemFieldDataModel>
```

`ListScreenInjector<TScreen, TRowModel>` provides three hooks:
- `GetScreenActions()` — toolbar buttons (what we need)
- `GetListItemActions(TRowModel model)` — per-row context menu items
- `GetCell(string propertyName, TRowModel model)` — custom cell rendering

By contrast, `AreaEditScreen` extends `EditScreenBase<AreaDataModel>`, so its injector correctly uses:

```csharp
public sealed class SerializerAreaEditInjector
    : EditScreenInjector<AreaEditScreen, AreaDataModel>
```

## Component Boundaries

### New Components

| Component | Type | Responsibility | Communicates With |
|-----------|------|---------------|-------------------|
| `SerializerItemFieldListInjector` | ListScreenInjector | Adds "Serialization" toolbar action on ItemFieldListScreen | Screen.Model for ItemTypeSystemName |
| `SerializerAreaEditInjector` | EditScreenInjector | Adds "Serialization" toolbar action on AreaEditScreen | Screen.Model for AreaId |
| `ItemTypeSerializationScreen` | EditScreenBase | Per-item-type field exclusion checkboxes | ConfigLoader, ConfigWriter, ItemManager.Metadata |
| `ItemTypeSerializationModel` | DataViewModelBase | Item type name + field exclusion state | N/A |
| `ItemTypeSerializationQuery` | QueryBase | Carries ItemTypeSystemName + predicate index | N/A |
| `SaveItemTypeSerializationCommand` | CommandBase | Writes `excludeFieldsByItemType` to config | ConfigLoader, ConfigWriter |
| `AreaSerializationScreen` | EditScreenBase | Area column exclusion management | ConfigLoader, ConfigWriter, DataGroupMetadataReader |
| `AreaSerializationModel` | DataViewModelBase | Area columns + exclusion state | N/A |
| `AreaSerializationQuery` | QueryBase | Carries AreaId | N/A |
| `SaveAreaSerializationCommand` | CommandBase | Writes area exclusions to matching predicate | ConfigLoader, ConfigWriter |
| `EmbeddedXmlListScreen` | ListScreenBase | Lists discovered XML types across predicates | ConfigLoader |
| `EmbeddedXmlListModel` | DataListViewModel | List of XML types with exclusion counts | N/A |
| `EmbeddedXmlEditScreen` | EditScreenBase | Per-XML-type element exclusion management | ConfigLoader, ConfigWriter |
| `EmbeddedXmlEditModel` | DataViewModelBase | XML type + excluded elements | N/A |
| `SaveEmbeddedXmlCommand` | CommandBase | Writes `excludeXmlElementsByType` to config | ConfigLoader, ConfigWriter |

### Modified Components

| Component | Change | Why |
|-----------|--------|-----|
| `SerializerSettingsNodeProvider` | Add "Embedded XML" child node under "Serialize" (sort=15, between Predicates and Log Viewer) | New tree entry for XML management |
| `PredicateEditScreen` | Replace ExcludeFields/ExcludeXmlElements textareas with read-only summaries + navigation links | Filtering moves to dedicated screens |
| `ProviderPredicateDefinition` | Add `ExcludeFieldsByItemType` and `ExcludeXmlElementsByType` dictionaries | Per-item-type and per-XML-type exclusions |
| `ConfigLoader` | Parse new dictionary fields from JSON | Read per-type exclusions |
| `ConfigWriter` | No code change needed (System.Text.Json serializes dictionaries natively) | N/A |

## Detailed Integration Patterns

### 1. ItemFieldListScreen Injection (Toolbar Action)

The `ItemFieldListDataModel` carries `ItemTypeSystemName` which is available via `Screen.Model`. The injector adds a toolbar action that navigates to a new serializer-owned screen.

```csharp
public sealed class SerializerItemFieldListInjector
    : ListScreenInjector<ItemFieldListScreen, ItemFieldDataModel>
{
    public override IEnumerable<ActionGroup>? GetScreenActions()
    {
        var model = Screen?.Model;
        if (model is null || string.IsNullOrEmpty(model.ItemTypeSystemName))
            return null;

        return new[]
        {
            new ActionGroup
            {
                Nodes = new List<ActionNode>
                {
                    new()
                    {
                        Name = "Serialization",
                        Icon = Icon.Exchange,
                        NodeAction = NavigateScreenAction
                            .To<ItemTypeSerializationScreen>()
                            .With(new ItemTypeSerializationQuery
                            {
                                ItemTypeSystemName = model.ItemTypeSystemName
                            })
                    }
                }
            }
        };
    }
}
```

**ItemTypeSerializationScreen** responsibilities:
1. Load item type fields via `ItemManager.Metadata.GetItemType(systemName)` — returns field names, types, groups
2. Load all predicates from config; find any that have this item type in `ExcludeFieldsByItemType`
3. Render a `CheckboxList` of field names — checked = excluded from serialization
4. `SaveItemTypeSerializationCommand` writes updated `ExcludeFieldsByItemType` across all relevant predicates

**Which predicates?** The screen must show which predicates this item type is relevant to. For Content predicates, any predicate whose area uses this item type (check via DW's `ItemHelper.GetAllowedItemsForAreaProperties()`). For simplicity in v0.6.0, the screen could apply exclusions globally to all predicates or let the user pick a predicate.

### 2. AreaEditScreen Injection (Toolbar Action)

**Why toolbar action, not tab:** `AreaDataModel` is a DW framework class. We cannot add properties to it. The `EditScreenInjector.OnBuildEditScreen(builder)` can call `builder.AddComponents("Serialization", ...)` which creates a tab, BUT `builder.EditorFor()` only works for properties that exist on `AreaDataModel`. Our serializer data (excluded columns) lives in our config JSON, not on the DW model.

**Solution:** Follow the same pattern as `SerializerPageEditInjector` — add a toolbar action via `GetScreenActions()` that navigates to a serializer-owned screen.

```csharp
public sealed class SerializerAreaEditInjector
    : EditScreenInjector<AreaEditScreen, AreaDataModel>
{
    public override IEnumerable<ActionGroup>? GetScreenActions()
    {
        var model = Screen?.Model;
        if (model is null || model.Id <= 0)
            return null;

        return new[]
        {
            new ActionGroup
            {
                Nodes = new List<ActionNode>
                {
                    new()
                    {
                        Name = "Serialization",
                        Icon = Icon.Exchange,
                        NodeAction = NavigateScreenAction
                            .To<AreaSerializationScreen>()
                            .With(new AreaSerializationQuery { AreaId = model.Id })
                    }
                }
            }
        };
    }
}
```

**AreaSerializationScreen** responsibilities:
1. Find Content predicates matching this AreaId from config
2. Load Area table columns via `DataGroupMetadataReader` (reuse existing `INFORMATION_SCHEMA` queries)
3. Render CheckboxList of column names — checked = excluded
4. Save back to the matching predicate's `ExcludeFields` list

### 3. Embedded XML Tree Node

Modify `SerializerSettingsNodeProvider.GetSubNodes()` to add a new node:

```csharp
// New constant:
internal const string EmbeddedXmlNodeId = "Serializer_EmbeddedXml";

// In GetSubNodes, under the SerializeNodeId branch, add:
yield return new NavigationNode
{
    Id = EmbeddedXmlNodeId,
    Name = "Embedded XML",
    Icon = Icon.Code,
    Sort = 15, // between Predicates (10) and Log Viewer (20)
    HasSubNodes = false,
    NodeAction = NavigateScreenAction.To<EmbeddedXmlListScreen>()
        .With(new EmbeddedXmlListQuery())
};
```

**EmbeddedXmlListScreen** discovers XML types:
- For Content predicates: known XML-bearing fields are `ModuleSettings`, `UrlDataProviderParameters`
- For SqlTable predicates: configured `XmlColumns` list on each predicate
- Aggregates into a list of XML type names with current exclusion counts

Clicking a type navigates to `EmbeddedXmlEditScreen` where individual element names can be toggled.

### 4. Predicate Read-Only Filtering View

Replace the "Filtering" group in `PredicateEditScreen.BuildEditScreen()`:

**Before (current):**
```csharp
groups.Add(new("Filtering", new List<EditorBase>
{
    EditorFor(m => m.ExcludeFields),      // Textarea
    EditorFor(m => m.ExcludeXmlElements)   // Textarea
}));
```

**After (v0.6.0):**
Display a read-only summary showing "3 fields excluded across 2 item types" with navigation links to the ItemTypeSerializationScreen and EmbeddedXmlListScreen. Implementation options:

1. Use `Text` editor with `ReadOnly = true` showing a summary string
2. Use `InfoBar` or `Widget` to display counts with action links
3. Keep fields as `ReadOnly` textareas showing current exclusion values (simplest)

The third option (read-only textareas) is simplest and still communicates the data. Add `NavigateScreenAction` links in the `GetScreenActions()` or as action nodes in the filtering group.

### 5. SqlTable Column Pickers

Replace free-text textareas with `CheckboxList` editors populated from INFORMATION_SCHEMA:

```csharp
// In PredicateEditScreen.GetEditor():
nameof(PredicateEditModel.Table) => CreateTableSelect(),  // Add ReloadOnChange
nameof(PredicateEditModel.ExcludeFields) => Model?.ProviderType == "SqlTable"
    ? CreateColumnCheckboxList(Model?.Table, "Exclude Fields")
    : new Textarea { ... },  // Keep textarea for Content predicates
nameof(PredicateEditModel.XmlColumns) => CreateColumnCheckboxList(Model?.Table, "XML Columns"),
```

Column discovery reuses `DataGroupMetadataReader.QueryAllColumns(tableName)` pattern — `SELECT TOP 0 * FROM [tableName]` then read field names from the reader.

**ReloadOnChange:** The `Table` field's Select editor gets `ReloadOnChange = true`. When the user picks a table, the screen reloads, and `Model.Table` is populated for the `CreateColumnCheckboxList` calls.

### 6. Config Storage Changes

**Current JSON structure:**
```json
{
  "predicates": [{
    "excludeFields": ["field1", "field2"],
    "excludeXmlElements": ["elem1"]
  }]
}
```

**Proposed JSON structure (additive):**
```json
{
  "predicates": [{
    "excludeFields": ["field1"],
    "excludeFieldsByItemType": {
      "Swift_Footer": ["FooterColumn3", "FooterColumn4"],
      "Swift_Header": ["HeaderSearch"]
    },
    "excludeXmlElements": ["elem1"],
    "excludeXmlElementsByType": {
      "ModuleSettings": ["SomeModule"],
      "UrlDataProviderParameters": ["SomeParam"]
    }
  }]
}
```

**Changes to ProviderPredicateDefinition:**
```csharp
public Dictionary<string, List<string>> ExcludeFieldsByItemType { get; init; } = new();
public Dictionary<string, List<string>> ExcludeXmlElementsByType { get; init; } = new();
```

**Changes to ConfigLoader:** Add dictionary parsing in `RawPredicateDefinition` and `BuildPredicate()`. System.Text.Json handles `Dictionary<string, List<string>>` natively — just add nullable properties to the raw model.

**Changes to serialization logic:** During content serialization, merge flat `ExcludeFields` with item-type-specific `ExcludeFieldsByItemType[currentItemType]` to get the effective exclusion set.

## Data Flow

### Field Exclusion Configuration Flow

```
User opens Settings > Item Types > [Type] > Fields
  -> ItemFieldListScreen renders field list
  -> SerializerItemFieldListInjector adds "Serialization" toolbar button
  -> User clicks "Serialization"
  -> NavigateScreenAction to ItemTypeSerializationScreen
  -> Screen loads fields from ItemManager.Metadata.GetItemType(systemName)
  -> Screen loads exclusions from ConfigLoader (all predicates' excludeFieldsByItemType)
  -> User toggles field checkboxes
  -> SaveItemTypeSerializationCommand writes to config JSON via ConfigWriter
```

### Predicate SqlTable Column Picker Flow

```
User opens Serialize > Predicates > [SqlTable predicate]
  -> PredicateEditScreen renders with Model.Table populated
  -> Table Select has ReloadOnChange = true
  -> ExcludeFields editor calls DataGroupMetadataReader for column list
  -> CheckboxList populated with all table columns
  -> User checks columns to exclude
  -> SavePredicateCommand persists as excludeFields list
```

## Patterns to Follow

### Pattern 1: ListScreenInjector for Toolbar Actions on List Screens
**What:** Add toolbar actions to DW list screens without modifying DW source
**When:** Target screen extends `ListScreenBase` (ItemFieldListScreen)
**Key:** Override `GetScreenActions()`, access `Screen.Model` for context data
**Example:** `SerializerItemFieldListInjector` — verified via `Dynamicweb.CoreUI\Screens\ListScreenInjector.cs`

### Pattern 2: EditScreenInjector GetScreenActions for Action Buttons
**What:** Add toolbar actions to DW edit screens
**When:** Target screen extends `EditScreenBase` but you cannot add properties to its model
**Key:** Override `GetScreenActions()` (not `OnBuildEditScreen`). Navigate to serializer-owned screen.
**Example:** Existing `SerializerPageEditInjector` in codebase; `AreaEditScreenInjector` in DW source

### Pattern 3: EditScreenInjector OnBuildEditScreen for New Tabs
**What:** Add entire tab sections to DW edit screens
**When:** You can bind to properties on the existing model
**Key:** `builder.AddComponents("TabName", "Heading", editors)` — each unique tabName creates a tab automatically
**Example:** `AreaEditScreenInjector` (Ecommerce tab) in `Dynamicweb.Global.UI`
**Why NOT used here:** We cannot add properties to AreaDataModel, so EditorFor() has nothing to bind to

### Pattern 4: ReloadOnChange for Dependent Fields
**What:** Trigger screen reload when a field value changes to populate dependent fields
**When:** One field determines another's options (table name -> column list)
**Key:** Set `ReloadOnChange = true` on driving editor; check `Model?.Property` in dependent editor builder
**Example:** `AreaEditScreen.CreateAreaItemSelect()` — reloads to show/hide WebsiteItem fields

### Pattern 5: NavigationNode Extension
**What:** Add tree nodes under existing tree structure
**When:** Need new entries in the Settings tree
**Key:** Match `parentNodePath.Last` against known node IDs, yield new NavigationNode
**Example:** Existing `SerializerSettingsNodeProvider` — add another yield return in SerializeNodeId branch

## Anti-Patterns to Avoid

### Anti-Pattern 1: EditScreenInjector on a ListScreenBase
**What:** Using `EditScreenInjector<ItemFieldListScreen, ItemFieldListDataModel>` 
**Why bad:** `ItemFieldListScreen` extends `ListScreenBase`, not `EditScreenBase`. Generic constraint `where TScreen : EditScreenBase<TModel>` would fail at compile time.
**Instead:** Use `ListScreenInjector<ItemFieldListScreen, ItemFieldDataModel>`

### Anti-Pattern 2: Adding Properties to DW Framework Models
**What:** Trying to extend `AreaDataModel` or `PageDataModel` with serializer properties
**Why bad:** These are compiled into DW NuGet packages — not our code
**Instead:** Use toolbar actions that navigate to serializer-owned screens with their own models

### Anti-Pattern 3: Direct SQL in UI Screen Classes
**What:** Running INFORMATION_SCHEMA queries directly in screen/editor builder methods
**Why bad:** Tight coupling, harder to test, mixes concerns
**Instead:** Reuse `DataGroupMetadataReader` which already has INFORMATION_SCHEMA queries with ISqlExecutor abstraction for testability

### Anti-Pattern 4: Storing Per-Item-Type Exclusions Outside Predicates
**What:** Creating a separate config section for item type exclusions
**Why bad:** Exclusions are predicate-scoped — different predicates may exclude different fields for the same item type
**Instead:** Store `excludeFieldsByItemType` inside each predicate definition

## Build Order (Dependency-Aware)

### Phase 1: Config Schema Extension (foundation)
1. Add `ExcludeFieldsByItemType` and `ExcludeXmlElementsByType` dictionaries to `ProviderPredicateDefinition`
2. Add nullable dictionary properties to `ConfigLoader.RawPredicateDefinition`
3. Update `ConfigLoader.BuildPredicate()` to map new dictionaries
4. Wire dictionaries through to serialization logic (merge with flat lists during serialize)

**Rationale:** Every UI feature depends on config being able to store per-type exclusions.

### Phase 2: SqlTable Column Pickers (predicate screen enhancement)
1. Add `ReloadOnChange` to Table Select in `PredicateEditScreen`
2. Create `CreateColumnCheckboxList(tableName)` helper reusing `DataGroupMetadataReader` pattern
3. Replace ExcludeFields textarea with CheckboxList for SqlTable predicates
4. Replace XmlColumns textarea with CheckboxList for SqlTable predicates

**Rationale:** Enhances existing screen with minimal new code. Good warmup for DW UI patterns.

### Phase 3: Embedded XML Tree Node + Screens
1. Add `EmbeddedXmlNodeId` constant and node to `SerializerSettingsNodeProvider.GetSubNodes()`
2. Create `EmbeddedXmlListScreen` + query + model (lists XML types from config)
3. Create `EmbeddedXmlEditScreen` + query + model + save command (per-type element exclusion)
4. Save command writes `ExcludeXmlElementsByType` to config

**Rationale:** Self-contained feature under existing Serialize tree. No external injection needed.

### Phase 4: Item Type Serialization Screen + Injector
1. Create `ItemTypeSerializationScreen` + query + model (field checkbox list using `ItemManager.Metadata`)
2. Create `SaveItemTypeSerializationCommand` (writes `ExcludeFieldsByItemType`)
3. Create `SerializerItemFieldListInjector` (ListScreenInjector — toolbar action)

**Rationale:** Most complex new screen. Depends on Phase 1 config. Uses `ItemManager.Metadata` API.

### Phase 5: Area Edit Injection
1. Create `AreaSerializationScreen` + query + model (area column checkboxes via INFORMATION_SCHEMA)
2. Create `SaveAreaSerializationCommand` (writes to matching predicate's `ExcludeFields`)
3. Create `SerializerAreaEditInjector` (EditScreenInjector — toolbar action)

**Rationale:** Similar to Phase 4 but for area columns. Reuses `DataGroupMetadataReader`.

### Phase 6: Predicate Read-Only Filtering View
1. Modify `PredicateEditScreen` "Filtering" section to read-only display of current exclusions
2. Add navigation links to ItemTypeSerializationScreen and EmbeddedXmlListScreen
3. Show summary counts: "X fields excluded across Y item types", "Z XML elements excluded"

**Rationale:** Last because it links to screens built in Phases 3-5. Cannot add links until targets exist.

### Dependency Graph

```
Phase 1: Config Schema
    |
    +---> Phase 2: SqlTable Column Pickers
    |
    +---> Phase 3: Embedded XML Screens
    |
    +---> Phase 4: Item Type Serialization
    |         |
    |         +---> Phase 6: Predicate Read-Only
    |
    +---> Phase 5: Area Edit Injection
              |
              +---> Phase 6: Predicate Read-Only
```

Phases 2-5 all depend on Phase 1 but are independent of each other.
Phase 6 depends on Phases 3, 4, and 5 (links to their screens).

## DW Screen Class Verification

All class names verified against DW10 source at `C:\Projects\temp\dw10source\`:

| Class | Base | Namespace |
|-------|------|-----------|
| `ItemFieldListScreen` | `ListScreenBase<ItemFieldListDataModel, ItemFieldDataModel>` | `Dynamicweb.Content.UI.Screens.Settings.ItemTypes` |
| `ItemFieldDataModel` | `DataViewModelBase` | `Dynamicweb.Content.UI.Models.Settings.ItemTypes` |
| `ItemFieldListDataModel` | `DataListViewModel<ItemFieldDataModel>` | `Dynamicweb.Content.UI.Models.Settings.ItemTypes` |
| `AreaEditScreen` | `EditScreenBase<AreaDataModel>` | `Dynamicweb.Content.UI.Screens` |
| `AreaDataModel` | (DW model) | `Dynamicweb.Content.UI.Models` |
| `EditScreenInjector<T,M>` | `ScreenInjector<T>` | `Dynamicweb.CoreUI.Screens` |
| `ListScreenInjector<T,R>` | `ScreenInjector<T>` | `Dynamicweb.CoreUI.Screens` |
| `ItemTypeEditScreen` | `EditScreenBase<ItemTypeDataModel>` (has tab: RestrictionsTabName) | `Dynamicweb.Content.UI.Screens.Settings.ItemTypes` |

**Key APIs confirmed:**
- `ItemFieldListDataModel.ItemTypeSystemName` — available for injector context
- `AreaDataModel.Id` — available for injector context (confirmed AreaEditScreen uses `Model.Id`)
- `ItemManager.Metadata.GetItemType(systemName)` — returns field metadata for checkbox list
- `builder.AddComponents(tabName, heading, editors)` — creates tabs in EditScreenInjector
- `ListScreenInjector.GetScreenActions()` — adds toolbar actions to list screens

## NuGet Reference Requirements

No new NuGet dependencies needed. All required packages already referenced:
- `Dynamicweb.Content.UI` — ItemFieldListScreen, AreaEditScreen, data models
- `Dynamicweb.CoreUI` — ListScreenInjector, EditScreenInjector, editors
- `Dynamicweb.Content` — ItemManager.Metadata for field discovery

## Sources

- DW10 source: `Dynamicweb.CoreUI\Screens\EditScreenInjector.cs` — EditScreenInjector pattern with OnBuildEditScreen, GetEditor, GetScreenActions
- DW10 source: `Dynamicweb.CoreUI\Screens\ListScreenInjector.cs` — ListScreenInjector pattern with GetScreenActions, GetListItemActions, GetCell
- DW10 source: `Dynamicweb.CoreUI\Screens\EditScreenBase.cs` — EditScreenBuilder.AddComponents creates tabs, injector lifecycle at lines 104-113
- DW10 source: `Dynamicweb.Global.UI\Content\AreaEditScreenInjector.cs` — Reference implementation adding "Ecommerce" tab via builder.AddComponents
- DW10 source: `Dynamicweb.Global.UI\Content\PageEditScreenInjector.cs` — Reference implementation adding "Ecommerce navigation" fields
- DW10 source: `Dynamicweb.Content.UI\Screens\Settings\ItemTypes\ItemFieldListScreen.cs` — Confirmed ListScreenBase<ItemFieldListDataModel, ItemFieldDataModel>
- DW10 source: `Dynamicweb.Content.UI\Screens\AreaEditScreen.cs` — Confirmed EditScreenBase<AreaDataModel>
- Existing codebase: `SerializerPageEditInjector.cs` — Existing EditScreenInjector toolbar action pattern
- Existing codebase: `SerializerFileOverviewInjector.cs` — Existing ScreenInjector OnAfter pattern
- Existing codebase: `DataGroupMetadataReader.cs` — INFORMATION_SCHEMA queries for column discovery

---
*Architecture research for: DynamicWeb.Serializer v0.6.0 structured UI integration*
*Researched: 2026-04-07*
