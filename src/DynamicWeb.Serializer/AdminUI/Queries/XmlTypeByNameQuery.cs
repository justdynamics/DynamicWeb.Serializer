using DynamicWeb.Serializer.AdminUI.Infrastructure;
using DynamicWeb.Serializer.AdminUI.Models;
using DynamicWeb.Serializer.Configuration;
using DynamicWeb.Serializer.Providers.SqlTable;
using Dynamicweb.CoreUI.Data;

namespace DynamicWeb.Serializer.AdminUI.Queries;

public sealed class XmlTypeByNameQuery : DataQueryIdentifiableModelBase<XmlTypeEditModel, string>
{
    public string TypeName { get; set; } = string.Empty;

    /// <summary>Optional config path override for tests -- bypasses ConfigPathResolver.</summary>
    public string? ConfigPath { get; set; }

    protected override void SetKey(string key)
    {
        TypeName = key;
    }

    public override XmlTypeEditModel? GetModel()
    {
        if (string.IsNullOrWhiteSpace(TypeName))
            return null;

        var configPath = ConfigPath ?? ConfigPathResolver.FindConfigFile();
        if (configPath == null) return null;

        var config = ConfigLoader.Load(configPath);
        // Phase 40 D-04: exclusion dict is top-level, mode-agnostic.
        if (!config.ExcludeXmlElementsByType.TryGetValue(TypeName, out var excludedElements))
            return null;

        return new XmlTypeEditModel
        {
            TypeName = TypeName,
            ExcludedElements = excludedElements.ToList()
        };
    }
}
