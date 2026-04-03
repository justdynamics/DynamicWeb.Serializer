# Architecture: Full Page Fidelity Extension

**Domain:** DynamicWeb content serialization -- extending existing pipeline for ~30 missing page properties
**Researched:** 2026-04-02
**Overall confidence:** HIGH (verified via DW 10.23.9 DLL decompilation)

## Executive Summary

The existing ContentMapper/ContentDeserializer/SerializedPage pipeline is well-structured and straightforward to extend. All ~30 missing page properties are simple scalar properties on the DW `Page` class that SavePage already persists. PageNavigationSettings is NOT a separate table -- it is columns on the Page table, saved inline by `SavePage` when `page.NavigationSettings != null`. Area ItemType connections use the existing `Area.ItemType`/`Area.Item`/`Area.ItemId` properties. Timestamp preservation requires a post-save SQL UPDATE because `new Page()` always sets `Audit = new AuditedEntity(DateTime.Now, userId)`, overwriting timestamps on INSERT.

## Question 1: DTO Structure -- Sub-Objects vs Flat Properties

### Recommendation: Grouped sub-objects for readability, flat assignment for persistence

Add properties to `SerializedPage` using **logical sub-records** to keep the DTO readable while maintaining simple assignment in the mapper/deserializer.

### Current SerializedPage (26 lines)

The existing DTO has 16 properties plus 4 collections. Adding 30+ flat properties would make it unwieldy.

### Recommended DTO Extension Strategy

```csharp
public record SerializedPage
{
    // --- Existing properties (keep as-is) ---
    public required Guid PageUniqueId { get; init; }
    public int? SourcePageId { get; init; }
    public required string Name { get; init; }
    public required string MenuText { get; init; }
    public required string UrlName { get; init; }
    public required int SortOrder { get; init; }
    public bool IsActive { get; init; }
    public string? ItemType { get; init; }
    public string? Layout { get; init; }
    public bool LayoutApplyToSubPages { get; init; }
    public bool IsFolder { get; init; }
    public string? TreeSection { get; init; }

    // --- NEW: Flat scalar properties (simple booleans/strings/ints) ---
    // These map 1:1 to Page properties and are simple enough to stay flat.
    public string? NavigationTag { get; init; }
    public string? ShortCut { get; init; }
    public bool Hidden { get; init; }
    public bool Published { get; init; }
    public bool Allowclick { get; init; } = true;
    public bool Allowsearch { get; init; } = true;
    public bool ShowInSitemap { get; init; } = true;
    public bool ShowInMenu { get; init; } = true;
    public bool ShowInLegend { get; init; } = true;
    public int SslMode { get; init; }
    public string? ColorSchemeId { get; init; }
    public string? ExactUrl { get; init; }
    public string? ContentType { get; init; }
    public string? TopImage { get; init; }
    public int DisplayMode { get; init; }

    // --- NEW: URL settings sub-object ---
    public SerializedUrlSettings? UrlSettings { get; init; }

    // --- NEW: SEO settings sub-object ---
    public SerializedSeoSettings? Seo { get; init; }

    // --- NEW: Visibility settings sub-object ---
    public SerializedVisibilitySettings? Visibility { get; init; }

    // --- NEW: Navigation (ecommerce) settings sub-object ---
    public SerializedNavigationSettings? NavigationSettings { get; init; }

    // --- NEW: Timestamp/audit sub-object ---
    public SerializedAudit? Audit { get; init; }

    // --- NEW: Date range ---
    public DateTime? ActiveFrom { get; init; }
    public DateTime? ActiveTo { get; init; }

    // --- Existing collections (keep as-is) ---
    public Dictionary<string, object> Fields { get; init; } = new();
    public Dictionary<string, object> PropertyFields { get; init; } = new();
    public List<SerializedPermission> Permissions { get; init; } = new();
    public List<SerializedGridRow> GridRows { get; init; } = new();
    public List<SerializedPage> Children { get; init; } = new();
}

public record SerializedUrlSettings
{
    public string? UrlDataProviderTypeName { get; init; }
    public string? UrlDataProviderParameters { get; init; }
    public bool UrlIgnoreForChildren { get; init; }
    public bool UrlUseAsWritten { get; init; }
}

public record SerializedSeoSettings
{
    public string? MetaTitle { get; init; }
    public string? MetaCanonical { get; init; }
    public string? Description { get; init; }
    public string? Keywords { get; init; }
    public bool Noindex { get; init; }
    public bool Nofollow { get; init; }
    public bool Robots404 { get; init; }
}

public record SerializedVisibilitySettings
{
    public bool HideForPhones { get; init; }
    public bool HideForTablets { get; init; }
    public bool HideForDesktops { get; init; }
}

public record SerializedNavigationSettings
{
    public bool UseEcomGroups { get; init; }
    public string? ParentType { get; init; }  // "Groups" or "Shop"
    public string? Groups { get; init; }
    public string? ShopID { get; init; }
    public int MaxLevels { get; init; }
    public string? ProductPage { get; init; }
    public string? NavigationProvider { get; init; }
    public bool IncludeProducts { get; init; }
}

public record SerializedAudit
{
    public DateTime? CreatedDate { get; init; }
    public DateTime? UpdatedDate { get; init; }
    public string? CreatedBy { get; init; }
    public string? UpdatedBy { get; init; }
}
```

