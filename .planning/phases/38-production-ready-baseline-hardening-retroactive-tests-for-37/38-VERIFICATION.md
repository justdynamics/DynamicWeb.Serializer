---
phase: 38-production-ready-baseline-hardening-retroactive-tests-for-37
verified: 2026-04-21T19:30:00Z
status: passed
score: 8/8 success criteria verified
overrides_applied: 1
overrides:
  - must_have: "SC 1 — Swift 2.2 → CleanDB round-trip runs with strictMode: true (config default) without a single escalated warning (B.1–B.4 closed or explicitly acknowledged via a sanctioned bypass)"
    reason: "gated-closed-on-38.1 — D-38-16 code+config change is complete (strictMode: false removed, acknowledgedOrphanPageIds: [15717] removed from swift2.2-combined.json; API/CLI default reverts to ON via StrictModeResolver). Final live E2E surfaced 4 explicitly-acknowledged carry-forwards to Phase 38.1: (1) B.5.1 SelectedValue paragraph-ID validation (~10 LOC extension of B.5's Pitfall 3 deferral); (2) B.4 SHOP19 FK cleanup SQL; (3) expanded B.3 scope (7 drift columns across EcomGroups+EcomProducts, not only the 3 Area columns B.3 originally named); (4) 142 GridRow NOT NULL. All four are source-data/schema-alignment work, not serializer code defects. User explicitly approved this disposition during the final checkpoint. Scope-expansion clause of SC 1 ('acknowledged via a sanctioned bypass') is satisfied by the 38-05-e2e-results.md disposition."
    accepted_by: "justin@justdynamics.nl"
    accepted_at: "2026-04-21T17:03:07Z"
---

# Phase 38: Production-Ready Baseline Hardening — Verification Report

**Phase Goal:** Close everything Phase 37's autonomous E2E round-trip surfaced but didn't fix. After Phase 38, the Swift 2.2 → CleanDB round-trip runs with `strictMode: true` without warnings-turned-failures, all follow-up code changes from 37 have proper test coverage, the one known silent data-loss path (EcomProducts 2051 → 582) is either fixed or explicitly documented, and customers have a written "what's NOT in the baseline" reference for per-env config.
**Verified:** 2026-04-21T19:30:00Z
**Status:** passed (1 override applied)
**Re-verification:** No — initial verification

## Goal Achievement

### Success Criteria (ROADMAP contract)

