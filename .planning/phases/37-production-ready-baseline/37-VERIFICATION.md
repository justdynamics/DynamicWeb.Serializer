---
phase: 37-production-ready-baseline
verified: 2026-04-20T00:00:00Z
status: gaps_found
score: 7/8 must-haves verified
overrides_applied: 0
gaps:
  - truth: "Config with SQL-injection-style identifiers rejected at config-load before any SQL runs"
    status: partial
    reason: "Validator infrastructure exists and is correct, but every production call site uses the 1-arg ConfigLoader.Load(path) overload which passes identifierValidator=null, skipping the ValidateIdentifiers gate. SC-3 and FILTER-01/SEED-002 are therefore not enforced end-to-end in production paths — only in tests. This is the same finding as 37-REVIEW.md CR-01."
    artifacts:
      - path: "src/DynamicWeb.Serializer/Configuration/ConfigLoader.cs"
        issue: "Line 18: 'public static SerializerConfiguration Load(string filePath) => Load(filePath, identifierValidator: null);' — default overload skips identifier validation. 22 call sites in src/DynamicWeb.Serializer/ use the 1-arg form, including SerializerSerializeCommand, SerializerDeserializeCommand, SavePredicateCommand, every query, and SerializerSettingsNodeProvider. None construct a SqlIdentifierValidator."
      - path: "src/DynamicWeb.Serializer/AdminUI/Commands/SerializerDeserializeCommand.cs"
        issue: "Line 70 loads config without identifier validator — this is the API/CLI entry point (CI/CD target) that SC-3 most directly governs."
      - path: "src/DynamicWeb.Serializer/AdminUI/Commands/SerializerSerializeCommand.cs"
        issue: "Line 56 loads config without identifier validator — same gap on the serialize side."
      - path: "src/DynamicWeb.Serializer/Providers/SqlTable/SqlTableReader.cs"
        issue: "Line ~29 composes `SELECT * FROM [{tableName}]` with TableName interpolated as SQL identifier. No runtime re-validation in SqlTableReader or SqlTableProvider.Serialize / Deserialize — the code trusts that ConfigLoader validated the identifier, but the default ConfigLoader path doesn't."
    missing:
      - "Make the 1-arg ConfigLoader.Load(path) overload always construct a default SqlIdentifierValidator() and delegate to the 2-arg overload — OR add a belt-and-braces validation pass in SqlTableProvider.Serialize/Deserialize that runs SqlIdentifierValidator.ValidateTable(predicate.Table!) before any SQL composition."
      - "Add an integration test asserting that loading a config file with a malicious Table identifier (e.g. 'EcomOrders] WHERE 1=1; DROP TABLE X; --') via the default ConfigLoader.Load path throws InvalidOperationException before any SQL is issued."
      - "Wire ProviderRegistry.CreateDefault to inject a shared SqlIdentifierValidator into SqlTableProvider so the registry path automatically runs the gate."
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

**Verified:** 2026-04-20
**Status:** gaps_found (1 partial + 5 human verification items)
**Re-verification:** No — initial verification.

## Goal Achievement

