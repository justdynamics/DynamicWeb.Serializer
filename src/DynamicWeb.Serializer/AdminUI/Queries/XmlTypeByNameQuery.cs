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

    /// <summary>
    /// Which <see cref="DeploymentMode"/> subtree this edit screen was opened under (Phase 37-01.1).
    /// The lookup in <see cref="ModeConfig.ExcludeXmlElementsByType"/> is scoped to this mode.
    /// </summary>
    public DeploymentMode Mode { get; set; } = DeploymentMode.Deploy;

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
        var modeConfig = config.GetMode(Mode);
        if (!modeConfig.ExcludeXmlElementsByType.TryGetValue(TypeName, out var excludedElements))
            return null;

        return new XmlTypeEditModel
        {
            TypeName = TypeName,
            Mode = Mode,
            ExcludedElements = string.Join("\n", excludedElements)
        };
    }
}
