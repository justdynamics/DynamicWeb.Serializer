using DynamicWeb.Serializer.Configuration;
using Dynamicweb.CoreUI.Data;

namespace DynamicWeb.Serializer.AdminUI.Models;

public sealed class XmlTypeEditModel : DataViewModelBase, IIdentifiable
{
    public string TypeName { get; set; } = string.Empty;

    /// <summary>
    /// Which <see cref="DeploymentMode"/> this XML type's exclude list lives under (Phase 37-01.1).
    /// Set by <see cref="Queries.XmlTypeByNameQuery"/>; read by
    /// <see cref="Commands.SaveXmlTypeCommand"/> to target the correct
    /// <see cref="ModeConfig.ExcludeXmlElementsByType"/> dictionary.
    /// </summary>
    public DeploymentMode Mode { get; set; } = DeploymentMode.Deploy;

    public string GetId() => TypeName;

    [ConfigurableProperty("Excluded Elements", explanation: "Select XML elements to exclude from serialization for this type")]
    public string ExcludedElements { get; set; } = string.Empty;
}
