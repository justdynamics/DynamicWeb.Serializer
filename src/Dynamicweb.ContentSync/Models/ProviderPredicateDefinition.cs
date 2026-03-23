namespace Dynamicweb.ContentSync.Models;

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

    /// <summary>DataGroup ID for SqlTable predicates (e.g., "Settings_Ecommerce_Orders_060_OrderFlows").</summary>
    public string? DataGroupId { get; init; }

    /// <summary>Area ID for Content predicates.</summary>
    public int AreaId { get; init; } = 0;

    /// <summary>Root path for Content predicates.</summary>
    public string Path { get; init; } = "";

    /// <summary>Page ID for Content predicates.</summary>
    public int PageId { get; init; } = 0;

    /// <summary>Paths or patterns to exclude.</summary>
    public List<string> Excludes { get; init; } = new();
}
