using Truvio.Commerce.Serializer.AdminUI.Models;
using Truvio.Commerce.Serializer.Configuration;
using Truvio.Commerce.Serializer.Models;
using Truvio.Commerce.Serializer.Reporting;
using Dynamicweb.CoreUI.Data;

namespace Truvio.Commerce.Serializer.AdminUI.Queries;

public sealed class SerializerSettingsQuery : DataQueryModelBase<SerializerSettingsModel>
{
    /// <summary>Coverage counting walks the page tree — skip with a note above this size.</summary>
    private const int CoveragePageCap = 2000;

    public override SerializerSettingsModel? GetModel()
    {
        var configPath = ConfigPathResolver.FindConfigFile();
        if (configPath == null)
            return new SerializerSettingsModel { NeedsSetup = true };

        var config = ConfigLoader.Load(configPath);

        var relativePath = configPath;
        var wwwrootMarker = Path.DirectorySeparatorChar + "wwwroot" + Path.DirectorySeparatorChar;
        var idx = configPath.IndexOf(wwwrootMarker, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
            relativePath = configPath[(idx + wwwrootMarker.Length)..];

        var deployCount = config.Predicates.Count(p => p.Mode == DeploymentMode.Deploy);
        var seedCount = config.Predicates.Count(p => p.Mode == DeploymentMode.Seed);

        return new SerializerSettingsModel
        {
            OutputDirectory = config.OutputDirectory,
            DeployOutputSubfolder = config.DeployOutputSubfolder,
            SeedOutputSubfolder = config.SeedOutputSubfolder,
            ShowSeedIndicators = config.ShowSeedIndicators,
            ShowDeployIndicators = config.ShowDeployIndicators,
            ConfigFilePath = relativePath,
            ItemTypeExcludesSummary = SummarizeExcludes(config.ExcludeFieldsByItemType,
                "field", "No per-item-type field excludes configured — every field of every item type syncs."),
            XmlExcludesSummary = SummarizeExcludes(config.ExcludeXmlElementsByType,
                "setting", "No embedded-XML excludes configured — module settings and provider parameters sync in full."),
            PredicatesSummary = (deployCount + seedCount) == 0
                ? "No predicates configured. Nothing will be synced."
                : $"{deployCount} deploy predicate(s), {seedCount} seed predicate(s) configured. Manage via the Predicates sub-node.",
            LastRunsSummary = BuildLastRunsSummary(configPath),
            CoverageSummary = BuildCoverageSummary(config),
            NeedsSetup = config.Predicates.Count == 0
        };
    }

    /// <summary>
    /// Compact inventory of a by-type exclusion dict: every type with a non-empty list and
    /// its count, e.g. "eCom_CartV2 (20 settings), UserAuthentication (6 settings)".
    /// Types discovered with empty lists (nothing excluded) are reported as a tail count.
    /// </summary>
    private static string SummarizeExcludes(
        IReadOnlyDictionary<string, List<string>> dict, string noun, string emptyMessage)
    {
        var withExcludes = dict
            .Where(kv => kv.Value.Count > 0)
            .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kv => $"{kv.Key} ({kv.Value.Count} {(kv.Value.Count == 1 ? noun : noun + "s")})")
            .ToList();

        if (withExcludes.Count == 0)
            return emptyMessage;

        var emptyCount = dict.Count(kv => kv.Value.Count == 0);
        var tail = emptyCount > 0 ? $" {emptyCount} more type(s) known with nothing excluded." : "";
        return $"These stay local per environment: {string.Join(", ", withExcludes)}.{tail}";
    }

    /// <summary>"Last deploy received: … · Last seed received: …" from the run logs.</summary>
    private static string BuildLastRunsSummary(string configPath)
    {
        try
        {
            var logDir = LastRunResolver.GetLogDir(configPath);
            return $"{Format(LastRunResolver.FindLastReceived(logDir, "deploy"), "Last deploy received")} · " +
                   Format(LastRunResolver.FindLastReceived(logDir, "seed"), "Last seed received");
        }
        catch
        {
            return "";
        }

        static string Format(LogFileSummary? summary, string label) => summary is null
            ? $"{label}: never"
            : $"{label}: {summary.Timestamp.ToLocalTime():dd MMM yyyy HH:mm} " +
              $"(created {summary.TotalCreated}, updated {summary.TotalUpdated}, failed {summary.TotalFailed})";
    }

    /// <summary>
    /// One-line coverage picture: per content area, how many pages deploy / seed / are
    /// unmanaged; plus SqlTable predicate counts per mode. Page counting walks the live
    /// tree through the same coverage evaluators as the tree icons — best-effort, and
    /// skipped with a note above <see cref="CoveragePageCap"/> pages.
    /// </summary>
    private static string BuildCoverageSummary(SerializerConfiguration config)
    {
        try
        {
            var contentPredicates = config.Predicates
                .Where(p => string.Equals(p.ProviderType, "Content", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var sqlDeploy = config.Predicates.Count(p =>
                string.Equals(p.ProviderType, "SqlTable", StringComparison.OrdinalIgnoreCase) && p.Mode == DeploymentMode.Deploy);
            var sqlSeed = config.Predicates.Count(p =>
                string.Equals(p.ProviderType, "SqlTable", StringComparison.OrdinalIgnoreCase) && p.Mode == DeploymentMode.Seed);

            var parts = new List<string>();

            foreach (var areaId in contentPredicates.Select(p => p.AreaId).Where(id => id > 0).Distinct().OrderBy(id => id))
            {
                var deployEval = new ContentCoverageEvaluator(
                    contentPredicates.Where(p => p.Mode == DeploymentMode.Deploy && p.AreaId == areaId));
                var seedEval = new ContentCoverageEvaluator(
                    contentPredicates.Where(p => p.Mode == DeploymentMode.Seed && p.AreaId == areaId), "seed");

                int deployPages = 0, seedPages = 0, unmanaged = 0, total = 0;
                var capped = false;

                void Walk(Dynamicweb.Content.Page page, string path)
                {
                    if (capped) return;
                    if (++total > CoveragePageCap) { capped = true; return; }

                    if (deployEval.GetManagingPredicateNames(path, areaId).Count > 0)
                        deployPages++;
                    else if (seedEval.GetManagingPredicateNames(path, areaId).Count > 0)
                        seedPages++;
                    else
                        unmanaged++;

                    foreach (var child in Dynamicweb.Content.Services.Pages.GetPagesByParentID(page.ID))
                        Walk(child, path + "/" + child.MenuText);
                }

                foreach (var root in Dynamicweb.Content.Services.Pages.GetRootPagesForArea(areaId))
                    Walk(root, "/" + root.MenuText);

                parts.Add(capped
                    ? $"Area {areaId}: more than {CoveragePageCap} pages — counts skipped"
                    : $"Area {areaId}: {deployPages} pages deploy, {seedPages} seed, {unmanaged} unmanaged");
            }

            parts.Add($"Tables: {sqlDeploy} deploy, {sqlSeed} seed");
            return string.Join(" · ", parts) + ".";
        }
        catch
        {
            // Coverage is informational; never break the settings screen over it.
            return "";
        }
    }
}
