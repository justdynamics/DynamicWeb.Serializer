using DynamicWeb.Serializer.Configuration;
using Dynamicweb.CoreUI.Data;

namespace DynamicWeb.Serializer.AdminUI.Models;

public sealed class PredicateListModel : DataViewModelBase
{
    public int Index { get; set; }

    /// <summary>
    /// Which ModeConfig this predicate lives under (Phase 37-01 D-02). Set by
    /// <see cref="Queries.PredicateListQuery"/>; used by
    /// <see cref="Tree.PredicateNavigationNodePathProvider"/> to terminate the navigation path
    /// at the correct per-mode predicate-group node.
    /// </summary>
    public DeploymentMode Mode { get; set; } = DeploymentMode.Deploy;

    [ConfigurableProperty("Name")]
    public string Name { get; set; } = string.Empty;

    [ConfigurableProperty("Type")]
    public string Type { get; set; } = string.Empty;

    [ConfigurableProperty("Target")]
    public string Target { get; set; } = string.Empty;
}
