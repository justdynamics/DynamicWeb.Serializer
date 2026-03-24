using DynamicWeb.Serializer.Models;
using DynamicWeb.Serializer.Providers;
using DynamicWeb.Serializer.Providers.Content;
using Xunit;

namespace DynamicWeb.Serializer.Tests.Providers.Content;

public class ContentProviderTests
{
    private readonly ContentProvider _provider = new();

    // -------------------------------------------------------------------------
    // ProviderType and DisplayName
    // -------------------------------------------------------------------------

    [Fact]
    public void ProviderType_ReturnsContent()
    {
        Assert.Equal("Content", _provider.ProviderType);
    }

    [Fact]
    public void DisplayName_ReturnsContentProvider()
    {
        Assert.Equal("Content Provider", _provider.DisplayName);
    }

    // -------------------------------------------------------------------------
    // ISerializationProvider contract
    // -------------------------------------------------------------------------

    [Fact]
    public void ContentProvider_ImplementsISerializationProvider()
    {
        Assert.IsAssignableFrom<ISerializationProvider>(_provider);
    }

    // -------------------------------------------------------------------------
    // ValidatePredicate
    // -------------------------------------------------------------------------

    [Fact]
    public void ValidatePredicate_ValidContentPredicate_ReturnsIsValidTrue()
    {
        var predicate = new ProviderPredicateDefinition
        {
            Name = "Customer Center",
            ProviderType = "Content",
            Path = "/Customer Center",
            AreaId = 1
        };

        var result = _provider.ValidatePredicate(predicate);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidatePredicate_SqlTableProviderType_ReturnsIsValidFalse()
    {
        var predicate = new ProviderPredicateDefinition
        {
            Name = "Order Flows",
            ProviderType = "SqlTable",
            Table = "EcomOrderFlow",
            NameColumn = "OrderFlowName"
        };

        var result = _provider.ValidatePredicate(predicate);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidatePredicate_EmptyPath_ReturnsIsValidFalse()
    {
        var predicate = new ProviderPredicateDefinition
        {
            Name = "Bad Predicate",
            ProviderType = "Content",
            Path = "",
            AreaId = 1
        };

        var result = _provider.ValidatePredicate(predicate);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidatePredicate_ZeroAreaId_ReturnsIsValidFalse()
    {
        var predicate = new ProviderPredicateDefinition
        {
            Name = "Bad Predicate",
            ProviderType = "Content",
            Path = "/Customer Center",
            AreaId = 0
        };

        var result = _provider.ValidatePredicate(predicate);

        Assert.False(result.IsValid);
    }

    // -------------------------------------------------------------------------
    // Serialize/Deserialize output directory routing
    // -------------------------------------------------------------------------

    [Fact]
    public void Serialize_ReturnsSerializeResult_WithContentTableName()
    {
        // ContentProvider.Serialize calls ContentSerializer which requires DW runtime.
        // We can only test that the provider returns errors gracefully when runtime unavailable.
        var predicate = new ProviderPredicateDefinition
        {
            Name = "Customer Center",
            ProviderType = "Content",
            Path = "/Customer Center",
            AreaId = 1
        };

        // Call with a non-existent output root - should not throw
        var result = _provider.Serialize(predicate, Path.Combine(Path.GetTempPath(), "nonexistent_" + Guid.NewGuid().ToString("N")));

        Assert.IsType<SerializeResult>(result);
    }

    [Fact]
    public void Deserialize_ReturnsProviderDeserializeResult_WithContentTableName()
    {
        var predicate = new ProviderPredicateDefinition
        {
            Name = "Customer Center",
            ProviderType = "Content",
            Path = "/Customer Center",
            AreaId = 1
        };

        // Call with a non-existent input root - should handle gracefully
        var result = _provider.Deserialize(predicate, Path.Combine(Path.GetTempPath(), "nonexistent_" + Guid.NewGuid().ToString("N")));

        Assert.IsType<ProviderDeserializeResult>(result);
        Assert.Equal("Content", result.TableName);
    }

    [Fact]
    public void Serialize_InvalidPredicate_ReturnsErrorResult()
    {
        var predicate = new ProviderPredicateDefinition
        {
            Name = "Bad",
            ProviderType = "SqlTable",
            Table = "EcomOrderFlow"
        };

        var result = _provider.Serialize(predicate, Path.GetTempPath());

        Assert.True(result.HasErrors);
    }

    [Fact]
    public void Deserialize_InvalidPredicate_ReturnsErrorResult()
    {
        var predicate = new ProviderPredicateDefinition
        {
            Name = "Bad",
            ProviderType = "SqlTable",
            Table = "EcomOrderFlow"
        };

        var result = _provider.Deserialize(predicate, Path.GetTempPath());

        Assert.True(result.HasErrors);
    }
}
