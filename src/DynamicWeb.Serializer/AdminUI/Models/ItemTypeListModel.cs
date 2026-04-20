using DynamicWeb.Serializer.Configuration;
using Dynamicweb.CoreUI.Data;

namespace DynamicWeb.Serializer.AdminUI.Models;

public sealed class ItemTypeListModel : DataViewModelBase
{
    /// <summary>
    /// Which <see cref="DeploymentMode"/> subtree this list is rendered under (Phase 37-01.1).
    /// Populated by <see cref="Queries.ItemTypeListQuery"/>; used by the path provider to
    /// terminate at the correct per-mode Item Types node in the tree.
    /// </summary>
    public DeploymentMode Mode { get; set; } = DeploymentMode.Deploy;

    [ConfigurableProperty("Item Type")]
    public string SystemName { get; set; } = string.Empty;

    [ConfigurableProperty("Name")]
    public string DisplayName { get; set; } = string.Empty;

    [ConfigurableProperty("Category")]
    public string Category { get; set; } = string.Empty;

    [ConfigurableProperty("Fields")]
    public int FieldCount { get; set; }

    [ConfigurableProperty("Excluded")]
    public int ExcludedFieldCount { get; set; }
}
