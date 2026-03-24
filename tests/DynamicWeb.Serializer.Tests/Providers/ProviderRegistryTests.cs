using DynamicWeb.Serializer.Providers;
using Moq;
using Xunit;

namespace DynamicWeb.Serializer.Tests.Providers;

[Trait("Category", "Phase13")]
public class ProviderRegistryTests
{
    private readonly ProviderRegistry _registry = new();

    private static Mock<ISerializationProvider> CreateMockProvider(string providerType)
    {
        var mock = new Mock<ISerializationProvider>();
        mock.Setup(p => p.ProviderType).Returns(providerType);
        return mock;
    }

    [Fact]
    public void Register_And_GetProvider_ReturnsCorrectProvider()
    {
        var mock = CreateMockProvider("SqlTable");
        _registry.Register(mock.Object);

        var result = _registry.GetProvider("SqlTable");

        Assert.Same(mock.Object, result);
    }

    [Fact]
    public void GetProvider_CaseInsensitive()
    {
        var mock = CreateMockProvider("SqlTable");
        _registry.Register(mock.Object);

        var result = _registry.GetProvider("sqltable");

        Assert.Same(mock.Object, result);
    }

    [Fact]
    public void GetProvider_UnregisteredType_ThrowsInvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _registry.GetProvider("NonExistent"));

        Assert.Contains("NonExistent", ex.Message);
    }

    [Fact]
    public void HasProvider_ReturnsTrueForRegistered()
    {
        var mock = CreateMockProvider("SqlTable");
        _registry.Register(mock.Object);

        Assert.True(_registry.HasProvider("SqlTable"));
    }

    [Fact]
    public void HasProvider_ReturnsFalseForUnregistered()
    {
        Assert.False(_registry.HasProvider("Unknown"));
    }

    [Fact]
    public void RegisteredTypes_ReturnsAllKeys()
    {
        var sqlMock = CreateMockProvider("SqlTable");
        var contentMock = CreateMockProvider("Content");
        _registry.Register(sqlMock.Object);
        _registry.Register(contentMock.Object);

        var types = _registry.RegisteredTypes;

        Assert.Contains("SqlTable", types);
        Assert.Contains("Content", types);
        Assert.Equal(2, types.Count);
    }
}
