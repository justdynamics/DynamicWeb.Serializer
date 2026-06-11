using Truvio.Commerce.Serializer.AdminUI.Models;
using Truvio.Commerce.Serializer.Configuration;
using Dynamicweb.CoreUI.Data;

namespace Truvio.Commerce.Serializer.AdminUI.Queries;

public sealed class PredicateByIndexQuery : DataQueryIdentifiableModelBase<PredicateEditModel, int>
{
    public int Index { get; set; } = -1;

    protected override void SetKey(int key)
    {
        // DW framework treats "0" as "no identifier" -- identifiers are 1-based, convert back to 0-based
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
            Mode = pred.Mode.ToString(),  // Phase 41 D-13: string-typed for DW Select binding (was enum). DeploymentMode.ToString() returns "Deploy" / "Seed", matching the Value strings emitted by PredicateEditScreen's Mode Select.
            Name = pred.Name,
            ProviderType = pred.ProviderType,
            AreaId = pred.AreaId,
            PageId = pred.PageId,
            IncludeLanguageLayers = pred.IncludeLanguageLayers,
            // List-typed (SelectMultiDual-bound) properties hydrate as copies; Textarea-bound
            // multi-value properties (ServiceCaches, ExcludeXmlElements) stay newline-joined.
            Excludes = pred.Excludes.ToList(),
            Table = pred.Table ?? string.Empty,
            NameColumn = pred.NameColumn ?? string.Empty,
            CompareColumns = pred.CompareColumns ?? string.Empty,
            ServiceCaches = string.Join("\n", pred.ServiceCaches),
            ExcludeFields = pred.ExcludeFields.ToList(),
            XmlColumns = pred.XmlColumns.ToList(),
            ExcludeXmlElements = string.Join("\n", pred.ExcludeXmlElements),
            ExcludeAreaColumns = pred.ExcludeAreaColumns.ToList(),
            WhereClause = pred.Where ?? string.Empty,
            IncludeFields = pred.IncludeFields.ToList(),
            ResolveLinksInColumns = pred.ResolveLinksInColumns.ToList()
        };
    }
}
