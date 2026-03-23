namespace Dynamicweb.ContentSync.Providers;

/// <summary>
/// Registry mapping provider type strings to provider instances.
/// Case-insensitive lookup by ProviderType.
/// </summary>
public class ProviderRegistry
{
    private readonly Dictionary<string, ISerializationProvider> _providers = new(StringComparer.OrdinalIgnoreCase);

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
}