### Rationale

- **Sub-objects for SEO, URL, Visibility, NavigationSettings, Audit**: These are logical groupings in the DW admin UI and produce cleaner YAML:
  ```yaml
  seo:
    metaTitle: "Page Title"
    noindex: true
  urlSettings:
    urlDataProviderTypeName: "Dynamicweb.Ecommerce..."
  ```
- **Flat for simple booleans**: `NavigationTag`, `ShortCut`, `Hidden` etc. are standalone concepts, not worth wrapping.
- **Remove old CreatedDate/UpdatedDate/CreatedBy/UpdatedBy from top level**: Move into `Audit` sub-object. The existing fields on SerializedPage were never populated anyway.
- **YAML backward compatibility**: New optional properties with defaults won't break existing YAML files. Missing properties deserialize as null/false/0 which is correct behavior.

### Confidence: HIGH
Verified by decompiling `Dynamicweb.Content.Page` from the 10.23.9 NuGet -- every property listed above has a public getter and setter on the Page class.

---

## Question 2: PageNavigationSettings -- Child Object or Separate API?

### Answer: It is columns on the Page table, saved inline by SavePage

**Verified by decompilation (HIGH confidence):**

PageNavigationSettings is NOT a separate database table. The settings are stored as columns directly on the `[Page]` table:

| DB Column | PageNavigationSettings Property |
|-----------|-------------------------------|
| `PageNavigation_UseEcomGroups` | `UseEcomGroups` |
| `PageNavigationParentType` | `ParentType` (enum: Groups=1, Shop=2) |
| `PageNavigationGroupSelector` | `Groups` |
| `PageNavigationShopSelector` | `ShopID` |
| `PageNavigationMaxLevels` | `MaxLevels` (>10 stored as "AllLevels") |
| `PageNavigationProductPage` | `ProductPage` |
| `PageNavigationIncludeProducts` | `IncludeProducts` |
| `PageNavigationProvider` | `NavigationProvider` |

### How it works in DW internals

**Serialization (read):** `PageRowExtractor.ExtractPage()` checks `PageNavigation_UseEcomGroups`. If true, creates a `PageNavigationSettings` object and calls `Fill(dataReader)` to populate from the same row.

**Deserialization (write):** `PageRepository.UpdatePage()` calls `AddNavigationSettingsUpdateStatement()` which writes the columns inline in the UPDATE if `page.NavigationSettings != null`. Same for INSERT.

### Integration approach

