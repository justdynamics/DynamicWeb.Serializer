using System.Data;
using DynamicWeb.Serializer.Infrastructure;
using DynamicWeb.Serializer.Models;
using DynamicWeb.Serializer.Providers.SqlTable;
using Dynamicweb.Data;
using Moq;
using Xunit;

namespace DynamicWeb.Serializer.Tests.Providers.SqlTable;

/// <summary>
/// Proves Phase 37-02 consolidation: SqlTableProvider's Deserialize path delegates
/// every type coercion + target-column filter call to the shared TargetSchemaCache,
/// and the duplicate CoerceRowTypes / IsStringType helpers are gone.
/// </summary>
[Trait("Category", "Phase37-02")]
public class SqlTableProviderCoercionTests
{
    private static readonly TableMetadata TestMetadata = new()
    {
        TableName = "EcomOrderFlow",
        NameColumn = "OrderFlowName",
        KeyColumns = new List<string> { "OrderFlowId" },
        IdentityColumns = new List<string> { "OrderFlowId" },
        AllColumns = new List<string> { "OrderFlowId", "OrderFlowName", "OrderFlowDescription" }
    };

    private static readonly ProviderPredicateDefinition TestPredicate = new()
    {
        Name = "Order Flows",
        ProviderType = "SqlTable",
        Table = "EcomOrderFlow",
        NameColumn = "OrderFlowName"
    };

    // -------------------------------------------------------------------------
    // Column filtering (target-missing column warning + exclusion)
    // -------------------------------------------------------------------------

    [Fact]
    public void Deserialize_SourceHasExtraColumn_MissingOnTarget_WarnsOnce_And_StripsColumn()
    {
        // Source row has two columns; target schema only knows OrderFlowId + OrderFlowName.
        var yamlRow = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["OrderFlowId"] = 99,
            ["OrderFlowName"] = "NewFlow",
            ["ExtraColumnMissingOnTarget"] = "some value"
        };

