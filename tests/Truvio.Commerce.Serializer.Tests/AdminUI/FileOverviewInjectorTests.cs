using Truvio.Commerce.Serializer.AdminUI.Injectors;
using Xunit;

namespace Truvio.Commerce.Serializer.Tests.AdminUI;

public class FileOverviewInjectorTests
{
    [Theory]
    [InlineData("/Files/System/Serializer/output.zip", "/System/Serializer", true)]
    [InlineData("/Files/System/Serializer/nested/data.zip", "/System/Serializer", true)]
    [InlineData("/Files/System/Serializer/output.zip", "\\System\\Serializer", true)]
    [InlineData("/Files/Other/output.zip", "/System/Serializer", false)]
    [InlineData("/Files/System/OtherPlugin/output.zip", "/System/Serializer", false)]
    public void IsPathUnderDirectory_MatchesCorrectly(string filePath, string directory, bool expected)
    {
        var result = SerializerFileOverviewInjector.IsPathUnderDirectory(filePath, directory);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null, "/System/Serializer")]
    [InlineData("", "/System/Serializer")]
    [InlineData("/Files/output.zip", null)]
    [InlineData("/Files/output.zip", "")]
    public void IsPathUnderDirectory_NullOrEmpty_ReturnsFalse(string? filePath, string? directory)
    {
        var result = SerializerFileOverviewInjector.IsPathUnderDirectory(filePath, directory);
        Assert.False(result);
    }

    [Theory]
    [InlineData(".zip", true)]
    [InlineData(".ZIP", true)]
    [InlineData(".Zip", true)]
    [InlineData(".ZiP", true)]
    public void IsZipExtension_ZipVariants_ReturnsTrue(string extension, bool expected)
    {
        var result = SerializerFileOverviewInjector.IsZipExtension(extension);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(".pdf", false)]
    [InlineData(".yml", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData(".tar.gz", false)]
    public void IsZipExtension_NonZip_ReturnsFalse(string? extension, bool expected)
    {
        var result = SerializerFileOverviewInjector.IsZipExtension(extension);
        Assert.Equal(expected, result);
    }
}
