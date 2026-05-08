# Architecture Research — Manifest-Driven Deserialize (v0.6.0)

**Domain:** Manifest-driven dispatch over a pluggable provider architecture (DynamicWeb.Serializer)
**Researched:** 2026-05-08
**Confidence:** HIGH — grounded in current code, no upstream library questions involved.

---

## Reality Check Up Front

The milestone context lists three providers — `ContentProvider`, `SqlTableProvider`, `EmbeddedXmlProvider` — but **only the first two exist as code today** (verified by `Glob` over `Providers/**/*.cs` and `Grep` for the type name). "Embedded XML" today is a *feature inside `SqlTableProvider`* (XML data-type columns merged via `XmlMergeHelper`); there is no separate provider class.

The roadmap therefore has a choice the milestone brief did not surface explicitly:
- **(α)** Treat embedded XML as it is today — a per-column branch in `SqlTableProvider.Deserialize`. The manifest entry hierarchy then has only `ContentEntry` and `SqlTableEntry`. Embedded XML lives inside `SqlTableEntry` (as today: `xmlColumns: []` plus the merge happens at deserialize against the live target). No third entry type.
- **(β)** Carve out an `EmbeddedXmlProvider` as part of v0.6.0, which means a real new provider, a real new entry type, and a real refactor of `SqlTableProvider.Deserialize`'s xml-column branch.

**Recommendation: option (α) for v0.6.0.** The pivot is already large (manifest schema, entry hierarchy, command surface, FK reorder relocation, per-entry reporting). Carving out a third provider concurrently is unrelated scope creep with no caller benefit — the merge logic still has to run during the SqlTable row loop because xml columns sit inside SQL rows. The roadmapper should either confirm (α) or split (β) into a follow-up milestone (v0.7.0). The rest of this document is written assuming (α) — call out (β) only as a follow-up note where it would change a decision.

---

## Existing Architecture (one-screen recap)

```
┌─────────────────────────── Caller surface ──────────────────────────────┐
│  SerializerSerializeCommand   SerializerDeserializeCommand   ZipImport  │
│         │                              │                          │     │
│         ▼                              ▼                          ▼     │
│   ConfigLoader.Load   ──►   config.Predicates (mode-filtered list)      │
│                              + flags (mode, strategy, strict, dryRun)   │
└─────────────────────────────────┬───────────────────────────────────────┘
                                  │
                                  ▼
┌────────────────── SerializerOrchestrator ─────────────────────────────┐
│  SerializeAll(predicates, root, mode, strategy, ...)                  │
│  DeserializeAll(predicates, root, mode, strategy, dryRun, escalator)  │
│   ├─ FK reorder (SqlTable parents-before-children)                    │
│   ├─ Content-before-SqlTable when ResolveLinksInColumns non-empty     │
│   ├─ for each predicate: ValidatePredicate → provider.Deserialize     │
│   ├─ aggregate page-id map (Content runs feed SqlTable runs)          │
│   └─ post-loop: ManifestWriter (serialize), StrictModeEscalator gate  │
└──────────────────────────────────┬────────────────────────────────────┘
                                   │
              ┌────────────────────┼────────────────────┐
              ▼                    ▼                    ▼
       ContentProvider     SqlTableProvider     (no other today)
       returns ProviderDeserializeResult { Created, Updated, Skipped, Failed, Errors[],
                                           SourceToTargetPageMap? }
```

`ManifestWriter` today writes a flat `{mode}-manifest.json` with `{ mode, writtenAtUtc, files[] }`. `ManifestCleaner` reads it post-run to delete stale files. **Nothing reads the manifest at deserialize time** — that's the whole pivot.

---

## 1. Entry Hierarchy

**Recommendation: (a) — sealed records inheriting an abstract `ManifestEntry` with `[JsonPolymorphic]`.**

```csharp
[JsonPolymorphic(TypeDiscriminatorPropertyName = "providerType")]
[JsonDerivedType(typeof(ContentEntry),     typeDiscriminator: "Content")]
[JsonDerivedType(typeof(SqlTableEntry),    typeDiscriminator: "SqlTable")]
public abstract record ManifestEntry
{
    /// <summary>Stable id within the manifest. Used for per-entry outcome reporting and logs.</summary>
    public required string EntryId { get; init; }       // e.g. "content/area-1/customer-center" or "sql/EcomOrderFlow"
    public required string ProviderType { get; init; }
    public required IReadOnlyList<string> Files { get; init; }   // POSIX-relative paths under modeRoot
}

public sealed record ContentEntry : ManifestEntry
{
    public required int    AreaId { get; init; }
    public required string AreaName { get; init; }       // for logs / disambiguation
    public required string Path { get; init; }
    public required int    PageId { get; init; }
    public IReadOnlyList<int> AcknowledgedOrphanPageIds { get; init; } = Array.Empty<int>();
    // Phase 37-05 LINK-02: ContentEntry runs before SqlTableEntry whose ResolveLinksInColumns≠[]
    // → no flag here; flag lives on SqlTableEntry.
}

public sealed record SqlTableEntry : ManifestEntry
{
    public required string Table { get; init; }
    public string? NameColumn { get; init; }             // identity column for lookups (today: predicate.NameColumn)
    public string? CompareColumns { get; init; }
    public IReadOnlyList<string> XmlColumns { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ResolveLinksInColumns { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ServiceCaches { get; init; } = Array.Empty<string>();
    public string? SchemaSync { get; init; }             // "EcomGroupFields" or null
    public ConflictStrategy? ConflictStrategyOverride { get; init; } = null; // tracked-but-deferred per milestone brief
}
```

