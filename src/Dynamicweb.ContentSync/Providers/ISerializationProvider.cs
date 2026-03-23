using Dynamicweb.ContentSync.Models;

namespace Dynamicweb.ContentSync.Providers;

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
    SerializeResult Serialize(ProviderPredicateDefinition predicate, string outputRoot, Action<string>? log = null);

    /// <summary>Deserialize YAML files from disk back into the database.</summary>
    /// <param name="predicate">The predicate defining what to deserialize.</param>
    /// <param name="inputRoot">Root directory containing YAML files.</param>
    /// <param name="log">Optional logging callback.</param>
    /// <param name="isDryRun">When true, reports what would change without modifying the database.</param>
    ProviderDeserializeResult Deserialize(ProviderPredicateDefinition predicate, string inputRoot, Action<string>? log = null, bool isDryRun = false);

    /// <summary>Validate that a predicate is correctly configured for this provider.</summary>
    ValidationResult ValidatePredicate(ProviderPredicateDefinition predicate);
}
