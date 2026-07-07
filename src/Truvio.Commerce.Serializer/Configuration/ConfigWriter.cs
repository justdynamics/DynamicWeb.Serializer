using System.Text.Json;
using System.Text.Json.Serialization;
using Truvio.Commerce.Serializer.Models;

namespace Truvio.Commerce.Serializer.Configuration;

/// <summary>
/// Writes <see cref="SerializerConfiguration"/> as a flat JSON document with a
/// per-predicate <c>mode</c> field. Never emits a section-level
/// <c>replace</c> / <c>merge</c> shape. <see cref="ConfigLoader"/> hard-rejects any file
/// containing those keys.
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

        var dto = new PersistedConfiguration
        {
            OutputDirectory = config.OutputDirectory,
            ReplaceOutputSubfolder = config.ReplaceOutputSubfolder,
            MergeOutputSubfolder = config.MergeOutputSubfolder,
            ExcludeFieldsByItemType = config.ExcludeFieldsByItemType.Count > 0 ? config.ExcludeFieldsByItemType : null,
            ExcludeXmlElementsByType = config.ExcludeXmlElementsByType.Count > 0 ? config.ExcludeXmlElementsByType : null,
            ShowMergeIndicators = config.ShowMergeIndicators,
            ShowReplaceIndicators = config.ShowReplaceIndicators,
            Predicates = config.Predicates
        };

        var json = JsonSerializer.Serialize(dto, _jsonOptions);

        var tempPath = filePath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, filePath, overwrite: true);
    }

    private sealed class PersistedConfiguration
    {
        public string OutputDirectory { get; init; } = "";
        public string ReplaceOutputSubfolder { get; init; } = "replace";
        public string MergeOutputSubfolder { get; init; } = "merge";
        public Dictionary<string, List<string>>? ExcludeFieldsByItemType { get; init; }
        public Dictionary<string, List<string>>? ExcludeXmlElementsByType { get; init; }
        public bool ShowMergeIndicators { get; init; }
        public bool ShowReplaceIndicators { get; init; } = true;
        public List<ProviderPredicateDefinition> Predicates { get; init; } = new();
    }
}
