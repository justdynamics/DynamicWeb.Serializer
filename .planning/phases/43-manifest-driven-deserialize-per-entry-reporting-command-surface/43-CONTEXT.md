# Phase 43: Manifest-driven deserialize + per-entry reporting + command surface — Context

**Gathered:** 2026-05-08
**Status:** Ready for planning

<domain>
## Phase Boundary

Pivot the deserialize path to consume the v0.6.0 manifest written by Phase 42. After this phase: `SerializerOrchestrator.DeserializeAll` reads the manifest and dispatches per-entry; `ConfigLoader.Load` is gone from every deserialize entry point; per-entry `EntryOutcome` (Succeeded | Failed | Warned | Skipped) replaces aggregate `DeserializeResults`; strict-mode default is sourced from `StrictModeResolver` (entry-point + per-call request override), no longer from `config.StrictMode`. Zip-import full convergence and Layer B test port stay in Phase 44 — Phase 43 only loosens what Phase 44 needs to finish.

Requirements locked by `.planning/REQUIREMENTS.md`: **DESER-01..05** (signature pivot, FK ordering on entries, provider Deserialize takes ManifestEntry, ConfigLoader.Load removed, StrictModeResolver wiring) + **REPORT-01..05** (EntryStatus enum, EntryOutcome record, OrchestratorResult.EntryOutcomes, HasErrors aggregation, per-entry log lines).

Acceptance criteria locked by `.planning/ROADMAP.md` SC-1..SC-6 for Phase 43.

</domain>

<decisions>
## Implementation Decisions

### Wave decomposition

- **D-01:** **Big-bang single plan.** Phase 43 ships as one PLAN.md (`43-01-PLAN.md`) covering all 10 requirements end-to-end: types (`EntryStatus`, `EntryOutcome`) → orchestrator pivot (`DeserializeAll(manifest, ...)` signature) → `ISerializationProvider.Deserialize(ManifestEntry, ...)` signature change → per-provider body changes → command-surface updates (`SerializerDeserializeCommand`, `DeserializeFromZipCommand`, strict-mode wiring) → Layer A orchestrator unit tests on entry fixtures.
  - **Rationale:** User explicitly chose this over a 4-5-plan incremental rollout. Trades commit-bisect granularity for paperwork minimisation. The full unit-test suite is the safety net.
  - **Risk to surface to planner:** This plan is significantly larger than any single plan in Phase 42 (which itself was sliced 4 ways for less surface). The planner SHOULD generate a task breakdown with atomic commits per task — even if there's one PLAN.md, the inside should still have ~6-10 tasks committed individually so bisect remains usable.

### `EntryStatus.Skipped` semantics

- **D-02:** **Tight definition.** `Skipped` is reserved for entries the orchestrator **never dispatched to a provider** — currently this is exclusively `providerFilter` exclusion (per SC-2). All other zero-write outcomes route to `Succeeded` (or `Failed` on actual error):
  - **Files don't exist on disk** ⇒ `Failed` (drift between manifest and disk is a real problem, not a quiet case).
  - **Dry-run that would have changed rows** ⇒ `Succeeded` with `Counts.Created`/`Counts.Updated` populated as-if (dry-run reports the would-be work; the entry executed its planning logic successfully).
  - **Seed-merge with all fields already set on target** ⇒ `Succeeded` with `Counts.Skipped: N` (entry executed its merge logic; just no writes; the per-row skip count is the signal).
  - **Rationale:** REPORT-01's framing is "today's silent-skip class becomes observable". The silent-skip class today is orchestrator-level skip (entry filtered out by config mismatch). That's what `Skipped` captures. Per-row skip counts inside a successful entry are a different observable, and `Counts.Skipped` is the right home for them.

### `DeserializeFromZipCommand` transitional handling

