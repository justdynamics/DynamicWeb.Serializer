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
            return new PredicateEditModel(); // new-predicate flow; Mode default = Deploy

        var configPath = ConfigPathResolver.FindConfigFile();
        if (configPath == null) return null;

        var config = ConfigLoader.Load(configPath);
        if (Index >= config.Predicates.Count) return null;

        var pred = config.Predicates[Index];
        return new PredicateEditModel
        {
            Index = Index,
            Mode = pred.Mode,  // Phase 40 D-01: predicate's own Mode
            Name = pred.Name,
            ProviderType = pred.ProviderType,
            AreaId = pred.AreaId,
            PageId = pred.PageId,
            Excludes = string.Join("\n", pred.Excludes),
            Table = pred.Table ?? string.Empty,
            NameColumn = pred.NameColumn ?? string.Empty,
            CompareColumns = pred.CompareColumns ?? string.Empty,
            ServiceCaches = string.Join("\n", pred.ServiceCaches),
            ExcludeFields = string.Join("\n", pred.ExcludeFields),
            XmlColumns = string.Join("\n", pred.XmlColumns),
            ExcludeXmlElements = string.Join("\n", pred.ExcludeXmlElements),
            ExcludeAreaColumns = string.Join("\n", pred.ExcludeAreaColumns),
            WhereClause = pred.Where ?? string.Empty,
            IncludeFields = string.Join("\n", pred.IncludeFields),
            ResolveLinksInColumns = string.Join("\n", pred.ResolveLinksInColumns)
        };
    }
}
