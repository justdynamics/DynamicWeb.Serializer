---
gsd_state_version: 1.0
milestone: v0.4.0
milestone_name: Full Page Fidelity
status: executing
stopped_at: Phase 39 context gathered
last_updated: "2026-04-28T21:41:45.689Z"
last_activity: 2026-04-28 -- Phase 40 planning complete
progress:
  total_phases: 30
  completed_phases: 28
  total_plans: 73
  completed_plans: 67
  percent: 92
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-04-03)

**Core value:** Developers can reliably move DynamicWeb database state between environments through source control, with serialized YAML files as the single source of truth.
**Current focus:** Phase 39 — Seed mode field-level merge

## Current Position

Phase: 39
Plan: Not started
Status: Ready to execute
Last activity: 2026-04-28 -- Phase 40 planning complete

## Recent Session — 2026-04-17 Autonomous Baseline Test

**Deliverables produced:**

- **D1 — Swift 2.2 baseline** (commits `9aa8421`, `f14f5ad`-adjacent):
  - `src/DynamicWeb.Serializer/Configuration/swift2.2-baseline.json` — 17-predicate
    deployment config

  - `docs/baselines/Swift2.2-baseline.md` — reasoning doc with the
    DEPLOYMENT / SEED / NOT-SERIALIZED three-bucket split

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

- Total plans completed: 35 (prior milestones)
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
| 38 | 5 | - | - |
| 38.1 | 5 | - | - |
| 39 | 3 | - | - |

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
| Phase 37-production-ready-baseline P37-05 | 48min | 3 tasks | 19 files |
| Phase 37-production-ready-baseline P37-06 | 5min | 2 tasks | 7 files |
| Phase 38.1 P01 | 240 | 6 tasks | 11 files |
| Phase 38.1 P02 | 18min | 3 tasks | 4 files |
| Phase 38.1 P03 | 6 | 2 tasks | 3 files |
| Phase 38.1 P04 | 60 | 2 tasks | 14 files |
| Phase 38.1 P05 | 5min | 1 tasks | 1 files |

## Accumulated Context

### Roadmap Evolution

