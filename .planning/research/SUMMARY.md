# Research Summary: Full Page Fidelity (v0.4.0)

**Domain:** DynamicWeb 10 page-level content serialization completeness
**Researched:** 2026-04-02 (updated with DLL decompilation verification)
**Overall confidence:** HIGH

## Executive Summary

Decompilation of the DW 10.23.9 DLL confirms that all ~30 missing page properties have public getters and setters on the `Page` class, and `SavePage` persists every one of them through inline SQL INSERT/UPDATE statements. No special API calls are needed for any of the missing properties -- they all flow through the same `SavePage` codepath.

The most important finding is the **timestamp mechanism**: `Page` inherits from `Entity<int>` which has an `Audit` property of type `AuditedEntity`. The `Page()` public constructor always sets `Audit = new AuditedEntity(DateTime.Now, userId)`, meaning new pages get current timestamps. The `internal` constructor accepts a custom `AuditedEntity` but is not accessible. The only way to preserve original timestamps on INSERT is a post-save direct SQL UPDATE using `Dynamicweb.Data.Database.ExecuteNonQuery()`, a pattern already established in our `SqlTableProvider`.

`PageNavigationSettings` is confirmed as **inline columns on the Page table**, not a separate entity. `PageRepository.UpdatePage()` calls `AddNavigationSettingsUpdateStatement()` which writes 8 `PageNavigation*` columns in the same UPDATE statement. On read, `PageRowExtractor.ExtractPage()` only creates the `NavigationSettings` object when `PageNavigation_UseEcomGroups` is true.

Area ItemType fields use the standard `Item.SerializeTo()`/`Item.DeserializeFrom()` pattern, same as page items. The `Area` class exposes `ItemType`, `ItemId`, and `Item` as public properties.

The DTO should use **sub-objects for logical groupings** (SEO, URL settings, visibility, navigation settings, audit) to keep YAML clean, while keeping simple standalone properties flat on SerializedPage.

## Key Findings

**Stack:** No new dependencies. All APIs exist in `Dynamicweb 10.23.9` NuGet. Direct SQL via `Dynamicweb.Data.Database` for timestamp preservation.
**Architecture:** Extend existing ContentMapper/ContentDeserializer. Sub-object DTOs for grouping. Post-save SQL for timestamps. Extend SerializedArea for item fields.
**Critical pitfall:** `new Page()` always sets timestamps to DateTime.Now; must use direct SQL post-save to restore originals. Boolean defaults (Allowclick=true, etc.) must be set as init defaults on DTO to avoid breaking old YAML files.

## Implications for Roadmap

Based on research, suggested phase structure:

1. **DTO + Mapper Extension** - Create sub-record types, extend SerializedPage with ~30 properties, extend ContentMapper
   - Addresses: All missing page properties (SEO, visibility, URL, navigation tag, etc.)
   - Avoids: Pitfall 10 (boolean defaults) by setting correct init defaults

2. **Deserializer Extension** - Extend ContentDeserializer INSERT and UPDATE paths to write all new properties
   - Addresses: Full round-trip for all page properties
   - Avoids: Pitfall 9 (null vs empty) with consistent normalization

3. **NavigationSettings + ShortCut** - Serialize PageNavigationSettings object, extend link resolution for ShortCut and ProductPage
   - Addresses: Ecommerce navigation config, page redirects
   - Avoids: Pitfall 2, 3 (internal links in ShortCut and ProductPage)

4. **Timestamp Preservation** - Direct SQL for CreatedDate/UpdatedDate/CreatedBy/UpdatedBy after SavePage
   - Addresses: Audit trail, content age tracking
   - Avoids: Pitfall 1 (Page constructor overwriting timestamps)

5. **Area ItemType Fields** - Extend SerializedArea with ItemType + ItemFields, include in link resolution
   - Addresses: Header/footer/master page connections
   - Avoids: Pitfall 4, 8 (page ID refs, null item)

6. **EcomProductGroupField Schema** - Ensure UpdateTable is called during field deserialization
   - Addresses: Custom column existence before data import
   - Avoids: Pitfall 5 (column not found errors)

**Phase ordering rationale:**
- Phases 1-2 are the core work (DTO + mapper + deserializer) with no external dependencies
- Phase 3 depends on InternalLinkResolver (already exists) and benefits from phase 2 being done
- Phase 4 is independent but logically follows after the save pipeline is complete
- Phase 5 extends SerializedArea (different part of pipeline, can be done in parallel with 3-4)
- Phase 6 is SqlTableProvider concern, orthogonal to content pipeline

**Research flags for phases:**
- Phases 1-2: Standard pattern, well-understood -- no research needed
- Phase 3: RESOLVED -- NavigationSettings saves inline via SavePage (verified by decompilation)
- Phase 4: RESOLVED -- `Dynamicweb.Data.Database.ExecuteNonQuery(CommandBuilder)` confirmed available (used by SqlTableProvider already)
- Phase 5: RESOLVED -- Area.Item uses same SerializeTo/DeserializeFrom pattern as page items
- Phase 6: Still needs verification of `ProductGroupFieldRepository.UpdateTable` behavior

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Page properties | HIGH | Verified all properties via DLL decompilation of Page class |
| NavigationSettings | HIGH | Verified inline SQL, PageRowExtractor, AddNavigationSettingsUpdateStatement |
| Area ItemType | HIGH | Verified Area.ItemType/ItemId/Item properties via decompilation |
| Timestamps | HIGH | Verified AuditedEntity, Page constructors, PageRepository.InsertPage/UpdatePage |
| Save order | HIGH | Verified PageRepository.Save, SavePage, and audit writing |
| EcomProductGroupField | MEDIUM | API exists per XML docs but UpdateTable behavior not decompiled |

## Gaps Resolved (from prior research)

- **ShortCutRedirect:** The `PageShortCutRedirect` DB column is always written as `true` in `PageRepository.UpdatePage()`. It is NOT exposed as a Page property. DW hardcodes it to `true`. No action needed.
- **NavigationSettings save behavior:** Confirmed working -- `SavePage` writes NavigationSettings inline when not null.
- **Dynamicweb.Data.Database API:** Confirmed via existing `DwSqlExecutor` in SqlTableProvider.
- **ItemService for Area items:** Uses same `Item.SerializeTo()`/`Item.DeserializeFrom()` + `Item.Save()` pattern.

## Remaining Gaps

- **NavigationSettings.Groups cross-env:** Group IDs in NavigationSettings may differ between environments. Out of scope for v0.4.0. Document as known limitation.
- **Audit.CreatedBy/LastModifiedBy cross-env:** User IDs are environment-specific. Serialize as-is, accept this limitation.
- **DisplayMode enum:** Should serialize as string name for readability. Minor enhancement.