- **D-03:** **Minimal diff in Phase 43.** Replace `ConfigLoader.Load(configPath)` in `DeserializeFromZipCommand` with a config-free path resolution helper for the `EnsureDirectories(systemDir)` call site (it's currently the only thing the loaded config is used for). Leave the direct `ContentDeserializer.Deserialize()` call intact. **Do not** route zip-import through the orchestrator in Phase 43.
  - **Rationale:** CONVERGE-02 (route zip through `SerializerOrchestrator.DeserializeAll`) is explicitly Phase 44 scope. Phase 43 owns DESER-04 only — strip the `ConfigLoader.Load` reference. Smallest diff; preserves the planned 43/44 scope split.
  - **Implication for the planner:** the path-helper extraction is a small, low-risk task. If during execution the planner discovers that the helper needs the `OutputDirectory` value (currently `config.OutputDirectory` is used by `ContentDeserializer` via the synthetic `SerializerConfiguration`), keep that synthetic-config construction local to the zip command (it's not config-on-disk; it's a temporary in-memory holder for zip-extraction state).

### `ToPredicate(Entry)` test-helper shim lifecycle

- **D-04:** **Lands early, removed at end of Phase 43.** The shim is introduced in the same task that flips the orchestrator's `DeserializeAll` signature so the existing **predicate-fixture integration tests** (Phase 44's Layer B port targets) keep compiling and passing while the orchestrator pivots underneath them. **Layer A tests** (orchestrator unit tests, owned by Phase 43) land directly on **entry fixtures** — they do not use the shim. The shim is deleted at the end of Phase 43 along with `ProviderPredicateDefinition` from any test fixture that the Phase 43 plan touched.
  - **Rationale:** Two-step migration risk-control: keep Phase 43's tests bisectable while orchestrator changes; let the Layer B Phase 44 port migrate tests directly to entry fixtures (Phase 44 will not need the shim — it's deleted between phases).
  - **Implication for the planner:** the shim's API is `ProviderPredicateDefinition ToPredicate(this ManifestEntry entry)` (test-assembly extension method), implemented as a thin field-by-field projection. Keep it `internal` and gated behind `InternalsVisibleTo` to the test assembly; don't ship it in the production assembly.

### Claude's Discretion

- **Strict-mode WARNING surface (DESER-05):** The "one-time WARNING when `config.StrictMode` is set but no longer consulted" message can land as a single `LogFileWriter`-routed log line at the start of every deserialize run that detects the no-longer-consulted setting. Console output is fine; admin-UI banner is overkill for a transitional warning (it'll go away once configs migrate). Planner picks shape; the requirement is "fires once per run, names the no-longer-consulted setting, points to the new entry-point default + per-call override".

- **Per-entry log line shape (REPORT-05 / SC-5):** The example in SC-5 (`[content/area-1/customer-center] Succeeded`, `[sql/EcomOrderFlow] Failed: 3 of 47 rows failed FK validation`) defines the format. Multi-line warnings can use indented continuation lines under the entry's primary line. Timestamps + duration go in the per-line tail (consistent with the existing log viewer's format). Planner picks the exact column layout.

- **Strict-mode resolution call site:** `StrictModeResolver.Resolve(entryPoint, configValue: null, requestValue)` — the `configValue: null` literal makes the "no longer consulted" semantic explicit at every call site, which is grep-friendly. (Per Phase 37-04 the resolver already exists; Phase 43 only changes which third arg gets passed in.)

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Locked requirements + acceptance
- `.planning/REQUIREMENTS.md` §"Deserialize pivot (DESER)" + §"Per-entry outcome reporting (REPORT)" — the 10 locked requirements (DESER-01..05, REPORT-01..05) for this phase. MUST read before planning.
- `.planning/ROADMAP.md` "### Phase 43" entry — Goal, Depends on, Requirements list, SC-1..SC-6 acceptance criteria. MUST read before planning.