### Tradeoffs (explicit)

| Option | Pros | Cons |
|--------|------|------|
| **(a) sealed records + `[JsonPolymorphic]`** | Compile-time exhaustiveness (`switch` warns when new entry added). Type-safe access to per-provider fields without casts at the dispatch site. Clean `System.Text.Json` integration — STJ handles discriminator + roundtrip natively in .NET 8. Diff-friendly JSON (each entry's keys are exactly its provider's fields, nothing extra). | Adds ~3 record types vs. one. Adding a new provider requires a new derived record + a new `[JsonDerivedType]` attribute (a 2-line change — flag this as the only friction point). |
| **(b) one fat record, all-optional** | Single class, easiest to add a field. | Anti-pattern: every consumer has to remember "this field is meaningful only for SqlTable". Refactors break silently. JSON output is noisy (every entry has every field, half of them null). Strong correlation with the kind of bugs we just shipped strict-mode to catch. |
| **(c) discriminator + `Dictionary<string,object> ProviderData`** | Zero new types; manifest is open-ended. | Loses type safety entirely. Every dispatch site does `(string)data["areaId"]` shenanigans with no compile-time check. JSON shape becomes a contract that lives only in code, not in the type system. Worst of both worlds for a project with ≤4 providers planned. |

(a) wins because the value the entry hierarchy gives us — *the dispatcher can `switch (entry)` and the compiler enforces a case per provider* — only exists in (a). (c) explicitly throws this away; (b) makes it a runtime check.

`System.Text.Json` polymorphism in .NET 8 is the supported path — no need for a custom `JsonConverter`. Verified by the existing `ManifestWriter.cs` which already uses `JsonSerializerOptions` with default STJ settings.

### Manifest envelope

```csharp
public sealed record Manifest
{
    public required int SchemaVersion { get; init; }     // bump = hard-reject, no backcompat
    public required string Mode { get; init; }           // "deploy" | "seed"
    public required DateTime WrittenAtUtc { get; init; }
    public required IReadOnlyList<ManifestEntry> Entries { get; init; }
}
```

Schema version starts at `2` (the existing flat-files manifest was implicitly v1; bump on shape change so a v0.5 manifest read by v0.6 code fails immediately with a clear error per milestone brief "no backcompat — old manifests fail at read with clear error").

---

## 2. `BuildManifestEntry` Contract

**Recommendation: extend `SerializeResult` with an `Entry` property; the orchestrator collects them as a side product of the existing `Serialize(...)` call. No second pass.**

### Method shape

```csharp
// SerializeResult.cs — extend, not replace
public record SerializeResult
{
    public int RowsSerialized { get; init; }
    public string TableName { get; init; } = "";
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> WrittenFiles { get; init; } = Array.Empty<string>();

    /// <summary>Manifest entry contributed by this serialize call. Null on validation failure / exception.</summary>
    public ManifestEntry? Entry { get; init; }     // NEW
}
```

`SerializationProviderBase` / `ISerializationProvider` already have everything needed at serialize time (the `predicate` is passed in, the `WrittenFiles` is built up during the run). Adding `BuildManifestEntry` as an abstract method on the base would force *every* provider to construct one, which is fine — but doing it as a `protected` helper called from the existing `Serialize` body keeps the public contract small.

```csharp
public abstract class SerializationProviderBase : ISerializationProvider
{
    // Existing: Serialize / Deserialize / ValidatePredicate

    /// <summary>
    /// Build the manifest entry for this provider. Called from each provider's Serialize
    /// implementation just before returning. Always supplied with the predicate that drove
    /// the run and the absolute paths of every file it emitted.
    /// </summary>
    protected abstract ManifestEntry BuildManifestEntry(
        ProviderPredicateDefinition predicate,
        string modeRoot,
        IReadOnlyList<string> writtenFiles);
}
```

`modeRoot` is passed so the provider can emit *relative* (POSIX) paths in `Entry.Files` — the manifest must be portable across Windows/Linux build hosts (existing `ManifestWriter` already does this trick on lines 41–43, replicate the contract).

### Why not a second pass

A second pass would need to re-derive everything `Serialize` already computed — table name, predicate fields, written-file list. Two reads, two dispatch paths, two failure modes. The first-pass option threads a single piece of data (`Entry`) through the existing return shape and costs nothing. The orchestrator already collects `WrittenFiles` from `SerializeResult` (lines 117–121 of `SerializerOrchestrator.cs`); adding `Entry` next to it is mechanical.

### Per-provider `BuildManifestEntry` bodies (sketch)

