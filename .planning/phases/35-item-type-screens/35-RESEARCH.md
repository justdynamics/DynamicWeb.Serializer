# Phase 35: Item Type Screens - Research

**Researched:** 2026-04-14
**Domain:** DW10 ItemType API + AdminUI tree/list/edit screens
**Confidence:** HIGH

## Summary

Phase 35 adds an "Item Types" tree node under Serialize that lists all item types discovered via the DW ItemType API, organized by category path in the tree. Each item type opens an edit screen with read-only metadata and a `SelectMultiDual` for field exclusions persisted to `excludeFieldsByItemType`.

The DW10 ItemType API is well-documented in source. `ItemManager.Metadata.GetMetadata().Items` returns all item types as `ItemMetadataCollection` (a `Collection<ItemType>`). Each `ItemType` has a `Category` property (`ItemTypeCategory`) with `FullName` (slash-separated path) and `Segments` (string array). Fields are available via `ItemManager.Metadata.GetItemFields(ItemType)` which returns `FieldMetadataCollection` (a `Collection<ItemField>`) including inherited fields. The Phase 34 Embedded XML pattern provides a near-identical template -- the main difference is live API discovery instead of scan-based discovery, and category-based tree nesting instead of flat child nodes.

**Primary recommendation:** Mirror the Phase 34 XmlType pattern exactly (list screen, edit screen, query, command, model, node path provider) substituting ItemType API calls for SQL-based XML discovery. Add category-based tree nesting in `SerializerSettingsNodeProvider`.

<user_constraints>

## User Constraints (from CONTEXT.md)

### Locked Decisions
- **D-01:** Discover item types via DW ItemType API (`ItemType.GetAllItemTypes()` or equivalent service call). Not SQL.
- **D-02:** Live discovery on every screen load -- no scan/persist pattern. ItemType API is fast and always reflects current system state. No scan button needed.
- **D-03:** Discover fields per item type via DW ItemType field API (the `Fields` collection on the `ItemType` object). Not SQL.
- **D-04:** Flat list screen + expandable tree children organized by category path. Item type categories use `/` as separator (e.g., `Swift-v2/Utilities`). Tree renders as nested folders.
- **D-05:** Item types with no category or empty category are grouped under an "Uncategorized" folder in the tree.
- **D-06:** Edit screen shows type name, category, and total field count as read-only info plus `SelectMultiDual` for field exclusions.
- **D-07:** `SelectMultiDual` is the standard control (per Phase 34 D-01/D-03).

### Claude's Discretion
- Exact DW API calls for ItemType discovery and field enumeration
- How to extract category from ItemType (property name, parsing logic)
- How tree node IDs are structured for nested category folders
- Whether to show field data types alongside field names in the SelectMultiDual options

### Deferred Ideas (OUT OF SCOPE)
None

</user_constraints>

<phase_requirements>

## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| ITEM-01 | "Item Types" tree node under Serialize lists all item types discovered in the system | DW API: `ItemManager.Metadata.GetMetadata().Items` returns all types; tree nesting via `ItemTypeCategory.Segments` |
| ITEM-02 | Item type edit screen shows all fields for that item type as a SelectMultiDual where user selects fields to exclude | `ItemManager.Metadata.GetItemFields(itemType)` returns `FieldMetadataCollection` with `SystemName` and `Name` per field |
| ITEM-03 | Item type field exclusions are persisted to config JSON under `excludeFieldsByItemType` and applied during serialize/deserialize | `SerializerConfiguration.ExcludeFieldsByItemType` already exists; `ExclusionMerger.MergeFieldExclusions()` already handles merge |

</phase_requirements>

## Architecture Patterns

### DW10 ItemType API Surface [VERIFIED: DW10 source code]

**Getting all item types:**
```csharp
// ItemManager.cs line 533 (private but called by public methods)
// The public access path:
var metadata = ItemManager.Metadata.GetMetadata();  // returns MetadataContainer
var allItemTypes = metadata.Items;  // ItemMetadataCollection : Collection<ItemType>
```

