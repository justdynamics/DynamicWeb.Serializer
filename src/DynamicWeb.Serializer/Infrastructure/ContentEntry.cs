using System.Text.Json.Serialization;

namespace DynamicWeb.Serializer.Infrastructure;

/// <summary>
/// Manifest entry for a Content provider run (one Area subtree per entry). Carries the
/// dispatch identifiers Phase 43 will need (AreaId, Path, PageId), plus the two
/// post-processing inputs for Content (AcknowledgedOrphanPageIds, ExcludeAreaColumns).
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ContentEntry : ManifestEntry
{
    /// <inheritdoc />
    [JsonIgnore]
    public override string ProviderType => "Content";

    /// <summary>DW Area id this entry serializes.</summary>
    public required int AreaId { get; init; }

    /// <summary>Human-readable area name (for logs / disambiguation only — not used for dispatch).</summary>
    public required string AreaName { get; init; }

    /// <summary>Subtree path inside the area (e.g. "/" or "/customer-center").</summary>
    public required string Path { get; init; }

    /// <summary>Root page id of the subtree (0 = whole-area).</summary>
    public required int PageId { get; init; }

    /// <summary>Page IDs whose unresolvable Default.aspx?ID= references should be warnings, not fatal — see ProviderPredicateDefinition.AcknowledgedOrphanPageIds.</summary>
    public IReadOnlyList<int> AcknowledgedOrphanPageIds { get; init; } = Array.Empty<int>();

    /// <summary>Area SQL-table column names to exclude from serialization output — see ProviderPredicateDefinition.ExcludeAreaColumns.</summary>
    public IReadOnlyList<string> ExcludeAreaColumns { get; init; } = Array.Empty<string>();
}
