using DynamicWeb.Serializer.Configuration;
using DynamicWeb.Serializer.Models;
using DynamicWeb.Serializer.Providers.SqlTable;

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

    public SerializerOrchestrator(
        ProviderRegistry registry,
        FkDependencyResolver? fkResolver = null,
        CacheInvalidator? cacheInvalidator = null,
        EcomGroupFieldSchemaSync? ecomSchemaSync = null)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _fkResolver = fkResolver;
        _cacheInvalidator = cacheInvalidator;
        _ecomSchemaSync = ecomSchemaSync;
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
        SerializeAll(predicates, outputRoot, DeploymentMode.Deploy, ConflictStrategy.SourceWins, log, providerFilter);

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
    /// </summary>
    public OrchestratorResult SerializeAll(
        List<ProviderPredicateDefinition> predicates,
        string outputRoot,
        DeploymentMode mode,
        ConflictStrategy strategy,
        Action<string>? log = null,
        string? providerFilter = null)
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
            var validation = provider.ValidatePredicate(predicate);
            if (!validation.IsValid)
            {
                errors.AddRange(validation.Errors.Select(e => $"{predicate.Name}: {e}"));
                log?.Invoke($"WARNING: Skipping predicate '{predicate.Name}' — validation failed: {string.Join(", ", validation.Errors)}");
                continue;
            }

            var result = provider.Serialize(predicate, outputRoot, log);
            results.Add(result);
        }

        return new OrchestratorResult { SerializeResults = results, Errors = errors };
    }

    /// <summary>
    /// Deserialize all predicates, scoped to the given mode. Under
    /// <see cref="ConflictStrategy.DestinationWins"/> (default for Seed, per D-06), per-predicate
    /// providers receive the strategy via <see cref="ISerializationProvider.Deserialize"/>'s
    /// optional strategy parameter and MUST skip rows/pages whose natural key is already present
    /// on target — SqlTableProvider skips by <c>RowExistsInTarget</c>, ContentProvider skips
    /// by <c>PageUniqueId</c> match. Nested content (paragraphs within existing pages) is
    /// out of scope for 37-01 and follows up in later plans.
    /// </summary>
    public OrchestratorResult DeserializeAll(
        List<ProviderPredicateDefinition> predicates,
        string inputRoot,
        DeploymentMode mode,
        ConflictStrategy strategy,
        Action<string>? log = null,
        bool isDryRun = false,
        string? providerFilter = null)
    {
        log?.Invoke($"=== Mode: {mode} | Strategy: {strategy} ===");

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
            var validation = provider.ValidatePredicate(predicate);
            if (!validation.IsValid)
            {
                errors.AddRange(validation.Errors.Select(e => $"{predicate.Name}: {e}"));
                log?.Invoke($"WARNING: Skipping predicate '{predicate.Name}' — validation failed: {string.Join(", ", validation.Errors)}");
                continue;
            }

            var result = provider.Deserialize(predicate, inputRoot, log, isDryRun, strategy);
            results.Add(result);

            // Cache invalidation: clear configured service caches after successful deserialize.
            if (!isDryRun && predicate.ServiceCaches.Count > 0 && !result.HasErrors)
            {
                if (_cacheInvalidator == null)
                {
                    log?.Invoke($"WARNING: Predicate '{predicate.Name}' declares {predicate.ServiceCaches.Count} service cache(s) but no CacheInvalidator is wired — caches will NOT be cleared");
                }
                else
                {
                    try
                    {
                        _cacheInvalidator.InvalidateCaches(predicate.ServiceCaches, log);
                    }
                    catch (Exception ex)
                    {
                        log?.Invoke($"WARNING: Cache invalidation failed for predicate '{predicate.Name}': {ex.Message}");
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
                    log?.Invoke($"Running schema sync for {predicate.Name}...");
                    _ecomSchemaSync.SyncSchema(log);
                }
                catch (Exception ex)
                {
                    log?.Invoke($"WARNING: Schema sync failed for predicate '{predicate.Name}': {ex.Message}");
                }
            }
        }

        return new OrchestratorResult { DeserializeResults = results, Errors = errors };
    }
}

/// <summary>
/// Aggregated result from orchestrator operations across multiple providers.
/// </summary>
public record OrchestratorResult
{
    public List<SerializeResult> SerializeResults { get; init; } = new();
    public List<ProviderDeserializeResult> DeserializeResults { get; init; } = new();
    public List<string> Errors { get; init; } = new();

    public bool HasErrors =>
        Errors.Count > 0 ||
        SerializeResults.Any(r => r.HasErrors) ||
        DeserializeResults.Any(r => r.HasErrors);

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

            if (DeserializeResults.Count > 0)
            {
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