| # | Success Criterion | Status | Evidence |
|---|-------------------|--------|----------|
| 1 | Swift 2.2 → CleanDB round-trip runs with `strictMode: true` (config default) without a single escalated warning (B.1–B.4 closed or explicitly acknowledged via a sanctioned bypass) | PASSED (override) | Config change in place: `! grep strictMode.*false swift2.2-combined.json` → no hits; `! grep acknowledgedOrphanPageIds swift2.2-combined.json` → no hits. Final live E2E (38-05-e2e-results.md) surfaced 4 explicitly-acknowledged items deferred to Phase 38.1 (B.5.1 SelectedValue, B.4 SHOP19, expanded B.3 drift, 142 GridRow NOT NULL). User-approved `gated-closed-on-38.1` disposition — see override. |
| 2 | `AcknowledgedOrphanPageIds` lives in exactly ONE place in the model layer, has 3+ unit tests proving the malicious/acknowledged/unlisted cases, and has one threat-model entry in PLAN.md | VERIFIED | `AcknowledgedOrphanPageIds` in model layer: exactly 1 occurrence in `src/DynamicWeb.Serializer/Models/ProviderPredicateDefinition.cs:95`; zero occurrences on `ModeConfig` (A.3 consolidation landed). Tests: `BaselineLinkSweeperAcknowledgmentTests.cs` has 3 `[Fact]` methods — `Sweep_UnacknowledgedOrphanId_Throws`, `Sweep_AcknowledgedOrphanId_IsFilteredFromFatal`, `Sweep_UnlistedOrphanId_Throws_EvenWhenOtherAcknowledged` (T-38-02 threat anchor). Threat-model entry: 38-02-PLAN.md contains `<threat_model>` block with T-38-02. |
| 3 | Area IDENTITY_INSERT has a unit test that fails if the wrapping is removed | VERIFIED | `tests/DynamicWeb.Serializer.Tests/Serialization/AreaIdentityInsertTests.cs` exists (151 lines, 2 `[Fact]` methods). Ordered regex assertion on line 105 uses `RegexOptions.Singleline \| RegexOptions.IgnoreCase` validating `SET IDENTITY_INSERT [Area] ON.*?INSERT INTO [?Area]?.*?SET IDENTITY_INSERT [Area] OFF` sequence. Test fails if ON/INSERT/OFF ordering is broken or any piece is missing. Internal test-hook `InvokeCreateAreaFromPropertiesForTest` wired via `ISqlExecutor` seam on `ContentDeserializer.cs` (3 occurrences of `_sqlExecutor.ExecuteNonQuery` + `InvokeCreateAreaFromPropertiesForTest`). |
| 4 | EcomProducts round-trip preserves ALL rows from Swift 2.2 to CleanDB, OR the filtering is explicitly documented with a test proving the filter criterion | VERIFIED | C.1 fix in `src/DynamicWeb.Serializer/Providers/SqlTable/FlatFileStore.cs:138` — monotonic-counter `for (int n = 1; n < 100_000; n++)` replaces hash-of-identity overwrite. Regression test: `FlatFileStoreDeduplicationTests.cs` (110 lines, 3 tests). Live E2E evidence in 38-03-e2e-results.md + 38-05-e2e-results.md: source 2051 EcomProducts → 2052 YAML → target 2051 rows. Pre-fix was 582 YAML (1469 silent losses); fix preserves all rows. |
| 5 | `POST /Admin/Api/SerializerSerialize?mode=seed` works identically to the JSON-body variant | VERIFIED | Query-param fallback landed unconditionally in both command files: `Dynamicweb.Context.Current?.Request?["mode"]` found in `SerializerSerializeCommand.cs:56` AND `SerializerDeserializeCommand.cs:69`. Fallback triggered when `Mode` stays at "deploy" default; re-validated via `Enum.TryParse<DeploymentMode>` (same threat gate as JSON body path, T-38-D1-01). Test: `Handle_QueryParamMode_BindsWhenDefault` in `SerializerSerializeCommandTests.cs:29`. |
| 6 | HTTP 400 is never returned on a fully-successful serialize/deserialize | VERIFIED | Status mapping extracted into pure `MapStatusFromResult(OrchestratorResult) → CommandResult` on both commands. Unconditional zero-error == Ok invariant proven via `SynthOrchestratorResult.WithEmptyErrors()` test helper driving `InvokeMapStatusForTest(...)`. Test methods: `Handle_ZeroErrors_SynthOrchestratorResult_ReturnsOk` (both Serialize and Deserialize test files, 4 total across `[Fact]` bodies). Plus anti-regression `Handle_ZeroErrors_MessageContainsErrorsLiteral_StatusStillOk`. |
| 7 | A new smoke tool exits 0 when the CleanDB frontend serves all expected pages as 2xx/3xx, non-zero with a report on any 5xx | VERIFIED | `tools/smoke/Test-BaselineFrontend.ps1` exists (228 lines) with `PageActive = 1` enumeration + `Invoke-WebRequest` + `SkipCertificateCheck`. Live verification in 38-04-smoke-results.md: happy-path 2xx=89/89 → exit 0; wrong-host transport=89 → exit 1; empty area → exit 0 with "Nothing to test" message. `tools/smoke/README.md` (115 lines) documents LOCAL-DEV-ONLY contract + D-38-13 provenance. |
| 8 | Swift2.2-baseline.md has a "known pre-existing source-data bugs" section; env-bucket.md explains Friendly URL + GlobalSettings.config + secrets are per-env infra | VERIFIED | `docs/baselines/Swift2.2-baseline.md:178` — `## Pre-existing source-data bugs caught by Phase 37 validators` section (lines 178–235) with 3 column-name mistakes table, 5 orphan page IDs table, 267+238 cleanup counts, and `acknowledgedOrphanPageIds: [15717]` temporary-note. `docs/baselines/env-bucket.md` exists with 9 `##` sections (covers DEPLOYMENT/SEED/ENVIRONMENT split cross-reference, GlobalSettings.config/Friendly URLs, Azure Key Vault, Per-env Area fields, Swift templates filesystem git-clone pointer, Azure App Service config, DW NuGet version alignment from B.3). |

**Score:** 8/8 success criteria verified (1 via override)

### Backlog Requirements Coverage (15 items)

