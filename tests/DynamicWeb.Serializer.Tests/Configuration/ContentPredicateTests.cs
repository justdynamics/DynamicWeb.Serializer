using DynamicWeb.Serializer.Configuration;
using DynamicWeb.Serializer.Models;
using Xunit;

namespace DynamicWeb.Serializer.Tests.Configuration;

public class ContentPredicateTests
{
    // -------------------------------------------------------------------------
    // Single predicate — path matching
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("/Customer Center", 1, true)]        // exact match
    [InlineData("/Customer Center/Products", 1, true)]  // child path
    [InlineData("/Blog", 1, false)]                  // unrelated path
    [InlineData("/Customer Center", 2, false)]       // wrong areaId
    [InlineData("/Customer Center2", 1, false)]      // path boundary (not a child)
    [InlineData("/customer center/Products", 1, true)] // case-insensitive
    public void ShouldInclude_BasicPathMatching(string contentPath, int areaId, bool expected)
    {
        var definition = new ProviderPredicateDefinition
        {
            Name = "Customer Center",
            ProviderType = "Content",
            Path = "/Customer Center",
            AreaId = 1
        };
        var predicate = new ContentPredicate(definition);

        var result = predicate.ShouldInclude(contentPath, areaId);

        Assert.Equal(expected, result);
    }

    // -------------------------------------------------------------------------
    // Single predicate — exclude overrides include
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("/Customer Center/Archive", 1, false)]      // exact exclude match
    [InlineData("/Customer Center/Archive/Old", 1, false)]  // child of excluded path
    [InlineData("/Customer Center/Products", 1, true)]      // non-excluded sibling
    public void ShouldInclude_ExcludeOverridesInclude(string contentPath, int areaId, bool expected)
    {
        var definition = new ProviderPredicateDefinition
        {
            Name = "Customer Center",
            ProviderType = "Content",
            Path = "/Customer Center",
            AreaId = 1,
            Excludes = new List<string> { "/Customer Center/Archive" }
        };
        var predicate = new ContentPredicate(definition);

        var result = predicate.ShouldInclude(contentPath, areaId);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("/Customer Center/Archive", 1, false)]   // first exclude
    [InlineData("/Customer Center/Drafts", 1, false)]    // second exclude
    [InlineData("/Customer Center/Products", 1, true)]   // non-excluded child
    public void ShouldInclude_MultipleExcludes(string contentPath, int areaId, bool expected)
    {
        var definition = new ProviderPredicateDefinition
        {
            Name = "Customer Center",
            ProviderType = "Content",
            Path = "/Customer Center",
            AreaId = 1,
            Excludes = new List<string> { "/Customer Center/Archive", "/Customer Center/Drafts" }
        };
        var predicate = new ContentPredicate(definition);

        var result = predicate.ShouldInclude(contentPath, areaId);

        Assert.Equal(expected, result);
    }

    // -------------------------------------------------------------------------
    // ContentPredicateSet — OR logic across multiple predicates
    // -------------------------------------------------------------------------

    [Fact]
    public void ContentPredicateSet_IncludesPath_IfAnyPredicateMatches()
    {
        var config = new SerializerConfiguration
        {
            OutputDirectory = "/out",
            Predicates = new List<ProviderPredicateDefinition>
            {
                new() { Name = "Customer Center", ProviderType = "Content", Path = "/Customer Center", AreaId = 1 },
                new() { Name = "Blog", ProviderType = "Content", Path = "/Blog", AreaId = 2 }
            }
        };
        var set = new ContentPredicateSet(config);

        Assert.True(set.ShouldInclude("/Customer Center/Products", 1));
        Assert.True(set.ShouldInclude("/Blog/Post1", 2));
    }

    [Fact]
    public void ContentPredicateSet_ExcludesPath_IfNoPredicateMatches()
    {
        var config = new SerializerConfiguration
        {
            OutputDirectory = "/out",
            Predicates = new List<ProviderPredicateDefinition>
            {
                new() { Name = "Customer Center", ProviderType = "Content", Path = "/Customer Center", AreaId = 1 }
            }
        };
        var set = new ContentPredicateSet(config);

        Assert.False(set.ShouldInclude("/Blog", 1));
        Assert.False(set.ShouldInclude("/Customer Center", 99));  // wrong areaId
    }

    [Fact]
    public void ContentPredicateSet_ExcludeOverridesInclude_InAggregateSet()
    {
        var config = new SerializerConfiguration
        {
            OutputDirectory = "/out",
            Predicates = new List<ProviderPredicateDefinition>
            {
                new()
                {
                    Name = "Customer Center",
                    ProviderType = "Content",
                    Path = "/Customer Center",
                    AreaId = 1,
                    Excludes = new List<string> { "/Customer Center/Archive" }
                }
            }
        };
        var set = new ContentPredicateSet(config);

        Assert.False(set.ShouldInclude("/Customer Center/Archive", 1));
        Assert.True(set.ShouldInclude("/Customer Center/Products", 1));
    }
}
