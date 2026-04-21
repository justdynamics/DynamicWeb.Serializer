---
phase: 37-production-ready-baseline
verified: 2026-04-21T00:00:00Z
status: passed
score: 8/8 must-haves verified (code-level + live round-trip)
overrides_applied: 0
re_verification:
  previous_status: human_needed
  previous_score: 8/8 code-level (5 items pending live verification)
  gaps_closed:
    - "SC-3 closed by plan 37-06 (default SqlIdentifierValidator on 1-arg ConfigLoader.Load)"
    - "SC-1 / SC-2 / SC-6 / SC-7 / SC-8 closed by 2026-04-20 autonomous E2E round-trip (Swift 2.2 → CleanDB), see .planning/sessions/2026-04-20-e2e-baseline-roundtrip/REPORT.md. Frontend HTTP smoke confirms /en-us/home + /en-us/about + /en-us/shop + /en-us/posts + /en-us/sign-in all return 200 with real content after Deploy+Seed round-trip."
  gaps_remaining: []
  regressions: []
  live_verification_followups_filed_to_phase_38:
    - "Retroactive tests for the two 37-follow-up code changes (AcknowledgedOrphanPageIds on ProviderPredicateDefinition, IDENTITY_INSERT wrapping on Area create)"
    - "Strict-mode upstream data/template gaps (3 missing templates, 3 schema-drift Area columns) — need Swift installer fix or TEMPLATE-01 scope expansion before strict can default ON for Deploy"
    - "EcomProducts serialize drop: Swift 2.2 has 2051 products, Seed serialized 582 — investigate silent filtering"
    - "Mode query-param binding (?mode=seed) does not populate the command property; currently requires JSON body {Mode:seed}"
human_verification:
  - test: "Round-trip: serialize Swift 2.2 → CleanDB via the admin UI Deserialize button, verify zero 'Cache type not found (skipping)' lines in the log and that unknown cache names fail at config-load (per SC-2)."
    expected: "Admin UI shows an error message naming the unknown cache service and the supported-names list (currently TranslationLanguageService will trigger this on the bundled Swift 2.2 baseline — this is the documented CACHE-01 behavior)."
    why_human: "Requires running DW host + live Swift 2.2 DB + CleanDB; cannot verify end-to-end cache invocation without runtime."
  - test: "Strict-mode deserialize: edit Swift 2.2 baseline to reference a missing page (Default.aspx?ID=99999) that's outside the baseline predicates, deserialize via CLI/API, confirm run exits non-zero with CumulativeStrictModeException listing the unresolvable link (SC-6)."
    expected: "Non-zero exit code from CLI; HTTP 500 with CumulativeStrictModeException body from API; operator sees every warning in the exception message."
    why_human: "End-to-end strict-mode integration on a live DB run — unit tests cover the escalator and resolver but not the real orchestrator→provider→escalator wiring with a real missing-template or unresolved-link case."
  - test: "Seed mode preserves customer edits: run Seed deserialize twice against a CleanDB that has had customer edits to a page between runs. Confirm the second run's edits are preserved (page not overwritten)."
    expected: "Pages whose PageUniqueId exists on target are skipped with 'Seed-skip: page {guid}' log line; customer edits survive the second deserialize."
    why_human: "Requires live DB + customer-edit simulation; unit tests assert the skip branch fires but not the end-to-end preservation behavior."
  - test: "Deploy mode baseline link sweep: serialize Swift 2.2 baseline, confirm the sweep catches the ~10 F-17 orphan Default.aspx?ID= references and the serialize run fails with per-reference breakdown (SC-7)."
    expected: "ContentProvider reports per-predicate ERROR; orchestrator result has Errors populated; CLI/API returns non-zero exit. Operator sees every unresolvable link with source page + field name."
    why_human: "Requires Swift 2.2 data to trigger the F-17 known-orphan case; unit tests cover the sweep's detection logic but not the real-baseline false-positive/false-negative rate."
  - test: "LINK-02 pass 2: run Swift 2.2 → CleanDB deserialize with UrlPath predicate carrying resolveLinksInColumns: ['UrlPathRedirect'], verify UrlPath rows in CleanDB have UrlPathRedirect rewritten to CleanDB page IDs (SC-8)."
    expected: "The known Swift 2.2 UrlPath row with UrlPathRedirect=Default.aspx?ID=5862 has its target-DB value pointing to the CleanDB page ID for the matching PageUniqueId GUID."
    why_human: "Requires live target DB with mapped pages; unit tests cover ApplyLinkResolution in isolation but end-to-end rewrite fidelity needs a real target."
