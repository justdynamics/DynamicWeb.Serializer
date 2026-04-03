# Roadmap: DynamicWeb.Serializer

## Milestones

- [x] **v1.0 MVP** - Phases 1-5 (shipped 2026-03-20) - [Archive](milestones/v1.0-ROADMAP.md)
- [x] **v1.1 Robustness** - Phase 6 (shipped 2026-03-20) - [Archive](milestones/v1.1-ROADMAP.md)
- [x] **v1.2 Admin UI** - Phases 7-10 (shipped 2026-03-22)
- [x] **v1.3 Permissions** - Phases 11-12 (shipped 2026-03-23)
- [x] **v2.0 DynamicWeb.Serializer** - Phases 13-18 (shipped 2026-03-24)
- [x] **v0.3.1 Internal Link Resolution** - Phases 19-22 (shipped 2026-04-03)
- [ ] **v0.4.0 Full Page Fidelity** - Phases 23-25 (in progress)

## Phases

<details>
<summary>v1.0 MVP (Phases 1-5) - SHIPPED 2026-03-20</summary>

- [x] Phase 1: Foundation (2/2 plans) - completed 2026-03-19
- [x] Phase 2: Configuration (1/1 plans) - completed 2026-03-19
- [x] Phase 3: Serialization (3/3 plans) - completed 2026-03-19
- [x] Phase 4: Deserialization (2/2 plans) - completed 2026-03-19
- [x] Phase 5: Integration (2/2 plans) - completed 2026-03-19

</details>

<details>
<summary>v1.1 Robustness (Phase 6) - SHIPPED 2026-03-20</summary>

- [x] Phase 6: Sync Robustness (2/2 plans) - completed 2026-03-20

</details>

<details>
<summary>v1.2 Admin UI (Phases 7-10) - SHIPPED 2026-03-22</summary>

- [x] Phase 7: Config Infrastructure + Settings Tree Node (2/2 plans) - completed
- [x] Phase 8: Settings Screen (1/1 plans) - completed
- [x] Phase 9: Predicate Management (2/2 plans) - completed
- [x] Phase 10: Context Menu Actions (3/3 plans) - completed 2026-03-22

</details>

<details>
<summary>v1.3 Permissions (Phases 11-12) - SHIPPED 2026-03-23</summary>

- [x] Phase 11: Permission Serialization (1/1 plans) - completed 2026-03-22
- [x] Phase 12: Permission Deserialization + Docs (2/2 plans) - completed 2026-03-23

</details>

<details>
<summary>v2.0 DynamicWeb.Serializer (Phases 13-18) - SHIPPED 2026-03-24</summary>

- [x] Phase 13: Provider Foundation + SqlTableProvider Proof (3/3 plans) - completed 2026-03-23
- [x] Phase 14: Content Migration + Orchestrator (2/2 plans) - completed 2026-03-24
- [x] Phase 15: Ecommerce Tables at Scale (2/2 plans) - completed 2026-03-24
- [x] Phase 16: Admin UX + Rename (5/5 plans) - completed 2026-03-24
- [x] Phase 17: Project Rename - Absorbed into Phase 16
- [x] Phase 18: Predicate Config Multi-Provider (2/2 plans) - completed 2026-03-24

</details>

<details>
<summary>v0.3.1 Internal Link Resolution (Phases 19-22) - SHIPPED 2026-04-03</summary>

- [x] Phase 19: Source ID Serialization (1/1 plans) - completed 2026-04-03
- [x] Phase 20: Link Resolution Core (2/2 plans) - completed 2026-04-03
- [x] Phase 21: Paragraph Anchor Resolution (1/1 plans) - completed 2026-04-03
- [x] Phase 22: Version Housekeeping (1/1 plans) - completed 2026-04-03

</details>

### v0.4.0 Full Page Fidelity (In Progress)

**Milestone Goal:** Serialize and deserialize ALL page-level settings, area ItemType connections, and ecommerce navigation configuration so that deserialized pages are functionally identical to the source.

- [x] **Phase 23: Full Page Properties + Navigation Settings** - Extend SerializedPage with all ~30 missing properties and PageNavigationSettings, with link resolution for ShortCut and ProductPage (completed 2026-04-03)
- [x] **Phase 24: Area ItemType Fields** - Serialize and deserialize Area-level ItemType connections with page ID resolution (completed 2026-04-03)
- [ ] **Phase 25: Ecommerce Schema Sync** - Ensure EcomProductGroupField custom columns exist before data import

## Phase Details

