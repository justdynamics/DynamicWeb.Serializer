using DynamicWeb.Serializer.AdminUI.Models;
using DynamicWeb.Serializer.Configuration;
using Dynamicweb.CoreUI.Data;

namespace DynamicWeb.Serializer.AdminUI.Queries;

public sealed class PredicateByIndexQuery : DataQueryIdentifiableModelBase<PredicateEditModel, int>
{
    public int Index { get; set; } = -1;

    protected override void SetKey(int key)
    {
        // DW framework treats "0" as "no identifier" — identifiers are 1-based, convert back to 0-based
        Index = key - 1;
    }

    public override PredicateEditModel? GetModel()
    {
        if (Index < 0)
            return new PredicateEditModel();

        var configPath = ConfigPathResolver.FindConfigFile();
        if (configPath == null) return null;

        var config = ConfigLoader.Load(configPath);
        if (Index >= config.Predicates.Count) return null;

        var pred = config.Predicates[Index];
        return new PredicateEditModel
        {
            Index = Index,
            Name = pred.Name,
            ProviderType = pred.ProviderType,
            AreaId = pred.AreaId,
            PageId = pred.PageId,
            Excludes = string.Join("\n", pred.Excludes),
            Table = pred.Table ?? string.Empty,
            NameColumn = pred.NameColumn ?? string.Empty,
            CompareColumns = pred.CompareColumns ?? string.Empty,
            ServiceCaches = string.Join("\n", pred.ServiceCaches)
        };
    }
}
