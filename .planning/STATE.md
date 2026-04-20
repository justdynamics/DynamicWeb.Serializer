---
gsd_state_version: 1.0
milestone: v0.4.0
milestone_name: Full Page Fidelity
status: executing
stopped_at: Completed 37-04-PLAN.md
last_updated: "2026-04-20T17:53:29.968Z"
last_activity: 2026-04-20
progress:
  total_phases: 26
  completed_phases: 24
  total_plans: 54
  completed_plans: 52
  percent: 96
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-04-03)

**Core value:** Developers can reliably move DynamicWeb database state between environments through source control, with serialized YAML files as the single source of truth.
**Current focus:** Phase 37 — production-ready-baseline

## Current Position

Phase: 37 (production-ready-baseline) — EXECUTING
Plan: 5 of 6
Status: Ready to execute
Last activity: 2026-04-20

## Recent Session — 2026-04-17 Autonomous Baseline Test

**Deliverables produced:**

- **D1 — Swift 2.2 baseline** (commits `9aa8421`, `f14f5ad`-adjacent):
  - `src/DynamicWeb.Serializer/Configuration/swift2.2-baseline.json` — 17-predicate
    deployment config

  - `docs/baselines/Swift2.2-baseline.md` — reasoning doc with the
    DEPLOYMENT / SEED / ENVIRONMENT three-bucket split

  - `baselines/Swift2.2/` — 1570 YAML files from live serialize run
  - Verified via full round-trip: Swift 2.2 → YAML → CleanDB, frontend renders
    correctly at `https://localhost:58217/Default.aspx?ID=9643` (Sign in page)

- **D2 — Improvement phase plan** (commits `a5d3738`, this commit):
  - Milestone v0.5.0 "Production-Ready Baseline" added to ROADMAP.md
  - Phase 37 with 4 plans covering findings F-01 through F-19
  - `.planning/phases/37-production-ready-baseline/37-0{1..4}-PLAN.md`
  - Promotes SEED-001 (strict mode) and SEED-002 (SQL identifier whitelist)

**Bugs fixed in-flight** (per `4C` authority):

- `a3d3140` — orchestrator warns when CacheInvalidator is missing but predicate
  declares service caches (no longer silent skip)

- `f0bfbba` — Area table schema-tolerance + datetime/bool/int type coercion
  (prevents "Invalid column name" and "Conversion failed" on cross-env deploys).
  Plan 37-02 broadens this to all raw-SQL write paths.

**Findings accumulated:** `.planning/sessions/2026-04-17-baseline-test/FINDINGS.md`
(F-01..F-19). Headline: seed-content vs deployment-data split — the serializer's
`source-wins` default cannot coexist with customer-edited content on a live prod
deploy.

**Test env state left behind:**

- Swift 2.2 config at `wwwroot/Files/Serializer.config.json` = baseline config;
  original at `Serializer.config.json.pre-baseline-test`

- CleanDB has baseline YAML deserialized; frontend verified working
- Both hosts are stopped at end of session

**Open questions for user on wake:**

- Approve Phase 37 plan shape before executing, or want to split / re-scope?
- Restore Swift 2.2's original Serializer.config.json or leave as baseline?
- Execute plans autonomously (via /gsd-execute-phase) or review each plan first?

Progress: [██████████] 100% (v0.4.0)

## Performance Metrics

**Velocity:**

- Total plans completed: 22 (prior milestones)
- Average duration: 4min
- Total execution time: ~1.4 hours

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| (prior milestones) | 20 | ~80min | ~4min |
| 19 | 1 | 3min | 3min |
| 20 | 2 | 7min | 3.5min |
| 21 | 1 | 3min | 3min |
| 22 | 1 | 1min | 1min |
| 23 | 1 | 3min | 3min |

**Recent Trend:**

- Last 5 plans: 5min, 2min, 3min, 1min, 3min
- Trend: Stable

*Updated after each plan completion*
| Phase 23 P02 | 5min | 2 tasks | 1 files |
| Phase 24 P01 | 2min | 2 tasks | 4 files |
| Phase 25 P01 | 5min | 2 tasks | 8 files |
| Phase 37 P37-01 | 95min | 3 tasks | 30 files |
| Phase 37-production-ready-baseline P01.1 | 23min | 2 tasks | 22 files |
| Phase 37-production-ready-baseline P37-02 | 13min | 2 tasks | 7 files |
| Phase 37-production-ready-baseline P37-03 | 24min | 3 tasks | 17 files |
| Phase 37-production-ready-baseline P37-04 | 105min | 3 tasks | 19 files |

