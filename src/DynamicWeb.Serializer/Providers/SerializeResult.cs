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

    public bool HasErrors => Errors.Count > 0;

    public string Summary =>
        $"{TableName}: {RowsSerialized} rows serialized.";
}
