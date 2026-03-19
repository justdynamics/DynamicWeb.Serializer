namespace Dynamicweb.ContentSync.Serialization;

public record DeserializeResult
{
    public int Created { get; init; }
    public int Updated { get; init; }
    public int Skipped { get; init; }
    public int Failed { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    public bool HasErrors => Failed > 0 || Errors.Count > 0;

    public string Summary =>
        $"Deserialization complete: {Created} created, {Updated} updated, " +
        $"{Skipped} skipped, {Failed} failed.";
}
