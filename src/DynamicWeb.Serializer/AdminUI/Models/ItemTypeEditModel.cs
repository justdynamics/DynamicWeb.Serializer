using Dynamicweb.CoreUI.Data;

namespace DynamicWeb.Serializer.AdminUI.Models;

public sealed class ItemTypeEditModel : DataViewModelBase, IIdentifiable
{
    public string SystemName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public int FieldCount { get; set; }

    public string GetId() => SystemName;

    [ConfigurableProperty("Excluded Fields", explanation: "Select fields to exclude from serialization for this item type")]
    public List<string> ExcludedFields { get; set; } = new();
}
