using DynamicWeb.Serializer.Infrastructure;
using DynamicWeb.Serializer.Models;
using DynamicWeb.Serializer.Tests.Fixtures;
using Xunit;
using YamlDotNet.Serialization;

namespace DynamicWeb.Serializer.Tests.Infrastructure;

public class YamlRoundTripTests
{
    private readonly ISerializer _serializer = YamlConfiguration.BuildSerializer();
    private readonly IDeserializer _deserializer = YamlConfiguration.BuildDeserializer();

    [Theory]
    [InlineData("~")]
    [InlineData("Hello\r\nWorld")]
    [InlineData("<p>Hello &amp; World</p>")]
    [InlineData("\"quoted\"")]
    [InlineData("!important")]
    [InlineData("normal string")]
    [InlineData("")]
    public void Yaml_RoundTrips_TrickyString(string original)
    {
        // Create a page with the tricky string in Fields
        var page = ContentTreeBuilder.BuildSinglePage("Test");
        // Use record with to set Fields containing original
        page = page with { Fields = new Dictionary<string, object> { ["body"] = original } };

        var yaml = _serializer.Serialize(page);
        var result = _deserializer.Deserialize<SerializedPage>(yaml);

        Assert.Equal(original, result.Fields["body"]?.ToString());
    }

    [Fact]
    public void Yaml_RoundTrips_FullPage_WithPopulatedFields()
    {
        var page = ContentTreeBuilder.BuildSinglePage("Customer Center");
        page = page with
        {
            Fields = new Dictionary<string, object>
            {
                ["title"] = "Customer Center",
                ["body"] = "<h1>Welcome</h1>\r\n<p>Hello &amp; World</p>",
                ["cssClass"] = "~hero-banner",
                ["tag"] = "!important",
                ["quote"] = "She said \"hello\""
            }
        };

        var yaml = _serializer.Serialize(page);
        var result = _deserializer.Deserialize<SerializedPage>(yaml);

        Assert.Equal(page.Name, result.Name);
        Assert.Equal(page.PageUniqueId, result.PageUniqueId);
        foreach (var kvp in page.Fields)
            Assert.Equal(kvp.Value?.ToString(), result.Fields[kvp.Key]?.ToString());
    }

    [Fact]
    public void Yaml_RoundTrips_DictionaryFields_PreserveAllEntries()
    {
        var fields = new Dictionary<string, object>
        {
            ["stringField"] = "hello",
            ["intField"] = 42,
            ["boolField"] = true,
            ["htmlField"] = "<div>test</div>"
        };
        var page = ContentTreeBuilder.BuildSinglePage("Test") with { Fields = fields };

        var yaml = _serializer.Serialize(page);
        var result = _deserializer.Deserialize<SerializedPage>(yaml);

        Assert.Equal(fields.Count, result.Fields.Count);
        Assert.Equal("hello", result.Fields["stringField"]?.ToString());
    }

    [Fact]
    public void Yaml_RoundTrips_PageWithPermissions()
    {
        var page = ContentTreeBuilder.BuildSinglePageWithPermissions("Secured Page");

        var yaml = _serializer.Serialize(page);
        var result = _deserializer.Deserialize<SerializedPage>(yaml);

        Assert.Equal(2, result.Permissions.Count);

        Assert.Equal("Anonymous", result.Permissions[0].Owner);
        Assert.Equal("role", result.Permissions[0].OwnerType);
        Assert.Null(result.Permissions[0].OwnerId);
        Assert.Equal("none", result.Permissions[0].Level);
        Assert.Equal(1, result.Permissions[0].LevelValue);

        Assert.Equal("AuthenticatedFrontend", result.Permissions[1].Owner);
        Assert.Equal("role", result.Permissions[1].OwnerType);
        Assert.Null(result.Permissions[1].OwnerId);
        Assert.Equal("read", result.Permissions[1].Level);
        Assert.Equal(4, result.Permissions[1].LevelValue);
    }

    [Fact]
    public void Yaml_RoundTrips_PageWithSourcePageId()
    {
        var page = ContentTreeBuilder.BuildSinglePage("Test") with { SourcePageId = 42 };

        var yaml = _serializer.Serialize(page);
        var result = _deserializer.Deserialize<SerializedPage>(yaml);

        Assert.Equal(42, result.SourcePageId);
    }

    [Fact]
    public void Yaml_RoundTrips_ParagraphWithSourceParagraphId()
    {
        var para = new SerializedParagraph
        {
            ParagraphUniqueId = Guid.NewGuid(),
            SortOrder = 1,
            SourceParagraphId = 99,
            ItemType = "ContentModule"
        };

        var yaml = _serializer.Serialize(para);
        var result = _deserializer.Deserialize<SerializedParagraph>(yaml);

        Assert.Equal(99, result.SourceParagraphId);
    }

