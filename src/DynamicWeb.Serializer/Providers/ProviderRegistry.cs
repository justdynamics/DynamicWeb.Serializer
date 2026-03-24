using System.Reflection;
using DynamicWeb.Serializer.Providers.Content;
using DynamicWeb.Serializer.Providers.SqlTable;

namespace DynamicWeb.Serializer.Providers;

/// <summary>
/// Production ICacheResolver using DW AddInManager to resolve service caches.
/// Replicates the pattern from DW10 LocalDeploymentProvider.ImportPackage:
///   AddInManager.GetTypeUnvalidated{ICacheStorage}(name) + GetInstance{ICacheStorage}(name).ClearCache()
/// Uses reflection to avoid compile-time dependency on AddInManager static methods,
/// which allows this code to compile and be tested outside a full DW runtime.
/// </summary>
public class DwCacheResolver : ICacheResolver
{
    private static Type? _addInManagerType;
    private static MethodInfo? _getTypeMethod;
    private static MethodInfo? _getInstanceMethod;

    /// <summary>
    /// Lazily locate the AddInManager type and its generic methods.
    /// </summary>
    private static bool EnsureAddInManager()
    {
        if (_addInManagerType != null) return true;

        _addInManagerType = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a =>
            {
                try { return a.GetType("Dynamicweb.Extensibility.AddInManager"); }
                catch { return null; }
            })
            .FirstOrDefault(t => t != null);

        if (_addInManagerType is null) return false;

        // Find the ICacheStorage type
        var cacheStorageType = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a =>
            {
                try { return a.GetType("Dynamicweb.Caching.ICacheStorage"); }
                catch { return null; }
            })
            .FirstOrDefault(t => t != null);

        if (cacheStorageType is null) return false;

        // Resolve AddInManager.GetTypeUnvalidated<ICacheStorage>(string)
        var getTypeGeneric = _addInManagerType.GetMethods(BindingFlags.Static | BindingFlags.Public)
            .FirstOrDefault(m => m.Name == "GetTypeUnvalidated" && m.IsGenericMethodDefinition
                                 && m.GetParameters().Length == 1
                                 && m.GetParameters()[0].ParameterType == typeof(string));
        _getTypeMethod = getTypeGeneric?.MakeGenericMethod(cacheStorageType);

        // Resolve AddInManager.GetInstance<ICacheStorage>(string)
        var getInstanceGeneric = _addInManagerType.GetMethods(BindingFlags.Static | BindingFlags.Public)
            .FirstOrDefault(m => m.Name == "GetInstance" && m.IsGenericMethodDefinition
                                 && m.GetParameters().Length == 1
                                 && m.GetParameters()[0].ParameterType == typeof(string));
        _getInstanceMethod = getInstanceGeneric?.MakeGenericMethod(cacheStorageType);

        return _getTypeMethod != null && _getInstanceMethod != null;
    }

    public Type? GetCacheType(string serviceCacheName)
    {
        try
        {
            if (!EnsureAddInManager() || _getTypeMethod is null) return null;
            return _getTypeMethod.Invoke(null, new object[] { serviceCacheName }) as Type;
        }
        catch
        {
            return null;
        }
    }

    public ICacheInstance? GetCacheInstance(string serviceCacheName)
    {
        try
        {
            if (!EnsureAddInManager() || _getInstanceMethod is null) return null;
            var instance = _getInstanceMethod.Invoke(null, new object[] { serviceCacheName });
            if (instance is null) return null;

            // Call ClearCache() via interface or reflection
            var clearMethod = instance.GetType().GetMethod("ClearCache", BindingFlags.Instance | BindingFlags.Public);
            if (clearMethod is null) return null;

            return new DwCacheInstance(instance, clearMethod);
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Wraps a DW ICacheStorage instance to implement our testable ICacheInstance interface.
/// Uses reflection to call ClearCache() to avoid compile-time coupling.
/// </summary>
public class DwCacheInstance : ICacheInstance
{
    private readonly object _instance;
    private readonly MethodInfo _clearMethod;

    public DwCacheInstance(object instance, MethodInfo clearMethod)
    {
        _instance = instance;
        _clearMethod = clearMethod;
    }

    public void ClearCache()
        => _clearMethod.Invoke(_instance, Array.Empty<object>());
}

/// <summary>
/// Registry mapping provider type strings to provider instances.
/// Case-insensitive lookup by ProviderType.
/// </summary>
public class ProviderRegistry
{
    private readonly Dictionary<string, ISerializationProvider> _providers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Creates a registry with all standard providers (Content + SqlTable) pre-registered.
    /// </summary>
    /// <param name="filesRoot">Optional Files/ root directory for ContentProvider template validation.</param>
    public static ProviderRegistry CreateDefault(string? filesRoot = null)
    {
        var registry = new ProviderRegistry();

        // Content provider
        registry.Register(new ContentProvider(filesRoot));

        // SqlTable provider
        var sqlExecutor = new DwSqlExecutor();
        var metadataReader = new DataGroupMetadataReader(sqlExecutor);
        var tableReader = new SqlTableReader(sqlExecutor);
        var fileStore = new FlatFileStore();
        var writer = new SqlTableWriter(sqlExecutor);
        registry.Register(new SqlTableProvider(metadataReader, tableReader, fileStore, writer));

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
        var cacheInvalidator = new CacheInvalidator(new DwCacheResolver());
        return new SerializerOrchestrator(registry, fkResolver, cacheInvalidator);
    }
}