### Observable Truths (ROADMAP Success Criteria)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Deploy mode overwrites (source-wins preserved); Seed mode skips pages whose PageUniqueId is already on target | VERIFIED | `ContentDeserializer.cs:628-630` Seed-skip branch on UPDATE path; `SqlTableProvider.cs:299` DestinationWins skip via existingChecksums lookup; `ContentProvider.cs:142-143` log line confirms wiring. Tests: ItemTypeCommandTests, DeployModeConfigLoaderTests, StrictModeIntegrationTests. |
| 2 | Swift 2.2 → CleanDB round-trip: zero ERROR lines from cache invalidation; unknown cache names fail at config-load | VERIFIED | `DwCacheServiceRegistry.cs` has 9 typed cache entries; `CacheInvalidator.cs:49-54` throws on unknown names; `ConfigLoader.cs:67` calls `ValidateServiceCaches` unconditionally (even via 1-arg overload). TranslationLanguageService IS absent from the registry and swift2.2-baseline.json DOES reference it — loading the bundled baseline will fail loud, which IS the CACHE-01 contract. |
| 3 | Config with SQL-injection-style identifiers rejected at config-load before any SQL runs | FAILED | `ConfigLoader.cs:18` — 1-arg `Load(path)` overload passes `identifierValidator: null`, skipping `ValidateIdentifiers`. **Every** production call site uses the 1-arg form (22 locations; see 37-REVIEW.md CR-01). SC-3 therefore passes in tests (which use the 2-arg overload) but not in any production path. Malicious Table="X; DROP TABLE Y;--" in a hand-edited config flows through ConfigLoader unblocked; SqlTableReader/SqlTableWriter interpolate TableName as a SQL identifier with no runtime re-validation. |
| 4 | Default serialize of EcomShops excludes env-specific search-index columns via RuntimeExcludes | VERIFIED | `RuntimeExcludes.cs:30-37` registers ShopIndexRepository/Name/DocumentType/Update/Builder for EcomShops + UrlPathVisitsCount for UrlPath; `SqlTableProvider.cs` computes `effectiveExcludes = ExcludeFields ∪ (RuntimeExcludes[table] \ IncludeFields)` and logs auto-exclusions. IncludeFields per-predicate opt-in works. Tests: RuntimeExcludesTests, SqlTableProviderSerializeTests. |
| 5 | Missing template files surfaced pre-deserialize via TemplateAssetManifest validation; strict mode escalates to hard failure | VERIFIED | `TemplateAssetManifest.cs` Write/Read/Validate exist; `ContentSerializer.cs:70-73` writes templates.manifest.yml post-serialize; `ContentDeserializer.cs:118,218-238` pre-flights validation before any page writes; `_templateEscalator` is lenient but WARNING lines flow through the orchestrator's log-wrapper (`SerializerOrchestrator.WrapLogWithEscalator` line 325-343) which records them for strict-mode end-of-run assertion. Tests: TemplateAssetManifestTests, TemplateReferenceScannerTests. |
| 6 | Strict mode run with any unresolved page link or missing template exits non-zero | VERIFIED (with human_needed for end-to-end) | `StrictModeEscalator.cs` + `CumulativeStrictModeException` + `StrictModeResolver` complete; `SerializerOrchestrator.DeserializeAll:307` calls `escalator.AssertNoWarnings()` at end-of-run; `WrapLogWithEscalator:325-343` intercepts every WARNING line; `SerializerDeserializeCommand` resolves strict per entry point (API/CLI=ON, AdminUi=OFF) via D-16. Tests: StrictModeEscalatorTests (19), StrictModeIntegrationTests (9). Real end-to-end on live DB needs human verification. |
| 7 | Serialize fails with pre-commit sweep when YAML tree contains Default.aspx?ID=N refs pointing outside baseline (F-07/F-17 via D-22 pass 1) | VERIFIED | `BaselineLinkSweeper.cs` walks pages recursively, matches InternalLinkPattern + SelectedValuePattern against valid SourcePageId set; `ContentSerializer.cs:84-96` runs sweep post-serialize and throws InvalidOperationException with per-reference breakdown on any orphan. ContentProvider catches the exception and returns SerializeResult.Errors — CI/CD sees the non-zero exit via orchestrator-result error flag. Tests: BaselineLinkSweeperTests (13). |
| 8 | SqlTable predicates opt-in to link resolution via `resolveLinksInColumns`; UrlPath.UrlPathRedirect rewrites source→target (D-22 pass 2) | VERIFIED (with human_needed for round-trip) | `ProviderPredicateDefinition.ResolveLinksInColumns` exists; `SqlTableWriter.ApplyLinkResolution:175-187` rewrites string columns via `resolver.ResolveInStringColumn`; `SqlTableProvider.cs:225-234` invokes on rows when resolver + opt-in are both present; `ContentProvider.BuildSourceToTargetMap:216` builds the map post-deserialize; `SerializerOrchestrator.DeserializeAll` conditionally reorders Content predicates to run before SqlTable when any predicate opts in (decision A). Tests: SqlTableLinkResolutionIntegrationTests (6). Live round-trip on CleanDB target needs human verification. |

