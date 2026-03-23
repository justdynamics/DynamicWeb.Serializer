namespace Dynamicweb.ContentSync.Providers;

/// <summary>
/// Result of a provider serialization operation (DB to disk).
/// </summary>
public record SerializeResult
{
    public int RowsSerialized { get; init; }
    public string TableName { get; init; } = "";
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    public bool HasErrors => Errors.Count > 0;

    public string Summary =>
        $"{TableName}: {RowsSerialized} rows serialized.";
}
