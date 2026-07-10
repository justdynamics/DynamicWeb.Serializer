using Truvio.Commerce.Serializer.Infrastructure;
using Truvio.Commerce.Serializer.Models;
using Truvio.Commerce.Serializer.Serialization;
using Truvio.Commerce.Serializer.Tests.Fixtures;
using Dynamicweb.Security.Permissions;
using Xunit;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Truvio.Commerce.Serializer.Tests.Serialization;

public class PermissionSerializationTests
{
    private readonly ISerializer _serializer;
    private readonly IDeserializer _deserializer = YamlConfiguration.BuildDeserializer();

    public PermissionSerializationTests()
    {
        // Use a serializer that omits empty collections (matches FileSystemStore behavior)
        _serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .WithEventEmitter(next => new ForceStringScalarEmitter(next))
            .ConfigureDefaultValuesHandling(
                DefaultValuesHandling.OmitNull | DefaultValuesHandling.OmitEmptyCollections)
            .Build();
    }

    // -------------------------------------------------------------------------
    // GetLevelName tests
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(PermissionLevel.None, "none")]
    [InlineData(PermissionLevel.Read, "read")]
    [InlineData(PermissionLevel.Edit, "edit")]
    [InlineData(PermissionLevel.Create, "create")]
    [InlineData(PermissionLevel.Delete, "delete")]
    [InlineData(PermissionLevel.All, "all")]
    public void GetLevelName_ReturnsExpectedName(PermissionLevel level, string expected)
    {
        Assert.Equal(expected, PermissionMapper.GetLevelName(level));
    }

    // -------------------------------------------------------------------------
    // IsRole tests
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("Anonymous", true)]
    [InlineData("AuthenticatedBackend", true)]
    [InlineData("AuthenticatedFrontend", true)]
    [InlineData("Administrator", true)]
    [InlineData("1325", false)]
    [InlineData("SomeGroup", false)]
    public void IsRole_IdentifiesRolesCorrectly(string ownerId, bool expected)
    {
        Assert.Equal(expected, PermissionMapper.IsRole(ownerId));
    }

    // -------------------------------------------------------------------------
    // YAML output tests
    // -------------------------------------------------------------------------

    [Fact]
    public void Yaml_PageWithPermissions_ContainsPermissionsSection()
    {
        var page = ContentTreeBuilder.BuildSinglePageWithPermissions("Secured Page");

        var yaml = _serializer.Serialize(page);

        // Keys are double-quoted by ForceStringScalarEmitter
        Assert.Contains("permissions", yaml);
        Assert.Contains("owner", yaml);
        Assert.Contains("ownerType", yaml);
        Assert.Contains("level", yaml);
        Assert.Contains("levelValue", yaml);
    }

    [Fact]
    public void Yaml_PageWithoutPermissions_HasNoPermissionsKey()
    {
        var page = ContentTreeBuilder.BuildSinglePage("Open Page");

        var yaml = _serializer.Serialize(page);

        Assert.DoesNotContain("permissions", yaml);
    }

    [Fact]
    public void Yaml_GridRowWithoutPermissions_HasNoPermissionsKey()
    {
        var row = new SerializedGridRow { Id = Guid.NewGuid(), SortOrder = 1 };

        var yaml = _serializer.Serialize(row);

        Assert.DoesNotContain("permissions", yaml);
    }

    [Fact]
    public void Yaml_ParagraphWithoutPermissions_HasNoPermissionsKey()
    {
        var para = new SerializedParagraph { ParagraphUniqueId = Guid.NewGuid(), SortOrder = 1 };

        var yaml = _serializer.Serialize(para);

        Assert.DoesNotContain("permissions", yaml);
    }

    [Fact]
    public void Yaml_PermissionWithSubName_ContainsSubName()
    {
        var para = ContentTreeBuilder.BuildParagraphWithPermissions();

        var yaml = _serializer.Serialize(para);

        Assert.Contains("subName", yaml);
        Assert.Contains("Paragraph", yaml);
    }
}
