using DynamicWeb.Serializer.Infrastructure;
using DynamicWeb.Serializer.Providers.Content;
using DynamicWeb.Serializer.Providers.SqlTable;

namespace DynamicWeb.Serializer.Providers;

/// <summary>
/// Registry mapping provider type strings to provider instances.
/// Case-insensitive lookup by ProviderType.
/// </summary>
public class ProviderRegistry
{
    private readonly Dictionary<string, ISerializationProvider> _providers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Creates a registry with all standard providers (Content + SqlTable) pre-registered.
    /// A single <see cref="TargetSchemaCache"/> instance is constructed and passed to the
    /// SqlTable provider (Phase 37-02); ContentProvider continues to construct its own
    /// ContentDeserializer per-predicate which instantiates a fresh cache since the Area
    /// write path is independent of the SqlTable path.
    /// </summary>
    /// <param name="filesRoot">Optional Files/ root directory for ContentProvider template validation.</param>
    public static ProviderRegistry CreateDefault(string? filesRoot = null)
    {
        var registry = new ProviderRegistry();

        // Content provider
        registry.Register(new ContentProvider(filesRoot));

        // SqlTable provider (shares the TargetSchemaCache across all SqlTable predicates
        // in this registry lifetime — one INFORMATION_SCHEMA query per distinct table).
        var sqlExecutor = new DwSqlExecutor();
        var metadataReader = new DataGroupMetadataReader(sqlExecutor);
        var tableReader = new SqlTableReader(sqlExecutor);
        var fileStore = new FlatFileStore();
        var writer = new SqlTableWriter(sqlExecutor);
        var schemaCache = new TargetSchemaCache();
        registry.Register(new SqlTableProvider(metadataReader, tableReader, fileStore, writer, schemaCache));

        return registry;
    }

    /// <summary>Register a provider. Overwrites any existing registration for the same type.</summary>
    public void Register(ISerializationProvider provider)
        => _providers[provider.ProviderType] = provider;

    /// <summary>Get a provider by type string. Throws if not registered.</summary>
    public ISerializationProvider GetProvider(string providerType)
        => _providers.TryGetValue(providerType, out var provider)
            ? provider
            : throw new InvalidOperationException($"No provider registered for type '{providerType}'");

    /// <summary>Check if a provider is registered for the given type.</summary>
    public bool HasProvider(string providerType)
        => _providers.ContainsKey(providerType);

    /// <summary>All registered provider type strings.</summary>
    public IReadOnlyCollection<string> RegisteredTypes => _providers.Keys;

    /// <summary>
    /// Creates a fully-wired SerializerOrchestrator with FK ordering and cache invalidation.
    /// </summary>
    public static SerializerOrchestrator CreateOrchestrator(string? filesRoot = null)
    {
        var registry = CreateDefault(filesRoot);
        var sqlExecutor = new DwSqlExecutor();
        var fkResolver = new FkDependencyResolver(sqlExecutor);
        var cacheInvalidator = new CacheInvalidator();
        var ecomSchemaSync = new EcomGroupFieldSchemaSync(sqlExecutor);
        return new SerializerOrchestrator(registry, fkResolver, cacheInvalidator, ecomSchemaSync);
    }
}