**Score:** 7/8 truths verified, 1 FAILED (SC-3)

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
| `src/DynamicWeb.Serializer/Configuration/SqlIdentifierValidator.cs` | Validates table/column names against INFORMATION_SCHEMA | VERIFIED | Present, ValidateTable + ValidateColumn + GetColumns methods exist; test-ctor loader hook. |
| `src/DynamicWeb.Serializer/Configuration/SqlWhereClauseValidator.cs` | Tokenizes + rejects SQLi patterns + unknown identifiers | VERIFIED | Present, BannedTokens/BannedKeywords/literal-stripping logic. 24 tests. |
| `src/DynamicWeb.Serializer/Configuration/RuntimeExcludes.cs` | Curated map tableName → runtime-only columns | VERIFIED | Present, 6 entries (UrlPath + 5× EcomShops). D-07 single-list model. |
| `src/DynamicWeb.Serializer/Infrastructure/TemplateAssetManifest.cs` | Manifest-only template tracking with validation | VERIFIED | Present, Write/Read/Validate + TemplateReference record. Path-traversal + DoS guards. |
| `src/DynamicWeb.Serializer/Infrastructure/TemplateReferenceScanner.cs` | Extracts layout/item-type/grid-row refs from SerializedPage trees | VERIFIED | Present, walks recursive children + collects referencedBy. |
| `src/DynamicWeb.Serializer/Infrastructure/BaselineLinkSweeper.cs` | Post-serialize sweep for Default.aspx?ID=N orphans | VERIFIED | Present, matches InternalLinkPattern + SelectedValuePattern. |
| `src/DynamicWeb.Serializer/Models/ProviderPredicateDefinition.cs` | Adds Where, IncludeFields, ResolveLinksInColumns | VERIFIED | All three fields present as optional properties. |
| `src/DynamicWeb.Serializer/Providers/CacheInvalidator.cs` | Rewritten to use DwCacheServiceRegistry, throws on unknown | VERIFIED | 61 lines. Old ICacheResolver/ICacheInstance deleted. Throws with supported-names listing. |

