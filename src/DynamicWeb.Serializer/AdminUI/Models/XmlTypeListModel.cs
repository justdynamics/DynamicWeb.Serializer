using DynamicWeb.Serializer.Configuration;
using Dynamicweb.CoreUI.Data;

namespace DynamicWeb.Serializer.AdminUI.Models;

public sealed class XmlTypeListModel : DataViewModelBase
{
    /// <summary>
    /// Which <see cref="DeploymentMode"/> subtree this list is rendered under (Phase 37-01.1).
    /// </summary>
    public DeploymentMode Mode { get; set; } = DeploymentMode.Deploy;

    [ConfigurableProperty("Type Name")]
    public string TypeName { get; set; } = string.Empty;

    [ConfigurableProperty("Excluded Elements")]
    public int ExcludedElementCount { get; set; }
}