```csharp
// In ContentMapper.MapPage():
var navSettings = page.NavigationSettings;
SerializedNavigationSettings? serializedNav = null;
if (navSettings != null && navSettings.UseEcomGroups)
{
    serializedNav = new SerializedNavigationSettings
    {
        UseEcomGroups = navSettings.UseEcomGroups,
        ParentType = navSettings.ParentType.ToString(),
        Groups = navSettings.Groups,
        ShopID = navSettings.ShopID,
        MaxLevels = navSettings.MaxLevels,
        ProductPage = navSettings.ProductPage,
        NavigationProvider = navSettings.NavigationProvider,
        IncludeProducts = navSettings.IncludeProducts
    };
}

// In ContentDeserializer.DeserializePage():
if (dto.NavigationSettings != null && dto.NavigationSettings.UseEcomGroups)
{
    page.NavigationSettings = new PageNavigationSettings
    {
        UseEcomGroups = dto.NavigationSettings.UseEcomGroups,
        Groups = dto.NavigationSettings.Groups,
        ShopID = dto.NavigationSettings.ShopID,
        MaxLevels = dto.NavigationSettings.MaxLevels,
        ProductPage = dto.NavigationSettings.ProductPage,
        NavigationProvider = dto.NavigationSettings.NavigationProvider,
        IncludeProducts = dto.NavigationSettings.IncludeProducts
    };
    // ParentType needs enum parse
    if (Enum.TryParse<EcommerceNavigationParentType>(
        dto.NavigationSettings.ParentType, true, out var pt))
        page.NavigationSettings.ParentType = pt;
}
```

No separate API call needed. `Services.Pages.SavePage(page)` handles it.

### Confidence: HIGH
Verified by decompiling `PageRepository.Save()`, `PageRepository.UpdatePage()`, `PageRowExtractor.ExtractPage()`, and `PageNavigationSettings.Fill()`.

---

## Question 3: Area ItemType -- New Provider or Extend ContentProvider?

### Answer: Extend ContentProvider (specifically SerializedArea + ContentMapper)

The Area class has these relevant properties:
- `Area.ItemType` (string) -- the ItemType system name
- `Area.ItemId` (string) -- the Item instance ID
- `Area.Item` (Item) -- the associated Item object (has header/footer/master connections as item fields)
- `Area.ItemTypePageProperty` (string) -- ItemType for page-level properties

### Why NOT a new provider

Area ItemType fields are conceptually part of the content tree. They define website-level settings (header page, footer page, master layout) that belong to the area being serialized. The existing ContentSerializer already receives the Area and already serializes `SerializedArea`.

### Integration approach

Extend `SerializedArea` to include ItemType fields:

```csharp
public record SerializedArea
{
    public required Guid AreaId { get; init; }
    public required string Name { get; init; }
    public required int SortOrder { get; init; }

    // NEW: Area-level ItemType data
    public string? ItemType { get; init; }
    public Dictionary<string, object> ItemFields { get; init; } = new();

    public List<SerializedPage> Pages { get; init; } = new();
}
```

In `ContentMapper.MapArea()`:

```csharp
public SerializedArea MapArea(Area area, List<SerializedPage> pages)
{
    var itemFields = new Dictionary<string, object>();
    if (!string.IsNullOrEmpty(area.ItemType) && area.Item != null)
    {
        var dict = new Dictionary<string, object?>();
        area.Item.SerializeTo(dict);
        foreach (var kvp in dict)
        {
            if (kvp.Value != null)
                itemFields[kvp.Key] = kvp.Value;
        }
    }

    return new SerializedArea
    {
        AreaId = area.UniqueId,
        Name = area.Name ?? string.Empty,
        SortOrder = area.Sort,
        ItemType = area.ItemType,
        ItemFields = itemFields,
        Pages = pages
    };
}
```

In `ContentDeserializer`, after resolving the target area, apply item fields via the same `SaveItemFields` pattern already used for pages.

### Note on internal link resolution

Area ItemType fields may contain page references (e.g., header page = `Default.aspx?ID=123`). The existing `InternalLinkResolver` phase should also scan Area item fields. This is a straightforward extension of `ResolveLinksInArea()`.

### Confidence: HIGH
Verified by decompiling `Dynamicweb.Content.Area` -- ItemType/ItemId/Item are standard public properties. The Item uses the same `SerializeTo`/`DeserializeFrom` pattern as page items.

---

## Question 4: Save Order for Timestamp Preservation

### Answer: SavePage ALWAYS overwrites timestamps. Use post-save SQL UPDATE.

**Critical finding from decompilation:**

The `Page()` constructor (used for new pages) sets:
```csharp
base.Audit = new AuditedEntity(DateTime.Now, userId.ToString());
```

The `AuditedEntity` constructor sets BOTH `CreatedAt` AND `LastModifiedAt` to `DateTime.Now`.

For the INSERT path, the SQL writes:
```sql
INSERT INTO [Page] (..., [PageCreatedDate], [PageUpdatedDate], ..., [PageUserCreate], [PageUserEdit], ...)
VALUES (..., page.Audit.CreatedAt, page.Audit.LastModifiedAt, ..., page.Audit.CreatedBy, page.Audit.LastModifiedBy, ...)
```