## Accumulated Context

### Decisions

Decisions are logged in PROJECT.md Key Decisions table.
Recent decisions affecting current work:

- Sub-objects (Seo, UrlSettings, Visibility) always serialized; NavigationSettings only when UseEcomGroups=true
- Allowclick/Allowsearch/ShowInSitemap/ShowInLegend default to true on DTO matching DW field initializers
- ActiveFrom/ActiveTo as nullable DateTime to distinguish unset from explicit
- All ~30 page properties have public setters, flow through SavePage -- no special API needed
- PageNavigationSettings is inline columns on Page table, not separate entity
- Area ItemType uses standard Item.SerializeTo()/DeserializeFrom() pattern
- InternalLinkResolver already exists from v0.3.1 -- reuse for ShortCut and ProductPage links
- Timestamps deferred to future milestone (requires direct SQL post-save)
- No backward compatibility needed (beta)
- Sub-object DTOs for logical groupings (SEO, URL settings, visibility, navigation) to keep YAML clean
- [Phase 23]: EcommerceNavigationParentType enum is in Dynamicweb.Content namespace (not Ecommerce.Navigation)
- [Phase 24]: Area ItemType uses standard Item.SerializeTo/DeserializeFrom pattern; ItemType not set on target (must be pre-configured)
- [Phase 37]: Phase 37-01: Deploy/Seed config structural split with legacy-flat pass-through shims, Seed icon=Flask, SerializeSubtreeCommand pinned to Deploy, SqlTable Seed-skip reuses existingChecksums, Content Seed-skip scoped to UPDATE path only
- [Phase 37-production-ready-baseline]: Phase 37-01.1: Per-mode Item Type + XML Type admin UI — models/queries/commands carry DeploymentMode and route to ModeConfig dictionaries; legacy ExcludeFieldsByItemType + ExcludeXmlElementsByType aliases removed; tree splits ItemTypes/EmbeddedXml into four per-mode subtrees with unique NodeIds; ContentSerializer/ContentDeserializer pinned to Deploy explicitly (mode-threading deferred); ConfigPathResolver.TestOverridePath added as AsyncLocal test hook for node-provider tests.
- [Phase 37-production-ready-baseline]: Phase 37-02: TargetSchemaCache consolidates ContentDeserializer Area-only schema tolerance (f0bfbba) + SqlTableProvider.CoerceRowTypes into one shared per-run cache; DataGroupMetadataReader kept parallel (smallest diff); ContentDeserializer cache not shared with SqlTableProvider cache since Area and SqlTable predicate tables never overlap
- [Phase 37-production-ready-baseline]: Phase 37-03: SqlIdentifierValidator + SqlWhereClauseValidator whitelist identifiers against INFORMATION_SCHEMA; RuntimeExcludes ships flat curated map (UrlPath.UrlPathVisitsCount + EcomShops.ShopIndex* per F-06/F-07) per D-07 no Runtime/Credential split; predicate.Where + predicate.IncludeFields round-trip through ConfigLoader/ConfigWriter; ConfigLoader.Load(path, validator?) overload aggregates errors across Deploy+Seed SqlTable predicates; SavePredicateCommand exposes IdentifierValidator/WhereValidator hooks; StripStringLiterals elides quoted content entirely (space separator, not 'x' fillers) to avoid tokenizer false positives while raw-clause BannedTokens scan still catches literal injection
- [Phase 37-production-ready-baseline]: Phase 37-04: DwCacheServiceRegistry curated static map (9 services via DW10 Dynamicweb.Ecommerce.Services static locator + Services.Areas + VatGroupCountryRelationService via DependencyResolver) replaces AddInManager-based ICacheResolver (F-10 silent skips); TranslationLanguageService dropped (absent from DW 10.23.9 NuGet). StrictModeEscalator + CumulativeStrictModeException + StrictModeResolver (D-16: CLI/API default ON, AdminUi default OFF; request>config>entry-point-default). Log-wrapper pattern at orchestrator boundary intercepts every 'WARNING:' line from any downstream code — chose over ctor-threading 5 classes for smallest diff. SerializerConfiguration.StrictMode (bool?) round-trips via ConfigLoader/ConfigWriter. SerializerDeserializeCommand.IsAdminUiInvocation flag distinguishes admin-UI entry point from API default.

### Pending Todos

None yet.

### Blockers/Concerns

None.

## Session Continuity

Last session: 2026-04-20T17:53:29.963Z
Stopped at: Completed 37-04-PLAN.md
Resume file: None
