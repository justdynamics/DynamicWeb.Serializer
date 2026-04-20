using DynamicWeb.Serializer.Infrastructure;

namespace DynamicWeb.Serializer.Providers;

/// <summary>
/// Phase 37-04 CACHE-01: clears DW service caches via <see cref="DwCacheServiceRegistry"/>
/// (direct typed calls — no reflection, no AddInManager). Operates under the trust that
/// <c>ConfigLoader</c> has already validated every cache name against the registry;
/// any unknown name reaching <see cref="InvalidateCaches"/> is treated as a bug and
/// throws loudly.
/// </summary>
public class CacheInvalidator
{
    private readonly Func<string, DwCacheServiceRegistry.CacheClearEntry?> _resolver;

    /// <summary>
    /// Production ctor — resolves against the real <see cref="DwCacheServiceRegistry"/>.
    /// </summary>
    public CacheInvalidator()
        : this(DwCacheServiceRegistry.Resolve)
    {
    }

    /// <summary>
    /// Test ctor — resolver lets tests exercise invocation without triggering real
    /// DW <c>ClearCache()</c> side-effects on typed service singletons.
    /// </summary>
    public CacheInvalidator(Func<string, DwCacheServiceRegistry.CacheClearEntry?> resolver)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }

    /// <summary>
    /// Iterate <paramref name="serviceCacheNames"/> (deduplicated, case-insensitive),
    /// resolve each via the registry and invoke its ClearCache action. Logs a
    /// "Clearing cache: {short} ({full})" line per invocation.
    ///
    /// Throws <see cref="InvalidOperationException"/> if a name is not registered —
    /// that condition means ConfigLoader's validation was bypassed or the registry
    /// changed shape mid-run.
    /// </summary>
    public void InvalidateCaches(IEnumerable<string> serviceCacheNames, Action<string>? log = null)
    {
        foreach (var name in serviceCacheNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var entry = _resolver(name);
            if (entry is null)
            {
                throw new InvalidOperationException(
                    $"Cache service '{name}' is not in DwCacheServiceRegistry. " +
                    "This should have been caught at config-load; an unknown name " +
                    "reaching InvalidateCaches is a bug. " +
                    $"Supported: {string.Join(", ", DwCacheServiceRegistry.AllSupportedNames.Take(20))}" +
                    (DwCacheServiceRegistry.AllSupportedNames.Count > 20 ? ", ..." : "") + ".");
            }

            log?.Invoke($"Clearing cache: {entry.ShortName} ({entry.FullTypeName})");
            entry.Invoke();
        }
    }
}
