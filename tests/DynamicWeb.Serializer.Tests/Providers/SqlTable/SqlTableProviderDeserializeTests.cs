using System.Data;
using DynamicWeb.Serializer.Models;
using DynamicWeb.Serializer.Providers.SqlTable;
using Dynamicweb.Data;
using Moq;
using Xunit;

namespace DynamicWeb.Serializer.Tests.Providers.SqlTable;

[Trait("Category", "Phase13")]
public class SqlTableProviderDeserializeTests
{
    private static readonly TableMetadata TestMetadata = new()
    {
        TableName = "EcomOrderFlow",
        NameColumn = "OrderFlowName",
        KeyColumns = new[] { "OrderFlowId" },
        IdentityColumns = new[] { "OrderFlowId" },
        AllColumns = new[] { "OrderFlowId", "OrderFlowName", "OrderFlowDescription" }
    };

    private static readonly ProviderPredicateDefinition TestPredicate = new()
    {
        Name = "Order Flows",
        ProviderType = "SqlTable",
        Table = "EcomOrderFlow",
        NameColumn = "OrderFlowName"
    };

    [Fact]
    public void Deserialize_SkipsUnchangedRows()
    {
        var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["OrderFlowId"] = 1,
            ["OrderFlowName"] = "Checkout",
            ["OrderFlowDescription"] = "Main flow"
        };

        // Same row in DB — checksum will match, so it should be skipped
        var (provider, executor, writer, inputRoot) = CreateProviderWithFiles(
            yamlRows: new[] { row },
            existingDbRows: new[] { row });

        var result = provider.Deserialize(TestPredicate, inputRoot);

