# Requirements: v0.6.0 Manifest-Driven Deserialize

**Defined:** 2026-05-08
**Core Value:** Developers can reliably move DynamicWeb database state between environments through source control, with serialized YAML files as the single source of truth.

**Milestone goal:** Pivot serialize to lock all knowledge into the artifact (manifest); deserialize executes purely from the manifest, no config consultation. Per-item `Succeeded | Failed | Warned | Skipped` reporting replaces today's silent-skip-on-config-mismatch model.

## v0.6.0 Requirements

### Manifest schema (MANIFEST)

- [x] **MANIFEST-01**: `{mode}-manifest.json` carries a versioned envelope (`schemaVersion`, `mode`, `writtenAtUtc`, `complete: true` sentinel, `entries[]`) that fail-fast errors when read by an incompatible version
- [x] **MANIFEST-02**: Manifest entries are polymorphic records discriminated by `providerType` (`ContentEntry`, `SqlTableEntry`); System.Text.Json `[JsonPolymorphic]` + `[JsonDerivedType]` with strict missing-discriminator failure
- [x] **MANIFEST-03**: Manifest read enforces strict shape — `[JsonUnmappedMemberHandling(Disallow)]` + `required` modifier — so torn or hand-edited manifests fail loudly at read time, not silently downstream
- [x] **MANIFEST-04**: Manifest write is atomic — temp file + `File.Move(overwrite: true)` + sentinel — so a crashed serialize never leaves a half-written manifest that the deserializer would partially trust
- [x] **MANIFEST-05**: Top-level `excludeFieldsByItemType` and `excludeXmlElementsByType` maps are baked into the manifest envelope at serialize time so deserialize does not need to read `Serializer.config.json`

### Provider entry build (PROVIDER)

- [x] **PROVIDER-01**: `SerializationProviderBase.BuildManifestEntry(predicate, modeRoot, writtenFiles)` is the abstract contract every provider implements; runs as part of the existing `Serialize(...)` call (single pass)
- [x] **PROVIDER-02**: `ContentProvider.BuildManifestEntry` produces a `ContentEntry` carrying `areaId`, `path`, `pageId`, owned `files[]`, post-processing hooks, and exclusion maps
- [x] **PROVIDER-03**: `SqlTableProvider.BuildManifestEntry` produces a `SqlTableEntry` carrying `table`, `nameColumn`, `xmlColumns`, `where`, owned `files[]`, post-processing hooks (`serviceCaches`, `schemaSync`, `resolveLinksInColumns`), and exclusion fields (`excludeAreaColumns`, `acknowledgedOrphanPageIds`)
- [x] **PROVIDER-04**: `SerializeResult` exposes the produced `ManifestEntry?` alongside `WrittenFiles`; orchestrator collects entries from results and hands them to `ManifestWriter`
- [x] **PROVIDER-05**: A round-trip property test asserts every one of the eight predicate fields that affect deserialize behavior (`ServiceCaches`, `SchemaSync`, `XmlColumns`, `ExcludeFields`, `ExcludeXmlElements`, `ExcludeAreaColumns`, `ResolveLinksInColumns`, `AcknowledgedOrphanPageIds`) survives the predicate → entry → manifest → entry trip with no loss

### Deserialize pivot (DESER)

- [ ] **DESER-01**: `SerializerOrchestrator.DeserializeAll` no longer accepts a predicates parameter; signature is `DeserializeAll(modeRoot, mode, strategy, dryRun, providerFilter, escalator, ...)` — reads the manifest from `modeRoot` and dispatches each entry
- [ ] **DESER-02**: FK ordering and Content-before-SqlTable reorder rules operate on `entries[]` (live-recomputed, not trusting manifest order); `FkDependencyResolver` is reused unchanged
- [ ] **DESER-03**: `ISerializationProvider.Deserialize` accepts a `ManifestEntry` (not a `ProviderPredicateDefinition`); `ValidatePredicate` is removed from the provider interface
- [ ] **DESER-04**: `SerializerDeserializeCommand`, `DeserializeFromZipCommand`, and any other deserialize entry point no longer call `ConfigLoader.Load` — config is irrelevant to the deserialize path
- [ ] **DESER-05**: Strict-mode default is sourced from the entry-point (API/CLI=true, AdminUI=false) plus a per-call request override; `config.StrictMode` is no longer consulted on the deserialize path. A one-time WARNING surfaces if `config.StrictMode` is set but no longer effective

