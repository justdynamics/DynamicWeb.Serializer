namespace Dynamicweb.ContentSync.Providers;

/// <summary>
/// Abstraction over DW AddInManager for testability.
/// Production code uses DwCacheResolver; tests inject a mock.
/// </summary>
public interface ICacheResolver
{
    /// <summary>Resolve the cache type by fully-qualified name. Returns null if not found.</summary>
    Type? GetCacheType(string serviceCacheName);

    /// <summary>Get a cache instance by fully-qualified name. Returns null if cannot be created.</summary>
    ICacheInstance? GetCacheInstance(string serviceCacheName);
}

/// <summary>
/// Abstraction over DW ICacheStorage for testability.
/// In production, wraps the real Dynamicweb.Caching.ICacheStorage instance.
/// </summary>
public interface ICacheInstance
{
    /// <summary>Clear all entries from this cache.</summary>
    void ClearCache();
}

/// <summary>
/// Clears DW service caches after deserialization.
/// Replicates the exact pattern from DW10 LocalDeploymentProvider.ImportPackage (lines 182-194):
/// resolve cache type via AddInManager, get instance, call ClearCache().
/// Per D-08: cache invalidation is per-predicate, not blanket.
/// Per D-09: applies to ALL providers (Content and SqlTable).
/// </summary>
public class CacheInvalidator
{
    private readonly ICacheResolver _cacheResolver;

    public CacheInvalidator(ICacheResolver cacheResolver)
    {
        _cacheResolver = cacheResolver ?? throw new ArgumentNullException(nameof(cacheResolver));
    }

    /// <summary>
    /// Clear DW service caches by their fully-qualified type names.
    /// Skips gracefully if a cache type is not found or cannot be instantiated (per Pitfall 3).
    /// Deduplicates cache names to avoid clearing the same cache twice.
    /// </summary>
    public void InvalidateCaches(IEnumerable<string> serviceCacheNames, Action<string>? log = null)
    {
        foreach (var serviceCacheName in serviceCacheNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var cacheType = _cacheResolver.GetCacheType(serviceCacheName);
            if (cacheType is null)
            {
                log?.Invoke($"Cache type not found: {serviceCacheName} (skipping)");
                continue;
            }

            var cacheInstance = _cacheResolver.GetCacheInstance(serviceCacheName);
            if (cacheInstance is null)
            {
                log?.Invoke($"Could not create cache instance: {serviceCacheName} (skipping)");
                continue;
            }

            log?.Invoke($"Clearing cache: {serviceCacheName}");
            cacheInstance.ClearCache();
        }
    }
}