Note: `GetAllItemTypes()` on `ItemManager` is `private static`. The public equivalent is `ItemManager.Metadata.GetMetadata().Items`. However, `ItemManager.GetByCategoryId(categoryName)` and `ItemManager.GetCategoriesByParentId(parentCategoryFullName)` are public and can be used for category-based listing. [VERIFIED: DW10 source ItemManager.cs lines 441-535]

**ItemType key properties:**
- `SystemName` (string) -- unique identifier, used as config key [VERIFIED: ItemType.cs line 40]
- `Name` (string) -- user-friendly display name [VERIFIED: ItemType.cs line 28]
- `Category` (ItemTypeCategory) -- has `FullName`, `Name`, `Segments` [VERIFIED: ItemType.cs line 108]
- `Fields` (FieldMetadataCollection) -- direct fields on this type [VERIFIED: ItemType.cs line 58]
- `Base` (string) -- system name of parent type for inheritance [VERIFIED: ItemType.cs line 22]

**ItemTypeCategory key properties:**
- `FullName` (string) -- slash-separated path e.g. "Swift-v2/Utilities" [VERIFIED: ItemTypeCategory.cs line 29]
- `Name` (string) -- last segment only e.g. "Utilities" [VERIFIED: ItemTypeCategory.cs line 53]
- `Segments` (string[]) -- split on "/" [VERIFIED: ItemTypeCategory.cs line 85]
- `Separator` -- const string "/" [VERIFIED: ItemTypeCategory.cs line 17]

**Getting all fields (including inherited):**
```csharp
// MetadataManager.cs line 275 -- includes inherited fields from Base types
var allFields = ItemManager.Metadata.GetItemFields(itemType);
// Returns FieldMetadataCollection : Collection<ItemField>
```

**ItemField key properties:**
- `SystemName` (string) -- field identifier [VERIFIED: ItemField.cs line 41]
- `Name` (string) -- user-friendly name [VERIFIED: ItemField.cs line 29]
- `UnderlyingType` (Type) -- data type, defaults to typeof(string) [VERIFIED: ItemField.cs line 77]
- `Parent` (string) -- system name of parent item type [VERIFIED: ItemField.cs line 23]

### Recommended File Structure
```
src/DynamicWeb.Serializer/AdminUI/
  Models/
    ItemTypeListModel.cs          # DataViewModelBase with SystemName, Name, Category, FieldCount
    ItemTypeEditModel.cs          # DataViewModelBase + IIdentifiable with SystemName, ExcludedFields
  Queries/
    ItemTypeListQuery.cs          # Live API: GetMetadata().Items
    ItemTypeBySystemNameQuery.cs  # Live API: GetItemType(systemName) + config lookup
  Commands/
    SaveItemTypeCommand.cs        # Persist to ExcludeFieldsByItemType
  Screens/
    ItemTypeListScreen.cs         # ListScreenBase<ItemTypeListModel>
    ItemTypeEditScreen.cs         # EditScreenBase<ItemTypeEditModel> with SelectMultiDual
  Tree/
    ItemTypeNavigationNodePathProvider.cs  # Breadcrumb for list model
    (SerializerSettingsNodeProvider.cs)    # Modified: add ItemTypes node with category children
```

### Pattern: Live Discovery List Query (no scan)
Unlike the Embedded XML pattern which scans and persists types to config, item types are live-discovered every time. The list query calls the DW API directly rather than reading from config:

