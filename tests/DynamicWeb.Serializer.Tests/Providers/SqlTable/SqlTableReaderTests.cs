using System.Data;
using DynamicWeb.Serializer.Providers.SqlTable;
using Dynamicweb.Data;
using Moq;
using Xunit;

namespace DynamicWeb.Serializer.Tests.Providers.SqlTable;

[Trait("Category", "Phase13")]
public class SqlTableReaderTests
{
    [Fact]
    public void ReadAllRows_ReturnsRowDictionaries()
    {
        // Arrange
        var columns = new[] { "Id", "Name", "Value" };
        var rows = new object[][]
        {
            new object[] { 1, "Checkout", "Flow1" },
            new object[] { 2, "Return", "Flow2" }
        };

        var mockReader = CreateMockDataReader(columns, rows);
        var mockExecutor = new Mock<ISqlExecutor>();
        mockExecutor.Setup(x => x.ExecuteReader(It.IsAny<CommandBuilder>())).Returns(mockReader.Object);

        var reader = new SqlTableReader(mockExecutor.Object);

        // Act
        var result = reader.ReadAllRows("TestTable").ToList();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(1, result[0]["Id"]);
        Assert.Equal("Checkout", result[0]["Name"]);
        Assert.Equal("Flow1", result[0]["Value"]);
        Assert.Equal(2, result[1]["Id"]);
    }

    [Fact]
    public void ReadAllRows_MapsDbNullToNull()
    {
        // Arrange
        var columns = new[] { "Id", "Name" };
        var rows = new object[][]
        {
            new object[] { 1, DBNull.Value }
        };

        var mockReader = CreateMockDataReader(columns, rows);
        var mockExecutor = new Mock<ISqlExecutor>();
        mockExecutor.Setup(x => x.ExecuteReader(It.IsAny<CommandBuilder>())).Returns(mockReader.Object);

        var reader = new SqlTableReader(mockExecutor.Object);

        // Act
        var result = reader.ReadAllRows("TestTable").ToList();

        // Assert
        Assert.Single(result);
        Assert.Null(result[0]["Name"]);
    }

    private static Mock<IDataReader> CreateMockDataReader(string[] columns, object[][] rows)
    {
        var mock = new Mock<IDataReader>();
        var rowIndex = -1;

        mock.Setup(r => r.Read()).Returns(() =>
        {
            rowIndex++;
            return rowIndex < rows.Length;
        });

        mock.Setup(r => r.FieldCount).Returns(columns.Length);
        for (int i = 0; i < columns.Length; i++)
        {
            var idx = i;
            mock.Setup(r => r.GetName(idx)).Returns(columns[idx]);
            mock.Setup(r => r.GetValue(idx)).Returns(() =>
                rowIndex >= 0 && rowIndex < rows.Length ? rows[rowIndex][idx] : DBNull.Value);
        }

        mock.Setup(r => r[It.IsAny<string>()]).Returns((string col) =>
        {
            var colIndex = Array.IndexOf(columns, col);
            return rowIndex >= 0 && rowIndex < rows.Length && colIndex >= 0
                ? rows[rowIndex][colIndex]
                : DBNull.Value;
        });

        mock.Setup(r => r.Dispose());
        return mock;
    }
}
