namespace DynamicWeb.Serializer.Models;

/// <summary>
/// Extended predicate definition for provider-based routing.
/// Includes fields for all provider types (Content, SqlTable, etc.).
/// </summary>
public record ProviderPredicateDefinition
{
    /// <summary>Human-readable predicate name.</summary>
    public required string Name { get; init; }

    /// <summary>Provider type to route to (e.g., "Content", "SqlTable").</summary>
    public required string ProviderType { get; init; }

    /// <summary>SQL table name for SqlTable predicates (e.g., "EcomOrderFlow").</summary>
    public string? Table { get; init; }

    /// <summary>Column used as natural key for row identity (e.g., "OrderFlowName"). Empty = use composite PK.</summary>
    public string? NameColumn { get; init; }

    /// <summary>Comma-separated columns used for change detection. Empty = use all non-identity columns.</summary>
    public string? CompareColumns { get; init; }

    /// <summary>Area ID for Content predicates.</summary>
    public int AreaId { get; init; } = 0;

    /// <summary>Root path for Content predicates.</summary>
    public string Path { get; init; } = "";

    /// <summary>Page ID for Content predicates.</summary>
    public int PageId { get; init; } = 0;

    /// <summary>Paths or patterns to exclude.</summary>
    public List<string> Excludes { get; init; } = new();

    /// <summary>
    /// Fully-qualified DW service cache type names to clear after deserialization.
    /// Sourced from DataGroup XML ServiceCaches sections.
    /// </summary>
    public List<string> ServiceCaches { get; init; } = new();

    /// <summary>
    /// Optional schema sync configuration. When set, the orchestrator runs
    /// post-deserialize schema sync to ensure custom columns exist on the target table.
    /// Format: "EcomGroupFields" (only supported value currently).
    /// </summary>
    public string? SchemaSync { get; init; }

    /// <summary>Column names containing XML that should be pretty-printed in YAML. SqlTable predicates only.</summary>
    public List<string> XmlColumns { get; init; } = new();
}