```csharp
// Source: Phase 35 pattern (adapted from XmlTypeListQuery)
public sealed class ItemTypeListQuery : DataQueryModelBase<DataListViewModel<ItemTypeListModel>>
{
    public override DataListViewModel<ItemTypeListModel>? GetModel()
    {
        var metadata = ItemManager.Metadata.GetMetadata();
        var allTypes = metadata?.Items ?? Enumerable.Empty<ItemType>();
        
        var configPath = ConfigPathResolver.FindConfigFile();
        var config = configPath != null ? ConfigLoader.Load(configPath) : null;
        
        var items = allTypes
            .OrderBy(t => t.Category?.FullName ?? "", StringComparer.OrdinalIgnoreCase)
            .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .Select(t => new ItemTypeListModel
            {
                SystemName = t.SystemName,
                DisplayName = t.Name,
                Category = t.Category?.FullName ?? "",
                FieldCount = t.Fields?.Count ?? 0,
                ExcludedFieldCount = GetExcludedCount(config, t.SystemName)
            });
        
        return new DataListViewModel<ItemTypeListModel>
        {
            Data = items,
            TotalCount = allTypes.Count()
        };
    }
}
```

### Pattern: Category-Based Tree Nesting
The tree must render nested folders from category paths. Each category segment becomes a tree node level:

```csharp
// In SerializerSettingsNodeProvider.GetSubNodes
// For ItemTypesNodeId: build category folder nodes + leaf item type nodes

// Strategy: 
// 1. Get all item types
// 2. Extract distinct category prefixes at the requested depth
// 3. For category folder nodes: HasSubNodes = true, NodeAction navigates to list (filtered?)
// 4. For leaf item types at this category: HasSubNodes = false, NodeAction navigates to edit screen
```

**Tree node ID structure for categories:**
```
Serializer_ItemTypes                          // root "Item Types" node
Serializer_ItemType_Cat_Swift-v2              // category folder
Serializer_ItemType_Cat_Swift-v2/Utilities    // nested category folder
Serializer_ItemType_Uncategorized             // "Uncategorized" folder
Serializer_ItemType_Swift-v2-CartApps         // leaf item type node
```

The approach: Use the `parentNodePath.Last` to determine which level we are at. For `ItemTypesNodeId`, emit top-level categories and uncategorized items. For `Serializer_ItemType_Cat_*`, extract the category path from the ID and emit sub-categories and item types at that level. [ASSUMED]

### Pattern: Edit Screen with Live Field Discovery
```csharp
// In ItemTypeEditScreen, CreateFieldSelector() method:
// 1. Get item type by system name
// 2. Get all fields (including inherited) via ItemManager.Metadata.GetItemFields(itemType)
// 3. Populate SelectMultiDual.Options with field SystemNames
// 4. Show field Name as label, SystemName as value
// 5. Pre-select currently excluded fields from config
```

### Anti-Patterns to Avoid
- **Do NOT use SQL to discover item types** -- D-01 explicitly requires DW ItemType API
- **Do NOT persist discovered types to config** -- D-02 says live discovery, no scan/persist
- **Do NOT use ItemType.Fields directly for all fields** -- use `ItemManager.Metadata.GetItemFields(itemType)` which includes inherited fields from Base types
- **Do NOT call GetAllItemTypes() directly** -- it is private; use `ItemManager.Metadata.GetMetadata().Items`

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Item type enumeration | SQL query against ItemType_ tables | `ItemManager.Metadata.GetMetadata().Items` | DW API handles activation workflows, caching, inheritance |
| Field enumeration (with inheritance) | Walk Base chain manually | `ItemManager.Metadata.GetItemFields(itemType)` | Already handles recursive Base resolution |
| Category path parsing | Custom string splitting | `ItemTypeCategory.Segments` property | Already provided, handles edge cases |
| Config persistence | Custom JSON manipulation | `ConfigWriter.Save()` with `config with { ExcludeFieldsByItemType = updated }` | Atomic save, consistent format |
| Exclusion merging | Custom union logic | `ExclusionMerger.MergeFieldExclusions()` | Already handles flat + typed union with case-insensitive lookup |

## Common Pitfalls

