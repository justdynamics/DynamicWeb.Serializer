# Project Research Summary — v0.6.0 Manifest-Driven Deserialize

**Project:** DynamicWeb.Serializer
**Domain:** Self-describing apply-pipeline artifact (Liquibase / Terraform-plan family) over a pluggable provider architecture
**Researched:** 2026-05-08
**Confidence:** HIGH

## Executive Summary

v0.6.0 pivots `SerializerOrchestrator.DeserializeAll` from a config-driven dispatcher to a manifest-driven one. The four researchers converge on a single shape: the `{mode}-manifest.json` becomes a versioned, polymorphic `entries[]` artifact where each entry carries everything the deserialize path needs (`providerType`, target identifiers, post-processing hints, owned files), the deserialize path stops calling `ConfigLoader.Load`, and per-entry `Succeeded | Failed | Warned | Skipped` outcomes replace today's silent-skip-on-config-mismatch model. Closest reference shapes are Terraform's plan file and Liquibase's changelog; Sitecore Unicorn — our cultural reference — is explicitly NOT manifest-driven on apply, and we are pivoting away from that model.

The recommended stack is **zero new NuGet dependencies**: `[JsonPolymorphic]` + `[JsonDerivedType]` for the entry hierarchy, `[JsonUnmappedMemberHandling(Disallow)]` plus the C# `required` keyword for strict reads, and a 10-line `JsonDocument` precheck for the `schemaVersion` gate. NJsonSchema and JsonSchema.Net both rejected — the manifest is producer-controlled and native STJ already throws targeted errors on unknown property / missing required / unknown discriminator. See STACK.md.

The dominant risk class is **silent-skip via lost metadata in `BuildManifestEntry`**: the predicate today exposes eight fields the orchestrator consults at deserialize (`ServiceCaches`, `SchemaSync`, `XmlColumns`, `ExcludeFields`, `ExcludeXmlElements`, `ExcludeAreaColumns`, `ResolveLinksInColumns`, `AcknowledgedOrphanPageIds`). If any are forgotten in the entry-builder, the deserializer loses post-processing without warning — the same shape as the v0.4.x cache-invalidator bug. Mandatory defense: per-field round-trip property test landing **in the same phase that introduces the contract**.

## Key Findings

### Recommended Stack
No new dependencies. Reuse `ManifestWriter.ManifestJsonOptions` as the canonical options bag — extend with `UnmappedMemberHandling.Disallow`, no parallel options bag.

- `System.Text.Json` 8.x — manifest read/write
- `[JsonPolymorphic]` + `[JsonDerivedType]` — entry hierarchy dispatch with `IgnoreUnrecognizedTypeDiscriminators=false`
- `[JsonUnmappedMemberHandling(Disallow)]` + C# `required` — strict-reads
- Hand-rolled `JsonDocument` precheck for `schemaVersion` — fail-fast version gate

**Rejected:** NJsonSchema, JsonSchema.Net, Newtonsoft `TypeNameHandling`, FluentMigrator-style migration framework, `IManifestStore`/`IEntryDispatcher` abstractions.

### Expected Features
**Must have:** `providerType` discriminator, stable `entryId`, typed target sub-record (Content `areaId`+`pageId`; SqlTable `table`+`nameColumn`), POSIX-relative `files[]`, `mode`+`writtenAtUtc`+`schemaVersion` envelope, per-entry `postProcessing` (`ServiceCaches`/`SchemaSync`/`ResolveLinksInColumns` — parity, not differentiator), 4-valued `EntryStatus { Succeeded | Failed | Warned | Skipped }`.

**Defer past v0.6.0:** per-entry checksum, per-entry conflict-strategy override, hand-edit fallback marker, manifest-level `dependencies[]`, per-entry `dependsOn[]` topo sort (P1 if budget allows, otherwise P2).

**Anti-features (caller-supplied, NOT in manifest):** `dryRun`, `strictMode`, `providerFilter`, run-wide `conflictStrategy`. Same artifact applied lenient to dev / strict to prod.

