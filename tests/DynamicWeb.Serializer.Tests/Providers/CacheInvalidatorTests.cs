using DynamicWeb.Serializer.Providers;
using Moq;
using Xunit;

namespace DynamicWeb.Serializer.Tests.Providers;

[Trait("Category", "Phase15")]
public class CacheInvalidatorTests
{
    [Fact]
    public void InvalidateCaches_ValidCacheName_CallsClearCache()
    {
        var mockResolver = new Mock<ICacheResolver>();
        var mockCache = new Mock<ICacheInstance>();
        mockResolver.Setup(r => r.GetCacheType("SomeCache")).Returns(typeof(object));
        mockResolver.Setup(r => r.GetCacheInstance("SomeCache")).Returns(mockCache.Object);

        var invalidator = new CacheInvalidator(mockResolver.Object);
        invalidator.InvalidateCaches(new[] { "SomeCache" });

        mockCache.Verify(c => c.ClearCache(), Times.Once);
    }

    [Fact]
    public void InvalidateCaches_UnknownCacheType_LogsWarningAndSkips()
    {
        var mockResolver = new Mock<ICacheResolver>();
        mockResolver.Setup(r => r.GetCacheType("UnknownCache")).Returns((Type?)null);

        var invalidator = new CacheInvalidator(mockResolver.Object);
        var logMessages = new List<string>();

        invalidator.InvalidateCaches(new[] { "UnknownCache" }, msg => logMessages.Add(msg));

        Assert.Contains(logMessages, m => m.Contains("Cache type not found"));
        Assert.Contains(logMessages, m => m.Contains("UnknownCache"));
        mockResolver.Verify(r => r.GetCacheInstance(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void InvalidateCaches_NullInstance_LogsWarningAndSkips()
    {
        var mockResolver = new Mock<ICacheResolver>();
        mockResolver.Setup(r => r.GetCacheType("BadCache")).Returns(typeof(object));
        mockResolver.Setup(r => r.GetCacheInstance("BadCache")).Returns((ICacheInstance?)null);

        var invalidator = new CacheInvalidator(mockResolver.Object);
        var logMessages = new List<string>();

        invalidator.InvalidateCaches(new[] { "BadCache" }, msg => logMessages.Add(msg));

        Assert.Contains(logMessages, m => m.Contains("Could not create cache instance"));
        Assert.Contains(logMessages, m => m.Contains("BadCache"));
    }

    [Fact]
    public void InvalidateCaches_EmptyList_DoesNothing()
    {
        var mockResolver = new Mock<ICacheResolver>();
        var invalidator = new CacheInvalidator(mockResolver.Object);

        invalidator.InvalidateCaches(Array.Empty<string>());

        mockResolver.Verify(r => r.GetCacheType(It.IsAny<string>()), Times.Never);
        mockResolver.Verify(r => r.GetCacheInstance(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void InvalidateCaches_DuplicateCacheNames_ClearsEachOnce()
    {
        var mockResolver = new Mock<ICacheResolver>();
        var mockCache = new Mock<ICacheInstance>();
        mockResolver.Setup(r => r.GetCacheType("DupeCache")).Returns(typeof(object));
        mockResolver.Setup(r => r.GetCacheInstance("DupeCache")).Returns(mockCache.Object);

        var invalidator = new CacheInvalidator(mockResolver.Object);

        invalidator.InvalidateCaches(new[] { "DupeCache", "DupeCache", "DUPECACHE" });

        mockCache.Verify(c => c.ClearCache(), Times.Once);
    }

    [Fact]
    public void InvalidateCaches_LogsEachCacheCleared()
    {
        var mockResolver = new Mock<ICacheResolver>();
        var mockCacheA = new Mock<ICacheInstance>();
        var mockCacheB = new Mock<ICacheInstance>();
        mockResolver.Setup(r => r.GetCacheType("CacheA")).Returns(typeof(object));
        mockResolver.Setup(r => r.GetCacheInstance("CacheA")).Returns(mockCacheA.Object);
        mockResolver.Setup(r => r.GetCacheType("CacheB")).Returns(typeof(object));
        mockResolver.Setup(r => r.GetCacheInstance("CacheB")).Returns(mockCacheB.Object);

        var invalidator = new CacheInvalidator(mockResolver.Object);
        var logMessages = new List<string>();

        invalidator.InvalidateCaches(new[] { "CacheA", "CacheB" }, msg => logMessages.Add(msg));

        Assert.Contains(logMessages, m => m.Contains("Clearing cache:") && m.Contains("CacheA"));
        Assert.Contains(logMessages, m => m.Contains("Clearing cache:") && m.Contains("CacheB"));
        mockCacheA.Verify(c => c.ClearCache(), Times.Once);
        mockCacheB.Verify(c => c.ClearCache(), Times.Once);
    }
}