        Assert.Equal(0, result.Created);
        Assert.Equal(0, result.Updated);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(0, result.Failed);
    }

    [Fact]
    public void Deserialize_CreatesNewRows()
    {
        var yamlRow = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["OrderFlowId"] = 99,
            ["OrderFlowName"] = "NewFlow",
            ["OrderFlowDescription"] = "Brand new"
        };

        // No existing rows in DB
        var (provider, executor, writer, inputRoot) = CreateProviderWithFiles(
            yamlRows: new[] { yamlRow },
            existingDbRows: Array.Empty<Dictionary<string, object?>>());

        writer.Setup(w => w.WriteRow(It.IsAny<Dictionary<string, object?>>(), It.IsAny<TableMetadata>(), false))
            .Returns(WriteOutcome.Created);

        var result = provider.Deserialize(TestPredicate, inputRoot);

        Assert.Equal(1, result.Created);
        Assert.Equal(0, result.Skipped);
        Assert.Equal(0, result.Updated);
    }

    [Fact]
    public void Deserialize_UpdatesChangedRows()
    {
        var yamlRow = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["OrderFlowId"] = 1,
            ["OrderFlowName"] = "Checkout",
            ["OrderFlowDescription"] = "Updated description"
        };

        var existingRow = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["OrderFlowId"] = 1,
            ["OrderFlowName"] = "Checkout",
            ["OrderFlowDescription"] = "Old description"
        };

        var (provider, executor, writer, inputRoot) = CreateProviderWithFiles(
            yamlRows: new[] { yamlRow },
            existingDbRows: new[] { existingRow });

        writer.Setup(w => w.WriteRow(It.IsAny<Dictionary<string, object?>>(), It.IsAny<TableMetadata>(), false))
            .Returns(WriteOutcome.Updated);

        var result = provider.Deserialize(TestPredicate, inputRoot);

        Assert.Equal(0, result.Created);
        Assert.Equal(0, result.Skipped);
        Assert.Equal(1, result.Updated);
    }

    [Fact]
    public void Deserialize_DryRun_NoSqlWrites()
    {
        var yamlRow = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["OrderFlowId"] = 99,
            ["OrderFlowName"] = "NewFlow",
            ["OrderFlowDescription"] = "Brand new"
        };

        var (provider, executor, writer, inputRoot) = CreateProviderWithFiles(
            yamlRows: new[] { yamlRow },
            existingDbRows: Array.Empty<Dictionary<string, object?>>());

        writer.Setup(w => w.WriteRow(It.IsAny<Dictionary<string, object?>>(), It.IsAny<TableMetadata>(), true))
            .Returns(WriteOutcome.Created);

        var result = provider.Deserialize(TestPredicate, inputRoot, isDryRun: true);

        Assert.Equal(1, result.Created);
        // Verify WriteRow was called with isDryRun=true
        writer.Verify(w => w.WriteRow(It.IsAny<Dictionary<string, object?>>(), It.IsAny<TableMetadata>(), true), Times.Once);
        // ExecuteNonQuery should never have been called (dry run)
        executor.Verify(x => x.ExecuteNonQuery(It.IsAny<CommandBuilder>()), Times.Never);
    }

    [Fact]
    public void Deserialize_ReportsAccurateCounts()
    {
        // 3 rows: 1 new, 1 changed, 1 unchanged
        var newRow = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["OrderFlowId"] = 99,
            ["OrderFlowName"] = "NewFlow",
            ["OrderFlowDescription"] = "Brand new"
        };
        var changedRow = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["OrderFlowId"] = 1,
            ["OrderFlowName"] = "Checkout",
            ["OrderFlowDescription"] = "Updated description"
        };
        var unchangedRow = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["OrderFlowId"] = 2,
            ["OrderFlowName"] = "Return",
            ["OrderFlowDescription"] = "Return flow"
        };

        var existingRows = new[]
        {
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["OrderFlowId"] = 1,
                ["OrderFlowName"] = "Checkout",
                ["OrderFlowDescription"] = "Old description"
            },
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["OrderFlowId"] = 2,
                ["OrderFlowName"] = "Return",
                ["OrderFlowDescription"] = "Return flow"
            }
        };

        var yamlRows = new[] { newRow, changedRow, unchangedRow };

        var (provider, executor, writer, inputRoot) = CreateProviderWithFiles(
            yamlRows: yamlRows,
            existingDbRows: existingRows);

        // New row => Created, Changed row => Updated
        writer.Setup(w => w.WriteRow(
                It.Is<Dictionary<string, object?>>(r => r["OrderFlowName"]!.ToString() == "NewFlow"),
                It.IsAny<TableMetadata>(), false))
            .Returns(WriteOutcome.Created);
        writer.Setup(w => w.WriteRow(
                It.Is<Dictionary<string, object?>>(r => r["OrderFlowName"]!.ToString() == "Checkout"),
                It.IsAny<TableMetadata>(), false))
            .Returns(WriteOutcome.Updated);

        var result = provider.Deserialize(TestPredicate, inputRoot);

        Assert.Equal(1, result.Created);
        Assert.Equal(1, result.Updated);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(0, result.Failed);
    }

    #region Helper Methods

    /// <summary>
    /// Create a fully wired provider with YAML files written to a temp directory.
    /// Returns the provider, mocks, and the input root path.
    /// </summary>
    private static (SqlTableProvider provider, Mock<ISqlExecutor> executor, Mock<SqlTableWriter> writer, string inputRoot)
        CreateProviderWithFiles(
            IEnumerable<Dictionary<string, object?>> yamlRows,
            IEnumerable<Dictionary<string, object?>> existingDbRows)
    {
        var mockExecutor = new Mock<ISqlExecutor>();

        // DataGroupMetadataReader mock
        var mockMetadataReader = new Mock<DataGroupMetadataReader>(mockExecutor.Object) { CallBase = false };
        mockMetadataReader.Setup(x => x.GetTableMetadata(It.IsAny<ProviderPredicateDefinition>()))
            .Returns(TestMetadata);

        // Set up ReadAllRows to return existing DB rows via mock reader
        var existingList = existingDbRows.ToList();
        var dbReaderMock = CreateMockDataReader(
            new[] { "OrderFlowId", "OrderFlowName", "OrderFlowDescription" },
            existingList.Select(r => new object[]
            {
                r.GetValueOrDefault("OrderFlowId") ?? DBNull.Value,
                r.GetValueOrDefault("OrderFlowName") ?? DBNull.Value,
                r.GetValueOrDefault("OrderFlowDescription") ?? DBNull.Value
            }).ToArray());
        mockExecutor.Setup(x => x.ExecuteReader(It.IsAny<CommandBuilder>()))
            .Returns(dbReaderMock.Object);

        var tableReader = new SqlTableReader(mockExecutor.Object);
        var fileStore = new FlatFileStore();

        // Write YAML files to temp directory
        var tempDir = Path.Combine(Path.GetTempPath(), $"contentsync_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var mockExecForIdentity = new Mock<ISqlExecutor>();
        var identityReader = new SqlTableReader(mockExecForIdentity.Object);

        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in yamlRows)
        {
            var identity = identityReader.GenerateRowIdentity(row, TestMetadata);
            fileStore.WriteRow(tempDir, TestMetadata.TableName, identity, row, usedNames);
        }
        fileStore.WriteMeta(tempDir, TestMetadata.TableName, TestMetadata);

        // SqlTableWriter mock — pass required constructor arg
        var writerMock = new Mock<SqlTableWriter>(mockExecutor.Object) { CallBase = false };

        var provider = new SqlTableProvider(mockMetadataReader.Object, tableReader, fileStore, writerMock.Object);

        return (provider, mockExecutor, writerMock, tempDir);
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

    #endregion
}
