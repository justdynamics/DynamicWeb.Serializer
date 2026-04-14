using DynamicWeb.Serializer.AdminUI.Infrastructure;
using DynamicWeb.Serializer.AdminUI.Models;
using DynamicWeb.Serializer.Configuration;
using DynamicWeb.Serializer.Providers.SqlTable;
using Dynamicweb.CoreUI.Data;

namespace DynamicWeb.Serializer.AdminUI.Queries;

public sealed class XmlTypeByNameQuery : DataQueryIdentifiableModelBase<XmlTypeEditModel, string>
{
    public string TypeName { get; set; } = string.Empty;

    protected override void SetKey(string key)
    {
        TypeName = key;
    }

    public override XmlTypeEditModel? GetModel()
    {
        if (string.IsNullOrWhiteSpace(TypeName))
            return null;

        var configPath = ConfigPathResolver.FindConfigFile();
        if (configPath == null) return null;

        var config = ConfigLoader.Load(configPath);
        if (!config.ExcludeXmlElementsByType.TryGetValue(TypeName, out var excludedElements))
            return null;

        return new XmlTypeEditModel
        {
            TypeName = TypeName,
            ExcludedElements = string.Join("\n", excludedElements)
        };
    }
}
