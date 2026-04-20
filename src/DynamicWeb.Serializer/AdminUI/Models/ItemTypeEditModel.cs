using DynamicWeb.Serializer.Configuration;
using Dynamicweb.CoreUI.Data;

namespace DynamicWeb.Serializer.AdminUI.Models;

public sealed class ItemTypeEditModel : DataViewModelBase, IIdentifiable
{
    public string SystemName { get; set; } = string.Empty;

    /// <summary>
    /// Which <see cref="DeploymentMode"/> this item type's exclude list lives under (Phase 37-01.1).
    /// Set by <see cref="Queries.ItemTypeBySystemNameQuery"/>; read by
    /// <see cref="Commands.SaveItemTypeCommand"/> to target the correct
    /// <see cref="ModeConfig.ExcludeFieldsByItemType"/> dictionary. Defaults to
    /// <see cref="DeploymentMode.Deploy"/> to match the predicate path default and avoid
    /// accidental Seed writes in pre-split call sites.
    /// </summary>
    public DeploymentMode Mode { get; set; } = DeploymentMode.Deploy;

    public string DisplayName { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public int FieldCount { get; set; }

    public string GetId() => SystemName;

    [ConfigurableProperty("Excluded Fields", explanation: "Select fields to exclude from serialization for this item type")]
    public string ExcludedFields { get; set; } = string.Empty;
}
