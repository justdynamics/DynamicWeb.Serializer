using Dynamicweb.CoreUI.Data;

namespace DynamicWeb.Serializer.AdminUI.Models;

public sealed class XmlTypeListModel : DataViewModelBase
{
    [ConfigurableProperty("Type Name")]
    public string TypeName { get; set; } = string.Empty;

    [ConfigurableProperty("Excluded Elements")]
    public int ExcludedElementCount { get; set; }
}
