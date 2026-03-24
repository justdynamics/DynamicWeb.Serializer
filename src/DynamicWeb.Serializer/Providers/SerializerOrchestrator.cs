using DynamicWeb.Serializer.Models;
using DynamicWeb.Serializer.Providers.SqlTable;

namespace DynamicWeb.Serializer.Providers;

/// <summary>
/// Central dispatch: iterates predicates, resolves providers via ProviderRegistry,
/// validates each predicate, and aggregates results across all providers.
/// Supports FK-ordered deserialization and per-predicate cache invalidation.
/// </summary>
public class SerializerOrchestrator
{
    private readonly ProviderRegistry _registry;
    private readonly FkDependencyResolver? _fkResolver;
    private readonly CacheInvalidator? _cacheInvalidator;

    public SerializerOrchestrator(
        ProviderRegistry registry,
        FkDependencyResolver? fkResolver = null,
        CacheInvalidator? cacheInvalidator = null)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _fkResolver = fkResolver;
        _cacheInvalidator = cacheInvalidator;
    }

    /// <summary>
    /// Serialize all predicates, optionally filtered by provider type.
    /// Unknown provider types and failed validations are logged and skipped.
    /// Note: FK ordering is NOT applied to serialization (order doesn't matter for reads).
    /// </summary>
    public OrchestratorResult SerializeAll(
        List<ProviderPredicateDefinition> predicates,
        string outputRoot,
        Action<string>? log = null,
        string? providerFilter = null)
    {
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
    /// Deserialize all predicates, optionally filtered by provider type.
    /// SqlTable predicates are reordered by FK dependency (parents first, children last).
    /// Cache invalidation runs after each successful predicate deserialize (skipped during dry-run).
    /// Unknown provider types and failed validations are logged and skipped.
    /// </summary>
    public OrchestratorResult DeserializeAll(
        List<ProviderPredicateDefinition> predicates,
        string inputRoot,
        Action<string>? log = null,
        bool isDryRun = false,
        string? providerFilter = null)
    {
        var results = new List<ProviderDeserializeResult>();
        var errors = new List<string>();

        // FK ordering: sort SqlTable predicates by dependency order (parents first, children last)
        // per D-04, D-05. Content and other predicates are unaffected.
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

                // Build a lookup: table name -> position in FK order
                var orderIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < orderedTables.Count; i++)
                    orderIndex[orderedTables[i]] = i;

                // Reorder: non-SqlTable predicates keep original position (front),
                // SqlTable predicates are sorted by FK dependency order (after non-SqlTable)
                var nonSqlPredicates = predicates
                    .Where(p => !string.Equals(p.ProviderType, "SqlTable", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                var sortedSqlPredicates = sqlTablePredicates
                    .OrderBy(p => orderIndex.TryGetValue(p.Table ?? "", out var idx) ? idx : int.MaxValue)
                    .ToList();

                predicates = nonSqlPredicates.Concat(sortedSqlPredicates).ToList();

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

            var result = provider.Deserialize(predicate, inputRoot, log, isDryRun);
            results.Add(result);

            // Cache invalidation: clear configured service caches after successful deserialize (per D-08, D-09)
            // Skip during dry-run (no data was actually written)
            if (!isDryRun && _cacheInvalidator != null && predicate.ServiceCaches.Count > 0 && !result.HasErrors)
            {
                try
                {
                    _cacheInvalidator.InvalidateCaches(predicate.ServiceCaches, log);
                }
                catch (Exception ex)
                {
                    log?.Invoke($"WARNING: Cache invalidation failed for predicate '{predicate.Name}': {ex.Message}");
                    // Don't fail the overall operation — cache invalidation is best-effort
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