All 15 artifacts present and substantive. No STUB or MISSING artifacts.

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|----|--------|---------|
| `SerializerSerializeCommand.Handle` / `SerializerDeserializeCommand.Handle` | config.Deploy / config.Seed | Mode-aware dispatch | VERIFIED | Both commands parse Mode from query/body, dispatch through `config.GetMode(mode)`. |
| `SerializerOrchestrator.DeserializeAll` | ModeConfig.ConflictStrategy | Seed skip flag to providers | VERIFIED | ConflictStrategy parameter threaded through ISerializationProvider.Deserialize; both providers check `strategy == DestinationWins`. |
| `SerializerSerializeCommand` | `ManifestWriter` / `ManifestCleaner` | Post-run cleanup | VERIFIED | Orchestrator calls both after SerializeAll completes, counted into OrchestratorResult.StaleFilesDeleted. |
| `ConfigLoader.Load` | `SqlIdentifierValidator` + `SqlWhereClauseValidator` | Validate predicates at load | NOT_WIRED (production) | 2-arg overload wires the validator; 1-arg overload (used by every production call site) does NOT. This is the root cause of SC-3 failure. |
| `ConfigLoader.Load` | `DwCacheServiceRegistry.Resolve` | Validate serviceCaches per predicate | VERIFIED | `ValidateServiceCaches` called unconditionally at line 67. |
| `CacheInvalidator.InvalidateCaches` | `DwCacheServiceRegistry` | Direct Action invocation | VERIFIED | Registry-resolved; no reflection; throws on unknown. |
| `ContentSerializer.Serialize` (post-run) | `TemplateReferenceScanner` + `TemplateAssetManifest.Write` | Emit templates.manifest.yml | VERIFIED | Lines 70-73 build and write. |
| `ContentSerializer.Serialize` (post-run) | `BaselineLinkSweeper.Sweep` | Throw on unresolvable | VERIFIED | Lines 84-96 throw InvalidOperationException. |
| `ContentDeserializer.Deserialize` | `TemplateAssetManifest.Validate` | Pre-flight template check | VERIFIED | Line 118 → `ValidateTemplateManifest()` → reads manifest + validates via `_templateEscalator` (whose WARNING emissions are intercepted by the orchestrator log-wrapper for strict-mode recording). |
| `SqlTableWriter.WriteRow` | `InternalLinkResolver.ResolveInStringColumn` | Rewrite string columns pre-MERGE | VERIFIED | `ApplyLinkResolution` iterates opted-in columns; `SqlTableProvider:225-226` invokes when resolver + opt-in both present. |
| `SerializerOrchestrator.DeserializeAll` | SourceToTargetPageMap | Build map from Content before SqlTable with ResolveLinksInColumns | VERIFIED | Content predicates reorder to run first when any SqlTable has resolveLinksInColumns; map aggregated from ProviderDeserializeResult and passed as InternalLinkResolver to subsequent SqlTable providers. |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Full test suite passes | `dotnet test tests/DynamicWeb.Serializer.Tests/DynamicWeb.Serializer.Tests.csproj --nologo --verbosity quiet` | `Passed! Failed: 0, Passed: 618, Skipped: 0, Total: 618, Duration: 719 ms` | PASS |
| Build succeeds | `dotnet build` | 0 errors (confirmed by test run which builds first) | PASS |
| Phase 37 artifacts exist | File existence check on 13 key infrastructure files | All 13 files found | PASS |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|-------------|--------|----------|
| SEED-01 | 37-01, 37-01.1 | Content predicates support per-subtree deserialize semantics — delivered as Deploy/Seed config split | SATISFIED | DeploymentMode enum + ModeConfig + admin UI tree split + ContentDeserializer Seed-skip on UPDATE path. Admin UI completion in 37-01.1 extends to Item Type + XML Type per-mode editing. |
| SEED-02 | 37-01 | SqlTable predicates support per-predicate deserialize semantics | SATISFIED | Same Deploy/Seed structural split; SqlTableProvider Seed-skip via existingChecksums lookup at line 299. |
| FILTER-01 | 37-03 | SqlTable predicates accept a `where` clause; column names validated against INFORMATION_SCHEMA | SATISFIED (test path) / BLOCKED (production path) | `Where` field exists on predicate model; SqlTableReader composes WHERE; SqlWhereClauseValidator implemented. But validation only runs when ConfigLoader called with 2-arg overload — production uses 1-arg. |
| SCHEMA-02 | 37-02 | Schema tolerance unified into TargetSchemaCache helper | SATISFIED | TargetSchemaCache + unified Coerce covers Area raw-SQL + SqlTable MERGE paths. 42 tests. |
| CLEANUP-01 | 37-01 | Files-written manifest per mode; stale files deleted post-run | SATISFIED | ManifestWriter + ManifestCleaner + OrchestratorResult.StaleFilesDeleted; per-mode containment (T-37-01-01) unit-tested. |
| RUNTIME-COLS-01 | 37-03 | Small flat curated list of runtime-only columns auto-excluded | SATISFIED | RuntimeExcludes map with 6 entries; SqlTableProvider applies effectiveExcludes = ExcludeFields ∪ (Runtime \ IncludeFields). |
| CACHE-01 | 37-04 | Curated DwCacheServiceRegistry with direct typed ClearCache; unresolved names = ERROR | SATISFIED | 9 typed entries; ValidateServiceCaches unconditional at ConfigLoader.Load; CacheInvalidator throws on unknown. F-10 silent-skip path removed. |
| STRICT-01 | 37-04 | `--strict` flag with entry-point-aware defaults; escalates warnings | SATISFIED | StrictModeEscalator + StrictModeResolver + WrapLogWithEscalator; AssertNoWarnings called at end-of-run; SerializerDeserializeCommand resolves per entry point with IsAdminUiInvocation flag. |
| TEMPLATE-01 | 37-05 | Template-asset manifest (manifest-only); validated at deserialize; strict escalates missing | SATISFIED | TemplateAssetManifest Write/Read/Validate + TemplateReferenceScanner; ContentSerializer emits manifest; ContentDeserializer pre-flights; template-escalator WARNINGs flow through orchestrator log-wrapper for strict recording. |
| LINK-02 | 37-05 | Two passes: serialize-time sweep + deserialize-time SqlTable column resolution | SATISFIED | BaselineLinkSweeper (pass 1) + InternalLinkResolver.ResolveInStringColumn + SqlTableWriter.ApplyLinkResolution + ResolveLinksInColumns opt-in (pass 2); orchestrator ordering reorders Content-first when pass 2 is active. |
| CRED-01 | DEFERRED.md | Credential column registry | DEFERRED to v0.6.0 | Documented in DEFERRED.md with README note about manual excludeFields workaround + pre-commit grep recipe. |
| DIFF-01 | DEFERRED.md | BaselineDiffWriter | DEFERRED to v0.6.0 | Documented in DEFERRED.md. |

