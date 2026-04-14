using DynamicWeb.Serializer.AdminUI.Models;
using DynamicWeb.Serializer.Configuration;
using Dynamicweb.CoreUI.Data;
using Dynamicweb.CoreUI.Lists;

namespace DynamicWeb.Serializer.AdminUI.Queries;

public sealed class XmlTypeListQuery : DataQueryModelBase<DataListViewModel<XmlTypeListModel>>
{
    public override DataListViewModel<XmlTypeListModel>? GetModel()
    {
        var configPath = ConfigPathResolver.FindConfigFile();
        if (configPath == null)
            return new DataListViewModel<XmlTypeListModel>();

        var config = ConfigLoader.Load(configPath);
        var items = config.ExcludeXmlElementsByType
            .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kv => new XmlTypeListModel
            {
                TypeName = kv.Key,
                ExcludedElementCount = kv.Value.Count
            });

        return new DataListViewModel<XmlTypeListModel>
        {
            Data = items,
            TotalCount = config.ExcludeXmlElementsByType.Count
        };
    }
}
