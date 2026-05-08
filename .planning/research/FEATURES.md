# Feature Research — Manifest-Driven Deserialize (v0.6.0)

**Domain:** Environment-to-environment data sync via versioned artifact + apply pipeline
**Researched:** 2026-05-08
**Confidence:** MEDIUM-HIGH (Sitecore Unicorn, Liquibase, Terraform, Helm verified against current docs; Pulumi state shape verified; DbUp/FluentMigrator verified; Octopus less directly comparable so weighted lower)

## Reference Systems Surveyed

How each comparable system makes its apply path executable from the artifact alone, without re-reading the authoring config:

| System | Artifact | Per-Entry Carries | Apply path needs config? |
|--------|----------|-------------------|--------------------------|
| **Sitecore Unicorn (Rainbow)** | One YAML file per item under a tree root | Item ID (GUID), Parent ID, Template, Path, fields. Predicate config is still consulted at apply for include/exclude evaluation — Unicorn's apply is *not* fully manifest-driven. | YES (predicate is read at sync time) |
| **Sitecore Content Serialization (SCS / module.json)** | YAML files + per-module `module.json` declaring includes | Module name, includes (path/scope), database. Each YAML still self-identifies (id/path/template). | Module manifest is read; YAML is self-describing |
| **Liquibase changelog** | Master changelog + included changesets | `id` + `author` + file path = unique key, `dbms`, `context`, `runOnChange`, `MD5SUM` checksum, `precondition`, `rollback` block. Tracking table `DATABASECHANGELOG` records executed checksums. | NO — changelog is the contract; apply walks it top-to-bottom |
| **Terraform plan file (`tfplan`)** | Binary blob (versioned protobuf); JSON via `terraform show -json` | Per resource_change: `address`, `mode` (managed/data), `type`, `name`, `provider_name`, `change.actions` (`["create"]`/`["update"]`/`["delete","create"]` etc.), `before`, `after`, `action_reason`. Plan is fully self-contained — apply consumes it without re-reading `.tf` files. | NO (this is the gold-standard self-describing artifact) |
| **Terraform state file** | JSON | Per resource: `type`, `name`, `provider`, `instances[].schema_version`, `instances[].attributes`, `instances[].sensitive_attributes`, `instances[].dependencies` (resource addresses) | NO |
| **Helm Chart.yaml** | YAML metadata header for the chart | `apiVersion` (v2), `name`, `version` (SemVer), `appVersion`, `type` (application/library), optional `dependencies[]`. Templates and values are separate; Chart.yaml is the manifest header. | Chart.yaml + values.yaml together; templates rendered from both |
| **Pulumi state (checkpoint)** | JSON snapshot | Per resource: `urn` (`urn:pulumi:<stack>::<project>::<type>::<name>`), `id` (provider-assigned), `provider` (versioned reference), `inputs`, `outputs`, `dependencies[]`. **List ordered by dependency** — engine relies on order. | NO |
| **DbUp** | Embedded SQL scripts + `SchemaVersions` journal table | Script name (filename) + execution timestamp. No checksums by default — relies on filename uniqueness + immutability. | NO (script files are the contract) |
| **FluentMigrator** | Compiled migration classes + `VersionInfo` table | `[Migration(versionNumber)]` attribute + class. Version + applied-at. | NO |
| **Octopus Deploy release snapshot** | Internal snapshot (deployment process + variables, not on-disk format) | Project steps, variable values (resolved at release-creation time), package version *references* (not packages). | Snapshot is the contract — release re-deploys are deterministic against the snapshot |

**Key takeaway:** Terraform's plan + Liquibase's changelog are the strongest analogs for what we want. Both encode a list of self-describing units of work, and both ship checksums (Liquibase) / before+after diffs (Terraform) so the apply path can detect drift without re-reading the authoring source. Unicorn — despite being our cultural reference — is *not* manifest-driven on apply; the predicate is re-evaluated each sync. We are pivoting away from Unicorn's model toward the Terraform/Liquibase model.

## Feature Landscape

### Table Stakes (Every Manifest Entry MUST Carry)

