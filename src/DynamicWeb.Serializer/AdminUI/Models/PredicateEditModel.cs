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

    [ConfigurableProperty("Provider Type", explanation: "Content syncs DW pages; SqlTable syncs database tables")]
    public string ProviderType { get; set; } = string.Empty;

    [ConfigurableProperty("Area", explanation: "DW area containing the content tree")]
    public int AreaId { get; set; }

    [ConfigurableProperty("Page", explanation: "Root page for this predicate")]
    public int PageId { get; set; }

    [ConfigurableProperty("Excludes", explanation: "One path per line. Pages under these paths will be excluded from sync.")]
    public string Excludes { get; set; } = string.Empty;

    [ConfigurableProperty("Table", explanation: "SQL table name (e.g., EcomOrderFlow)")]
    public string Table { get; set; } = string.Empty;

    [ConfigurableProperty("Name Column", explanation: "Column used as natural key for row identity (leave empty for composite PK)")]
    public string NameColumn { get; set; } = string.Empty;

    [ConfigurableProperty("Compare Columns", explanation: "Comma-separated columns for change detection (leave empty for all non-identity columns)")]
    public string CompareColumns { get; set; } = string.Empty;

    [ConfigurableProperty("Service Caches", explanation: "One fully-qualified DW cache type per line. Cleared after deserialization.")]
    public string ServiceCaches { get; set; } = string.Empty;

    [ConfigurableProperty("Exclude Fields", explanation: "One field name per line. These fields will be omitted from serialization.")]
    public string ExcludeFields { get; set; } = string.Empty;

    [ConfigurableProperty("XML Columns", explanation: "One column name per line. These SQL table columns contain XML that should be pretty-printed.")]
    public string XmlColumns { get; set; } = string.Empty;

    [ConfigurableProperty("Exclude XML Elements", explanation: "One XML element name per line. These elements will be stripped from embedded XML blobs.")]
    public string ExcludeXmlElements { get; set; } = string.Empty;

    [ConfigurableProperty("Exclude Area Columns", explanation: "Area table columns to exclude from serialization.")]
    public string ExcludeAreaColumns { get; set; } = string.Empty;
}
