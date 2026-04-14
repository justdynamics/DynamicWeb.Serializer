using System.Data;
using DynamicWeb.Serializer.AdminUI.Infrastructure;
using DynamicWeb.Serializer.Providers.SqlTable;
using Dynamicweb.Data;
using Xunit;

namespace DynamicWeb.Serializer.Tests.AdminUI;

public class XmlTypeDiscoveryTests
{
    /// <summary>
    /// Minimal ISqlExecutor that maps SQL query substrings to canned DataTable results.
    /// </summary>
    private sealed class FakeSqlExecutor : ISqlExecutor
    {
        private readonly List<(string Substring, DataTable Result)> _mappings = new();

        public void AddMapping(string querySubstring, DataTable result)
        {
            _mappings.Add((querySubstring, result));
        }

        public IDataReader ExecuteReader(CommandBuilder command)
        {
            var sql = command.ToString() ?? string.Empty;
            foreach (var (substring, result) in _mappings)
            {
                if (sql.Contains(substring, StringComparison.OrdinalIgnoreCase))
                    return result.CreateDataReader();
            }
            // Return empty reader for unmatched queries
            return new DataTable().CreateDataReader();
        }

        public int ExecuteNonQuery(CommandBuilder command) => 0;
    }

    private static DataTable CreateSingleColumnTable(string columnName, params string[] values)
    {
        var dt = new DataTable();
        dt.Columns.Add(columnName, typeof(string));
        foreach (var v in values)
            dt.Rows.Add(v);
        return dt;
    }

    // -------------------------------------------------------------------------
    // DiscoverXmlTypes tests
    // -------------------------------------------------------------------------

    [Fact]
    public void DiscoverXmlTypes_ReturnsPageUrlDataProviderTypes()
    {
        var executor = new FakeSqlExecutor();
        executor.AddMapping("PageUrlDataProviderType",
            CreateSingleColumnTable("PageUrlDataProviderType",
                "Dynamicweb.Content.Items.Providers.UrlDataProvider.UrlDataProvider"));
        executor.AddMapping("ParagraphModuleSystemName",
            CreateSingleColumnTable("ParagraphModuleSystemName"));

        var discovery = new XmlTypeDiscovery(executor);
        var types = discovery.DiscoverXmlTypes();

        Assert.Contains("Dynamicweb.Content.Items.Providers.UrlDataProvider.UrlDataProvider", types);
    }

    [Fact]
    public void DiscoverXmlTypes_ReturnsParagraphModuleTypes()
    {
        var executor = new FakeSqlExecutor();
        executor.AddMapping("PageUrlDataProviderType",
            CreateSingleColumnTable("PageUrlDataProviderType"));
        executor.AddMapping("ParagraphModuleSystemName",
            CreateSingleColumnTable("ParagraphModuleSystemName",
                "Dynamicweb.UserManagement.UserManagementSearchModule"));

        var discovery = new XmlTypeDiscovery(executor);
        var types = discovery.DiscoverXmlTypes();

        Assert.Contains("Dynamicweb.UserManagement.UserManagementSearchModule", types);
    }

    [Fact]
    public void DiscoverXmlTypes_DeduplicatesAcrossTables()
    {
        var executor = new FakeSqlExecutor();
        executor.AddMapping("PageUrlDataProviderType",
            CreateSingleColumnTable("PageUrlDataProviderType", "SharedType"));
        executor.AddMapping("ParagraphModuleSystemName",
            CreateSingleColumnTable("ParagraphModuleSystemName", "SharedType"));

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
        executor.AddMapping("PageUrlDataProviderType",
            CreateSingleColumnTable("PageUrlDataProviderType", "ValidType", ""));
        executor.AddMapping("ParagraphModuleSystemName",
            CreateSingleColumnTable("ParagraphModuleSystemName"));

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
            CreateSingleColumnTable("PageUrlDataProviderParameters",
                "<settings><sort/><pagesize/><filtervalue/></settings>"));
        executor.AddMapping("ParagraphModuleSettings",
            CreateSingleColumnTable("ParagraphModuleSettings"));

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
            CreateSingleColumnTable("PageUrlDataProviderParameters",
                "NOT VALID XML <<>>",
                "<settings><goodElement/></settings>"));
        executor.AddMapping("ParagraphModuleSettings",
            CreateSingleColumnTable("ParagraphModuleSettings"));

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
            CreateSingleColumnTable("PageUrlDataProviderParameters",
                "<settings><Sort/></settings>",
                "<settings><sort/><extra/></settings>"));
        executor.AddMapping("ParagraphModuleSettings",
            CreateSingleColumnTable("ParagraphModuleSettings"));

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
            CreateSingleColumnTable("PageUrlDataProviderParameters"));
        executor.AddMapping("ParagraphModuleSettings",
            CreateSingleColumnTable("ParagraphModuleSettings"));

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
