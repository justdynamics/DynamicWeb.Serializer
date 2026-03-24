# Roadmap: DynamicWeb.Serializer

## Milestones

- [x] **v1.0 MVP** - Phases 1-5 (shipped 2026-03-20) - [Archive](milestones/v1.0-ROADMAP.md)
- [x] **v1.1 Robustness** - Phase 6 (shipped 2026-03-20) - [Archive](milestones/v1.1-ROADMAP.md)
- [x] **v1.2 Admin UI** - Phases 7-10 (shipped 2026-03-22)
- [x] **v1.3 Permissions** - Phases 11-12 (shipped 2026-03-23)
- [ ] **v2.0 DynamicWeb.Serializer** - Phases 13-17 (in progress)

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

### v2.0 DynamicWeb.Serializer (In Progress)

**Milestone Goal:** Broaden from content-only sync to full database serialization with a pluggable provider architecture, starting with ecommerce settings tables, admin UX improvements, and project rename.

- [x] **Phase 13: Provider Foundation + SqlTableProvider Proof** - ISerializationProvider interface, provider registry, and SqlTableProvider proven on EcomOrderFlow round-trip (completed 2026-03-23)
- [x] **Phase 14: Content Migration + Orchestrator** - ContentProvider adapter wraps existing serializers, orchestrator routes predicates by data type (completed 2026-03-24)
- [x] **Phase 15: Ecommerce Tables at Scale** - All ~15 ecommerce settings tables with FK ordering, cache invalidation, and duplicate DataItemType handling (completed 2026-03-24)
- [ ] **Phase 16: Admin UX** - Log viewer, asset management deserialize action, menu relocation, scheduled task deprecation
- [ ] **Phase 17: Project Rename** - Rename from Dynamicweb.ContentSync to DynamicWeb.Serializer (namespace, assembly, NuGet package)

## Phase Details

### Phase 13: Provider Foundation + SqlTableProvider Proof
**Goal**: A pluggable provider architecture exists and SqlTableProvider can round-trip a single SQL table (EcomOrderFlow) to YAML and back
**Depends on**: Phase 12 (v1.3 codebase)
**Requirements**: PROV-01, PROV-02, SQL-01, SQL-02, SQL-04, SQL-05
**Success Criteria** (what must be TRUE):
  1. ISerializationProvider interface defines Serialize/Deserialize/DryRun contract and a new provider can be registered by implementing it
  2. Provider registry resolves the correct provider instance given a data type string (e.g., "SqlTable" returns SqlTableProvider)
  3. SqlTableProvider reads DataGroup XML metadata (Table, NameColumn, CompareColumns) and serializes all rows of EcomOrderFlow to individual YAML files
  4. SqlTableProvider deserializes YAML files back into EcomOrderFlow table, matching rows by NameColumn with CompareColumns fallback, reporting rows added/updated/skipped per table
  5. Source-wins strategy applies: YAML rows overwrite matched target rows on deserialize
**Plans**: 3 plans
Plans:
- [x] 13-01-PLAN.md — Provider interface, registry, result types, ISqlExecutor abstraction
- [x] 13-02-PLAN.md — SqlTableProvider serialization (DataGroup metadata, SQL reading, YAML writing)
- [x] 13-03-PLAN.md — SqlTableProvider deserialization (MERGE upsert, source-wins, dry-run)

### Phase 14: Content Migration + Orchestrator
**Goal**: Existing content serialization works unchanged through the new provider architecture, and the orchestrator can dispatch to multiple providers based on predicate configuration
**Depends on**: Phase 13
**Requirements**: PROV-03, PROV-04, CACHE-02, CACHE-03
**Success Criteria** (what must be TRUE):
  1. ContentProvider adapter wraps ContentSerializer/ContentDeserializer without any changes to their internal logic, and all existing content round-trips produce identical YAML output
  2. Orchestrator iterates configured predicates, dispatches each to the correct provider based on its DataType field, and aggregates results
  3. Predicates in the config file accept a DataType field that routes to the appropriate provider (e.g., "Content" or "SqlTable")
  4. Existing v1.x config files without a DataType field default to "Content" and work without modification
**Plans**: 2 plans
Plans:
- [x] 14-01-PLAN.md — ConfigLoader migration to ProviderPredicateDefinition + ContentProvider adapter
- [x] 14-02-PLAN.md — SerializerOrchestrator + unified commands + scheduled task updates

### Phase 15: Ecommerce Tables at Scale
**Goal**: All ecommerce settings tables (~26) serialize and deserialize reliably with correct FK ordering, cache invalidation, and no duplicate rows from shared DataItemTypes
**Depends on**: Phase 13 (SqlTableProvider), Phase 14 (orchestrator)
**Requirements**: ECOM-01, ECOM-02, ECOM-03, ECOM-04, SQL-03, CACHE-01
**Success Criteria** (what must be TRUE):
  1. OrderFlows, OrderStates, and their relationship tables serialize to YAML and deserialize back without FK constraint violations
  2. Payment and Shipping methods serialize and deserialize, including shared junction tables (e.g., EcomMethodCountryRelation) without creating duplicate rows
  3. Countries, Currencies, and VAT settings round-trip through YAML correctly
  4. After deserialization, DW admin UI reflects the new ecommerce data without requiring an application restart (service caches invalidated)
  5. Tables are deserialized in FK dependency order determined by topological sort of sys.foreign_keys
**Plans**: 2 plans
Plans:
- [x] 15-01-PLAN.md — FkDependencyResolver + CacheInvalidator + ServiceCaches config field
- [x] 15-02-PLAN.md — Orchestrator FK/cache integration + ecommerce predicate config documentation

### Phase 16: Admin UX
**Goal**: Users have a log viewer with guided advice, can deserialize from asset management, find the settings screen at its new location, and scheduled tasks are deprecated
**Depends on**: Phase 14 (orchestrator produces structured logs), Phase 15 (ecommerce providers generate log data)
**Requirements**: UX-01, UX-02, UX-03, UX-04
**Success Criteria** (what must be TRUE):
  1. Log viewer screen shows per-provider summaries (rows added/updated/skipped/failed) with actionable advice (e.g., "Create missing groups: X, Y")
  2. A "Deserialize" action appears on zip files in the DW asset management file detail page
  3. Admin tree node is located at Settings > Database > Serialize (moved from Settings > Content > Sync)
  4. Scheduled tasks are deprecated with a logged warning directing users to API commands as the replacement
**Plans**: TBD

### Phase 17: Project Rename
**Goal**: The project identity is updated from Dynamicweb.ContentSync to DynamicWeb.Serializer across all artifacts
**Depends on**: Phase 16 (all feature work complete before rename)
**Requirements**: REN-01
**Success Criteria** (what must be TRUE):
  1. Root namespace is DynamicWeb.Serializer and the assembly/NuGet package is named DynamicWeb.Serializer
  2. Config file path, log file path, and all user-facing strings reference "Serializer" not "ContentSync"
  3. The built DLL loads and functions in a DynamicWeb 10.x instance under the new name
**Plans**: TBD

## Progress

**Execution Order:** Phases 13 -> 14 -> 15 -> 16 -> 17

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
| 13. Provider Foundation + SqlTableProvider Proof | v2.0 | 3/3 | Complete    | 2026-03-23 |
| 14. Content Migration + Orchestrator | v2.0 | 2/2 | Complete    | 2026-03-24 |
| 15. Ecommerce Tables at Scale | v2.0 | 2/2 | Complete    | 2026-03-24 |
| 16. Admin UX | v2.0 | 0/0 | Not started | - |
| 17. Project Rename | v2.0 | 0/0 | Not started | - |
