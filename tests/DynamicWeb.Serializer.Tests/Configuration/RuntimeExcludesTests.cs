using DynamicWeb.Serializer.Configuration;
using Xunit;

namespace DynamicWeb.Serializer.Tests.Configuration;

public class RuntimeExcludesTests
{
    [Fact]
    public void GetAutoExcludedColumns_UrlPath_IncludesVisitsCount()
    {
        var cols = RuntimeExcludes.GetAutoExcludedColumns("UrlPath");

        Assert.Contains("UrlPathVisitsCount", cols, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetAutoExcludedColumns_EcomShops_IncludesIndexFields()
    {
        var cols = RuntimeExcludes.GetAutoExcludedColumns("EcomShops");

        Assert.Contains("ShopIndexRepository", cols, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("ShopIndexName", cols, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("ShopIndexDocumentType", cols, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetAutoExcludedColumns_UnknownTable_ReturnsEmpty()
    {
        var cols = RuntimeExcludes.GetAutoExcludedColumns("NotATable");

        Assert.Empty(cols);
    }

    [Fact]
    public void GetAutoExcludedColumns_CaseInsensitive_ReturnsSameList()
    {
        var lower = RuntimeExcludes.GetAutoExcludedColumns("ecomshops");
        var upper = RuntimeExcludes.GetAutoExcludedColumns("ECOMSHOPS");
        var mixed = RuntimeExcludes.GetAutoExcludedColumns("EcomShops");

        Assert.Equal(mixed.Count, lower.Count);
        Assert.Equal(mixed.Count, upper.Count);
    }

    [Fact]
    public void All_ContainsAllKnownTables()
    {
        var all = RuntimeExcludes.All;

        Assert.Contains("UrlPath", all.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("EcomShops", all.Keys, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void AutoExcluded_MinusIncludeFields_HonorsUserOptIn()
    {
        var baseList = RuntimeExcludes.GetAutoExcludedColumns("EcomShops").ToList();
        var includeFields = new List<string> { "ShopIndexRepository" };

        var effective = baseList
            .Except(includeFields, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.Equal(baseList.Count - 1, effective.Count);
        Assert.DoesNotContain("ShopIndexRepository", effective, StringComparer.OrdinalIgnoreCase);
    }
}
