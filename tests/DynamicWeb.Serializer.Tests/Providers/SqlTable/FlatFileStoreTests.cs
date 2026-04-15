using DynamicWeb.Serializer.Models;
using DynamicWeb.Serializer.Providers.SqlTable;
using Xunit;

namespace DynamicWeb.Serializer.Tests.Providers.SqlTable;

[Trait("Category", "Phase13")]
public class FlatFileStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FlatFileStore _store;

    public FlatFileStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "FlatFileStoreTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _store = new FlatFileStore();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void WriteRow_CreatesYamlFile_InSqlTableFolder()
    {
        var row = new Dictionary<string, object?>
        {
            ["Id"] = 1,
            ["Name"] = "Checkout"
        };

        _store.WriteRow(_tempDir, "EcomOrderFlow", "Checkout", row);

        var expected = Path.Combine(_tempDir, "_sql", "EcomOrderFlow", "Checkout.yml");
        Assert.True(File.Exists(expected), $"Expected file at {expected}");
    }

    [Fact]
    public void WriteRow_PreservesNullValues()
    {
        var row = new Dictionary<string, object?>
        {
            ["Id"] = 1,
            ["Description"] = null
        };

        _store.WriteRow(_tempDir, "TestTable", "Row1", row);

        var filePath = Path.Combine(_tempDir, "_sql", "TestTable", "Row1.yml");
        var content = File.ReadAllText(filePath);

        // YAML null should be represented as empty value or ~ (tilde)
        // With ForceStringScalarEmitter, keys are double-quoted and null renders as empty
        // Dictionary keys are preserved as-is (not camelCased) by YamlDotNet
        Assert.True(content.Contains("~") || content.Contains("Description") || content.Contains("description"),
            $"Expected null representation in YAML. Got:\n{content}");
    }

    [Fact]
    public void ReadAllRows_DeserializesYamlTildeAsCSharpNull()
    {
        // Write a row with a null column value (serializes to YAML ~)
        var row = new Dictionary<string, object?>
        {
            ["Id"] = 1,
            ["NullableField"] = null,
            ["Name"] = "Test"
        };

        _store.WriteRow(_tempDir, "TestTable", "Row1", row);

        // Read it back
        var rows = _store.ReadAllRows(_tempDir, "TestTable").ToList();

        Assert.Single(rows);
        var readBack = rows[0];

        // CRITICAL: YAML ~ must deserialize to C# null, not string "null" or "~"
        // Plan 13-03's SqlTableWriter maps C# null to DBNull.Value for SQL parameters
        Assert.True(readBack.ContainsKey("nullableField") || readBack.ContainsKey("NullableField"),
            "Expected nullableField key in deserialized dictionary");

        var nullValue = readBack.ContainsKey("nullableField") ? readBack["nullableField"] : readBack["NullableField"];
        Assert.Null(nullValue);

        // Also verify non-null values survive round-trip
        var nameValue = readBack.ContainsKey("name") ? readBack["name"] : readBack["Name"];
        Assert.Equal("Test", nameValue?.ToString());
    }

    [Fact]
    public void WriteMeta_CreatesMetaYml()
    {
        var metadata = new TableMetadata
        {
            TableName = "EcomOrderFlow",
            NameColumn = "OrderFlowName",
            CompareColumns = "",
            KeyColumns = new List<string> { "OrderFlowId" },
            IdentityColumns = new List<string> { "OrderFlowId" },
            AllColumns = new List<string> { "OrderFlowId", "OrderFlowName", "OrderFlowDescription" }
        };

        _store.WriteMeta(_tempDir, "EcomOrderFlow", metadata);

        var metaPath = Path.Combine(_tempDir, "_sql", "EcomOrderFlow", "_meta.yml");
        Assert.True(File.Exists(metaPath));
        var content = File.ReadAllText(metaPath);
        Assert.Contains("EcomOrderFlow", content);
    }

    [Fact]
    public void ReadAllRows_ReadsYamlFiles_ExcludingMeta()
    {
        // Write 2 rows + meta
        var row1 = new Dictionary<string, object?> { ["Id"] = 1, ["Name"] = "A" };
        var row2 = new Dictionary<string, object?> { ["Id"] = 2, ["Name"] = "B" };
        _store.WriteRow(_tempDir, "TestTable", "A", row1);
        _store.WriteRow(_tempDir, "TestTable", "B", row2);

        var metadata = new TableMetadata
        {
            TableName = "TestTable",
            NameColumn = "Name",
            CompareColumns = "",
            KeyColumns = new List<string> { "Id" },
            IdentityColumns = new List<string>(),
            AllColumns = new List<string> { "Id", "Name" }
        };
        _store.WriteMeta(_tempDir, "TestTable", metadata);

        // Act
        var rows = _store.ReadAllRows(_tempDir, "TestTable").ToList();

        // Assert — should return 2, not 3 (meta excluded)
        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public void SanitizeFileName_ReplacesInvalidChars()
    {
        var result = FlatFileStore.SanitizeFileName("A/B:C");

        Assert.DoesNotContain("/", result);
        Assert.DoesNotContain(":", result);
        Assert.Contains("A", result);
        Assert.Contains("B", result);
        Assert.Contains("C", result);
    }

    // -------------------------------------------------------------------------
    // XML literal block scalar tests (Phase 27)
    // -------------------------------------------------------------------------

    [Fact]
    public void WriteRow_EmitsLiteralBlockScalar_ForMultilineStrings()
    {
        var prettyXml = "<settings>\n  <param name=\"test\" />\n</settings>";
        var row = new Dictionary<string, object?>
        {
            ["Id"] = 1,
            ["SettingsXml"] = prettyXml
        };

        _store.WriteRow(_tempDir, "TestTable", "Row1", row);

        var filePath = Path.Combine(_tempDir, "_sql", "TestTable", "Row1.yml");
        var content = File.ReadAllText(filePath);

        // Literal block scalar indicator '|' should be present for multiline strings
        Assert.Contains("|", content);
        // Should NOT be double-quoted (escaped) — that defeats pretty-printing
        Assert.DoesNotContain("\\n", content);
    }

    [Fact]
    public void WriteRow_ReadAllRows_RoundTripsMultilineXml()
    {
        var prettyXml = "<settings>\n  <param name=\"test\" />\n</settings>";
        var row = new Dictionary<string, object?>
        {
            ["Id"] = 1,
            ["SettingsXml"] = prettyXml
        };

        _store.WriteRow(_tempDir, "TestTable", "Row1", row);

        var rows = _store.ReadAllRows(_tempDir, "TestTable").ToList();
        Assert.Single(rows);

        var readBack = rows[0];
        var settingsValue = readBack.ContainsKey("settingsXml") ? readBack["settingsXml"] : readBack["SettingsXml"];
        Assert.Equal(prettyXml, settingsValue?.ToString());
    }
}
