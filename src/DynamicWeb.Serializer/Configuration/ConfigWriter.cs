using System.Text.Json;
using System.Text.Json.Serialization;
using DynamicWeb.Serializer.Models;

namespace DynamicWeb.Serializer.Configuration;

/// <summary>
/// Writes <see cref="SerializerConfiguration"/> to disk as camelCase JSON using the
/// Phase 37-01 Deploy/Seed shape. Never emits the legacy flat 'predicates' field —
/// ConfigLoader will migrate old files on next load, and the first subsequent save
/// rewrites them in the new shape.
/// </summary>
public static class ConfigWriter
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static void Save(SerializerConfiguration config, string filePath)
    {
        var directory = Path.GetDirectoryName(filePath)!;
        Directory.CreateDirectory(directory);

        // Map to a disk-shape DTO so serialization doesn't emit pass-through legacy fields
        // and so we can control the ordering of top-level keys for human readability.
        var dto = new PersistedConfiguration
        {
            OutputDirectory = config.OutputDirectory,
            LogLevel = config.LogLevel,
            DryRun = config.DryRun,
            Deploy = ToPersistedMode(config.Deploy),
            Seed = ToPersistedMode(config.Seed)
        };

        var json = JsonSerializer.Serialize(dto, _jsonOptions);

        var tempPath = filePath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, filePath, overwrite: true);
    }

    private static PersistedModeSection ToPersistedMode(ModeConfig mode) => new()
    {
        OutputSubfolder = mode.OutputSubfolder,
        ConflictStrategy = mode.ConflictStrategy,
        Predicates = mode.Predicates,
        ExcludeFieldsByItemType = mode.ExcludeFieldsByItemType.Count > 0 ? mode.ExcludeFieldsByItemType : null,
        ExcludeXmlElementsByType = mode.ExcludeXmlElementsByType.Count > 0 ? mode.ExcludeXmlElementsByType : null
    };

    private sealed class PersistedConfiguration
    {
        public string OutputDirectory { get; init; } = "";
        public string LogLevel { get; init; } = "info";
        public bool DryRun { get; init; }
        public PersistedModeSection Deploy { get; init; } = new();
        public PersistedModeSection Seed { get; init; } = new();
    }

    private sealed class PersistedModeSection
    {
        public string OutputSubfolder { get; init; } = "";
        public ConflictStrategy ConflictStrategy { get; init; }
        public List<ProviderPredicateDefinition> Predicates { get; init; } = new();
        public Dictionary<string, List<string>>? ExcludeFieldsByItemType { get; init; }
        public Dictionary<string, List<string>>? ExcludeXmlElementsByType { get; init; }
    }
}
