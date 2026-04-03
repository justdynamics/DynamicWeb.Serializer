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
