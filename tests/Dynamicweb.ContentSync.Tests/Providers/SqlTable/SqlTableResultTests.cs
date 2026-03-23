using Dynamicweb.ContentSync.Providers.SqlTable;
using Xunit;

namespace Dynamicweb.ContentSync.Tests.Providers.SqlTable;

[Trait("Category", "Phase13")]
public class SqlTableResultTests
{
    [Fact]
    public void Summary_FormatsCorrectly()
    {
        var result = new SqlTableResult
        {
            TableName = "EcomOrderFlow",
            Created = 3,
            Updated = 1,
            Skipped = 2,
            Failed = 0
        };

        Assert.Equal("EcomOrderFlow: 3 added, 1 updated, 2 skipped, 0 failed.", result.Summary);
    }

    [Fact]
    public void HasErrors_TrueWhenFailedGreaterThanZero()
    {
        var result = new SqlTableResult
        {
            TableName = "EcomOrderFlow",
            Failed = 1
        };

        Assert.True(result.HasErrors);
    }

    [Fact]
    public void HasErrors_TrueWhenErrorsNotEmpty()
    {
        var result = new SqlTableResult
        {
            TableName = "EcomOrderFlow",
            Failed = 0,
            Errors = new[] { "some error" }
        };

        Assert.True(result.HasErrors);
    }

    [Fact]
    public void HasErrors_FalseWhenNoFailuresOrErrors()
    {
        var result = new SqlTableResult
        {
            TableName = "EcomOrderFlow",
            Failed = 0
        };

        Assert.False(result.HasErrors);
    }

    [Fact]
    public void DefaultErrors_IsEmptyCollection()
    {
        var result = new SqlTableResult
        {
            TableName = "EcomOrderFlow"
        };

        Assert.Equal(0, result.Errors.Count);
    }
}