### Pitfall 1: Private GetAllItemTypes()
**What goes wrong:** Attempting to call `ItemManager.GetAllItemTypes()` which is `private static`.
**Why it happens:** CONTEXT.md references "GetAllItemTypes()" but this method is private in the source.
**How to avoid:** Use `ItemManager.Metadata.GetMetadata().Items` instead.
**Warning signs:** Compilation error about accessibility.

### Pitfall 2: ItemType.Fields vs GetItemFields()
**What goes wrong:** Using `itemType.Fields` directly, which only returns fields declared on that specific type, not inherited ones.
**Why it happens:** The `Fields` property exists and seems sufficient.
**How to avoid:** Always use `ItemManager.Metadata.GetItemFields(itemType)` which recursively includes inherited fields from the `Base` type chain.
**Warning signs:** Missing fields for item types that inherit from a base type.

### Pitfall 3: Empty/null Category Handling
**What goes wrong:** NullReferenceException or missing items when category is null or has empty FullName.
**Why it happens:** `Category` property defaults to `new ItemTypeCategory()` which has empty FullName, but could also be null in some cases.
**How to avoid:** Always null-check: `itemType.Category?.FullName`. Group empty/null under "Uncategorized" folder per D-05.
**Warning signs:** Items disappearing from tree, NRE in tree rendering.

### Pitfall 4: Case Sensitivity in SystemName Keys
**What goes wrong:** Config key "Swift-v2-CartApps" doesn't match runtime lookup "swift-v2-cartapps".
**Why it happens:** DW may return different casing than what was stored in config.
**How to avoid:** Use `StringComparer.OrdinalIgnoreCase` when building dictionaries, consistent with `ExclusionMerger` pattern.
**Warning signs:** Exclusions configured but not applied at runtime.

### Pitfall 5: Tree Node ID Collisions
**What goes wrong:** Category folder node IDs clash with item type node IDs.
**Why it happens:** A category name could theoretically match a system name.
**How to avoid:** Use distinct prefixes: `Serializer_ItemType_Cat_` for category folders, `Serializer_ItemType_` for leaf items.
**Warning signs:** Wrong screen opens when clicking tree node.

## Code Examples

### Model Classes
```csharp
// Source: Adapted from XmlTypeListModel.cs / XmlTypeEditModel.cs
public sealed class ItemTypeListModel : DataViewModelBase
{
    [ConfigurableProperty("Item Type")]
    public string SystemName { get; set; } = string.Empty;

    [ConfigurableProperty("Name")]
    public string DisplayName { get; set; } = string.Empty;

    [ConfigurableProperty("Category")]
    public string Category { get; set; } = string.Empty;

    [ConfigurableProperty("Fields")]
    public int FieldCount { get; set; }

    [ConfigurableProperty("Excluded")]
    public int ExcludedFieldCount { get; set; }
}

public sealed class ItemTypeEditModel : DataViewModelBase, IIdentifiable
{
    public string SystemName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int FieldCount { get; set; }

    public string GetId() => SystemName;

    [ConfigurableProperty("Excluded Fields", explanation: "Select fields to exclude from serialization for this item type")]
    public string ExcludedFields { get; set; } = string.Empty;
}
```

### Save Command
```csharp
// Source: Adapted from SaveXmlTypeCommand.cs
public sealed class SaveItemTypeCommand : CommandBase<ItemTypeEditModel>
{
    public string? ConfigPath { get; set; }

    public override CommandResult Handle()
    {
        if (Model is null)
            return new() { Status = CommandResult.ResultType.Invalid, Message = "Model data must be given" };

        if (string.IsNullOrWhiteSpace(Model.SystemName))
            return new() { Status = CommandResult.ResultType.Invalid, Message = "Item type system name is required" };

        var configPath = ConfigPath ?? ConfigPathResolver.FindOrCreateConfigFile();
        var config = ConfigLoader.Load(configPath);

        var excludedFields = (Model.ExcludedFields ?? string.Empty)
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(f => f.Trim())
            .Where(f => f.Length > 0)
            .ToList();

        var updated = new Dictionary<string, List<string>>(config.ExcludeFieldsByItemType, StringComparer.OrdinalIgnoreCase);
        updated[Model.SystemName] = excludedFields;

        var newConfig = config with { ExcludeFieldsByItemType = updated };
        ConfigWriter.Save(newConfig, configPath);

        return new() { Status = CommandResult.ResultType.Ok, Model = Model };
    }
}
```

