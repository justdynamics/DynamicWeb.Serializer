using Dynamicweb.CoreUI.Data;

namespace DynamicWeb.Serializer.AdminUI.Models;

public sealed class XmlTypeEditModel : DataViewModelBase, IIdentifiable
{
    public string TypeName { get; set; } = string.Empty;

    public string GetId() => TypeName;

    [ConfigurableProperty("Excluded Elements", explanation: "Select XML elements to exclude from serialization for this type")]
    public List<string> ExcludedElements { get; set; } = new();
}