For the UPDATE path, when loading an existing page via `GetPage()`, the `PageRowExtractor` correctly restores the original Audit from the DB. So updates preserve the original CreatedAt. However, the `LastModifiedAt` will be updated to `DateTime.Now` if any code calls `MarkAsUpdated()`.

### Timestamp preservation strategy

**For INSERT (new pages):** There is no way to set the Audit via public API before the constructor runs. The `internal Page(AuditedEntity audit, ...)` constructor is not accessible. Solution: **Post-save SQL UPDATE**.

**For UPDATE (existing pages):** The existing Audit is loaded from DB by `PageRowExtractor`. When we load via `Services.Pages.GetPage(id)` and re-save, the original `CreatedAt` is preserved and `LastModifiedAt` reflects the actual update time. If we want to preserve the ORIGINAL `LastModifiedAt`, we need a post-save SQL UPDATE.

### Recommended save order

```
1. SavePage(page)                    // Normal DW save (creates/updates page, assigns ID)
2. SaveItemFields(...)               // Item fields via ItemService
3. SavePropertyItemFields(...)       // PropertyItem fields
4. SQL UPDATE timestamps             // Post-save direct SQL to restore original timestamps
```

### SQL UPDATE for timestamp restoration

```csharp
private void RestoreTimestamps(int pageId, SerializedAudit? audit)
{
    if (audit == null) return;

    var cb = new CommandBuilder();
    cb.Add("UPDATE [Page] SET ", Array.Empty<object>());

    var setClauses = new List<string>();
    if (audit.CreatedDate.HasValue)
        cb.Add<DateTime>("[PageCreatedDate]={0}", audit.CreatedDate.Value);
    if (audit.UpdatedDate.HasValue)
        cb.Add<DateTime>(",[PageUpdatedDate]={0}", audit.UpdatedDate.Value);
    if (!string.IsNullOrEmpty(audit.CreatedBy))
        cb.Add<string>(",[PageUserCreate]={0}", audit.CreatedBy);
    if (!string.IsNullOrEmpty(audit.UpdatedBy))
        cb.Add<string>(",[PageUserEdit]={0}", audit.UpdatedBy);

    cb.Add<int>(" WHERE [PageID]={0}", pageId);
    Database.ExecuteNonQuery(cb);
}
```

This pattern is consistent with the existing `SqlTableProvider` which already uses `Dynamicweb.Data.Database` for direct SQL. The `DwSqlExecutor` provides the abstraction.

### Important: Audit data in ContentMapper

The mapper currently does NOT extract audit data. Extension needed:

```csharp
// In ContentMapper.MapPage():
Audit = new SerializedAudit
{
    CreatedDate = page.Audit?.CreatedAt,      // from Entity<int>.Audit
    UpdatedDate = page.Audit?.LastModifiedAt,
    CreatedBy = page.Audit?.CreatedBy,
    UpdatedBy = page.Audit?.LastModifiedBy
}
```

Note: `page.Audit` is accessed via the `Entity<int>` base class. Since `Page : Entity<int>`, it is directly accessible. The cast `((Dynamicweb.Core.Entity<int>)page).Audit` may be needed if the compiler does not resolve it implicitly. The `Audit` property is public on `Entity<T>`.

### Confidence: HIGH
Verified via decompilation of `PageRepository.InsertPage()`, `PageRepository.UpdatePage()`, `PageRowExtractor.ExtractPage()`, and `AuditedEntity` constructors.

---

## Question 5: Properties Requiring Special DW API Calls

### UrlDataProvider

**No special API needed.** UrlDataProviderTypeName and UrlDataProviderParameters are plain string properties on Page, persisted by SavePage. They just store the .NET type name and its serialized parameters.

```csharp
page.UrlDataProviderTypeName = dto.UrlSettings?.UrlDataProviderTypeName;
page.UrlDataProviderParameters = dto.UrlSettings?.UrlDataProviderParameters;
```

Caveat: The UrlDataProviderTypeName contains a fully-qualified .NET type name (e.g., `Dynamicweb.Ecommerce.Frontend.EcommerceUrlDataProvider`). This is environment-safe because it references the class name, not an instance. It will only work if the target environment has the same DW modules installed.

