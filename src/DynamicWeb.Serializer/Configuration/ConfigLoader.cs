using System.Text.Json;
using DynamicWeb.Serializer.Models;

namespace DynamicWeb.Serializer.Configuration;

public static class ConfigLoader
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static SerializerConfiguration Load(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Configuration file not found: '{filePath}'", filePath);

        var json = File.ReadAllText(filePath);

        // Deserialize to a raw model first (without required constraints) so we can validate with clear messages
        var raw = JsonSerializer.Deserialize<RawSerializerConfiguration>(json, _jsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize configuration file — result was null.");

        Validate(raw);

        if (!Directory.Exists(raw.OutputDirectory))
        {
            Console.Error.WriteLine(
                $"[Serializer] Warning: OutputDirectory '{raw.OutputDirectory}' does not exist. " +
                "Serialization will create it; deserialization requires it to exist.");
        }

        // Build the validated model
        return new SerializerConfiguration
        {
            OutputDirectory = raw.OutputDirectory!,
            LogLevel = string.IsNullOrWhiteSpace(raw.LogLevel) ? "info" : raw.LogLevel,
            DryRun = raw.DryRun ?? false,
            ConflictStrategy = ParseConflictStrategy(raw.ConflictStrategy),
            Predicates = raw.Predicates!.Select(BuildPredicate).ToList(),
            ExcludeFieldsByItemType = raw.ExcludeFieldsByItemType ?? new Dictionary<string, List<string>>(),
            ExcludeXmlElementsByType = raw.ExcludeXmlElementsByType ?? new Dictionary<string, List<string>>()
        };
    }

    private static void Validate(RawSerializerConfiguration raw)
    {
        if (string.IsNullOrWhiteSpace(raw.OutputDirectory))
            throw new InvalidOperationException("Configuration is invalid: 'outputDirectory' is required and must not be empty.");

        if (raw.Predicates is null)
            raw.Predicates = new List<RawPredicateDefinition>();

        for (var i = 0; i < raw.Predicates.Count; i++)
        {
            var p = raw.Predicates[i];
            if (string.IsNullOrWhiteSpace(p.Name))
                throw new InvalidOperationException($"Configuration is invalid: predicate[{i}] is missing required field 'name'.");

            // SqlTable predicates don't need path/areaId — they use table/nameColumn instead
            var isContentPredicate = string.IsNullOrEmpty(p.ProviderType) || string.Equals(p.ProviderType, "Content", StringComparison.OrdinalIgnoreCase);
            if (isContentPredicate)
            {
                if (string.IsNullOrWhiteSpace(p.Path))
                    throw new InvalidOperationException($"Configuration is invalid: predicate[{i}] is missing required field 'path'.");
                if (p.AreaId <= 0)
                    throw new InvalidOperationException($"Configuration is invalid: predicate[{i}] is missing required field 'areaId' (must be > 0).");
            }
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

    private static ProviderPredicateDefinition BuildPredicate(RawPredicateDefinition raw) => new()
    {
        Name = raw.Name!,
        ProviderType = string.IsNullOrEmpty(raw.ProviderType) ? "Content" : raw.ProviderType,
        Path = raw.Path ?? "",
        AreaId = raw.AreaId,
        PageId = raw.PageId,
        Excludes = raw.Excludes ?? new List<string>(),
        Table = raw.Table,
        NameColumn = raw.NameColumn,
        CompareColumns = raw.CompareColumns,
        ServiceCaches = raw.ServiceCaches ?? new List<string>(),
        SchemaSync = raw.SchemaSync,
        XmlColumns = raw.XmlColumns ?? new List<string>(),
        ExcludeFields = raw.ExcludeFields ?? new List<string>(),
        ExcludeXmlElements = raw.ExcludeXmlElements ?? new List<string>()
    };

    // Raw (nullable) models for deserialization — no required constraints so we can produce clear validation errors
    private sealed class RawSerializerConfiguration
    {
        public string? OutputDirectory { get; set; }
        // ExportDirectory removed — subfolders derived from OutputDirectory
        public string? LogLevel { get; set; }
        public bool? DryRun { get; set; }
        public string? ConflictStrategy { get; set; }
        public List<RawPredicateDefinition>? Predicates { get; set; }
        public Dictionary<string, List<string>>? ExcludeFieldsByItemType { get; set; }
        public Dictionary<string, List<string>>? ExcludeXmlElementsByType { get; set; }
    }

    private sealed class RawPredicateDefinition
    {
        public string? Name { get; set; }
        public string? ProviderType { get; set; }
        public string? Path { get; set; }
        public int AreaId { get; set; }
        public int PageId { get; set; }
        public List<string>? Excludes { get; set; }
        public string? Table { get; set; }
        public string? NameColumn { get; set; }
        public string? CompareColumns { get; set; }
        public List<string>? ServiceCaches { get; set; }
        public string? SchemaSync { get; set; }
        public List<string>? XmlColumns { get; set; }
        public List<string>? ExcludeFields { get; set; }
        public List<string>? ExcludeXmlElements { get; set; }
    }
}
