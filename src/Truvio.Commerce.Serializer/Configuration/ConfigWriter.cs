using System.Text.Json;
using System.Text.Json.Serialization;
using Truvio.Commerce.Serializer.Models;

namespace Truvio.Commerce.Serializer.Configuration;

/// <summary>
/// Phase 40: writes <see cref="SerializerConfiguration"/> as a flat JSON document with
/// per-predicate <c>mode</c> field. Never emits the legacy section-level
/// <c>deploy</c> / <c>seed</c> shape. <see cref="ConfigLoader"/> hard-rejects any file
/// containing those keys (D-03, no backcompat).
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
            DeployOutputSubfolder = config.DeployOutputSubfolder,
            SeedOutputSubfolder = config.SeedOutputSubfolder,
            ExcludeFieldsByItemType = config.ExcludeFieldsByItemType.Count > 0 ? config.ExcludeFieldsByItemType : null,
            ExcludeXmlElementsByType = config.ExcludeXmlElementsByType.Count > 0 ? config.ExcludeXmlElementsByType : null,
            ShowSeedIndicators = config.ShowSeedIndicators,
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
        public string DeployOutputSubfolder { get; init; } = "deploy";
        public string SeedOutputSubfolder { get; init; } = "seed";
        public Dictionary<string, List<string>>? ExcludeFieldsByItemType { get; init; }
        public Dictionary<string, List<string>>? ExcludeXmlElementsByType { get; init; }
        public bool ShowSeedIndicators { get; init; }
        public List<ProviderPredicateDefinition> Predicates { get; init; } = new();
    }
}