### Phase 23: Full Page Properties + Navigation Settings
**Goal**: Deserialized pages carry every page-level setting (SEO, visibility, URL, navigation, SSL, permissions, display) and ecommerce navigation configuration, with internal links resolved to target IDs
**Depends on**: Phase 22 (current codebase with InternalLinkResolver)
**Requirements**: PAGE-01, PAGE-02, ECOM-01, ECOM-02
**Success Criteria** (what must be TRUE):
  1. A page serialized from source environment produces YAML containing all ~30 properties (NavigationTag, ShortCut, UrlName, MetaTitle, MetaDescription, SSLMode, PermissionType, visibility flags, etc.) with correct values
  2. Deserializing that YAML into a clean target environment creates a page with all property values matching the source (verified by comparing DB column values)
  3. PageNavigationSettings (UseEcomGroups, ParentType, ShopId, MaxLevels, ProductPage, IncludeProducts, NavigationProvider) round-trips correctly through serialize/deserialize
  4. ShortCut values containing Default.aspx?ID=NNN are rewritten to the correct target page ID during deserialization
  5. ProductPage values in NavigationSettings containing Default.aspx?ID=NNN are rewritten to the correct target page ID during deserialization
**Plans:** 2/2 plans complete
Plans:
- [x] 23-01-PLAN.md — DTO models + sub-records + ContentMapper extension + tests
- [x] 23-02-PLAN.md — ContentDeserializer extension + ShortCut/ProductPage link resolution

### Phase 24: Area ItemType Fields
**Goal**: Area-level ItemType connections (header, footer, master page) are preserved through serialize/deserialize with page references resolved to target environment IDs
**Depends on**: Phase 23 (extended content pipeline)
**Requirements**: AREA-01, AREA-02
**Success Criteria** (what must be TRUE):
  1. SerializedArea YAML includes ItemType name and all ItemType field values for each area
  2. Deserializing an area restores its ItemType connection and field values, so the area's header/footer/master page configuration matches the source
  3. Page ID references within Area ItemType field values (e.g., header page link) are resolved via InternalLinkResolver to correct target IDs
**Plans:** 1/1 plans complete
Plans:
- [x] 24-01-PLAN.md — SerializedArea DTO + mapper + deserializer + link resolution

### Phase 25: Ecommerce Schema Sync
**Goal**: EcomProductGroupField custom columns are guaranteed to exist on the EcomGroups table before any product group data is deserialized, preventing column-not-found errors
**Depends on**: Phase 23 (navigation settings context)
**Requirements**: SCHEMA-01
**Success Criteria** (what must be TRUE):
  1. During deserialization, EcomProductGroupField definitions are processed and UpdateTable() is called before any EcomGroups row data is inserted
  2. Custom columns created by EcomProductGroupField are present on the EcomGroups table after deserialization (verified by querying table schema)
**Plans:** 1 plan
Plans:
- [ ] 25-01-PLAN.md — EcomGroupFieldSchemaSync + orchestrator integration + tests

## Progress

**Execution Order:** Phases 23 -> 24 -> 25

| Phase | Milestone | Plans Complete | Status | Completed |
|-------|-----------|----------------|--------|-----------|
| 1. Foundation | v1.0 | 2/2 | Complete | 2026-03-19 |
| 2. Configuration | v1.0 | 1/1 | Complete | 2026-03-19 |
| 3. Serialization | v1.0 | 3/3 | Complete | 2026-03-19 |
| 4. Deserialization | v1.0 | 2/2 | Complete | 2026-03-19 |
| 5. Integration | v1.0 | 2/2 | Complete | 2026-03-19 |
| 6. Sync Robustness | v1.1 | 2/2 | Complete | 2026-03-20 |
| 7. Config Infrastructure | v1.2 | 2/2 | Complete | 2026-03-22 |
| 8. Settings Screen | v1.2 | 1/1 | Complete | 2026-03-22 |
| 9. Predicate Management | v1.2 | 2/2 | Complete | 2026-03-22 |
| 10. Context Menu Actions | v1.2 | 3/3 | Complete | 2026-03-22 |
| 11. Permission Serialization | v1.3 | 1/1 | Complete | 2026-03-22 |
| 12. Permission Deserialization + Docs | v1.3 | 2/2 | Complete | 2026-03-23 |
| 13. Provider Foundation + SqlTableProvider Proof | v2.0 | 3/3 | Complete | 2026-03-23 |
| 14. Content Migration + Orchestrator | v2.0 | 2/2 | Complete | 2026-03-24 |
| 15. Ecommerce Tables at Scale | v2.0 | 2/2 | Complete | 2026-03-24 |
| 16. Admin UX + Rename | v2.0 | 5/5 | Complete | 2026-03-24 |
| 17. Project Rename | v2.0 | N/A | Absorbed into P16 | - |
| 18. Predicate Config Multi-Provider | v2.0 | 2/2 | Complete | 2026-03-24 |
| 19. Source ID Serialization | v0.3.1 | 1/1 | Complete | 2026-04-03 |
| 20. Link Resolution Core | v0.3.1 | 2/2 | Complete | 2026-04-03 |
| 21. Paragraph Anchor Resolution | v0.3.1 | 1/1 | Complete | 2026-04-03 |
| 22. Version Housekeeping | v0.3.1 | 1/1 | Complete | 2026-04-03 |
| 23. Full Page Properties + Navigation Settings | v0.4.0 | 2/2 | Complete   | 2026-04-03 |
| 24. Area ItemType Fields | v0.4.0 | 1/1 | Complete   | 2026-04-03 |
| 25. Ecommerce Schema Sync | v0.4.0 | 0/1 | Not started | - |
