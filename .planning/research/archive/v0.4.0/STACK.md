# Technology Stack: Full Page Fidelity

**Project:** DynamicWeb.Serializer v0.4.0 -- Full Page Property Serialization
**Researched:** 2026-04-02
**Confidence:** HIGH (verified against DW 10.23.9 NuGet XML docs + live Swift 2.2 SQL schema)

## Recommended Stack

No new dependencies needed. All APIs are available in the existing `Dynamicweb 10.23.9` NuGet package.

### Core Framework (unchanged)
| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| .NET | 8.0 | Runtime | Already in use |
| Dynamicweb | 10.23.9 | DW10 API | Already referenced; has all needed APIs |
| Dynamicweb.Ecommerce | 10.23.9 | EcomProductGroupField API | Transitive dependency via `Dynamicweb` metapackage |
| YamlDotNet | 13.7.1 | YAML serialization | Already in use |

## API Surface for v0.4.0 Features

### 1. Page Properties -- Complete Column-to-API Mapping

The `Page` table has 103 columns. The DW10 `Dynamicweb.Content.Page` class exposes 93 documented properties. Below is the complete mapping of ALL page columns to their C# API access method.

**Currently serialized (already in ContentMapper):**

| DB Column | C# Property | SerializedPage Field |
|-----------|------------|---------------------|
| PageMenuText | `page.MenuText` | MenuText |
| PageUrlName | `page.UrlName` | UrlName |
| PageSort | `page.Sort` | SortOrder |
| PageActive | `page.Active` | IsActive |
| PageItemType | `page.ItemType` | ItemType |
| PageLayout | `page.LayoutTemplate` | Layout |
| PageLayoutApplyToSubPages | `page.LayoutApplyToSubPages` | LayoutApplyToSubPages |
| PageIsFolder | `page.IsFolder` | IsFolder |
| PageTreeSection | `page.TreeSection` | TreeSection |
| PageUniqueId | `page.UniqueId` | PageUniqueId |
| PagePropertyItemId | `page.PropertyItemId` | (PropertyFields dict) |

**Missing -- need to add to SerializedPage and ContentMapper:**

| DB Column | C# Property | Proposed DTO Field | Priority |
|-----------|------------|-------------------|----------|
| PageNavigationTag | `page.NavigationTag` | NavigationTag | HIGH -- used for frontend nav targeting |
| PageShortCut | `page.ShortCut` | ShortCut | HIGH -- page redirects |
| PageShortCutRedirect | (no direct prop; see notes) | ShortCutRedirect | HIGH -- redirect type flag |
| PageDescription | `page.Description` | Description | HIGH -- SEO meta description |
| PageKeywords | `page.Keywords` | Keywords | HIGH -- SEO meta keywords |
| PageMetaTitle | `page.MetaTitle` | MetaTitle | HIGH -- SEO title |
| PageMetaCanonical | `page.MetaCanonical` | MetaCanonical | MEDIUM -- SEO canonical |
| PageExactUrl | `page.ExactUrl` | ExactUrl | MEDIUM -- custom URL |
| PageNoindex | `page.Noindex` | Noindex | MEDIUM -- SEO robots |
| PageNofollow | `page.Nofollow` | Nofollow | MEDIUM -- SEO robots |
| PageRobots404 | `page.Robots404` | Robots404 | MEDIUM -- SEO robots |
| PageSSLMode | `page.SslMode` | SslMode | MEDIUM -- SSL enforcement |
| PageHidden | `page.Hidden` | Hidden | MEDIUM -- hidden from nav |
| PageActiveFrom | `page.ActiveFrom` | ActiveFrom | MEDIUM -- scheduled visibility |
| PageActiveTo | `page.ActiveTo` | ActiveTo | MEDIUM -- scheduled visibility |
| PageAllowsearch | `page.Allowsearch` | AllowSearch | MEDIUM -- search indexing |
| PageShowInSitemap | `page.ShowInSitemap` | ShowInSitemap | MEDIUM -- sitemap inclusion |
| PageAllowClick | `page.Allowclick` | AllowClick | MEDIUM -- nav clickability |
| PageUrlIgnoreForChildren | `page.UrlIgnoreForChildren` | UrlIgnoreForChildren | MEDIUM -- URL inheritance |
| PageUrlUseAsWritten | `page.UrlUseAsWritten` | UrlUseAsWritten | MEDIUM -- URL exact match |
| PageUrlDataProvider | `page.UrlDataProviderTypeName` | UrlDataProvider | LOW -- custom URL provider |
| PageUrlDataProviderParameters | `page.UrlDataProviderParameters` | UrlDataProviderParameters | LOW -- URL provider config |
| PageDisplayMode | `page.DisplayMode` | DisplayMode | LOW -- display mode enum |
| PageContentType | `page.ContentType` | ContentType | LOW -- MIME type |
| PageColorSchemeId | `page.ColorSchemeId` | ColorSchemeId | MEDIUM -- visual theming |
| PageHideForPhones | `page.HideForPhones` | HideForPhones | LOW -- responsive visibility |
| PageHideForTablets | `page.HideForTablets` | HideForTablets | LOW -- responsive visibility |
| PageHideForDesktops | `page.HideForDesktops` | HideForDesktops | LOW -- responsive visibility |
| PageShowInLegend | `page.ShowInLegend` | ShowInLegend | LOW -- legend visibility |
| PageShowUpdateDate | `page.ShowUpdateDate` | ShowUpdateDate | LOW -- display flag |
| PageMasterPageId | `page.MasterPageId` | MasterPageId | LOW -- language master |
| PageMasterType | `page.MasterType` | MasterType | LOW -- master type enum |

