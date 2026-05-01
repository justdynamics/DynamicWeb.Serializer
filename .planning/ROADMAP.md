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

- [x] **Phase 37: Production-Ready Baseline** - Deploy/Seed config bifurcation, schema tolerance unification, SqlTable filtering with identifier whitelist, cache invalidation rework, strict mode, template manifest, cross-env link resolution. Re-planned 2026-04-20 from CONTEXT.md.
 (completed 2026-04-20)

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
**Plans:** 7/7 plans complete
Plans:
- [x] 37-01-PLAN.md — Deploy/Seed config split + files-written manifest cleanup (SEED-01, SEED-02, CLEANUP-01; F-01, F-04; D-01..D-06, D-10..D-12)
- [x] 37-01.1-PLAN.md — Per-mode Item Type + XML Type admin UI (follow-up to 37-01; closes D-02 for non-predicate config)
- [x] 37-02-PLAN.md — TargetSchemaCache unification (SCHEMA-02; F-12, F-14)
- [x] 37-03-PLAN.md — SqlTable `where` clause + SQL identifier whitelist + runtime-column auto-exclusion (FILTER-01, RUNTIME-COLS-01, SEED-002; F-02, F-06, F-07, F-08; D-07, D-08)
- [x] 37-04-PLAN.md — DwCacheServiceRegistry + Strict mode with entry-point-aware defaults (CACHE-01, STRICT-01, SEED-001; F-10; D-16, D-17, D-18)
- [x] 37-05-PLAN.md — Template asset manifest + LINK-02 two-pass cross-env link resolution (TEMPLATE-01, LINK-02; F-07, F-15, F-17; D-19..D-24)
- [x] 37-06-PLAN.md — Gap closure for SC-3 / CR-01: wire default SqlIdentifierValidator into 1-arg ConfigLoader.Load (FILTER-01, SEED-002)

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
| 37. Production-Ready Baseline | v0.5.0 | 7/7 | Complete   | 2026-04-20 |

### Phase 38: Production-Ready Baseline Hardening

**Goal:** Close everything Phase 37's autonomous E2E round-trip surfaced but didn't fix. After Phase 38, the Swift 2.2 → CleanDB round-trip runs with `strictMode: true` (the intended production default) without warnings-turned-failures, all follow-up code changes from 37 have proper test coverage, the one known silent data-loss path (EcomProducts 2051 → 582) is either fixed or explicitly documented, and customers have a written "what's NOT in the baseline" reference for per-env config.

**Depends on:** Phase 37 (Production-Ready Baseline)
**Source of findings:** `.planning/sessions/2026-04-20-e2e-baseline-roundtrip/REPORT.md` — every item below maps to a concrete finding in the E2E session.

**Requirements / grouped backlog** (user-approved scope 2026-04-21, groups A+B+C+D+E):

**Group A — Lock in the 37 follow-up code changes** (shipped inline during E2E without proper test coverage)
- `A.1`: TDD unit tests for `AcknowledgedOrphanPageIds` — malicious-ID rejection, acknowledged-ID warning path, strict-still-fatal-for-unlisted path
- `A.2`: TDD integration test for `IDENTITY_INSERT` wrapping on Area create (real target DB with identity column)
- `A.3`: Consolidate `AcknowledgedOrphanPageIds` to ONE source-of-truth (currently duplicated on both `ProviderPredicateDefinition` and `ModeConfig`); remove the unused one

**Group B — Close the strict-mode data/template gaps** (the 7 warnings that forced `strictMode: false` for the E2E deploy)
- `B.1`: Missing grid-row templates `1ColumnEmail`, `2ColumnsEmail` — investigate whether these ship with the Swift nupkg on CleanDB or need TEMPLATE-01 scope expansion to capture email templates
- `B.2`: Missing page-layout template `Swift-v2_PageNoLayout.cshtml` — same investigation as B.1
- `B.3`: 3 schema-drift Area columns (`AreaHtmlType`, `AreaLayoutPhone`, `AreaLayoutTablet`) on CleanDB — determine whether CleanDB is on an older DW version (remediable) or whether Swift 2.2 adds columns DW core doesn't ship (permanent drift, document as such)
- `B.4`: FK re-enable warning on `EcomShopGroupRelation → EcomShops.ShopId` during deserialize — determine whether it fires in production deploys or only after an aggressive purge
- `B.5`: BaselineLinkSweeper false-positive on paragraph anchors. Pattern `Default.aspx?ID=4897#15717` treats `15717` as a separate page ID even though it's a paragraph anchor. Fix: either skip the anchor portion in the sweep, or validate the anchor against serialized paragraph SourceIds. Discovered 2026-04-21 after Swift 2.2 data cleanup reduced orphans to exactly this 1 false-positive.