| ID | Requirement | Source Plan | Status | Evidence |
|----|-------------|-------------|--------|----------|
| A.1 | TDD unit tests for AcknowledgedOrphanPageIds (malicious/acknowledged/unlisted) | 38-02 | SATISFIED | `BaselineLinkSweeperAcknowledgmentTests.cs` — 3 `[Fact]` methods |
| A.2 | TDD integration test for IDENTITY_INSERT wrapping on Area create | 38-02 | SATISFIED | `AreaIdentityInsertTests.cs` — ordered regex + ISqlExecutor seam |
| A.3 | Consolidate AcknowledgedOrphanPageIds to ONE source-of-truth | 38-02 | SATISFIED | Only on `ProviderPredicateDefinition`; zero `ModeConfig` references; ConfigLoader legacy-warn-and-drop path |
| B.1 | Missing grid-row templates (1ColumnEmail, 2ColumnsEmail) cleanup | 38-03 | SATISFIED | `tools/swift22-cleanup/05-null-stale-template-refs.sql` + verified 0 stale refs across 5278 YAML files post-cleanup |
| B.2 | Missing page-layout template (Swift-v2_PageNoLayout.cshtml) cleanup | 38-03 | SATISFIED | Same SQL 05 script handles all 3 template names; ItemType + Page.PageLayout + Paragraph.ParagraphItemType + GridRow.GridRowDefinitionId locations |
| B.3 | 3 schema-drift Area columns investigation | 38-03 | SATISFIED (investigation) | `38-03-b3-investigation.md` — outcome-a-operational (DW-core drift); documented in env-bucket.md. Scope expanded to 7 more columns per live E2E, deferred to Phase 38.1 extension. |
| B.4 | FK re-enable warning on EcomShopGroupRelation → EcomShops.ShopId | 38-03 | SATISFIED (investigation) | `38-03-b4-investigation.md` — source-data orphan SHOP19 row; fix deferred to Phase 38.1 per <30-LOC scope rule |
| B.5 | BaselineLinkSweeper paragraph-anchor false-positive | 38-02 | SATISFIED | `BaselineLinkSweeper.cs` — `CollectSourceParagraphIds` walker + `validParagraphIds` HashSet + 4 tests in `BaselineLinkSweeperParagraphAnchorTests.cs`. Scope: `Default.aspx?ID=X#Y` only; ButtonEditor `SelectedValue` JSON case (Pitfall 3) deferred to B.5.1 in Phase 38.1. |
| C.1 | EcomProducts 2051 → 582 silent-data-loss diagnosis + fix | 38-03 | SATISFIED | FlatFileStore monotonic-counter fix + 3 regression tests; live E2E proves 2051 → 2051 preservation |
| D.1 | `?mode=seed` query-param binding | 38-01 | SATISFIED | Unconditional fallback in both Serialize + Deserialize commands |
| D.2 | HTTP status code bug (400 on 0-error serialize) | 38-01 | SATISFIED | `MapStatusFromResult` + `SynthOrchestratorResult` + unconditional Ok assertion |
| D.3 | New SerializerSmoke admin command / CLI | 38-04 | SATISFIED | `tools/smoke/Test-BaselineFrontend.ps1` + live verification 3 scenarios |
| E.1 | Swift2.2-baseline.md "pre-existing source-data bugs" section | 38-01 | SATISFIED | Lines 178–235 with 4 required sub-content points |
| E.2 | env-bucket.md per-env config reference | 38-01 | SATISFIED | 9 `##` sections, three-bucket-split cross-reference, all 5 required content areas |
| D-38-16 | Restore strictMode default ON + remove ack workaround | 38-05 | SATISFIED | swift2.2-combined.json has no `strictMode: false` and no `acknowledgedOrphanPageIds` |

