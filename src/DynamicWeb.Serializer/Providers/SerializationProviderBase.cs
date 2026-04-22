using DynamicWeb.Serializer.Configuration;
using DynamicWeb.Serializer.Infrastructure;
using DynamicWeb.Serializer.Models;
using DynamicWeb.Serializer.Serialization;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DynamicWeb.Serializer.Providers;

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
        ProviderPredicateDefinition predicate,
        string inputRoot,
        Action<string>? log = null,
        bool isDryRun = false,
        ConflictStrategy strategy = ConflictStrategy.SourceWins,
        InternalLinkResolver? linkResolver = null,
        IReadOnlyDictionary<string, List<string>>? excludeFieldsByItemType = null,
        IReadOnlyDictionary<string, List<string>>? excludeXmlElementsByType = null);

    public abstract ValidationResult ValidatePredicate(ProviderPredicateDefinition predicate);

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