**Group C — Investigate real data loss** (silent, worst-case)
- `C.1`: EcomProducts serialize emitted 582 rows, Swift 2.2 has 2051. Diagnose where 1469 products vanished — candidate theories: empty-name products filtered silently, duplicate-NameColumn collision causing file overwrites, `SkipOnUnchanged` bug in the checksums lookup

**Group D — API/UX polish**
- `D.1`: `?mode=seed` query-param binding on `SerializerSerialize`/`SerializerDeserialize` API — doc comments say it works; in practice only the JSON body `{"Mode":"seed"}` binds
- `D.2`: HTTP status code bug — serialize returns HTTP 400 even when 0 errors because the trailing `Errors: ` string is always present in the message
- `D.3`: New `SerializerSmoke` admin command / CLI — hit every active page post-deserialize, report 2xx/3xx/4xx/5xx bucket counts + excerpt any 5xx HTML body. Turns the E2E smoke into a first-class repeatable tool

**Group E — Docs**
- `E.1`: Update `docs/baselines/Swift2.2-baseline.md` — add "Pre-existing source-data bugs caught by Phase 37 validators" section (the 3 column-name fixes: VatName→VatGroupName, ShopAutoId on EcomPayments, ShippingParameters→ShippingServiceParameters, and the 5 orphan page IDs)
- `E.2`: Add `docs/baselines/env-bucket.md` — per-env config reference: `/Files/GlobalSettings.config` (including Friendly URL `/en-us/` routing), Azure Key Vault secrets, AreaDomain/CDN bindings — what's NOT in the baseline and why

**Success Criteria** (what must be TRUE at end of phase):
  1. The Swift 2.2 → CleanDB round-trip runs with `strictMode: true` (config default) without a single escalated warning (B.1–B.4 closed or explicitly acknowledged via a sanctioned bypass)
  2. `AcknowledgedOrphanPageIds` lives in exactly ONE place in the model layer, has 3+ unit tests proving the malicious/acknowledged/unlisted cases, and has one threat-model entry in PLAN.md
  3. Area IDENTITY_INSERT has a unit test that fails if the wrapping is removed
  4. EcomProducts round-trip preserves ALL rows from Swift 2.2 to CleanDB, OR the filtering is explicitly documented with a test proving the filter criterion
  5. `POST /Admin/Api/SerializerSerialize?mode=seed` works identically to the JSON-body variant
  6. HTTP 400 is never returned on a fully-successful serialize/deserialize
  7. A new smoke tool exits 0 when the CleanDB frontend serves all expected pages as 2xx/3xx, non-zero with a report on any 5xx
  8. Swift2.2-baseline.md has a "known pre-existing source-data bugs" section; env-bucket.md explains Friendly URL + GlobalSettings.config + secrets are per-env infra

**Plans:** 5/5 plans complete

Plans:
- [x] 38-01-PLAN.md — Wave 1 quick wins: D.1 query-param binding, D.2 HTTP 400 bug, E.1 baseline doc extension, E.2 env-bucket.md (D.1, D.2, E.1, E.2)
- [x] 38-02-PLAN.md — Wave 2 retroactive tests + consolidation: A.3 AcknowledgedOrphanPageIds consolidation → A.1 TDD tests → A.2 ISqlExecutor seam + IDENTITY_INSERT test → B.5 paragraph-anchor sweep fix (A.1, A.2, A.3, B.5)
- [x] 38-03-PLAN.md — Wave 3 investigations + data-loss fix: C.1 FlatFileStore monotonic-counter dedup, B.1/B.2 SQL cleanup for 3 orphan templates, B.3 schema-drift investigation, B.4 FK re-enable investigation, live E2E gate (B.1, B.2, B.3, B.4, C.1)
- [x] 38-04-PLAN.md — Wave 4 smoke tool: tools/smoke/Test-BaselineFrontend.ps1 + README.md (D.3)
- [x] 38-05-PLAN.md — Wave 5 final (manual sign-off): restore strictMode default ON + remove acknowledgedOrphanPageIds workaround + final live E2E (D-38-16)

