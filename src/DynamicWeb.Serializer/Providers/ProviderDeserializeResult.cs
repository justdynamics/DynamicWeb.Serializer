namespace DynamicWeb.Serializer.Providers;

/// <summary>
/// Result of a provider deserialization operation (disk to DB).
/// Separate from Serialization.DeserializeResult which is content-specific.
/// </summary>
public record ProviderDeserializeResult
{
    public int Created { get; init; }
    public int Updated { get; init; }
    public int Skipped { get; init; }
    public int Failed { get; init; }
    public string TableName { get; init; } = "";
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Phase 37-05 / LINK-02 pass 2: populated by ContentProvider after a successful
    /// content deserialize. Maps source-environment page IDs to the newly-assigned
    /// target-environment page IDs. The orchestrator aggregates maps across all
    /// Content predicates, then threads the combined map into SqlTable predicates
    /// that opt in via <see cref="Models.ProviderPredicateDefinition.ResolveLinksInColumns"/>.
    /// Null for non-Content providers or dry-run / failed runs.
    /// </summary>
    public IReadOnlyDictionary<int, int>? SourceToTargetPageMap { get; init; }

    public bool HasErrors => Failed > 0 || Errors.Count > 0;

    public string Summary =>
        $"{TableName}: {Created} created, {Updated} updated, " +
        $"{Skipped} skipped, {Failed} failed.";
}