### Architecture Approach
**Reality check:** `EmbeddedXmlProvider` does NOT exist in code (verified by Glob/Grep). Embedded XML is a per-column branch inside `SqlTableProvider` (`XmlColumns` + `XmlMergeHelper`). **Recommendation: option (α) — entry hierarchy is `ContentEntry` + `SqlTableEntry` only.** Embedded XML stays a field on `SqlTableEntry`. Carving out a third provider is v0.7.0 scope.

**Major components:**
1. `Manifest` envelope record (`schemaVersion=2`, hard-cut, no migration story per `feedback_no_backcompat.md`)
2. `ManifestEntry` polymorphic hierarchy — abstract record + sealed `ContentEntry`/`SqlTableEntry`
3. `SerializeResult.Entry` (additive nullable) populated via new `protected abstract BuildManifestEntry(predicate, modeRoot, writtenFiles)` on `SerializationProviderBase`
4. `SerializerOrchestrator.DeserializeAll(modeRoot, mode, strategy, dryRun, providerFilter, escalator, ...)` — predicates parameter goes away; reads manifest first; existing FK + LINK-02 reorder applied to entries
5. `EntryOutcome` + `OrchestratorResult.EntryOutcomes` — replaces `DeserializeResults`. `ProviderDeserializeResult` survives as the per-provider DTO feeding `EntryOutcome.From(...)`. `HasErrors` aggregates from outcomes (single source of truth for HTTP-status invariant)

**Anti-patterns refused:** `IManifestStore` interface, `IEntryDispatcher` extraction, visitor pattern, bidirectional manifest evolution.

### Critical Pitfalls
1. **Lost post-processing metadata** (PITFALLS §2) — 8-vector silent-skip class. Defense: per-field round-trip property test in the SAME phase that ships `BuildManifestEntry`.
2. **Torn manifest from crashed serialize** (§1) — `File.WriteAllText` is not atomic on Windows. Defense: temp-file + `File.Move(overwrite: true)` + `"complete": true` sentinel.
3. **STJ polymorphism discriminator-property fragility** (§4) — discriminator must be position-0, case-sensitive, can't be `[Required]`. Defense: pin via `Utf8JsonWriter` ordering + round-trip test with hand-edited reorder.
4. **Per-entry reporting aggregates wrong** (§9) — naive reshape returns HTTP 200 on entry-level failure. Defense: `HasErrors = entries.Any(e => e.Outcome is Failed)`, extend D-38-12 guard test.
5. **DeserializeFromZipCommand drift** (§7) — must converge on shared `BuildContentEntryForArea` builder.
6. **Test churn hides regressions** (§8) — defense: ratchet-style port (Layer A in pivot phase, Layer B in cleanup phase) with transitional `Predicate ToPredicate(Entry)` test-helper shim.

## Implications for Roadmap

### Phase Decomposition: 3 Phases (reconciliation)

Architecture researcher recommended **2 phases**; pitfalls researcher recommended **5–6 phases**. **Recommendation: 3.** Reconciliation: pitfalls' 5–6 includes deferred features (drift detection / migration upgrader) that are explicitly track-but-defer per FEATURES Tier B and PITFALLS §3 — they belong in v0.6.x or v0.7.0. That collapses to 3. Architecture's 2-phase undercounts zip-import + full test cleanup, which carry meaningful surface area.

#### Phase 1 — Manifest Schema + Entry Hierarchy + Serialize-Side Build
**Rationale:** Purely additive on the serialize side; end-of-phase test is "serialize emits new manifest, all existing deserialize tests pass unchanged."
**Delivers:** `Manifest` envelope (schemaVersion=2, complete sentinel, atomic write), polymorphic `ManifestEntry`/`ContentEntry`/`SqlTableEntry`, `[JsonUnmappedMemberHandling(Disallow)]`+`required` everywhere, `SerializeResult.Entry`, `BuildManifestEntry` per provider, `ManifestWriter`/`ManifestCleaner` rewrite, `ExcludeFieldsByItemType`/`ExcludeXmlElementsByType` baked into envelope at serialize time, **mandatory 8-field round-trip property test**, `ManifestWriterTests`/`ManifestCleanerTests`.
**Avoids:** Pitfalls 1, 2, 3, 4.

