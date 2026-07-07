using Truvio.Commerce.Serializer.Configuration;
using Dynamicweb.CoreUI.Data;

namespace Truvio.Commerce.Serializer.AdminUI.Models;

public sealed class PredicateListModel : DataViewModelBase
{
    public int Index { get; set; }

    /// <summary>
    /// The predicate's own mode. Surfaced in the list view via ModeDisplay
    /// and used by PredicateListScreen to thread the mode into Edit/Delete actions on each row.
    /// </summary>
    public SerializerMode Mode { get; set; } = SerializerMode.Replace;

    [ConfigurableProperty("Mode")]
    public string ModeDisplay => Mode == SerializerMode.Replace ? "Replace" : "Merge";

    [ConfigurableProperty("Name")]
    public string Name { get; set; } = string.Empty;

    [ConfigurableProperty("Type")]
    public string Type { get; set; } = string.Empty;

    [ConfigurableProperty("Target")]
    public string Target { get; set; } = string.Empty;
}