### Source research (background; informs *why*, not *what*)
- `.planning/research/SUMMARY.md` — milestone v0.6.0 reconciled research; HIGH confidence; 3-phase decomposition rationale.
- `.planning/research/STACK.md` — System.Text.Json strict-mode, native `Disallow` + `required` (no third-party schema lib).
- `.planning/research/ARCHITECTURE.md` — entry hierarchy 2-types decision (option α: `ContentEntry` + `SqlTableEntry`; no separate `EmbeddedXmlEntry`).
- `.planning/research/PITFALLS.md` §3 + §10 — strict-mode default sourcing (entry-point + per-call request override) + the `config.StrictMode` deprecation logic.

### Phase 42 outcome (the manifest you're about to consume)
- `.planning/phases/42-manifest-schema-entry-hierarchy-serialize-side-build/42-01-SUMMARY.md` — Manifest envelope + entry-hierarchy types. **`ManifestEntry.ProviderType` is abstract get-only `[JsonIgnore]`** — derived per-record. Don't construct entries with a `ProviderType` setter.
- `.planning/phases/42-manifest-schema-entry-hierarchy-serialize-side-build/42-02-SUMMARY.md` — atomic `ManifestWriter.Write/Read`, schemaVersion gate, `complete: true` sentinel.
- `.planning/phases/42-manifest-schema-entry-hierarchy-serialize-side-build/42-03-SUMMARY.md` — provider `BuildManifestEntry` shape; orchestrator collects entries + threads `excludeFieldsByItemType` / `excludeXmlElementsByType` to envelope.
- `.planning/phases/42-manifest-schema-entry-hierarchy-serialize-side-build/42-04-SUMMARY.md` — round-trip property tests prove which field lands where.
- `.planning/phases/42-manifest-schema-entry-hierarchy-serialize-side-build/42-VERIFICATION-FINAL.md` — verifier verdict; SC-6 deviation note (.NET 8 STJ throws `NotSupportedException` not `JsonException`; both are loud-fail and acceptable).

### Existing surface to refactor (read before planning the diff)
- `src/DynamicWeb.Serializer/Providers/SerializerOrchestrator.cs` — current `DeserializeAll` signatures (line 48: legacy + line 150: current); `OrchestratorResult.DeserializeResults` (line 372); `HasErrors` aggregation (line 384); summary builder (line 398).
- `src/DynamicWeb.Serializer/Providers/ISerializationProvider.cs` — current `Deserialize` contract; `ValidatePredicate` to remove.
- `src/DynamicWeb.Serializer/Providers/Content/ContentProvider.cs` — current `Deserialize` body; predicate consumption to migrate.
- `src/DynamicWeb.Serializer/Providers/SqlTable/SqlTableProvider.cs` — same.
- `src/DynamicWeb.Serializer/AdminUI/Commands/SerializerDeserializeCommand.cs` — main entry point; `ConfigLoader.Load` at line 101.
- `src/DynamicWeb.Serializer/AdminUI/Commands/DeserializeFromZipCommand.cs` — zip path; `ConfigLoader.Load` at line 48; direct `ContentDeserializer.Deserialize` at line 92.
- `src/DynamicWeb.Serializer/StrictMode/StrictModeResolver.cs` — already wired from Phase 37-04; only the call sites change.
- `src/DynamicWeb.Serializer/Infrastructure/ManifestWriter.cs` — `Read(modeRoot, mode)` returns `Manifest`; entry list lives at `manifest.Entries`.

### Cross-phase test infrastructure
- `tests/DynamicWeb.Serializer.Tests/Providers/SerializerOrchestratorTests.cs` — Layer A target. Currently uses predicate fixtures.
- `tests/DynamicWeb.Serializer.Tests/Providers/Content/ContentProviderTests.cs`, `Providers/SqlTable/SqlTableProviderDeserializeTests.cs`, etc. — Layer B targets (Phase 44, not Phase 43).

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`StrictModeResolver`** (Phase 37-04): already wired with `Resolve(entryPoint, configValue, requestValue)`. Phase 43 only changes the second argument from `config.StrictMode` to `null` at every deserialize call site.
- **`StrictModeEscalator` + `CumulativeStrictModeException`** (Phase 37-04): the orchestrator already catches and collects strict-mode escalations into per-entry errors. Phase 43 routes those through `EntryOutcome.Errors[]` instead of the run-level `OrchestratorResult.Errors`.
- **`FkDependencyResolver`**: per DESER-02, reused unchanged. Live-recomputes FK order from `entries[]` regardless of manifest order.
- **`ProviderDeserializeResult`** (existing): survives as a per-table DTO that feeds `EntryOutcome.From(...)` (per REPORT-03). Don't delete it.
- **`LogFileWriter` + log-viewer infrastructure** (Phases 16/37): per-entry log lines route through the same plumbing; only the line format changes.

