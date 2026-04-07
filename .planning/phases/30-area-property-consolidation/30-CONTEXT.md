# Phase 30: Area Property Consolidation - Context

**Gathered:** 2026-04-07
**Status:** Ready for planning
**Mode:** Auto-generated (autonomous mode)

<domain>
## Phase Boundary

ContentProvider serializes and deserializes all 60+ Area columns (Domain, Layout, Culture, EcomSettings, SSL, CDN, etc.) in area.yml, with field-level blacklist for environment-specific values. Currently area.yml only has 5 fields (areaId, name, sortOrder, itemType, itemFields). This phase expands it to carry all Area table properties alongside ItemType fields.

</domain>

<decisions>
## Implementation Decisions

### Area Property Model
- Extend SerializedArea with a `Properties` dictionary (Dictionary<string, object>) for all Area columns
- This is consistent with the existing Fields/ItemFields pattern on the model
- Properties dictionary populated via DW Area API or direct SQL SELECT on the [Area] table
- On deserialize, write properties back via DW API or SQL UPDATE

### Area Creation on Deserialize
- If the area doesn't exist on target, create it (currently ContentDeserializer skips if area missing)
- Must clear AreaService cache after creation (from project memory: project_dw_area_cache.md)

### Field-Level Blacklist for Areas
- ExcludeFields from Phase 28 applies to area properties too
- Environment-specific columns like AreaDomain, AreaNoindex, AreaGoogleTagManagerID should be excludable
- The excludeFields mechanism already exists — just need to apply it during area property mapping

### Claude's Discretion
- Whether to use DW Area C# API properties or direct SQL for reading/writing area columns
- Architecture research suggests Dictionary<string, object> via Database.CreateDataReader — resilient to schema changes
- Which area columns to read (all vs a curated list)
- Whether to use the existing `areaId` GUID field as the identity key (already present in area.yml)

</decisions>

<code_context>
## Existing Code Insights

### Current SerializedArea Model
From Phase 24, area.yml has: areaId (GUID), name, sortOrder, itemType, itemFields

### Integration Points
- ContentMapper.MapArea() — extend to read full Area properties
- ContentDeserializer — extend to write Area properties back, create area if missing
- SerializedArea model — add Properties dictionary

### DW Area API
- Services.Areas provides area access
- Area class has 60+ C# properties matching SQL columns
- Must verify which properties are settable vs read-only

### Cache Clearing
- Services.Areas.ClearCache() or equivalent after area creation/update
- Critical for subsequent ContentProvider operations that depend on area existing

</code_context>

<specifics>
## Specific Ideas

Real Area SQL table columns (from full sync YAML analysis):
AreaId, AreaStyleId, AreaName, AreaDomain, AreaEncoding, AreaPermission, AreaPermissionTemplate, AreaTitle, AreaKeywords, AreaDescription, AreaFrontpage, AreaDateformat, AreaCodepage, AreaLanguage, StyleId, AreaMasterTemplate, AreaHtmlType, AreaCulture, AreaApprovalType, AreaEcomLanguageId, AreaEcomCurrencyId, AreaActive, AreaSort, AreaMasterAreaId, AreaRobotsTxt, AreaRobotsTxtIncludeSitemap, AreaDomainLock, AreaUserManagementPermissions, AreaUrlName, AreaUpdatedDate, AreaCreatedDate, AreaCopyOf, AreaLayout, AreaNotFound, AreaRedirectFirstPage, AreaLayoutPhone, AreaLayoutTablet, AreaLockPagesToDomain, AreaEcomCountryCode, AreaEcomShopId, AreaUrlIgnoreForChildren, AreaItemType, AreaItemId, AreaCookieWarningTemplate, AreaCookieCustomNotifications, AreaItemTypePageProperty, AreaIncludeProductsInSitemap, AreaSSLMode, AreaEcomPricesWithVat, AreaIsCdnActive, AreaCdnHost, AreaCdnImageHost, AreaStockLocationID, AreaReverseChargeForVat, AreaUniqueId, AreaNofollow, AreaNoindex, AreaPublished, AreaDeleted, AreaDeletedBy, AreaDeletedAt, AreaColorSchemeGroupId, AreaColorSchemeId, AreaTypographyId, AreaButtonStyleId

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>
