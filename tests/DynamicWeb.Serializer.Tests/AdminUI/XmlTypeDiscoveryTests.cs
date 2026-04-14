using DynamicWeb.Serializer.AdminUI.Infrastructure;
using DynamicWeb.Serializer.Tests.TestHelpers;
using Xunit;

namespace DynamicWeb.Serializer.Tests.AdminUI;

public class XmlTypeDiscoveryTests
{
    // -------------------------------------------------------------------------
    // DiscoverXmlTypes tests
    // -------------------------------------------------------------------------

    [Fact]
    public void DiscoverXmlTypes_ReturnsPageUrlDataProviders()
    {
        var executor = new FakeSqlExecutor();
        executor.AddMapping("PageUrlDataProvider",
            TestTableHelper.CreateSingleColumnTable("PageUrlDataProvider",
                "Dynamicweb.Content.Items.Providers.UrlDataProvider.UrlDataProvider"));
        executor.AddMapping("ParagraphModuleSystemName",
            TestTableHelper.CreateSingleColumnTable("ParagraphModuleSystemName"));

        var discovery = new XmlTypeDiscovery(executor);
        var types = discovery.DiscoverXmlTypes();

        Assert.Contains("Dynamicweb.Content.Items.Providers.UrlDataProvider.UrlDataProvider", types);
    }

    [Fact]
    public void DiscoverXmlTypes_ReturnsParagraphModuleTypes()
    {
        var executor = new FakeSqlExecutor();
        executor.AddMapping("PageUrlDataProvider",
            TestTableHelper.CreateSingleColumnTable("PageUrlDataProvider"));
        executor.AddMapping("ParagraphModuleSystemName",
            TestTableHelper.CreateSingleColumnTable("ParagraphModuleSystemName",
                "Dynamicweb.UserManagement.UserManagementSearchModule"));

        var discovery = new XmlTypeDiscovery(executor);
        var types = discovery.DiscoverXmlTypes();

        Assert.Contains("Dynamicweb.UserManagement.UserManagementSearchModule", types);
    }

    [Fact]
    public void DiscoverXmlTypes_DeduplicatesAcrossTables()
    {
        var executor = new FakeSqlExecutor();
        executor.AddMapping("PageUrlDataProvider",
            TestTableHelper.CreateSingleColumnTable("PageUrlDataProvider", "SharedType"));
        executor.AddMapping("ParagraphModuleSystemName",
            TestTableHelper.CreateSingleColumnTable("ParagraphModuleSystemName", "SharedType"));

        var discovery = new XmlTypeDiscovery(executor);
        var types = discovery.DiscoverXmlTypes();

        Assert.Single(types);
        Assert.Contains("SharedType", types);
    }

    [Fact]
    public void DiscoverXmlTypes_ExcludesEmptyTypeNames()
    {
        var executor = new FakeSqlExecutor();
        // SQL WHERE clause filters empty/null, but just in case the DB returns them
        executor.AddMapping("PageUrlDataProvider",
            TestTableHelper.CreateSingleColumnTable("PageUrlDataProvider", "ValidType", ""));
        executor.AddMapping("ParagraphModuleSystemName",
            TestTableHelper.CreateSingleColumnTable("ParagraphModuleSystemName"));

        var discovery = new XmlTypeDiscovery(executor);
        var types = discovery.DiscoverXmlTypes();

        Assert.Single(types);
        Assert.Contains("ValidType", types);
    }

    // -------------------------------------------------------------------------
    // DiscoverElementsForType tests
    // -------------------------------------------------------------------------

    [Fact]
    public void DiscoverElementsForType_ReturnsRootChildElements()
    {
        var executor = new FakeSqlExecutor();
        executor.AddMapping("PageUrlDataProviderParameters",
            TestTableHelper.CreateSingleColumnTable("PageUrlDataProviderParameters",
                "<settings><sort/><pagesize/><filtervalue/></settings>"));
        executor.AddMapping("ParagraphModuleSettings",
            TestTableHelper.CreateSingleColumnTable("ParagraphModuleSettings"));

        var discovery = new XmlTypeDiscovery(executor);
        var elements = discovery.DiscoverElementsForType("SomeValidType");

        Assert.Contains("sort", elements, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("pagesize", elements, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("filtervalue", elements, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void DiscoverElementsForType_SkipsMalformedXml()
    {
        var executor = new FakeSqlExecutor();
        executor.AddMapping("PageUrlDataProviderParameters",
            TestTableHelper.CreateSingleColumnTable("PageUrlDataProviderParameters",
                "NOT VALID XML <<>>",
                "<settings><goodElement/></settings>"));
        executor.AddMapping("ParagraphModuleSettings",
            TestTableHelper.CreateSingleColumnTable("ParagraphModuleSettings"));

        var discovery = new XmlTypeDiscovery(executor);
        var elements = discovery.DiscoverElementsForType("SomeValidType");

        Assert.Contains("goodElement", elements, StringComparer.OrdinalIgnoreCase);
        Assert.Single(elements); // only the valid blob's element
    }

    [Fact]
    public void DiscoverElementsForType_DeduplicatesCaseInsensitive()
    {
        var executor = new FakeSqlExecutor();
        executor.AddMapping("PageUrlDataProviderParameters",
            TestTableHelper.CreateSingleColumnTable("PageUrlDataProviderParameters",
                "<settings><Sort/></settings>",
                "<settings><sort/><extra/></settings>"));
        executor.AddMapping("ParagraphModuleSettings",
            TestTableHelper.CreateSingleColumnTable("ParagraphModuleSettings"));

        var discovery = new XmlTypeDiscovery(executor);
        var elements = discovery.DiscoverElementsForType("SomeValidType");

        // "Sort" and "sort" should be deduplicated (case-insensitive)
        Assert.Equal(2, elements.Count); // sort + extra
    }

    [Fact]
    public void DiscoverElementsForType_ReturnsEmptyForNoMatchingRows()
    {
        var executor = new FakeSqlExecutor();
        executor.AddMapping("PageUrlDataProviderParameters",
            TestTableHelper.CreateSingleColumnTable("PageUrlDataProviderParameters"));
        executor.AddMapping("ParagraphModuleSettings",
            TestTableHelper.CreateSingleColumnTable("ParagraphModuleSettings"));

        var discovery = new XmlTypeDiscovery(executor);
        var elements = discovery.DiscoverElementsForType("NonExistentType");

        Assert.Empty(elements);
    }

    [Fact]
    public void DiscoverElementsForType_RejectsInvalidTypeName()
    {
        var executor = new FakeSqlExecutor();
        var discovery = new XmlTypeDiscovery(executor);

        // SQL injection attempt
        var elements = discovery.DiscoverElementsForType("'; DROP TABLE Page; --");

        Assert.Empty(elements);
    }
}