**Backlog coverage:** 15/15 requirement IDs directly mapped to plan-level `requirements:` frontmatter. No orphaned requirements.

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `tests/DynamicWeb.Serializer.Tests/AdminUI/SerializerSerializeCommandTests.cs` | D.1/D.2 tests with QueryParamMode + SynthOrchestratorResult | VERIFIED | 5 `[Fact]` methods; `QueryParamMode` marker on line 29; `SynthOrchestratorResult.WithEmptyErrors` used on lines 55, 71 |
| `tests/DynamicWeb.Serializer.Tests/AdminUI/SerializerDeserializeCommandTests.cs` | D.1/D.2 parity tests | VERIFIED | 4 `[Fact]` methods; same unconditional zero-error invariant |
| `tests/DynamicWeb.Serializer.Tests/AdminUI/SynthOrchestratorResult.cs` | Zero-error OrchestratorResult factory | VERIFIED | Shared helper — referenced by both command test files |
| `tests/DynamicWeb.Serializer.Tests/Infrastructure/BaselineLinkSweeperAcknowledgmentTests.cs` | A.1 retroactive tests | VERIFIED | 142 lines, 3 `[Fact]` methods, T-38-02 threat anchor present |
| `tests/DynamicWeb.Serializer.Tests/Serialization/AreaIdentityInsertTests.cs` | A.2 ordered-regex test | VERIFIED | 151 lines, 2 `[Fact]` methods, `RegexOptions.Singleline` at line 105 |
| `tests/DynamicWeb.Serializer.Tests/Infrastructure/BaselineLinkSweeperParagraphAnchorTests.cs` | B.5 paragraph-anchor tests | VERIFIED | 121 lines, 4 `[Fact]` methods |
| `tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/FlatFileStoreDeduplicationTests.cs` | C.1 dedup regression tests | VERIFIED | 110 lines, 3 regression tests |
| `src/DynamicWeb.Serializer/Configuration/swift2.2-combined.json` | Post-Phase-38 canonical (no strictMode override, no ack) | VERIFIED | Zero hits on `strictMode.*false` and zero hits on `acknowledgedOrphanPageIds` |
| `src/DynamicWeb.Serializer/Providers/SqlTable/FlatFileStore.cs` | Monotonic-counter dedup | VERIFIED | `for (int n = 1; n < 100_000; n++)` at line 138 |
| `src/DynamicWeb.Serializer/Infrastructure/BaselineLinkSweeper.cs` | Paragraph-anchor validation | VERIFIED | `CollectSourceParagraphIds` walker + `validParagraphIds` HashSet (14 grep hits) |
| `src/DynamicWeb.Serializer/Serialization/ContentDeserializer.cs` | ISqlExecutor seam + test hooks | VERIFIED | 3 occurrences of `InvokeCreateAreaFromPropertiesForTest` / `_sqlExecutor.ExecuteNonQuery` |
| `src/DynamicWeb.Serializer/Serialization/ContentSerializer.cs` | Per-predicate ack aggregation | VERIFIED | `Deploy.Predicates.SelectMany(p => p.AcknowledgedOrphanPageIds)` at line 91 |
| `src/DynamicWeb.Serializer/AdminUI/Commands/SerializerSerializeCommand.cs` | D.1 fallback + D.2 MapStatusFromResult | VERIFIED | Query-param fallback at line 56; 6 references to SynthOrchestratorResult/MapStatusFromResult/InvokeMapStatusForTest |
| `src/DynamicWeb.Serializer/AdminUI/Commands/SerializerDeserializeCommand.cs` | D.1 fallback + D.2 MapStatusFromResult | VERIFIED | Query-param fallback at line 69; 6 references to helpers |
| `tools/swift22-cleanup/05-null-stale-template-refs.sql` | 3 orphan template cleanup SQL | VERIFIED | 155 lines, 18 references to required patterns (ItemType_Swift-v2_%, GridRowDefinitionId, Swift-v2_PageNoLayout) |
| `tools/smoke/Test-BaselineFrontend.ps1` | D.3 standalone PS7 smoke tool | VERIFIED | 228 lines, 4 required patterns present (PageActive=1, Invoke-WebRequest, SkipCertificateCheck) |
| `tools/smoke/README.md` | Smoke tool usage docs | VERIFIED | 115 lines, LOCAL-DEV ONLY banner, D-38-13 provenance |
| `docs/baselines/Swift2.2-baseline.md` | Pre-existing bugs section | VERIFIED | `## Pre-existing source-data bugs caught by Phase 37 validators` at line 178 |
| `docs/baselines/env-bucket.md` | Per-env config reference | VERIFIED | 9 H2 sections, 7 GlobalSettings.config references, cross-references to three-bucket split |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|----|--------|---------|
| `SerializerSerializeCommand.Handle()` | `Dynamicweb.Context.Current.Request["mode"]` | query-param fallback when Mode is default | WIRED | Line 56 |
| `SerializerDeserializeCommand.Handle()` | `Dynamicweb.Context.Current.Request["mode"]` | query-param fallback when Mode is default | WIRED | Line 69 |
| `ContentSerializer` sweep-ack filter | `_configuration.{Deploy,Seed}.Predicates.SelectMany(p => p.AcknowledgedOrphanPageIds)` | per-predicate aggregation | WIRED | Lines 91–92 |
| `ContentDeserializer.CreateAreaFromProperties` | `_sqlExecutor.ExecuteNonQuery(cb)` | ISqlExecutor seam | WIRED | 3 grep hits (2 write-sites + 1 internal hook) |
| `BaselineLinkSweeper.Sweep` | `validParagraphIds.Contains(paraId)` | anchor validation against collected paragraph source IDs | WIRED | 14 `validParagraphIds` references |
| `FlatFileStore.DeduplicateFileName` | monotonic counter enumeration | `for (int n = 1; n < 100_000; n++)` | WIRED | Line 138 |
| `docs/baselines/env-bucket.md` | `docs/baselines/Swift2.2-baseline.md` | cross-reference to three-bucket split | WIRED | 7 `GlobalSettings.config` + cross-reference heading present |
| `tools/smoke/Test-BaselineFrontend.ps1` | SQL enumeration + HTTP probe | `Invoke-Sqlcmd WHERE PageAreaID + Invoke-WebRequest` | WIRED | 4 required pattern hits |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|--------------------|--------|
| `FlatFileStore.DeduplicateFileName` | filename variants | identity hash + monotonic counter | Live E2E: 2051 source → 2052 YAML → 2051 target (matches source count; the +1 is stale-file cleanup artifact, unrelated to dedup) | FLOWING |
| `BaselineLinkSweeper.Sweep` | `validParagraphIds` | `CollectSourceParagraphIds` walker | 4 tests exercise both resolve and non-resolve paths on realistic fixtures | FLOWING |
| `ContentSerializer` per-predicate ack | `Deploy.Predicates.SelectMany` | Config-loaded `ProviderPredicateDefinition.AcknowledgedOrphanPageIds` | ConfigLoaderTests exercise legacy-drop path; BaselineLinkSweeperAcknowledgmentTests exercise sweep path | FLOWING |
| `MapStatusFromResult` | `OrchestratorResult.HasErrors` | Real `OrchestratorResult` at Handle() exit; synthetic zero-error helper at test | Unconditional Ok assertion on synth zero-error; no environment dependency | FLOWING |
| `Test-BaselineFrontend.ps1` | Page rows | `Invoke-Sqlcmd` on Page table | Happy-path live verification: 89 rows enumerated, 89 buckets populated | FLOWING |
| `swift2.2-combined.json` | Config flags | ConfigLoader reads to `SerializerConfiguration.StrictMode` (bool?) | Live E2E (38-05-e2e-results.md) confirms strictMode default ON via StrictModeResolver when JSON has no override | FLOWING |

