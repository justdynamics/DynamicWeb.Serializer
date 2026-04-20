using System.Globalization;
using DynamicWeb.Serializer.Infrastructure;
using Xunit;

namespace DynamicWeb.Serializer.Tests.Infrastructure;

/// <summary>
/// Tests for the unified target-schema cache used by ContentDeserializer (Area writes)
/// and SqlTableProvider (MERGE writes). Verifies schema loading caches per-table, that
/// Coerce handles every SQL type currently covered by the two legacy helpers, and that
/// missing-column logging is suppressed after the first occurrence.
/// </summary>
public class TargetSchemaCacheTests
{
    // -------------------------------------------------------------------------
    // Helper: fixture loader that tracks how many times it was invoked per table
    // -------------------------------------------------------------------------

    private static Func<string, (HashSet<string> cols, Dictionary<string, string> types)> CreateLoader(
        Dictionary<string, Dictionary<string, string>> fixture,
        Dictionary<string, int> callCounts)
    {
        return (tableName) =>
        {
            callCounts[tableName] = callCounts.GetValueOrDefault(tableName, 0) + 1;
            if (!fixture.TryGetValue(tableName, out var typeMap))
            {
                return (new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
            }
            return (new HashSet<string>(typeMap.Keys, StringComparer.OrdinalIgnoreCase),
                    new Dictionary<string, string>(typeMap, StringComparer.OrdinalIgnoreCase));
        };
    }

    private static TargetSchemaCache BuildCache(
        Dictionary<string, Dictionary<string, string>> fixture,
        out Dictionary<string, int> callCounts)
    {
        callCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        return new TargetSchemaCache(CreateLoader(fixture, callCounts));
    }

    private static TargetSchemaCache BuildAreaCache() => new(tableName =>
    {
        if (!tableName.Equals("Area", StringComparison.OrdinalIgnoreCase))
        {
            return (new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        }
        var types = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["AreaID"] = "int",
            ["AreaName"] = "nvarchar",
            ["AreaActive"] = "bit",
            ["AreaSort"] = "int",
            ["AreaCreatedDate"] = "datetime",
            ["AreaCreatedDateOffset"] = "datetimeoffset",
            ["AreaUniqueId"] = "uniqueidentifier",
            ["AreaVisitorCount"] = "bigint",
            ["AreaRevenue"] = "decimal",
            ["AreaScore"] = "float",
            ["AreaMini"] = "real",
        };
        return (new HashSet<string>(types.Keys, StringComparer.OrdinalIgnoreCase), types);
    });

    // -------------------------------------------------------------------------
    // Schema caching
    // -------------------------------------------------------------------------

    [Fact]
    public void GetColumns_CachesPerTable_OneLoaderCallPerTable()
    {
        var fixture = new Dictionary<string, Dictionary<string, string>>
        {
            ["Area"] = new() { ["AreaID"] = "int", ["AreaName"] = "nvarchar" },
            ["EcomOrders"] = new() { ["OrderId"] = "int" },
        };
        var cache = BuildCache(fixture, out var calls);

        // 3 calls, 2 tables → loader hit once per table
        _ = cache.GetColumns("Area");
        _ = cache.GetColumns("Area");
        _ = cache.GetColumns("EcomOrders");
        _ = cache.GetColumnTypes("Area"); // already cached — must not re-load
        _ = cache.GetColumnTypes("EcomOrders");

        Assert.Equal(1, calls["Area"]);
        Assert.Equal(1, calls["EcomOrders"]);
    }

    [Fact]
    public void GetColumns_UnknownTable_ReturnsEmpty()
    {
        var cache = BuildCache(new Dictionary<string, Dictionary<string, string>>(), out _);
        var cols = cache.GetColumns("DoesNotExist");
        Assert.NotNull(cols);
        Assert.Empty(cols);
    }

    [Fact]
    public void GetColumnTypes_UnknownTable_ReturnsEmpty()
    {
        var cache = BuildCache(new Dictionary<string, Dictionary<string, string>>(), out _);
        var types = cache.GetColumnTypes("DoesNotExist");
        Assert.NotNull(types);
        Assert.Empty(types);
    }

    [Fact]
    public void GetColumns_IsCaseInsensitive()
    {
        var fixture = new Dictionary<string, Dictionary<string, string>>
        {
            ["Area"] = new() { ["AreaName"] = "nvarchar" },
        };
        var cache = BuildCache(fixture, out _);

        Assert.Contains("areaname", cache.GetColumns("Area"), StringComparer.OrdinalIgnoreCase);
        Assert.Contains("AREANAME", cache.GetColumns("Area"), StringComparer.OrdinalIgnoreCase);
    }

    // -------------------------------------------------------------------------
    // Coerce: datetime family
    // -------------------------------------------------------------------------

    [Fact]
    public void Coerce_DateTimeString_ToDateTime()
    {
        var cache = BuildAreaCache();
        var result = cache.Coerce("Area", "AreaCreatedDate", "2021-01-04T15:53:06.0730000");
        var expected = DateTime.Parse(
            "2021-01-04T15:53:06.0730000",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        Assert.IsType<DateTime>(result);
        Assert.Equal(expected, (DateTime)result!);
    }

    [Fact]
    public void Coerce_DateTimeOffsetString_ToDateTimeOffset()
    {
        var cache = BuildAreaCache();
        var result = cache.Coerce("Area", "AreaCreatedDateOffset", "2021-01-04T15:53:06+02:00");
        Assert.IsType<DateTimeOffset>(result);
    }

    [Fact]
    public void Coerce_Datetime2_Date_Smalldatetime_AllParseToDateTime()
    {
        var cache = BuildCache(new Dictionary<string, Dictionary<string, string>>
        {
            ["T"] = new() { ["D1"] = "datetime2", ["D2"] = "date", ["D3"] = "smalldatetime" }
        }, out _);

        Assert.IsType<DateTime>(cache.Coerce("T", "D1", "2021-01-04"));
        Assert.IsType<DateTime>(cache.Coerce("T", "D2", "2021-01-04"));
        Assert.IsType<DateTime>(cache.Coerce("T", "D3", "2021-01-04 12:00"));
    }

    // -------------------------------------------------------------------------
    // Coerce: numeric and bit
    // -------------------------------------------------------------------------

    [Fact]
    public void Coerce_BoolString_True_AndOne_AndFalse_AndZero()
    {
        var cache = BuildAreaCache();
        Assert.Equal(true, cache.Coerce("Area", "AreaActive", "true"));
        Assert.Equal(true, cache.Coerce("Area", "AreaActive", "True"));
        Assert.Equal(true, cache.Coerce("Area", "AreaActive", "1"));
        Assert.Equal(false, cache.Coerce("Area", "AreaActive", "false"));
        Assert.Equal(false, cache.Coerce("Area", "AreaActive", "False"));
        Assert.Equal(false, cache.Coerce("Area", "AreaActive", "0"));
    }

    [Fact]
    public void Coerce_IntString_ToInt()
    {
        var cache = BuildAreaCache();
        Assert.Equal(42, cache.Coerce("Area", "AreaSort", "42"));
    }

    [Fact]
    public void Coerce_SmallInt_And_TinyInt_FromString_ToInt()
    {
        var cache = BuildCache(new Dictionary<string, Dictionary<string, string>>
        {
            ["T"] = new() { ["S"] = "smallint", ["T"] = "tinyint" }
        }, out _);
        Assert.Equal(42, cache.Coerce("T", "S", "42"));
        Assert.Equal(5, cache.Coerce("T", "T", "5"));
    }

    [Fact]
    public void Coerce_BigIntString_ToLong()
    {
        var cache = BuildAreaCache();
        var result = cache.Coerce("Area", "AreaVisitorCount", "9999999999");
        Assert.IsType<long>(result);
        Assert.Equal(9999999999L, (long)result!);
    }

    [Fact]
    public void Coerce_IntToLong_ForBigintColumn()
    {
        var cache = BuildAreaCache();
        var result = cache.Coerce("Area", "AreaVisitorCount", 42);
        Assert.IsType<long>(result);
        Assert.Equal(42L, (long)result!);
    }

    [Fact]
    public void Coerce_DecimalString_ToDecimal()
    {
        var cache = BuildAreaCache();
        var result = cache.Coerce("Area", "AreaRevenue", "123.45");
        Assert.IsType<decimal>(result);
        Assert.Equal(123.45m, (decimal)result!);
    }

    [Fact]
    public void Coerce_FloatString_ToDouble()
    {
        var cache = BuildAreaCache();
        var result = cache.Coerce("Area", "AreaScore", "3.14");
        Assert.IsType<double>(result);
        Assert.Equal(3.14d, (double)result!);
    }

    [Fact]
    public void Coerce_RealString_ToFloat()
    {
        var cache = BuildAreaCache();
        var result = cache.Coerce("Area", "AreaMini", "1.5");
        Assert.IsType<float>(result);
        Assert.Equal(1.5f, (float)result!);
    }

    [Fact]
    public void Coerce_UniqueIdentifier_ToGuid()
    {
        var cache = BuildAreaCache();
        var guid = Guid.NewGuid();
        var result = cache.Coerce("Area", "AreaUniqueId", guid.ToString());
        Assert.IsType<Guid>(result);
        Assert.Equal(guid, (Guid)result!);
    }

    // -------------------------------------------------------------------------
    // Coerce: null / empty / pass-through
    // -------------------------------------------------------------------------

    [Fact]
    public void Coerce_NullValue_ReturnsDbNull()
    {
        var cache = BuildAreaCache();
        Assert.Equal(DBNull.Value, cache.Coerce("Area", "AreaSort", null));
    }

    [Fact]
    public void Coerce_DbNullValue_ReturnsDbNull()
    {
        var cache = BuildAreaCache();
        Assert.Equal(DBNull.Value, cache.Coerce("Area", "AreaSort", DBNull.Value));
    }

    [Fact]
    public void Coerce_EmptyStringForNonStringCol_ReturnsDbNull()
    {
        var cache = BuildAreaCache();
        Assert.Equal(DBNull.Value, cache.Coerce("Area", "AreaCreatedDate", ""));
        Assert.Equal(DBNull.Value, cache.Coerce("Area", "AreaSort", ""));
    }

    [Fact]
    public void Coerce_WhitespaceStringForNonStringCol_ReturnsDbNull()
    {
        var cache = BuildAreaCache();
        Assert.Equal(DBNull.Value, cache.Coerce("Area", "AreaCreatedDate", "   "));
    }

    [Fact]
    public void Coerce_EmptyStringForStringCol_PreservesEmptyString()
    {
        var cache = BuildAreaCache();
        Assert.Equal("", cache.Coerce("Area", "AreaName", ""));
    }

    [Fact]
    public void Coerce_UnknownColumn_PassesThrough()
    {
        var cache = BuildAreaCache();
        Assert.Equal("x", cache.Coerce("Area", "UnknownCol", "x"));
    }

    [Fact]
    public void Coerce_UnparseableString_PassesThrough()
    {
        var cache = BuildAreaCache();
        // Not-a-date on a datetime column should return the original string rather than throw.
        Assert.Equal("not-a-date", cache.Coerce("Area", "AreaCreatedDate", "not-a-date"));
    }

    [Fact]
    public void Coerce_NvarcharColumn_ReturnsStringUnchanged()
    {
        var cache = BuildAreaCache();
        Assert.Equal("Swift 2", cache.Coerce("Area", "AreaName", "Swift 2"));
    }

    // -------------------------------------------------------------------------
    // LogMissingColumnOnce
    // -------------------------------------------------------------------------

    [Fact]
    public void LogMissingColumnOnce_FirstCallLogs_SubsequentSuppressed()
    {
        var cache = BuildAreaCache();
        var logged = new List<string>();
        Action<string> log = logged.Add;

        var first = cache.LogMissingColumnOnce("Area", "AreaHtmlType", log);
        var second = cache.LogMissingColumnOnce("Area", "AreaHtmlType", log);
        var third = cache.LogMissingColumnOnce("Area", "AreaHtmlType", log);

        Assert.True(first);
        Assert.False(second);
        Assert.False(third);
        Assert.Single(logged);
        Assert.Contains("AreaHtmlType", logged[0]);
        Assert.Contains("Area", logged[0]);
    }

    [Fact]
    public void LogMissingColumnOnce_DifferentColumns_EachLogged()
    {
        var cache = BuildAreaCache();
        var logged = new List<string>();
        Action<string> log = logged.Add;

        cache.LogMissingColumnOnce("Area", "AreaHtmlType", log);
        cache.LogMissingColumnOnce("Area", "AreaLayoutPhone", log);
        cache.LogMissingColumnOnce("EcomOrders", "AreaHtmlType", log); // same col name, different table

        Assert.Equal(3, logged.Count);
    }

    [Fact]
    public void LogMissingColumnOnce_NullLog_DoesNotThrow()
    {
        var cache = BuildAreaCache();
        var first = cache.LogMissingColumnOnce("Area", "X", null);
        var second = cache.LogMissingColumnOnce("Area", "X", null);
        Assert.True(first);
        Assert.False(second);
    }
}