```csharp
// ContentProvider.cs
protected override ManifestEntry BuildManifestEntry(
    ProviderPredicateDefinition p, string modeRoot, IReadOnlyList<string> written)
    => new ContentEntry
    {
        EntryId  = $"content/area-{p.AreaId}{(p.Path == "/" ? "" : p.Path)}",
        ProviderType = "Content",
        Files    = written.Select(f => Path.GetRelativePath(modeRoot, f).Replace('\\','/'))
                          .OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList(),
        AreaId   = p.AreaId,
        AreaName = ResolveAreaName(p.AreaId),  // existing helper, today implicit in logs
        Path     = p.Path,
        PageId   = p.PageId,
        AcknowledgedOrphanPageIds = p.AcknowledgedOrphanPageIds.ToList()
    };

// SqlTableProvider.cs
protected override ManifestEntry BuildManifestEntry(
    ProviderPredicateDefinition p, string modeRoot, IReadOnlyList<string> written)
    => new SqlTableEntry
    {
        EntryId  = $"sql/{p.Table}",
        ProviderType = "SqlTable",
        Files    = written.Select(...).OrderBy(...).ToList(),
        Table    = p.Table!,
        NameColumn = p.NameColumn,
        CompareColumns = p.CompareColumns,
        XmlColumns = p.XmlColumns.ToList(),
        ResolveLinksInColumns = p.ResolveLinksInColumns.ToList(),
        ServiceCaches = p.ServiceCaches.ToList(),
        SchemaSync = p.SchemaSync
    };
```

`EntryId` design note: deterministic + stable — same predicate produces same id across runs. Used for log prefixes ("`[content/area-1/customer-center] WARNING: ...`") and per-entry outcome correlation. Falls naturally out of the existing on-disk layout (`_content/area-N/...` vs `_sql/{Table}/`).

---

## 3. Reorder Logic Relocation

**Today** (`SerializerOrchestrator.DeserializeAll` lines 162–218):
- FK reorder operates on `predicates.Where(p => p.ProviderType == "SqlTable")`
- Content-before-SqlTable rule operates on `predicates.Any(p => ResolveLinksInColumns.Count > 0)`

**After the pivot:** identical structure, applied to entries instead of predicates. **`FkDependencyResolver` does not change** — it queries `INFORMATION_SCHEMA` from a list of table names; whether those names came from a predicate's `Table` field or an entry's `Table` field is irrelevant to it. (Verified: `FkDependencyResolver.cs` takes `IEnumerable<string> tableNames` as input — confirmed lines 22–30.)

```csharp
// SerializerOrchestrator.DeserializeAll — new shape
public OrchestratorResult DeserializeAll(
    string modeRoot,
    DeploymentMode mode,
    ConflictStrategy strategy,
    Action<string>? log = null,
    bool dryRun = false,
    string? providerFilter = null,
    StrictModeEscalator? escalator = null,
    IReadOnlyDictionary<string, List<string>>? excludeFieldsByItemType = null,
    IReadOnlyDictionary<string, List<string>>? excludeXmlElementsByType = null)
{
    escalator ??= StrictModeEscalator.Null;
    var wrappedLog = WrapLogWithEscalator(log, escalator);
    wrappedLog($"=== Mode: {mode} | Strategy: {strategy} | Strict: {escalator.IsStrict} ===");

    // Read manifest. No fallback — absent or wrong-version manifest fails fast.
    var manifest = new ManifestWriter().Read(modeRoot, mode.ToString().ToLowerInvariant())
        ?? throw new InvalidOperationException(
            $"No manifest found at {modeRoot}/{mode.ToString().ToLowerInvariant()}-manifest.json. " +
            "Run serialize first.");

    if (manifest.SchemaVersion != Manifest.CurrentSchemaVersion)
        throw new InvalidOperationException(
            $"Manifest schema version {manifest.SchemaVersion} not supported. " +
            $"Expected {Manifest.CurrentSchemaVersion}. Re-run serialize.");

    var entries = manifest.Entries.AsEnumerable();

    if (providerFilter != null)
        entries = entries.Where(e => string.Equals(e.ProviderType, providerFilter, StringComparison.OrdinalIgnoreCase));

    var entryList = entries.ToList();

    // FK reorder over SqlTableEntry.Table (unchanged FkDependencyResolver).
    if (_fkResolver != null)
    {
        var sqlEntries = entryList.OfType<SqlTableEntry>().ToList();
        if (sqlEntries.Count > 1)
        {
            var ordered = _fkResolver.GetDeserializationOrder(sqlEntries.Select(e => e.Table));
            var idx = ordered.Select((t, i) => (t, i)).ToDictionary(x => x.t, x => x.i, StringComparer.OrdinalIgnoreCase);
            var nonSql = entryList.Where(e => e is not SqlTableEntry).ToList();
            var sortedSql = sqlEntries.OrderBy(e => idx.TryGetValue(e.Table, out var i) ? i : int.MaxValue).ToList();
            entryList = sortedSql.Concat(nonSql).ToList();
            wrappedLog($"FK ordering: {string.Join(" -> ", ordered)}");
        }
    }

    // Content-before-SqlTable rule. Trigger condition reads the entry property directly.
    if (entryList.OfType<SqlTableEntry>().Any(e => e.ResolveLinksInColumns.Count > 0))
    {
        var content = entryList.OfType<ContentEntry>().Cast<ManifestEntry>().ToList();
        var rest    = entryList.Where(e => e is not ContentEntry).ToList();
        if (content.Count > 0)
        {
            entryList = content.Concat(rest).ToList();
            wrappedLog($"LINK-02 ordering: running {content.Count} Content entry(ies) first.");
        }
    }

    // Dispatch.
    var aggregatedPageMap = new Dictionary<int, int>();
    var entryOutcomes = new List<EntryOutcome>();   // see Section 4

    foreach (var entry in entryList)
    {
        if (!_registry.HasProvider(entry.ProviderType))
        {
            entryOutcomes.Add(EntryOutcome.Failed(entry, $"No provider registered for type '{entry.ProviderType}'"));
            wrappedLog($"WARNING: [{entry.EntryId}] no provider for type '{entry.ProviderType}'");
            continue;
        }

        var provider = _registry.GetProvider(entry.ProviderType);
        // ValidatePredicate goes away — entries are already shape-valid by construction.

        var resolver = entry is SqlTableEntry sql && sql.ResolveLinksInColumns.Count > 0 && aggregatedPageMap.Count > 0
            ? new InternalLinkResolver(aggregatedPageMap, wrappedLog)
            : null;

        var providerResult = provider.Deserialize(
            entry, modeRoot, wrappedLog, dryRun, strategy, resolver,
            excludeFieldsByItemType, excludeXmlElementsByType);

        entryOutcomes.Add(EntryOutcome.From(entry, providerResult));

        if (providerResult.SourceToTargetPageMap is { } map)
            foreach (var kv in map) aggregatedPageMap.TryAdd(kv.Key, kv.Value);

        // Cache invalidation + schema sync logic relocates from predicate.ServiceCaches
        // to ((SqlTableEntry)entry).ServiceCaches. Same logic, different field source.
        if (entry is SqlTableEntry sqlEntry && !dryRun && !providerResult.HasErrors)
        {
            if (sqlEntry.ServiceCaches.Count > 0)
                _cacheInvalidator?.InvalidateCaches(sqlEntry.ServiceCaches, wrappedLog);
            if (string.Equals(sqlEntry.SchemaSync, "EcomGroupFields", StringComparison.OrdinalIgnoreCase))
                _ecomSchemaSync?.SyncSchema(wrappedLog);
        }
    }

    try { escalator.AssertNoWarnings(); }
    catch (CumulativeStrictModeException ex)
    {
        entryOutcomes.Add(EntryOutcome.RunLevelError(ex.Message));
        wrappedLog($"ERROR: {ex.Message}");
    }

    return new OrchestratorResult { EntryOutcomes = entryOutcomes };
}
```

