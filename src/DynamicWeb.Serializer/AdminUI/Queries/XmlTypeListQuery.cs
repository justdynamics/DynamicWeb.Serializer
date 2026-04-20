using DynamicWeb.Serializer.AdminUI.Models;
using DynamicWeb.Serializer.Configuration;
using Dynamicweb.CoreUI.Data;
using Dynamicweb.CoreUI.Lists;

namespace DynamicWeb.Serializer.AdminUI.Queries;

public sealed class XmlTypeListQuery : DataQueryModelBase<DataListViewModel<XmlTypeListModel>>
{
    /// <summary>Optional config path override for tests -- bypasses ConfigPathResolver.</summary>
    public string? ConfigPath { get; set; }

    /// <summary>
    /// Which <see cref="DeploymentMode"/>'s <see cref="ModeConfig.ExcludeXmlElementsByType"/> is
    /// enumerated (Phase 37-01.1). Seed and Deploy XML-type subtrees list only their own keys.
    /// </summary>
    public DeploymentMode Mode { get; set; } = DeploymentMode.Deploy;

    public override DataListViewModel<XmlTypeListModel>? GetModel()
    {
        var configPath = ConfigPath ?? ConfigPathResolver.FindConfigFile();
        if (configPath == null)
            return new DataListViewModel<XmlTypeListModel>();

        var config = ConfigLoader.Load(configPath);
        var dict = config.GetMode(Mode).ExcludeXmlElementsByType;
        var mode = Mode;
        var items = dict
            .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kv => new XmlTypeListModel
            {
                Mode = mode,
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