### NavigationSettings

As documented in Question 2, handled inline by SavePage. Set `page.NavigationSettings = new PageNavigationSettings { ... }` before calling SavePage.

### Permissions

Already handled by existing `PermissionMapper.ApplyPermissions()`. No change needed.

### Properties that are truly read-only or not settable

| Property | Settable? | Notes |
|----------|-----------|-------|
| `Level` | Read-only | Computed from tree depth, not serializable |
| `IsMaster` | Read-only | Computed from `Area.IsMaster` |
| `IsLanguage` | Read-only | Computed from `Area.IsLanguage` |
| `Parent` | Read-only navigation | Set via `ParentPageId` |
| `MasterPage` | Read-only navigation | Set via `MasterPageId` |
| `Layout` (object) | Read-only | Computed from `LayoutLocator.FindLayout(this)`. Use `LayoutTemplate` string. |
| `ShowInMenu` | Writable | Standard bool property, persisted by SavePage |
| `Published` | Writable | Standard bool property, persisted by SavePage |

### Confidence: HIGH
Verified via decompilation -- all writable properties confirmed to have public setters.

---

## Complete Property Mapping Reference

This table maps every DW Page property to the DTO location. Properties already serialized are marked with [EXISTING].

| DW Page Property | DTO Location | Type | Default |
|-----------------|-------------|------|---------|
| UniqueId | PageUniqueId [EXISTING] | Guid | required |
| ID | SourcePageId [EXISTING] | int? | - |
| MenuText | MenuText [EXISTING] | string | required |
| UrlName | UrlName [EXISTING] | string | required |
| Sort | SortOrder [EXISTING] | int | required |
| Active | IsActive [EXISTING] | bool | false |
| ItemType | ItemType [EXISTING] | string? | null |
| LayoutTemplate | Layout [EXISTING] | string? | null |
| LayoutApplyToSubPages | LayoutApplyToSubPages [EXISTING] | bool | false |
| IsFolder | IsFolder [EXISTING] | bool | false |
| TreeSection | TreeSection [EXISTING] | string? | null |
| NavigationTag | NavigationTag (flat) | string? | null |
| ShortCut | ShortCut (flat) | string? | null |
| Hidden | Hidden (flat) | bool | false |
| Allowclick | Allowclick (flat) | bool | true |
| Allowsearch | Allowsearch (flat) | bool | true |
| ShowInSitemap | ShowInSitemap (flat) | bool | true |
| ShowInLegend | ShowInLegend (flat) | bool | true |
| ShowInMenu | ShowInMenu (flat) | bool | true |
| SslMode | SslMode (flat) | int | 0 |
| ColorSchemeId | ColorSchemeId (flat) | string? | null |
| ExactUrl | ExactUrl (flat) | string? | null |
| ContentType | ContentType (flat) | string? | null |
| TopImage | TopImage (flat) | string? | null |
| DisplayMode | DisplayMode (flat) | int | 0 |
| ActiveFrom | ActiveFrom (flat) | DateTime? | null |
| ActiveTo | ActiveTo (flat) | DateTime? | null |
| UrlDataProviderTypeName | UrlSettings.UrlDataProviderTypeName | string? | null |
| UrlDataProviderParameters | UrlSettings.UrlDataProviderParameters | string? | null |
| UrlIgnoreForChildren | UrlSettings.UrlIgnoreForChildren | bool | false |
| UrlUseAsWritten | UrlSettings.UrlUseAsWritten | bool | false |
| MetaTitle | Seo.MetaTitle | string? | null |
| MetaCanonical | Seo.MetaCanonical | string? | null |
| Description | Seo.Description | string? | null |
| Keywords | Seo.Keywords | string? | null |
| Noindex | Seo.Noindex | bool | false |
| Nofollow | Seo.Nofollow | bool | false |
| Robots404 | Seo.Robots404 | bool | false |
| HideForPhones | Visibility.HideForPhones | bool | false |
| HideForTablets | Visibility.HideForTablets | bool | false |
| HideForDesktops | Visibility.HideForDesktops | bool | false |
| NavigationSettings.* | NavigationSettings.* | sub-object | null |
| Audit.CreatedAt | Audit.CreatedDate | DateTime? | null |
| Audit.LastModifiedAt | Audit.UpdatedDate | DateTime? | null |
| Audit.CreatedBy | Audit.CreatedBy | string? | null |
| Audit.LastModifiedBy | Audit.UpdatedBy | string? | null |