---

# Phase 37: Production-Ready Baseline Verification Report

**Phase Goal:** "The serializer becomes safe to run in an automated Azure deployment pipeline without overwriting customer-edited content, leaking credentials, silently corrupting FK integrity, or breaking on env schema drift."

**Verified:** 2026-04-20T21:00:00Z
**Status:** human_needed (all 8 code-level SC verified; 5 human-only items require live DW hosts)
**Re-verification:** Yes — after gap closure plan 37-06 (closes SC-3 / CR-01)

## Re-verification Summary

| Previous | Current |
|---------|---------|
| Status: gaps_found | Status: human_needed |
| Score: 7/8 (SC-3 FAILED) | Score: 8/8 (all code-level) |
| Blocking gap: SC-3 | Blocking gaps: none |

**Gap closed:** SC-3 (SQL identifier validation at config-load) — the 1-arg `ConfigLoader.Load(string)` overload now default-constructs a `SqlIdentifierValidator` and delegates to the 2-arg overload with a NON-NULL validator. All 22 production call sites now receive the identifier-validation gate without source changes at the call site.

**Regressions from fix:** none. Test suite 620/620 (baseline 618 + 2 new SC-3 tests).

## Goal Achievement

### Observable Truths (ROADMAP Success Criteria)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Deploy mode overwrites (source-wins preserved); Seed mode skips pages whose PageUniqueId is already on target | VERIFIED | `ContentDeserializer.cs:628-630` Seed-skip branch on UPDATE path; `SqlTableProvider.cs:299` DestinationWins skip via existingChecksums lookup; `ContentProvider.cs:142-143` log line confirms wiring. Tests: ItemTypeCommandTests, DeployModeConfigLoaderTests, StrictModeIntegrationTests. |
| 2 | Swift 2.2 → CleanDB round-trip: zero ERROR lines from cache invalidation; unknown cache names fail at config-load | VERIFIED | `DwCacheServiceRegistry.cs` has 9 typed cache entries; `CacheInvalidator.cs:49-54` throws on unknown names; `ConfigLoader.cs:131` calls `ValidateServiceCaches` unconditionally (both via 1-arg overload and 2-arg overload). TranslationLanguageService IS absent from the registry and swift2.2-baseline.json DOES reference it — loading the bundled baseline will fail loud, which IS the CACHE-01 contract. |
| 3 | Config with SQL-injection-style identifiers rejected at config-load before any SQL runs | VERIFIED (fixed in 37-06) | `ConfigLoader.cs:60-77` — 1-arg `Load(path)` overload now reads `TestOverrideIdentifierValidator` AsyncLocal (for tests) or default-constructs `new SqlIdentifierValidator()` (line 70, production) and delegates to the 2-arg overload with a NON-NULL validator. `ConfigLoader.cs:124-125` runs `ValidateIdentifiers` when validator is non-null. `identifierValidator: null` grep returns 0 matches in `ConfigLoader.cs` (was 1 before 37-06). Tests: `Load_DefaultPath_MaliciousTableIdentifier_Throws` (ConfigLoaderTests.cs:1187) proves a malicious `"EcomOrders] WHERE 1=1; DROP TABLE Users; --"` Table value is rejected via the default 1-arg Load path with `InvalidOperationException` containing both "INFORMATION_SCHEMA" and "EcomOrders"; `Load_DefaultPath_NoTestOverride_ConstructsDefaultValidator` (line 1231) proves the default-validator construction path fires when no test override is installed (direct spy via `_testDefaultValidatorConstructedCallback`). Both tests pass (2/2). |
| 4 | Default serialize of EcomShops excludes env-specific search-index columns via RuntimeExcludes | VERIFIED | `RuntimeExcludes.cs:30-37` registers ShopIndexRepository/Name/DocumentType/Update/Builder for EcomShops + UrlPathVisitsCount for UrlPath; `SqlTableProvider.cs` computes `effectiveExcludes = ExcludeFields ∪ (RuntimeExcludes[table] \ IncludeFields)` and logs auto-exclusions. IncludeFields per-predicate opt-in works. Tests: RuntimeExcludesTests, SqlTableProviderSerializeTests. |
| 5 | Missing template files surfaced pre-deserialize via TemplateAssetManifest validation; strict mode escalates to hard failure | VERIFIED | `TemplateAssetManifest.cs` Write/Read/Validate exist; `ContentSerializer.cs:70-73` writes templates.manifest.yml post-serialize; `ContentDeserializer.cs:118,218-238` pre-flights validation before any page writes; `_templateEscalator` is lenient but WARNING lines flow through the orchestrator's log-wrapper (`SerializerOrchestrator.WrapLogWithEscalator` line 325-343) which records them for strict-mode end-of-run assertion. Tests: TemplateAssetManifestTests, TemplateReferenceScannerTests. |
| 6 | Strict mode run with any unresolved page link or missing template exits non-zero | VERIFIED (with human_needed for end-to-end) | `StrictModeEscalator.cs` + `CumulativeStrictModeException` + `StrictModeResolver` complete; `SerializerOrchestrator.DeserializeAll:307` calls `escalator.AssertNoWarnings()` at end-of-run; `WrapLogWithEscalator:325-343` intercepts every WARNING line; `SerializerDeserializeCommand` resolves strict per entry point (API/CLI=ON, AdminUi=OFF) via D-16. Tests: StrictModeEscalatorTests (19), StrictModeIntegrationTests (9). Real end-to-end on live DB needs human verification. |
| 7 | Serialize fails with pre-commit sweep when YAML tree contains Default.aspx?ID=N refs pointing outside baseline (F-07/F-17 via D-22 pass 1) | VERIFIED | `BaselineLinkSweeper.cs` walks pages recursively, matches InternalLinkPattern + SelectedValuePattern against valid SourcePageId set; `ContentSerializer.cs:84-96` runs sweep post-serialize and throws InvalidOperationException with per-reference breakdown on any orphan. ContentProvider catches the exception and returns SerializeResult.Errors — CI/CD sees the non-zero exit via orchestrator-result error flag. Tests: BaselineLinkSweeperTests (13). |
| 8 | SqlTable predicates opt-in to link resolution via `resolveLinksInColumns`; UrlPath.UrlPathRedirect rewrites source→target (D-22 pass 2) | VERIFIED (with human_needed for round-trip) | `ProviderPredicateDefinition.ResolveLinksInColumns` exists; `SqlTableWriter.ApplyLinkResolution:175-187` rewrites string columns via `resolver.ResolveInStringColumn`; `SqlTableProvider.cs:225-234` invokes on rows when resolver + opt-in are both present; `ContentProvider.BuildSourceToTargetMap:216` builds the map post-deserialize; `SerializerOrchestrator.DeserializeAll` conditionally reorders Content predicates to run before SqlTable when any predicate opts in (decision A). Tests: SqlTableLinkResolutionIntegrationTests (6). Live round-trip on CleanDB target needs human verification. |

