# Technology Stack

**Project:** DynamicWeb.Serializer v0.6.0 - UI Configuration Improvements
**Researched:** 2026-04-07
**Focus:** DW CoreUI APIs for tab injection, screen injection, item type field enumeration, SQL schema introspection, embedded XML type discovery

## Core APIs Needed

### 1. Tab Injection on Page/Item Edit Screens

**Pattern:** `EditScreenInjector<TScreen, TModel>` with `OnBuildEditScreen`
**Confidence:** HIGH (verified in DW10 source)

The `EditScreenInjector<TScreen, TModel>` base class provides an `OnBuildEditScreen(EditScreenBuilder builder)` method that is called during screen construction. The `EditScreenBuilder` exposes `AddComponent`, `AddComponents`, and `AddDynamicFields` methods that add tabs/groups to the screen.

| Class | Namespace | Purpose |
|-------|-----------|---------|
| `EditScreenInjector<TScreen, TModel>` | `Dynamicweb.CoreUI.Screens` | Base class for injecting into edit screens |
| `EditScreenBuilder` | `EditScreenBase<TModel>.EditScreenBuilder` (nested class) | Builder passed to `OnBuildEditScreen` |
| `PageEditScreen` | `Dynamicweb.Content.UI.Screens` | The page edit screen to inject into |
| `PageDataModel` | `Dynamicweb.Content.UI.Models` | Data model for page edit |
| `ItemTypeEditScreen` | `Dynamicweb.Content.UI.Screens.Settings.ItemTypes` | The item type settings edit screen |
| `ItemTypeDataModel` | `Dynamicweb.Content.UI.Models.Settings.ItemTypes` | Data model for item type |

**How tabs are created:** `builder.AddComponents("TabName", "GroupHeading", components)` creates a tab named "TabName" with a group "GroupHeading". The first argument to `AddComponents` is the tab name. If the tab already exists, the group is added to it.

**Existing DW examples verified in source:**
- `AreaEditScreenInjector` adds "Ecommerce" tab with "Ecommerce settings" group to AreaEditScreen
- `PageEditScreenInjector` adds "Ecommerce" tab with "Ecommerce navigation" group to PageEditScreen
- Both use `builder.AddComponents(tabName, groupName, editorArray)` pattern

**For "Serialization" tab on Item Type Edit screen:**
```csharp
public sealed class SerializerItemTypeEditInjector : EditScreenInjector<ItemTypeEditScreen, ItemTypeDataModel>
{
    public override void OnBuildEditScreen(EditScreenBase<ItemTypeDataModel>.EditScreenBuilder builder)
    {
        // This creates a "Serialization" tab on the Item Type Edit screen
        builder.AddComponents("Serialization", "Field exclusions", new[]
        {
            // checkbox list of fields to exclude from serialization
        });
    }
}
```

**Discovery:** Injectors are auto-discovered by `AddInManager.GetInstances<ScreenInjector<T>>()` in `ScreenInjectorHandler`. No registration needed.

### 2. Screen Injection on Area Edit Screen

**Pattern:** Same `EditScreenInjector<AreaEditScreen, AreaDataModel>`
**Confidence:** HIGH (verified - AreaEditScreenInjector does exactly this)

The existing `AreaEditScreenInjector` in `Dynamicweb.Global.UI.Content` adds an "Ecommerce" tab to the Area Edit screen. Our injector follows the identical pattern.

| Class | Namespace | Purpose |
|-------|-----------|---------|
| `AreaEditScreen` | `Dynamicweb.Content.UI.Screens` | Area edit screen |
| `AreaDataModel` | `Dynamicweb.Content.UI.Models` | Area data model |

```csharp
public sealed class SerializerAreaEditInjector : EditScreenInjector<AreaEditScreen, AreaDataModel>
{
    public override void OnBuildEditScreen(EditScreenBase<AreaDataModel>.EditScreenBuilder builder)
    {
        builder.AddComponents("Serialization", "Column exclusions", new[]
        {
            // Read-only display of area serialization exclusion settings
        });
    }
}
```

### 3. Item Type Field Enumeration

**Pattern:** `MetadataManager.Current.GetItemType(systemName)` then `ItemType.Fields` or `ItemType.GetAllFields()`
**Confidence:** HIGH (verified in DW10 source)

