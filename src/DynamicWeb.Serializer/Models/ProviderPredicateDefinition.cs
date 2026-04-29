using System.Text.Json.Serialization;
using DynamicWeb.Serializer.Configuration;

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

    /// <summary>
    /// Phase 40 D-01: which DeploymentMode this predicate runs under. Replaces the section-level
    /// Deploy/Seed split — predicates now declare their own mode and the orchestrator filters
    /// `config.Predicates` by `p.Mode == DeploymentMode.Deploy` (or Seed) when iterating.
    /// JSON key is "mode" (lowercase, camelCase convention); on-disk values are "Deploy" / "Seed"
    /// (read case-insensitively).
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DeploymentMode Mode { get; init; } = DeploymentMode.Deploy;

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

    /// <summary>Column names containing embedded XML content for SqlTable predicates.</summary>
    public List<string> XmlColumns { get; init; } = new();

    /// <summary>Field names to exclude from serialization output.</summary>
    public List<string> ExcludeFields { get; init; } = new();

    /// <summary>XML element names to exclude from embedded XML content during serialization.</summary>
    public List<string> ExcludeXmlElements { get; init; } = new();

    /// <summary>Area SQL table column names to exclude from serialization. Content predicates only.</summary>
    public List<string> ExcludeAreaColumns { get; init; } = new();

    /// <summary>
    /// Optional WHERE clause for SqlTable predicates, applied at serialize time to filter rows
    /// (FILTER-01 / Phase 37-03). Validated at config-load via SqlWhereClauseValidator — every
    /// identifier must match INFORMATION_SCHEMA.COLUMNS of <see cref="Table"/>; banned tokens
    /// (<c>;</c>, <c>--</c>, <c>/*</c>, <c>EXEC</c>, <c>xp_</c>, etc.) are rejected.
    /// Example: <c>AccessUserType = 2 AND AccessUserUserName IN ('Admin','Editors')</c>.
    /// </summary>
    public string? Where { get; init; }

    /// <summary>
    /// Optional per-predicate column opt-in (Phase 37-03, RUNTIME-COLS-01): columns listed here
    /// are KEPT in serialization output even if they would otherwise be auto-excluded by
    /// <see cref="Configuration.RuntimeExcludes"/>. Case-insensitive.
    /// </summary>
    public List<string> IncludeFields { get; init; } = new();

    /// <summary>
    /// Optional per-SqlTable-predicate (Phase 37-05 / LINK-02 pass 2, D-22): column names whose
    /// string values get <see cref="Serialization.InternalLinkResolver.ResolveInStringColumn"/>
    /// applied at deserialize. Default.aspx?ID=N references in these columns are rewritten
    /// source→target page ID using the cross-environment map built from Content-provider runs.
    /// Example: <c>UrlPath</c> predicate with <c>ResolveLinksInColumns = ["UrlPathRedirect"]</c>
    /// lets the <c>UrlPathRedirect</c> field survive cross-env deploys. Empty = no link
    /// resolution for this table's columns.
    /// </summary>
    public List<string> ResolveLinksInColumns { get; init; } = new();

    /// <summary>
    /// Per-predicate Baseline link-sweep bypass (2026-04-20 follow-up to Phase 37-05 LINK-02).
    /// Page IDs whose unresolvable references should be logged as warnings rather than raised
    /// as fatal errors by the serialize-time <see cref="Infrastructure.BaselineLinkSweeper"/>.
    /// Content predicates only. Use for known-broken source data that cannot be cleaned upstream
    /// in time; any unresolvable NOT in this list still fails serialize.
    /// </summary>
    public List<int> AcknowledgedOrphanPageIds { get; init; } = new();
}