These are the fields without which the deserialize path cannot dispatch correctly. Missing any of these = either we re-consult config (the thing we're killing) or we silently misroute work.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| **`providerType`** (string) | Dispatch — without it the orchestrator cannot pick a provider. The whole pivot hinges on this. Equivalent: Terraform's `provider_name`, Pulumi's `urn` type segment, Liquibase's `dbms` (looser analog). | LOW | Already on `ProviderPredicateDefinition`; promote verbatim. |
| **Stable entry identity** (`name` + `path`) | Per-entry reporting, log correlation, dedup. Liquibase = `author:id:filepath`; Terraform = `address`; Pulumi = `urn`. Without it we cannot say "entry X failed". | LOW | Reuse `predicate.Name` as `name`; add the manifest-relative file/folder path the entry produced. |
| **Provider-specific target identifiers** | The provider needs to know *what* to write to. SqlTable: `table`, `nameColumn`. Content: `areaId`, `pageId`, `path`. Embed as a typed `target` sub-object keyed by provider (or a string-keyed `parameters` bag the provider parses). Liquibase parallel: `dbms`-scoped attributes per change type. | MEDIUM | Polymorphism question — see ARCHITECTURE.md. Recommend discriminated `target` payload keyed by `providerType`. |
| **Mode tag** (`Deploy` / `Seed`) | Phase 40 already pushed this onto each predicate; manifest must preserve it so per-entry conflict-strategy defaulting (D-06: Seed → DestinationWins) survives. Mode also implicitly says *which manifest file*. | LOW | Already on `ProviderPredicateDefinition.Mode`; copy in. |
| **Files this entry owns** (`files[]`, paths POSIX-relative to mode root) | Per-entry cleanup correctness (today's `ManifestCleaner` runs at the global level — when we go per-entry we need the per-entry file ownership to be explicit). Also lets us report "entry X: 3/4 files applied". Terraform plan parallel: `before`/`after` per resource. Unicorn parallel: each YAML file IS the entry, no aggregation needed; we have aggregation so we must record it. | LOW | Aggregate `WrittenFiles` per provider call at serialize time. |
| **Manifest schema version** (top-level) | Forward compat for the format itself. Liquibase, Terraform plan, Pulumi state, Helm Chart.yaml all carry this. Single-tenant tool but we will iterate the format; v0 to v1 migration without it is painful. | LOW | Top-level `schemaVersion: 1`. Trivial. |
| **Mode + WrittenAtUtc** (top-level, already present) | Audit / debug / "is this manifest stale". Already present in current `Manifest` record. Keep. | LOW | No change. |

### Differentiators (Worth Considering, Tier by Value)

Features other tools have that would meaningfully improve our system. Tiered by whether they earn their keep at v0.6.0 vs later — flagged where we'd be over-engineering for a single-tenant tool.

#### Tier A — Strong case for v0.6.0

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| **Per-entry `postProcessing` hook list** (e.g. `["clearCache:Dynamicweb.Foo.Bar", "schemaSync:EcomGroupFields"]`) | Today these are predicate fields (`ServiceCaches`, `SchemaSync`) that the orchestrator reads from config. Pivot says config is gone on the deserialize path — these MUST move into the entry, or the orchestrator silently stops invalidating caches. **Non-negotiable for parity, not really a "differentiator"** — promoting it here because it's what makes the manifest fully self-describing. Liquibase parallel: `runOnChange`, `runAlways`, `precondition`. | LOW | Plain string list; orchestrator's existing `CacheInvalidator` + `EcomGroupFieldSchemaSync` consume them. |
| **Per-entry `dependsOn[]`** (entry names this one needs run first) | Replaces today's hardcoded "FK ordering for SqlTable" + "Content-before-SqlTable when ResolveLinksInColumns" reorder logic in `SerializerOrchestrator.DeserializeAll`. Locks ordering into the artifact (Terraform/Pulumi do this); apply path becomes a topological sort, not a hardcoded preference list. **Big win:** removes an entire class of "why didn't FK ordering kick in?" debugging. | MEDIUM | At serialize time, providers know their dependencies (SqlTableProvider's FK resolver runs at serialize, not deserialize). Deserialize becomes pure topo-sort. |
| **Per-entry outcome record with sub-entry granularity** | See "Per-Entry Outcome Reporting Model" section below — this is a v0.6.0 deliverable per PROJECT.md. Today's `ProviderDeserializeResult` aggregates Created/Updated/Skipped/Failed per *predicate*; we want per-*entry* + ideally per-row drilldown for the Failed bucket so operators can find the actual broken row. | MEDIUM | Extends `ProviderDeserializeResult`, doesn't replace it. |

#### Tier B — Defer past v0.6.0, but explicitly track

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| **Per-entry checksum** (e.g. SHA256 over the entry's files) | Liquibase's killer feature. Apply detects "files were edited since serialize"; refuse or warn. PROJECT.md already lists "provenance/checksum" as a track-but-defer item. **Verdict: defer.** Single-tenant tool, files are git-tracked already (git provides the change-detection layer Liquibase needs in-band because Liquibase doesn't assume git). Re-evaluate when a multi-environment workflow brings non-git artifact transport. | MEDIUM | Cheap to add but earns less here than in Liquibase. |
| **Per-entry conflict-strategy override** | PROJECT.md lists this as track-but-defer. Useful when one table needs DestinationWins inside a Deploy run. Defer until a real use case shows up. | LOW | Field on the entry; orchestrator reads override-if-present-else-runtime-arg. |
| **Provider-type tiebreaker** | PROJECT.md track-but-defer. Useful when two providers could handle the same target (none today). | LOW | Defer until needed. |
| **Hand-edit fallback marker** | PROJECT.md track-but-defer. "This entry was edited by hand post-serialize, accept anyway". Pairs with checksum. Defer with checksum. | LOW | — |
| **Top-level `dependencies[]` for the manifest as a whole** (cross-manifest, e.g. deploy-manifest depends on seed-manifest) | Helm-style `dependencies` field. Useful when we eventually run mixed Deploy+Seed in one CLI invocation. Today's two-manifest model is parallel, not chained. Defer. | LOW | — |

#### Tier C — Nice but probably never

| Feature | Why Not Now | Notes |
|---------|-------------|-------|
| **Signed manifests** (e.g. cosign / minisign) | Open-source tool, single-tenant, files are in git, signing is solved one layer up. | Don't build. |
| **Manifest dry-run "diff" output** (preview which entries would create/update/delete, à la `terraform plan`) | Real value but big surface area. Defer to a separate v0.7.0 milestone if/when ops asks for it. The current `dryRun` flag covers ad-hoc inspection. | Track separately. |
| **Per-entry rollback block** (Liquibase `<rollback>`) | Source-wins is the current model and reverting means re-running serialize from the previous git commit. Liquibase needs in-band rollback because no git assumed. | Don't build. |
| **Encrypted/sensitive-field markers** (Terraform `sensitive_attributes`) | Possibly relevant for AccessUserPassword and similar. Currently we dump everything. Track separately as a security concern, not a manifest concern. | Don't build at the manifest layer. |

### Anti-Features (Belong in Runtime Args, NOT the Manifest)

These are inputs that change per-invocation. Baking them into the artifact would force a re-serialize for what should be a CLI flag flip.

| Anti-Feature | Why Tempting | Why Wrong | Where It Belongs |
|--------------|--------------|-----------|------------------|
| **`dryRun: true` in the manifest** | "Pin a manifest as preview-only" sounds reassuring | Serialize never knows whether the consumer wants dry-run; it's a property of the apply invocation, not the artifact. Liquibase has `--dry-run` as a runtime flag; Terraform plan files are *always* "the plan" and `apply -auto-approve` is the runtime decision. | Caller-supplied at deserialize time (already in PROJECT.md spec). |
| **`strictMode: true` in the manifest** | "Strict for prod, lenient for dev" feels like it belongs to the artifact | Strictness is an environment concern (prod vs dev), not an artifact concern. Same artifact gets applied lenient to dev and strict to prod. PROJECT.md correctly puts this in caller-supplied params. | Caller-supplied at deserialize time. |
| **`providerFilter`** | "Only run SqlTable entries this run" | Useful as a debugging flag but invocation-specific. Manifest is the full plan; filter narrows what we apply *from* the plan. | Caller-supplied. |
| **`conflictStrategy` (top-level run-wide)** | "Whole manifest defaults to SourceWins" | Already implied by `mode` (Deploy → SourceWins, Seed → DestinationWins per D-06). Per-entry override is Tier-B if we ever need it. | Caller-supplied (overrides mode default), with optional per-entry override deferred. |
| **Authoring metadata** (e.g. who serialized, what predicate config name was used) | Useful for forensics | Not needed at apply time; if useful, write it to a sidecar `provenance.json`, not the canonical manifest. Avoid coupling apply parsing to fields that exist only for humans. | Sidecar file or git history. |
| **Output formatting / log verbosity hints** | "This entry is noisy, log less" | Trivial example but representative — consumer concerns leak in fast. | Caller-supplied. |
| **Environment-specific config values** (e.g. target connection strings) | "Pin the target DB into the manifest" | The artifact is environment-portable by design. Targets are runtime context. (Helm gets this right: Chart.yaml is metadata, values.yaml is environment-overrideable.) | Runtime context (DW host's DB connection). |

### Per-Entry Outcome Reporting Model

The pivot's headline UX win: replace today's silent-skip-on-config-mismatch with explicit per-entry reporting. What shape do other tools emit? What should ours look like?

#### What others emit

- **Terraform `apply -json`** — streams a `change_summary` event up front (`add`, `change`, `remove`), then `apply_start`/`apply_progress`/`apply_complete`/`apply_errored` events keyed by resource `address`. Final summary is a count plus a list of errored addresses with their messages. Per-resource, not per-attribute.
- **Liquibase** — emits per-changeset `ran`/`skipped` (with skip reason: precondition / dbms mismatch / already-executed) / `failed` (with SQL error). Sub-changeset granularity is rare because changesets are typically atomic.
- **Pulumi** — per-resource step events: `same`, `create`, `update`, `replace`, `delete`, `read`, with diff summary. Errors carry the URN and the provider error message.
- **Helm** — per-release status (`deployed`, `failed`, `pending-upgrade`, `superseded`), aggregate not per-resource. Less informative than Terraform's per-resource model.
- **Sitecore Unicorn** — per-item logs in the sync UI: `created` / `updated` / `skipped (predicate excluded)` / `error`. Closest to ours.
- **Octopus** — per-step deploy results. Less applicable (different domain shape).

#### Recommended shape for `ProviderDeserializeResult` evolution

The current shape rolls up four counts (`Created`, `Updated`, `Skipped`, `Failed`) per *predicate*. Two problems for v0.6.0:

1. We're killing predicates. The unit of work is now an entry.
2. `Skipped` today is silent — operator doesn't know whether 5 rows were skipped because of conflict-strategy or because of a config mismatch. The pivot's whole point is making this visible.

Proposed shape (orthogonal to the existing aggregate counts — keep both for the summary line):

```csharp
public record EntryOutcome
{
    public required string EntryName { get; init; }   // matches manifest entry .name
    public required string ProviderType { get; init; }
    public required EntryStatus Status { get; init; } // Succeeded | Failed | Warned | Skipped
    public string? Reason { get; init; }              // free-form; required when Status != Succeeded
    public int Created { get; init; }
    public int Updated { get; init; }
    public int Skipped { get; init; }                 // sub-entry: rows skipped within a successful entry
    public int Failed { get; init; }                  // sub-entry: rows that failed within an otherwise-successful entry
    public IReadOnlyList<string> RowErrors { get; init; } = Array.Empty<string>(); // sub-entry: which rows failed and why
    public TimeSpan Duration { get; init; }
}

public enum EntryStatus { Succeeded, Failed, Warned, Skipped }
```

Decisions:

- **Entry-level status is mandatory and 4-valued** — `Succeeded` / `Failed` / `Warned` / `Skipped`. PROJECT.md specifies "succeeded/failed/warned" — `Skipped` is a separate concept (filter excluded the entry; provider never ran) and should not be conflated with `Succeeded` (the lie today). Liquibase precedent supports the four-bucket split.
- **Sub-entry granularity stays count-based + an error list, not a full per-row log** — for SqlTable predicates that touch 10k rows, a per-row `EntryStatus` would explode the result object. Match Terraform's "per-resource summary, errors listed for the failures" pattern. The full per-row trail belongs in the log stream, not the result record.
- **`Reason` is free-form on non-`Succeeded` statuses** — examples: `"providerType 'Foo' not registered"`, `"conflictStrategy=DestinationWins and target row exists"`, `"providerFilter excluded"`, `"3/47 rows failed FK validation"`. Operators read this; don't over-structure it.
- **`Duration` per entry** — cheap to capture, makes it easy to spot the pathologically slow entry. Pulumi/Terraform ship this; the third-party `tfjournal` tool exists *because* the native output omits it. Add it now.
- **Keep `OrchestratorResult.Errors` as the run-level error list**, but the Entry shape carries entry-level errors. Two-tier (run-level fatal vs entry-level non-fatal) is the right cut.

#### Reporting back to the caller

Final shape returned from `DeserializeAll`:

```csharp
OrchestratorResult {
    EntryOutcomes: List<EntryOutcome>,    // NEW — replaces DeserializeResults as primary
    DeserializeResults: List<...>,        // KEEP for back-compat / aggregate counts
    Errors: List<string>,                 // KEEP — run-level errors
    StaleFilesDeleted: int,               // KEEP
    Summary: string                       // ENHANCED — includes "X succeeded, Y warned, Z failed, W skipped"
}
```

## Feature Dependencies

```
Manifest schema upgrade (Entry shape with providerType + targets + postProcessing)
    ├──requires──> Provider builds Entry at serialize time (BuildManifestEntry)
    │                  └──requires──> ProviderPredicateDefinition fields fully captured in Entry
    │
    ├──enables──> Deserialize reads manifest only (drop config consultation)
    │                  ├──requires──> EntryOutcome reporting (per-entry status not predicate-aggregated)
    │                  └──requires──> Caller-supplied runtime params (mode/conflictStrategy/strictMode/dryRun/providerFilter)
    │
    ├──enables──> Per-entry dependsOn[] (Tier A differentiator)
    │                  └──replaces──> Hardcoded FK + Content-before-SqlTable ordering in DeserializeAll
    │
    └──unblocks──> Deferred items (Tier B)
                    ├── per-entry checksum
                    ├── per-entry conflict-strategy override
                    └── per-entry hand-edit fallback
```

### Dependency Notes

- **Entry shape requires provider's `BuildManifestEntry`:** the provider knows its target shape; the orchestrator can't synthesize the entry generically without re-introducing config knowledge it just lost. Each provider must own emitting its entries.
- **Deserialize-reads-manifest-only requires EntryOutcome:** if we drop predicates from the deserialize signature, we lose the natural unit for aggregation. EntryOutcome IS the new aggregation unit.
- **`dependsOn[]` replaces hardcoded ordering:** the orchestrator currently has two bespoke reorder passes (FK; Content-before-SqlTable for link resolution). Both are computed from predicate fields at deserialize time. Push the computation to serialize time, encode as `dependsOn[]`, run topological sort at deserialize. Cleaner.
- **Checksum requires Entry stability:** can't checksum until the Entry shape is locked. Defer to v0.6.x or v0.7.0.

## MVP Definition

### Launch With (v0.6.0)

The minimum to deliver the pivot per PROJECT.md.

- [ ] **Manifest schema v1** — top-level: `schemaVersion`, `mode`, `writtenAtUtc`, `entries[]`. Per-entry: `name`, `providerType`, `mode`, `target` (provider-discriminated), `files[]`, `postProcessing[]`, `dependsOn[]`. — *The artifact contract.*
- [ ] **`IProvider.BuildManifestEntry(predicate, serializeResult)`** — each provider builds its own entry; orchestrator collects them. — *The serialize-side change.*
- [ ] **`SerializerOrchestrator.DeserializeAll(manifestPath, mode, conflictStrategy, strictMode, dryRun, providerFilter, log)`** — new signature, no predicates parameter, no config consultation. Reads manifest, topo-sorts by `dependsOn`, dispatches per entry. — *The deserialize-side change.*
- [ ] **`EntryOutcome` record + `OrchestratorResult.EntryOutcomes`** — per-entry status (Succeeded/Failed/Warned/Skipped), reason, sub-entry counts, duration. — *The reporting change.*
- [ ] **Drop `provider.ValidatePredicate` from deserialize path** — validation moves to serialize time (catch misconfig before it gets baked into a manifest). — *Removes the silent-skip mechanism.*
- [ ] **Drop `config.Predicates` lookup + `ConfigLoader.Load` from deserialize path** — the whole point of the pivot.

### Add After Validation (v0.6.x)

- [ ] **Per-entry checksum** — once the Entry shape is stable in production for ~1 milestone. SHA256 of the entry's files. Apply path warns on mismatch (refuses on strict mode).
- [ ] **Per-entry conflict-strategy override** — first real use case opens this; until then runtime arg is sufficient.
- [ ] **Hand-edit fallback marker** — pairs with checksum.

### Future Consideration (v0.7.0+)

- [ ] **Manifest dry-run diff output** — Terraform-style preview of what would change. Real engineering, not a checkbox.
- [ ] **Cross-manifest `dependencies[]`** — when chained Deploy → Seed runs become a thing.
- [ ] **Sensitive-field markers** — security review, not just a manifest field.

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|---------------------|----------|
| Manifest schema v1 (Entry shape) | HIGH | MEDIUM | P1 |
| `BuildManifestEntry` per provider | HIGH | MEDIUM | P1 |
| Deserialize reads manifest only | HIGH (it IS the milestone) | MEDIUM | P1 |
| `EntryOutcome` reporting | HIGH (operator UX) | LOW | P1 |
| Drop `ValidatePredicate` from deserialize | MEDIUM (removes silent-skip) | LOW | P1 |
| Per-entry `dependsOn[]` + topo sort | MEDIUM (cleaner orchestrator) | MEDIUM | P1 *if budget allows*, else P2 |
| Per-entry `postProcessing[]` (caches, schemaSync) | HIGH (parity, not optional really) | LOW | P1 |
| Per-entry checksum | MEDIUM | LOW | P2 |
| Per-entry conflict-strategy override | LOW | LOW | P2 |
| Hand-edit fallback marker | LOW | LOW | P2 |
| Manifest signing | LOW | MEDIUM | P3 |
| Manifest dry-run diff | MEDIUM | HIGH | P3 |
| Per-entry rollback block | LOW (source-wins via git) | HIGH | P3 |

**Priority key:**
- P1: Required for v0.6.0 milestone close
- P2: Track but defer to v0.6.x or v0.7.0
- P3: Future consideration, unlikely to ship

## Competitor Feature Analysis

| Feature | Sitecore Unicorn | Liquibase | Terraform | Our Approach (v0.6.0) |
|---------|------------------|-----------|-----------|-----------------------|
| Self-describing artifact | NO (predicate config still consulted on sync) | YES (changelog walked top-down) | YES (plan + state are canonical) | YES — manifest is the contract |
| Per-entry dispatch metadata | Item GUID + template + path (item-level) | `id` + `author` + `dbms` + `precondition` | `address` + `provider` + `change.actions` + `before`/`after` | `name` + `providerType` + `target` + `mode` |
| Dependency ordering in artifact | NO (modules, not items) | Implicit (file order) | YES (state list is dependency-ordered; plan derives from it) | YES — `dependsOn[]` per entry, topo-sort at apply |
| Checksum / drift detection | NO (re-compares against current Sitecore item) | YES (MD5SUM in DATABASECHANGELOG) | YES (state-vs-config diff) | DEFER — git provides drift detection |
| Per-entry outcome on apply | Per-item: created/updated/skipped/error | Per-changeset: ran/skipped(reason)/failed | Per-resource: same/create/update/replace/delete + errors | Per-entry: Succeeded/Failed/Warned/Skipped + reason + sub-entry counts |
| Sub-entry (per-row) granularity | YES (each item is its own file) | RARE (changesets atomic) | NO (per-resource is the grain) | YES — counts + error list inside each EntryOutcome |
| Schema versioning of artifact format | NO | YES (changelog `xsd` versioned) | YES (`format_version` in plan/state JSON) | YES — top-level `schemaVersion` |
| Runtime args separated from artifact | Partial (predicate is config-side) | YES (`--dry-run` etc. are flags) | YES (apply flags separate from plan) | YES — runtime params caller-supplied |

## Single-Tenant Reality Check

**Where we'd be over-engineering** (called out so the roadmapper doesn't grab them):

1. **Signed manifests** — public OSS, single-tenant deployments, no adversarial input model. Don't.
2. **Per-entry rollback blocks** — git is the rollback mechanism for source-wins. Liquibase needs in-band rollback because it doesn't assume source control. We do. Don't.
3. **Tenant-scoped variables** (Octopus pattern) — single tenant. Don't.
4. **Multi-format manifest** (XML + YAML + JSON like Liquibase) — JSON is fine, no need for ceremony.
5. **Runtime variable substitution in manifest** (Helm-style `{{ .Values.foo }}`) — manifest is a dumped artifact, not a template. Substitution belongs at the YAML payload layer (where `Default.aspx?ID=` link rewriting already happens), not the manifest layer.
6. **Pluggable manifest backends** (file / S3 / etcd, like Pulumi/Terraform state backends) — file is fine, this is a git-tracked artifact.

**Where the single-tenant rationale does NOT excuse skipping:**

- Schema versioning — *cheap*, and the cost of needing it later without it is high.
- Per-entry duration — *cheap*, big debug payoff.
- 4-valued status (vs 3-valued succeeded/failed/warned) — `Skipped` exists as a distinct case (provider filter, dependsOn upstream failed) and conflating it with anything else recreates the silent-skip problem we are pivoting to fix.

## Sources

- [GitHub - SitecoreUnicorn/Unicorn](https://github.com/SitecoreUnicorn/Unicorn) — predicate / target data store / dependencies model
- [GitHub - SitecoreUnicorn/Rainbow](https://github.com/SitecoreUnicorn/Rainbow) — YAML serialization format
- [Setting sequence of modules for Unicorn Sync in Sitecore](https://tothecore.sk/2018/05/14/setting-sequence-of-modules-for-unicorn-sync-in-sitecore/) — module-level `dependencies` attribute
- [Liquibase: What is a Changeset?](https://docs.liquibase.com/concepts/changelogs/changeset.html) — `id` + `author` + filepath as unique key
- [Liquibase: Changelog attributes - dbms](https://docs.liquibase.com/secure/reference-guide-5-1/changelog-attributes/dbms) — per-changeset DBMS targeting
- [Liquibase: What is a Changeset checksum?](https://docs.liquibase.com/secure/user-guide-5-1/what-is-a-changeset-checksum) — MD5SUM in DATABASECHANGELOG, drift detection
- [Terraform JSON output format](https://developer.hashicorp.com/terraform/internals/json-format) — plan resource_changes, state resource attributes/dependencies/sensitive_attributes
- [Terraform plan command reference](https://developer.hashicorp.com/terraform/cli/commands/plan) — binary plan format, `-out` flag
- [Helm Charts documentation](https://helm.sh/docs/topics/charts/) — Chart.yaml required fields (apiVersion, name, version, type, appVersion)
- [Pulumi Resource Names and Identity](https://www.pulumi.com/docs/iac/concepts/resources/names/) — URN format `urn:pulumi:<stack>::<project>::<type>::<name>`
- [Pulumi State and Backends](https://www.pulumi.com/docs/concepts/state/) — checkpoint format, dependency ordering
- [Pulumi Editing State Files](https://www.pulumi.com/docs/support/troubleshooting/editing-state-files/) — resources list ordering enforced by engine
- [DbUp Journaling](https://dbup.readthedocs.io/en/latest/more-info/journaling/) — SchemaVersions table, no checksum
- [FluentMigrator Quick Start](https://fluentmigrator.github.io/intro/quick-start.html) — VersionInfo table
- [Octopus: Releases, Deployments and Variable Snapshots](https://octopus.com/blog/releases-and-snapshots) — snapshot model, references vs values
- [tfjournal — Terraform per-resource event capture](https://github.com/Owloops/tfjournal) — third-party tool that exists *because* native per-resource timing is missing

---
*Feature research for: manifest-driven deserialize artifact format and per-entry outcome reporting*
*Researched: 2026-05-08*
