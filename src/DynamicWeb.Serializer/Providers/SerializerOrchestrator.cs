using DynamicWeb.Serializer.Configuration;
using DynamicWeb.Serializer.Infrastructure;
using DynamicWeb.Serializer.Models;
using DynamicWeb.Serializer.Providers.SqlTable;
using DynamicWeb.Serializer.Reporting;
using DynamicWeb.Serializer.Serialization;

namespace DynamicWeb.Serializer.Providers;

/// <summary>
/// Central dispatch: iterates predicates, resolves providers via ProviderRegistry,
/// validates each predicate, and aggregates results across all providers.
/// Supports FK-ordered deserialization, per-predicate cache invalidation, and
/// mode-aware (Deploy/Seed) execution per Phase 37-01.
/// </summary>
public class SerializerOrchestrator
{
    private readonly ProviderRegistry _registry;
    private readonly FkDependencyResolver? _fkResolver;
    private readonly CacheInvalidator? _cacheInvalidator;
    private readonly EcomGroupFieldSchemaSync? _ecomSchemaSync;
    private readonly ManifestWriter _manifestWriter;

    public SerializerOrchestrator(
        ProviderRegistry registry,
        FkDependencyResolver? fkResolver = null,
        CacheInvalidator? cacheInvalidator = null,
        EcomGroupFieldSchemaSync? ecomSchemaSync = null,
        ManifestWriter? manifestWriter = null)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _fkResolver = fkResolver;
        _cacheInvalidator = cacheInvalidator;
        _ecomSchemaSync = ecomSchemaSync;
        // Phase 43 / DESER-01: ManifestWriter is needed by the manifest-driven DeserializeAll
        // signature. Defaulting to a fresh instance keeps the legacy DeserializeAll(predicates, ...)
        // overload (which doesn't read the manifest) callable without explicit wiring.
        _manifestWriter = manifestWriter ?? new ManifestWriter();
    }

    // -------------------------------------------------------------------------
    // Legacy overloads (pre-Phase-37 call sites). Default to Deploy mode + SourceWins
    // so existing callers / tests compile without touching them.
    // -------------------------------------------------------------------------

    [Obsolete("Pass DeploymentMode explicitly — see Phase 37-01.")]
    public OrchestratorResult SerializeAll(
        List<ProviderPredicateDefinition> predicates,
        string outputRoot,
        Action<string>? log = null,
        string? providerFilter = null) =>
        SerializeAll(predicates, outputRoot, DeploymentMode.Deploy, ConflictStrategy.SourceWins, log, providerFilter, manifestWriter: null, manifestCleaner: null);

    [Obsolete("Pass DeploymentMode and ConflictStrategy explicitly — see Phase 37-01.")]
    public OrchestratorResult DeserializeAll(
        List<ProviderPredicateDefinition> predicates,
        string inputRoot,
        Action<string>? log = null,
        bool isDryRun = false,
        string? providerFilter = null) =>
        DeserializeAll(predicates, inputRoot, DeploymentMode.Deploy, ConflictStrategy.SourceWins, log, isDryRun, providerFilter);

    // -------------------------------------------------------------------------
    // Mode-aware overloads (Phase 37-01)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Serialize all predicates, scoped to the given mode. The mode/strategy pair is logged at the
    /// start of the run. Strategy is currently unused on the serialize path (it only affects
    /// deserialize conflict resolution), but is threaded through for symmetry with DeserializeAll.
    /// When <paramref name="manifestWriter"/> / <paramref name="manifestCleaner"/> are supplied,
    /// the orchestrator emits <c>{mode}-manifest.json</c> and deletes stale files under
    /// <paramref name="outputRoot"/> after the run (Phase 37-01 Task 2). Exceptions bubble out
    /// BEFORE the manifest step, so partial/failed runs leave stale files intact for debugging.
    /// </summary>
    public OrchestratorResult SerializeAll(
        List<ProviderPredicateDefinition> predicates,
        string outputRoot,
        DeploymentMode mode,
        ConflictStrategy strategy,
        Action<string>? log = null,
        string? providerFilter = null,
        ManifestWriter? manifestWriter = null,
        ManifestCleaner? manifestCleaner = null,
        IReadOnlyDictionary<string, List<string>>? excludeFieldsByItemType = null,
        IReadOnlyDictionary<string, List<string>>? excludeXmlElementsByType = null)
    {
        log?.Invoke($"=== Mode: {mode} | Strategy: {strategy} ===");

        var results = new List<SerializeResult>();
        var errors = new List<string>();

        foreach (var predicate in predicates)
        {
            if (providerFilter != null &&
                !string.Equals(predicate.ProviderType, providerFilter, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!_registry.HasProvider(predicate.ProviderType))
            {
                var msg = $"No provider registered for type '{predicate.ProviderType}' (predicate: {predicate.Name})";
                errors.Add(msg);
                log?.Invoke($"WARNING: Skipping predicate '{predicate.Name}' — no provider for type '{predicate.ProviderType}'");
                continue;
            }

            var provider = _registry.GetProvider(predicate.ProviderType);

            // Phase 43 / DESER-03: ValidatePredicate is no longer on the interface; each provider
            // exposes it concretely. Pre-flight via SerializeAllValidate to keep this loop's
            // skip-on-invalid behaviour. Each provider's own Serialize body validates again
            // internally, so the pre-flight is a logging convenience, not a correctness gate.
            var validation = ValidateBeforeSerialize(provider, predicate);
            if (!validation.IsValid)
            {
                errors.AddRange(validation.Errors.Select(e => $"{predicate.Name}: {e}"));
                log?.Invoke($"WARNING: Skipping predicate '{predicate.Name}' — validation failed: {string.Join(", ", validation.Errors)}");
                continue;
            }

            var result = provider.Serialize(predicate, outputRoot, log,
                excludeFieldsByItemType, excludeXmlElementsByType);
            results.Add(result);
        }

        int stale = 0;
        if (manifestWriter != null || manifestCleaner != null)
        {
            var modeName = mode.ToString().ToLowerInvariant();
            var allWritten = results.SelectMany(r => r.WrittenFiles).ToList();

            // Phase 42-03: collect non-null Entry instances across providers. Validation-failed
            // results return null Entry (per SerializeResult.Entry docstring); they don't appear
            // in the manifest, but their files (if any) still feed the cleaner.
            var entries = results
                .Where(r => r.Entry is not null)
                .Select(r => r.Entry!)
                .ToList();

            // Phase 42-03 / MANIFEST-05: bake the by-ItemType exclusion maps into the envelope
            // so the deserialize path (Phase 43) does not need to consult Serializer.config.json
            // to read them.
            manifestWriter?.Write(outputRoot, modeName, entries,
                excludeFieldsByItemType: excludeFieldsByItemType,
                excludeXmlElementsByType: excludeXmlElementsByType);

            if (manifestCleaner != null)
                stale = manifestCleaner.CleanStale(outputRoot, modeName, allWritten, log);
        }

        return new OrchestratorResult { SerializeResults = results, Errors = errors, StaleFilesDeleted = stale };
    }

    /// <summary>
    /// Deserialize all predicates, scoped to the given mode.
    /// </summary>
    /// <remarks>
    /// Phase 43 / DESER-01: this predicate-typed overload is obsolete — Phase 44 deletes it
    /// (CONVERGE-04). New callers MUST use <see cref="DeserializeAll(string, DeploymentMode, ConflictStrategy, Action{string}, bool, string, StrictModeEscalator, IReadOnlyDictionary{string, List{string}}, IReadOnlyDictionary{string, List{string}})"/>
    /// which reads the manifest and dispatches per-entry. The body of this overload now
    /// converts each predicate to a transient <see cref="ManifestEntry"/> via
    /// <see cref="ISerializationProvider.BuildManifestEntry"/> before dispatching, but this
    /// is a wave-bounded compile bridge — Phase 44 removes both the overload and the bridge.
    /// </remarks>
    [Obsolete("Phase 43 (DESER-01): pass modeRoot + mode and let the orchestrator read the manifest. This overload deletes in Phase 44 (CONVERGE-04). Use DeserializeAll(modeRoot, mode, strategy, log, isDryRun, providerFilter, escalator, ...).", error: false)]
    public OrchestratorResult DeserializeAll(
        List<ProviderPredicateDefinition> predicates,
        string inputRoot,
        DeploymentMode mode,
        ConflictStrategy strategy,
        Action<string>? log = null,
        bool isDryRun = false,
        string? providerFilter = null,
        StrictModeEscalator? escalator = null,
        IReadOnlyDictionary<string, List<string>>? excludeFieldsByItemType = null,
        IReadOnlyDictionary<string, List<string>>? excludeXmlElementsByType = null)
    {
        // Phase 37-04 STRICT-01: wrap the caller's log so every "WARNING:" line flows
        // through the escalator. Non-warning lines pass through untouched. Legacy
        // callers that don't provide an escalator get StrictModeEscalator.Null, which
        // is always-lenient — log-and-continue (v0.4.x parity).
        escalator ??= StrictModeEscalator.Null;
        var wrappedLog = WrapLogWithEscalator(log, escalator);

        wrappedLog($"=== Mode: {mode} | Strategy: {strategy} | Strict: {escalator.IsStrict} ===");

        var results = new List<ProviderDeserializeResult>();
        var errors = new List<string>();

        // FK ordering: sort SqlTable predicates by dependency order (parents first, children last).
        // Content and other predicates are unaffected.
        if (_fkResolver != null)
        {
            var sqlTablePredicates = predicates
                .Where(p => string.Equals(p.ProviderType, "SqlTable", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (sqlTablePredicates.Count > 1)
            {
                var tableNames = sqlTablePredicates
                    .Where(p => !string.IsNullOrEmpty(p.Table))
                    .Select(p => p.Table!)
                    .ToList();

                var orderedTables = _fkResolver.GetDeserializationOrder(tableNames);

                var orderIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < orderedTables.Count; i++)
                    orderIndex[orderedTables[i]] = i;

                var nonSqlPredicates = predicates
                    .Where(p => !string.Equals(p.ProviderType, "SqlTable", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                var sortedSqlPredicates = sqlTablePredicates
                    .OrderBy(p => orderIndex.TryGetValue(p.Table ?? "", out var idx) ? idx : int.MaxValue)
                    .ToList();

                predicates = sortedSqlPredicates.Concat(nonSqlPredicates).ToList();

                log?.Invoke($"FK ordering: {string.Join(" -> ", orderedTables)}");
            }
        }

        // Phase 37-05 / LINK-02 pass 2 (D-22): when ANY SqlTable predicate has a non-empty
        // ResolveLinksInColumns list, Content predicates MUST run BEFORE those SqlTable
        // predicates so the source→target page ID map is built and available at write time.
        // Chosen over the "second deserialize pass" alternative because it's a simple list
        // reorder — no second sweep through SqlTable data needed.
        var anySqlNeedsLinks = predicates.Any(p =>
            string.Equals(p.ProviderType, "SqlTable", StringComparison.OrdinalIgnoreCase)
            && p.ResolveLinksInColumns.Count > 0);
        if (anySqlNeedsLinks)
        {
            var contentPredicates = predicates
                .Where(p => string.Equals(p.ProviderType, "Content", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var otherPredicates = predicates
                .Where(p => !string.Equals(p.ProviderType, "Content", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (contentPredicates.Count > 0)
            {
                predicates = contentPredicates.Concat(otherPredicates).ToList();
                wrappedLog(
                    $"LINK-02 ordering: running {contentPredicates.Count} Content predicate(s) " +
                    "first so cross-env page ID map is available to SqlTable link resolution.");
            }
        }

        // Accumulates the source→target page ID map across Content predicate runs.
        // Populated when a ContentProvider returns a non-null SourceToTargetPageMap and
        // consumed by subsequent SqlTable predicates whose ResolveLinksInColumns is non-empty.
        var aggregatedPageMap = new Dictionary<int, int>();

        foreach (var predicate in predicates)
        {
            if (providerFilter != null &&
                !string.Equals(predicate.ProviderType, providerFilter, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!_registry.HasProvider(predicate.ProviderType))
            {
                var msg = $"No provider registered for type '{predicate.ProviderType}' (predicate: {predicate.Name})";
                errors.Add(msg);
                wrappedLog($"WARNING: Skipping predicate '{predicate.Name}' — no provider for type '{predicate.ProviderType}'");
                continue;
            }

            var provider = _registry.GetProvider(predicate.ProviderType);

            // Phase 43 / DESER-03: ValidatePredicate is no longer on the interface; each provider
            // exposes it concretely. Pre-flight via typed dispatch to keep skip-on-invalid behaviour.
            var validation = ValidateBeforeSerialize(provider, predicate);
            if (!validation.IsValid)
            {
                errors.AddRange(validation.Errors.Select(e => $"{predicate.Name}: {e}"));
                wrappedLog($"WARNING: Skipping predicate '{predicate.Name}' — validation failed: {string.Join(", ", validation.Errors)}");
                continue;
            }

            // Phase 37-05 / LINK-02: build an InternalLinkResolver from the accumulated map
            // when this predicate is a SqlTable that opted in via ResolveLinksInColumns.
            InternalLinkResolver? perRunResolver = null;
            var needsLinks = string.Equals(predicate.ProviderType, "SqlTable", StringComparison.OrdinalIgnoreCase)
                             && predicate.ResolveLinksInColumns.Count > 0
                             && aggregatedPageMap.Count > 0;
            if (needsLinks)
                perRunResolver = new InternalLinkResolver(aggregatedPageMap, wrappedLog);

            // Phase 43 / DESER-03: provider.Deserialize now takes a ManifestEntry. The legacy
            // predicate-typed DeserializeAll converts via the provider's BuildManifestEntry
            // (Phase 42 contract, predicate-typed input). The synthetic entry never escapes
            // this loop. Phase 44 deletes this overload entirely.
            var entry = provider.BuildManifestEntry(predicate, inputRoot, Array.Empty<string>());
            var result = provider.Deserialize(entry, inputRoot, wrappedLog, isDryRun, strategy, perRunResolver,
                excludeFieldsByItemType, excludeXmlElementsByType);
            results.Add(result);

            // Accumulate source→target map contributions from Content predicates so
            // subsequent SqlTable predicates can use them for link resolution.
            if (result.SourceToTargetPageMap != null)
            {
                foreach (var kvp in result.SourceToTargetPageMap)
                    aggregatedPageMap.TryAdd(kvp.Key, kvp.Value);
            }

            // Cache invalidation: clear configured service caches after successful deserialize.
            if (!isDryRun && predicate.ServiceCaches.Count > 0 && !result.HasErrors)
            {
                if (_cacheInvalidator == null)
                {
                    wrappedLog($"WARNING: Predicate '{predicate.Name}' declares {predicate.ServiceCaches.Count} service cache(s) but no CacheInvalidator is wired — caches will NOT be cleared");
                }
                else
                {
                    try
                    {
                        _cacheInvalidator.InvalidateCaches(predicate.ServiceCaches, wrappedLog);
                    }
                    catch (Exception ex)
                    {
                        wrappedLog($"WARNING: Cache invalidation failed for predicate '{predicate.Name}': {ex.Message}");
                    }
                }
            }

            // Schema sync: create custom columns on target table after field definitions are imported
            if (!isDryRun && _ecomSchemaSync != null
                && !string.IsNullOrEmpty(predicate.SchemaSync)
                && string.Equals(predicate.SchemaSync, "EcomGroupFields", StringComparison.OrdinalIgnoreCase)
                && !result.HasErrors)
            {
                try
                {
                    wrappedLog($"Running schema sync for {predicate.Name}...");
                    _ecomSchemaSync.SyncSchema(wrappedLog);
                }
                catch (Exception ex)
                {
                    wrappedLog($"WARNING: Schema sync failed for predicate '{predicate.Name}': {ex.Message}");
                }
            }
        }

        // Phase 37-04 STRICT-01: end-of-run gate. In strict mode with any escalated warnings,
        // this throws CumulativeStrictModeException. We catch and collect into Errors so the
        // OrchestratorResult surfaces the failure without masking successful per-predicate work;
        // the caller (CLI/API) inspects HasErrors and the exception text in the log.
        try
        {
            escalator.AssertNoWarnings();
        }
        catch (CumulativeStrictModeException ex)
        {
            errors.Add(ex.Message);
            wrappedLog($"ERROR: {ex.Message}");
        }

        return new OrchestratorResult { DeserializeResults = results, Errors = errors };
    }

    // -------------------------------------------------------------------------
    // Phase 43 / DESER-01..05 + REPORT-01..05: manifest-driven deserialize.
    // Reads {mode}-manifest.json from modeRoot and dispatches each entry. No
    // predicates parameter; no Serializer.config.json consultation; per-entry
    // EntryOutcome reporting.
    // -------------------------------------------------------------------------

    /// <summary>
    /// Phase 43 / DESER-01: manifest-driven deserialize. Reads
    /// <c>{mode}-manifest.json</c> from <paramref name="modeRoot"/> and dispatches each entry
    /// to the registered provider for its <c>ProviderType</c>. Per-entry outcomes (status,
    /// counts, errors, duration) populate <see cref="OrchestratorResult.EntryOutcomes"/>.
    /// </summary>
    /// <param name="modeRoot">Mode-scoped serialize directory (the dir containing
    /// <c>{mode}-manifest.json</c> + the per-provider subtrees).</param>
    /// <param name="mode">Deployment mode. The lowercased form is used as the manifest filename
    /// prefix (<c>"deploy"</c> / <c>"seed"</c>).</param>
    /// <param name="strategy">Conflict strategy (Deploy=SourceWins, Seed=DestinationWins).</param>
    /// <param name="log">Optional log sink — every entry emits a <c>[entryId] Status</c> line per
    /// REPORT-05 / SC-5.</param>
    /// <param name="isDryRun">When true, providers report would-be work without touching the DB.</param>
    /// <param name="providerFilter">Optional filter. Entries whose <c>ProviderType</c> doesn't
    /// match get an <see cref="EntryStatus.Skipped"/> outcome rather than being silently dropped
    /// (per REPORT-01 / D-02).</param>
    /// <param name="escalator">Optional strict-mode escalator. Phase 37-04 wiring is preserved
    /// unchanged; <see cref="CumulativeStrictModeException"/> at end-of-run produces an
    /// <see cref="EntryOutcome.RunLevelError"/> in addition to the run-level errors list.</param>
    /// <param name="excludeFieldsByItemType">Caller-supplied fallback when the manifest envelope
    /// has no envelope-level by-ItemType field exclusions. The manifest envelope (when populated
    /// per MANIFEST-05) takes precedence.</param>
    /// <param name="excludeXmlElementsByType">Same, for XML element exclusions.</param>
    public OrchestratorResult DeserializeAll(
        string modeRoot,
        DeploymentMode mode,
        ConflictStrategy strategy = ConflictStrategy.SourceWins,
        Action<string>? log = null,
        bool isDryRun = false,
        string? providerFilter = null,
        StrictModeEscalator? escalator = null,
        IReadOnlyDictionary<string, List<string>>? excludeFieldsByItemType = null,
        IReadOnlyDictionary<string, List<string>>? excludeXmlElementsByType = null)
    {
        var modeName = mode.ToString().ToLowerInvariant();
        var manifest = _manifestWriter.Read(modeRoot, modeName)
            ?? throw new InvalidOperationException(
                $"Manifest not found at {Path.Combine(modeRoot, $"{modeName}-manifest.json")}. " +
                "Run serialize first to produce the manifest, then re-run deserialize.");

        // Per MANIFEST-05: envelope-level by-ItemType exclusions are baked at serialize time and
        // take precedence over caller-supplied params (which exist only as a transitional fallback
        // for tests / call sites that haven't migrated). Empty envelope dicts mean "no exclusions"
        // — they don't fall back to caller; only an absent envelope key would, but Phase 42's
        // Manifest.ExcludeFieldsByItemType is `required` so it's always present.
        var effectiveExcludeFields = manifest.ExcludeFieldsByItemType.Count > 0
            ? (IReadOnlyDictionary<string, List<string>>)manifest.ExcludeFieldsByItemType
            : excludeFieldsByItemType;
        var effectiveExcludeXml = manifest.ExcludeXmlElementsByType.Count > 0
            ? (IReadOnlyDictionary<string, List<string>>)manifest.ExcludeXmlElementsByType
            : excludeXmlElementsByType;

        return DeserializeEntries(manifest.Entries, modeRoot, mode, strategy, log, isDryRun,
            providerFilter, escalator, effectiveExcludeFields, effectiveExcludeXml);
    }

    /// <summary>
    /// Phase 43 internal test seam (per ARCHITECTURE.md §5): dispatch a pre-built entry list
    /// without touching the filesystem. Production callers go through the public
    /// <see cref="DeserializeAll(string, DeploymentMode, ConflictStrategy, Action{string}, bool, string, StrictModeEscalator, IReadOnlyDictionary{string, List{string}}, IReadOnlyDictionary{string, List{string}})"/>
    /// which reads the manifest and calls this. Tests construct entry fixtures directly.
    /// </summary>
    internal OrchestratorResult DeserializeEntries(
        IReadOnlyList<ManifestEntry> entries,
        string modeRoot,
        DeploymentMode mode,
        ConflictStrategy strategy,
        Action<string>? log,
        bool isDryRun,
        string? providerFilter,
        StrictModeEscalator? escalator,
        IReadOnlyDictionary<string, List<string>>? excludeFieldsByItemType,
        IReadOnlyDictionary<string, List<string>>? excludeXmlElementsByType)
    {
        // Phase 37-04 STRICT-01: wrap log with escalator (verbatim from legacy body).
        escalator ??= StrictModeEscalator.Null;
        var wrappedLog = WrapLogWithEscalator(log, escalator);
        wrappedLog($"=== Mode: {mode} | Strategy: {strategy} | Strict: {escalator.IsStrict} ===");

        var workingEntries = entries.ToList();

        // Phase 43 / DESER-02 / SC-6: FK ordering on entries[]. Same algorithm as the
        // predicate-typed legacy body, swapping `predicates` for `workingEntries.OfType<SqlTableEntry>`.
        if (_fkResolver != null)
        {
            var sqlEntries = workingEntries.OfType<SqlTableEntry>().ToList();
            if (sqlEntries.Count > 1)
            {
                var tableNames = sqlEntries
                    .Where(e => !string.IsNullOrEmpty(e.Table))
                    .Select(e => e.Table)
                    .ToList();

                var orderedTables = _fkResolver.GetDeserializationOrder(tableNames);

                var orderIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < orderedTables.Count; i++)
                    orderIndex[orderedTables[i]] = i;

                // Reorder SqlTable entries by FK order; keep non-SqlTable entries in their
                // original relative order. Same shape as the legacy predicate-typed sort.
                var nonSqlEntries = workingEntries
                    .Where(e => e is not SqlTableEntry)
                    .ToList();
                var sortedSqlEntries = sqlEntries
                    .OrderBy(e => orderIndex.TryGetValue(e.Table, out var idx) ? idx : int.MaxValue)
                    .Cast<ManifestEntry>()
                    .ToList();

                workingEntries = sortedSqlEntries.Concat(nonSqlEntries).ToList();

                wrappedLog($"FK ordering: {string.Join(" -> ", orderedTables)}");
            }
        }

        // Phase 37-05 / LINK-02 pass 2 (D-22): when ANY SqlTable entry has a non-empty
        // ResolveLinksInColumns list, Content entries MUST run BEFORE those SqlTable entries
        // so the source→target page ID map is built and available at write time.
        var anySqlNeedsLinks = workingEntries
            .OfType<SqlTableEntry>()
            .Any(s => s.ResolveLinksInColumns.Count > 0);
        if (anySqlNeedsLinks)
        {
            var contentEntries = workingEntries.OfType<ContentEntry>().Cast<ManifestEntry>().ToList();
            var otherEntries = workingEntries.Where(e => e is not ContentEntry).ToList();
            if (contentEntries.Count > 0)
            {
                workingEntries = contentEntries.Concat(otherEntries).ToList();
                wrappedLog(
                    $"LINK-02 ordering: running {contentEntries.Count} Content entries " +
                    "first so cross-env page ID map is available to SqlTable link resolution.");
            }
        }

        // Per-entry dispatch loop. Builds EntryOutcome per entry per REPORT-02; legacy
        // ProviderDeserializeResult list stays populated as a transient compatibility surface
        // (consumers should drive off EntryOutcomes per REPORT-03).
        var entryOutcomes = new List<EntryOutcome>();
        var legacyResults = new List<ProviderDeserializeResult>();
        var errors = new List<string>();
        var aggregatedPageMap = new Dictionary<int, int>();

        foreach (var entry in workingEntries)
        {
            // providerFilter exclusion → Skipped per REPORT-01 / D-02 / SC-2.
            if (providerFilter != null &&
                !string.Equals(entry.ProviderType, providerFilter, StringComparison.OrdinalIgnoreCase))
            {
                entryOutcomes.Add(EntryOutcome.Skipped(entry,
                    $"providerFilter='{providerFilter}' excluded providerType='{entry.ProviderType}'"));
                wrappedLog($"[{entry.EntryId}] Skipped: providerFilter exclusion");
                continue;
            }

            // No provider registered → Failed per D-02.
            if (!_registry.HasProvider(entry.ProviderType))
            {
                var msg = $"No provider registered for type '{entry.ProviderType}' (entry: {entry.EntryId})";
                errors.Add(msg);
                entryOutcomes.Add(EntryOutcome.Failed(entry, msg));
                wrappedLog($"[{entry.EntryId}] Failed: {msg}");
                continue;
            }

            // Phase 37-05 / LINK-02 pass 2: build an InternalLinkResolver from the accumulated
            // map when this entry is a SqlTableEntry that opted in via ResolveLinksInColumns.
            InternalLinkResolver? perRunResolver = null;
            var needsLinks = entry is SqlTableEntry sqlNeedsLinks
                             && sqlNeedsLinks.ResolveLinksInColumns.Count > 0
                             && aggregatedPageMap.Count > 0;
            if (needsLinks)
                perRunResolver = new InternalLinkResolver(aggregatedPageMap, wrappedLog);

            var provider = _registry.GetProvider(entry.ProviderType);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            ProviderDeserializeResult result;
            try
            {
                result = provider.Deserialize(entry, modeRoot, wrappedLog, isDryRun, strategy,
                    perRunResolver, excludeFieldsByItemType, excludeXmlElementsByType);
            }
            catch (Exception ex)
            {
                sw.Stop();
                var emsg = $"Entry '{entry.EntryId}' threw: {ex.Message}";
                errors.Add(emsg);
                entryOutcomes.Add(EntryOutcome.Failed(entry, emsg, sw.Elapsed));
                wrappedLog($"[{entry.EntryId}] Failed: {emsg}");
                continue;
            }
            sw.Stop();

            legacyResults.Add(result);
            entryOutcomes.Add(EntryOutcome.From(entry, result, sw.Elapsed));

            // Per-entry log line per REPORT-05 / SC-5 (CONTEXT line 50 format).
            wrappedLog($"[{entry.EntryId}] {entryOutcomes[^1].Status}: {result.Summary}");

            // Aggregate source→target page map (Content provider populates it; downstream
            // SqlTable entries with ResolveLinksInColumns consume it via perRunResolver).
            if (result.SourceToTargetPageMap != null)
            {
                foreach (var kvp in result.SourceToTargetPageMap)
                    aggregatedPageMap.TryAdd(kvp.Key, kvp.Value);
            }

            // Cache invalidation gated on entry being a SqlTableEntry with ServiceCaches set.
            if (!isDryRun && entry is SqlTableEntry sqlEntryCache
                && sqlEntryCache.ServiceCaches.Count > 0
                && !result.HasErrors)
            {
                if (_cacheInvalidator == null)
                {
                    wrappedLog(
                        $"WARNING: Entry '{entry.EntryId}' declares {sqlEntryCache.ServiceCaches.Count} " +
                        "service cache(s) but no CacheInvalidator is wired — caches will NOT be cleared");
                }
                else
                {
                    try { _cacheInvalidator.InvalidateCaches(sqlEntryCache.ServiceCaches.ToList(), wrappedLog); }
                    catch (Exception ex)
                    {
                        wrappedLog($"WARNING: Cache invalidation failed for entry '{entry.EntryId}': {ex.Message}");
                    }
                }
            }

            // Schema sync gated on entry being a SqlTableEntry with SchemaSync = "EcomGroupFields".
            if (!isDryRun && _ecomSchemaSync != null
                && entry is SqlTableEntry sqlEntrySync
                && !string.IsNullOrEmpty(sqlEntrySync.SchemaSync)
                && string.Equals(sqlEntrySync.SchemaSync, "EcomGroupFields", StringComparison.OrdinalIgnoreCase)
                && !result.HasErrors)
            {
                try
                {
                    wrappedLog($"Running schema sync for {entry.EntryId}...");
                    _ecomSchemaSync.SyncSchema(wrappedLog);
                }
                catch (Exception ex)
                {
                    wrappedLog($"WARNING: Schema sync failed for entry '{entry.EntryId}': {ex.Message}");
                }
            }
        }

        // Phase 37-04 STRICT-01: end-of-run gate. CONTEXT line 99-100 — strict-mode
        // CumulativeStrictModeException is routed into both the run-level errors list AND
        // a synthetic RunLevelError EntryOutcome so HasErrors aggregates from EntryOutcomes.
        try
        {
            escalator.AssertNoWarnings();
        }
        catch (CumulativeStrictModeException ex)
        {
            errors.Add(ex.Message);
            entryOutcomes.Add(EntryOutcome.RunLevelError(ex.Message));
            wrappedLog($"ERROR: {ex.Message}");
        }

        return new OrchestratorResult
        {
            DeserializeResults = legacyResults,
            EntryOutcomes = entryOutcomes,
            Errors = errors
        };
    }

    /// <summary>
    /// Phase 43 / DESER-03: typed-dispatch validation helper for the serialize-side and the
    /// legacy predicate-typed DeserializeAll body. ValidatePredicate is no longer on the
    /// <see cref="ISerializationProvider"/> contract; each concrete provider keeps it as a
    /// public method for serialize-time input gating. This helper polymorphically routes to
    /// the right concrete method without re-introducing the interface dependency. Returns
    /// <see cref="ValidationResult.Success"/> for unrecognised provider types so callers
    /// fall through to the provider's own internal validation in Serialize/Deserialize.
    /// </summary>
    private static ValidationResult ValidateBeforeSerialize(ISerializationProvider provider, ProviderPredicateDefinition predicate)
    {
        return provider switch
        {
            Content.ContentProvider c => c.ValidatePredicate(predicate),
            SqlTable.SqlTableProvider s => s.ValidatePredicate(predicate),
            _ => ValidationResult.Success()
        };
    }

    /// <summary>
    /// Phase 37-04: wrap the caller's log so every "WARNING:" line (from anywhere —
    /// orchestrator, provider, ContentDeserializer, InternalLinkResolver, etc.) routes
    /// through the escalator. Non-WARNING lines pass through unchanged. In strict mode
    /// the warning is recorded for end-of-run assertion; the single log emission still
    /// reaches the caller's sink so operators see every warning in real time.
    /// </summary>
    private static Action<string> WrapLogWithEscalator(Action<string>? callerLog, StrictModeEscalator escalator)
    {
        return msg =>
        {
            if (msg is null)
            {
                callerLog?.Invoke(string.Empty);
                return;
            }

            // Forward to the caller's log first so the line appears in real-time output.
            callerLog?.Invoke(msg);

            // Route WARNING lines into the escalator's record buffer (strict) without a
            // second log emission — we pass a null log sink to Escalate.
            if (msg.TrimStart().StartsWith("WARNING", StringComparison.OrdinalIgnoreCase))
                escalator.RecordOnly(msg);
        };
    }
}

/// <summary>
/// Aggregated result from orchestrator operations across multiple providers.
/// </summary>
public record OrchestratorResult
{
    public List<SerializeResult> SerializeResults { get; init; } = new();

    /// <summary>
    /// Per-table deserialize results from the dispatch loop. Phase 43 / REPORT-03 demoted this
    /// from canonical-truth to a transient compatibility surface during the orchestrator pivot.
    /// New consumers MUST drive off <see cref="EntryOutcomes"/> instead. Phase 44 deletes this.
    /// </summary>
    public List<ProviderDeserializeResult> DeserializeResults { get; init; } = new();

    /// <summary>
    /// Phase 43 / REPORT-03: per-entry outcomes — one <see cref="EntryOutcome"/> per dispatched
    /// manifest entry, plus optional <c>Skipped</c> outcomes (providerFilter exclusion) and
    /// optional run-level synthetic outcomes (strict-mode escalation). Replaces
    /// <see cref="DeserializeResults"/> as the canonical source of truth driving
    /// <see cref="HasErrors"/> per REPORT-04.
    /// </summary>
    public List<EntryOutcome> EntryOutcomes { get; init; } = new();

    public List<string> Errors { get; init; } = new();

    /// <summary>
    /// Stale files deleted by <see cref="ManifestCleaner"/> during post-serialize cleanup
    /// (Phase 37-01 Task 2). Zero when no cleaner was wired or no stale files were found.
    /// </summary>
    public int StaleFilesDeleted { get; init; }

    /// <summary>
    /// Phase 43 / REPORT-04 / SC-3: HasErrors aggregates from
    /// <list type="number">
    /// <item>Run-level <see cref="Errors"/> (e.g. orchestrator-level wiring failures).</item>
    /// <item>Any <see cref="SerializeResults"/> entry with errors.</item>
    /// <item>Any <see cref="EntryOutcomes"/> entry whose status is <see cref="EntryStatus.Failed"/>.</item>
    /// </list>
    /// The <c>DeserializeResults.Any(r =&gt; r.HasErrors)</c> clause is intentionally dropped —
    /// EntryOutcome.From propagates ProviderDeserializeResult.HasErrors into EntryStatus.Failed,
    /// so the new clause covers exactly the same surface plus the orchestrator-level
    /// failure modes (no provider registered, dispatch threw, strict-mode RunLevelError).
    /// </summary>
    public bool HasErrors =>
        Errors.Count > 0 ||
        SerializeResults.Any(r => r.HasErrors) ||
        EntryOutcomes.Any(e => e.Status == EntryStatus.Failed);

    public string Summary
    {
        get
        {
            var parts = new List<string>();

            if (SerializeResults.Count > 0)
            {
                var totalRows = SerializeResults.Sum(r => r.RowsSerialized);
                parts.Add($"Serialized: {totalRows} rows across {SerializeResults.Count} predicates");
            }

            // Phase 43: prefer EntryOutcomes (canonical) once the dispatch loop populates it
            // (Task 6 wires this); fall back to DeserializeResults for the transient state where
            // Task 2 has shipped but Task 6 has not. The else-if branch is removed in Task 6.
            if (EntryOutcomes.Count > 0)
            {
                var created = EntryOutcomes.Sum(o => o.Counts.Created);
                var updated = EntryOutcomes.Sum(o => o.Counts.Updated);
                var skipped = EntryOutcomes.Sum(o => o.Counts.Skipped);
                var failed = EntryOutcomes.Sum(o => o.Counts.Failed);
                parts.Add($"Deserialized: {created} created, {updated} updated, {skipped} skipped, {failed} failed across {EntryOutcomes.Count} entries");
            }
            else if (DeserializeResults.Count > 0)
            {
                // Transient fallback — populated state lives in DeserializeResults until Task 6
                // wires EntryOutcomes. Removed in Task 6.
                var created = DeserializeResults.Sum(r => r.Created);
                var updated = DeserializeResults.Sum(r => r.Updated);
                var skipped = DeserializeResults.Sum(r => r.Skipped);
                var failed = DeserializeResults.Sum(r => r.Failed);
                parts.Add($"Deserialized: {created} created, {updated} updated, {skipped} skipped, {failed} failed across {DeserializeResults.Count} predicates");
            }

            if (Errors.Count > 0)
                parts.Add($"Errors: {Errors.Count}");

            return string.Join(". ", parts);
        }
    }
}
