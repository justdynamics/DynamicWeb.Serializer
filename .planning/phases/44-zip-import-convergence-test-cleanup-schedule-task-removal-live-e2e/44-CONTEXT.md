# Phase 44: Zip-import convergence + test cleanup + Obsolete deletion + REVIEW fold-in — Context

**Gathered:** 2026-05-11
**Status:** Ready for planning

<domain>
## Phase Boundary

Cleanup tail of the v0.6.0 manifest pivot. After this phase: zip-import runs through the same orchestrator pipeline as full deserialize via a shared `ContentProvider.BuildContentEntryForArea` helper + a new public `SerializerOrchestrator.DeserializeAll(Manifest, contentRoot, ...)` overload; `ContentDeserializer` is `ContentEntry`-typed (no inner synthetic predicate); the test suite is free of predicate-fixture debt across 7 named files; three `[Obsolete]` overloads on `SerializerOrchestrator` + their predicate→entry bridge are gone; schedule-task absence in `src/` is ratified; the open Phase 43 REVIEW.md findings (4 warnings + 4 of 6 info items) are folded in.

**Scope dropped during discuss (2026-05-11):** CONVERGE-06 (live E2E re-validation against Swift 2.2 → CleanDB + DAP/pim.carriageservices under `strictMode: true`) was struck from v0.6.0 — see REQUIREMENTS.md note. The `tools/e2e/full-clean-roundtrip.ps1` pipeline stays in-repo for on-demand runs. Rationale: 861/861 unit + integration coverage gates the refactor; Phase 38.1 already validated the pipeline once; the wall-clock + machine setup cost of a second live E2E was not worth a second proof.

**Requirements locked by `.planning/REQUIREMENTS.md`:** CONVERGE-01..05, CONVERGE-07. **Acceptance** locked by `.planning/ROADMAP.md` SC-1..SC-5 for Phase 44.

</domain>

<decisions>
## Implementation Decisions

### Zip-import convergence shape (CONVERGE-01 + CONVERGE-02)

