using System.Data;
using DynamicWeb.Serializer.Providers.SqlTable;
using Dynamicweb.Data;
using Moq;
using Xunit;

namespace DynamicWeb.Serializer.Tests.Providers.SqlTable;

/// <summary>
/// Phase 37-03 (FILTER-01): SqlTableReader composes SELECT ... WHERE {clause} when a
/// non-null whereClause is passed. The clause is trusted (validated upstream by
/// SqlWhereClauseValidator at config-load and at admin-UI save).
/// </summary>
public class SqlTableReaderWhereClauseTests
{
    [Fact]
    public void ReadAllRows_WithoutWhereClause_ComposesPlainSelect()
    {
        var mockExec = new Mock<ISqlExecutor>();
        CommandBuilder? captured = null;
        mockExec.Setup(x => x.ExecuteReader(It.IsAny<CommandBuilder>()))
            .Callback<CommandBuilder>(cb => captured = cb)
            .Returns(EmptyReader());

        var reader = new SqlTableReader(mockExec.Object);
        _ = reader.ReadAllRows("AccessUser").ToList();

        Assert.NotNull(captured);
        var sql = captured!.ToString();
        Assert.Contains("SELECT * FROM [AccessUser]", sql);
        Assert.DoesNotContain("WHERE", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadAllRows_WithWhereClause_ComposesSelectWithWhere()
    {
        var mockExec = new Mock<ISqlExecutor>();
        CommandBuilder? captured = null;
        mockExec.Setup(x => x.ExecuteReader(It.IsAny<CommandBuilder>()))
            .Callback<CommandBuilder>(cb => captured = cb)
            .Returns(EmptyReader());

        var reader = new SqlTableReader(mockExec.Object);
        _ = reader.ReadAllRows("AccessUser", "AccessUserType = 2").ToList();

        Assert.NotNull(captured);
        var sql = captured!.ToString();
        Assert.Contains("SELECT * FROM [AccessUser] WHERE AccessUserType = 2", sql);
    }

    [Fact]
    public void ReadAllRows_EmptyWhereClause_ComposesPlainSelect()
    {
        var mockExec = new Mock<ISqlExecutor>();
        CommandBuilder? captured = null;
        mockExec.Setup(x => x.ExecuteReader(It.IsAny<CommandBuilder>()))
            .Callback<CommandBuilder>(cb => captured = cb)
            .Returns(EmptyReader());

        var reader = new SqlTableReader(mockExec.Object);
        _ = reader.ReadAllRows("AccessUser", "").ToList();

        Assert.NotNull(captured);
        var sql = captured!.ToString();
        Assert.Contains("SELECT * FROM [AccessUser]", sql);
        Assert.DoesNotContain("WHERE", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadAllRows_WhereClauseIsTrustedAfterValidation_ComposedLiterally()
    {
        // Contract: whereClause passed in is assumed pre-validated (upstream guard at
        // config-load and admin-UI save). Reader composes it literally into SQL.
        var mockExec = new Mock<ISqlExecutor>();
        CommandBuilder? captured = null;
        mockExec.Setup(x => x.ExecuteReader(It.IsAny<CommandBuilder>()))
            .Callback<CommandBuilder>(cb => captured = cb)
            .Returns(EmptyReader());

        var reader = new SqlTableReader(mockExec.Object);
        _ = reader.ReadAllRows(
            "AccessUser",
            "AccessUserType = 2 AND AccessUserUserName IN ('Admin','Editors')").ToList();

        Assert.NotNull(captured);
        Assert.Contains(
            "WHERE AccessUserType = 2 AND AccessUserUserName IN ('Admin','Editors')",
            captured!.ToString());
    }

    private static IDataReader EmptyReader()
    {
        var mock = new Mock<IDataReader>();
        mock.Setup(r => r.Read()).Returns(false);
        mock.Setup(r => r.FieldCount).Returns(0);
        mock.Setup(r => r.Dispose());
        return mock.Object;
    }
}
