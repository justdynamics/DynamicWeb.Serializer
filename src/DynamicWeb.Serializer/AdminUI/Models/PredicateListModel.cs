using DynamicWeb.Serializer.Configuration;
using Dynamicweb.CoreUI.Data;

namespace DynamicWeb.Serializer.AdminUI.Models;

public sealed class PredicateListModel : DataViewModelBase
{
    public int Index { get; set; }

    /// <summary>
    /// Phase 40 D-06: the predicate's own deployment mode. Surfaced in the list view via ModeDisplay
    /// and used by PredicateListScreen to thread the mode into Edit/Delete actions on each row.
    /// </summary>
    public DeploymentMode Mode { get; set; } = DeploymentMode.Deploy;

    [ConfigurableProperty("Mode")]
    public string ModeDisplay => Mode == DeploymentMode.Deploy ? "Deploy" : "Seed";

    [ConfigurableProperty("Name")]
    public string Name { get; set; } = string.Empty;

    [ConfigurableProperty("Type")]
    public string Type { get; set; } = string.Empty;

    [ConfigurableProperty("Target")]
    public string Target { get; set; } = string.Empty;
}
