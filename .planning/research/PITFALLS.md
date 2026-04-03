# Domain Pitfalls: Full Page Fidelity (v0.4.0)

**Domain:** Expanding page property coverage in DynamicWeb content serializer
**Researched:** 2026-04-02 (updated with DLL decompilation findings)

## Critical Pitfalls

### Pitfall 1: Page() Constructor Always Sets Audit to DateTime.Now
**What goes wrong:** `new Page()` calls `base.Audit = new AuditedEntity(DateTime.Now, userId)`. This means every newly created page gets current timestamps. The `internal Page(AuditedEntity, ...)` constructor that accepts custom timestamps is not accessible from outside the DW assembly.
**Why it happens:** DW's entity pattern sets audit info in the constructor. There is no public API to override it before SavePage.
**Consequences:** All deserialized NEW pages show "created" and "last updated" as the deserialization time, not the original content creation/update time.
**Prevention:** Use a two-step approach: (1) `SavePage` via API to create the page and get its ID, (2) direct SQL `UPDATE [Page] SET [PageCreatedDate]=@date, [PageUpdatedDate]=@date WHERE [PageID]=@id` to restore original timestamps. The `Dynamicweb.Data.Database.ExecuteNonQuery(CommandBuilder)` pattern is already used in SqlTableProvider.
**Detection:** Compare `PageCreatedDate`/`PageUpdatedDate` in target DB with values in YAML after deserialization.

### Pitfall 2: ShortCut Contains Internal Page Links
**What goes wrong:** `Page.ShortCut` stores values like `"Default.aspx?ID=8284"`. If serialized as-is, the numeric page ID will point to a different (or non-existent) page in the target environment.
**Why it happens:** ShortCut uses the same internal link format as ItemType field references.
**Consequences:** Page redirects break or redirect to wrong pages after deserialization.
**Prevention:** Run `ShortCut` values through `InternalLinkResolver` during the link resolution phase. This requires extending the resolver to handle non-item-field string values.
**Detection:** Check for `Default.aspx?ID=` patterns in serialized ShortCut values.

### Pitfall 3: NavigationSettings.ProductPage Contains Internal Page Links
**What goes wrong:** `PageNavigationSettings.ProductPage` stores values like `"Default.aspx?Id=5862"`. Same problem as ShortCut.
**Consequences:** Ecommerce product navigation links to wrong product detail page.
**Prevention:** Include `NavigationSettings.ProductPage` in the InternalLinkResolver pass. This is a separate string from item fields, so needs explicit handling.
**Detection:** Check for `Default.aspx?Id=` patterns in NavigationSettings ProductPage values.

### Pitfall 4: Area ItemType Fields Contain Raw Page IDs
**What goes wrong:** Area-level item fields like `HeaderDesktop = "121"` are raw numeric page IDs. These differ between environments.
**Why it happens:** The ItemType table stores page references as string-encoded numeric IDs, not as GUIDs.
**Consequences:** Header/footer pages point to wrong pages (or non-existent pages) in target environment, breaking the entire site layout.
**Prevention:** Extend `ResolveLinksInArea()` to also scan Area item fields. The existing `InternalLinkResolver` pattern applies. After page deserialization builds the source-to-target page ID map, resolve Area item field values.
**Detection:** Verify that header/footer pages render correctly after deserialization.

### Pitfall 5: EcomProductGroupField Column Must Exist Before EcomGroups Data Import
**What goes wrong:** If `SqlTableProvider` deserializes `EcomGroups` data before `EcomProductGroupField` schema is applied, INSERT/UPDATE fails because custom columns don't exist on the target `EcomGroups` table.
**Why it happens:** `ProductGroupFieldRepository.Save()` + `UpdateTable()` creates columns on `EcomGroups`. If the field definition rows are imported but `UpdateTable()` is not called, the physical columns are missing.
**Consequences:** Ecommerce group data deserialization fails with SQL column-not-found errors.
**Prevention:** Ensure `EcomProductGroupField` deserialization calls `UpdateTable()` for each field, or ensure schema sync runs before data sync.
**Detection:** SQL errors referencing missing columns on `EcomGroups` during deserialization.

## Moderate Pitfalls

### Pitfall 6: NavigationSettings Only Populated When UseEcomGroups is True
**What goes wrong:** `PageRowExtractor.ExtractPage()` only creates a `NavigationSettings` object when `PageNavigation_UseEcomGroups` is true in the DB. If you create a `PageNavigationSettings` on a page that previously had no ecommerce navigation, `SavePage` will write the columns. But if you want to REMOVE navigation settings, setting `page.NavigationSettings = null` may not clear the DB columns because `AddNavigationSettingsUpdateStatement` skips when NavigationSettings is null.
**Prevention:** To clear navigation settings, explicitly set `UseEcomGroups = false` and save, rather than setting NavigationSettings to null. On deserialization, if YAML has no NavigationSettings, do NOT touch the property (leave it as loaded from DB).
**Detection:** Round-trip test: serialize a page with ecommerce navigation, deserialize, verify settings persist.

