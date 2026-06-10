# Pitfalls Research — Manifest-Driven Deserialize (v0.6.0)

**Domain:** Pivoting a config-driven dispatcher (`SerializerOrchestrator.DeserializeAll`) to a manifest-driven one (`{mode}-manifest.json` with per-Entry dispatch).
**Researched:** 2026-05-08
**Confidence:** HIGH on internal failure modes (grounded in v0.4.0 / v0.5.0 incident history); MEDIUM on cross-tool patterns (Liquibase, Sitecore Unicorn, Helm) cited by analogy.

> Scope: failure modes a roadmap must defend against. Generic "test things" advice is excluded. Each pitfall ties to a roadmap phase and a "won't-happen" success criterion the roadmapper can lift verbatim.

---

## Critical Pitfalls

### Pitfall 1: Torn manifest from a crashed serialize

**What goes wrong:**
Serialize emits N YAML files, then crashes (DB timeout, OOM, host kill) before `ManifestWriter.Write` runs — or after some files but before manifest contents finalize. Disk is left with two pathological states:

- **Files-without-manifest:** stray YAML on disk, no manifest references them. Subsequent serialize-then-clean *appears* fine (cleaner deletes the orphans), but if the next operation is *deserialize*, the prior `{mode}-manifest.json` from a successful older run is what gets read — silently shipping a stale snapshot.
- **Manifest-references-missing-files:** manifest written before some files (unlikely with current `Write` order, but trivial to introduce when entries grow `BuildManifestEntry` writes per-file). Deserialize then fails per-entry on `File.ReadAllText`, with no global "this manifest is broken" signal.

The current `ManifestWriter.Write` uses `File.WriteAllText`, which is **not atomic on Windows** — a kill at exactly the wrong moment leaves a truncated JSON file that throws `JsonException` at deserialize time.

**Why it happens:**
Naive single-step writes. Serialize is treated as "many independent file writes plus a manifest"; nothing marks the run as a unit of work. Crash recovery is an afterthought — current code in `SerializerOrchestrator.SerializeAll` (lines 114–122) writes the manifest only *after* the predicate loop finishes, meaning any exception thrown in a provider leaves files on disk with no manifest at all.

**How to avoid:**
Three layered defenses, all cheap:

1. **Atomic manifest write.** Write to `{mode}-manifest.json.tmp`, fsync, then `File.Move(tmp, final, overwrite: true)`. .NET's `File.Move` with `overwrite` falls through to `MoveFileEx(MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH)` on NTFS — close enough to atomic for this use case.
2. **Schema-version + completion sentinel inside the manifest itself.** Add `"complete": true` and `"schemaVersion": 1` as the *first* fields written. A truncated JSON parse fails fast; a manifest missing `"complete": true` is treated as "torn — refuse to deserialize".
3. **Refuse to deserialize with a *stale* manifest if any unreferenced YAML files exist under `modeRoot`.** Currently `ManifestCleaner` deletes them post-serialize — but if serialize crashed, cleaner never ran. On the deserialize path, an unexpected file is a smoking gun for a failed prior run. Fail-loud.

**Warning signs:**
- `JsonException` on manifest read with no preceding "Manifest written" log line.
- Stray YAML files under `modeRoot/...` not present in `manifest.entries[*].files`.
- Manifest `writtenAtUtc` newer than the newest YAML file under it (impossible if serialize succeeded; means manifest was rewritten standalone).

**Phase to address:**
**Phase 42 (Manifest schema + atomic write)** — the foundation phase. Atomic write + schema version + completion sentinel must land before any provider builds entries, otherwise every provider phase ships its own torn-write bug.

---

### Pitfall 2: Lost post-processing metadata silently skips drift

**What goes wrong:**
This has already happened once. v0.5.0 / Phase 37 added an explicit WARNING ([SerializerOrchestrator.cs:274](src/Truvio.Commerce.Serializer/Providers/SerializerOrchestrator.cs)):

> `WARNING: Predicate '{Name}' declares {N} service cache(s) but no CacheInvalidator is wired — caches will NOT be cleared`

That fix only catches the *infrastructure* gap (CacheInvalidator absent). The new failure mode is shape-equivalent but at the data layer: if `BuildManifestEntry` on a provider forgets to copy `ServiceCaches` (or `SchemaSync`, `ResolveLinksInColumns`, `ExcludeAreaColumns`, `Excludes`) into the manifest entry, deserialize-side post-processing **never fires** — and there is no warning, because as far as the deserializer can see, the entry simply has no caches to clear.

The current predicate has *eight* fields the orchestrator threads into deserialize-time behavior ([ProviderPredicateDefinition.cs:53–108](src/Truvio.Commerce.Serializer/Models/ProviderPredicateDefinition.cs)): `ServiceCaches`, `SchemaSync`, `XmlColumns`, `ExcludeFields`, `ExcludeXmlElements`, `ExcludeAreaColumns`, `ResolveLinksInColumns`, `AcknowledgedOrphanPageIds`. Each is a distinct silent-skip vector.

The cache-invalidator incident shipped in v0.4.x; it took weeks to detect in baseline-test FINDINGS F-10 ("string-based serviceCaches config is essentially dead code right now — the type-name→runtime-type lookup fails silently for most entries"). We do not want to repeat that lifecycle for seven more fields.

**Why it happens:**
Two-step data lifecycle (serialize-time read of predicate → manifest write → deserialize-time read of entry) with no mechanical guarantee that the second step preserves what the first cared about. A provider author writes `BuildManifestEntry`, looks at the *most obvious* fields (Name, AreaId, files) and ships. Subtle hint fields are forgotten, and the test fixtures don't exercise them because predicate-driven tests bypass `BuildManifestEntry` entirely.