| Class | Namespace | Purpose |
|-------|-----------|---------|
| `MetadataManager` | `Dynamicweb.Content.Items.Metadata` | Singleton for item type metadata |
| `ItemType` | `Dynamicweb.Content.Items.Metadata` | Item type definition with fields |
| `ItemField` | `Dynamicweb.Content.Items.Metadata` | Individual field metadata |
| `FieldMetadataCollection` | `Dynamicweb.Content.Items.Metadata` | Collection of ItemField |
| `MetadataContainer` | `Dynamicweb.Content.Items.Metadata` | Container for all item types |

**How to enumerate all item types:**
```csharp
var container = MetadataManager.Current.GetMetadata();
// container.Items is ItemMetadataCollection containing all ItemType objects
foreach (var itemType in container.Items)
{
    // itemType.SystemName, itemType.Name, itemType.Fields
}
```

**How to get fields for a specific item type:**
```csharp
var itemType = MetadataManager.Current.GetItemType("MyItemType");
if (itemType != null)
{
    // Direct fields only
    var fields = itemType.Fields; // FieldMetadataCollection
    
    // Including inherited fields from base types
    var allFields = itemType.GetAllFields(); // calls ItemManager.Metadata.GetItemFields(this)
    // Or equivalently:
    var allFields2 = MetadataManager.Current.GetItemFields(itemType); // includes inherited
    
    foreach (var field in allFields)
    {
        // field.SystemName - e.g., "Title", "Description"
        // field.Name - display name
        // field.Editor - editor metadata
        // field.UnderlyingType - CLR type
    }
}
```

**Key `ItemField` properties for building checkbox UI:**
- `SystemName` (string) - unique identifier within item type
- `Name` (string) - user-friendly display name
- `Description` (string) - help text
- `Editor` (EditorMetadata) - editor type info
- `UnderlyingType` (Type) - CLR type of value

### 4. SQL Table Column Schema Introspection

**Pattern:** `DatabaseSchema` and internal `SqlSchemaHelper`
**Confidence:** HIGH (verified in DW10 source)

| Class | Namespace | Purpose |
|-------|-----------|---------|
| `DatabaseSchema` | `Dynamicweb.Data` | Public API for DB schema queries |
| `SqlSchemaHelper` | `Dynamicweb.Data.DataProviders` | Internal helper (not accessible) |
| `Database` | `Dynamicweb.Data` | Connection factory |

**`DatabaseSchema` is the public API.** It provides:
- `GetTables()` - returns DataTable of all table names
- `GetTableColumns(IDbConnection, tableName)` - returns DataTable with column schema (ColumnName, DataType, IsKey, IsIdentity, etc.)

**How to get columns for a table:**
```csharp
using var connection = Database.CreateConnection();
var schema = new DatabaseSchema();
var schemaTable = schema.GetTableColumns(connection, "EcomShops");
foreach (DataRow row in schemaTable.Rows)
{
    var columnName = (string)row["ColumnName"];
    var dataType = (Type)row["DataType"];
    var isKey = (bool)row["IsKey"];
    // Use for building column picker UI
}
```

**Note:** `SqlSchemaHelper` is `internal` to `Dynamicweb.Core`. We must use the public `DatabaseSchema` class directly and write our own column-name extraction from the DataTable result. This is straightforward but worth noting.

### 5. Module/Content Module Type Discovery (moduleSystemName)

**Pattern:** `ExtensibilityTypeSelect` with `BaseType = typeof(ContentModuleBaseAddIn)`
**Confidence:** HIGH (verified in ParagraphEditScreen source)

| Class | Namespace | Purpose |
|-------|-----------|---------|
| `ExtensibilityTypeSelect` | `Dynamicweb.CoreUI.Editors.Lists` | Select editor that auto-populates from AddIn types |
| `ContentModuleBaseAddIn` | `Dynamicweb.Application.UI.ContentModules` | Base type for content modules |
| `AddInManager` | `Dynamicweb.Extensibility.AddIns` | AddIn discovery/management |

**How ParagraphEditScreen discovers content modules:**
```csharp
private static ExtensibilityTypeSelect CreateContentModuleTypeEditor() => new()
{
    BaseType = typeof(ContentModuleBaseAddIn),
    ReloadOnChange = true,
    ShowNothingSelectedOption = true,
};
```

