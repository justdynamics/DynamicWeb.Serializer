using DynamicWeb.Serializer.Infrastructure;
using Xunit;

namespace DynamicWeb.Serializer.Tests.Infrastructure;

/// <summary>
/// Phase 37-04 / CACHE-01: curated compile-time map of DW service caches.
/// The production Invoke actions require a live DW runtime to execute without throwing;
/// tests here assert resolution contracts only, not invocation side-effects.
/// </summary>
[Trait("Category", "Phase37-04")]
public class DwCacheServiceRegistryTests
{
    [Fact]
    public void Resolve_ShortName_ReturnsEntry()
    {
        var entry = DwCacheServiceRegistry.Resolve("CountryService");

        Assert.NotNull(entry);
        Assert.Equal("CountryService", entry!.ShortName);
        Assert.Equal("Dynamicweb.Ecommerce.International.CountryService", entry.FullTypeName);
        Assert.NotNull(entry.Invoke);
    }

    [Fact]
    public void Resolve_FullTypeName_ReturnsSameEntry()
    {
        var byShort = DwCacheServiceRegistry.Resolve("CountryService");
        var byFull = DwCacheServiceRegistry.Resolve("Dynamicweb.Ecommerce.International.CountryService");

        Assert.NotNull(byShort);
        Assert.NotNull(byFull);
        Assert.Same(byShort, byFull);
    }

    [Fact]
    public void Resolve_Unknown_ReturnsNull()
    {
        var entry = DwCacheServiceRegistry.Resolve("NonexistentService");
        Assert.Null(entry);
    }

    [Fact]
    public void Resolve_CaseInsensitive_ShortName()
    {
        var lower = DwCacheServiceRegistry.Resolve("countryservice");
        var upper = DwCacheServiceRegistry.Resolve("COUNTRYSERVICE");
        var mixed = DwCacheServiceRegistry.Resolve("CoUnTrYsErViCe");

        Assert.NotNull(lower);
        Assert.NotNull(upper);
        Assert.NotNull(mixed);
        Assert.Same(lower, upper);
        Assert.Same(lower, mixed);
    }

    [Fact]
    public void Resolve_CaseInsensitive_FullName()
    {
        var lower = DwCacheServiceRegistry.Resolve("dynamicweb.ecommerce.international.countryservice");
        var exact = DwCacheServiceRegistry.Resolve("Dynamicweb.Ecommerce.International.CountryService");

        Assert.NotNull(lower);
        Assert.NotNull(exact);
        Assert.Same(lower, exact);
    }

    [Fact]
    public void Resolve_Null_ReturnsNull()
    {
        Assert.Null(DwCacheServiceRegistry.Resolve(null!));
    }

    [Fact]
    public void Resolve_Empty_ReturnsNull()
    {
        Assert.Null(DwCacheServiceRegistry.Resolve(""));
        Assert.Null(DwCacheServiceRegistry.Resolve("   "));
    }

    [Fact]
    public void AllSupportedNames_ContainsAtLeast10Services()
    {
        // Planner minimum: 10 services (the Swift 2.2 baseline's complete cache name inventory).
        // Each service has a short name + full name, so the count is >= 20.
        var names = DwCacheServiceRegistry.AllSupportedNames;
        Assert.True(names.Count >= 20,
            $"Expected >= 20 supported names (10 services * 2 forms each), got {names.Count}");
    }

    [Fact]
    public void AllSupportedNames_IncludesBaselineCacheNames()
    {
        // Concrete list pulled from swift2.2-baseline.json — the registry MUST cover
        // every cache name the baseline declares or deserializer runs will fail loud.
        var baselineNames = new[]
        {
            "Dynamicweb.Ecommerce.International.CountryRelationService",
            "Dynamicweb.Ecommerce.International.CountryService",
            "Dynamicweb.Ecommerce.International.CurrencyService",
            "Dynamicweb.Ecommerce.International.LanguageService",
            "Dynamicweb.SystemTools.TranslationLanguageService",
            "Dynamicweb.Ecommerce.International.VatGroupService",
            "Dynamicweb.Ecommerce.International.VatGroupCountryRelationService",
            "Dynamicweb.Ecommerce.Orders.PaymentService",
            "Dynamicweb.Ecommerce.Orders.ShippingService",
        };

        foreach (var name in baselineNames)
        {
            Assert.True(DwCacheServiceRegistry.Resolve(name) is not null,
                $"Baseline cache name '{name}' is not registered in DwCacheServiceRegistry");
        }
    }

    [Fact]
    public void AllSupportedNames_IsSorted()
    {
        var names = DwCacheServiceRegistry.AllSupportedNames.ToList();
        var sorted = names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        Assert.Equal(sorted, names);
    }

    [Fact]
    public void AllSupportedNames_HasNoDuplicates()
    {
        var names = DwCacheServiceRegistry.AllSupportedNames;
        var unique = names.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        Assert.Equal(unique.Count, names.Count);
    }

    [Fact]
    public void Resolve_AreaService_FromShortName()
    {
        var entry = DwCacheServiceRegistry.Resolve("AreaService");
        Assert.NotNull(entry);
        Assert.NotNull(entry!.Invoke);
    }
}
