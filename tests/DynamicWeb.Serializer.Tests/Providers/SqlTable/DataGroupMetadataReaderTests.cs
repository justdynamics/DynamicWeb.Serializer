using System.Data;
using DynamicWeb.Serializer.Models;
using DynamicWeb.Serializer.Providers.SqlTable;
using Dynamicweb.Data;
using Moq;
using Xunit;

namespace DynamicWeb.Serializer.Tests.Providers.SqlTable;

[Trait("Category", "Phase13")]
public class DataGroupMetadataReaderTests
{
    [Fact]
    public void GetTableMetadata_BuildsFromPredicateAndSchema()
    {
        // Arrange
        var mockExecutor = new Mock<ISqlExecutor>();

        var pkReader = CreateMockReader(new[] { "COLUMN_NAME" }, new object[][] { new object[] { "OrderFlowId" } });
        var pkCalled = false;

        var idReader = CreateMockReader(new[] { "COLUMN_NAME" }, new object[][] { new object[] { "OrderFlowId" } });
        var idCalled = false;

        var allColReader = CreateSchemaReader(new[] { "OrderFlowId", "OrderFlowName", "OrderFlowDescription" });

        mockExecutor.Setup(x => x.ExecuteReader(It.IsAny<CommandBuilder>()))
            .Returns((CommandBuilder cb) =>
            {
                var sql = cb.ToString();
                if (!pkCalled && sql.Contains("sp_pkeys")) { pkCalled = true; return pkReader.Object; }
                if (!idCalled && sql.Contains("INFORMATION_SCHEMA")) { idCalled = true; return idReader.Object; }
                return allColReader.Object;
            });

        var reader = new DataGroupMetadataReader(mockExecutor.Object);

        var predicate = new ProviderPredicateDefinition
        {
            Name = "Order Flows",
            ProviderType = "SqlTable",
            Table = "EcomOrderFlow",
            NameColumn = "OrderFlowName",
            CompareColumns = ""
        };

        // Act
        var metadata = reader.GetTableMetadata(predicate);

        // Assert
        Assert.Equal("EcomOrderFlow", metadata.TableName);
        Assert.Equal("OrderFlowName", metadata.NameColumn);
        Assert.Equal("", metadata.CompareColumns);
        Assert.Contains("OrderFlowId", metadata.KeyColumns);
        Assert.Contains("OrderFlowId", metadata.IdentityColumns);
        Assert.Equal(3, metadata.AllColumns.Count);
    }

    [Fact]
    public void GetTableMetadata_ThrowsForMissingTable()
    {
        var mockExecutor = new Mock<ISqlExecutor>();
        var reader = new DataGroupMetadataReader(mockExecutor.Object);

        var predicate = new ProviderPredicateDefinition
        {
            Name = "Bad Predicate",
            ProviderType = "SqlTable",
            Table = null
        };

        Assert.Throws<InvalidOperationException>(() =>
            reader.GetTableMetadata(predicate));
    }

    private static Mock<IDataReader> CreateMockReader(string[] columns, object[][] rows)
    {
        var mock = new Mock<IDataReader>();
        var rowIndex = -1;

        mock.Setup(r => r.Read()).Returns(() =>
        {
            rowIndex++;
            return rowIndex < rows.Length;
        });

        mock.Setup(r => r[It.IsAny<string>()]).Returns((string col) =>
        {
            var colIndex = Array.IndexOf(columns, col);
            return rowIndex >= 0 && rowIndex < rows.Length && colIndex >= 0
                ? rows[rowIndex][colIndex]
                : DBNull.Value;
        });

        mock.Setup(r => r.FieldCount).Returns(columns.Length);
        for (int i = 0; i < columns.Length; i++)
        {
            var idx = i;
            mock.Setup(r => r.GetName(idx)).Returns(columns[idx]);
            mock.Setup(r => r.GetValue(idx)).Returns(() =>
                rowIndex >= 0 && rowIndex < rows.Length ? rows[rowIndex][idx] : DBNull.Value);
        }

        mock.Setup(r => r.Dispose());
        return mock;
    }

    private static Mock<IDataReader> CreateSchemaReader(string[] columnNames)
    {
        var mock = new Mock<IDataReader>();
        mock.Setup(r => r.Read()).Returns(false);
        mock.Setup(r => r.FieldCount).Returns(columnNames.Length);
        for (int i = 0; i < columnNames.Length; i++)
        {
            var idx = i;
            mock.Setup(r => r.GetName(idx)).Returns(columnNames[idx]);
        }
        mock.Setup(r => r.Dispose());
        return mock;
    }
}