**To enumerate programmatically (for building auto-discovery lists):**
```csharp
var types = AddInManager.GetTypes(typeof(ContentModuleBaseAddIn));
foreach (var type in types)
{
    if (!AddInManager.GetAddInActive(type)) continue;
    if (AddInManager.GetAddInIgnore(type)) continue;
    if (AddInManager.GetAddInDeprecated(type)) continue;
    
    var label = AddInManager.GetAddInName(type);
    var typeName = type.GetTypeNameWithAssembly();
}
```

### 6. URL Data Provider Type Discovery (urlDataProviderTypeName)

**Pattern:** `ExtensibilityTypeSelect` with `BaseType = typeof(UrlDataProvider)`
**Confidence:** HIGH (verified in PageEditScreen source)

| Class | Namespace | Purpose |
|-------|-----------|---------|
| `UrlDataProvider` | `Dynamicweb.Frontend.UrlHandling` | Abstract base for URL data providers |

**How PageEditScreen discovers URL data providers:**
```csharp
private static ExtensibilityTypeSelect CreateUrlDataProviderTypeEditor() => new()
{
    ReloadOnChange = true,
    ShowNothingSelectedOption = true,
    BaseType = typeof(UrlDataProvider)
};
```

## Recommended Stack Additions

### Core Framework (no new NuGet packages needed)
| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| `Dynamicweb.CoreUI` | (existing) | EditScreenInjector, EditScreenBuilder | Already a dependency; all tab/screen injection APIs live here |
| `Dynamicweb.Content.UI` | (existing) | PageEditScreen, AreaEditScreen, ItemTypeEditScreen, models | Already a dependency for existing injectors |
| `Dynamicweb.Data` | (existing) | DatabaseSchema for SQL column introspection | Already a transitive dependency via Dynamicweb.Core |
| `Dynamicweb.Content` | (existing) | MetadataManager, ItemType, ItemField for field enumeration | Already a transitive dependency |
| `Dynamicweb.Application.UI` | (existing) | ContentModuleBaseAddIn for module discovery | Already a transitive dependency |

### No New Dependencies
Everything needed for v0.6.0 is available through existing DW10 APIs. No additional NuGet packages are required.

## Key Integration Points

### Existing Injector Pattern (already validated)
The project already has two working injectors:
1. `SerializerPageEditInjector` - extends `EditScreenInjector<PageEditScreen, PageDataModel>`, uses `GetScreenActions()` to add action menu items
2. `SerializerFileOverviewInjector` - extends `ScreenInjector<FileOverviewScreen>`, uses `OnAfter()` to modify screen layout

### New Injectors Needed
| Injector | Base Class | Target Screen | Method |
|----------|-----------|---------------|--------|
| `SerializerItemTypeEditInjector` | `EditScreenInjector<ItemTypeEditScreen, ItemTypeDataModel>` | Item type settings | `OnBuildEditScreen` (adds "Serialization" tab) |
| `SerializerAreaEditInjector` | `EditScreenInjector<AreaEditScreen, AreaDataModel>` | Area edit | `OnBuildEditScreen` (adds "Serialization" tab) |

### Data Flow for Field Exclusion UI
1. Injector's `OnBuildEditScreen` is called with `EditScreenBuilder`
2. Use `Screen.Model.SystemName` (for ItemTypeEditScreen) to get the item type system name
3. Call `MetadataManager.Current.GetItemType(systemName)` to get the ItemType
4. Iterate `itemType.GetAllFields()` to build checkbox list
5. Load current exclusion config from `ConfigLoader` to set checkbox states
6. On save, update config file via existing `ConfigLoader` patterns

### Data Flow for SQL Column Picker
1. Use `DatabaseSchema.GetTableColumns()` to get column list
2. Build multi-select or checkbox list from column names
3. Load current exclusion config from `ConfigLoader`
4. On save, update config file

### Data Flow for Module/Provider Discovery
1. Use `AddInManager.GetTypes(typeof(ContentModuleBaseAddIn))` for module system names
2. Use `AddInManager.GetTypes(typeof(UrlDataProvider))` for URL data provider types
3. Both can be rendered via `ExtensibilityTypeSelect` or manual Select population

## Alternatives Considered

