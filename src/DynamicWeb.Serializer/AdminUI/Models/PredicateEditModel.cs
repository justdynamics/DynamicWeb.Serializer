using Dynamicweb.CoreUI.Data;
using Dynamicweb.CoreUI.Data.Validation;

namespace DynamicWeb.Serializer.AdminUI.Models;

public sealed class PredicateEditModel : DataViewModelBase, IIdentifiable
{
    public int Index { get; set; } = -1;

    // DW framework treats "0" as "no identifier" — use 1-based for round-tripping
    public string GetId() => (Index + 1).ToString();

    [ConfigurableProperty("Name", explanation: "Unique name for this predicate")]
    [Required(ErrorMessage = "Name is required")]
    public string Name { get; set; } = string.Empty;

    [ConfigurableProperty("Area", explanation: "DW area containing the content tree")]
    [Required(ErrorMessage = "Area is required")]
    public int AreaId { get; set; }

    [ConfigurableProperty("Page", explanation: "Root page for this predicate")]
    [Required(ErrorMessage = "Page is required")]
    public int PageId { get; set; }

    [ConfigurableProperty("Excludes", explanation: "One path per line. Pages under these paths will be excluded from sync.")]
    public string Excludes { get; set; } = string.Empty;
}
