using DynamicWeb.Serializer.Configuration;
using DynamicWeb.Serializer.Models;
using DynamicWeb.Serializer.Serialization;

namespace DynamicWeb.Serializer.Providers;

/// <summary>
/// Core provider interface for serializing/deserializing data to/from YAML.
/// Each provider type (Content, SqlTable, Settings, Schema) implements this contract.
/// </summary>
public interface ISerializationProvider
{
    /// <summary>Provider type identifier (e.g., "Content", "SqlTable").</summary>
    string ProviderType { get; }

    /// <summary>Human-readable display name for UI.</summary>
    string DisplayName { get; }

    /// <summary>Serialize data from the database to YAML files on disk.</summary>
    /// <param name="predicate">The predicate defining what to serialize.</param>
    /// <param name="outputRoot">Root directory to write YAML into.</param>
    /// <param name="log">Optional logging callback.</param>
    /// <param name="excludeFieldsByItemType">
    /// Top-level <see cref="SerializerConfiguration.ExcludeFieldsByItemType"/> dict (Phase 40 D-04).
    /// ContentProvider threads this down to <see cref="ContentSerializer"/> so ItemType-scoped
    /// field exclusions apply across every predicate. SqlTableProvider currently ignores this — it
    /// uses its own per-predicate field-level mechanisms.
    /// </param>
    /// <param name="excludeXmlElementsByType">
    /// Top-level <see cref="SerializerConfiguration.ExcludeXmlElementsByType"/> dict (Phase 40 D-04)
    /// keyed by XML type name (page.UrlDataProviderTypeName or paragraph.ModuleSystemName).
    /// ContentProvider threads this down so XML element stripping applies by type instead of
    /// relying solely on per-predicate flat lists.
    /// </param>
    SerializeResult Serialize(
        ProviderPredicateDefinition predicate,
        string outputRoot,
        Action<string>? log = null,
        IReadOnlyDictionary<string, List<string>>? excludeFieldsByItemType = null,
        IReadOnlyDictionary<string, List<string>>? excludeXmlElementsByType = null);

    /// <summary>Deserialize YAML files from disk back into the database.</summary>
    /// <param name="predicate">The predicate defining what to deserialize.</param>
    /// <param name="inputRoot">Root directory containing YAML files.</param>
    /// <param name="log">Optional logging callback.</param>
    /// <param name="isDryRun">When true, reports what would change without modifying the database.</param>
    /// <param name="strategy">
    /// Conflict strategy (Phase 37-01). <see cref="ConflictStrategy.SourceWins"/> preserves the
    /// pre-Phase-37 behavior — YAML overwrites target. <see cref="ConflictStrategy.DestinationWins"/>
    /// skips rows/pages whose natural key / PageUniqueId is already present on target.
    /// </param>
    /// <param name="linkResolver">
    /// Phase 37-05 / LINK-02 pass 2: optional cross-environment link resolver. SqlTableProvider
    /// threads this into <see cref="SqlTable.SqlTableWriter.ApplyLinkResolution"/> so every
    /// row, for every column listed in <see cref="ProviderPredicateDefinition.ResolveLinksInColumns"/>,
    /// gets its Default.aspx?ID=N references rewritten source→target. Null = no rewrite (the
    /// provider's current behaviour when no Content-provider has yet populated the map).
    /// </param>
    /// <param name="excludeFieldsByItemType">Parent mode's ItemType field exclusion dict (see <see cref="Serialize"/>).</param>
    /// <param name="excludeXmlElementsByType">Parent mode's XML element exclusion dict (see <see cref="Serialize"/>).</param>
    ProviderDeserializeResult Deserialize(
        ProviderPredicateDefinition predicate,
        string inputRoot,
        Action<string>? log = null,
        bool isDryRun = false,
        ConflictStrategy strategy = ConflictStrategy.SourceWins,
        InternalLinkResolver? linkResolver = null,
        IReadOnlyDictionary<string, List<string>>? excludeFieldsByItemType = null,
        IReadOnlyDictionary<string, List<string>>? excludeXmlElementsByType = null);

    /// <summary>Validate that a predicate is correctly configured for this provider.</summary>
    ValidationResult ValidatePredicate(ProviderPredicateDefinition predicate);
}