| Category | Recommended | Alternative | Why Not |
|----------|-------------|-------------|---------|
| Tab injection | `EditScreenInjector.OnBuildEditScreen` | Custom standalone screen | Tab injection provides native UX integrated into existing screens |
| SQL schema | `DatabaseSchema.GetTableColumns` | Direct SQL INFORMATION_SCHEMA query | DatabaseSchema is the official DW API; using raw SQL bypasses abstraction |
| Field enumeration | `MetadataManager.Current.GetItemType` | Direct file parsing of /Files/System/Items/ | MetadataManager handles file+DB sources and caching; direct parsing is fragile |
| Module discovery | `AddInManager.GetTypes(baseType)` | Hardcoded module list | AddInManager auto-discovers all installed modules; hardcoding breaks on new installations |

## Important Notes

### SqlSchemaHelper is Internal
The convenient `SqlSchemaHelper.GetColumns(tableName)` method is `internal` to `Dynamicweb.Core`. We must use the public `DatabaseSchema` class directly and write our own column-name extraction from the DataTable result. This is straightforward but worth noting.

### EditScreenBuilder is Nested
The `EditScreenBuilder` is a nested class within `EditScreenBase<TModel>`. The method signature for `OnBuildEditScreen` is:
```csharp
public virtual void OnBuildEditScreen(EditScreenBase<TModel>.EditScreenBuilder builder)
```
This is important for correct type references.

### Injector Auto-Discovery
All injectors are discovered by `AddInManager.GetInstances<ScreenInjector<T>>()` during screen construction. No manual registration is needed -- just implement the correct base class and the DW AddIn system handles discovery.

### Read-Only vs Editable Injected Content
The `EditScreenBuilder` provides `EditorFor` (for model-bound editors) and `AddComponents` (for arbitrary UI components). Since our config lives in a separate JSON file (not the screen's model), we will likely use custom editors or read-only display components rather than model-bound editors. The `GetEditor` override on the injector can provide custom editors for specific property names.

### Config Persistence Challenge
The injected UI reads/writes to the serializer config file (JSON), not to DW's data model. This means:
- Read: `ConfigLoader.Load()` at screen construction time
- Write: Need a save mechanism -- either a custom command or hooking into the screen's save flow via the injector pattern
- The `GetScreenActions()` method on EditScreenInjector can add action buttons (like "Save serialization config") to the screen

### ItemTypeEditScreen Tab Names
The ItemTypeEditScreen already has "Settings" and "Restrictions" tabs. Our "Serialization" tab will appear after these, which is the desired position.

## Sources

- DW10 source: `Dynamicweb.CoreUI\Screens\EditScreenInjector.cs`
- DW10 source: `Dynamicweb.CoreUI\Screens\EditScreenBase.cs` (EditScreenBuilder nested class, AddComponents method)
- DW10 source: `Dynamicweb.CoreUI\Screens\ScreenInjector.cs`
- DW10 source: `Dynamicweb.CoreUI\Screens\ScreenInjectorHandler.cs` (auto-discovery via AddInManager)
- DW10 source: `Dynamicweb.Global.UI\Content\AreaEditScreenInjector.cs` (reference implementation)
- DW10 source: `Dynamicweb.Global.UI\Content\PageEditScreenInjector.cs` (reference implementation)
- DW10 source: `Dynamicweb.Content.UI\Screens\AreaEditScreen.cs`
- DW10 source: `Dynamicweb.Content.UI\Screens\PageEditScreen.cs`
- DW10 source: `Dynamicweb.Content.UI\Screens\Settings\ItemTypes\ItemTypeEditScreen.cs`
- DW10 source: `Dynamicweb.Content.Items.Metadata\MetadataManager.cs`
- DW10 source: `Dynamicweb.Content.Items.Metadata\ItemType.cs`
- DW10 source: `Dynamicweb.Content.Items.Metadata\ItemField.cs`
- DW10 source: `Dynamicweb.Data\DatabaseSchema.cs`
- DW10 source: `Dynamicweb.Data.DataProviders\SqlSchemaHelper.cs`
- DW10 source: `Dynamicweb.CoreUI.Editors.Lists\ExtensibilityTypeSelect.cs`
- DW10 source: `Dynamicweb.Content.UI\Screens\ParagraphEditScreen.cs` (module discovery)
- DW10 source: `Dynamicweb.Frontend.UrlHandling\UrlDataProvider.cs`
