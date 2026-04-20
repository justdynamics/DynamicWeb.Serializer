using Dynamicweb.Content;
using DynamicWeb.Serializer.AdminUI.Models;
using DynamicWeb.Serializer.Configuration;
using Dynamicweb.CoreUI.Data;
using Dynamicweb.CoreUI.Lists;

namespace DynamicWeb.Serializer.AdminUI.Queries;

/// <summary>
/// Lists predicates for one <see cref="DeploymentMode"/> (Phase 37-01 D-02). The tree query-with
/// clause supplies <see cref="Mode"/>; the list screen uses it to pick the matching ModeConfig
/// and to title itself "Deploy Predicates" / "Seed Predicates".
/// </summary>
public sealed class PredicateListQuery : DataQueryModelBase<DataListViewModel<PredicateListModel>>
{
    public DeploymentMode Mode { get; set; } = DeploymentMode.Deploy;

    public override DataListViewModel<PredicateListModel>? GetModel()
    {
        var configPath = ConfigPathResolver.FindConfigFile();
        if (configPath == null)
            return new DataListViewModel<PredicateListModel>();

        var config = ConfigLoader.Load(configPath);
        var predicates = config.GetMode(Mode).Predicates;
        var mode = Mode;
        var items = predicates.Select((p, i) =>
        {
            string type = p.ProviderType == "SqlTable" ? "SQL Table" : "Content";
            string target = p.ProviderType == "SqlTable"
                ? p.Table ?? "(no table)"
                : $"{(Services.Areas?.GetArea(p.AreaId)?.Name ?? $"Area {p.AreaId}")}: {p.Path}";

            return new PredicateListModel
            {
                Index = i,
                Mode = mode,
                Name = p.Name,
                Type = type,
                Target = target
            };
        });

        return new DataListViewModel<PredicateListModel>
        {
            Data = items,
            TotalCount = predicates.Count
        };
    }
}
