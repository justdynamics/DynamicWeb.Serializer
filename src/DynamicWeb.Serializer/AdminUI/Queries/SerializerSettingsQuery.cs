using DynamicWeb.Serializer.AdminUI.Models;
using DynamicWeb.Serializer.Configuration;
using Dynamicweb.CoreUI.Data;

namespace DynamicWeb.Serializer.AdminUI.Queries;

public sealed class SerializerSettingsQuery : DataQueryModelBase<SerializerSettingsModel>
{
    public override SerializerSettingsModel? GetModel()
    {
        var configPath = ConfigPathResolver.FindConfigFile();
        if (configPath == null)
            return new SerializerSettingsModel();

        var config = ConfigLoader.Load(configPath);

        // Make config path relative to wwwroot
        var relativePath = configPath;
        var wwwrootMarker = Path.DirectorySeparatorChar + "wwwroot" + Path.DirectorySeparatorChar;
        var idx = configPath.IndexOf(wwwrootMarker, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
            relativePath = configPath[(idx + wwwrootMarker.Length)..];

        // Phase 37-01 D-02: count Deploy + Seed predicates separately for the summary.
        var deployCount = config.Deploy.Predicates.Count;
        var seedCount = config.Seed.Predicates.Count;

        return new SerializerSettingsModel
        {
            OutputDirectory = config.OutputDirectory,
            LogLevel = config.LogLevel,
            DryRun = config.DryRun,
            StrictMode = config.StrictMode, // Phase 37-04 STRICT-01
            ConflictStrategy = config.Deploy.ConflictStrategy switch
            {
                Configuration.ConflictStrategy.SourceWins => "source-wins",
                Configuration.ConflictStrategy.DestinationWins => "destination-wins",
                _ => "source-wins"
            },
            ConfigFilePath = relativePath,
            PredicatesSummary = (deployCount + seedCount) == 0
                ? "No predicates configured. Nothing will be synced."
                : $"{deployCount} deploy predicate(s), {seedCount} seed predicate(s) configured. Manage via the Deploy / Seed Predicates sub-nodes."
        };
    }
}