**Intentionally NOT serialized (environment-specific or deprecated):**

| DB Column | Reason to Skip |
|-----------|---------------|
| PageId | Numeric ID; use UniqueId instead |
| PageParentPageId | Reconstructed from tree hierarchy |
| PageAreaId | Set from predicate config |
| PagePassword | Security concern |
| PagePermission | Handled separately via PermissionMapper |
| PagePermissionType | Handled separately via PermissionMapper |
| PagePermissionTemplate | Handled separately via PermissionMapper |
| PageUserManagementPermissions | Handled separately via PermissionMapper |
| PageUserCreate | Numeric user ID; environment-specific |
| PageUserEdit | Numeric user ID; environment-specific |
| PageCreatedDate | Timestamp; see section 4 below |
| PageUpdatedDate | Timestamp; see section 4 below |
| PageStylesheet | Deprecated (numeric ID) |
| PageBackgroundImage | Deprecated |
| PageTopLogoImage | Deprecated |
| PageTopLogoImageAlt | Deprecated |
| PageMenuLogoImage | Deprecated |
| PageFooterImage | Deprecated |
| PageMouseOver | Deprecated |
| PageImageMouseOver | Deprecated |
| PageImageMouseOut | Deprecated |
| PageImageActivePage | Deprecated |
| PageTextAndImage | Deprecated |
| PageShowTopImage | Deprecated |
| PageRotation | Deprecated |
| PageRotationType | Deprecated |
| PageDublinCore | Deprecated |
| PageManager | Deprecated workflow |
| PageManageFrequence | Deprecated workflow |
| PageShowFavorites | Deprecated |
| PageCacheMode | Server-side, not content |
| PageCacheFrequence | Server-side, not content |
| PageApprovalType | Workflow, environment-specific |
| PageApprovalState | Workflow, environment-specific |
| PageApprovalStep | Workflow, environment-specific |
| PageTopLevelIntegration | Data integration, environment-specific |
| PageForIntegration | Data integration, environment-specific |
| PageCopyOf | Copy tracking, environment-specific |
| PageProtect | Legacy password protection |
| PageIsTemplate | Template management, not content |
| PageTemplateDescription | Template management |
| PageTemplateImage | Template management |
| PageHasExperiment | A/B test state, environment-specific |
| PageCreationRules | Content creation workflow |
| PageDeleted | Soft delete state |
| PageDeletedBy | Soft delete metadata |
| PageDeletedAt | Soft delete metadata |
| PageIcon | Already in PropertyFields dict |
| PageLayoutPhone | Deprecated responsive layout |
| PageLayoutTablet | Deprecated responsive layout |