    [Fact]
    public void Yaml_Deserializes_PageWithoutSourcePageId_AsNull()
    {
        var page = ContentTreeBuilder.BuildSinglePage("Test");
        // SourcePageId is not set, defaults to null

        var yaml = _serializer.Serialize(page);
        var result = _deserializer.Deserialize<SerializedPage>(yaml);

        Assert.Null(result.SourcePageId);
    }

    [Fact]
    public void Yaml_Deserializes_ParagraphWithoutSourceParagraphId_AsNull()
    {
        var para = new SerializedParagraph
        {
            ParagraphUniqueId = Guid.NewGuid(),
            SortOrder = 1
        };
        // SourceParagraphId is not set, defaults to null

        var yaml = _serializer.Serialize(para);
        var result = _deserializer.Deserialize<SerializedParagraph>(yaml);

        Assert.Null(result.SourceParagraphId);
    }

    [Fact]
    public void Yaml_RoundTrips_PageWithAllNewFlatProperties()
    {
        var activeFrom = new DateTime(2025, 1, 15, 10, 0, 0, DateTimeKind.Utc);
        var activeTo = new DateTime(2025, 12, 31, 23, 59, 59, DateTimeKind.Utc);

        var page = ContentTreeBuilder.BuildSinglePage("Full Props") with
        {
            NavigationTag = "main-nav",
            ShortCut = "Default.aspx?ID=42",
            Hidden = true,
            Allowclick = false,
            Allowsearch = false,
            ShowInSitemap = false,
            ShowInLegend = false,
            SslMode = 2,
            ColorSchemeId = "dark-theme",
            ExactUrl = "/custom-url",
            ContentType = "text/html",
            TopImage = "/images/banner.jpg",
            DisplayMode = "List",
            ActiveFrom = activeFrom,
            ActiveTo = activeTo,
            PermissionType = 3
        };

        var yaml = _serializer.Serialize(page);
        var result = _deserializer.Deserialize<SerializedPage>(yaml);

        Assert.Equal("main-nav", result.NavigationTag);
        Assert.Equal("Default.aspx?ID=42", result.ShortCut);
        Assert.True(result.Hidden);
        Assert.False(result.Allowclick);
        Assert.False(result.Allowsearch);
        Assert.False(result.ShowInSitemap);
        Assert.False(result.ShowInLegend);
        Assert.Equal(2, result.SslMode);
        Assert.Equal("dark-theme", result.ColorSchemeId);
        Assert.Equal("/custom-url", result.ExactUrl);
        Assert.Equal("text/html", result.ContentType);
        Assert.Equal("/images/banner.jpg", result.TopImage);
        Assert.Equal("List", result.DisplayMode);
        Assert.Equal(activeFrom, result.ActiveFrom);
        Assert.Equal(activeTo, result.ActiveTo);
        Assert.Equal(3, result.PermissionType);
    }

    [Fact]
    public void Yaml_RoundTrips_PageWithSeoSubObject()
    {
        var page = ContentTreeBuilder.BuildSinglePage("SEO Test") with
        {
            Seo = new SerializedSeoSettings
            {
                MetaTitle = "My Page Title",
                MetaCanonical = "https://example.com/page",
                Description = "A test page description",
                Keywords = "test, page, seo",
                Noindex = true,
                Nofollow = true,
                Robots404 = true
            }
        };

        var yaml = _serializer.Serialize(page);
        var result = _deserializer.Deserialize<SerializedPage>(yaml);

        Assert.NotNull(result.Seo);
        Assert.Equal("My Page Title", result.Seo.MetaTitle);
        Assert.Equal("https://example.com/page", result.Seo.MetaCanonical);
        Assert.Equal("A test page description", result.Seo.Description);
        Assert.Equal("test, page, seo", result.Seo.Keywords);
        Assert.True(result.Seo.Noindex);
        Assert.True(result.Seo.Nofollow);
        Assert.True(result.Seo.Robots404);
    }

    [Fact]
    public void Yaml_RoundTrips_PageWithUrlSettings()
    {
        var page = ContentTreeBuilder.BuildSinglePage("URL Test") with
        {
            UrlSettings = new SerializedUrlSettings
            {
                UrlDataProviderTypeName = "CustomProvider",
                UrlDataProviderParameters = "param1=val1&param2=val2",
                UrlIgnoreForChildren = true,
                UrlUseAsWritten = true
            }
        };

        var yaml = _serializer.Serialize(page);
        var result = _deserializer.Deserialize<SerializedPage>(yaml);

        Assert.NotNull(result.UrlSettings);
        Assert.Equal("CustomProvider", result.UrlSettings.UrlDataProviderTypeName);
        Assert.Equal("param1=val1&param2=val2", result.UrlSettings.UrlDataProviderParameters);
        Assert.True(result.UrlSettings.UrlIgnoreForChildren);
        Assert.True(result.UrlSettings.UrlUseAsWritten);
    }