All 12 requirement IDs accounted for. SEED-001 and SEED-002 (seed promotions, not ROADMAP REQs) also completed.

### Anti-Patterns Found

All 13 review findings from 37-REVIEW.md are recorded below with their severity mapped to verifier categories. Note: the Phase 37 REVIEW ran 2026-04-20 and its critical/warning/info findings are the authoritative anti-pattern ledger for this phase.

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `ConfigLoader.cs` | 18 | 1-arg `Load(path)` overload skips identifier validation in production paths (CR-01) | BLOCKER | Opens SQL identifier injection surface; SC-3 not enforced end-to-end; this is the primary gap for Phase 37. |
| `ManifestCleaner.cs` | 24-54 | `Path.GetFullPath` doesn't resolve symlinks; containment check may be bypassable on Windows (CR-02) | WARNING | File-loss risk if an admin symlinks a mode root; mitigated by default-deny on reparse points but edge cases remain. |
| `TargetSchemaCache.cs` | 15-19 | Shared cache uses plain `Dictionary<,>` / `HashSet<>`, no thread-safety (WR-01) | INFO | Safe today (orchestrator sequential); time-bomb on any future `Parallel.ForEach` over predicates. |
| `SerializerOrchestrator.cs` | 325-343 + `StrictModeEscalator.cs:41-50` | Warning-double-record via `Escalate` + `WrapLogWithEscalator` (WR-02) | INFO | No current call site triggers it; latent if any downstream class starts using `Escalate` against a wrapped log. |
| `PredicateEditScreen.cs` | 169 | Table-name regex `^[A-Za-z_][A-Za-z0-9_]*$` is narrower than SqlIdentifierValidator whitelist (WR-03) | INFO | Admin UI and backend validators can diverge; not a security hole, just UX inconsistency. |
| `ContentProvider.cs` | 222-229 | `BuildSerializerConfiguration` uses legacy `Predicates` setter, leaving `Deploy.ExcludeFieldsByItemType` empty; Seed exclusions don't propagate (WR-04) | WARNING | Seed predicate exclusions silently dropped when routed through ContentProvider. Documented follow-up in ContentDeserializer:261-264; doesn't block SC-1 because Seed's primary mechanism is skip-on-present, not excludes. |
| `SqlTableProvider.cs` | 241-243 | `DisableForeignKeys` wrapped in broad `try { } catch { }` with no logging (WR-05) | WARNING | Silent failure possible if FK disable fails for permission reasons; affected MERGE may break FK constraints without warning. |
| `SqlTableWriter.cs` | 329-343 | Dead code + `keyCol` still interpolated as identifier (WR-06) | INFO | Dead code harmless; key column identifier injection is same category as CR-01. |
| `InternalLinkResolver.cs` | 71-94 | `_resolvedCount` / `_unresolvedCount` not incremented for SelectedValuePattern path (WR-07) | INFO | Stats under-report; no correctness impact. |
| `ContentDeserializer.cs` | 148-161 + `ContentProvider.cs:189-206` | Directory enumeration from YAML without containment check against Files/ (WR-08) | INFO | OutputDirectory="../../sensitive" could escape on a read pass; not a delete path. |
| `ConfigPathResolver.cs` | 7-18 | Static `CandidatePaths` captures `Directory.GetCurrentDirectory()` at type-init (WR-09) | INFO | xUnit parallel test interaction, mitigated by AsyncLocal TestOverridePath. |
| `SerializerOrchestrator.cs` | 183, 208 | In-place predicates list reassignment (WR-10) | INFO | Defensive-copy recommended but no current bug. |
| `SqlTableProvider.cs` | 327-331 | `EnableForeignKeys` WARNING relies on prefix matching (WR-11) | INFO | Current behavior correct; implicit contract between log prefix and escalator is fragile. |