**Execution waves** (for /gsd-execute-phase):
- Wave 1: 38-01
- Wave 2: 38-02 (no file overlap with Wave 1; independent)
- Wave 3: 38-03 (depends_on: [02] — needs A.3 post-state for serialize verification)
- Wave 4: 38-04 (depends_on: [03] — exercises post-deserialize CleanDB)
- Wave 5: 38-05 (depends_on: [01, 02, 03, 04] — gating final E2E + D-38-16 config flip)

### Phase 38.1: Close Phase 38 deferrals (INSERTED)

**Goal:** Close the four items Phase 38 deferred so the live Swift 2.2 → CleanDB round-trip can pass under `strictMode: true` end-to-end. Phase 38's D-38-16 code change restored the strict-mode default in config, but the final E2E surfaced four source-data + sweeper-scope blockers that were explicitly held back per the gated-closed-on-38.1 disposition.

**Depends on:** Phase 38 (Production-Ready Baseline Hardening)
**Source of findings:** `.planning/phases/38-production-ready-baseline-hardening-retroactive-tests-for-37/38-05-e2e-results.md` + `38-05-SUMMARY.md` + `deferred-items.md`.

**Backlog (4 items):**

- `B.5.1`: Extend `BaselineLinkSweeper.SelectedValuePattern` loop (~line 167) to validate `SelectedValue` paragraph IDs against the existing `validParagraphIds` HashSet (already built by B.5 in Phase 38-02). Without this, the `ButtonEditor` JSON with `"SelectedValue": "15717"` fails strict-mode serialize even after Phase 38's B.5 page-anchor fix. Estimated ~10 LOC + 1 unit test.
- `B.4.1`: Ship `tools/swift22-cleanup/06-delete-orphan-ecomshopgrouprelation.sql` to remove the `SHOP19` orphan row in `[EcomShopGroupRelation]` that references a non-existent shop. Closes the B.4 FK re-enable escalation identified in `38-03-b4-investigation.md`.
- `B.3.1`: Extend B.3 scope — the final E2E surfaced 7 Area schema-drift columns, not just the 3 originally named. Either widen `knownEnvSchemaDrift` coverage or document additional columns in `env-bucket.md` per outcome-A precedent.
- `GRID-01`: Resolve the 142 `GridRowDefinitionId` NOT NULL deserialize failures introduced by the B.1/B.2 template cleanup. Choose between a new source-cleanup script (`tools/swift22-cleanup/07-delete-stale-email-gridrows.sql`) OR a serializer-side empty-string coalesce in the GridRow write path.

**Success Criteria:**
  1. Final live Swift 2.2 → CleanDB round-trip under `strictMode: true` returns HTTP 200 on all 4 API calls (serialize deploy/seed, deserialize deploy/seed)
  2. EcomProducts preservation still holds (source == target count)
  3. Zero escalated warnings for the 4 deferred items (B.5.1, B.4.1, B.3.1, GRID-01)
  4. Optional: extract shared paragraph-ID collector helper between `BaselineLinkSweeper` and `InternalLinkResolver` (carried from checker warning W6)

**Plans:** 5/5 plans complete

