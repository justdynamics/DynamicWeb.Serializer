using System.Text.Json;

namespace Dynamicweb.ContentSync.Configuration;

public static class ConfigLoader
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static SyncConfiguration Load(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Configuration file not found: '{filePath}'", filePath);

        var json = File.ReadAllText(filePath);

        // Deserialize to a raw model first (without required constraints) so we can validate with clear messages
        var raw = JsonSerializer.Deserialize<RawSyncConfiguration>(json, _jsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize configuration file — result was null.");

        Validate(raw);

        if (!Directory.Exists(raw.OutputDirectory))
        {
            Console.Error.WriteLine(
                $"[ContentSync] Warning: OutputDirectory '{raw.OutputDirectory}' does not exist. " +
                "Serialization will create it; deserialization requires it to exist.");
        }

        // Build the validated model
        return new SyncConfiguration
        {
            OutputDirectory = raw.OutputDirectory!,
            LogLevel = string.IsNullOrWhiteSpace(raw.LogLevel) ? "info" : raw.LogLevel,
            DryRun = raw.DryRun ?? false,
            ConflictStrategy = ParseConflictStrategy(raw.ConflictStrategy),
            Predicates = raw.Predicates!.Select(BuildPredicate).ToList()
        };
    }

    private static void Validate(RawSyncConfiguration raw)
    {
        if (string.IsNullOrWhiteSpace(raw.OutputDirectory))
            throw new InvalidOperationException("Configuration is invalid: 'outputDirectory' is required and must not be empty.");

        if (raw.Predicates is null || raw.Predicates.Count == 0)
            throw new InvalidOperationException("Configuration is invalid: 'predicates' is required and must contain at least one entry.");

        for (var i = 0; i < raw.Predicates.Count; i++)
        {
            var p = raw.Predicates[i];
            if (string.IsNullOrWhiteSpace(p.Name))
                throw new InvalidOperationException($"Configuration is invalid: predicate[{i}] is missing required field 'name'.");
            if (string.IsNullOrWhiteSpace(p.Path))
                throw new InvalidOperationException($"Configuration is invalid: predicate[{i}] is missing required field 'path'.");
            if (p.AreaId <= 0)
                throw new InvalidOperationException($"Configuration is invalid: predicate[{i}] is missing required field 'areaId' (must be > 0).");
        }
    }

    private static ConflictStrategy ParseConflictStrategy(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return ConflictStrategy.SourceWins;
        if (string.Equals(value, "source-wins", StringComparison.OrdinalIgnoreCase))
            return ConflictStrategy.SourceWins;
        return ConflictStrategy.SourceWins; // Unknown values default to source-wins
    }

    private static PredicateDefinition BuildPredicate(RawPredicateDefinition raw) => new()
    {
        Name = raw.Name!,
        Path = raw.Path!,
        AreaId = raw.AreaId,
        Excludes = raw.Excludes ?? new List<string>()
    };

    // Raw (nullable) models for deserialization — no required constraints so we can produce clear validation errors
    private sealed class RawSyncConfiguration
    {
        public string? OutputDirectory { get; set; }
        public string? LogLevel { get; set; }
        public bool? DryRun { get; set; }
        public string? ConflictStrategy { get; set; }
        public List<RawPredicateDefinition>? Predicates { get; set; }
    }

    private sealed class RawPredicateDefinition
    {
        public string? Name { get; set; }
        public string? Path { get; set; }
        public int AreaId { get; set; }
        public List<string>? Excludes { get; set; }
    }
}