#### Phase 2 — Manifest-Driven Deserialize + Per-Entry Reporting + Command Surface
**Rationale:** Reads what Phase 1 wrote. Pivot, public-API reshape, command-surface change, strict-mode location decision all land together — splitting creates re-touch churn on every API/CLI/AdminUI test.
**Delivers:** New `DeserializeAll` signature (no predicates parameter), `ISerializationProvider.Deserialize(ManifestEntry, ...)`, `ValidatePredicate` removed, `EntryOutcome`+`EntryStatus`+`ProviderCounts`, `OrchestratorResult.EntryOutcomes`, strict-mode escalator captures per-entry warnings, `SerializerDeserializeCommand` drops `ConfigLoader.Load`, Layer A test port (~30 orchestrator unit tests), strict-mode resolver wired entry-point-default + per-call request override + one-time WARNING when `config.StrictMode` set, HTTP status invariant guard test extension.
**Avoids:** Pitfalls 6, 9, 10.

#### Phase 3 — Zip-Import Convergence + Test Cleanup + Schedule-Task Removal
**Rationale:** Zip-import convergence must come *after* Phase 2 stabilizes the orchestrator surface. Layer B test cleanup ratchets behind proven-correct Phase 2 layer-A coverage.
**Delivers:** Shared `BuildContentEntryForArea` builder, `DeserializeFromZipCommand` rewritten via in-memory `Manifest`, Layer B test port (`SqlTableProviderDeserializeTests`, `SqlTableProviderSeedMergeTests`, `SqlTableLinkResolutionIntegrationTests`, `ContentProviderTests`, `SerializerDeserializeCommandTests`, `SerializerSerializeCommandTests`, `StrictModeIntegrationTests`), shim removal, `[Obsolete]` overload removal, schedule-task removal (already in PROJECT.md Active list), Swift 2.2 baseline E2E re-validation + DAP/pim.carriageservices live deploy.
**Avoids:** Pitfalls 7, 8.

### Phase Ordering Rationale
- Phase 2 reads what Phase 1 writes — atomic-write + schema-version + entry shape on disk before reader is testable.
- Test-coverage ratchet: round-trip property test (Phase 1) → orchestrator unit tests on entry fixtures (Phase 2) → bulk integration test port (Phase 3). No big-bang.
- Zip-import last because it benefits from stable `BuildContentEntryForArea` (Phase 1) + stable `DeserializeAll(manifest,...)` (Phase 2).