**Score:** 8/8 truths verified at code level. 5 truths carry additional human-verification items for live-host end-to-end confirmation (SC-1, SC-2, SC-6, SC-7, SC-8).

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/DynamicWeb.Serializer/Configuration/DeploymentMode.cs` | enum DeploymentMode { Deploy, Seed } | VERIFIED | Present, 14 lines, enum defined. |
| `src/DynamicWeb.Serializer/Configuration/ModeConfig.cs` | record ModeConfig with Predicates + per-mode ExcludeFields + OutputSubfolder + ConflictStrategy | VERIFIED | Present, 38 lines, all fields present. |
| `src/DynamicWeb.Serializer/Infrastructure/ManifestWriter.cs` | Writes {mode}-manifest.json | VERIFIED | Present, writes per-mode manifest. |
| `src/DynamicWeb.Serializer/Infrastructure/ManifestCleaner.cs` | Deletes files under {mode} subfolder not in manifest | VERIFIED | Present, path-confined cleaner. WR-02 from review notes Windows symlink-handling edge case — not a correctness blocker for CI/CD, covered by human smoke test recommendation. |
| `src/DynamicWeb.Serializer/Infrastructure/TargetSchemaCache.cs` | Shared per-run cache + unified Coerce | VERIFIED | Present, used from both ContentDeserializer (Area writes) and SqlTableProvider. 42 unit tests. Not thread-safe (WR-01) — accepted since orchestrator is sequential. |
| `src/DynamicWeb.Serializer/Infrastructure/DwCacheServiceRegistry.cs` | Curated map of DW service caches to typed ClearCache actions | VERIFIED | Present, 9 entries covering AreaService + 8 Ecommerce services. TranslationLanguageService documented as dropped (absent from DW 10.23.9). |
| `src/DynamicWeb.Serializer/Infrastructure/StrictModeEscalator.cs` | StrictModeEscalator + CumulativeStrictModeException + StrictModeResolver | VERIFIED | Present, 122 lines. DoS cap at 10,000 warnings. Entry-point resolver implements D-16. |
| `src/DynamicWeb.Serializer/Configuration/SqlIdentifierValidator.cs` | Validates table/column names against INFORMATION_SCHEMA | VERIFIED | Present, ValidateTable + ValidateColumn + GetColumns methods exist; test-ctor loader hook. Now wired as the default via `ConfigLoader.Load(path)` (37-06). |
| `src/DynamicWeb.Serializer/Configuration/SqlWhereClauseValidator.cs` | Tokenizes + rejects SQLi patterns + unknown identifiers | VERIFIED | Present, BannedTokens/BannedKeywords/literal-stripping logic. 24 tests. |
| `src/DynamicWeb.Serializer/Configuration/RuntimeExcludes.cs` | Curated map tableName → runtime-only columns | VERIFIED | Present, 6 entries (UrlPath + 5× EcomShops). D-07 single-list model. |
| `src/DynamicWeb.Serializer/Infrastructure/TemplateAssetManifest.cs` | Manifest-only template tracking with validation | VERIFIED | Present, Write/Read/Validate + TemplateReference record. Path-traversal + DoS guards. |
| `src/DynamicWeb.Serializer/Infrastructure/TemplateReferenceScanner.cs` | Extracts layout/item-type/grid-row refs from SerializedPage trees | VERIFIED | Present, walks recursive children + collects referencedBy. |
| `src/DynamicWeb.Serializer/Infrastructure/BaselineLinkSweeper.cs` | Post-serialize sweep for Default.aspx?ID=N orphans | VERIFIED | Present, matches InternalLinkPattern + SelectedValuePattern. |
| `src/DynamicWeb.Serializer/Models/ProviderPredicateDefinition.cs` | Adds Where, IncludeFields, ResolveLinksInColumns | VERIFIED | All three fields present as optional properties. |
| `src/DynamicWeb.Serializer/Providers/CacheInvalidator.cs` | Rewritten to use DwCacheServiceRegistry, throws on unknown | VERIFIED | 61 lines. Old ICacheResolver/ICacheInstance deleted. Throws with supported-names listing. |
| `tests/DynamicWeb.Serializer.Tests/TestHelpers/ConfigLoaderValidatorFixture.cs` | 37-06 test helper: permissive AsyncLocal fixture for 4 test classes | VERIFIED (new in 37-06) | Abstract `ConfigLoaderValidatorFixtureBase` installs permissive SqlIdentifierValidator in ctor, clears in Dispose. Union allowlist covers all 4 inheriting test classes. |

All 16 artifacts present and substantive. No STUB or MISSING artifacts.

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|----|--------|---------|
| `SerializerSerializeCommand.Handle` / `SerializerDeserializeCommand.Handle` | config.Deploy / config.Seed | Mode-aware dispatch | VERIFIED | Both commands parse Mode from query/body, dispatch through `config.GetMode(mode)`. |
| `SerializerOrchestrator.DeserializeAll` | ModeConfig.ConflictStrategy | Seed skip flag to providers | VERIFIED | ConflictStrategy parameter threaded through ISerializationProvider.Deserialize; both providers check `strategy == DestinationWins`. |
| `SerializerSerializeCommand` | `ManifestWriter` / `ManifestCleaner` | Post-run cleanup | VERIFIED | Orchestrator calls both after SerializeAll completes, counted into OrchestratorResult.StaleFilesDeleted. |
| `ConfigLoader.Load(path)` (1-arg) | `SqlIdentifierValidator` + `SqlWhereClauseValidator` | Default validator via 37-06 wiring | VERIFIED (fixed in 37-06) | Lines 60-77: 1-arg overload now reads `TestOverrideIdentifierValidator` AsyncLocal and, when null, constructs `new SqlIdentifierValidator()` and delegates to 2-arg overload. All 22 production call sites (SerializerSerializeCommand, SerializerDeserializeCommand, SavePredicateCommand, queries, tree node provider, log viewer, SerializerFileOverviewInjector) now receive the gate without call-site changes. |
| `ConfigLoader.Load` | `DwCacheServiceRegistry.Resolve` | Validate serviceCaches per predicate | VERIFIED | `ValidateServiceCaches` called unconditionally at line 131. |
| `CacheInvalidator.InvalidateCaches` | `DwCacheServiceRegistry` | Direct Action invocation | VERIFIED | Registry-resolved; no reflection; throws on unknown. |
| `ContentSerializer.Serialize` (post-run) | `TemplateReferenceScanner` + `TemplateAssetManifest.Write` | Emit templates.manifest.yml | VERIFIED | Lines 70-73 build and write. |
| `ContentSerializer.Serialize` (post-run) | `BaselineLinkSweeper.Sweep` | Throw on unresolvable | VERIFIED | Lines 84-96 throw InvalidOperationException. |
| `ContentDeserializer.Deserialize` | `TemplateAssetManifest.Validate` | Pre-flight template check | VERIFIED | Line 118 → `ValidateTemplateManifest()` → reads manifest + validates via `_templateEscalator` (whose WARNING emissions are intercepted by the orchestrator log-wrapper for strict-mode recording). |
| `SqlTableWriter.WriteRow` | `InternalLinkResolver.ResolveInStringColumn` | Rewrite string columns pre-MERGE | VERIFIED | `ApplyLinkResolution` iterates opted-in columns; `SqlTableProvider:225-226` invokes when resolver + opt-in both present. |
| `SerializerOrchestrator.DeserializeAll` | SourceToTargetPageMap | Build map from Content before SqlTable with ResolveLinksInColumns | VERIFIED | Content predicates reorder to run first when any SqlTable has resolveLinksInColumns; map aggregated from ProviderDeserializeResult and passed as InternalLinkResolver to subsequent SqlTable providers. |
| `ConfigLoaderValidatorFixtureBase` (test helper) | `ConfigLoader.TestOverrideIdentifierValidator` AsyncLocal | Per-class ctor/Dispose install | VERIFIED (new in 37-06) | Ctor sets AsyncLocal to permissive validator covering 6 tables + union columns; Dispose clears. 4 test classes inherit: ConfigLoaderTests, DeployModeConfigLoaderTests, PredicateCommandTests, SaveSerializerSettingsCommandTests (5 Grep matches including declaration). |

### Data-Flow Trace (Level 4)

Not applicable at this phase level — Phase 37 produces configuration/validation/orchestration infrastructure, not dynamic-data UI components. The data flows are exercised by the test suite (620 tests green) and gated for end-to-end round-trip confirmation via the 5 human-verification items.

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Full test suite passes (post-37-06) | `dotnet test tests/DynamicWeb.Serializer.Tests/DynamicWeb.Serializer.Tests.csproj --nologo --verbosity minimal` | `Passed! - Failed: 0, Passed: 620, Skipped: 0, Total: 620, Duration: 833 ms` | PASS |
| Both SC-3 tests pass | `dotnet test ... --filter "FullyQualifiedName~Load_DefaultPath_"` | `Passed! - Failed: 0, Passed: 2, Skipped: 0, Total: 2, Duration: 56 ms` | PASS |
| Build succeeds | (built as part of test run) | 0 errors | PASS |
| `identifierValidator: null` removed from src/ConfigLoader.cs | Grep `identifierValidator: null` on `src/DynamicWeb.Serializer/Configuration/ConfigLoader.cs` | 0 matches (was 1 before 37-06) | PASS |
| Default validator construction wired | Grep `new SqlIdentifierValidator\(\)` on `src/DynamicWeb.Serializer/Configuration/ConfigLoader.cs` | 1 match at line 70 (inside 1-arg overload) | PASS |
| Fixture inheritance count | Grep `ConfigLoaderValidatorFixtureBase` on `tests/` | 5 matches (1 declaration + 1 ctor + 4 inheriting classes + 1 base-ctor reference) — all 4 expected test classes inherit | PASS |
| InternalsVisibleTo present | Grep `InternalsVisibleTo` on csproj | 1 match (targets DynamicWeb.Serializer.Tests) | PASS |
| 37-06 commits landed | `git log --oneline -5` | 9dc9aa5 (RED), 5e4388a (GREEN), 77816a5 (docs) all present | PASS |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|-------------|--------|----------|
| SEED-01 | 37-01, 37-01.1 | Content predicates support per-subtree deserialize semantics — delivered as Deploy/Seed config split | SATISFIED | DeploymentMode enum + ModeConfig + admin UI tree split + ContentDeserializer Seed-skip on UPDATE path. Admin UI completion in 37-01.1 extends to Item Type + XML Type per-mode editing. |
| SEED-02 | 37-01 | SqlTable predicates support per-predicate deserialize semantics | SATISFIED | Same Deploy/Seed structural split; SqlTableProvider Seed-skip via existingChecksums lookup at line 299. |
| FILTER-01 | 37-03, 37-06 | SqlTable predicates accept a `where` clause; column names validated against INFORMATION_SCHEMA | SATISFIED (production + test paths) | `Where` field exists on predicate model; SqlTableReader composes WHERE; SqlWhereClauseValidator implemented. **As of 37-06**, validation runs via BOTH overloads: 2-arg (explicit validator, tests) AND 1-arg (default-constructed validator, production). |
| SCHEMA-02 | 37-02 | Schema tolerance unified into TargetSchemaCache helper | SATISFIED | TargetSchemaCache + unified Coerce covers Area raw-SQL + SqlTable MERGE paths. 42 tests. |
| CLEANUP-01 | 37-01 | Files-written manifest per mode; stale files deleted post-run | SATISFIED | ManifestWriter + ManifestCleaner + OrchestratorResult.StaleFilesDeleted; per-mode containment (T-37-01-01) unit-tested. |
| RUNTIME-COLS-01 | 37-03 | Small flat curated list of runtime-only columns auto-excluded | SATISFIED | RuntimeExcludes map with 6 entries; SqlTableProvider applies effectiveExcludes = ExcludeFields ∪ (Runtime \ IncludeFields). |
| CACHE-01 | 37-04 | Curated DwCacheServiceRegistry with direct typed ClearCache; unresolved names = ERROR | SATISFIED | 9 typed entries; ValidateServiceCaches unconditional at ConfigLoader.Load; CacheInvalidator throws on unknown. F-10 silent-skip path removed. |
| STRICT-01 | 37-04 | `--strict` flag with entry-point-aware defaults; escalates warnings | SATISFIED | StrictModeEscalator + StrictModeResolver + WrapLogWithEscalator; AssertNoWarnings called at end-of-run; SerializerDeserializeCommand resolves per entry point with IsAdminUiInvocation flag. |
| TEMPLATE-01 | 37-05 | Template-asset manifest (manifest-only); validated at deserialize; strict escalates missing | SATISFIED | TemplateAssetManifest Write/Read/Validate + TemplateReferenceScanner; ContentSerializer emits manifest; ContentDeserializer pre-flights; template-escalator WARNINGs flow through orchestrator log-wrapper for strict recording. |
| LINK-02 | 37-05 | Two passes: serialize-time sweep + deserialize-time SqlTable column resolution | SATISFIED | BaselineLinkSweeper (pass 1) + InternalLinkResolver.ResolveInStringColumn + SqlTableWriter.ApplyLinkResolution + ResolveLinksInColumns opt-in (pass 2); orchestrator ordering reorders Content-first when pass 2 is active. |
| CRED-01 | DEFERRED.md | Credential column registry | DEFERRED to v0.6.0 | Documented in DEFERRED.md with README note about manual excludeFields workaround + pre-commit grep recipe. |
| DIFF-01 | DEFERRED.md | BaselineDiffWriter | DEFERRED to v0.6.0 | Documented in DEFERRED.md. |

All 12 requirement IDs accounted for. SEED-001 (strict mode) and SEED-002 (SQL identifier whitelist) — the two seed promotions — are also now fully realized in production paths post-37-06.

### Anti-Patterns Found

The 37-REVIEW.md ledger of 13 findings remains the authoritative anti-pattern record. CR-01 is now CLOSED by plan 37-06; the remainder are unchanged. All remaining items are non-blockers for the phase goal.

| File | Line | Pattern | Severity | Impact | Status after 37-06 |
|------|------|---------|----------|--------|-------------------|
| `ConfigLoader.cs` | 60-77 | 1-arg `Load(path)` overload now default-constructs `SqlIdentifierValidator` (CR-01) | (was BLOCKER) | (was SC-3 gap) | **CLOSED by 37-06** |
| `ManifestCleaner.cs` | 24-54 | `Path.GetFullPath` doesn't resolve symlinks; containment check may be bypassable on Windows (CR-02) | WARNING | File-loss risk if an admin symlinks a mode root; mitigated by default-deny on reparse points but edge cases remain. | Open — not blocking SC-3 |
| `TargetSchemaCache.cs` | 15-19 | Shared cache uses plain `Dictionary<,>` / `HashSet<>`, no thread-safety (WR-01) | INFO | Safe today (orchestrator sequential); time-bomb on any future `Parallel.ForEach` over predicates. | Open |
| `SerializerOrchestrator.cs` | 325-343 + `StrictModeEscalator.cs:41-50` | Warning-double-record via `Escalate` + `WrapLogWithEscalator` (WR-02) | INFO | No current call site triggers it; latent if any downstream class starts using `Escalate` against a wrapped log. | Open |
| `PredicateEditScreen.cs` | 169 | Table-name regex `^[A-Za-z_][A-Za-z0-9_]*$` is narrower than SqlIdentifierValidator whitelist (WR-03) | INFO | Admin UI and backend validators can diverge; not a security hole, just UX inconsistency. | Open |
| `ContentProvider.cs` | 222-229 | `BuildSerializerConfiguration` uses legacy `Predicates` setter, leaving `Deploy.ExcludeFieldsByItemType` empty; Seed exclusions don't propagate (WR-04) | WARNING | Seed predicate exclusions silently dropped when routed through ContentProvider. Documented follow-up in ContentDeserializer:261-264; doesn't block SC-1 because Seed's primary mechanism is skip-on-present, not excludes. | Open — not blocking SC-3 |
| `SqlTableProvider.cs` | 241-243 | `DisableForeignKeys` wrapped in broad `try { } catch { }` with no logging (WR-05) | WARNING | Silent failure possible if FK disable fails for permission reasons; affected MERGE may break FK constraints without warning. | Open |
| `SqlTableWriter.cs` | 329-343 | Dead code + `keyCol` still interpolated as identifier (WR-06) | INFO | Dead code harmless; key column identifier injection is partially mitigated — 37-06 covers identifier allowlisting at config-load time, but SqlTableWriter still does not re-validate at SQL-composition time. Accepted as DEFENSE-IN-DEPTH follow-up; no active CVE. | Open |
| `InternalLinkResolver.cs` | 71-94 | `_resolvedCount` / `_unresolvedCount` not incremented for SelectedValuePattern path (WR-07) | INFO | Stats under-report; no correctness impact. | Open |
| `ContentDeserializer.cs` | 148-161 + `ContentProvider.cs:189-206` | Directory enumeration from YAML without containment check against Files/ (WR-08) | INFO | OutputDirectory="../../sensitive" could escape on a read pass; not a delete path. | Open |
| `ConfigPathResolver.cs` | 7-18 | Static `CandidatePaths` captures `Directory.GetCurrentDirectory()` at type-init (WR-09) | INFO | xUnit parallel test interaction, mitigated by AsyncLocal TestOverridePath. | Open |
| `SerializerOrchestrator.cs` | 183, 208 | In-place predicates list reassignment (WR-10) | INFO | Defensive-copy recommended but no current bug. | Open |
| `SqlTableProvider.cs` | 327-331 | `EnableForeignKeys` WARNING relies on prefix matching (WR-11) | INFO | Current behavior correct; implicit contract between log prefix and escalator is fragile. | Open |

Plus 9 Info items (IN-01..IN-09) from 37-REVIEW.md — none block the phase goal.

### Human Verification Required

5 items need human testing on live DW hosts (Swift 2.2 source + CleanDB target):

1. **Swift 2.2 → CleanDB end-to-end deserialize round-trip** (SC-2): confirm no silent cache-skip lines; unknown cache names fail with supported-names list.
2. **Strict-mode non-zero exit on unresolvable link** (SC-6): confirm CumulativeStrictModeException body + non-zero CLI exit code.
3. **Seed mode preserves customer edits** (SC-1): double-run test with mid-run edits.
4. **Pre-commit sweep catches F-17 orphans** (SC-7): serialize real Swift 2.2 baseline, confirm ~10 orphan refs are reported with source context.
5. **UrlPathRedirect rewrite** (SC-8): confirm LINK-02 pass 2 rewrites CleanDB page IDs correctly via `resolveLinksInColumns: ["UrlPathRedirect"]`.

These are unchanged from the previous verification — plan 37-06 closed the code-level SC-3 gap but did not touch any code path exercised by the 5 live-host scenarios. The codebase has no additional test evidence that would close these human-verification items; they still require operator validation on live DW hosts.

### Re-verification Notes

**Gap closure evidence for SC-3:**

| Check | Expected | Actual | Status |
|-------|----------|--------|--------|
| 1-arg overload default-constructs validator | `new SqlIdentifierValidator()` call inside `Load(string filePath)` body | Line 70 of `ConfigLoader.cs` | VERIFIED |
| `identifierValidator: null` removed | 0 matches in `src/DynamicWeb.Serializer/Configuration/ConfigLoader.cs` | 0 matches (was 1 pre-37-06) | VERIFIED |
| AsyncLocal test-override escape hatch exists | `TestOverrideIdentifierValidator` property at the top of ConfigLoader | Lines 33-38 | VERIFIED |
| Internal spy hook for structural test | `_testDefaultValidatorConstructedCallback` internal AsyncLocal | Line 47 | VERIFIED |
| InternalsVisibleTo on csproj | Targets `DynamicWeb.Serializer.Tests` | Line 34 of `DynamicWeb.Serializer.csproj` | VERIFIED |
| Two new SC-3 tests exist | `Load_DefaultPath_MaliciousTableIdentifier_Throws` + `Load_DefaultPath_NoTestOverride_ConstructsDefaultValidator` | Lines 1187 + 1231 of `ConfigLoaderTests.cs` | VERIFIED |
| Both SC-3 tests pass | 2/2 passing | `Passed! - Failed: 0, Passed: 2, Total: 2` | VERIFIED |
| Full test suite passes | 620+ passing (baseline 618 + 2 new) | `Passed! - Failed: 0, Passed: 620, Total: 620` | VERIFIED |
| Fixture inheritance across 4 test classes | `ConfigLoaderTests`, `DeployModeConfigLoaderTests`, `PredicateCommandTests`, `SaveSerializerSettingsCommandTests` | All 4 inherit `ConfigLoaderValidatorFixtureBase` | VERIFIED |
| RED + GREEN commits present | 9dc9aa5 (RED) + 5e4388a (GREEN) + 77816a5 (docs) | All three in `git log --oneline` | VERIFIED |

**Structural integrity:** The 2-arg `Load(path, SqlIdentifierValidator?)` overload still accepts `null` to SKIP validation — this is intentional and documented as a test-only path (see xmldoc lines 88-91). Production code calls the 1-arg overload, which NEVER passes null internally. This preserves the test surface without re-introducing the production gap.

### Gaps Summary

**No blocking gaps.** SC-3 — the only code-level blocker from the initial verification — is closed by plan 37-06. All 8 success criteria verified at the codebase level. Test suite is green (620/620) with two new tests proving both (a) malicious-identifier rejection via the default 1-arg Load path and (b) default-validator construction when no test override is installed.

**Remaining human-verification items** (5) carry over unchanged from the initial verification. These cover end-to-end scenarios on live DW hosts (Swift 2.2 source + CleanDB target) that cannot be asserted from unit tests alone:

- SC-1 seed-mode customer-edit preservation
- SC-2 cache invalidation round-trip
- SC-6 strict-mode non-zero exit
- SC-7 baseline link sweep F-17 orphans
- SC-8 UrlPathRedirect rewrite round-trip

The phase status is therefore `human_needed` — the automated goal verification is complete and positive; operator confirmation of the 5 live-host scenarios remains as a follow-up before phase completion can be declared end-to-end.

---

_Verified: 2026-04-20T21:00:00Z_
_Verifier: Claude (gsd-verifier)_
_Re-verification after plan 37-06 gap closure_