- **D-01:** **Add a public `DeserializeAll(Manifest manifest, string contentRoot, mode, strategy, log, isDryRun, providerFilter, escalator, ...)` overload on `SerializerOrchestrator`.** The disk-reading `DeserializeAll(modeRoot, mode, ...)` (Phase 43 surface) becomes a thin wrapper that calls `ManifestWriter.Read(modeRoot, mode)` and forwards to this new overload. Zip-import builds its in-memory `Manifest` (containing one synthesised `ContentEntry`) and calls the new overload directly. Both paths share the FK-reorder + per-entry switch + `EntryOutcome` aggregation core — single canonical dispatch site.
  - **Rejected:** writing a temp `manifest.json` on disk in `ZipImport/` (would couple `ManifestWriter`'s atomic-write semantics to a transient in-process value) and promoting the internal `DeserializeEntries` test-seam to public (would cement two public dispatch surfaces, exactly the fragmentation CONVERGE-01/02 was meant to eliminate per ARCHITECTURE.md §5).

- **D-02:** **`ContentProvider.BuildContentEntryForArea` is a `public static` method on `ContentProvider`.** Signature: `public static ContentEntry BuildContentEntryForArea(int areaId, string contentRoot, IEnumerable<string>? acknowledgedOrphanPageIds = null)`. No DW-service dependencies (pure `Directory.EnumerateFiles` + path/area population), so `static` is honest about the contract. Refactor `ContentProvider.BuildManifestEntry(predicate, ...)` to internally project `predicate` → `(areaId, contentRoot)` and call `BuildContentEntryForArea` — single canonical shape definition shared between full deserialize and zip-import.
  - **Rejected:** free-standing `Infrastructure/ContentEntryBuilder` static helper (drifts the builder away from `BuildManifestEntry`, defeats the "shared helper" goal) and instance method on `ContentProvider` (forces zip-import to DI-instantiate a provider for a stateless projection).

- **D-03:** **Zip-import wires strict-mode via the same `StrictModeResolver` literal as `SerializerDeserializeCommand`.** `DeserializeFromZipCommand` gets a `bool? StrictMode { get; set; }` property + `IsAdminUiInvocation` flag mirroring D-38-11 / Phase 37-04 D-16. Behaviour: admin-UI button → default OFF, management-API POST with `?strictMode=true` → ON, `?strictMode=false` → OFF. Call site uses the grep-friendly `StrictModeResolver.Resolve(entryPoint, configValue: null, requestValue: StrictMode)` literal. Single canonical strict-mode plumbing across all three deserialize entry points.

- **D-04:** **Pivot `ContentDeserializer.Deserialize` to `ContentEntry`-typed.** Signature change: `ContentDeserializer.Deserialize(predicate, ...)` → `ContentDeserializer.Deserialize(ContentEntry entry, ...)`. The synthetic predicate at `ContentProvider.Deserialize` (Phase 43 SUMMARY's "Phase 44 candidate") goes away. Single shape end-to-end from public API down to row-write code. Aligns with no-backcompat policy per `feedback_no_backcompat.md`.

### Layer B test port + Obsolete deletion ordering (CONVERGE-03 + CONVERGE-04)

- **D-05:** **Port-then-delete ratchet: 1 commit per test file, then 1 deletion commit.** Sequence:
  1. Port `tests/Providers/Content/ContentProviderTests.cs` to entry fixtures (commit 1)
  2. Port `tests/Providers/SqlTable/SqlTableProviderCoercionTests.cs` (commit 2)
  3. Port `tests/Providers/SqlTable/SqlTableProviderDeserializeTests.cs` (commit 3)
  4. Port `tests/Providers/SqlTable/SqlTableProviderSeedMergeTests.cs` (commit 4)
  5. Port `tests/Providers/SqlTable/EcomXmlMergeTests.cs` (commit 5)
  6. Port `tests/Integration/StrictModeIntegrationTests.cs` (commit 6)
  7. Reconcile `tests/Providers/SerializerOrchestratorTests.cs` Layer A residual (53 predicate refs) + delete `tests/Helpers/ToPredicateExtensions.cs` + delete the 3 `[Obsolete]` overloads on `SerializerOrchestrator` + delete the predicate→entry bridge inside the body (commit 7)
  - **Rationale:** Tests pass at every commit boundary; bisect remains usable. Mirrors Phase 43's 9-commit atomic-task philosophy. The "tests transient-broken between Tasks 3 and 8" pattern from Phase 43 is explicitly NOT repeated — Phase 44 keeps the test build green throughout.

- **D-06:** **`SerializerOrchestratorTests.cs` Layer A residual (53 `ProviderPredicateDefinition` refs) split by test purpose.** Each predicate-fixture test is classified during commit 7's audit:
  - **Tests targeting the `[Obsolete]` overload's behaviour** (predicate→entry bridge inside the body, predicate-shape `Mock<ISerializationProvider>.Setup`) → **delete in commit 7** along with the overload. These specs are testing dead code the moment CONVERGE-04 lands.
  - **Tests asserting orchestrator semantics that still hold post-pivot** (e.g., generic providerFilter behaviour, error-aggregation invariants) → **port to entry fixtures**. Estimate from grep: ~10-12 deleted, ~3-5 ported. Planner verifies during PLAN.md task breakdown.

- **D-07:** **Delete all three `[Obsolete]` overloads.** Phase 43 SUMMARY's "two `[Obsolete]` overloads" wording was inaccurate vs code reality. Delete in commit 7:
  1. `SerializerOrchestrator.cs:46` — `[Obsolete] SerializeAll(predicates, outputRoot, log, providerFilter)` (Phase 37-01 era)
  2. `SerializerOrchestrator.cs:54` — `[Obsolete] DeserializeAll(predicates, log, isDryRun, providerFilter)` (Phase 37-01 era)
  3. `SerializerOrchestrator.cs:165` — `[Obsolete] DeserializeAll(predicates, inputRoot, mode, strategy, ...)` (Phase 43 era) + its predicate→entry bridge body
  - **Outcome:** zero `[Obsolete]` attributes on `SerializerOrchestrator`. Re-worded SC-3 in ROADMAP.md to "three overloads".

- **D-08:** **Schedule-task verification is assertion-only (CONVERGE-05).** `git grep -ri 'schedule\|ScheduledTask'` against `src/` already returns 0 (commit `a32703f` removed them). Plan's CONVERGE-05 task runs this grep + commits an evidence note; no new code-removal work. Same pattern for SC-2's full 7-file list — 3 of the named files (`SerializerDeserializeCommandTests`, `SerializerSerializeCommandTests`, `SqlTableLinkResolutionIntegrationTests`) already grep zero for `ProviderPredicateDefinition`; the SC-2 grep gate at end of phase guards against regression.

### Phase 43 REVIEW.md fold-in (CONVERGE-07)

- **D-09:** **Fold in all warnings (WR-02..04) + structurally-significant info items (IN-01, IN-02, IN-03, IN-06).** WR-01 (`HasErrors` regression on legacy overload) auto-fixes via CONVERGE-04 deletion — no separate work needed. Specific items folded:
  - **WR-02:** Delete `EntryStatus.Warned` enum value. No production code path produces `Warned`. If a future warning emitter wants it back, re-introduce explicitly.
  - **WR-03:** Tighten the `catch` in `StrictModeDeprecationWarning.EmitIfLegacyValueSet` from `catch (Exception)` to specific expected exceptions (`JsonException`, `IOException`, `UnauthorizedAccessException`). Reason: catch-all swallows surprise exceptions (`OutOfMemoryException`, `ThreadAbortException` analogues) that should propagate.
  - **WR-04:** Re-order `SerializerDeserializeCommand.Handle()` so the log file is created (and `LogFileWriter` instance bound to a method that writes to it) BEFORE `StrictModeDeprecationWarning.EmitIfLegacyValueSet(configPath, Log)` fires. Today the warning fires before the log file exists, so it routes nowhere observable.
  - **IN-01:** Delete `OrchestratorResult.DeserializeResults` field entirely. Migrate `Infrastructure/AdviceGenerator.cs` to consume `IReadOnlyList<EntryOutcome>` instead of `List<ProviderDeserializeResult>` — `EntryOutcome.Counts (ProviderCounts)` provides the same Created/Updated/Skipped/Failed fields. Single canonical surface = `EntryOutcomes`.
  - **IN-02:** Delete the dead `OrchestratorResult.Summary` `else if (DeserializeResults.Count > 0)` branch — unreachable post-IN-01.
  - **IN-03 + IN-06:** Replace `"<run-level>"` string literals in `EntryOutcome.RunLevelError` (EntryId + ProviderType + propagation into `LogFileSummary.Predicates[].Name`) with `public const string RunLevelEntryId = "<run-level>"` named constants on `EntryOutcome`. Single source of truth; grep-friendly.
  - **Skipped:** IN-04 (`SerializerPathResolver.EnsureDirectories` null-validation — defensive nit; current behaviour fail-fast on `Path.Combine(null, ...)` is acceptable) and IN-05 (`ToPredicateExtensions.ToManifestEntry` SqlTable `predicate.Table ?? string.Empty` fallback — dies with the shim deletion in commit 7).

- **D-10:** **`AdviceGenerator` migration preserves public advice-text contract.** Input type changes from `List<ProviderDeserializeResult>` to `IReadOnlyList<EntryOutcome>`. Public advice strings stay semantically identical (per-entry counts → aggregate advice). Log viewer surfaces don't change. If a future enhancement wants per-entry advice (which `EntryOutcome.Errors` + `EntryOutcome.EntryId` would now enable), that's a separate decision.

### Claude's Discretion

- **In-memory Manifest construction details:** zip-import builds the `Manifest` with `complete: true` sentinel + correct `schemaVersion`; `ExcludeFieldsByItemType` / `ExcludeXmlElementsByType` envelopes default to empty (per MANIFEST-05 precedence rule from Phase 43 SUMMARY: empty envelope = "no exclusions"). Planner picks whether to expose `acknowledgedOrphanPageIds` on `DeserializeFromZipCommand` or hardcode an empty list (today's zip-import has no orphan-acknowledgement surface; not changing that here).

- **`internal SerializerOrchestrator.DeserializeEntries(IReadOnlyList<ManifestEntry>, ...) test seam` retention:** ARCHITECTURE.md §5 introduced this internal seam for Layer A tests. Post-CONVERGE-04 the seam may or may not be needed depending on whether the new public `DeserializeAll(Manifest, ...)` overload covers all Layer A test needs. Planner decides: delete the seam if every test can call the public overload, or keep it if test ergonomics suffer.

- **Commit message prefix convention:** continue `(44-01):` for atomic commits inside Plan 01 per Phase 43 precedent. Phase 44 fits one PLAN.md (no multi-plan decomposition — the work is linear and the ratchet is the natural decomposition).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Locked requirements + acceptance
- `.planning/REQUIREMENTS.md` §"Convergence + cleanup (CONVERGE)" — CONVERGE-01..05 + CONVERGE-07. CONVERGE-06 dropped (note inline). MUST read before planning.
- `.planning/ROADMAP.md` "### Phase 44" entry — Goal, Depends on, Requirements list, SC-1..SC-5 acceptance criteria (re-numbered after CONVERGE-06 drop). MUST read before planning.

### Phase 43 outcome (what Phase 44 cleans up)
- `.planning/phases/43-manifest-driven-deserialize-per-entry-reporting-command-surface/43-CONTEXT.md` §"`DeserializeFromZipCommand` transitional handling" (D-03) + §"`ToPredicate(Entry)` test-helper shim lifecycle" (D-04) — explicit hand-off of zip-import + reverse-shim to Phase 44.
- `.planning/phases/43-manifest-driven-deserialize-per-entry-reporting-command-surface/43-01-SUMMARY.md` "Next Phase Readiness" — explicit list of what Phase 44 must finish per CONVERGE-01..06 (read this to confirm scope deltas).
- `.planning/phases/43-manifest-driven-deserialize-per-entry-reporting-command-surface/43-REVIEW.md` — WR-01..04, IN-01..06. CONVERGE-07 in Phase 44 folds in WR-02..04 + IN-01..03 + IN-06.

### Source research (background; informs *why*, not *what*)
- `.planning/research/SUMMARY.md` — milestone v0.6.0 reconciled research; HIGH confidence.
- `.planning/research/ARCHITECTURE.md` §5 — internal `DeserializeEntries` test seam rationale.

### Existing surface to refactor (read before planning the diff)
- `src/DynamicWeb.Serializer/Providers/SerializerOrchestrator.cs` — current `DeserializeAll(modeRoot, mode, ...)` (the disk-reading entry point that becomes a thin wrapper); `[Obsolete]` overloads at lines 46, 54, 165 (all three deleted); predicate→entry bridge body inside line-165 overload.
- `src/DynamicWeb.Serializer/Providers/Content/ContentProvider.cs` — `BuildManifestEntry(predicate, ...)` (refactors to call new `BuildContentEntryForArea`); `Deserialize(ManifestEntry, ...)` body (synthetic predicate at the `ContentDeserializer.Deserialize` call site goes away under D-04).
- `src/DynamicWeb.Serializer/Serialization/ContentDeserializer.cs` — `Deserialize` signature pivots from `predicate`-typed to `ContentEntry`-typed (D-04). Largest diff inside this file in the whole phase.
- `src/DynamicWeb.Serializer/AdminUI/Commands/DeserializeFromZipCommand.cs` — synthetic `SerializerConfiguration` + direct `ContentDeserializer.Deserialize()` call deleted; replaced by in-memory `Manifest` + new public `DeserializeAll(Manifest, contentRoot, ...)` overload. Adds `bool? StrictMode` property + `IsAdminUiInvocation` flag per D-03.
- `src/DynamicWeb.Serializer/AdminUI/Commands/SerializerDeserializeCommand.cs` — log-init re-ordering per WR-04; reference impl for `StrictModeResolver` wiring per D-03.
- `src/DynamicWeb.Serializer/Reporting/EntryStatus.cs` — delete `Warned` value per WR-02.
- `src/DynamicWeb.Serializer/Reporting/EntryOutcome.cs` — `"<run-level>"` literals → named constants per IN-03/IN-06.
- `src/DynamicWeb.Serializer/Reporting/OrchestratorResult.cs` (within `SerializerOrchestrator.cs`) — `DeserializeResults` field deletion + dead `Summary` branch deletion per IN-01/IN-02.
- `src/DynamicWeb.Serializer/Infrastructure/StrictModeDeprecationWarning.cs` — tighten `catch` per WR-03.
- `src/DynamicWeb.Serializer/Infrastructure/AdviceGenerator.cs` — input migration to `IReadOnlyList<EntryOutcome>` per D-10.

### Test surface to port + reconcile
- `tests/DynamicWeb.Serializer.Tests/Helpers/ToPredicateExtensions.cs` — reverse-shim deleted with its file (commit 7).
- `tests/DynamicWeb.Serializer.Tests/Providers/Content/ContentProviderTests.cs` — port to entry fixtures (commit 1).
- `tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/SqlTableProviderCoercionTests.cs` — port (commit 2).
- `tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/SqlTableProviderDeserializeTests.cs` — port (commit 3).
- `tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/SqlTableProviderSeedMergeTests.cs` — port (commit 4).
- `tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/EcomXmlMergeTests.cs` — port (commit 5).
- `tests/DynamicWeb.Serializer.Tests/Integration/StrictModeIntegrationTests.cs` — port (commit 6).
- `tests/DynamicWeb.Serializer.Tests/Providers/SerializerOrchestratorTests.cs` — Layer A residual (53 predicate refs) reconciled per D-06 (commit 7).

### Live-pipeline reference (out of Phase 44 scope but retained)
- `tools/e2e/full-clean-roundtrip.ps1` + `tools/e2e/README.md` — Swift 2.2 → CleanDB pipeline; stays in-repo. On-demand only. CONVERGE-06 dropped from v0.6.0.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`SerializerOrchestrator.DeserializeAll(modeRoot, mode, ...)`** — Phase 43's canonical surface. Phase 44 splits this into a thin disk-reading wrapper + a new public `DeserializeAll(Manifest, contentRoot, ...)` that owns the dispatch loop.
- **`ManifestWriter.Read(modeRoot, modeName)`** — used inside the thin wrapper; unchanged.
- **`StrictModeResolver.Resolve(entryPoint, configValue, requestValue)`** — wired identically at the zip-import call site (D-03). `configValue: null` literal preserved per Phase 43 D-04 grep-friendly precedent.
- **`StrictModeDeprecationWarning.EmitIfLegacyValueSet(configPath, log)`** — gets a tighter `catch` clause (WR-03) but otherwise unchanged; the call site moves AFTER log init (WR-04).
- **`EntryOutcome.From / Skipped / Failed / RunLevelError`** factories — unchanged shape; the four `EntryStatus` values become three (after `Warned` deletion per WR-02).
- **`ProviderCounts(Created, Updated, Skipped, Failed)`** — same fields `ProviderDeserializeResult` carried; powers `AdviceGenerator` migration without behaviour change (D-10).

### Established Patterns
- **Atomic-task-commits inside one PLAN:** Phase 44 fits one PLAN.md decomposed into 7 atomic commits (D-05). Build green at every commit boundary on both production AND test assemblies — Phase 43's "transient red between Tasks 3 and 8" pattern is explicitly NOT repeated.
- **Polymorphic entry switch:** preserved — both the public `DeserializeAll(Manifest, ...)` overload and the (post-D-04) `ContentDeserializer.Deserialize(ContentEntry, ...)` consume entries via `switch (entry) { ... }`.
- **Reverse-shim/typed-dispatch helpers:** all deletions; no new ones introduced.
- **`internal` test seam:** `SerializerOrchestrator.DeserializeEntries(IReadOnlyList<ManifestEntry>, ...)` may or may not be retained — planner's call (Claude's Discretion above).

### Integration Points
- **`DeserializeAll(Manifest, contentRoot, ...)`** (new public) is the new direct entry point for zip-import. Future entry points (e.g., partial-import, hand-edit-fallback) plug in here without re-implementing dispatch.
- **`ContentProvider.BuildContentEntryForArea(...)`** (new public static) is the shared shape source. Full deserialize calls it via `BuildManifestEntry` (Phase 42 contract); zip-import calls it directly.
- **`AdviceGenerator`** consumes `EntryOutcomes` post-migration. The log viewer's per-table advice display surface stays semantically identical.

</code_context>

<specifics>
## Specific Ideas

- **No new tooling for live E2E in this phase.** `tools/e2e/full-clean-roundtrip.ps1` stays as-is; no `dap-clean-roundtrip.ps1` is added; no `-Demo` switch on the Swift pipeline. CONVERGE-06 dropped; live re-validation is on-demand only for v0.6.0.
- **Plan should re-word ROADMAP SC-3 to "three overloads"** (currently still says "two" in some prose — verify the planner catches this). REQUIREMENTS.md + ROADMAP.md were edited during discuss-phase 2026-05-11 to lock the corrected scope; the planner reads the corrected text.
- **`SerializerOrchestratorTests.cs` predicate-fixture audit is a planner deliverable.** Plan must call out the audit explicitly as a task substep within commit 7 — not handed to the executor as an opaque "reconcile residual" instruction. Expected output: a tiny markdown table inside the plan classifying each predicate-using test as Delete/Port/Retained-as-Bridge-Test.

</specifics>

<deferred>
## Deferred Ideas

- **Live E2E re-validation (was CONVERGE-06):** dropped from v0.6.0 scope on 2026-05-11. `tools/e2e/full-clean-roundtrip.ps1` remains in-repo. Re-validation runs on-demand only. Re-promote in v0.7.0 if regression suspected.
- **DAP / pim.carriageservices live deploy SC (was Phase 44 SC-5):** dropped along with CONVERGE-06.
- **B.5.2 PropertyItem GUID sweep + 47 orphan page-IDs across 20 distinct IDs (Phase 38.1 open-with-gap):** stays as Phase 38.1 backlog; v0.7.0 candidate. Architecturally correct fix per saved memory: ItemType-XML-aware enumeration via `ItemManager.Metadata.GetItemType(systemName)`, detect LinkEditor/ItemEditor with ID-valued storage, serialize as GUID symmetric with `GlobalRecordPageGuid` OR extend `BaselineLinkSweeper` to fail-fast.
- **ITEM-01 ItemEditor field handling:** stays as Phase 38.1 backlog; v0.7.0 candidate. Same architectural fix family as B.5.2.
- **Per-entry advice surface (post-`AdviceGenerator` migration):** D-10 keeps the aggregate advice contract identical. A future enhancement could surface per-entry advice using `EntryOutcome.Errors` + `EntryOutcome.EntryId` directly in the log viewer; v0.7.0 candidate.
- **Defensive null-validation in `SerializerPathResolver.EnsureDirectories` (IN-04):** defensive nit; current fail-fast behaviour acceptable. Re-promote if a non-test caller surfaces a null path.

</deferred>

---

*Phase: 44-zip-import-convergence-test-cleanup-schedule-task-removal-live-e2e*
*Context gathered: 2026-05-11*
