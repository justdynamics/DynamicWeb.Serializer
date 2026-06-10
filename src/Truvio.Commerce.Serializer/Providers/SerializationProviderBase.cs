using Truvio.Commerce.Serializer.Configuration;
using Truvio.Commerce.Serializer.Infrastructure;
using Truvio.Commerce.Serializer.Models;
using Truvio.Commerce.Serializer.Serialization;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Truvio.Commerce.Serializer.Providers;

/// <summary>
/// Abstract base class for serialization providers.
/// Provides shared YAML serializer/deserializer instances and logging helpers.
/// </summary>
public abstract class SerializationProviderBase : ISerializationProvider
{
    protected readonly ISerializer _yamlSerializer;
    protected readonly IDeserializer _yamlDeserializer;

    protected SerializationProviderBase()
    {
        _yamlSerializer = YamlConfiguration.BuildSerializer();
        _yamlDeserializer = YamlConfiguration.BuildDeserializer();
    }

    public abstract string ProviderType { get; }
    public abstract string DisplayName { get; }

    public abstract SerializeResult Serialize(
        ProviderPredicateDefinition predicate,
        string outputRoot,
        Action<string>? log = null,
        IReadOnlyDictionary<string, List<string>>? excludeFieldsByItemType = null,
        IReadOnlyDictionary<string, List<string>>? excludeXmlElementsByType = null);

    public abstract ProviderDeserializeResult Deserialize(
        ManifestEntry entry,
        string inputRoot,
        Action<string>? log = null,
        bool isDryRun = false,
        ConflictStrategy strategy = ConflictStrategy.SourceWins,
        InternalLinkResolver? linkResolver = null,
        IReadOnlyDictionary<string, List<string>>? excludeFieldsByItemType = null,
        IReadOnlyDictionary<string, List<string>>? excludeXmlElementsByType = null);

    // Phase 43 / DESER-03: abstract ValidatePredicate declaration removed — interface
    // no longer requires it (validation moves to manifest read time). Concrete providers
    // may keep ValidatePredicate as a private/public helper for serialize-side input
    // gating, but it is no longer part of the polymorphic contract.

    /// <summary>
    /// Phase 42-03 / PROVIDER-01: see <see cref="ISerializationProvider.BuildManifestEntry"/>.
    /// Re-declared here as <c>public abstract</c> so every subclass must implement it.
    /// </summary>
    public abstract ManifestEntry BuildManifestEntry(
        ProviderPredicateDefinition predicate,
        string modeRoot,
        IReadOnlyList<string> writtenFiles);

    /// <summary>
    /// Builds a YAML serializer that does NOT omit nulls — emits null as ~ (tilde).
    /// Required for SQL table serialization where NULL vs empty string matters.
    /// </summary>
    protected static ISerializer BuildSqlYamlSerializer() =>
        new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.Preserve)
            .Build();

    /// <summary>Log a message if a logging callback is provided.</summary>
    protected static void Log(string message, Action<string>? log) => log?.Invoke(message);
}