### SelectMultiDual for Fields
```csharp
// Source: Adapted from XmlTypeEditScreen.CreateElementSelector()
private SelectMultiDual CreateFieldSelector()
{
    var editor = new SelectMultiDual
    {
        Label = "Exclude Fields",
        Explanation = "Select fields to exclude from serialization for this item type.",
        SortOrder = OrderBy.Default
    };

    if (string.IsNullOrWhiteSpace(Model?.SystemName))
        return editor;

    var itemType = ItemManager.Metadata.GetItemType(Model.SystemName);
    if (itemType == null)
    {
        editor.Explanation = "Item type not found in the system.";
        return editor;
    }

    // GetItemFields includes inherited fields
    var allFields = ItemManager.Metadata.GetItemFields(itemType);
    
    editor.Options = allFields
        .Where(f => !string.IsNullOrEmpty(f.SystemName))
        .OrderBy(f => f.SystemName, StringComparer.OrdinalIgnoreCase)
        .Select(f => new ListOption 
        { 
            Value = f.SystemName, 
            Label = $"{f.Name} ({f.SystemName})"  // Show both friendly name and system name
        })
        .ToList();

    var selected = (Model.ExcludedFields ?? string.Empty)
        .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
        .Select(v => v.Trim())
        .Where(v => v.Length > 0)
        .ToArray();

    if (selected.Length > 0)
        editor.Value = selected;

    return editor;
}
```

### Tree Node Category Nesting
```csharp
// In SerializerSettingsNodeProvider.GetSubNodes()
// When parentNodePath.Last == ItemTypesNodeId:
//   1. Get all item types
//   2. Build distinct top-level categories
//   3. Emit category folder nodes + uncategorized folder
// When parentNodePath.Last starts with "Serializer_ItemType_Cat_":
//   1. Extract category path from node ID
//   2. Find sub-categories and item types at that level
//   3. Emit sub-category folders and item type leaf nodes
```

## Discretion Recommendations

### Field Display in SelectMultiDual
**Recommendation:** Show `Name (SystemName)` as the label -- e.g., "Title (PageTitle)". This gives the user both the friendly name they see in the DW admin and the technical system name used in config. The `Value` should be just `SystemName` since that is what gets persisted. [ASSUMED]

### Category Tree Node IDs
**Recommendation:** Use prefix-based IDs:
- `Serializer_ItemType_Cat_{categoryFullName}` for category folders
- `Serializer_ItemType_{systemName}` for leaf item type nodes
- `Serializer_ItemType_Uncategorized` for the uncategorized folder

This avoids collisions and allows pattern matching in `GetSubNodes`. [ASSUMED]

