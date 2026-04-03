# Requirements: v0.4.0 Full Page Fidelity

**Defined:** 2026-04-03
**Core Value:** Developers can reliably move DynamicWeb database state between environments through source control, with serialized YAML files as the single source of truth.

## Page Properties

- [ ] **PAGE-01**: All ~30 missing page properties are serialized to YAML and deserialized back (NavigationTag, ShortCut, UrlName, MetaTitle, MetaDescription, MetaCanonical, Noindex, Nofollow, Robots404, SSLMode, PermissionType, UrlIgnoreForChildren, UrlDataProvider, ExactUrl, AllowClick, ShowInSitemap, AllowSearch, ShowInLegend, HideForPhones, HideForTablets, HideForDesktops, ActiveFrom, ActiveTo, DisplayMode, MasterPageId, MasterType, ContentType, ColorSchemeId, TopImage)
- [ ] **PAGE-02**: ShortCut field values containing Default.aspx?ID=NNN are resolved via InternalLinkResolver during deserialization

## Ecommerce Navigation

- [ ] **ECOM-01**: PageNavigationSettings (UseEcomGroups, ParentType, Groups, ShopId, MaxLevels, ProductPage, IncludeProducts, NavigationProvider) are serialized and deserialized
- [ ] **ECOM-02**: ProductPage field in NavigationSettings containing Default.aspx?ID=NNN is resolved via InternalLinkResolver

## Area Configuration

- [ ] **AREA-01**: Area-level ItemType fields (header/footer/master page connections) are serialized to YAML and deserialized back
- [ ] **AREA-02**: Page ID references in Area ItemType fields are resolved via InternalLinkResolver

## Schema Sync

- [ ] **SCHEMA-01**: EcomProductGroupField custom columns are created on EcomGroups table during deserialization before product group data is imported

## Future Requirements (deferred)

- Timestamp preservation (CreatedDate/UpdatedDate) — requires direct SQL, lower priority
- NavigationSettings.Groups ecommerce group ID portability — depends on ecommerce data also being serialized
- User ID portability for CreatedBy/UpdatedBy — environment-specific, document as limitation

## Out of Scope

- Backward compatibility with pre-v0.4.0 YAML format — beta, no external consumers
- Page workflow/approval fields — empty in Swift 2.2, add when needed
- Page versioning fields — empty in Swift 2.2, add when needed

## Traceability

| REQ-ID | Phase | Plan | Status |
|--------|-------|------|--------|
| PAGE-01 | Phase 23 | — | Pending |
| PAGE-02 | Phase 23 | — | Pending |
| ECOM-01 | Phase 23 | — | Pending |
| ECOM-02 | Phase 23 | — | Pending |
| AREA-01 | Phase 24 | — | Pending |
| AREA-02 | Phase 24 | — | Pending |
| SCHEMA-01 | Phase 25 | — | Pending |