### Established Patterns
- **Polymorphic entry switch:** consume entries via `switch (entry) { case ContentEntry c: ...; case SqlTableEntry s: ...; }` (per Phase 42 ARCHITECTURE.md option α). Don't introduce a visitor.
- **Single options bag:** all `JsonSerializerOptions` for manifest reads come from `ManifestSchema.ManifestJsonOptions` (Phase 42 Plan 01 pattern).
- **Atomic test commits inside one PLAN:** even with the big-bang plan choice, the planner SHOULD task-decompose so each task commits atomically (Phase 42 wave 3 was 4 tasks → 4 commits inside one PLAN).

### Integration Points
- **`SerializerOrchestrator.DeserializeAll(modeRoot, mode, strategy, dryRun, providerFilter, escalator, ...)`** is the new public surface. Old signatures stay `[Obsolete]` until Phase 44 (CONVERGE-04 deletes them).
- **`OrchestratorResult.EntryOutcomes: List<EntryOutcome>`** replaces `DeserializeResults: List<ProviderDeserializeResult>`. The `ProviderDeserializeResult` type itself stays (per-table DTO).
- **`SerializerSerializeCommand`**: NOT touched in Phase 43. Phase 42 already owns the serialize side.

</code_context>

<specifics>
## Specific Ideas

- **Big-bang plan must produce atomic-commit tasks.** User chose 1 plan for paperwork reasons; the planner must still break the plan into 6-10 tasks with one commit each so `git bisect` works during the merge-back. Per the Phase 42 pattern.
- **`config.StrictMode` deprecation log line should name the file path and the suggested replacement** (e.g., "config.StrictMode is set in `Serializer.config.json` but no longer consulted on the deserialize path; use the per-call `?strictMode=true` query parameter or rely on the entry-point default").

</specifics>

<deferred>
## Deferred Ideas

- **Phase 38.1 open-with-gap (B.5.2 link sweeper extension to PropertyItem GUIDs + 47 orphan page-ID occurrences across 20 distinct IDs + ITEM-01 ItemEditor follow-up + Phase 38.1 Plan 04 script-08 revision):** explicitly folded into Phase 44's E2E sweep (CONVERGE-06 already plans the live `strictMode: true` re-validation against Swift 2.2 → CleanDB and DAP / pim.carriageservices). Phase 43 does not touch live data hygiene.
- **Per-entry checksum / drift detection (DRIFT-01):** future requirement; out of v0.6.0 scope per REQUIREMENTS.md.
- **Per-entry conflictStrategy override (OVERRIDE-01):** future; tracked.
- **Per-entry `dependsOn[]` topological sort (DEPENDS-01):** future; FK ordering stays as-is (live-recomputed) per DESER-02.
- **Provider-type tiebreaker / file-header proof (TIEBREAK-01):** future; manifest is authoritative in v0.6.0.
- **Hand-edit fallback / standalone YAML import (HAND-EDIT-01):** future; deserialize requires a manifest.
- **`EmbeddedXmlProvider` carve-out (CARVE-EMBEDDED-XML):** v0.7.0 candidate; defer until a third provider lands.

</deferred>

---

*Phase: 43-manifest-driven-deserialize-per-entry-reporting-command-surface*
*Context gathered: 2026-05-08*