**How to avoid:**
1. **Round-trip property test in the test pyramid:** for every predicate field that survives into deserialize behavior, assert `BuildManifestEntry(predicate).Round-trip → ManifestEntry → DeserializeEntry-internal-state` preserves the value. One test per field. This is THE defense — it doesn't require humans to remember.
2. **Make the entry shape declarative** — derive both from the same source of truth. If `Entry` is built by reflection-copying from `ProviderPredicateDefinition` in v0.6.0 (transitionally), forgetting a field becomes a compile error when the predicate type evolves. Less elegant than hand-built entries but mechanically safe.
3. **Reverse the cache-invalidator log shape on the entry side too:** "Entry '{Name}' has no `ServiceCaches`. If you expected post-deserialize cache invalidation, check `BuildManifestEntry` on the provider that produced this entry." Loud-when-empty, not just loud-when-broken.

**Warning signs:**
- A test that worked under predicate-driven dispatch fails post-pivot — the test asserts cache clears, manifest entry has no `ServiceCaches`, and the failure mode is "0 caches cleared" rather than "test crashes".
- Schema-sync warnings disappear from logs after the pivot ("we got faster!"). They didn't disappear; they were silenced.
- Production drift report: source-to-target page-id resolution fails on a SqlTable column that *used to* resolve. `ResolveLinksInColumns` was lost in entry build.

**Phase to address:**
**Phase 43 (BuildManifestEntry contract)** — the same phase that introduces `BuildManifestEntry` must ship the round-trip property test. Not a follow-up phase. The "field count on Entry == field count on PredicateDefinition that affects deserialize" should be a test, not a code review checklist item.

---

### Pitfall 3: Schema evolution leaves users stuck on incompatible manifests

**What goes wrong:**
Bob serializes against v0.6.0. Two months later, v0.7.0 lands with a renamed Entry field (`provider` → `providerType`, or a new required field). Bob's CI/CD pipeline pulls v0.7.0, runs deserialize against the v0.6 manifest, and:

- **Best case:** STJ throws `JsonException: missing required property` — pipeline halts, Bob investigates.
- **Common case:** STJ silently ignores the unknown old name, reads the missing new field as default, and *partially* deserializes — silently shipping a degraded baseline.
- **Worst case:** Two providers both register for the same payload because the discriminator changed shape.

The current scaffold has `schemaVersion` as a planned field but **no migration story** beyond fail-fast.

