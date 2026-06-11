using Dynamicweb.CoreUI.Data;

namespace Truvio.Commerce.Serializer.AdminUI.Models;

/// <summary>Input for the "Start from the Swift starter" Get-started action.</summary>
public sealed class StarterConfigModel : DataViewModelBase
{
    [ConfigurableProperty("What this does", explanation: "")]
    public string Summary { get; set; } =
        "Writes the Swift starter configuration: the platform-wired site deploys (checkout, customer center, " +
        "service pages, commerce framework tables), the customer-owned content surfaces seed (Home, About, blog, " +
        "footer pages, example newsletters, catalog data), and the known environment-specific fields stay local. " +
        "Nothing is synced by applying it — review the predicates first, then run a dry-run preview.";

    [ConfigurableProperty("Website", explanation: "The website (area) the starter's content predicates target. The starter is written for a Swift site.")]
    public int AreaId { get; set; }
}