Plans:
- [x] 38.1-01-PLAN.md — Single-plan 6-task sequence: B.5.1 SelectedValue sweeper → B.4.1 SHOP19 cleanup SQL → B.3.1 env-bucket.md drift columns → GRID-01 stale GridRow SQL → W6 ParagraphIdCollector extraction → NOT-SERIALIZED rename + live E2E (closed 4 named deferrals; open-with-gap on wider 57-escalation surface — see 38.1-VERIFICATION.md)
- [x] 38.1-02-PLAN.md — Gap closure Wave 1a: investigation of 20 orphan page IDs + scripts 08 (null-orphan-page-link-refs) and 09 (fix-misconfigured-property-pages for PageID 88/103) + README rows 08 & 09
- [x] 38.1-03-PLAN.md — Gap closure Wave 1b (parallel with 02): cleandb-align-schema.sql (10 idempotent ALTER) + tools/e2e/full-clean-roundtrip.ps1 unattended pipeline + tools/e2e/README.md
- [x] 38.1-04-PLAN.md — Gap closure Wave 2: run full pipeline end-to-end + capture 38.1-02-e2e-results.md with disposition CLOSED
- [x] 38.1-05-PLAN.md — Gap closure Wave 3: docs/findings/swift22-cleanup-overview.md (email-able full cleanup surface inventory)

**Gap-closure success criteria (appends to the above 4 when Plans 02-05 complete):**
  1. tools/e2e/full-clean-roundtrip.ps1 runs unattended end-to-end against a freshly-restored Swift-2.2 bacpac and exits 0
  2. HTTP 200 on all 4 API calls (serialize deploy/seed, deserialize deploy/seed) under strictMode: true
  3. EcomProducts preservation 2051 → 2051
  4. Zero orphan-ID escalations (20 IDs: 1, 2, 4, 16, 19, 21, 23, 33, 34, 37, 40, 41, 42, 44, 48, 60, 97, 98, 104, 113) in any API call
  5. Zero "Could not load PropertyItem" warnings for PageID 88 + 103 (misconfigured PropertyItem refs cleared by script 09)
  6. Smoke tool exits 0 AND exercises at least one page (non-vacuous)
  7. Disposition CLOSED captured in 38.1-02-e2e-results.md
  8. docs/findings/swift22-cleanup-overview.md delivered as single-document inventory of the full cleanup surface

**Execution waves** (for /gsd-execute-phase --gaps-only):
- Wave 1: 38.1-02 (investigation + scripts 08/09), 38.1-03 (cleandb-align-schema + pipeline PS) — parallel (zero file overlap)
- Wave 2: 38.1-04 (pipeline run + e2e-results capture) — depends on 02 and 03
- Wave 3: 38.1-05 (overview finding doc) — depends on 02 and 04

### Phase 39: Seed mode field-level merge — Deploy/Seed split intent is field-level merge, not row-level skip. Fix ContentDeserializer (currently skips whole pages by PageUniqueId match on DestinationWins) and SqlTableProvider (currently skips rows on RowExistsInTarget match) to merge per-field: on an existing row/page, write only fields NOT already set on target (i.e. NULL / default / empty). Deploy YAML (source-wins) + Seed YAML (destination-wins, empty exclusions) must combine cleanly on both fresh and re-deploy targets: Deploy writes its field subset, Seed fills remaining fields once, customer tweaks survive re-deploys. Acceptance: on a target where Deploy has already run, running Seed populates the excluded fields (Mail1SenderEmail, error strings, branding) without overwriting any field already set. Scope: both providers, plus XML-element merge layer for SQL-column-hosted XML payloads (EcomPayments.PaymentGatewayParameters, EcomShippings.ShippingServiceParameters) since Mail1SenderEmail lives as an XML leaf inside those columns; adjust tests that assert row-level skip.