**Why it happens:**
Fail-fast on `schemaVersion mismatch` solves the *detection* half but stops there. Real-world adopters serialize with version N and deserialize with version N+k for N+k ≥ 1 routinely (the manifest *is* in their git history; they don't re-serialize on every CI run).

**How to avoid:**

1. **Lock the v0.6.0 schema explicitly.** Document `schemaVersion: 1` as the v0.6.0 contract. Any field added to Entry in v0.6.x patch releases must be optional with a compatible default.
2. **Two-step migration on read, not write.** When deserializer encounters `schemaVersion < current`, it runs an **in-memory upgrade pipeline** before dispatch (e.g. `UpgradeFromV1ToV2(json) → UpgradeFromV2ToV3(json)`). The original file on disk is untouched. This matches Liquibase's "checksum + upgrade-on-read" approach — Liquibase records a checksum per migration; mismatched checksums fail validation, but Liquibase also has explicit `ALTER` workflow for schema evolution. We need the analogous "manifest evolution" hook.
3. **Refuse to deserialize a *newer* manifest than the binary understands.** A v0.7 manifest fed to a v0.6 deployer should fail with a clear message ("manifest schemaVersion 2 > maxSupportedVersion 1, upgrade Truvio.Commerce.Serializer"). Better than silently dropping new fields.
4. **Document the migration policy in `docs/manifest-evolution.md`** at v0.6.0 launch. Set the precedent before the first bump.

**Warning signs:**
- A new field appears on Entry but no upgrader exists for older schema versions.
- Tests pass against the *current* schema but no test fixture pins a v1 manifest deserialized by v2 code.
- `StackOverflowException` or `JsonException` from prod after a release — almost always a schema mismatch the binary didn't catch.

**Phase to address:**
**Phase 42 (Manifest schema)** — version field + accept-only-schemaVersion=1 lands here. **Phase 47 (post-MVP migration story)** lands the upgrader infrastructure once we have an actual v2 to migrate to. Ship Phase 42 with a placeholder `MigrateOnRead` interface that throws "no upgraders registered" so the seam exists.

---

### Pitfall 4: System.Text.Json polymorphism — discriminator-property fragility

**What goes wrong:**
The new `entries[]` array is the textbook polymorphic-deserialization case: each entry is dispatched to a different provider, almost certainly via a `$type`-style discriminator. STJ has known traps that bite specifically here ([dotnet/runtime#78338](https://github.com/dotnet/runtime/issues/78338), [dotnet/runtime#118786](https://github.com/dotnet/runtime/issues/118786), [dotnet/runtime#110248](https://github.com/dotnet/runtime/issues/110248)):

- **Discriminator must be the FIRST property** in the JSON object. STJ uses a streaming parser; if the discriminator appears after other properties, it either throws `NotSupportedException` or silently picks the base type.
- **Discriminator key is case-sensitive even with `PropertyNameCaseInsensitive = true`.** A camelCase serializer policy applied globally will rename `$type` to `$type` (no change), but a custom `TypeDiscriminatorPropertyName = "ProviderType"` becomes `providerType` on write and *fails to match* `ProviderType` on read.
- **Discriminator cannot be marked `[Required]`** — STJ throws if you try.
- **Discriminator must have the shortest key length** in some configurations ([JasperFx/marten#2586](https://github.com/JasperFx/marten/issues/2586)). A `"providerType"` key (12 chars) silently wins over `"version"` (7 chars) — meaning if `"version"` is shorter than the discriminator, deserialization fails non-obviously.

The `ManifestWriter` already uses `PropertyNamingPolicy = JsonNamingPolicy.CamelCase` ([ManifestWriter.cs:23](src/Truvio.Commerce.Serializer/Infrastructure/ManifestWriter.cs)). Without explicit handling, the discriminator name will be camelCase'd asymmetrically with whatever attribute we choose.

**Why it happens:**
STJ's polymorphism story shipped in .NET 7 and is still rough. Most teams hit these issues only at production-scale data; toy round-trip tests use a single object literal and pass.

**How to avoid:**
1. **Use `$type` (with the dollar sign) as discriminator.** It's STJ's default and avoids the camelCase asymmetry entirely. Document the choice.
2. **Pin discriminator to position 0** by writing it manually with `Utf8JsonWriter` rather than letting the property-order be whatever reflection produces. Or accept that *reading* tolerates any position and only *writing* needs to be careful — but the bug surfaces if a hand-edit reorders fields.
3. **Make the discriminator non-optional via test, not via `[Required]`.** Round-trip test: every Entry round-trips its `$type`. Deserialize entry with missing `$type` → fail with a typed `ManifestSchemaException`, not `NotSupportedException`.
4. **Reject manifest if *any* entry's discriminator doesn't resolve to a registered provider.** Today `_registry.HasProvider` is checked per-entry; that's correct. But the failure must be per-entry-actionable, not "first failure aborts deserialize". See Pitfall 9 (per-entry reporting).

**Warning signs:**
- Manifest hand-edit moves a field above `$type`. Tests that worked against the un-edited file fail with `NotSupportedException`.
- A property added to Entry whose name is alphabetically or lexicographically before `$type` — STJ may pick it up first depending on writer policy.
- Different test runs produce different deserialization outcomes (rare but flagged in dotnet/runtime issues — depends on dictionary iteration order).

**Phase to address:**
**Phase 42 (Manifest schema)** — discriminator choice + writer pinning is part of the schema definition. Decision must be locked before any provider's `BuildManifestEntry` returns its first concrete subclass.

---

### Pitfall 5: Hand-edited manifest, drift between manifest and disk

**What goes wrong:**
A baseline reviewer opens `deploy-manifest.json` in their editor to "fix a typo in an entry name" and commits the change. Or git's auto-line-ending conversion mangles `\r\n` between Windows and Linux build hosts. Or someone deletes an entry to skip it for one deploy. Now manifest disagrees with the YAML files on disk.

Three concrete failure shapes:

- **Manifest references a file that doesn't exist** (file deleted, manifest entry not). On deserialize: per-entry `FileNotFoundException`.
- **File exists that no manifest entry references** (file added, manifest not regenerated). On deserialize: file is silently ignored — its content never deploys, but no warning fires because the loop iterates entries, not files.
- **Same entry appears twice** (duplicate from copy-paste edit). On deserialize: provider runs twice, second run sees data already deployed by first → conflict-strategy decides, possibly silently skipping correct work.

How other manifest-driven tools handle this:
- **Liquibase** records a checksum per change; mismatched checksum fails validation. Hand edits are mechanically detected.
- **Sitecore Unicorn** uses item field-level hashes via `RevisionPredicate` to detect drift — hand edits to YAML are the *normal* workflow, but the evaluator (not the predicate) compares content hashes per-field.
- **Helm 3.18.5** tightened JSON schema validation specifically to catch hand-edited drift in `values.schema.json`; some teams' deployments started failing because they had been silently relying on permissive validation.

**Why it happens:**
JSON files are inviting to edit. The YAML+manifest split makes it easy to fix one and forget the other. CI doesn't naturally catch the drift because CI only runs the deserialize side, which can't tell what *should* be there.

**How to avoid:**
**Recommendation: fail-loud on drift, with a `--allow-drift` escape hatch for emergencies.**

Specifically:

1. **Per-entry checksum** of the YAML file content, recorded in the manifest. On deserialize, recompute and compare. Mismatch = error (strict) / warning (lenient).
2. **Pre-flight scan** at deserialize start: enumerate `*.yml` under modeRoot. Cross-check against entry file lists. **Files without entries** = error in strict / warning in lenient ("file present but no manifest entry; refusing to deploy untracked content"). **Entries without files** = always error (we cannot proceed).
3. **No automatic regeneration on drift.** "Auto-rebuild manifest from disk" sounds helpful but defeats the purpose; the manifest is supposed to be the audit record of what serialize chose to emit.
4. **Match the strictness defaults:** Liquibase fails by default; Helm 3.18.5 fails by default; Unicorn warns by default but has strict-mode evaluators. Our `StrictModeEscalator` already exists — drift becomes another escalation channel.

This aligns with v0.5.0's strict-mode philosophy: production runs (CI/CD entry points) default strict, admin-UI runs default lenient ([StrictModeEscalator.cs:107–122](src/Truvio.Commerce.Serializer/Infrastructure/StrictModeEscalator.cs)).

**Warning signs:**
- Manifest `writtenAtUtc` more than ~1s older or newer than YAML file mtimes (within tolerance for clock skew).
- A YAML file's parsed name/identity doesn't match what the manifest entry claims.
- Git diff shows manifest unchanged but multiple YAML files changed (or vice versa).

**Phase to address:**
**Phase 44 (Drift detection on deserialize path)** — checksum + pre-flight scan + strict/lenient escalation. Ships *after* Phase 42/43 because it depends on the entry shape being stable.

---

### Pitfall 6: Manifest reorder breaks FK / link-resolution ordering

**What goes wrong:**
Today, `DeserializeAll` recomputes ordering live ([SerializerOrchestrator.cs:160–218](src/Truvio.Commerce.Serializer/Providers/SerializerOrchestrator.cs)):

- FK ordering: SqlTable predicates are sorted by FkDependencyResolver.
- LINK-02 ordering: any SqlTable with `ResolveLinksInColumns` non-empty forces Content predicates to run first.

Under the manifest model, a hand-edit (or a regenerate-from-disk shortcut) could reorder `entries[]`. If we trust the manifest order, a simple `mv` on the array breaks the deploy non-obviously.

If we recompute live, why even record the order in the manifest?

**Why it happens:**
"It's just an array; order is presentational" is the natural assumption. The actual constraint (Content-before-SqlTable-with-link-resolution) is invisible in the JSON and lives only in `SerializerOrchestrator.cs`.

**How to avoid:**
**Decision: Recompute, do not trust.** Concretely:

1. **Manifest order is informative, not authoritative.** Document that the order in `entries[]` is "the order serialize emitted them, recomputed at deserialize time".
2. **The deserialize ordering logic is unchanged in v0.6.0.** Same FK resolver, same LINK-02 reorder, but operating on `Entry` instead of `Predicate`. Each Entry needs to expose enough fields for the ordering decisions: `providerType`, `table` (SqlTable entries), `resolveLinksInColumns` (SqlTable entries).
3. **Optional Phase 47 polish:** validate that the manifest's recorded order *would have produced* the same final order if trusted — if not, log a single INFO line "manifest order differs from FK-recomputed order; using FK-recomputed". This surfaces the "someone hand-reordered" case without breaking it.
4. **Reject only the unrecoverable case:** if FK resolution *cannot* find an order (cycle), fail loudly. Today this would also fail; we just want to keep that working.

**Warning signs:**
- Deploy logs differ in entry execution order between two runs of the same manifest. Today this should not happen given deterministic ordering — if it does, either the resolver is non-deterministic or the manifest recomputation was skipped.
- A SqlTable with `resolveLinksInColumns` runs before its source Content entry. Symptom: `Unresolvable page ID NNNN in link` warnings spike.

**Phase to address:**
**Phase 43 (BuildManifestEntry / orchestrator pivot)** — the orchestrator port has to keep the existing ordering logic. This is part of the migration, not a follow-up.

---

### Pitfall 7: DeserializeFromZipCommand drifts from manifest-driven path

**What goes wrong:**
Today ([DeserializeFromZipCommand.cs:74–93](src/Truvio.Commerce.Serializer/AdminUI/Commands/DeserializeFromZipCommand.cs)) the zip-import path constructs a synthetic predicate:

```csharp
var importPredicate = new ProviderPredicateDefinition {
    Name = "ZipImport", ProviderType = "Content",
    AreaId = TargetAreaId, Path = "/", PageId = 0,
    Excludes = new List<string>()
};
```

…and bypasses the orchestrator entirely, calling `ContentDeserializer` directly.

Under the manifest model, this code path has three plausible futures, each with a failure mode:

1. **Zip contains a manifest** — read it, dispatch as normal. **Risk:** zip imports historically contained YAML only. Older zips break.
2. **Zip contains no manifest, synthesise an Entry inline** — drift between this synthesis path and the real `BuildManifestEntry` on the Content provider. The existing inline construction already missed `Includes`/`ServiceCaches`/`SchemaSync` on the predicate; the same forgetfulness will recur on Entry.
3. **Refuse to import a zip without a manifest** — most user-friendly is the most disruptive (zips from v0.5 stop working).

The core risk is divergence: two paths that *should* produce identical Entries diverge in subtle ways, and the divergence ships silently because the zip-import path doesn't run on most CI runs.

**Why it happens:**
Zip-import is "just one provider, one area" — no orchestration needed, hence the bypass. That convenience is exactly what drifts.

**How to avoid:**
**Recommendation: option 2 + force convergence via shared builder.**

1. **`Entry BuildContentEntryForArea(int areaId, string path, ...)`** — single entry-builder method on `ContentProvider`. Both real-serialize (via `BuildManifestEntry`) and zip-import (via this method called from `DeserializeFromZipCommand`) call the same builder. Zip-import doesn't need a manifest *file*; it just needs to produce a synthetic in-memory manifest and dispatch through `DeserializeAll`.
2. **Zip-import path shrinks to:**
   ```
   extract zip → BuildContentEntryForArea(targetAreaId, "/") → in-memory Manifest with one entry → DeserializeAll(manifest)
   ```
   No `ContentDeserializer` direct call. No bypassing of orchestrator-level features (strict mode, FK ordering — moot here, link resolution — moot here).
3. **Zips from v0.5 (manifest-less) are accepted via this synthesis.** Zips from v0.6+ MAY contain a manifest; if they do, prefer it. Single behavioral note in docs.

**Warning signs:**
- Strict mode works on the `SerializerDeserializeCommand` path but doesn't work on `DeserializeFromZipCommand`. (Already true today; we'd be perpetuating it.)
- A change to `BuildManifestEntry` doesn't propagate to zip-import behavior.
- Zip-imported content lacks post-processing that disk-based imports got (cache invalidation, schema sync) — silent drift in production.

**Phase to address:**
**Phase 45 (Zip-import convergence)** — runs *after* Phase 43 (BuildManifestEntry exists). Don't try to refactor zip-import in parallel with the orchestrator pivot; sequence avoids merge conflicts and lets the test suite stabilize between.

---

### Pitfall 8: Test churn blows up the diff and hides regressions

**What goes wrong:**
Today's test suite has predicate fixtures everywhere — `ProviderPredicateDefinition` instances built inline in test bodies, JSON config fixtures with predicate arrays, helper builders that produce predicates. A naive "rename Predicate → Entry, fix everything" sweep produces a ~thousand-line PR diff in tests alone, in which a regression in actual *behavior* hides among rename noise.

The risk shape: reviewer fatigue → "looks like search-and-replace" → ship → bug.

**Why it happens:**
Predicate-driven test coverage was the *correct* coverage strategy under v0.5.0. The pivot makes it the wrong shape, but inertia keeps it.

**How to avoid:**
**TDD-friendly migration path: shim predicate-from-entry, port test layers in order.**

1. **Phase 43 introduces `Entry` as the new dispatch target,** and a one-liner shim `Predicate ToPredicate(Entry e) => ...` so legacy predicate-driven tests work unchanged. Orchestrator internally consumes Entry; the shim only exists in test-helper code.
2. **Two test layers, ported in sequence:**
   - **Layer A — orchestrator unit tests** (~30 tests). Port these to use Entry directly. Small, contained, high signal. Land in Phase 43.
   - **Layer B — provider-roundtrip integration tests.** Keep using predicate fixtures but route through `Provider.BuildManifestEntry(predicate)` to validate the round-trip property. New tests, not edited. Land in Phase 43.
3. **Bottom-up port of remaining tests in Phase 46 (test cleanup),** at which point the shim is removed. By that point Layers A and B have ratchet-style validated correctness, so the remaining bulk port is safe.
4. **No big-bang. No PR with >300 line test changes if avoidable.** Each phase's test diff stays human-reviewable.

**Warning signs:**
- A single PR has both new feature changes and >500 lines of test reshaping. This is the high-risk shape.
- Test names that are misleading post-pivot ("predicate fixture") but still pass — meaning they no longer test what the name claims.
- New tests added on the pivot land green, but pre-existing tests are touched only mechanically — high chance the pre-existing tests don't actually exercise the new code path.

**Phase to address:**
**Phase 43 (orchestrator pivot)** — port Layer A. **Phase 46 (test cleanup)** — port Layer B remainder, remove shim.

---

### Pitfall 9: Per-entry reporting that aggregates wrong

**What goes wrong:**
PROJECT.md commits to *per-entry succeeded/failed/warned reporting* in `OrchestratorResult`. The shape today aggregates by predicate ([SerializerOrchestrator.cs:355–399](src/Truvio.Commerce.Serializer/Providers/SerializerOrchestrator.cs)), which already loses some granularity. Naive ports preserve the bug:

- One entry processes multiple files (a Content entry covers many pages). If three pages succeed and one fails, "succeeded/failed/warned" is *what* — succeed-1-fail-1-warn-0? Or succeed-3-fail-1?
- A warning during entry processing escalates via `StrictModeEscalator` end-of-run. The per-entry report says "warned: yes", but if the user inspects only the per-entry summary they don't see *which* warnings escalated.
- `result.HasErrors` today drives HTTP status mapping ([SerializerDeserializeCommand.cs:199–206](src/Truvio.Commerce.Serializer/AdminUI/Commands/SerializerDeserializeCommand.cs)). If we add per-entry status without rewiring `HasErrors`, an entry-level failure with zero predicate-level errors returns HTTP 200 OK.

**Why it happens:**
"Add a list" is the simplest implementation. Status aggregation is fiddly, easy to get wrong, and the existing API contract (`HasErrors` ⇒ HTTP error) is load-bearing for D-38-12 callers.

**How to avoid:**
1. **Define entry status as a sum type, not a boolean tuple:** `EntryOutcome { Succeeded | SucceededWithWarnings | FailedRecoverable | FailedFatal | Skipped }`. Forces explicit handling of the warning case.
2. **Define what "succeeded" means per provider in the entry contract.** A Content entry succeeds = every page in its `files[]` deserialized. SqlTable entry succeeds = every row processed without error. Document at provider level; test both halves.
3. **Aggregate `HasErrors` from the entry list, not from a parallel error collection.** `HasErrors = entries.Any(e => e.Outcome is FailedRecoverable or FailedFatal)`. Single source of truth.
4. **Test for HTTP status invariant:** synth a result with one `FailedRecoverable` entry and zero predicate-level errors; assert HTTP returns Error. Pin it as a guard test (the existing D-38-12 guard is good prior art — extend it).

**Warning signs:**
- HTTP status returns OK while individual entries report failure. Users tail the log and see errors that didn't fail the build.
- Per-entry status matrix is asymmetric: Content entries have rich status, SqlTable entries are succeeded/failed only because nobody added the granular states.
- `OrchestratorResult.Summary` string format breaks downstream parsers (CI/CD scripts that grep for "Errors:" pattern). Backcompat note: per memory `feedback_no_backcompat.md` we're greenfield enough to reshape, but make the change deliberate, not accidental.

**Phase to address:**
**Phase 43 (orchestrator pivot)** — `OrchestratorResult` reshape happens with the dispatch port, not after. Reshaping the result type later means re-touching every API/CLI/AdminUI test.

---

### Pitfall 10: Strict-mode default location — config vs runtime vs entry-point

**What goes wrong:**
The user explicitly flagged this as an open question. Today strict mode is read from `config.StrictMode` and defaulted via `StrictModeResolver` based on entry point ([StrictModeEscalator.cs:107–122](src/Truvio.Commerce.Serializer/Infrastructure/StrictModeEscalator.cs)). Since the v0.6.0 milestone *removes config consultation from the deserialize path*, strict mode loses its current home.

Three options:

| Option | Mechanism | Pros | Cons |
|--------|-----------|------|------|
| **(a) Hardcoded by entry-point** | `StrictModeResolver.Resolve(entryPoint, configValue: null, requestValue)` — no config layer | Simplest. No new file. Honors v0.6.0 "no config on deserialize". | Loses per-environment override. CI/CD can't say "this prod env is strict, this stage env is lenient" without flag plumbing on every command. |
| **(b) Tiny `serializer-runtime.json` next to manifest** | Read from `{modeRoot}/serializer-runtime.json` if present — strict, dryRun, conflictStrategy | Co-located with manifest. Fits "all knowledge in artifacts" goal. Still no fully-fledged config. | New file = new spec = new lifecycle. Becomes a config file by stealth. |
| **(c) Keep reading `config.StrictMode` only** | Don't break the strict-mode-from-config path even if other config consultation is dropped | Zero migration. Existing users keep working. | Violates the milestone goal ("drop ConfigLoader.Load from the deserialize path"). Half-pivot. |

**Recommendation: (a) hardcoded by entry-point, with explicit per-call override.**

Rationale:

1. **Aligns with milestone goal.** v0.6.0 explicitly removes config consultation. Option (c) fights the milestone; option (b) re-introduces config under a different name.
2. **The use case for per-environment strict-vs-lenient is already covered by the request-parameter override.** `StrictModeResolver.Resolve(entryPoint, configValue: null, requestValue)` already accepts a per-call value with highest precedence ([StrictModeEscalator.cs:111](src/Truvio.Commerce.Serializer/Infrastructure/StrictModeEscalator.cs)). CI/CD environments pass it explicitly via `--strict=true|false` or query param; admin-UI clicks pass nothing and get the entry-point default.
3. **Option (b) sounds like a tiny convenience but has 80% of config's lifecycle costs.** Where does it live in git? Who edits it? How do tests fixture it? Once it exists, it grows fields.
4. **Per-environment strict policy belongs in the deploy-pipeline shell, not in the artifact.** A baseline that's strict-only-on-prod is a deploy-time policy, not an artifact-level fact.
5. **Backward-compatibility cost is low** per `feedback_no_backcompat.md` — no users depending on `config.StrictMode` behavior we'd break. The path being removed *will* break (correctly), and the migration is "set the entry-point default in your CI script."

What option (a) **costs**: nothing new to design. What it **gains**: a sharp answer to "where does runtime policy live?" — it lives in the entry-point and the request, period. No third location.

**Risks of (a) we accept:**
- A user who sets `"strictMode": false` in their `Serializer.config.json` today and relies on it for the deserialize path won't notice it stopped being consulted until something escalates. Surface a one-time WARNING when config has `StrictMode` set but the deserialize path no longer reads it.
- An admin-UI run that flips strict to lenient via a per-call flag still works because of the request override.

**Warning signs:**
- A user files a bug "my strict mode setting isn't being honored". The likely cause is config-side setting + Option (a) ignoring it.
- Tests that fixture `config.StrictMode = true` and expect strict behavior on the deserialize path. These tests must port to passing strict via the request.

**Phase to address:**
**Phase 43 (orchestrator pivot)** — the `DeserializeAll` signature already takes an `escalator` parameter. Removing config plumbing means the resolver is called once at the command boundary (`SerializerDeserializeCommand.Handle`) and the escalator is constructed from the result. Lock the policy at the same time as the dispatch pivot.

---

## Technical Debt Patterns

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|----------------|-----------------|
| Skip atomic write — `File.WriteAllText` for manifest | One-line code | Torn manifests on crash; baseline corruption that ships to prod | **Never** — atomic write costs ~5 lines |
| Auto-rebuild manifest from disk on drift | "Just works" UX | Defeats the audit-trail purpose of the manifest; users lose the ability to detect "someone added an unexpected file" | **Never** for production; opt-in for dev-loop maybe |
| Reflection-copy Predicate→Entry instead of explicit `BuildManifestEntry` | Avoids Pitfall 2 mechanically | Couples Entry shape to Predicate shape forever; can't evolve them independently | Acceptable as a *transitional* shim in Phase 43, removed by Phase 46 |
| Trust manifest order; skip live FK recomputation | Less code in `DeserializeAll` | Hand-edit reorder bugs become silent prod failures | **Never** — the FK resolver is cheap to run |
| Skip the per-entry checksum; rely on file presence only | One JSON field saved | Hand-edit drift goes undetected; Pitfall 5 ships in production | Acceptable for v0.6.0 IF Phase 44 lands within the next milestone; not acceptable indefinitely |

## Integration Gotchas

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|------------------|
| System.Text.Json polymorphism | Custom discriminator name + camelCase policy → asymmetric serialize/deserialize | Use `$type`; pin discriminator to first-property in writes |
| Windows `File.Move` | Assume non-overwriting move = atomic | Pass `overwrite: true`; verify NTFS in test environment matches CI environment |
| YamlDotNet round-trip + JSON manifest | Different libraries normalize line endings differently → checksum mismatches | Compute checksum on bytes, not strings; specify exact normalization (e.g., LF-only) at write time |
| DW Schedule task config (legacy entry point) | Builds predicates from config; expects them to work on deserialize | Phase 43: schedule task removed (per PROJECT.md "Remove scheduled tasks") — deprecate cleanly, don't half-port |
| Dynamicweb log file format | Per-entry status updates breaking the LogFileSummary contract | Reshape `LogFileSummary.Predicates` → `LogFileSummary.Entries` at the same time as orchestrator result type |

## Performance Traps

Manifest-driven deserialize doesn't introduce new perf-cliff scale thresholds beyond what predicate-driven had; same row counts processed. Two new traps worth flagging:

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|----------------|
| Entry list grows unbounded if every Content page is its own entry | JSON parse time on manifest read >100ms; manifest file >5MB | Granularity of Entry = predicate-equivalent (one Entry per former predicate), not one Entry per file | Trips at ~5,000 entries; Swift 2.2 baseline today is ~30 predicates → ~30 entries → fine |
| Per-entry checksum on a 1500-page baseline | Pre-flight scan time >10s; CI feedback loop slows | Checksum at serialize time, store in manifest; deserialize-side compare is then file-read + memcmp, not file-rehash | Trips above ~10,000 files |

## Security Mistakes

| Mistake | Risk | Prevention |
|---------|------|------------|
| Symlink in manifest's `files[]` lets entry resolve to a path outside `modeRoot` | Path traversal — deserialize reads/overwrites arbitrary files (already exploitable via `ManifestCleaner.CleanStale` if the corresponding guard is missing on the read side) | Reuse the `T-37-01-01` resolved-path-must-start-with-modeRootPrefix check from `ManifestCleaner` ([ManifestCleaner.cs:24–55](src/Truvio.Commerce.Serializer/Infrastructure/ManifestCleaner.cs)) on the deserialize side. Ship as part of Phase 43. |
| Manifest field that names a SQL table not in the predicate-time validation set | Phase 37-03 SqlTable provider validates table names against `INFORMATION_SCHEMA`; Entry might bypass that gate if dispatch trusts the manifest | Re-run identifier-whitelist validation on Entry dispatch — the validation lives in the provider's deserialize path today; verify it still runs when input is Entry-shaped, not Predicate-shaped |
| Manifest entry contains a `where` clause (currently a Predicate field per Phase 37-03) | Same SQL-injection surface as today | Keep the `SqlWhereClauseValidator` invocation; if the Entry copies `where` from Predicate, validation must also copy |

## UX Pitfalls

| Pitfall | User Impact | Better Approach |
|---------|-------------|-----------------|
| Error on deserialize says "manifest schema version mismatch" with no remediation | Operator hits the error in CI/CD with no fix path | Error message includes the version expected, the version found, and the upgrade command (or "no upgrader available; serialize again with v0.7+"). The escalator already does this for warnings; mirror the pattern for errors |
| Drift detection fires loudly on the first run after the user hand-edited a YAML to fix a typo | Operator views it as noise; turns off strict mode | Drift reporter shows a per-file diff line ("`Pages/Home.yml`: file changed since manifest written, checksum mismatch"). Concrete, actionable. Lifts directly from Liquibase's drift-report style |
| Per-entry status table prints 5,000 rows when 5,000 entries succeed | Log scrollback overflow; CI log truncated | Default to summary; show per-entry detail only on `--verbose` or when there are failures. Same pattern as the existing `LogFileSummary.Advice` field |

## "Looks Done But Isn't" Checklist

- [ ] **`BuildManifestEntry` per provider:** verify all eight predicate-side hint fields (`ServiceCaches`, `SchemaSync`, `XmlColumns`, `ExcludeFields`, `ExcludeXmlElements`, `ExcludeAreaColumns`, `ResolveLinksInColumns`, `AcknowledgedOrphanPageIds`) are copied into Entry — round-trip property test asserts each.
- [ ] **Atomic manifest write:** `ManifestWriter.Write` uses temp-file + `File.Move(overwrite: true)`. Test simulates kill-after-temp-write, verifies original manifest unchanged.
- [ ] **Schema version field:** `manifest.schemaVersion = 1` written; deserialize rejects mismatch with typed exception.
- [ ] **Polymorphic discriminator:** `$type` chosen; round-trip test asserts position-0 in writes.
- [ ] **Pre-flight drift scan:** files-without-entries triggers warn (lenient) / error (strict); entries-without-files triggers error always.
- [ ] **Per-entry checksum:** computed at serialize, verified at deserialize; mismatch escalates through `StrictModeEscalator`.
- [ ] **Zip-import path:** routes through shared `BuildContentEntryForArea` builder, not direct `ContentDeserializer` call.
- [ ] **Strict-mode resolution:** entry-point default + request override; config.StrictMode no longer consulted on deserialize path; one-time warning when config has obsolete value.
- [ ] **HTTP status invariant:** zero entry-level errors → HTTP OK; ≥1 `FailedRecoverable`/`FailedFatal` entry → HTTP Error. Pin via guard test.
- [ ] **Test shim removed:** no `Predicate ToPredicate(Entry)` helper remains in tests by end of Phase 46.

## Recovery Strategies

| Pitfall | Recovery Cost | Recovery Steps |
|---------|---------------|----------------|
| Torn manifest detected | LOW | Re-run serialize. Cleaner + atomic write produces a clean state |
| Manifest schema mismatch (older binary, newer manifest) | LOW–MEDIUM | Upgrade `Truvio.Commerce.Serializer` package; re-run deserialize. Migration story (Phase 47) makes this MEDIUM if upgraders accumulate |
| Hand-edit drift detected by checksum | LOW | Operator inspects drift report; either accepts the edit (re-serialize to refresh manifest) or reverts. No data loss |
| Lost post-processing metadata after `BuildManifestEntry` bug | MEDIUM–HIGH | Once shipped to prod and a deploy ran with `ServiceCaches` lost, caches are stale until manual app restart. Operationally recoverable, but requires identifying the symptom (stale data in admin UI) |
| Per-entry status mis-aggregation → false-positive HTTP 200 | HIGH | Failed deploys ship as successful; only detected by downstream symptoms. Recovery is forensic — comb logs, identify the run, manually replay |
| Discriminator collision (two providers register the same type name) | LOW | Caught at registry load (Phase 43 should validate uniqueness); fail fast at startup, not at deserialize |

## Pitfall-to-Phase Mapping

| Pitfall | Prevention Phase | Verification |
|---------|------------------|--------------|
| 1: Torn manifest | Phase 42 (Manifest schema + atomic write) | Test: kill process between temp-write and rename → original manifest readable |
| 2: Lost post-processing metadata | Phase 43 (BuildManifestEntry contract) | Round-trip property test per hint field; `HasErrors` invariants on cache-clear/schema-sync/link-resolution |
| 3: Schema evolution | Phase 42 (version field) + Phase 47 (upgrader infra) | Test: synthetic v2 manifest fed to v1 binary → typed `ManifestSchemaException` |
| 4: STJ polymorphism fragility | Phase 42 (schema definition) | Test: hand-construct manifest with discriminator at non-zero position → still deserializes (or fails with typed error, not `NotSupportedException`) |
| 5: Hand-edited drift | Phase 44 (drift detection) | Test: tamper with one YAML byte → checksum mismatch → strict raises, lenient warns |
| 6: Manifest reorder breaks ordering | Phase 43 (orchestrator pivot) | Test: shuffle `entries[]` → deserialize order matches FK-recomputed order (deterministic) |
| 7: DeserializeFromZipCommand drift | Phase 45 (zip-import convergence) | Test: zip-import with strict mode honors strict mode; cache-invalidation fires on zip-imported content |
| 8: Test churn hides regression | Phase 43 (Layer A port) + Phase 46 (Layer B port) | PR diff size budget: no PR with >300 lines of mechanical test changes |
| 9: Per-entry reporting aggregates wrong | Phase 43 (OrchestratorResult reshape) | Test: synth result with one failed entry, zero predicate errors → `HasErrors == true`, HTTP Error |
| 10: Strict-mode default location | Phase 43 (orchestrator pivot) | Test: config with `StrictMode: true` ignored on deserialize path; one-time WARNING fires; per-request override wins |

## Sources

- **Internal incident history:** Phase 37 cache-invalidator silent-skip fix ([SerializerOrchestrator.cs:274](src/Truvio.Commerce.Serializer/Providers/SerializerOrchestrator.cs)); baseline test FINDINGS F-04 (stale output), F-10 (cache types not resolved); v0.5.0 strict-mode design ([StrictModeEscalator.cs](src/Truvio.Commerce.Serializer/Infrastructure/StrictModeEscalator.cs))
- **System.Text.Json polymorphism issues:**
  - [dotnet/runtime#78338 — discriminator must be first property](https://github.com/dotnet/runtime/issues/78338)
  - [dotnet/runtime#118786 — case-insensitive discriminator bug](https://github.com/dotnet/runtime/issues/118786)
  - [dotnet/runtime#110248 — discriminator cannot be required](https://github.com/dotnet/runtime/issues/110248)
  - [JasperFx/marten#2586 — discriminator key length quirk](https://github.com/JasperFx/marten/issues/2586)
  - [Microsoft Learn — STJ polymorphism docs](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/polymorphism)
- **Atomic file write patterns:**
  - [Antony Male — Atomic File Writes on Windows](https://antonymale.co.uk/windows-atomic-file-writes.html)
  - [HN — pain with atomic writing on Windows](https://news.ycombinator.com/item?id=16573770)
- **Drift / hand-edit detection in similar tools:**
  - [Liquibase Drift Detection (community 4.23 and lower)](https://support.liquibase.com/hc/en-us/articles/29383072320667-How-to-use-Drift-Detection-Version-4-23-0-and-lower)
  - [Liquibase docs — drift detection](https://docs.liquibase.com/workflows/liquibase-community/drift-detection.html)
  - [Liquibase blog — Detect and Prevent Database Schema Drift](https://www.liquibase.com/blog/database-drift)
  - [Sitecore Unicorn — predicates vs evaluators](https://blog.martinmiles.net/post/separating-content-items-from-definition-using-unicorn-s-newitemsevaluator)
  - [Unicorn — working with serialized items](https://unicorn.kamsar.net/working-with-unicorn.html)
- **Helm schema validation:**
  - [Helm 3.18.5 schema validation tightening — community report](https://community.replicated.com/t/helm-3-18-5-upgrade-impact-schema-validation-changes/1577)
  - [Helm — Charts](https://helm.sh/docs/topics/charts/)

---
*Pitfalls research for: manifest-driven deserialize pivot (v0.6.0)*
*Researched: 2026-05-08*
