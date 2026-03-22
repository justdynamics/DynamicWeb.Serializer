using Dynamicweb.ContentSync.Infrastructure;
using Dynamicweb.ContentSync.Models;
using Dynamicweb.ContentSync.Serialization;
using Dynamicweb.ContentSync.Tests.Fixtures;
using Dynamicweb.Security.Permissions;
using Xunit;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Dynamicweb.ContentSync.Tests.Serialization;

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
}