    [Fact]
    public void Yaml_RoundTrips_PageWithVisibility()
    {
        var page = ContentTreeBuilder.BuildSinglePage("Visibility Test") with
        {
            Visibility = new SerializedVisibilitySettings
            {
                HideForPhones = true,
                HideForTablets = true,
                HideForDesktops = false
            }
        };

        var yaml = _serializer.Serialize(page);
        var result = _deserializer.Deserialize<SerializedPage>(yaml);

        Assert.NotNull(result.Visibility);
        Assert.True(result.Visibility.HideForPhones);
        Assert.True(result.Visibility.HideForTablets);
        Assert.False(result.Visibility.HideForDesktops);
    }

    [Fact]
    public void Yaml_RoundTrips_PageWithNavigationSettings()
    {
        var page = ContentTreeBuilder.BuildSinglePage("Nav Test") with
        {
            NavigationSettings = new SerializedNavigationSettings
            {
                UseEcomGroups = true,
                ParentType = "Groups",
                Groups = "GROUP1",
                ShopID = "SHOP1",
                MaxLevels = 3,
                ProductPage = "Default.aspx?Id=42",
                NavigationProvider = "EcomNavProvider",
                IncludeProducts = true
            }
        };

        var yaml = _serializer.Serialize(page);
        var result = _deserializer.Deserialize<SerializedPage>(yaml);

        Assert.NotNull(result.NavigationSettings);
        Assert.True(result.NavigationSettings.UseEcomGroups);
        Assert.Equal("Groups", result.NavigationSettings.ParentType);
        Assert.Equal("GROUP1", result.NavigationSettings.Groups);
        Assert.Equal("SHOP1", result.NavigationSettings.ShopID);
        Assert.Equal(3, result.NavigationSettings.MaxLevels);
        Assert.Equal("Default.aspx?Id=42", result.NavigationSettings.ProductPage);
        Assert.Equal("EcomNavProvider", result.NavigationSettings.NavigationProvider);
        Assert.True(result.NavigationSettings.IncludeProducts);
    }

    [Fact]
    public void Yaml_BackwardCompat_OldYamlWithoutNewProperties_DeserializesCorrectly()
    {
        // Simulate old YAML that only has the original required fields
        var page = ContentTreeBuilder.BuildSinglePage("Old Page");

        var yaml = _serializer.Serialize(page);
        var result = _deserializer.Deserialize<SerializedPage>(yaml);

        // Boolean defaults should match DW defaults
        Assert.True(result.Allowclick);
        Assert.True(result.Allowsearch);
        Assert.True(result.ShowInSitemap);
        Assert.True(result.ShowInLegend);
        Assert.False(result.Hidden);

        // Nullable DateTime should be null
        Assert.Null(result.ActiveFrom);
        Assert.Null(result.ActiveTo);

        // Sub-objects should be null
        Assert.Null(result.Seo);
        Assert.Null(result.UrlSettings);
        Assert.Null(result.Visibility);
        Assert.Null(result.NavigationSettings);
    }

    [Fact]
    public void Yaml_RoundTrips_AreaWithItemTypeFields()
    {
        var area = new SerializedArea
        {
            AreaId = Guid.NewGuid(),
            Name = "Test Area",
            SortOrder = 1,
            ItemType = "AreaSettings",
            ItemFields = new Dictionary<string, object>
            {
                ["HeaderPage"] = "Default.aspx?ID=121",
                ["FooterPage"] = "Default.aspx?ID=122",
                ["MasterPage"] = "Default.aspx?ID=123",
                ["SiteName"] = "My Website"
            },
            Pages = new List<SerializedPage>()
        };

        var yaml = _serializer.Serialize(area);
        var result = _deserializer.Deserialize<SerializedArea>(yaml);

        Assert.Equal(area.ItemType, result.ItemType);
        Assert.Equal(area.ItemFields.Count, result.ItemFields.Count);
        Assert.Equal("Default.aspx?ID=121", result.ItemFields["HeaderPage"]?.ToString());
        Assert.Equal("My Website", result.ItemFields["SiteName"]?.ToString());
    }

    [Fact]
    public void Yaml_Serialization_IsDeterministic()
    {
        var page = ContentTreeBuilder.BuildSinglePage("Test") with
        {
            Fields = new Dictionary<string, object>
            {
                ["alpha"] = "first",
                ["beta"] = "second",
                ["gamma"] = "third"
            }
        };

        var yaml1 = _serializer.Serialize(page);
        var yaml2 = _serializer.Serialize(page);

        Assert.Equal(yaml1, yaml2);
    }
}