### Per-entry outcome reporting (REPORT)

- [ ] **REPORT-01**: An `EntryStatus` enum exists with four values: `Succeeded`, `Failed`, `Warned`, `Skipped`. `Skipped` is distinct from `Succeeded` so today's silent-skip class becomes observable
- [ ] **REPORT-02**: Each manifest entry produces an `EntryOutcome` record carrying `EntryId`, `ProviderType`, `Status`, `Message`, `Errors[]`, `Warnings[]`, `Counts` (created/updated/skipped/failed), and `Duration`
- [ ] **REPORT-03**: `OrchestratorResult.EntryOutcomes` replaces `OrchestratorResult.DeserializeResults`. `ProviderDeserializeResult` survives as a per-table DTO that feeds `EntryOutcome.From(...)`
- [ ] **REPORT-04**: `OrchestratorResult.HasErrors` aggregates from outcomes (`entries.Any(e => e.Status is Failed)`) and is the single source of truth for the HTTP-status invariant; the D-38-12 zero-error == HTTP 200 guard test is extended to cover entry-level failure shapes
- [ ] **REPORT-05**: Per-entry log lines surface in the admin-UI log viewer (`Files/System/Serializer/Log/`) so operators can read per-entry outcomes without re-running

### Convergence + cleanup (CONVERGE)

- [ ] **CONVERGE-01**: A shared `BuildContentEntryForArea` helper exists; both the full deserialize path and `DeserializeFromZipCommand` route through it so zip-import and full-import cannot diverge in entry shape
- [ ] **CONVERGE-02**: `DeserializeFromZipCommand` builds an in-memory `Manifest` containing one synthesised `ContentEntry` and runs through the same orchestrator pipeline as the full deserialize (no separate code path)
- [ ] **CONVERGE-03**: All predicate-fixture test files migrate to entry fixtures via a ratchet (Layer A: orchestrator unit tests in the pivot phase; Layer B: provider integration + command + strict-mode integration tests in the cleanup phase). A transitional `ToPredicate(Entry)` test-helper shim is permitted during Phase 2 and removed in Phase 3
- [ ] **CONVERGE-04**: The two `[Obsolete]` `SerializeAll`/`DeserializeAll` overloads on `SerializerOrchestrator` are removed
- [ ] **CONVERGE-05**: Remaining schedule-task code paths (already in PROJECT.md Active list) are removed as part of the cleanup phase
- [ ] **CONVERGE-06**: Live E2E re-validation passes against Swift 2.2 → CleanDB and against the DAP/pim.carriageservices deploy under `strictMode: true`

## Future Requirements (deferred — tracked, not in v0.6.0 roadmap)

Per FEATURES.md Tier B and PITFALLS §3, these are explicitly tracked but out of v0.6.0 scope:

