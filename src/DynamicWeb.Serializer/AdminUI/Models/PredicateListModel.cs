using Dynamicweb.CoreUI.Data;

namespace DynamicWeb.Serializer.AdminUI.Models;

public sealed class PredicateListModel : DataViewModelBase
{
    public int Index { get; set; }

    [ConfigurableProperty("Name")]
    public string Name { get; set; } = string.Empty;

    [ConfigurableProperty("Type")]
    public string Type { get; set; } = string.Empty;

    [ConfigurableProperty("Target")]
    public string Target { get; set; } = string.Empty;
}
