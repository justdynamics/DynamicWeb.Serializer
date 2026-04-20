using DynamicWeb.Serializer.Infrastructure;
using DynamicWeb.Serializer.Providers;
using Xunit;

namespace DynamicWeb.Serializer.Tests.Providers;

/// <summary>
/// Phase 37-04 CACHE-01: CacheInvalidator rewritten around DwCacheServiceRegistry
/// (no more AddInManager / ICacheResolver). Unknown names must throw because
/// ConfigLoader is the validation choke point — an unknown name reaching
/// InvalidateCaches is a bug.
/// </summary>
[Trait("Category", "Phase37-04")]
public class CacheInvalidatorTests
{
    // ---------- Helpers ----------

    /// <summary>
    /// Build a CacheInvalidator that resolves against a caller-supplied lookup so tests
    /// can verify invocation without triggering the real DW ClearCache() side-effects.
    /// </summary>
    private static CacheInvalidator NewTestInvalidator(
        Func<string, DwCacheServiceRegistry.CacheClearEntry?> resolver)
        => new CacheInvalidator(resolver);

    private static DwCacheServiceRegistry.CacheClearEntry FakeEntry(string shortName, Action invoke)
        => new(shortName, $"Test.{shortName}", invoke);

    // ---------- Behavior ----------

    [Fact]
    public void InvalidateCaches_KnownName_InvokesAction()
    {
        var invoked = 0;
        var fake = FakeEntry("CountryService", () => invoked++);

        var invalidator = NewTestInvalidator(name =>
            name.Equals("CountryService", StringComparison.OrdinalIgnoreCase) ? fake : null);

        invalidator.InvalidateCaches(new[] { "CountryService" });

        Assert.Equal(1, invoked);
    }

    [Fact]
    public void InvalidateCaches_UnknownName_Throws()
    {
        var invalidator = NewTestInvalidator(_ => null);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            invalidator.InvalidateCaches(new[] { "MysteryCache" }));

        Assert.Contains("MysteryCache", ex.Message);
        Assert.Contains("DwCacheServiceRegistry", ex.Message);
    }

    [Fact]
    public void InvalidateCaches_LogsClearingMessage()
    {
        var fake = FakeEntry("CountryService", () => { });
        var invalidator = NewTestInvalidator(_ => fake);

        var logs = new List<string>();
        invalidator.InvalidateCaches(new[] { "CountryService" }, logs.Add);

        Assert.Contains(logs, l => l.Contains("Clearing cache") && l.Contains("CountryService"));
    }

    [Fact]
    public void InvalidateCaches_EmptyList_DoesNothing()
    {
        var invalidator = NewTestInvalidator(_ =>
            throw new InvalidOperationException("resolver should not be called for empty list"));

        invalidator.InvalidateCaches(Array.Empty<string>()); // no throw
    }

    [Fact]
    public void InvalidateCaches_DuplicateNames_InvokesOnce()
    {
        var invoked = 0;
        var fake = FakeEntry("DupeCache", () => invoked++);

        var invalidator = NewTestInvalidator(name =>
            name.Equals("DupeCache", StringComparison.OrdinalIgnoreCase) ? fake : null);

        invalidator.InvalidateCaches(new[] { "DupeCache", "DupeCache", "DUPECACHE" });

        Assert.Equal(1, invoked);
    }

    [Fact]
    public void InvalidateCaches_MultipleNames_InvokesEach()
    {
        var invokedA = 0;
        var invokedB = 0;
        var fakeA = FakeEntry("A", () => invokedA++);
        var fakeB = FakeEntry("B", () => invokedB++);

        var invalidator = NewTestInvalidator(name => name switch
        {
            "A" => fakeA,
            "B" => fakeB,
            _ => null,
        });

        invalidator.InvalidateCaches(new[] { "A", "B" });

        Assert.Equal(1, invokedA);
        Assert.Equal(1, invokedB);
    }

    [Fact]
    public void InvalidateCaches_DefaultConstructor_ResolvesAgainstRealRegistry()
    {
        // Smoke-test the production ctor: with a real DwCacheServiceRegistry, an
        // unknown name must still throw (fail-loud on config-load misconfiguration).
        var invalidator = new CacheInvalidator();
        Assert.Throws<InvalidOperationException>(() =>
            invalidator.InvalidateCaches(new[] { "NotARealCache" }));
    }
}
