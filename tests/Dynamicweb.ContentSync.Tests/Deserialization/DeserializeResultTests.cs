using Dynamicweb.ContentSync.Serialization;
using Xunit;

namespace Dynamicweb.ContentSync.Tests.Deserialization;

[Trait("Category", "Deserialization")]
public class DeserializeResultTests
{
    [Fact]
    public void Summary_WithCounts_FormatsCorrectly()
    {
        var result = new DeserializeResult { Created = 3, Updated = 2, Skipped = 1, Failed = 0 };

        Assert.Equal("Deserialization complete: 3 created, 2 updated, 1 skipped, 0 failed.", result.Summary);
    }

    [Fact]
    public void HasErrors_WhenFailedZeroAndNoErrors_ReturnsFalse()
    {
        var result = new DeserializeResult { Failed = 0, Errors = Array.Empty<string>() };

        Assert.False(result.HasErrors);
    }

    [Fact]
    public void HasErrors_WhenFailedGreaterThanZero_ReturnsTrue()
    {
        var result = new DeserializeResult { Failed = 1 };

        Assert.True(result.HasErrors);
    }

    [Fact]
    public void HasErrors_WhenErrorsNotEmpty_ReturnsTrue()
    {
        var result = new DeserializeResult { Failed = 0, Errors = new[] { "some error" } };

        Assert.True(result.HasErrors);
    }
}