### Behavioral Spot-Checks

All behavioral spot-checks were already run during plan execution as part of live E2E gates (see 38-03-e2e-results.md and 38-05-e2e-results.md). Re-running at verification time would require live DW hosts. Representative results from the existing evidence:

| Behavior | Evidence | Result | Status |
|----------|----------|--------|--------|
| Unit test suite passes | 38-02-SUMMARY: `dotnet test --filter "Category!=Integration" --nologo` → 640/640 passing; 38-03 adds 3 more tests; 38-01 adds 9 tests | 640+ passing, 0 failed | PASS |
| Smoke tool happy-path exit 0 | 38-04-smoke-results.md — 2xx=89/89 against CleanDB :58217 | exit 0 | PASS |
| Smoke tool wrong-host exit 1 | 38-04-smoke-results.md — transport=89, exit 1 | exit 1 | PASS |
| Smoke tool empty area exit 0 | 38-04-smoke-results.md — "No active pages found" | exit 0 | PASS |
| EcomProducts 2051 → 2051 round-trip | 38-03-e2e-results.md — source 2051 → 2052 YAML → target 2051 | preserved | PASS |
| B.1/B.2 stale template refs gone | 38-03-e2e-results.md — 0 matches across 5278 YAML files | clean | PASS |
| strictMode default restored via config absence | 38-05-e2e-results.md — config edit verified; E2E ran with strict behavior (escalations fired as expected on deferred items) | in place | PASS |
| Pre-existing integration tests require live DW host | deferred-items.md — 9 `IntegrationTests` fail with `DependencyResolverException` as pre-existing environmental issue; unit suite (`DynamicWeb.Serializer.Tests`) is CI-runnable and green | pre-existing, out of scope | N/A |

### Anti-Patterns Scan

Code review (38-REVIEW.md) identified 0 critical, 3 warning, 6 info items across 25 files. None are blockers:

