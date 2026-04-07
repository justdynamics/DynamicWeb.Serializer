using System.Data;
using DynamicWeb.Serializer.Models;
using DynamicWeb.Serializer.Providers.SqlTable;
using Moq;
using Xunit;

namespace DynamicWeb.Serializer.Tests.Providers.SqlTable;

[Trait("Category", "Phase29")]
public class SqlTableProviderSerializeTests
{
    private static readonly TableMetadata TestMetadata = new()
    {
        TableName = "TestTable",
        NameColumn = "Name",
        KeyColumns = new List<string> { "Id" },
        IdentityColumns = new List<string> { "Id" },
        AllColumns = new List<string> { "Id", "Name", "Description", "SettingsXml" }
    };

    [Fact]
    public void Serialize_ExcludeFields_OmitsColumns()
    {
        var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Id"] = 1,
            ["Name"] = "Test",
            ["Description"] = "To be excluded",
            ["SettingsXml"] = "<root />"
        };

        var predicate = new ProviderPredicateDefinition
        {
            Name = "Test",
            ProviderType = "SqlTable",
            Table = "TestTable",
            NameColumn = "Name",
            ExcludeFields = new List<string> { "Description" }
        };

        var (provider, _, outputRoot) = CreateProviderForSerialize(new[] { row });

        var result = provider.Serialize(predicate, outputRoot);

        Assert.Equal(1, result.RowsSerialized);

        // Read the written YAML file and verify excluded column is absent
        var fileStore = new FlatFileStore();
        var rows = fileStore.ReadAllRows(outputRoot, "TestTable").ToList();
        Assert.Single(rows);
        Assert.False(rows[0].ContainsKey("Description"), "Excluded field 'Description' should not be in serialized output");
        Assert.True(rows[0].ContainsKey("Id"));
        Assert.True(rows[0].ContainsKey("Name"));
    }

    [Fact]
    public void Serialize_ExcludeXmlElements_StripsElements()
    {
        var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Id"] = 1,
            ["Name"] = "Test",
            ["Description"] = "Desc",
            ["SettingsXml"] = "<root><pagesize>10</pagesize><layout>grid</layout></root>"
        };

        var predicate = new ProviderPredicateDefinition
        {
            Name = "Test",
            ProviderType = "SqlTable",
            Table = "TestTable",
            NameColumn = "Name",
            XmlColumns = new List<string> { "SettingsXml" },
            ExcludeXmlElements = new List<string> { "pagesize" }
        };

        var (provider, _, outputRoot) = CreateProviderForSerialize(new[] { row });

        var result = provider.Serialize(predicate, outputRoot);

        Assert.Equal(1, result.RowsSerialized);

        var fileStore = new FlatFileStore();
        var rows = fileStore.ReadAllRows(outputRoot, "TestTable").ToList();
        Assert.Single(rows);
        var settingsXml = rows[0]["SettingsXml"]?.ToString() ?? "";
        Assert.DoesNotContain("pagesize", settingsXml);
        Assert.Contains("layout", settingsXml);
    }

    [Fact]
    public void Serialize_NoExcludeFields_AllColumnsPresent()
    {
        var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Id"] = 1,
            ["Name"] = "Test",
            ["Description"] = "Full row",
            ["SettingsXml"] = "<root />"
        };

        var predicate = new ProviderPredicateDefinition
        {
            Name = "Test",
            ProviderType = "SqlTable",
            Table = "TestTable",
            NameColumn = "Name"
            // No ExcludeFields, no ExcludeXmlElements
        };

        var (provider, _, outputRoot) = CreateProviderForSerialize(new[] { row });

        var result = provider.Serialize(predicate, outputRoot);

        Assert.Equal(1, result.RowsSerialized);

        var fileStore = new FlatFileStore();
        var rows = fileStore.ReadAllRows(outputRoot, "TestTable").ToList();
        Assert.Single(rows);
        Assert.True(rows[0].ContainsKey("Id"));
        Assert.True(rows[0].ContainsKey("Name"));
        Assert.True(rows[0].ContainsKey("Description"));
        Assert.True(rows[0].ContainsKey("SettingsXml"));
    }

    #region Helper Methods

    private static (SqlTableProvider provider, Mock<ISqlExecutor> executor, string outputRoot)
        CreateProviderForSerialize(IEnumerable<Dictionary<string, object?>> rows)
    {
        var mockExecutor = new Mock<ISqlExecutor>();

        // DataGroupMetadataReader mock
        var mockMetadataReader = new Mock<DataGroupMetadataReader>(mockExecutor.Object) { CallBase = false };
        mockMetadataReader.Setup(x => x.GetTableMetadata(It.IsAny<ProviderPredicateDefinition>(), It.IsAny<bool>()))
            .Returns(TestMetadata);

        // SqlTableReader mock -- ReadAllRows returns the test rows
        var rowList = rows.ToList();
        var dbReaderMock = CreateMockDataReader(
            new[] { "Id", "Name", "Description", "SettingsXml" },
            rowList.Select(r => new object[]
            {
                r.GetValueOrDefault("Id") ?? DBNull.Value,
                r.GetValueOrDefault("Name") ?? DBNull.Value,
                r.GetValueOrDefault("Description") ?? DBNull.Value,
                r.GetValueOrDefault("SettingsXml") ?? DBNull.Value
            }).ToArray());
        mockExecutor.Setup(x => x.ExecuteReader(It.IsAny<Dynamicweb.Data.CommandBuilder>()))
            .Returns(dbReaderMock.Object);

        var tableReader = new SqlTableReader(mockExecutor.Object);
        var fileStore = new FlatFileStore();

        // Output directory
        var tempDir = Path.Combine(Path.GetTempPath(), $"contentsync_serialize_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var writerMock = new Mock<SqlTableWriter>(mockExecutor.Object) { CallBase = false };
        var provider = new SqlTableProvider(mockMetadataReader.Object, tableReader, fileStore, writerMock.Object);

        return (provider, mockExecutor, tempDir);
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
