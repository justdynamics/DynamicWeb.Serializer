using DynamicWeb.Serializer.Infrastructure;

namespace DynamicWeb.Serializer.Providers;

/// <summary>
/// Result of a provider serialization operation (DB to disk).
/// </summary>
public record SerializeResult
{
    public int RowsSerialized { get; init; }
    public string TableName { get; init; } = "";
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Absolute paths of every file this predicate emitted during the run.
    /// Aggregated by <see cref="SerializerOrchestrator.SerializeAll(System.Collections.Generic.List{Models.ProviderPredicateDefinition}, string, Configuration.DeploymentMode, Configuration.ConflictStrategy, System.Action{string}?, string?)"/>
    /// and handed to <see cref="DynamicWeb.Serializer.Infrastructure.ManifestWriter"/> + the
    /// <see cref="DynamicWeb.Serializer.Infrastructure.ManifestCleaner"/> post-run (Phase 37-01 Task 2).
    /// </summary>
    public IReadOnlyList<string> WrittenFiles { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Phase 42-03 / PROVIDER-04: the manifest entry produced by this serialize call. Null on
    /// validation failure / exception (the per-provider Serialize body returns early before
    /// BuildManifestEntry runs in those cases). The orchestrator collects non-null entries
    /// across all providers and hands them to <see cref="Infrastructure.ManifestWriter"/>.
    /// </summary>
    public ManifestEntry? Entry { get; init; }

    public bool HasErrors => Errors.Count > 0;

    public string Summary =>
        $"{TableName}: {RowsSerialized} rows serialized.";
}