**Goal:** Convert Seed mode (ConflictStrategy.DestinationWins) in ContentDeserializer and SqlTableProvider from whole-entity skip to per-field merge, including XML-element merge for SQL-hosted XML payloads, so Deploy + Seed YAML combine cleanly on both fresh and re-deploy targets. Customer tweaks between Deploy and Seed survive intrinsically (no persisted marker); Deploy-excluded fields (including XML leaves like Mail1SenderEmail) fill on Seed exactly once.
**Requirements**: D-01..D-27 from 39-CONTEXT.md (phase has no legacy REQ-IDs; CONTEXT decisions are acceptance)
**Supersedes**: Phase 37 §D-06 (row-level skip semantics for DestinationWins)
**Depends on:** Phase 38
**Success Criteria** (what must be TRUE at end of phase):
  1. MergePredicate.IsUnsetForMerge helper exists in Infrastructure/ with full type-matrix unit coverage (D-01, D-08)
  2. ContentDeserializer.DeserializePage Seed-skip block at lines ~684-692 is replaced with per-field merge branch; old `Seed-skip:` log line no longer emitted (D-11)
  3. SqlTableWriter.UpdateColumnSubset exists as a virtual method with parameterized narrowed-UPDATE SQL (D-17)
  4. SqlTableProvider.DeserializeCoreLogic Seed-skip block at lines ~313-322 is replaced with column + XML merge branch; checksum fast-path at ~304-311 still runs first (D-18)
  5. XmlMergeHelper in Infrastructure/ merges per XML leaf — fills elements absent or empty on target, preserves target-only elements, hardened against DTD/billion-laughs (D-22..D-25, D-27)
  6. EcomPayments.PaymentGatewayParameters and EcomShippings.ShippingServiceParameters participate in XML-element merge (D-21)
  7. Permissions NEVER touched on Seed UPDATE path (D-06)
  8. Sub-object DTOs (Seo, UrlSettings, Visibility, NavigationSettings) merge per-property not per-sub-object (D-04)
  9. GridRow and Paragraph UPDATE paths inherit the merge predicate (D-07)
  10. tools/e2e/full-clean-roundtrip.ps1 has a DeployThenTweakThenSeed mode that exits 0 against live Swift 2.2 + CleanDB with all six D-15 assertions passing (Mail1SenderEmail fills, customer tweak preserved, D-09 idempotency)
  11. Full test suite remains green throughout — no regression on existing source-wins paths
**Plans:** 3/3 plans complete

Plans:
- [x] 39-01-PLAN.md — Content merge: shared MergePredicate helper + ContentDeserializer DeserializePage/GridRow/Paragraph UPDATE retrofit + MergePredicateTests + ContentDeserializerSeedMergeTests (D-01..D-11, D-13, D-14, D-16, D-19, D-20)
- [x] 39-02-PLAN.md — SqlTable merge (column + XML-element): SqlTableWriter.UpdateColumnSubset + SqlTableProvider merge branch with existingRowsByIdentity extension + XmlMergeHelper + 4 new test files covering unit + integration + EcomPayments/EcomShippings round-trip (D-01, D-08, D-10..D-19, D-21..D-27)
- [x] 39-03-PLAN.md — E2E gate: tools/e2e/full-clean-roundtrip.ps1 DeployThenTweakThenSeed mode + Invoke-Sqlcmd-StringScalar helper + README.md update; live acceptance (D-15, D-09, D-11, D-19, D-26 verified against running hosts)

**Execution waves** (for /gsd-execute-phase):
- Wave 1: 39-01 (no deps — ships MergePredicate contract consumed by 39-02)
- Wave 2: 39-02 (depends_on: [01] — consumes MergePredicate + adds XmlMergeHelper + SqlTable branch)
- Wave 3: 39-03 (depends_on: [01, 02] — live E2E gate; includes blocking checkpoint for operator-run pipeline verification)

### Phase 40: Per-predicate Deploy/Seed split — replace section-level config arrays with single predicate list carrying per-item Mode field

**Goal:** Replace the section-level deploy.predicates / seed.predicates arrays with a single flat predicates list where each predicate carries its own mode field (Deploy or Seed). Mode-shared keys move to top-level (D-04). No backcompat — ConfigLoader rejects the legacy shape with a clear error (D-03). swift2.2-combined.json rewritten in the new shape (D-05). Admin UI tree collapses to a single Predicates subtree with a Mode badge per row (D-06). Phase 39 runtime (MergePredicate, XmlMergeHelper, ContentDeserializer/SqlTableProvider Seed-merge) is unaffected — only config schema + readers/writers + admin UI + baseline + docs.
**Requirements**: D-01..D-08 from orchestrator brief (no legacy REQ-IDs; brief decisions are acceptance)
**Depends on:** Phase 39
**Plans:** 5/5 plans complete