### Provider signature change

`ISerializationProvider.Deserialize` parameters change: `predicate` → `entry`. The provider switches on its concrete entry subtype (or asserts and casts):

```csharp
public override ProviderDeserializeResult Deserialize(
    ManifestEntry entry, string modeRoot, ...)
{
    if (entry is not SqlTableEntry sql)
        throw new ArgumentException($"SqlTableProvider received non-SqlTable entry: {entry.GetType().Name}");
    // ... existing body, replacing every `predicate.X` with `sql.X`
}
```

`ValidatePredicate` is gone — entries arrive shape-valid (the manifest writer constructed them; an old/wrong-shape manifest fails the schema-version check at read time). The single remaining "validation" is the registry lookup (`_registry.HasProvider`) which returns `EntryOutcome.Failed` instead of skipping silently.

---

## 4. Per-Entry Outcome Reporting

**Recommendation: replace `OrchestratorResult.DeserializeResults` with `EntryOutcomes`. Keep `ProviderDeserializeResult` as a per-provider DTO but drop it from the public `OrchestratorResult` surface.**

```csharp
public sealed record EntryOutcome
{
    public required string EntryId { get; init; }
    public required string ProviderType { get; init; }
    public required EntryStatus Status { get; init; }       // Succeeded | Warned | Failed | Skipped (dry-run / filter)
    public required string Message { get; init; }           // human-readable summary
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();   // captured during the entry's run
    public ProviderCounts Counts { get; init; } = ProviderCounts.Zero;

    public static EntryOutcome From(ManifestEntry entry, ProviderDeserializeResult r) =>
        new() {
            EntryId = entry.EntryId,
            ProviderType = entry.ProviderType,
            Status = r.HasErrors ? EntryStatus.Failed
                   : r.Failed > 0 ? EntryStatus.Warned
                   : EntryStatus.Succeeded,
            Message = r.Summary,
            Errors  = r.Errors.ToList(),
            Counts  = new ProviderCounts(r.Created, r.Updated, r.Skipped, r.Failed)
        };

    public static EntryOutcome Failed(ManifestEntry entry, string error) => /* ... */;
    public static EntryOutcome RunLevelError(string error) => /* ... */;
}

public enum EntryStatus { Succeeded, Warned, Failed, Skipped }

public readonly record struct ProviderCounts(int Created, int Updated, int Skipped, int Failed)
{
    public static ProviderCounts Zero => default;
}
```

```csharp
public record OrchestratorResult
{
    // Deserialize path
    public IReadOnlyList<EntryOutcome> EntryOutcomes { get; init; } = Array.Empty<EntryOutcome>();

    // Serialize path — keep as is (manifest writer already aggregates)
    public IReadOnlyList<SerializeResult> SerializeResults { get; init; } = Array.Empty<SerializeResult>();
    public int StaleFilesDeleted { get; init; }

    public IReadOnlyList<string> Errors => EntryOutcomes
        .Where(o => o.Status == EntryStatus.Failed)
        .SelectMany(o => o.Errors.DefaultIfEmpty(o.Message))
        .Concat(SerializeResults.SelectMany(r => r.Errors))
        .ToList();

    public bool HasErrors => Errors.Count > 0;

    // Summary continues to render the same human string; rebuild from EntryOutcomes.
    public string Summary => /* aggregate counts across EntryOutcomes */;
}
```