### Properties explicitly NOT serialized (computed/internal)

| Property | Reason |
|----------|--------|
| Level | Computed from tree depth |
| IsMaster / IsLanguage | Computed from Area |
| Parent / MasterPage | Navigation properties |
| Layout (object) | Computed; use LayoutTemplate string |
| Languages | Separate area concept |
| Item / PropertyItem | Serialized via Fields/PropertyFields dictionaries |
| CreationRules | Internal template creation rules |
| ApprovalType/State/Step | Workflow state, not content |
| PermissionType/Template | Handled by PermissionMapper |
| CopyOf / MasterPageId / MasterType | Template/language system internals |

---

## Component Modification Map

### Files to modify

| File | Change | Complexity |
|------|--------|-----------|
| `Models/SerializedPage.cs` | Add ~15 flat properties, 5 sub-object properties | Low |
| `Models/SerializedArea.cs` | Add ItemType + ItemFields | Low |
| `Models/` (new files) | Create 5 sub-record types | Low |
| `Serialization/ContentMapper.cs` | Extend `MapPage()` and `MapArea()` | Low-Med |
| `Serialization/ContentDeserializer.cs` | Extend `DeserializePage()` (both INSERT and UPDATE paths), add `RestoreTimestamps()` | Medium |
| `Serialization/ContentSerializer.cs` | No change needed (mapper handles it) | None |

### Files NOT modified

| File | Reason |
|------|--------|
| `Providers/Content/ContentProvider.cs` | Wrapper only, delegates to ContentSerializer/ContentDeserializer |
| `Providers/ISerializationProvider.cs` | Interface unchanged |
| `Infrastructure/FileSystemStore.cs` | YAML serialization handles new properties automatically |

---

## Data Flow

### Serialization (read from DW, write to YAML)

```
DW Page (with Audit, NavigationSettings, all properties)
  |
  v
ContentMapper.MapPage()  -- reads page.NavigationTag, page.Seo, page.Audit, etc.
  |                         creates sub-objects (SerializedSeoSettings, etc.)
  |
  v
SerializedPage DTO (with sub-objects)
  |
  v
YamlDotNet serialization --> .yml file on disk
```

### Deserialization (read from YAML, write to DW)

```
.yml file on disk
  |
  v
YamlDotNet deserialization --> SerializedPage DTO
  |
  v
ContentDeserializer.DeserializePage()
  |
  +-- Step 1: Set all scalar properties on Page object
  +-- Step 2: Set NavigationSettings sub-object if present
  +-- Step 3: Services.Pages.SavePage(page)
  +-- Step 4: SaveItemFields() / SavePropertyItemFields()
  +-- Step 5: RestoreTimestamps() via direct SQL
  +-- Step 6: PermissionMapper.ApplyPermissions()
  |
  v
DW Database (page with all properties preserved)
```

### Area ItemType deserialization

```
SerializedArea with ItemType + ItemFields
  |
  v
ContentDeserializer.DeserializePredicate()
  +-- Load target Area via Services.Areas.GetArea()
  +-- If area.ItemType matches dto.ItemType:
  |     SaveItemFields(area.ItemType, area.ItemId, dto.ItemFields)
  +-- Run InternalLinkResolver on Area item fields too
```

---

## Sources

- DW 10.23.9 DLL decompilation (Dynamicweb.dll, Dynamicweb.Core.dll) -- PRIMARY SOURCE for all findings
- [Page Class Properties](https://doc.dynamicweb.dev/api/Dynamicweb.Content.Page.html)
- [PageNavigationSettings](https://doc.dynamicweb.com/api/html/b9de46f1-8065-0ba0-6c5b-4fa01d90de7e.htm)
- [Area ItemTypes manual](https://doc.dynamicweb.dev/manual/dynamicweb10/settings/areas/content/itemtypes.html)
- [Area Class API](https://doc.dynamicweb.dev/api/Dynamicweb.Content.Area.html)
- [PageService API](https://doc.dynamicweb.dev/api/Dynamicweb.Content.PageService.html)