Plans:
- [x] 40-01-PLAN.md — Model + ConfigLoader/Writer flat shape with strict legacy rejection (D-01..D-04, D-08)
- [x] 40-02-PLAN.md — Runtime/orchestrator/provider routing rewrite (ContentSerializer, ContentProvider, SerializeSubtreeCommand, SerializerSerializeCommand, SerializerDeserializeCommand) (D-07)
- [x] 40-03-PLAN.md — Admin UI collapse: predicate tree + Item/XML type detach from Mode + tests (D-06, D-04)
- [x] 40-04-PLAN.md — swift2.2-combined.json rewrite + docs/baselines/Swift2.2-baseline.md + docs/configuration.md + live-host human-verify checkpoint (D-05)
- [x] 40-05-PLAN.md — Solution-wide build + test + regression-grep gate; produces 40-VERIFICATION.md

**Execution waves** (for /gsd-execute-phase):
- Wave 1: 40-01 (model + loader + writer; downstream files compile-broken until Wave 2)
- Wave 2: 40-02, 40-03, 40-04 (parallel — disjoint file sets: runtime/orchestrator vs. admin UI vs. baseline+docs)
- Wave 3: 40-05 (gate — full build + test + regression grep)

### Phase 41: Admin-UI polish + cross-page consistency

**Goal:** Resolve the five admin-UI issues surfaced during manual verification of Phase 40's flat-shape deploy:
- Rename "Item Types" tree node to "Item Type Excludes" (the page manages per-ItemType field exclusions, not item types)
- Sanity-check the empty `excludeFieldsByItemType` on the Phase 40 Swift 2.2 baseline (likely lost during the 40-04 flat-shape rewrite — restore or document)
- Fix eCom_CartV2 list-vs-detail mismatch (list shows 21 excluded elements, detail dual-list shows 0 — likely a key/lookup mismatch in `XmlTypeByNameQuery`)
- Enlarge the "Sample XML from database" editor on `XmlTypeEditScreen` to fill the screen by default
- Fix the Mode dropdown error: parenthetical option labels ("Deploy (source-wins)", "Seed (field-level merge)") cause a screen error — drop the suffix; explanatory text belongs in a help tooltip

**Requirements**: D-01..D-13 from 41-CONTEXT.md (goal-driven phase; CONTEXT decisions are acceptance — RESEARCH supersedes for D-05 root cause and D-13 root cause)
**Depends on:** Phase 40
**Plans:** 1/3 plans executed

Plans:
- [x] 41-01-PLAN.md — Wave 0 RED tests: XmlTypeEditScreenTests + ItemTypeEditScreenTests + extend PredicateCommandTests + SerializerSettingsNodeProviderModeTreeTests (proves D-01, D-05, D-06, D-11, D-12, D-13 bug shapes before fixes)
- [ ] 41-02-PLAN.md — Renames + Mode label cleanup + baseline doc: D-01 tree+list+edit-screen rename, D-11 drop parens from option labels, D-03 document intentional empty excludeFieldsByItemType
- [ ] 41-03-PLAN.md — Substantive fixes: D-05 dual-list merge in XmlTypeEditScreen, D-06 same-shape in ItemTypeEditScreen, D-08/D-09/D-10 Sample XML Rows=30, D-12 hint copy, D-13 string-Mode binding (model + SavePredicateCommand Enum.Parse + PredicateByIndexQuery .ToString()), XmlTypeDiscovery DI seam

**Execution waves** (for /gsd-execute-phase):
- Wave 1: 41-01 (RED tests; test files only, no production-code overlap with later plans)
- Wave 2: 41-02 (renames + label cleanup + docs; touches SerializerSettingsNodeProvider, ItemTypeListScreen, ItemTypeEditScreen.GetScreenName ONLY, PredicateEditScreen labels ONLY, baseline doc)
- Wave 3: 41-03 (substantive fixes; touches ItemTypeEditScreen.CreateFieldSelector + XmlTypeEditScreen + PredicateEditModel + SavePredicateCommand + PredicateByIndexQuery — overlap with 41-02 on ItemTypeEditScreen forces sequencing)
