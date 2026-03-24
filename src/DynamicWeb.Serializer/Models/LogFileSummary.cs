namespace DynamicWeb.Serializer.Models;

public record LogFileSummary
{
    public string Operation { get; init; } = "";
    public DateTime Timestamp { get; init; }
    public bool DryRun { get; init; }
    public List<PredicateSummary> Predicates { get; init; } = new();
    public int TotalCreated { get; init; }
    public int TotalUpdated { get; init; }
    public int TotalSkipped { get; init; }
    public int TotalFailed { get; init; }
    public List<string> Errors { get; init; } = new();
    public List<string> Advice { get; init; } = new();
}

public record PredicateSummary
{
    public string Name { get; init; } = "";
    public string Table { get; init; } = "";
    public int Created { get; init; }
    public int Updated { get; init; }
    public int Skipped { get; init; }
    public int Failed { get; init; }
    public List<string> Errors { get; init; } = new();
}
