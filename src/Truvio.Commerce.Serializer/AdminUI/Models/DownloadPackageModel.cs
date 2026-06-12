using Dynamicweb.CoreUI.Data;
using Truvio.Commerce.Serializer.Serialization;

namespace Truvio.Commerce.Serializer.AdminUI.Models;

/// <summary>Model for the Download Package dialog (tree right-click / page Actions).</summary>
public sealed class DownloadPackageModel : DataViewModelBase
{
    public int PageId { get; set; }

    public int AreaId { get; set; }

    [ConfigurableProperty("Page", explanation: "The page this package is built from.")]
    public string PageName { get; set; } = string.Empty;

    [ConfigurableProperty("Content scope")]
    public string Scope { get; set; } = PackageBuilder.ScopePageAndSubpages;

    [ConfigurableProperty("Include referenced assets",
        explanation: "Bundle the images and files this content references (from the Files archive) into the package. Layouts and item types are not bundled — they ship with the design and are verified when the package is uploaded.")]
    public bool IncludeAssets { get; set; }
}
