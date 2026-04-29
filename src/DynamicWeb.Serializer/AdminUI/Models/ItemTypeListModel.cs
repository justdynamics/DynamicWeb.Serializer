using Dynamicweb.CoreUI.Data;

namespace DynamicWeb.Serializer.AdminUI.Models;

public sealed class ItemTypeListModel : DataViewModelBase
{
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