Plus 9 Info items (IN-01..IN-09) from 37-REVIEW.md — none block the phase goal.

### Human Verification Required

5 items need human testing on live DW hosts (Swift 2.2 source + CleanDB target):

1. **Swift 2.2 → CleanDB end-to-end deserialize round-trip** (SC-2): confirm no silent cache-skip lines; unknown cache names fail with supported-names list.
2. **Strict-mode non-zero exit on unresolvable link** (SC-6): confirm CumulativeStrictModeException body + non-zero CLI exit code.
3. **Seed mode preserves customer edits** (SC-1): double-run test with mid-run edits.
4. **Pre-commit sweep catches F-17 orphans** (SC-7): serialize real Swift 2.2 baseline, confirm ~10 orphan refs are reported with source context.
5. **UrlPathRedirect rewrite** (SC-8): confirm LINK-02 pass 2 rewrites CleanDB page IDs correctly via `resolveLinksInColumns: ["UrlPathRedirect"]`.

### Gaps Summary

**One blocking gap:** SC-3 (SQL identifier validation at config-load) is not enforced in production paths. The infrastructure (SqlIdentifierValidator + SqlWhereClauseValidator + ValidateIdentifiers helper) is fully implemented and tested, but every production call site uses the 1-arg `ConfigLoader.Load(path)` overload which passes `identifierValidator: null`, silently skipping the validation pass. This is 37-REVIEW.md's CR-01 finding — same root cause.

**Risk:** The phase goal states "safe to run in an automated Azure deployment pipeline without... silently corrupting FK integrity". An admin-user-supplied malicious `Table` string (e.g. via manually-edited Serializer.config.json, which is an explicitly-supported workflow per `SerializerSettingsModel.ConfigFilePath`) flows through `SqlTableReader`/`SqlTableWriter`'s `[{tableName}]` SQL-text interpolation unchecked. The admin-auth gate limits the blast radius to admin-trusted users, but the phase's explicit SEED-002 contract is specifically about not trusting the config file as a raw SQL surface.

**Fix path:** make `ConfigLoader.Load(path)` construct a default `SqlIdentifierValidator()` and delegate to the 2-arg overload, OR add a per-invocation `SqlIdentifierValidator.ValidateTable(predicate.Table!)` check in `SqlTableProvider.Serialize` / `Deserialize` before any SQL composition. Either fix would need an integration test asserting the default ConfigLoader path throws on a malicious Table identifier.

**Everything else is on-goal:** 7 of 8 Success Criteria verified; all 10 in-scope requirements satisfied; both deferred (CRED-01, DIFF-01) properly documented; all 15 key artifacts present and substantive; 618/618 tests passing; 93 files reviewed with 2 critical, 11 warning, 9 info findings in 37-REVIEW.md. The test suite is green and the architecture is sound.

---

_Verified: 2026-04-20_
_Verifier: Claude (gsd-verifier)_