### 2. PageNavigationSettings -- Ecommerce Navigation Config

**Key finding:** PageNavigationSettings is NOT a separate table. It is a child object of `Page` whose properties are stored as inline columns on the `Page` table (prefixed `PageNavigation_*` and `PageNavigation*`).

**C# API access pattern:**
```csharp
// READ
Page page = Services.Pages.GetPage(pageId);
PageNavigationSettings navSettings = page.NavigationSettings;
bool useEcomGroups = navSettings.UseEcomGroups;
string parentType = navSettings.ParentType;
string groups = navSettings.Groups;        // Comma-separated group IDs
string shopId = navSettings.ShopID;
string maxLevels = navSettings.MaxLevels;  // "AllLevels" or numeric
string productPage = navSettings.ProductPage;  // "Default.aspx?Id=NNNN"
string navProvider = navSettings.NavigationProvider;
bool includeProducts = navSettings.IncludeProducts;
```

**DB column to C# property mapping:**

| DB Column | C# Property on `page.NavigationSettings` | Type |
|-----------|------------------------------------------|------|
| PageNavigation_UseEcomGroups | UseEcomGroups | bool |
| PageNavigationParentType | ParentType | string ("Shop", "Root", "Group") |
| PageNavigationGroupSelector | Groups | string (comma-delimited group IDs or "[all]") |
| PageNavigationMaxLevels | MaxLevels | string ("AllLevels" or numeric) |
| PageNavigationProductPage | ProductPage | string (page link like "Default.aspx?Id=5862") |
| PageNavigationShopSelector | ShopID | string (shop ID like "SHOP1") |
| PageNavigationIncludeProducts | IncludeProducts | bool |
| PageNavigationProvider | NavigationProvider | string (custom provider class name) |

**CRITICAL: PageNavigationSettings.ProductPage contains internal page links** ("Default.aspx?Id=5862") that need to go through InternalLinkResolver during deserialization, exactly like ShortCut values.

**WRITE pattern:** Setting `page.NavigationSettings` properties and then calling `Services.Pages.SavePage(page)` persists the navigation settings. The `PageRepository.AddNavigationSettingsUpdateStatement` method is called internally by the repository's Save/Update flow (verified in XML docs).

**Serialization approach:** Flatten `NavigationSettings` properties into the SerializedPage DTO as individual fields with a clear prefix (e.g., `NavUseEcomGroups`, `NavParentType`, etc.) or as a nested sub-object.

### 3. Area ItemType (Header/Footer/Master) Connections

**Key finding:** Area-level ItemType connections work exactly like Page-level ItemType connections. The Area table has:
- `AreaItemType` -- the ItemType system name (e.g., "Swift-v2_Master")
- `AreaItemId` -- the numeric item instance ID (e.g., "6")
- `AreaItemTypePageProperty` -- the page property ItemType (e.g., "Swift-v2_PageProperties")

**C# API access pattern:**
```csharp
// READ
Area area = Services.Areas.GetArea(areaId);
string itemType = area.ItemType;    // "Swift-v2_Master"
string itemId = area.ItemId;        // "6"
string pagePropertyType = area.ItemTypePageProperty;  // "Swift-v2_PageProperties"

// Read item fields (header/footer page references)
Dynamicweb.Content.Items.Item item = area.Item;
// item["HeaderDesktop"] = "121" (page ID)
// item["FooterDesktop"] = "146" (page ID)
// item["HeaderMobile"] = "122" (page ID)
// item["FooterMobile"] = "147" (page ID)
```