- **DRIFT-01**: Per-entry `Sha256` checksum + pre-flight scan that detects manifest/disk drift (files-without-entries, entries-without-files, content-edited-without-manifest-update). Liquibase precedent.
- **OVERRIDE-01**: Per-entry `conflictStrategy` override allowing a specific entry to ignore the run-wide setting (rare use case; track as future)
- **DEPENDS-01**: Per-entry `dependsOn[]` field with topological sort at apply time, replacing the two hardcoded reorder passes (FK ordering, Content-before-SqlTable)
- **TIEBREAK-01**: Per-file provider-type tiebreaker (the file's own header proves what it is — defends against moved/renamed files where the manifest is wrong)
- **PROVENANCE-01**: Provenance/checksum sidecar headers on individual files for signed-artifact workflows
- **HAND-EDIT-01**: Hand-edited file fallback so a YAML can be deserialized standalone without a manifest entry
- **MIGRATE-01**: Schema migration infrastructure for future `schemaVersion` bumps (today's policy is hard-cut, no backcompat — re-serialize). Becomes relevant once the user base requires forward-compat.
- **CARVE-EMBEDDED-XML**: Carve out `EmbeddedXmlProvider` from `SqlTableProvider` so embedded XML is a first-class entry type rather than a `SqlTableEntry` field. Defer until a third provider lands; v0.7.0 candidate.

## Out of Scope

| Item | Reason |
|------|--------|
| Backwards compatibility with v0.5.x manifest shape | No backcompat policy (per `feedback_no_backcompat.md`) — failed reads emit a clear "re-run serialize against this build" error |
| `dryRun`, `strictMode`, `providerFilter`, `conflictStrategy` in the manifest | Caller-supplied at deserialize time; same artifact must apply lenient to dev / strict to prod (universal pattern across Terraform, Liquibase, Pulumi) |
| JSON Schema validation library (NJsonSchema, JsonSchema.Net) | Native System.Text.Json `Disallow` + `required` cover the producer-controlled use case (per STACK.md) |
| `IManifestStore` / `IEntryDispatcher` abstractions | Two providers do not earn an interface; concrete `ManifestWriter` is sufficient (per ARCHITECTURE.md) |
| Visitor pattern over entries | Same — `switch (entry)` over the polymorphic record hierarchy is enough |
| Bidirectional manifest evolution (round-trip across schema versions) | Hard-cut, fail-fast on version mismatch — no migration story |
| Hand-edit-friendly manifest format | Producer-controlled; manifest is a build artifact, not a config |

## Traceability

Empty until roadmap creation. Each requirement maps to exactly one phase.

| REQ-ID | Phase | Status |
|--------|-------|--------|
| MANIFEST-01 | Phase 42 | Complete |
| MANIFEST-02 | Phase 42 | Complete |
| MANIFEST-03 | Phase 42 | Complete |
| MANIFEST-04 | Phase 42 | Complete |
| MANIFEST-05 | Phase 42 | Complete |
| PROVIDER-01 | Phase 42 | Complete |
| PROVIDER-02 | Phase 42 | Complete |
| PROVIDER-03 | Phase 42 | Complete |
| PROVIDER-04 | Phase 42 | Complete |
| PROVIDER-05 | Phase 42 | Complete |
| DESER-01 | Phase 43 | Pending |
| DESER-02 | Phase 43 | Pending |
| DESER-03 | Phase 43 | Pending |
| DESER-04 | Phase 43 | Pending |
| DESER-05 | Phase 43 | Pending |
| REPORT-01 | Phase 43 | Pending |
| REPORT-02 | Phase 43 | Pending |
| REPORT-03 | Phase 43 | Pending |
| REPORT-04 | Phase 43 | Pending |
| REPORT-05 | Phase 43 | Pending |
| CONVERGE-01 | Phase 44 | Pending |
| CONVERGE-02 | Phase 44 | Pending |
| CONVERGE-03 | Phase 44 | Pending |
| CONVERGE-04 | Phase 44 | Pending |
| CONVERGE-05 | Phase 44 | Pending |
| CONVERGE-06 | Phase 44 | Pending |

**Coverage:**
- v0.6.0 requirements: 26 total
- Mapped to phases: 26 (Phase 42: 10, Phase 43: 10, Phase 44: 6)
- Unmapped: 0 ✓

---
*Requirements defined: 2026-05-08*
*Source research: `.planning/research/SUMMARY.md`*