### Why not "extend `ProviderDeserializeResult` with an entry reference"

Because `ProviderDeserializeResult` is per-table — `TableName` is its identity field today, and `Created/Updated/Skipped/Failed` are *row* counts, not *entry* counts. An entry maps 1:1 to a `ProviderDeserializeResult` today (one predicate → one provider call → one result), so attaching an entry ref is technically possible, but then `OrchestratorResult.DeserializeResults` is already the per-entry list — just thinly disguised. The point of `EntryOutcome` is to introduce a *status field* (`Succeeded | Warned | Failed | Skipped`) that the current `ProviderDeserializeResult` lacks — its only signal is `HasErrors`, which collapses Warned and Failed.

Per the milestone brief, "per-item succeeded/failed/warned reporting replaces the current silent-skip-on-config-mismatch model". The status enum is the meat of that change. `EntryOutcome` makes it the public surface; `ProviderDeserializeResult` stays as the provider's internal contract (Created/Updated/Skipped/Failed counts feeding into the outcome).

**Side effect cleanup:** the legacy `OrchestratorResult.DeserializeResults` field can be removed in the same change. Callers that read it (`SerializerDeserializeCommand` lines 152–166) need rewiring to `EntryOutcomes`.

### Per-entry warnings

The `StrictModeEscalator` records all warnings into a flat list today. To populate `EntryOutcome.Warnings`, the escalator wrapper needs to know *which entry is currently running* — a single `currentEntryId` ambient field on the orchestrator that the wrap-log closure captures. This is a one-line capture; no architectural change. The roadmapper should call this out as a sub-task in Phase 2 (per-entry reporting).

---

## 5. Cleanup Map

### Files that need an edit

