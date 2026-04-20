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
- [x] **Phase 25: Ecommerce Schema Sync** - Ensure EcomProductGroupField custom columns exist before data import (completed 2026-04-03)

### v0.5.0 Production-Ready Baseline (Planned)

**Milestone Goal:** Make the serializer safe to wire into a real Azure dev→test→QA→prod deployment pipeline. Today's `source-wins` model cannot coexist with live customer-edited content; today's fragility to schema drift, contamination, and env-specific credentials makes a prod rollout risky. This milestone closes those gaps.

**Source of findings:** Autonomous baseline test on Swift 2.2 → CleanDB round-trip, documented in `.planning/sessions/2026-04-17-baseline-test/FINDINGS.md` (F-01..F-19).

**CONTEXT reshape (2026-04-20):** Phase 37 was re-scoped after /gsd-discuss-phase produced 24 locked decisions. See `.planning/phases/37-production-ready-baseline/37-CONTEXT.md` and `DEFERRED.md` for the canonical view. Key shifts vs. original 2026-04-17 draft:
- SEED-01/SEED-02 land as a top-level Deploy/Seed config split, NOT a per-predicate `deserializeMode` enum (D-01)
- RUNTIME-COLS-01 and CRED-01 stop being two classification registries. RUNTIME-COLS-01 is a small flat curated exclusion list; CRED-01 is DEFERRED to v0.6.0 (D-07, D-09)
- DIFF-01 (BaselineDiffWriter) is DEFERRED to v0.6.0 (D-14)
- STRICT-01 defaults differ by entry point: API/CLI default ON, admin UI default OFF (D-16)
- TEMPLATE-01 is manifest-only; no template content in the baseline (D-19, D-20)
- LINK-02 runs TWO passes: serialize-time pre-commit sweep + deserialize-time SqlTable column resolution (D-22)

- [x] **Phase 37: Production-Ready Baseline** - Deploy/Seed config bifurcation, schema tolerance unification, SqlTable filtering with identifier whitelist, cache invalidation rework, strict mode, template manifest, cross-env link resolution. Re-planned 2026-04-20 from CONTEXT.md. (completed 2026-04-20)

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
- [x] 25-01-PLAN.md — EcomGroupFieldSchemaSync + orchestrator integration + tests

### Phase 37: Production-Ready Baseline
**Goal**: The serializer becomes safe to run in an automated Azure deployment pipeline without overwriting customer-edited content, leaking credentials, silently corrupting FK integrity, or breaking on env schema drift.
**Depends on**: v0.4.0 phases (Phase 23 page properties, Phase 24 area item types, Phase 25 schema sync)
**Findings addressed**: F-01, F-02, F-04..F-10, F-12, F-14, F-15, F-17, F-18 (see `.planning/sessions/2026-04-17-baseline-test/FINDINGS.md`). F-19 is deferred to v0.6.0.
**Seeds promoted**: SEED-001 (strict mode), SEED-002 (SQL identifier whitelist)
**CONTEXT**: `.planning/phases/37-production-ready-baseline/37-CONTEXT.md` (D-01..D-24 — source of truth on requirement shape)
**Deferred**: `.planning/phases/37-production-ready-baseline/DEFERRED.md` (CRED-01, DIFF-01)
**Requirements** (in scope — covered by plans):
  - `SEED-01`: Content predicates support per-subtree deserialize semantics — delivered as Deploy/Seed config split (D-01) in Plan 37-01
  - `SEED-02`: SqlTable predicates support per-predicate deserialize semantics — delivered as Deploy/Seed config split in Plan 37-01
  - `FILTER-01`: SqlTable predicates accept a `where` clause; column names validated against INFORMATION_SCHEMA (Plan 37-03)
  - `SCHEMA-02`: Schema tolerance unified into TargetSchemaCache helper; covers Area raw-SQL paths and SqlTable MERGE paths (Plan 37-02)
  - `CLEANUP-01`: Files-written manifest per mode; stale files deleted post-run (Plan 37-01)
  - `RUNTIME-COLS-01`: Small flat curated list of runtime-only columns auto-excluded (Plan 37-03)
  - `CACHE-01`: Curated DwCacheServiceRegistry with direct typed ClearCache actions; unresolved names = ERROR (Plan 37-04)
  - `STRICT-01`: `--strict` flag with entry-point-aware defaults (D-16); escalates all warnings (Plan 37-04)
  - `TEMPLATE-01`: Template-asset manifest (manifest-only per D-19); validated at deserialize; strict escalates missing (Plan 37-05)
  - `LINK-02`: Two passes — serialize-time sweep (D-22 pass 1) + deserialize-time SqlTable column resolution (D-22 pass 2) (Plan 37-05)
