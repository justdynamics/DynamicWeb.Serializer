using DynamicWeb.Serializer.Serialization;
using Dynamicweb.Security.Permissions;
using Xunit;

namespace DynamicWeb.Serializer.Tests.Serialization;

public class PermissionDeserializationTests
{
    // -------------------------------------------------------------------------
    // ParseLevelName — all 6 known levels
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("none", PermissionLevel.None)]
    [InlineData("read", PermissionLevel.Read)]
    [InlineData("edit", PermissionLevel.Edit)]
    [InlineData("create", PermissionLevel.Create)]
    [InlineData("delete", PermissionLevel.Delete)]
    [InlineData("all", PermissionLevel.All)]
    public void ParseLevelName_ReturnsExpectedLevel(string name, PermissionLevel expected)
    {
        Assert.Equal(expected, PermissionMapper.ParseLevelName(name));
    }

    // -------------------------------------------------------------------------
    // ParseLevelName — case-insensitivity
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("NONE", PermissionLevel.None)]
    [InlineData("Read", PermissionLevel.Read)]
    [InlineData("EDIT", PermissionLevel.Edit)]
    [InlineData("Create", PermissionLevel.Create)]
    [InlineData("DELETE", PermissionLevel.Delete)]
    [InlineData("All", PermissionLevel.All)]
    public void ParseLevelName_IsCaseInsensitive(string name, PermissionLevel expected)
    {
        Assert.Equal(expected, PermissionMapper.ParseLevelName(name));
    }

    // -------------------------------------------------------------------------
    // ParseLevelName — unknown string throws ArgumentException
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("unknown")]
    [InlineData("readwrite")]
    [InlineData("")]
    public void ParseLevelName_ThrowsForUnknownString(string name)
    {
        Assert.Throws<ArgumentException>(() => PermissionMapper.ParseLevelName(name));
    }
}
