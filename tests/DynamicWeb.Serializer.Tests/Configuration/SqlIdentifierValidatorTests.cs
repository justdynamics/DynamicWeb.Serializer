using DynamicWeb.Serializer.Configuration;
using Xunit;

namespace DynamicWeb.Serializer.Tests.Configuration;

public class SqlIdentifierValidatorTests
{
    private static HashSet<string> Tables(params string[] names) =>
        new(names, StringComparer.OrdinalIgnoreCase);

    private static Dictionary<string, HashSet<string>> Columns(params (string table, string[] cols)[] entries)
    {
        var map = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (t, cs) in entries)
            map[t] = new HashSet<string>(cs, StringComparer.OrdinalIgnoreCase);
        return map;
    }

    private static SqlIdentifierValidator Build(
        HashSet<string> tables,
        Dictionary<string, HashSet<string>> columns,
        Action<string>? onColumnLoad = null)
    {
        return new SqlIdentifierValidator(
            tableLoader: () => tables,
            columnLoader: tableName =>
            {
                onColumnLoad?.Invoke(tableName);
                return columns.TryGetValue(tableName, out var cs)
                    ? cs
                    : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            });
    }

    [Fact]
    public void ValidateTable_ExistingTable_Succeeds()
    {
        var v = Build(Tables("Area", "AccessUser"), Columns());

        // Should not throw
        v.ValidateTable("Area");
        v.ValidateTable("AccessUser");
    }

    [Fact]
    public void ValidateTable_CaseInsensitive_Succeeds()
    {
        var v = Build(Tables("Area"), Columns());

        v.ValidateTable("area");
        v.ValidateTable("AREA");
    }

    [Fact]
    public void ValidateTable_UnknownTable_ThrowsWithMessage()
    {
        var v = Build(Tables("Area"), Columns());

        var ex = Assert.Throws<InvalidOperationException>(() => v.ValidateTable("NotARealTable"));
        Assert.Contains("identifier not in INFORMATION_SCHEMA", ex.Message);
        Assert.Contains("NotARealTable", ex.Message);
    }

    [Fact]
    public void ValidateTable_InjectionAttempt_ThrowsWithMessage()
    {
        var v = Build(Tables("Products"), Columns());

        var ex = Assert.Throws<InvalidOperationException>(
            () => v.ValidateTable("Products; DROP TABLE EcomOrders;--"));
        Assert.Contains("identifier not in INFORMATION_SCHEMA", ex.Message);
    }

    [Fact]
    public void ValidateTable_EmptyString_Throws()
    {
        var v = Build(Tables("Area"), Columns());

        Assert.Throws<InvalidOperationException>(() => v.ValidateTable(""));
        Assert.Throws<InvalidOperationException>(() => v.ValidateTable("   "));
    }

    [Fact]
    public void ValidateColumn_ExistingColumn_Succeeds()
    {
        var v = Build(
            Tables("Area"),
            Columns(("Area", new[] { "AreaID", "AreaName" })));

        v.ValidateColumn("Area", "AreaID");
        v.ValidateColumn("Area", "AreaName");
    }

    [Fact]
    public void ValidateColumn_CaseInsensitive_Succeeds()
    {
        var v = Build(
            Tables("Area"),
            Columns(("Area", new[] { "AreaID" })));

        v.ValidateColumn("Area", "areaid");
        v.ValidateColumn("Area", "AREAID");
    }

    [Fact]
    public void ValidateColumn_UnknownColumn_ThrowsWithMessage()
    {
        var v = Build(
            Tables("Area"),
            Columns(("Area", new[] { "AreaID" })));

        var ex = Assert.Throws<InvalidOperationException>(
            () => v.ValidateColumn("Area", "NonexistentColumn"));
        Assert.Contains("Column identifier not in INFORMATION_SCHEMA", ex.Message);
        Assert.Contains("Area", ex.Message);
        Assert.Contains("NonexistentColumn", ex.Message);
    }

    [Fact]
    public void ValidateColumn_InjectionAttempt_Throws()
    {
        var v = Build(
            Tables("Area"),
            Columns(("Area", new[] { "AreaID" })));

        var ex = Assert.Throws<InvalidOperationException>(
            () => v.ValidateColumn("Area", "X'; DROP y;--"));
        Assert.Contains("Column identifier not in INFORMATION_SCHEMA", ex.Message);
    }

    [Fact]
    public void ValidateColumn_EmptyString_Throws()
    {
        var v = Build(
            Tables("Area"),
            Columns(("Area", new[] { "AreaID" })));

        Assert.Throws<InvalidOperationException>(() => v.ValidateColumn("Area", ""));
    }

    [Fact]
    public void GetColumns_ReturnsCachedColumnSet()
    {
        var v = Build(
            Tables("Area"),
            Columns(("Area", new[] { "AreaID", "AreaName", "AreaSort" })));

        var cols = v.GetColumns("Area");

        Assert.Contains("AreaID", cols);
        Assert.Contains("AreaName", cols);
        Assert.Contains("AreaSort", cols);
        Assert.Equal(3, cols.Count);
    }

    [Fact]
    public void GetColumns_CachesLookup_OnePerTable()
    {
        var loadCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var v = Build(
            Tables("Area", "AccessUser"),
            Columns(
                ("Area", new[] { "AreaID" }),
                ("AccessUser", new[] { "AccessUserID" })),
            onColumnLoad: t => loadCount[t] = loadCount.TryGetValue(t, out var c) ? c + 1 : 1);

        // First access loads
        v.GetColumns("Area");
        // Subsequent accesses hit cache
        v.GetColumns("Area");
        v.GetColumns("Area");
        v.ValidateColumn("Area", "AreaID");

        // Other table loads separately
        v.GetColumns("AccessUser");
        v.GetColumns("AccessUser");

        Assert.Equal(1, loadCount["Area"]);
        Assert.Equal(1, loadCount["AccessUser"]);
    }
}