- 2026-04-21: Phase 38 added — Production-Ready Baseline Hardening. User-approved full scope (groups A+B+C+D+E) after autonomous E2E round-trip on 2026-04-20 surfaced 13 distinct follow-up items that Phase 37 itself correctly did NOT attempt to fix. Findings source: `.planning/sessions/2026-04-20-e2e-baseline-roundtrip/REPORT.md`.
- 2026-04-21: Phase 38.1 inserted after Phase 38 — Close Phase 38 deferrals (URGENT). Four carry-forwards from Phase 38's gated-closed-on-38.1 disposition: B.5.1 SelectedValue paragraph-ID sweeper extension, B.4.1 SHOP19 FK cleanup SQL, B.3.1 wider schema-drift scope (7 columns not 3), GRID-01 142 GridRowDefinitionId NOT NULL resolution. Target: live Swift 2.2 → CleanDB E2E passes under strictMode: true.
- 2026-04-22: Phase 39 added — Seed mode field-level merge. ContentDeserializer and SqlTableProvider currently implement DestinationWins as row-level skip; the Deploy/Seed split intent is field-level merge so Seed fills what Deploy excluded without overwriting already-set fields. Unblocks the expanded swift2.2-combined.json Seed Content predicate.
- 2026-04-28: Phase 40 added — Per-predicate Deploy/Seed split. User pivoted from section-level (deploy.predicates / seed.predicates arrays) to a single flat predicate list with a per-item `mode` field. Significantly simpler config setup. Phase 39 runtime (MergePredicate, XmlMergeHelper, Seed-merge in ContentDeserializer + SqlTableProvider) is unaffected — only config schema + ConfigLoader/ConfigWriter readers + SavePredicateCommand routing + swift2.2-combined.json baseline change. No backcompat per project policy.

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
- [Phase 37-production-ready-baseline]: Phase 37-05 (TEMPLATE-01 + LINK-02): TemplateAssetManifest + TemplateReferenceScanner + BaselineLinkSweeper close the last two baseline-correctness gaps. Pre-flight manifest validation at start of Deserialize replaces ~80 LOC of per-page inline template validation. BaselineLinkSweeper fails serialize at source when the baseline has orphan Default.aspx?ID= refs. SqlTable predicates opt in to at-deserialize link resolution via resolveLinksInColumns — orchestrator reorders Content-first (approach A) and threads aggregated source->target map into SqlTableWriter.ApplyLinkResolution. ISerializationProvider.Deserialize gained optional InternalLinkResolver? 6th param; every Moq .Setup/.Verify/.Returns lambda was updated inline (Rule 3 blocker).
- [Phase 37-production-ready-baseline]: Phase 37-06 (gap closure for SC-3/CR-01): wired default SqlIdentifierValidator into the 1-arg ConfigLoader.Load(path) overload — all 22 production call sites now enforce identifier allowlisting without call-site changes. Added TestOverrideIdentifierValidator AsyncLocal (mirrors ConfigPathResolver.TestOverridePath) + internal _testDefaultValidatorConstructedCallback spy hook (InternalsVisibleTo narrows exposure to test assembly only per T-37-06-03). ConfigLoaderValidatorFixtureBase abstract test-helper class installs a permissive union-allowlist AsyncLocal override in per-class ctor/Dispose — 4 test classes inherit it (ConfigLoaderTests, DeployModeConfigLoaderTests, PredicateCommandTests, SaveSerializerSettingsCommandTests); 6 Content-only test classes audited and left alone. Test 2 uses a spy-callback direct proof rather than Assert.ThrowsAny to avoid coupling to DB-layer exception shape when INFORMATION_SCHEMA is unreachable. 620/620 tests passing (baseline 618 + 2 new SC-3 tests).
- [Phase 38.1]: D-38.1-19 live E2E: 4 named deferrals (B.5.1, B.4.1, B.3.1, GRID-01) all CLOSED; wider pre-existing content-link orphan gap (20 page IDs × 47 occurrences + 10 PropertyItem GUIDs) surfaced by Phase 38-05's acknowledgedOrphanPageIds removal — needs B.5.2 + ITEM-01 follow-up; disposition open-with-gap
- [Phase 38.1]: D-38.1-16/17/18 ENVIRONMENT bucket renamed to NOT-SERIALIZED across Swift2.2-baseline.md, env-bucket.md, .planning/STATE.md; one ALLCAPS ENVIRONMENT intentionally retained in 'formerly ENVIRONMENT' rationale blockquote per acceptance regex
- [Phase 38.1]: Plan 02: D-38.1-02-01 script 09 scope extended 2→10 pages (Rule 2); D-38.1-02-02 live SQL unavailable, investigation substituted evidence-based artifact (Rule 3); dynamic-SQL sweep pattern for script 08 (no pre-catalog needed, matches script 01 ID-15717 precedent); idempotent zero-case commits empty transaction for Plan 04 pipeline safety
- [Phase 38.1]: Plan 03: D-38.1-03-02 Administrator password reseed deferred to documented SSMS fallback rather than inlined PBKDF2 hash (version-specific/brittle); D-38.1-03-04 pipeline uses 38 throw statements with per-step evidence logs for loud-fail discipline; cleandb-align-schema.sql ships 10 idempotent ALTER statements gated by IF COL_LENGTH IS NULL
- [Phase 38.1]: Plan 04: D-38.1-04-01 — halt per <failure_handling> step 3 on script 08 Msg 8623 + Part B/C predicate false-positive (1641 false matches vs expected 47); re-derivation requires DW ItemType metadata unavailable in SQL surface; Plan 02 artefact left intact for proper revision
- [Phase 38.1]: Plan 04: scripts 06 + 07 re-proven on fresh bacpac — B.4.1 (SHOP19 1→0) and GRID-01 (142 stale-emails→0) remain CLOSED; scripts 01-07 chain is idempotent and safe for re-run
- [Phase 38.1]: Plan 05: swift22-cleanup-overview.md ships as email-able single-document inventory of the Swift 2.2 cleanup surface; 8-section split (6 Class-A canonical + 2 Class-B defensive) reflects Plan 04 CLOSED reframing of scripts 08/09 as defensive tooling, not mandatory

### Pending Todos

None yet.

### Blockers/Concerns

- Phase 38.1 open-with-gap: Deserialize Deploy HTTP 400 with 57 escalations (10 PropertyItem GUIDs + 47 orphan page-ID link occurrences across 20 distinct IDs — not one of the 4 named Phase 38 deferrals; surfaced by Phase 38-05 acknowledgedOrphanPageIds removal; follow-up B.5.2 + ITEM-01 needed)
- Phase 38.1 Plan 04 halted: script 08 predicate misderivation + Msg 8623. Needs Plan 02 revision.

## Session Continuity

Last session: 2026-04-22T19:58:59.631Z
Stopped at: Phase 39 context gathered
Resume file: .planning/phases/39-seed-mode-field-level-merge-deploy-seed-split-intent-is-fiel/39-CONTEXT.md
