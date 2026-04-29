using Dynamicweb.Content;
using DynamicWeb.Serializer.AdminUI.Models;
using DynamicWeb.Serializer.Configuration;
using Dynamicweb.CoreUI.Data;
using Dynamicweb.CoreUI.Lists;

namespace DynamicWeb.Serializer.AdminUI.Queries;

/// <summary>
/// Lists all predicates from the flat config (Phase 40 D-01). Each row carries its own Mode.
/// </summary>
public sealed class PredicateListQuery : DataQueryModelBase<DataListViewModel<PredicateListModel>>
{
    public override DataListViewModel<PredicateListModel>? GetModel()
    {
        var configPath = ConfigPathResolver.FindConfigFile();
        if (configPath == null)
            return new DataListViewModel<PredicateListModel>();

        var config = ConfigLoader.Load(configPath);
        var predicates = config.Predicates;
        var items = predicates.Select((p, i) =>
        {
            string type = p.ProviderType == "SqlTable" ? "SQL Table" : "Content";
            string target = p.ProviderType == "SqlTable"
                ? p.Table ?? "(no table)"
                : $"{(Services.Areas?.GetArea(p.AreaId)?.Name ?? $"Area {p.AreaId}")}: {p.Path}";
            return new PredicateListModel
            {
                Index = i,
                Mode = p.Mode,
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
