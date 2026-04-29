using DynamicWeb.Serializer.AdminUI.Models;
using DynamicWeb.Serializer.Configuration;
using Dynamicweb.CoreUI.Data;
using Dynamicweb.CoreUI.Lists;

namespace DynamicWeb.Serializer.AdminUI.Queries;

public sealed class XmlTypeListQuery : DataQueryModelBase<DataListViewModel<XmlTypeListModel>>
{
    /// <summary>Optional config path override for tests -- bypasses ConfigPathResolver.</summary>
    public string? ConfigPath { get; set; }

    public override DataListViewModel<XmlTypeListModel>? GetModel()
    {
        var configPath = ConfigPathResolver.FindConfigFile();
        if (configPath == null)
            return new DataListViewModel<XmlTypeListModel>();

        var config = ConfigLoader.Load(configPath);
        // Phase 40 D-04: exclusion dict is top-level, mode-agnostic.
        var dict = config.ExcludeXmlElementsByType;
        var items = dict
            .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kv => new XmlTypeListModel
            {
                TypeName = kv.Key,
                ExcludedElementCount = kv.Value.Count
            })
            .ToList();

        return new DataListViewModel<XmlTypeListModel>
        {
            Data = items,
            TotalCount = dict.Count
        };
    }
}
