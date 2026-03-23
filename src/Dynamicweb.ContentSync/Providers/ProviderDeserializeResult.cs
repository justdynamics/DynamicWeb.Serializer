namespace Dynamicweb.ContentSync.Providers;

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

    public bool HasErrors => Failed > 0 || Errors.Count > 0;

    public string Summary =>
        $"{TableName}: {Created} created, {Updated} updated, " +
        $"{Skipped} skipped, {Failed} failed.";
}