**Live data from Swift 2.2 (Area ID=3, "Swift 2"):**
- AreaItemType = "Swift-v2_Master"
- AreaItemId = "6"
- ItemType table `ItemType_Swift-v2_Master` stores fields:
  - HeaderDesktop = "121" (page ID)
  - HeaderMobile = "122" (page ID)
  - FooterDesktop = "146" (page ID)
  - FooterMobile = "147" (page ID)
  - Plus ~30 SEO/social/config fields (Google_Site_Verification, Open_Graph_*, Twitter_*, etc.)

**CRITICAL: Header/footer values are numeric page IDs that need GUID resolution.**
During serialization, resolve page ID -> PageUniqueId GUID.
During deserialization, resolve GUID -> target environment page ID.

**WRITE pattern:**
```csharp
// Write area item fields via ItemService
Area area = Services.Areas.GetArea(areaId);
Services.Items.UpdateItem(area.ItemType, area.ItemId, fieldValues);
// Or: use area.Item[fieldName] = value; then SaveArea
```

**Serialization approach:** Expand `SerializedArea` to include Area ItemType fields dict (like page `Fields`), resolving page ID references to GUIDs. On deserialization, resolve GUIDs back to target page IDs using the same `InternalLinkResolver` pattern.

### 4. Timestamp Preservation During Deserialization

**Key finding:** The `Page` class does NOT expose `CreatedDate` or `UpdatedDate` as public C# properties. These exist only as DB columns:
- `PageCreatedDate` (datetime, nullable)
- `PageUpdatedDate` (datetime, nullable)

The `PageRepository.Save()` method internally sets `PageUpdatedDate = DateTime.Now` (and `PageCreatedDate` on insert). There is no public API to override these values.

**Approach: Direct SQL UPDATE after SavePage:**

```csharp
// After Services.Pages.SavePage(page):
using var cmd = Database.CreateConnection().CreateCommand();
cmd.CommandText = "UPDATE Page SET PageCreatedDate = @created, PageUpdatedDate = @updated WHERE PageId = @id";
cmd.Parameters.AddWithValue("@created", dto.CreatedDate);
cmd.Parameters.AddWithValue("@updated", dto.UpdatedDate);
cmd.Parameters.AddWithValue("@id", savedPage.ID);
cmd.ExecuteNonQuery();
```

**READ pattern for serialization:**
Since `Page` doesn't expose these as properties, serialization also needs direct SQL:
```csharp
// Read timestamps for a page
using var cmd = Database.CreateConnection().CreateCommand();
cmd.CommandText = "SELECT PageCreatedDate, PageUpdatedDate, PageUserCreate, PageUserEdit FROM Page WHERE PageId = @id";
```

**Alternative (simpler): Use `Dynamicweb.Data.Database` helper:**
```csharp
var reader = Database.CreateDataReader("SELECT PageCreatedDate, PageUpdatedDate FROM Page WHERE PageId = @id", new { id = pageId });
```

**Recommended approach:** Serialize timestamps during serialization via direct SQL read. During deserialization, after `SavePage`, issue a direct SQL UPDATE to restore the original timestamps. This is a known pattern in DW data integration scenarios where the API doesn't expose all DB columns.

### 5. EcomProductGroupField and Custom Column Schema

**Key finding:** `EcomProductGroupField` is in namespace `Dynamicweb.Ecommerce.Products`. It defines custom fields that are physically added as columns to the `EcomGroups` table.

**C# API classes:**
- `Dynamicweb.Ecommerce.Products.ProductGroupField` -- field definition entity
- `Dynamicweb.Ecommerce.Products.ProductGroupFieldRepository` -- CRUD operations
- `Dynamicweb.Ecommerce.Products.ProductGroupFieldCollection` -- collection type