### Pitfall 7: Audit.CreatedBy/LastModifiedBy Store User IDs as Strings
**What goes wrong:** The `PageUserCreate` and `PageUserEdit` columns store numeric user IDs as strings (verified: `Converter.ToInt32(dataReader["PageUserCreate"])` then `num.ToString(CultureInfo.InvariantCulture)`). User IDs are environment-specific -- user ID 5 in source may not be the same user in target.
**Prevention:** Serialize the user ID as-is. Accept that user attribution may not resolve correctly in the target environment. This is a known limitation -- user sync is out of scope for v0.4.0. Document it.
**Detection:** Check `PageUserCreate`/`PageUserEdit` values after deserialization.

### Pitfall 8: Area.Item May Be Null for Areas Without ItemType
**What goes wrong:** Calling `area.Item.SerializeTo(dict)` when `area.ItemType` is null/empty throws a NullReferenceException.
**Prevention:** Always check `!string.IsNullOrEmpty(area.ItemType)` AND `area.Item != null` before accessing.

### Pitfall 9: YAML Null vs Empty String for Optional Properties
**What goes wrong:** YAML serializes `null` and `""` differently. When deserializing, a property that was `null` in source might become `""` in target (or vice versa), causing unnecessary "updates" on every sync.
**Prevention:** Normalize: treat both `null` and `""` as "no value" for string properties. Use `string.IsNullOrEmpty()` consistently. For boolean defaults (Allowclick=true, Allowsearch=true, etc.), ensure YAML omits default values or always includes them.

### Pitfall 10: Boolean Properties with Non-False Defaults
**What goes wrong:** Several Page properties default to `true` in DW (Allowclick, Allowsearch, ShowInSitemap, ShowInLegend, Active). If old YAML files don't include these new properties, YamlDotNet will deserialize them as `false`, changing the page behavior.
**Prevention:** Set `init` defaults on the DTO to match DW defaults: `public bool Allowclick { get; init; } = true;`. This ensures missing YAML keys produce correct defaults.
**Detection:** Deserialize old YAML with new code; verify these booleans are true.

## Minor Pitfalls

### Pitfall 11: Large Description/Keywords Fields
**What goes wrong:** `PageDescription` and `PageKeywords` are ntext columns. Very large values may cause YAML readability issues.
**Prevention:** Use DoubleQuoted YAML style for multiline strings (already established pattern in codebase).

### Pitfall 12: NavigationSettings.Groups Contains Ecom Group IDs
**What goes wrong:** `NavigationSettings.Groups` may contain ecommerce group IDs that differ between environments.
**Prevention:** For v0.4.0, serialize as-is. Group ID resolution is out of scope. Document as a known limitation.

### Pitfall 13: NavigationSettings.MaxLevels "AllLevels" Encoding
**What goes wrong:** DW stores `MaxLevels > 10` as the string `"AllLevels"` in the DB. When reading, it converts back to `100`. If our YAML stores `100`, it will write `100` instead of `"AllLevels"`, but DW handles this correctly (both produce the same behavior). However, it means the DB value changes format.
**Prevention:** Consider storing as `100` in YAML consistently. The DW `AddNavigationSettingsUpdateStatement` method handles the `>10` check and writes `"AllLevels"` automatically.

### Pitfall 14: DisplayMode is an Enum Stored as Int
**What goes wrong:** `Page.DisplayMode` uses the `DisplayMode` enum. Serializing as int works but is not human-readable in YAML.
**Prevention:** Serialize as string name (like NavigationSettings.ParentType), parse back with `Enum.TryParse` on deserialization. This matches the existing VerticalAlignment pattern in GridRow serialization.

## Phase-Specific Warnings

| Phase Topic | Likely Pitfall | Mitigation |
|-------------|---------------|------------|
| Page scalar properties | Pitfall 9, 10 (null vs empty, boolean defaults) | Normalize nulls, set correct init defaults |
| NavigationSettings | Pitfall 3, 6, 13 (ProductPage links, null handling, MaxLevels) | Link resolver + careful null logic |
| ShortCut | Pitfall 2 (internal links) | InternalLinkResolver |
| Timestamps | Pitfall 1, 7 (SavePage overwrites, user IDs cross-env) | Direct SQL after SavePage |
| Area ItemType | Pitfall 4, 8 (page ID refs, null item) | GUID resolution + null checks |
| EcomProductGroupField | Pitfall 5 (column ordering) | Schema before data |

## Sources

- DW 10.23.9 DLL decompilation: `PageRepository.InsertPage()`, `PageRepository.UpdatePage()`, `PageRowExtractor.ExtractPage()`, `AuditedEntity` constructors, `AddNavigationSettingsUpdateStatement()` -- PRIMARY SOURCE
- [Page Class API](https://doc.dynamicweb.dev/api/Dynamicweb.Content.Page.html)
- [PageNavigationSettings](https://doc.dynamicweb.com/api/html/b9de46f1-8065-0ba0-6c5b-4fa01d90de7e.htm)
- Existing codebase patterns (InternalLinkResolver, ContentDeserializer, SqlTableProvider)
