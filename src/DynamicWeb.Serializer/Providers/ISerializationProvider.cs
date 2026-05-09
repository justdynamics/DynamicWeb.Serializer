using DynamicWeb.Serializer.Configuration;
using DynamicWeb.Serializer.Infrastructure;
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

    /// <summary>
    /// Phase 43 / DESER-03: deserialize YAML files from disk back into the database, driven
    /// by a polymorphic <see cref="ManifestEntry"/> read from <c>{mode}-manifest.json</c>.
    /// Implementations downcast the entry to their concrete type (<see cref="ContentEntry"/>
    /// for ContentProvider, <see cref="SqlTableEntry"/> for SqlTableProvider) and return a
    /// <see cref="ProviderDeserializeResult"/> with per-row counts for the orchestrator to
    /// roll up into an <see cref="Reporting.EntryOutcome"/>.
    /// </summary>
    /// <param name="entry">The manifest entry defining what to deserialize. Must be the
    /// concrete subtype matching <see cref="ProviderType"/>.</param>
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
    /// row, for every column listed in <see cref="SqlTableEntry.ResolveLinksInColumns"/>,
    /// gets its Default.aspx?ID=N references rewritten source→target. Null = no rewrite.
    /// </param>
    /// <param name="excludeFieldsByItemType">Parent mode's ItemType field exclusion dict (see <see cref="Serialize"/>).</param>
    /// <param name="excludeXmlElementsByType">Parent mode's XML element exclusion dict (see <see cref="Serialize"/>).</param>
    ProviderDeserializeResult Deserialize(
        ManifestEntry entry,
        string inputRoot,
        Action<string>? log = null,
        bool isDryRun = false,
        ConflictStrategy strategy = ConflictStrategy.SourceWins,
        InternalLinkResolver? linkResolver = null,
        IReadOnlyDictionary<string, List<string>>? excludeFieldsByItemType = null,
        IReadOnlyDictionary<string, List<string>>? excludeXmlElementsByType = null);

    // Phase 43 / DESER-03: ValidatePredicate removed from interface — validation moves
    // to manifest read time (via ManifestSchema strict-read + ManifestEntry required modifiers).
    // Per-provider internal validation surface lives on the concrete provider class.

    /// <summary>
    /// Phase 42-03 / PROVIDER-01: build the manifest entry for this provider given the predicate
    /// that drove the run, the mode root the run wrote into, and the absolute file paths produced.
    /// Called from each provider's <see cref="Serialize"/> implementation just before returning.
    /// Files in the returned <see cref="ManifestEntry.Files"/> MUST be POSIX-relative under
    /// <paramref name="modeRoot"/> (forward slashes, no leading slash) so the manifest is portable
    /// across Windows/Linux build hosts.
    /// </summary>
    ManifestEntry BuildManifestEntry(
        ProviderPredicateDefinition predicate,
        string modeRoot,
        IReadOnlyList<string> writtenFiles);
}
