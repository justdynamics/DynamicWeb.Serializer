using Dynamicweb.Content;
using DynamicWeb.Serializer.AdminUI.Models;
using DynamicWeb.Serializer.Configuration;
using Dynamicweb.CoreUI.Data;
using Dynamicweb.CoreUI.Lists;

namespace DynamicWeb.Serializer.AdminUI.Queries;

public sealed class PredicateListQuery : DataQueryModelBase<DataListViewModel<PredicateListModel>>
{
    public override DataListViewModel<PredicateListModel>? GetModel()
    {
        var configPath = ConfigPathResolver.FindConfigFile();
        if (configPath == null)
            return new DataListViewModel<PredicateListModel>();

        var config = ConfigLoader.Load(configPath);
        var items = config.Predicates.Select((p, i) => new PredicateListModel
        {
            Index = i,
            Name = p.Name,
            Path = p.Path,
            AreaName = Services.Areas.GetArea(p.AreaId)?.Name ?? $"Area {p.AreaId}"
        });

        return new DataListViewModel<PredicateListModel>
        {
            Data = items,
            TotalCount = config.Predicates.Count
        };
    }
}
