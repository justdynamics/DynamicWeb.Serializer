namespace Dynamicweb.ContentSync.Providers.SqlTable;

/// <summary>
/// Structured result for SQL table operations, reporting per-table counts.
/// Modeled after Serialization.DeserializeResult but specific to SQL table operations.
/// </summary>
public record SqlTableResult
{
    public required string TableName { get; init; }
    public int Created { get; init; }
    public int Updated { get; init; }
    public int Skipped { get; init; }
    public int Failed { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    public bool HasErrors => Failed > 0 || Errors.Count > 0;

    public string Summary =>
        $"{TableName}: {Created} added, {Updated} updated, {Skipped} skipped, {Failed} failed.";
}
