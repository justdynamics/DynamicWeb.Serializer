using System.Text.Json.Serialization;

namespace Truvio.Commerce.Serializer.Infrastructure;

/// <summary>
/// Manifest entry for a SqlTable provider run (one SQL table per entry). Embedded XML
/// stays as a field (XmlColumns) on this entry, NOT a third entry type — per
/// ARCHITECTURE.md §reality-check (option α). Carries every deserialize-affecting field
/// from ProviderPredicateDefinition that the orchestrator post-processing depends on:
/// ServiceCaches, SchemaSync, ResolveLinksInColumns, XmlColumns. Defends pitfall #1
/// (lost post-processing metadata) — Plan 04 ships the round-trip property test.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SqlTableEntry : ManifestEntry
{
    /// <inheritdoc />
    [JsonIgnore]
    public override string ProviderType => "SqlTable";

    /// <summary>SQL table name (e.g. "EcomOrderFlow"). MUST be a valid identifier — Phase 37-03 SqlIdentifierValidator gate runs at config-load before the predicate becomes an entry.</summary>
    public required string Table { get; init; }

    /// <summary>Column used as natural key for row identity (e.g. "OrderFlowName"). Empty/null = composite PK.</summary>
    public string? NameColumn { get; init; }

    /// <summary>Comma-separated columns used for change detection.</summary>
    public string? CompareColumns { get; init; }

    /// <summary>Column names containing embedded XML content — XmlMergeHelper merges per-element on Merge deserialize.</summary>
    public IReadOnlyList<string> XmlColumns { get; init; } = Array.Empty<string>();

    /// <summary>String-column names whose Default.aspx?ID=N values get rewritten source→target via InternalLinkResolver — Phase 37-05 LINK-02 pass 2.</summary>
    public IReadOnlyList<string> ResolveLinksInColumns { get; init; } = Array.Empty<string>();

    /// <summary>Fully-qualified DW service cache type names to clear after deserialization — Phase 37-04 CACHE-01.</summary>
    public IReadOnlyList<string> ServiceCaches { get; init; } = Array.Empty<string>();

    /// <summary>Optional schema-sync hook ("EcomGroupFields" — only supported value today).</summary>
    public string? SchemaSync { get; init; }
}
