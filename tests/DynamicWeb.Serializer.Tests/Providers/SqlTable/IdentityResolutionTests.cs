using DynamicWeb.Serializer.Models;
using DynamicWeb.Serializer.Providers.SqlTable;
using Moq;
using Xunit;

namespace DynamicWeb.Serializer.Tests.Providers.SqlTable;

[Trait("Category", "Phase13")]
public class IdentityResolutionTests
{
    private readonly SqlTableReader _reader;

    public IdentityResolutionTests()
    {
        var mockExecutor = new Mock<ISqlExecutor>();
        _reader = new SqlTableReader(mockExecutor.Object);
    }

    [Fact]
    public void GenerateRowIdentity_UsesNameColumn_WhenPresent()
    {
        var metadata = CreateMetadata(nameColumn: "OrderFlowName", keyColumns: new[] { "Id" });
        var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Id"] = 1,
            ["OrderFlowName"] = "Checkout"
        };

        var identity = _reader.GenerateRowIdentity(row, metadata);

        Assert.Equal("Checkout", identity);
    }

    [Fact]
    public void GenerateRowIdentity_UsesCompositePk_WhenNoNameColumn()
    {
        var metadata = CreateMetadata(nameColumn: "", keyColumns: new[] { "ColB", "ColA" });
        var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["ColA"] = "X",
            ["ColB"] = "Y"
        };

        var identity = _reader.GenerateRowIdentity(row, metadata);

        // Alphabetical: ColA first, then ColB
        Assert.Equal("X$$Y", identity);
    }

    [Fact]
    public void GenerateRowIdentity_TrimsValues()
    {
        var metadata = CreateMetadata(nameColumn: "Name", keyColumns: new[] { "Id" });
        var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Id"] = 1,
            ["Name"] = "  Checkout  "
        };

        var identity = _reader.GenerateRowIdentity(row, metadata);

        Assert.Equal("Checkout", identity);
    }

    [Fact]
    public void CalculateChecksum_ExcludesIdentityColumns()
    {
        var metadata = new TableMetadata
        {
            TableName = "Test",
            NameColumn = "Name",
            CompareColumns = "",
            KeyColumns = new[] { "Id" },
            IdentityColumns = new[] { "Id" },
            AllColumns = new[] { "Id", "Name", "Value" }
        };

        var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Id"] = 1,
            ["Name"] = "Test",
            ["Value"] = "ABC"
        };

        var checksum = _reader.CalculateChecksum(row, metadata);

        Assert.NotEmpty(checksum);
        Assert.Equal(32, checksum.Length); // MD5 hex is 32 chars

        // Verify identity column exclusion: different Id, same Name/Value = same checksum
        var row2 = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Id"] = 999,
            ["Name"] = "Test",
            ["Value"] = "ABC"
        };

        var checksum2 = _reader.CalculateChecksum(row2, metadata);
        Assert.Equal(checksum, checksum2);
    }

    [Fact]
    public void CalculateChecksum_UsesCompareColumns_WhenSpecified()
    {
        var metadata = new TableMetadata
        {
            TableName = "Test",
            NameColumn = "Name",
            CompareColumns = "Name,Value",
            KeyColumns = new[] { "Id" },
            IdentityColumns = new[] { "Id" },
            AllColumns = new[] { "Id", "Name", "Value", "Extra" }
        };

        var row1 = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Id"] = 1,
            ["Name"] = "Test",
            ["Value"] = "ABC",
            ["Extra"] = "Different1"
        };

        var row2 = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Id"] = 2,
            ["Name"] = "Test",
            ["Value"] = "ABC",
            ["Extra"] = "Different2"
        };

        // Only Name and Value are compared; Extra and Id differ but checksum should match
        var checksum1 = _reader.CalculateChecksum(row1, metadata);
        var checksum2 = _reader.CalculateChecksum(row2, metadata);
        Assert.Equal(checksum1, checksum2);
    }

    private static TableMetadata CreateMetadata(string nameColumn, string[] keyColumns) => new()
    {
        TableName = "TestTable",
        NameColumn = nameColumn,
        CompareColumns = "",
        KeyColumns = keyColumns,
        IdentityColumns = Array.Empty<string>(),
        AllColumns = Array.Empty<string>()
    };
}
