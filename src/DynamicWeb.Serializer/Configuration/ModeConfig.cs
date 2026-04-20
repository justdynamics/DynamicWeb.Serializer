using DynamicWeb.Serializer.Models;

namespace DynamicWeb.Serializer.Configuration;

/// <summary>
/// Per-mode serialization scope. Each <see cref="SerializerConfiguration"/> carries one ModeConfig
/// for <see cref="DeploymentMode.Deploy"/> and one for <see cref="DeploymentMode.Seed"/> — the two
/// modes have independent predicate lists, exclusion config, conflict strategy and output folder.
/// Introduced in Phase 37-01 per D-01..D-06.
/// </summary>
public record ModeConfig
{
    /// <summary>Predicates serialized / deserialized when this mode runs.</summary>
    public List<ProviderPredicateDefinition> Predicates { get; init; } = new();

    /// <summary>Global per-item-type field exclusions, scoped to this mode.</summary>
    public Dictionary<string, List<string>> ExcludeFieldsByItemType { get; init; } = new();

    /// <summary>Global per-type XML element exclusions, scoped to this mode.</summary>
    public Dictionary<string, List<string>> ExcludeXmlElementsByType { get; init; } = new();

    /// <summary>
    /// Subfolder under <see cref="SerializerConfiguration.SerializeRoot"/> where this mode's
    /// YAML output is written. Default is set by <see cref="ConfigLoader"/> — "deploy" for Deploy,
    /// "seed" for Seed. Validated against a safe-name regex to prevent path traversal (T-37-01-02).
    /// </summary>
    public string OutputSubfolder { get; init; } = "";

    /// <summary>
    /// How to resolve conflicts on deserialize. Deploy defaults to <see cref="ConflictStrategy.SourceWins"/>
    /// (YAML overwrites target — v0.4.x behavior preserved). Seed defaults to
    /// <see cref="ConflictStrategy.DestinationWins"/> — rows/pages whose natural key is already on target
    /// are left untouched. Default for this record itself is SourceWins; the Seed-specific default is
    /// applied by <see cref="SerializerConfiguration.Seed"/> and by <see cref="ConfigLoader"/>.
    /// </summary>
    public ConflictStrategy ConflictStrategy { get; init; } = ConflictStrategy.SourceWins;

    /// <summary>
    /// Phase 37-05 LINK-02 pass-1 bypass (D-22 escape hatch, added 2026-04-20 follow-up):
    /// page IDs whose unresolvable references the baseline link sweep should log as warnings
    /// rather than raise as fatal errors. Use only for known-broken source data that cannot
    /// be cleaned upstream in time for a deploy. Each listed ID is accepted as an acknowledged
    /// orphan — any OTHER unresolvable reference still fails serialize. Empty list preserves
    /// strict-by-default behavior.
    /// </summary>
    public List<int> AcknowledgedOrphanPageIds { get; init; } = new();
}
