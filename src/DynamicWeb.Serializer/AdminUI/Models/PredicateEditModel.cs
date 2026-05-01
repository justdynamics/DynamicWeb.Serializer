using DynamicWeb.Serializer.Configuration;
using Dynamicweb.CoreUI.Data;
using Dynamicweb.CoreUI.Data.Validation;

namespace DynamicWeb.Serializer.AdminUI.Models;

public sealed class PredicateEditModel : DataViewModelBase, IIdentifiable
{
    public int Index { get; set; } = -1;

    /// <summary>
    /// Phase 41 D-13: string-typed for DW Select binding. The DW framework matches Select.ListOption.Value
    /// (string) against the model property by string equality on render -- an enum-typed Mode breaks the
    /// match and the screen errors out with "The selected option no longer exists" (project memory
    /// feedback_dw_patterns.md). Mirrors the LogLevel / ConflictStrategy precedent on SerializerSettingsModel.
    ///
    /// Persists into ProviderPredicateDefinition.Mode (DeploymentMode enum) via Enum.Parse&lt;DeploymentMode&gt;
    /// in SavePredicateCommand, and hydrates from the enum via .ToString() in PredicateByIndexQuery.
    /// Default value mirrors nameof(DeploymentMode.Deploy) to keep the new-predicate flow valid for the
    /// Enum.Parse round-trip.
    /// </summary>
    [ConfigurableProperty("Mode", hint: "Deploy = source-wins (YAML overwrites destination). Seed = destination-wins field-level merge (only fills empty destination fields).")]
    public string Mode { get; set; } = nameof(DeploymentMode.Deploy);

    // DW framework treats "0" as "no identifier" -- use 1-based for round-tripping
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

    [ConfigurableProperty("Where Clause",
        explanation: "SqlTable only. SQL WHERE clause applied at serialize. Identifiers must exist in the target table schema. No semicolons, SQL comments, or subqueries. Example: AccessUserType = 2 AND AccessUserUserName IN ('Admin','Editors')")]
    public string WhereClause { get; set; } = string.Empty;

    [ConfigurableProperty("Include Fields",
        explanation: "SqlTable only. Columns kept in serialization output even if they would be auto-excluded by the runtime-exclusion registry. One per line.")]
    public string IncludeFields { get; set; } = string.Empty;

    [ConfigurableProperty("Resolve Links In Columns",
        explanation: "SqlTable only. Column names (one per line) whose string values contain Default.aspx?ID=N references to DW pages. At deserialize, the source page ID is rewritten to the target page ID using the cross-environment map built from the Content predicates in this run. Example: UrlPathRedirect")]
    public string ResolveLinksInColumns { get; set; } = string.Empty;
}