**Requirements DEFERRED to v0.6.0** (see DEFERRED.md):
  - `CRED-01`: Credential column registry (per D-07, D-09 — env-config workflow comes with v0.6.0)
  - `DIFF-01`: BaselineDiffWriter (per D-14 — observability aid, no correctness impact)
**Success Criteria** (what must be TRUE):
  1. A baseline serialized in Deploy mode overwrites target as before (preserves v0.4.x source-wins behavior); a baseline in Seed mode skips pages whose PageUniqueId is already on target (preserves customer edits)
  2. Swift 2.2 → baseline → CleanDB round-trip runs with zero ERROR lines from cache invalidation (F-10 closed); unknown cache names fail at config-load, not silently
  3. A config with SQL-injection-style identifiers (e.g. `"table": "X; DROP TABLE Y"`) is rejected at config-load with a clear error before any SQL runs (F-02, SEED-002)
  4. Default serialize of EcomShops excludes the env-specific search-index columns listed in RuntimeExcludes without the user listing them manually (F-06); CRED-01 is documented as NOT auto-excluded in v0.5.0
  5. Missing template files are surfaced pre-deserialize via the TemplateAssetManifest validation (F-15); strict mode escalates to hard failure
  6. Strict mode run on a baseline with any unresolved page link or missing template exits non-zero (SEED-001)
  7. Serialize fails with a pre-commit sweep error when the YAML tree contains Default.aspx?ID=N refs pointing outside the baseline (F-07, F-17 closed via D-22 pass 1)
  8. SqlTable predicates opt-in to link resolution via `resolveLinksInColumns`; UrlPath.UrlPathRedirect rewrites correctly source→target (D-22 pass 2)
**Plans:** 6/6 plans complete
Plans:
- [x] 37-01-PLAN.md — Deploy/Seed config split + files-written manifest cleanup (SEED-01, SEED-02, CLEANUP-01; F-01, F-04; D-01..D-06, D-10..D-12)
- [x] 37-01.1-PLAN.md — Per-mode Item Type + XML Type admin UI (follow-up to 37-01; closes D-02 for non-predicate config)
- [x] 37-02-PLAN.md — TargetSchemaCache unification (SCHEMA-02; F-12, F-14)
- [x] 37-03-PLAN.md — SqlTable `where` clause + SQL identifier whitelist + runtime-column auto-exclusion (FILTER-01, RUNTIME-COLS-01, SEED-002; F-02, F-06, F-07, F-08; D-07, D-08)
- [x] 37-04-PLAN.md — DwCacheServiceRegistry + Strict mode with entry-point-aware defaults (CACHE-01, STRICT-01, SEED-001; F-10; D-16, D-17, D-18)
- [x] 37-05-PLAN.md — Template asset manifest + LINK-02 two-pass cross-env link resolution (TEMPLATE-01, LINK-02; F-07, F-15, F-17; D-19..D-24)
- [ ] 37-06-PLAN.md — Gap closure for SC-3 / CR-01: wire default SqlIdentifierValidator into 1-arg ConfigLoader.Load (FILTER-01, SEED-002)

**Execution waves** (for /gsd-execute-phase):
- Wave 1: 37-01
- Wave 2: 37-01.1, 37-02, 37-03 (parallel — no file overlap between any pair)
- Wave 3: 37-04 (touches ContentDeserializer + orchestrator + config; depends on 37-01/37-02/37-03 ordering for shared files)
- Wave 4: 37-05 (touches ContentDeserializer + orchestrator + InternalLinkResolver; depends on all prior waves)
- Wave 5: 37-06 (gap closure — depends on 37-03/37-05; no overlap with other plans)

## Progress

**Execution Order:** Phases 23 -> 24 -> 25 -> 37

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
| 25. Ecommerce Schema Sync | v0.4.0 | 1/1 | Complete | 2026-04-03 |
| 37. Production-Ready Baseline | v0.5.0 | 6/6 | Complete   | 2026-04-20 |