        // Second row shares the missing column — to prove the warning is logged ONCE, not twice
        var yamlRow2 = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["OrderFlowId"] = 100,
            ["OrderFlowName"] = "AnotherFlow",
            ["ExtraColumnMissingOnTarget"] = "other value"
        };

        var schemaCache = new TargetSchemaCache(tableName =>
        {
            var types = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["OrderFlowId"] = "int",
                ["OrderFlowName"] = "nvarchar"
            };
            return (new HashSet<string>(types.Keys, StringComparer.OrdinalIgnoreCase), types);
        });

        var logged = new List<string>();
        Dictionary<string, object?>? capturedRow = null;

        var (provider, executor, writer, inputRoot) = CreateProviderWithFiles(
            yamlRows: new[] { yamlRow, yamlRow2 },
            existingDbRows: Array.Empty<Dictionary<string, object?>>(),
            schemaCache: schemaCache);

        writer.Setup(w => w.WriteRow(
                It.IsAny<Dictionary<string, object?>>(),
                It.IsAny<TableMetadata>(),
                false,
                It.IsAny<Action<string>?>(),
                It.IsAny<HashSet<string>?>()))
            .Returns((Dictionary<string, object?> r, TableMetadata _, bool _, Action<string>? _, HashSet<string>? _) =>
            {
                capturedRow ??= new Dictionary<string, object?>(r, StringComparer.OrdinalIgnoreCase);
                return WriteOutcome.Created;
            });

        var result = provider.Deserialize(TestPredicate, inputRoot, log: logged.Add);

        // Unknown column stripped before MERGE command composition
        Assert.NotNull(capturedRow);
        Assert.False(capturedRow!.ContainsKey("ExtraColumnMissingOnTarget"),
            "Row passed to SqlTableWriter must not contain target-missing column");
        Assert.True(capturedRow.ContainsKey("OrderFlowId"));
        Assert.True(capturedRow.ContainsKey("OrderFlowName"));

        // Warning logged exactly once across both rows (LogMissingColumnOnce dedupes per (table,column))
        var missingWarnings = logged.Where(l =>
            l.Contains("ExtraColumnMissingOnTarget", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.Single(missingWarnings);
    }

    [Fact]
    public void Deserialize_CoercionDelegated_StringDateTime_BecomesDateTimeInRow()
    {
        // Test metadata with a datetime column so the coercion path runs
        var metadataWithDate = new TableMetadata
        {
            TableName = "TestTable",
            NameColumn = "Name",
            KeyColumns = new List<string> { "Id" },
            IdentityColumns = new List<string>(),
            AllColumns = new List<string> { "Id", "Name", "CreatedDate" }
        };
        var predicate = new ProviderPredicateDefinition
        {
            Name = "Test",
            ProviderType = "SqlTable",
            Table = "TestTable",
            NameColumn = "Name"
        };

        var yamlRow = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Id"] = 1,
            ["Name"] = "Row1",
            ["CreatedDate"] = "2021-01-04T15:53:06.0730000"
        };

        var schemaCache = new TargetSchemaCache(tableName =>
        {
            var types = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Id"] = "int",
                ["Name"] = "nvarchar",
                ["CreatedDate"] = "datetime"
            };
            return (new HashSet<string>(types.Keys, StringComparer.OrdinalIgnoreCase), types);
        });

        Dictionary<string, object?>? capturedRow = null;

        var (provider, _, writer, inputRoot) = CreateProviderWithFiles(
            yamlRows: new[] { yamlRow },
            existingDbRows: Array.Empty<Dictionary<string, object?>>(),
            schemaCache: schemaCache,
            metadata: metadataWithDate,
            existingRowColumns: new[] { "Id", "Name", "CreatedDate" });

        writer.Setup(w => w.WriteRow(
                It.IsAny<Dictionary<string, object?>>(),
                It.IsAny<TableMetadata>(),
                false,
                It.IsAny<Action<string>?>(),
                It.IsAny<HashSet<string>?>()))
            .Returns((Dictionary<string, object?> r, TableMetadata _, bool _, Action<string>? _, HashSet<string>? _) =>
            {
                capturedRow = new Dictionary<string, object?>(r, StringComparer.OrdinalIgnoreCase);
                return WriteOutcome.Created;
            });

        _ = provider.Deserialize(predicate, inputRoot);

        Assert.NotNull(capturedRow);
        Assert.IsType<DateTime>(capturedRow!["CreatedDate"]);
    }

    #region Helper Methods

    private static (SqlTableProvider provider, Mock<ISqlExecutor> executor, Mock<SqlTableWriter> writer, string inputRoot)
        CreateProviderWithFiles(
            IEnumerable<Dictionary<string, object?>> yamlRows,
            IEnumerable<Dictionary<string, object?>> existingDbRows,
            TargetSchemaCache schemaCache,
            TableMetadata? metadata = null,
            string[]? existingRowColumns = null)
    {
        var md = metadata ?? TestMetadata;
        var cols = existingRowColumns ?? new[] { "OrderFlowId", "OrderFlowName", "OrderFlowDescription" };
        var mockExecutor = new Mock<ISqlExecutor>();

        var mockMetadataReader = new Mock<DataGroupMetadataReader>(mockExecutor.Object) { CallBase = false };
        mockMetadataReader.Setup(x => x.GetTableMetadata(It.IsAny<ProviderPredicateDefinition>(), It.IsAny<bool>()))
            .Returns(md);
        mockMetadataReader.Setup(x => x.TableExists(It.IsAny<string>())).Returns(true);
        mockMetadataReader.Setup(x => x.GetColumnTypes(It.IsAny<string>()))
            .Returns(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        mockMetadataReader.Setup(x => x.GetNotNullColumns(It.IsAny<string>()))
            .Returns(new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        var existingList = existingDbRows.ToList();
        var dbReaderMock = CreateMockDataReader(
            cols,
            existingList.Select(r => cols.Select(c => r.GetValueOrDefault(c) ?? DBNull.Value).ToArray()).ToArray());
        mockExecutor.Setup(x => x.ExecuteReader(It.IsAny<CommandBuilder>()))
            .Returns(dbReaderMock.Object);

        var tableReader = new SqlTableReader(mockExecutor.Object);
        var fileStore = new FlatFileStore();

        var tempDir = Path.Combine(Path.GetTempPath(), $"contentsync_coerce_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var mockExecForIdentity = new Mock<ISqlExecutor>();
        var identityReader = new SqlTableReader(mockExecForIdentity.Object);
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in yamlRows)
        {
            var identity = identityReader.GenerateRowIdentity(row, md);
            fileStore.WriteRow(tempDir, md.TableName, identity, row, usedNames);
        }
        fileStore.WriteMeta(tempDir, md.TableName, md);

        var writerMock = new Mock<SqlTableWriter>(mockExecutor.Object) { CallBase = false };

        var provider = new SqlTableProvider(
            mockMetadataReader.Object, tableReader, fileStore, writerMock.Object, schemaCache);

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
