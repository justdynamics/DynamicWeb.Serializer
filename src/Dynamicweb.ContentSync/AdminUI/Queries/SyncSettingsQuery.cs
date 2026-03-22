using Dynamicweb.ContentSync.AdminUI.Models;
using Dynamicweb.ContentSync.Configuration;
using Dynamicweb.CoreUI.Data;

namespace Dynamicweb.ContentSync.AdminUI.Queries;

public sealed class SyncSettingsQuery : DataQueryModelBase<SyncSettingsModel>
{
    public override SyncSettingsModel? GetModel()
    {
        var configPath = ConfigPathResolver.FindConfigFile();
        if (configPath == null)
            return new SyncSettingsModel();

        var config = ConfigLoader.Load(configPath);

        // Make config path relative to wwwroot
        var relativePath = configPath;
        var wwwrootMarker = Path.DirectorySeparatorChar + "wwwroot" + Path.DirectorySeparatorChar;
        var idx = configPath.IndexOf(wwwrootMarker, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
            relativePath = configPath[(idx + wwwrootMarker.Length)..];

        var predicateCount = config.Predicates.Count;

        return new SyncSettingsModel
        {
            OutputDirectory = config.OutputDirectory,
            ExportDirectory = config.ExportDirectory ?? string.Empty,
            LogLevel = config.LogLevel,
            DryRun = config.DryRun,
            ConflictStrategy = config.ConflictStrategy switch
            {
                Configuration.ConflictStrategy.SourceWins => "source-wins",
                _ => "source-wins"
            },
            ConfigFilePath = relativePath,
            PredicatesSummary = predicateCount == 0
                ? "No predicates configured. Nothing will be synced."
                : $"{predicateCount} predicate(s) configured. Manage via the Predicates sub-node."
        };
    }
}