### Research Flags
**Needs research (`/gsd-research-phase`):**
- **Phase 2** — STJ polymorphism quirks at discriminator-property level (dotnet/runtime #78338, #110248, #118786). 30-min spike to confirm writer pins position-0 deterministically and reader handles non-zero position with typed error not `NotSupportedException`.
- **Phase 3** — zip-import path with strict-mode escalator. Today zip bypasses strict-mode entirely; verify in-memory-`Manifest` route makes strict mode work transparently.

**Standard patterns (skip research):**
- **Phase 1** — schema definition, atomic-write/temp-file/rename, polymorphic record hierarchy. All well-trodden, MS Learn-documented.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | MS Learn docs verified 2025-12-04 / 2025-01-15; existing reuse points verified by file read |
| Features | MEDIUM-HIGH | Liquibase/Terraform/Pulumi/Helm/DbUp/FluentMigrator current docs verified; Octopus weighted lower |
| Architecture | HIGH | Grounded in current code; `EmbeddedXmlProvider` non-existence verified by Glob+Grep |
| Pitfalls | HIGH | Internal incident history (Phase 37 cache-invalidator, FINDINGS F-04/F-10); STJ quirks from open dotnet/runtime issues |

**Overall confidence:** HIGH

### Settled Open Questions (from milestone brief — recommendations, not just listed)

1. **`ExcludeFieldsByItemType` / `ExcludeXmlElementsByType` location?** **Bake into `Manifest` envelope at serialize time** (top-level, not per-entry). They affect on-disk artifact shape, so they're properly part of the artifact. This is what cleanly removes `ConfigLoader.Load` from `SerializerDeserializeCommand`. Per ARCHITECTURE §5.
2. **Strict mode default location?** **Option (a): entry-point default + per-call request override; drop config consultation entirely.** Per PITFALLS §10. `StrictModeResolver.Resolve(entryPoint, configValue: null, requestValue)` already supports this. Cost: surface a one-time WARNING when `config.StrictMode` is set but no longer consulted on deserialize.
3. **Phase decomposition (architecture said 2, pitfalls said 5–6)?** **3 phases**, per reconciliation above.

### Open Questions Remaining for Roadmapper / Discuss-Phase

- **`EntryStatus.Skipped` exact semantics:** providerFilter excluded? `dependsOn` upstream failed? dry-run? Decide before Phase 2 to keep `EntryOutcome.From(...)` consistent across providers.
- **`complete: true` sentinel position vs discriminator position-0:** envelope-level vs entry-level so they don't conflict, but writer order needs explicit confirmation in Phase 1.
- **`EntryId` derivation rule** (`"content/area-{p.AreaId}{p.Path}"` / `"sql/{p.Table}"`): verify uniqueness across the full Swift 2.2 baseline (~30 predicates) before locking — whole-area predicates may collide on path-based ids.
- **Embedded XML follow-up timing:** option (β) carving out `EmbeddedXmlProvider` is deferred. Roadmapper decides if v0.7.0 picks it up immediately or it stays open until a third provider lands.

## Sources

### Primary (HIGH)
- [STJ polymorphism — MS Learn](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/polymorphism)
- [Handle unmapped members — MS Learn](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/missing-members)
- Local files: `Infrastructure/ManifestWriter.cs`, `ManifestCleaner.cs`, `Providers/SerializerOrchestrator.cs`, `Providers/SerializationProviderBase.cs`, `ISerializationProvider.cs`, `SerializeResult.cs`, `Providers/Content/ContentProvider.cs`, `Providers/SqlTable/SqlTableProvider.cs`, `FkDependencyResolver.cs`, `Models/ProviderPredicateDefinition.cs`, `Configuration/ConfigLoader.cs`, `Infrastructure/StrictModeEscalator.cs`, `AdminUI/Commands/SerializerDeserializeCommand.cs`, `DeserializeFromZipCommand.cs`
- Internal incident history: Phase 37 cache-invalidator silent-skip, baseline FINDINGS F-04/F-10
- [Liquibase docs](https://docs.liquibase.com/) — apply-pipeline reference shape
- [Terraform JSON output format](https://developer.hashicorp.com/terraform/internals/json-format) — self-describing artifact

### Secondary (MEDIUM)
- [dotnet/runtime#78338](https://github.com/dotnet/runtime/issues/78338), [#110248](https://github.com/dotnet/runtime/issues/110248), [#118786](https://github.com/dotnet/runtime/issues/118786)
- [Pulumi state](https://www.pulumi.com/docs/iac/concepts/resources/names/), [Helm Charts](https://helm.sh/docs/topics/charts/)
- [Sitecore Unicorn](https://github.com/SitecoreUnicorn/Unicorn) (cultural reference, NOT manifest-driven on apply)
- [Atomic File Writes on Windows](https://antonymale.co.uk/windows-atomic-file-writes.html)

### Detailed research files
- `.planning/research/STACK.md`
- `.planning/research/FEATURES.md`
- `.planning/research/ARCHITECTURE.md`
- `.planning/research/PITFALLS.md`
