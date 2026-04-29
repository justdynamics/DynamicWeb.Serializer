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

        var relativePath = configPath;
        var wwwrootMarker = Path.DirectorySeparatorChar + "wwwroot" + Path.DirectorySeparatorChar;
        var idx = configPath.IndexOf(wwwrootMarker, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
            relativePath = configPath[(idx + wwwrootMarker.Length)..];

        // Phase 40 D-01: count Deploy + Seed predicates from the flat list.
        var deployCount = config.Predicates.Count(p => p.Mode == DeploymentMode.Deploy);
        var seedCount = config.Predicates.Count(p => p.Mode == DeploymentMode.Seed);

        return new SerializerSettingsModel
        {
            OutputDirectory = config.OutputDirectory,
            LogLevel = config.LogLevel,
            DryRun = config.DryRun,
            StrictMode = config.StrictMode,
            ConflictStrategy = "source-wins", // hardcoded; field is read-only in v0.5.x per D-02
            ConfigFilePath = relativePath,
            PredicatesSummary = (deployCount + seedCount) == 0
                ? "No predicates configured. Nothing will be synced."
                : $"{deployCount} deploy predicate(s), {seedCount} seed predicate(s) configured. Manage via the Predicates sub-node."
        };
    }
}
