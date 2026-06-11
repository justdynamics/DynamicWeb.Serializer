using Truvio.Commerce.Serializer.Configuration;
using Xunit;

namespace Truvio.Commerce.Serializer.Tests.Configuration;

public class FieldExclusionInspectorTests
{
    private static readonly Dictionary<string, List<string>> Empty = new();

    [Fact]
    public void Describe_EmptyDicts_ReturnsEmpty()
    {
        var result = FieldExclusionInspector.Describe(
            "Swift_Page", null,
            new[] { ((string?)"Swift_Text", (string?)"eCom_CartV2") },
            Empty, Empty);

        Assert.Empty(result);
    }

    [Fact]
    public void Describe_CartParagraph_ReportsXmlCarveOut()
    {
        var xmlExcludes = new Dictionary<string, List<string>>
        {
            ["eCom_CartV2"] = new() { "Mail1Recipient", "DefaultPaymentId", "DefaultShippingId" }
        };

        var result = FieldExclusionInspector.Describe(
            null, null,
            new[] { ((string?)null, (string?)"eCom_CartV2") },
            Empty, xmlExcludes);

        var carveOut = Assert.Single(result);
        Assert.Equal(CarveOutKind.XmlElements, carveOut.Kind);
        Assert.Equal("eCom_CartV2", carveOut.TypeName);
        Assert.Equal(3, carveOut.Count);
        Assert.Equal("eCom_CartV2 (3 settings)", carveOut.Label);
    }

    [Fact]
    public void Describe_TypeWithEmptyExclusionList_IsIgnored()
    {
        // Scan-discovered types land with empty lists — nothing is actually excluded.
        var xmlExcludes = new Dictionary<string, List<string>>
        {
            ["eCom_Catalog"] = new()
        };

        var result = FieldExclusionInspector.Describe(
            null, null,
            new[] { ((string?)null, (string?)"eCom_Catalog") },
            Empty, xmlExcludes);

        Assert.Empty(result);
    }

    [Fact]
    public void Describe_TypeLookup_IsCaseInsensitive()
    {
        var xmlExcludes = new Dictionary<string, List<string>>
        {
            ["eCom_CartV2"] = new() { "Mail1Recipient" }
        };

        var result = FieldExclusionInspector.Describe(
            null, null,
            new[] { ((string?)null, (string?)"ECOM_CARTV2") },
            Empty, xmlExcludes);

        var carveOut = Assert.Single(result);
        Assert.Equal(1, carveOut.Count);
        Assert.EndsWith("(1 setting)", carveOut.Label);
    }

    [Fact]
    public void Describe_PageItemTypeAndUrlProvider_BothReported()
    {
        var fieldExcludes = new Dictionary<string, List<string>>
        {
            ["Swift_Page"] = new() { "InternalNote", "DebugMarker" }
        };
        var xmlExcludes = new Dictionary<string, List<string>>
        {
            ["Dynamicweb.Ecommerce.Frontend.UrlHandling.ShopUrlDataProvider, Dynamicweb.Ecommerce"] = new() { "ShopId" }
        };

        var result = FieldExclusionInspector.Describe(
            "Swift_Page",
            "Dynamicweb.Ecommerce.Frontend.UrlHandling.ShopUrlDataProvider, Dynamicweb.Ecommerce",
            Array.Empty<(string?, string?)>(),
            fieldExcludes, xmlExcludes);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, c => c.Kind == CarveOutKind.ItemTypeFields && c.TypeName == "Swift_Page" && c.Label == "Swift_Page (2 fields)");
        Assert.Contains(result, c => c.Kind == CarveOutKind.XmlElements && c.TypeName.Contains("ShopUrlDataProvider"));
    }

    [Fact]
    public void Describe_RepeatedType_ReportedOnce()
    {
        var xmlExcludes = new Dictionary<string, List<string>>
        {
            ["eCom_CartV2"] = new() { "Mail1Recipient" }
        };

        var result = FieldExclusionInspector.Describe(
            null, null,
            new[]
            {
                ((string?)null, (string?)"eCom_CartV2"),
                ((string?)null, (string?)"eCom_CartV2")
            },
            Empty, xmlExcludes);

        Assert.Single(result);
    }
}