### GetItemType for Edit Query
**Recommendation:** Use the public `ItemManager.Metadata.GetItemType(systemName)` which returns a single item type by system name. This is cleaner than iterating all types. For the list query, use `GetMetadata().Items` to get all. [VERIFIED: MetadataManager.cs line 215]

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit (existing) |
| Config file | tests/DynamicWeb.Serializer.Tests/DynamicWeb.Serializer.Tests.csproj |
| Quick run command | `dotnet test tests/DynamicWeb.Serializer.Tests --filter "FullyQualifiedName~ItemType" --no-build` |
| Full suite command | `dotnet test tests/DynamicWeb.Serializer.Tests --no-build` |

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| ITEM-01 | Item types listed from DW API | integration (needs DW runtime) | manual DW admin verification | N/A -- UI list backed by live API |
| ITEM-02 | Edit screen shows fields as SelectMultiDual | integration (needs DW runtime) | manual DW admin verification | N/A -- UI screen with live API |
| ITEM-03 | Field exclusions saved to config | unit | `dotnet test --filter "FullyQualifiedName~ItemTypeCommand"` | Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test tests/DynamicWeb.Serializer.Tests --filter "FullyQualifiedName~ItemType" --no-build`
- **Per wave merge:** `dotnet test tests/DynamicWeb.Serializer.Tests --no-build`
- **Phase gate:** Full suite green before `/gsd-verify-work`

### Wave 0 Gaps
- [ ] `tests/DynamicWeb.Serializer.Tests/AdminUI/ItemTypeCommandTests.cs` -- covers ITEM-03 (SaveItemTypeCommand save/update/validation)

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Category tree nesting approach using prefix-based node IDs in GetSubNodes | Architecture Patterns / Discretion | Tree structure may not render correctly if DW navigation framework handles node IDs differently |
| A2 | Show "Name (SystemName)" format in SelectMultiDual labels | Discretion | Minor UX impact only |
| A3 | GetMetadata().Items is called from admin UI without needing Initialize() first | Architecture Patterns | Items could be empty if metadata not initialized; DW likely auto-initializes in admin context |

## Open Questions (RESOLVED)

1. **Does GetMetadata().Items require ItemManager.Initialize() first in admin context?**
   - What we know: `Initialize()` is called during DW startup; admin UI typically runs after initialization.
   - What's unclear: Whether there's a race condition on first load.
   - RESOLVED: Assume DW initializes automatically in admin context. Add a null/empty check and return empty list gracefully. Plans include graceful null handling.

2. **Should item types with no fields be shown in the list?**
   - What we know: `IsEmpty` property exists on ItemType (true when no fields).
   - What's unclear: Whether showing empty types adds noise.
   - RESOLVED: Show all types — user can see they have no fields and skip them. Plans show all types regardless.

## Sources

### Primary (HIGH confidence)
- DW10 source: `src/Features/Content/Dynamicweb/Content/Items/Metadata/ItemType.cs` -- ItemType class, properties, Category
- DW10 source: `src/Features/Content/Dynamicweb/Content/Items/Metadata/ItemField.cs` -- ItemField class, properties
- DW10 source: `src/Features/Content/Dynamicweb/Content/Items/Metadata/ItemTypeCategory.cs` -- Category with FullName, Segments, Separator
- DW10 source: `src/Features/Content/Dynamicweb/Content/Items/Metadata/MetadataManager.cs` -- GetMetadata(), GetItemType(), GetItemFields()
- DW10 source: `src/Features/Content/Dynamicweb/Content/Items/ItemManager.cs` -- GetByCategoryId(), GetCategoriesByParentId(), private GetAllItemTypes()
- Project source: `src/DynamicWeb.Serializer/AdminUI/Screens/XmlTypeEditScreen.cs` -- Phase 34 edit screen pattern
- Project source: `src/DynamicWeb.Serializer/AdminUI/Tree/SerializerSettingsNodeProvider.cs` -- Tree node provider pattern
- Project source: `src/DynamicWeb.Serializer/Configuration/ExclusionMerger.cs` -- Existing merge logic
- Project source: `src/DynamicWeb.Serializer/Configuration/SerializerConfiguration.cs` -- ExcludeFieldsByItemType already exists

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- all DW APIs verified in source
- Architecture: HIGH -- Phase 34 pattern is a direct template; DW API surface fully examined
- Pitfalls: HIGH -- identified from source code analysis (private methods, inheritance chains, null categories)

**Research date:** 2026-04-14
**Valid until:** 2026-05-14 (stable DW10 API, no expected changes)