| Finding | Severity | Impact |
|---------|----------|--------|
| WR-01 ContentSerializer iterates legacy Deploy-aliased Predicates — masked by `ContentProvider` always wiring Deploy | Warning | Latent footgun for future direct callers; current production path unaffected |
| WR-02 Area IDENTITY_INSERT inline concatenation relies on single-batch `ExecuteNonQuery` | Warning | Works under current `DwSqlExecutor`; at risk if driver ever splits by `;` |
| WR-03 PowerShell `$errors` shadows automatic variable in smoke script | Warning | Cosmetic — functionally harmless; PSScriptAnalyzer would flag |
| IN-01..IN-06 | Info | Duplicated mode-parsing across two commands, empty catch blocks, duplicated paragraph walker (W6 ack'd), magic-number cap, SQL-param style hint |

No TODO/FIXME/stub patterns found in the delivered code. No empty implementations. No placeholder returns. All `[Fact]` test methods have real assertions (not just `Assert.True(true)`).

### Commit Verification

All 20 claimed commits across 5 plans exist in `git log --all`:
- Plan 01: `048e78c`, `29a644e`, `e5681c7`
- Plan 02: `b0b6b83`, `2af12a1`, `b68f259`, `a515ec5`, `7ddd37c`
- Plan 03: `7f6af36`, `692e184`, `9a061e1`, `ed06c64`, `fff81e8`, `a159672`, `6065b96`, `77ad64a`
- Plan 04: `cb754bc`, `9e8cbc5`
- Plan 05: `2c70fc2`, `177487c`

### Deferred Items (Carry-Forward to Phase 38.1)

These items surfaced during Phase 38 execution and are explicitly deferred to Phase 38.1. They are **not** gaps against Phase 38 — they are known, user-approved carry-forwards. Phase 38.1 does not yet exist as a ROADMAP section, but the carry-forward queue is documented in:
- `deferred-items.md` (pre-existing integration-test environmental issue)
- `38-03-b4-investigation.md` (B.4 SHOP19 orphan cleanup)
- `38-05-e2e-results.md` (B.5.1 ButtonEditor SelectedValue, expanded B.3 scope, 142 GridRow NOT NULL)
- `38-05-SUMMARY.md` §"Phase 38.1 Queue (in priority order)"

| # | Deferred Item | Source | Rationale |
|---|---------------|--------|-----------|
| 1 | B.5.1 — Extend `BaselineLinkSweeper.CheckField.SelectedValuePattern` to validate against `validParagraphIds` | Plan 02 Pitfall 3 + Plan 05 E2E surfaced ButtonEditor JSON case | ~10 LOC extension of B.5's shipped fix; scope boundary held intentionally |
| 2 | B.4 close — `tools/swift22-cleanup/06-delete-orphan-ecomshopgrouprelation.sql` | Plan 03 Task 4 investigation | Required new file outside Plan 03 `files_modified`; escalation rule: defer, don't expand |
| 3 | B.3 scope extension — 7 EcomGroups/EcomProducts drift columns (operational DW NuGet alignment OR per-predicate allowlist) | Plan 05 E2E expanded the 3-Area scope | Same fix pattern; broader data scope |
| 4 | 142 GridRow `GridRowDefinitionId` NOT NULL | Plan 03 Task 5 cleanup side effect | Either serializer coalesce OR `06-delete-stale-email-gridrows.sql` |
| 5 | Integration tests require live DW host | Pre-existing before Phase 38 | Env setup concern; orthogonal to this phase |

### Regressions / Unplanned Failures

**None.** All deviations in the plan SUMMARYs are either:
- Auto-fixed during execution (SQL column reference errors in script 05 — Rule 1 auto-fixes in Plan 03)
- Scoped-to-plan deferrals per documented escalation rules
- Pre-existing environmental issues (integration tests)

No regressions introduced by Phase 38 code changes. Full unit test suite `dotnet test --filter "Category!=Integration"` stayed green at 640+/640+.

### Human Verification Required

**None.** The user-approved `gated-closed-on-38.1` disposition for SC 1 is treated as an override. All other success criteria verified programmatically.

### Gaps Summary

**No gaps against Phase 38.** Code+config change for D-38-16 is in place; all 15 requirement IDs satisfied; 8/8 success criteria verified (SC 1 via user-approved override for documented carry-forwards). Phase 38.1 queue is well-documented with prioritized items and a repro recipe for the final clean strict-mode E2E.

---

*Verified: 2026-04-21T19:30:00Z*
*Verifier: Claude (gsd-verifier)*