| File | Change | Risk |
|------|--------|------|
| `Infrastructure/ManifestWriter.cs` | Replace flat `Manifest { mode, writtenAtUtc, files[] }` with `{ schemaVersion, mode, writtenAtUtc, entries[] }`. New `Write(string modeRoot, string mode, IReadOnlyList<ManifestEntry> entries)` signature. Read returns `Manifest?` (already does). | Low — single class, JSON shape change. |
| `Infrastructure/ManifestCleaner.cs` | Adapt to new manifest shape — flatten `entries[].files[]` to compute the written-set. Logic unchanged after the flatten. | Low. |
| `Providers/SerializeResult.cs` | Add `Entry` property (nullable `ManifestEntry`). | Low — additive. |
| `Providers/SerializationProviderBase.cs` | Add `protected abstract ManifestEntry BuildManifestEntry(...)`. | Low. |
| `Providers/ISerializationProvider.cs` | `Deserialize(...)` first param `ProviderPredicateDefinition` → `ManifestEntry`. Drop `ValidatePredicate`. | High — every implementation + every test mock breaks. Mechanical sweep. |
| `Providers/Content/ContentProvider.cs` | Implement `BuildManifestEntry` → `ContentEntry`. Rewrite `Deserialize` to take `ContentEntry` (downcast at top). Drop `ValidatePredicate`. | Medium — pattern-match on `ContentEntry`, replace every `predicate.AreaId/Path/PageId/AcknowledgedOrphanPageIds` access. |
| `Providers/SqlTable/SqlTableProvider.cs` | Same as ContentProvider but for `SqlTableEntry`. ~530 LOC file with ~15 `predicate.X` accesses across both Serialize and Deserialize. | Medium. |
| `Providers/SerializerOrchestrator.cs` | This is the heart of the change. New `DeserializeAll` signature (predicates parameter goes away, `modeRoot` becomes the entry point, manifest read happens first). Rewrite the dispatch loop to operate on entries. Reorder logic moves from operating on `predicates` to operating on `entries`. Cache invalidation reads `((SqlTableEntry)entry).ServiceCaches`. Remove the two `[Obsolete]` legacy overloads at lines 39–54 — nobody should be calling them after this milestone. | High — 350 LOC file mostly rewritten. |
| `Providers/SerializerOrchestrator.cs` (`OrchestratorResult`) | Replace `DeserializeResults` with `EntryOutcomes`. Rebuild `HasErrors` / `Summary`. | Medium — public type. |
| `Providers/ProviderDeserializeResult.cs` | Remains as the per-provider DTO. No structural change — but its consumer relationship narrows (now consumed only by `EntryOutcome.From`). Add doc clarifying that. | Low. |
| `Providers/ProviderRegistry.cs` | No structural change. (It still maps `providerType` strings to `ISerializationProvider` instances.) | None. |
| `AdminUI/Commands/SerializerSerializeCommand.cs` | No real change — `SerializeAll` keeps its predicates-driven shape. The only side effect is that the `ManifestWriter` now emits the richer manifest, which `SerializeAll` already builds via the `SerializeResult.Entry` field (additive). Remove the line that aggregates `WrittenFiles` and replace with aggregating `Entry` instances. | Low. |
| `AdminUI/Commands/SerializerDeserializeCommand.cs` | Drop the `ConfigLoader.Load` call entirely. Drop `config.Predicates.Where(...)` filter. Drop `excludeFieldsByItemType: config.ExcludeFieldsByItemType` etc. — those move to query-string / body params (or stay in config, see open question below). Caller-supplied `mode`, `conflictStrategy`, `strictMode`, `dryRun`, `providerFilter` come straight from the request; the orchestrator reads everything else from the manifest. Adapt the log-summary build to read `EntryOutcomes` instead of `DeserializeResults`. | High — primary command surface. **Open Q: do `ExcludeFieldsByItemType` / `ExcludeXmlElementsByType` move into the manifest, or into the request, or stay in config?** Recommend baking into the manifest at serialize time — they affect the output shape, so they're properly part of the artifact. |
| `AdminUI/Commands/DeserializeFromZipCommand.cs` | The zip command synthesises one inline `ContentEntry` (no manifest involved — there's no predicate either today, just a synthetic predicate at lines 76–84). Replace the synthetic predicate with a synthetic `ContentEntry`. Calls `ContentProvider.Deserialize` directly without going through the orchestrator. | Low — local rewrite, ≤20 LOC. |
| `Configuration/SerializerConfiguration.cs` | No structural change — config still drives serialize. The deserialize path stops *consulting* it. | None. |
| `Configuration/ConfigLoader.cs` | No change — still loads + validates predicates for serialize. | None. |
| `AdminUI/Tree/SerializerSettingsNodeProvider.cs`, `AdminUI/Queries/PredicateListQuery.cs` etc. | No change — these are admin-UI views over `config.Predicates`, all on the *serialize* / config-management path. | None. |

### Dead code paths that drop

| Path | Reason |
|------|--------|
| `SerializerOrchestrator.DeserializeAll(predicates, ...)` legacy `[Obsolete]` overload (lines 47–54) | Caller list is empty after the pivot. |
| `SerializerOrchestrator.SerializeAll(predicates, ...)` legacy `[Obsolete]` overload (lines 39–45) | Drop in the same sweep — cosmetic, not a milestone blocker, but free cleanup. |
| `ISerializationProvider.ValidatePredicate` | Validation happens at config-load (existing `ConfigLoader.ValidatePredicates`) for the serialize path. The deserialize path never sees a predicate, so validation is unreachable. |
| `SerializerOrchestrator` `validation = provider.ValidatePredicate(predicate)` blocks (lines 101–107 in Serialize, 240–246 in Deserialize) | Removed with `ValidatePredicate`. |
| `OrchestratorResult.DeserializeResults` | Replaced by `EntryOutcomes`. |
| `ProviderDeserializeResult.SourceToTargetPageMap` is a tunnel for cross-entry data. **It stays.** | Still needed: `ContentProvider.Deserialize` returns it, orchestrator threads into next `SqlTableEntry`. The tunnel is between providers, not between predicate and entry. |

### Test fixtures to replace

| Test file | Fixture today | Replacement |
|-----------|---------------|-------------|
| `tests/Providers/SerializerOrchestratorTests.cs` (~700 LOC) | Static `ContentPred1/2`, `SqlTablePred` records; tests inject these into `_orchestrator.SerializeAll(predicates, ...)` and `DeserializeAll(predicates, ...)`. The first few tests still target the predicate path (Serialize), which is fine. The deserialize tests need a manifest fixture. | Add a `BuildManifest(params ManifestEntry[])` helper. Tests that today inject predicates into `DeserializeAll` need to (a) write a manifest to a temp dir or (b) accept a test-only constructor overload that takes `Manifest` directly. Recommend (b) — `DeserializeAll(Manifest manifest, string modeRoot, ...)` as an internal overload, with the public one reading from disk. |
| `tests/Integration/StrictModeIntegrationTests.cs` (~200 LOC) | All five tests call `orchestrator.DeserializeAll(predicates, modeRoot, ...)`. | Convert each test to write a manifest file (or use the test-only overload) with a single `SqlTableEntry`. Mock provider stays the same except it receives `ManifestEntry` instead of `ProviderPredicateDefinition` in its `Deserialize` setup. |
| `tests/Providers/SqlTable/SqlTableProviderDeserializeTests.cs` | Calls provider directly, passes a predicate. | Rewrite to pass a `SqlTableEntry`. ~30 occurrences. |
| `tests/Providers/SqlTable/SqlTableProviderSeedMergeTests.cs` | Same pattern. | Same fix. |
| `tests/Providers/SqlTable/SqlTableLinkResolutionIntegrationTests.cs` | Same. | Same. |
| `tests/Providers/Content/ContentProviderTests.cs` | Calls `ContentProvider.Deserialize` with a predicate. | Pass a `ContentEntry`. |
| `tests/AdminUI/SerializerDeserializeCommandTests.cs` | Synth `OrchestratorResult` with `DeserializeResults`. | Synth with `EntryOutcomes`. |
| `tests/AdminUI/SerializerSerializeCommandTests.cs` | Same shape; also asserts on `result.HasErrors` mapping. | The serialize half is unchanged, but the `OrchestratorResult` shape changed — fix the synth to build `EntryOutcomes = []` for the deserialize half. |
| `tests/Infrastructure/ManifestWriterTests.cs` | Asserts on `{ mode, writtenAtUtc, files[] }`. | Assert on `{ schemaVersion, mode, writtenAtUtc, entries[] }` with polymorphic JSON. |
| `tests/Infrastructure/ManifestCleanerTests.cs` | Builds a manifest from a `List<string>` of files. | Adapt to the new shape (flatten `entries[].files[]` in the test setup). |
| `tests/IntegrationTests/CustomerCenterDeserializationTests.cs` | Builds a `SerializerConfiguration` with one predicate, calls `ContentSerializer/ContentDeserializer` directly (not via orchestrator) — *unaffected by the manifest change* since it bypasses the orchestrator. | None — flag as the one intentional bypass test. |

**Predicate-fixture vs entry-fixture summary:** ~7 test files need a fixture migration. None of them is small but none is structural — straight find-and-replace `ProviderPredicateDefinition { Name=, ProviderType="X", ... }` → `XEntry { EntryId=, ... }`.

### Open questions for the roadmapper

1. **Do `ExcludeFieldsByItemType` / `ExcludeXmlElementsByType` go into the manifest?** They affect serialize output, so they shape the on-disk artifact. **Strong recommendation: yes.** That's how the deserialize path stops needing config. Bake them into the `Manifest` envelope (top-level, not per-entry) at serialize time. ConfigLoader.Load disappears from `SerializerDeserializeCommand` cleanly.
2. **Strict mode: caller-supplied via request, or in manifest?** Strict mode is a *runtime* deserialize concern (does this run fail on warnings?), not an artifact concern. Keep it caller-supplied — it stays in the request.
3. **Default dry-run:** today read from `config.DryRun`. After pivot: caller-supplied. Default `false`. (Already covered by the milestone brief's "caller-supplied" list.)

---

## 6. Phase Decomposition Recommendation

**Two phases, dependency: Phase 1 → Phase 2.** A third phase is only needed if the per-entry reporting work shows up bigger than estimated; based on the LOC-count above I expect two phases is right.

### Phase 1: Manifest schema + entry hierarchy + serialize-side build

**Deliverables:**
- `ManifestEntry` abstract record + `ContentEntry` + `SqlTableEntry` sealed records (Section 1)
- `Manifest` envelope with `SchemaVersion = 2` (Section 1)
- `ManifestWriter`/`ManifestCleaner` rewritten for new shape (Section 5)
- `SerializeResult.Entry` property (Section 2)
- `SerializationProviderBase.BuildManifestEntry` abstract method (Section 2)
- `ContentProvider.BuildManifestEntry` + `SqlTableProvider.BuildManifestEntry` (Section 2)
- `SerializerOrchestrator.SerializeAll` aggregates entries into manifest, hands to `ManifestWriter`
- Tests: `ManifestWriterTests`, `ManifestCleanerTests` rewritten; provider tests get `BuildManifestEntry` coverage

**What's NOT in Phase 1:**
- Deserialize still consumes predicates (untouched). Old `DeserializeAll(predicates, ...)` keeps working. The pivot lands fully in Phase 2. This means after Phase 1: a manifest of the *new shape* is written, but the deserialize path still ignores it (just like today). End-of-Phase-1 verification: serialize emits the new manifest, deserialize still passes all existing tests.
- No `EntryOutcome` yet; `OrchestratorResult.DeserializeResults` unchanged.
- No command-surface change.

**Why this line:** Phase 1 is purely additive on the serialize side. It can ship in isolation, prove the manifest shape works in real serialize runs (Swift 2.2 baseline E2E re-run), and de-risk Phase 2.

### Phase 2: Manifest-driven deserialize + per-entry reporting + command surface

**Deliverables:**
- `SerializerOrchestrator.DeserializeAll` new signature (Section 3) — drops predicates parameter, reads manifest, dispatches by entry
- `EntryOutcome` + `EntryStatus` types (Section 4)
- `OrchestratorResult.EntryOutcomes` replaces `DeserializeResults` (Section 4)
- `ISerializationProvider.Deserialize(ManifestEntry, ...)` signature change
- `ContentProvider.Deserialize` / `SqlTableProvider.Deserialize` rewired to `ContentEntry` / `SqlTableEntry`
- `ValidatePredicate` removed from `ISerializationProvider` and base class
- `SerializerDeserializeCommand` drops `ConfigLoader.Load`, reads request params only
- `DeserializeFromZipCommand` synthesises `ContentEntry` inline
- `ExcludeFieldsByItemType` / `ExcludeXmlElementsByType` baked into `Manifest` envelope at serialize time (back-fill into Phase 1's manifest writer if not already there — note for the roadmapper)
- All deserialize-path tests migrated to entry fixtures (Section 5)
- Strict-mode escalator captures per-entry warnings (Section 4 "Per-entry warnings")

**Dependency arrow:** Phase 2 reads what Phase 1 wrote. Cannot start Phase 2 until Phase 1 ships an end-to-end serialize → on-disk manifest pipeline.

### Phase split decision criteria (recap)

| Concern | Phase 1 | Phase 2 |
|---------|:-------:|:-------:|
| Manifest schema design | ✅ | reads only |
| Entry record types | ✅ defined | ✅ consumed |
| Serialize-side `BuildManifestEntry` | ✅ | (already there) |
| Deserialize signature change | ❌ | ✅ |
| Command-surface change | ❌ | ✅ |
| Drop `ValidatePredicate` | ❌ | ✅ |
| Per-entry outcome reporting | ❌ | ✅ |
| `OrchestratorResult.EntryOutcomes` | ❌ | ✅ |
| Test fixture migration (predicate → entry) | only `ManifestWriterTests` / `ManifestCleanerTests` | all the rest (~7 files) |

The split lines up with verifiability: end-of-Phase-1 = "serialize emits a v2 manifest, all existing tests pass, on-disk shape can be inspected and reviewed by hand". End-of-Phase-2 = "deserialize works from manifest only, command surface no longer reads config, per-entry outcomes light up the UI/log viewer".

### Optional Phase 3 (only if Phase 2 grows past one phase)

If Phase 2's scope shows up larger in the plan than expected (likely culprits: `SerializerOrchestrator.cs` rewrite + 7 test files + manifest top-level baking of exclusion dicts), split off "command surface + zip synth + log viewer wiring" as Phase 3. Triggers for the split:
- Phase 2's planned task list exceeds ~6 days of work
- The exclusion-dict baking forces a Phase 1 re-open

**Default position: 2 phases.** The roadmapper should assume 2 unless concrete planning evidence forces a third.

---

## Architectural Anti-Patterns to Avoid

### Anti-Pattern 1: `IManifestStore` abstraction

**What people do:** Wrap `ManifestWriter` + `ManifestCleaner` behind an `IManifestStore` interface "for testability".
**Why it's wrong:** The current concrete `ManifestWriter`/`ManifestCleaner` classes are already trivially testable (file-system fixtures in temp dirs, see `ManifestWriterTests`). Adding an interface is 100% pure indirection — there's one implementation, ever. Any future need (S3, etc.) is conjecture. `ManifestWriter.Read(modeRoot, mode)` returning `Manifest?` is the right shape; calling sites that need to mock it can use a `class TestManifestWriter : ManifestWriter` subclass.
**Do this instead:** Concrete classes. If a test needs a manifest in memory, build one and write it to a temp dir; the file system *is* the abstraction.

### Anti-Pattern 2: `IEntryDispatcher` extracted from the orchestrator

**What people do:** "The dispatch loop is complex, let's extract `IEntryDispatcher.Dispatch(ManifestEntry, ...) → EntryOutcome` and inject it into the orchestrator."
**Why it's wrong:** The dispatch loop is ~30 lines and shares state with the FK reorder, the page-id map, the cache invalidator, and the strict-mode escalator. Pulling it out splits the state across two types and makes the orchestrator's job harder to follow. The orchestrator IS the dispatcher.
**Do this instead:** Keep the loop in `SerializerOrchestrator.DeserializeAll`. Extract small private static helpers (`ReorderEntries`, `BuildResolverFor`) if blocks get noisy.

### Anti-Pattern 3: Polymorphic dispatch via `entry.Accept(visitor)`

**What people do:** Visitor pattern over `ManifestEntry` — `entry.Dispatch(orchestrator)` calls back into the orchestrator with the concrete type.
**Why it's wrong:** Two types in v0.6.0, three at most after a `EmbeddedXmlEntry` follow-up. Visitor is overkill for ≤4 types. The provider lookup is already by `providerType` string + a registry — that's the dispatch mechanism, and it works. The provider does its own downcast (`if (entry is not SqlTableEntry sql) throw...`) which is one line and self-documenting.
**Do this instead:** String-keyed registry → provider; provider downcasts at the top of `Deserialize`.

### Anti-Pattern 4: Manifest as both serialize-output AND deserialize-input format, bidirectionally evolved

**What people do:** Allow callers to hand-edit the manifest between runs — "as a feature".
**Why it's wrong:** The milestone brief explicitly defers this ("hand-edit fallback" is in the tracked-but-deferred list). Treating the manifest as machine-generated only keeps the contract narrow. If a user hand-edits and breaks it, schema-version-bump + clear error is the response, not bidirectional reconciliation.
**Do this instead:** Manifest is write-once-by-orchestrator, read-only-by-orchestrator. Document this as a comment on `Manifest`. Phase 2 schema-version check enforces it.

---

## Sources

- `src/DynamicWeb.Serializer/Providers/SerializerOrchestrator.cs` (lines 1–399)
- `src/DynamicWeb.Serializer/Providers/SerializationProviderBase.cs`
- `src/DynamicWeb.Serializer/Providers/Content/ContentProvider.cs`
- `src/DynamicWeb.Serializer/Providers/SqlTable/SqlTableProvider.cs`
- `src/DynamicWeb.Serializer/Providers/SqlTable/FkDependencyResolver.cs`
- `src/DynamicWeb.Serializer/Providers/SerializeResult.cs`, `ProviderDeserializeResult.cs`
- `src/DynamicWeb.Serializer/Providers/ProviderRegistry.cs`, `ISerializationProvider.cs`
- `src/DynamicWeb.Serializer/Models/ProviderPredicateDefinition.cs`
- `src/DynamicWeb.Serializer/Infrastructure/ManifestWriter.cs`, `ManifestCleaner.cs`
- `src/DynamicWeb.Serializer/Configuration/SerializerConfiguration.cs`
- `src/DynamicWeb.Serializer/AdminUI/Commands/SerializerSerializeCommand.cs`, `SerializerDeserializeCommand.cs`, `DeserializeFromZipCommand.cs`
- `tests/DynamicWeb.Serializer.Tests/Providers/SerializerOrchestratorTests.cs`
- `tests/DynamicWeb.Serializer.Tests/Integration/StrictModeIntegrationTests.cs`
- `.planning/PROJECT.md` (milestone v0.6.0 section)

---
*Architecture research for: DynamicWeb.Serializer v0.6.0 Manifest-Driven Deserialize*
*Researched: 2026-05-08*