**ProductGroupField properties:**
| Property | DB Column | Type |
|----------|-----------|------|
| Id | ProductGroupFieldId | string (e.g., "GROUPFIELD5") |
| Name | ProductGroupFieldName | string |
| SystemName | ProductGroupFieldSystemName | string |
| TemplateName | ProductGroupFieldTemplateName | string |
| TypeId | ProductGroupFieldTypeId | int |
| TypeName | ProductGroupFieldTypeName | string |
| Locked | ProductGroupFieldLocked | bool |
| Sort | ProductGroupFieldSort | int |
| ListPresentationType | ProductGroupFieldListPresentationType | int |
| Required | ProductGroupFieldRequired | bool |
| Description | ProductGroupFieldDescription | string |
| FieldValueConversionPreset | ProductGroupFieldValueConversionPreset | int |
| FieldValueConversionDecimals | ProductGroupFieldValueConversionDecimals | int |
| FieldValueConversionDisplayRule | ProductGroupFieldValueConversionDisplayRule | int |

**READ pattern:**
```csharp
var repo = new ProductGroupFieldRepository();
var fields = repo.GetProductGroupFields();  // All fields
var field = repo.GetProductGroupFieldById("GROUPFIELD5");  // By ID
```

**WRITE pattern:**
```csharp
var repo = new ProductGroupFieldRepository();
// Save field definition (creates column on EcomGroups if new)
repo.Save(productGroupField);
// UpdateTable adds the physical column to EcomGroups
repo.UpdateTable(productGroupField, fieldType);
```

**How custom columns work:**
1. `EcomProductGroupField` table stores field definitions (metadata)
2. When a field is saved, `ProductGroupFieldRepository.UpdateTable()` creates a corresponding column on the `EcomGroups` table with the field's `SystemName` as the column name
3. The column type depends on the `FieldType` (e.g., Link -> nvarchar, Checkbox -> bit, File Manager -> nvarchar, etc.)

**Live example from Swift 2.2:**
| FieldId | SystemName | Column on EcomGroups |
|---------|-----------|---------------------|
| GROUPFIELD5 | ProductGroupPromotionLink | ProductGroupPromotionLink |
| GROUPFIELD8 | SelectedGroup | SelectedGroup |
| GROUPFIELD1 | ProductGroupPromotionImage | ProductGroupPromotionImage |

**Serialization approach:** This is already handled by `SqlTableProvider` for the `EcomProductGroupField` table. The schema sync (ensuring columns exist on `EcomGroups` before data import) needs the `ProductGroupFieldRepository.Save()` + `UpdateTable()` calls during deserialization. However, since this is a schema-level concern, it may be better handled as a pre-deserialization step or as a separate SchemaProvider.

**NOTE:** The existing `SqlTableProvider` (phase 15) already serializes the `EcomProductGroupField` rows. The v0.4.0 concern is ensuring the physical `EcomGroups` column exists before the `EcomGroups` data is deserialized. This is a dependency ordering issue, not a new provider.

## Alternatives Considered

| Category | Recommended | Alternative | Why Not |
|----------|-------------|-------------|---------|
| Timestamps | Direct SQL after SavePage | Skip timestamps entirely | Timestamps are important for audit/compliance; direct SQL is the only option since Page API doesn't expose them |
| NavigationSettings | Page.NavigationSettings object | Direct SQL read/write | The API object works; no need for raw SQL |
| Area ItemType | Area.Item + ItemService | Direct SQL | API is cleaner and handles item table routing |
| EcomProductGroupField | ProductGroupFieldRepository | Direct SQL | Repository handles column creation automatically |

## Sources

- DW 10.23.9 NuGet XML docs: `~/.nuget/packages/dynamicweb/10.23.9/lib/net8.0/Dynamicweb.xml` (HIGH confidence)
- DW 10.23.9 Ecommerce XML docs: `~/.nuget/packages/dynamicweb.ecommerce/10.23.9/lib/net8.0/Dynamicweb.Ecommerce.xml` (HIGH confidence)
- Live SQL schema from Swift-2.2 database on `localhost\SQLEXPRESS` (HIGH confidence)
- [DW10 Page Class API](https://doc.dynamicweb.com/api/html/7a9e65e0-8347-372b-1b89-617a12bc4b5c.htm)
- [DW10 PageService API](https://doc.dynamicweb.com/api/html/15516fc9-3e1c-ac41-9849-cc6ad67bb84d.htm)
