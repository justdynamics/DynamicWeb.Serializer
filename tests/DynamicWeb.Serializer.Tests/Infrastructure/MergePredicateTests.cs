using System;
using DynamicWeb.Serializer.Infrastructure;
using Xunit;

namespace DynamicWeb.Serializer.Tests.Infrastructure;

/// <summary>
/// Phase 39 D-01/D-02/D-03 unit coverage for <see cref="MergePredicate"/>.
/// Verifies the baseline unset rule (NULL OR type default) across the object-typed
/// entry point, per-type convenience overloads, and the SQL-DATA_TYPE-aware overload.
/// </summary>
[Trait("Category", "Phase39")]
public class MergePredicateTests
{
    // -----------------------------------------------------------------------
    // Object-typed entry point — IsUnsetForMerge(object?, Type)
    // -----------------------------------------------------------------------

    [Fact]
    public void IsUnsetForMerge_Object_NullString_ReturnsTrue()
    {
        Assert.True(MergePredicate.IsUnsetForMerge(null, typeof(string)));
    }

    [Fact]
    public void IsUnsetForMerge_Object_EmptyString_ReturnsTrue()
    {
        Assert.True(MergePredicate.IsUnsetForMerge("", typeof(string)));
    }

    [Fact]
    public void IsUnsetForMerge_Object_WhitespaceString_ReturnsFalse()
    {
        // Whitespace is "set" per D-01 strict reading (RESEARCH Open Question 1)
        Assert.False(MergePredicate.IsUnsetForMerge(" ", typeof(string)));
    }

    [Fact]
    public void IsUnsetForMerge_Object_NonEmptyString_ReturnsFalse()
    {
        Assert.False(MergePredicate.IsUnsetForMerge("value", typeof(string)));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(42, false)]
    [InlineData(-1, false)]
    public void IsUnsetForMerge_Object_Int(int input, bool expected)
    {
        Assert.Equal(expected, MergePredicate.IsUnsetForMerge(input, typeof(int)));
    }

    [Fact]
    public void IsUnsetForMerge_Object_LongZero_ReturnsTrue()
    {
        Assert.True(MergePredicate.IsUnsetForMerge(0L, typeof(long)));
    }

    [Fact]
    public void IsUnsetForMerge_Object_LongNonZero_ReturnsFalse()
    {
        Assert.False(MergePredicate.IsUnsetForMerge(42L, typeof(long)));
    }

    [Fact]
    public void IsUnsetForMerge_Object_DecimalZero_ReturnsTrue()
    {
        Assert.True(MergePredicate.IsUnsetForMerge(0m, typeof(decimal)));
    }

    [Fact]
    public void IsUnsetForMerge_Object_DecimalNonZero_ReturnsFalse()
    {
        Assert.False(MergePredicate.IsUnsetForMerge(1.5m, typeof(decimal)));
    }

    [Fact]
    public void IsUnsetForMerge_Object_DoubleZero_ReturnsTrue()
    {
        Assert.True(MergePredicate.IsUnsetForMerge(0.0, typeof(double)));
    }

    [Fact]
    public void IsUnsetForMerge_Object_DoubleNonZero_ReturnsFalse()
    {
        Assert.False(MergePredicate.IsUnsetForMerge(3.14, typeof(double)));
    }

    [Fact]
    public void IsUnsetForMerge_Object_BoolFalse_ReturnsTrue()
    {
        Assert.True(MergePredicate.IsUnsetForMerge(false, typeof(bool)));
    }

    [Fact]
    public void IsUnsetForMerge_Object_BoolTrue_ReturnsFalse()
    {
        Assert.False(MergePredicate.IsUnsetForMerge(true, typeof(bool)));
    }

    [Fact]
    public void IsUnsetForMerge_Object_DateTimeMinValue_ReturnsTrue()
    {
        Assert.True(MergePredicate.IsUnsetForMerge(DateTime.MinValue, typeof(DateTime)));
    }

    [Fact]
    public void IsUnsetForMerge_Object_DateTimeNonMin_ReturnsFalse()
    {
        Assert.False(MergePredicate.IsUnsetForMerge(new DateTime(2024, 1, 1), typeof(DateTime)));
    }

    [Fact]
    public void IsUnsetForMerge_Object_NullableDateTimeNull_ReturnsTrue()
    {
        Assert.True(MergePredicate.IsUnsetForMerge(null, typeof(DateTime?)));
    }

    [Fact]
    public void IsUnsetForMerge_Object_NullableDateTimeWithValue_ReturnsFalse()
    {
        object boxed = (DateTime?)new DateTime(2024, 1, 1);
        Assert.False(MergePredicate.IsUnsetForMerge(boxed, typeof(DateTime?)));
    }

    [Fact]
    public void IsUnsetForMerge_Object_GuidEmpty_ReturnsTrue()
    {
        Assert.True(MergePredicate.IsUnsetForMerge(Guid.Empty, typeof(Guid)));
    }

    [Fact]
    public void IsUnsetForMerge_Object_GuidNonEmpty_ReturnsFalse()
    {
        Assert.False(MergePredicate.IsUnsetForMerge(Guid.NewGuid(), typeof(Guid)));
    }

    [Fact]
    public void IsUnsetForMerge_Object_DBNull_String_ReturnsTrue()
    {
        Assert.True(MergePredicate.IsUnsetForMerge(DBNull.Value, typeof(string)));
    }

    [Fact]
    public void IsUnsetForMerge_Object_DBNull_Int_ReturnsTrue()
    {
        Assert.True(MergePredicate.IsUnsetForMerge(DBNull.Value, typeof(int)));
    }

    [Fact]
    public void IsUnsetForMerge_Object_DBNull_DateTime_ReturnsTrue()
    {
        Assert.True(MergePredicate.IsUnsetForMerge(DBNull.Value, typeof(DateTime)));
    }

    [Fact]
    public void IsUnsetForMerge_Object_NullFallback_ReturnsTrue()
    {
        // Null always unset — fallback for unknown reference type
        Assert.True(MergePredicate.IsUnsetForMerge(null, typeof(object)));
    }

    // -----------------------------------------------------------------------
    // Per-type convenience overloads
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("x", false)]
    public void IsUnsetForMerge_String(string? input, bool expected)
    {
        Assert.Equal(expected, MergePredicate.IsUnsetForMerge(input));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(42, false)]
    public void IsUnsetForMerge_Int(int input, bool expected)
    {
        Assert.Equal(expected, MergePredicate.IsUnsetForMerge(input));
    }

    [Fact]
    public void IsUnsetForMerge_LongZero_ReturnsTrue()
    {
        Assert.True(MergePredicate.IsUnsetForMerge(0L));
    }

    [Fact]
    public void IsUnsetForMerge_LongNonZero_ReturnsFalse()
    {
        Assert.False(MergePredicate.IsUnsetForMerge(42L));
    }

    [Fact]
    public void IsUnsetForMerge_DecimalZero_ReturnsTrue()
    {
        Assert.True(MergePredicate.IsUnsetForMerge(0m));
    }

    [Fact]
    public void IsUnsetForMerge_DecimalNonZero_ReturnsFalse()
    {
        Assert.False(MergePredicate.IsUnsetForMerge(1.5m));
    }

    [Fact]
    public void IsUnsetForMerge_DoubleZero_ReturnsTrue()
    {
        Assert.True(MergePredicate.IsUnsetForMerge(0.0));
    }

    [Fact]
    public void IsUnsetForMerge_FloatZero_ReturnsTrue()
    {
        Assert.True(MergePredicate.IsUnsetForMerge(0.0f));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void IsUnsetForMerge_Bool(bool input, bool expected)
    {
        Assert.Equal(expected, MergePredicate.IsUnsetForMerge(input));
    }

    [Fact]
    public void IsUnsetForMerge_DateTimeMinValue_ReturnsTrue()
    {
        Assert.True(MergePredicate.IsUnsetForMerge(DateTime.MinValue));
    }

    [Fact]
    public void IsUnsetForMerge_DateTimeNonMin_ReturnsFalse()
    {
        Assert.False(MergePredicate.IsUnsetForMerge(DateTime.UtcNow));
    }

    [Fact]
    public void IsUnsetForMerge_NullableDateTimeNull_ReturnsTrue()
    {
        Assert.True(MergePredicate.IsUnsetForMerge((DateTime?)null));
    }

    [Fact]
    public void IsUnsetForMerge_NullableDateTimeMinValue_ReturnsTrue()
    {
        Assert.True(MergePredicate.IsUnsetForMerge((DateTime?)DateTime.MinValue));
    }

    [Fact]
    public void IsUnsetForMerge_NullableDateTimeWithValue_ReturnsFalse()
    {
        Assert.False(MergePredicate.IsUnsetForMerge((DateTime?)new DateTime(2024, 1, 1)));
    }

    [Fact]
    public void IsUnsetForMerge_GuidEmpty_ReturnsTrue()
    {
        Assert.True(MergePredicate.IsUnsetForMerge(Guid.Empty));
    }

    [Fact]
    public void IsUnsetForMerge_GuidNewGuid_ReturnsFalse()
    {
        Assert.False(MergePredicate.IsUnsetForMerge(Guid.NewGuid()));
    }

    // -----------------------------------------------------------------------
    // SQL-type-aware overload — IsUnsetForMergeBySqlType(object?, string?)
    // -----------------------------------------------------------------------

    [Fact]
    public void IsUnsetForMergeBySqlType_NullValue_Nvarchar_ReturnsTrue()
    {
        Assert.True(MergePredicate.IsUnsetForMergeBySqlType(null, "nvarchar"));
    }

    [Fact]
    public void IsUnsetForMergeBySqlType_EmptyString_Nvarchar_ReturnsTrue()
    {
        Assert.True(MergePredicate.IsUnsetForMergeBySqlType("", "nvarchar"));
    }

    [Fact]
    public void IsUnsetForMergeBySqlType_NonEmptyString_Nvarchar_ReturnsFalse()
    {
        Assert.False(MergePredicate.IsUnsetForMergeBySqlType("x", "nvarchar"));
    }

    [Fact]
    public void IsUnsetForMergeBySqlType_Zero_Int_ReturnsTrue()
    {
        Assert.True(MergePredicate.IsUnsetForMergeBySqlType(0, "int"));
    }

    [Fact]
    public void IsUnsetForMergeBySqlType_NonZero_Int_ReturnsFalse()
    {
        Assert.False(MergePredicate.IsUnsetForMergeBySqlType(1, "int"));
    }

    [Fact]
    public void IsUnsetForMergeBySqlType_False_Bit_ReturnsTrue()
    {
        Assert.True(MergePredicate.IsUnsetForMergeBySqlType(false, "bit"));
    }

    [Fact]
    public void IsUnsetForMergeBySqlType_True_Bit_ReturnsFalse()
    {
        Assert.False(MergePredicate.IsUnsetForMergeBySqlType(true, "bit"));
    }

    [Fact]
    public void IsUnsetForMergeBySqlType_DBNull_DateTime2_ReturnsTrue()
    {
        Assert.True(MergePredicate.IsUnsetForMergeBySqlType(DBNull.Value, "datetime2"));
    }

    [Fact]
    public void IsUnsetForMergeBySqlType_CaseInsensitive_UppercaseNvarchar_ReturnsTrue()
    {
        // Case-insensitive dispatch: "NVARCHAR" with "" -> true
        Assert.True(MergePredicate.IsUnsetForMergeBySqlType("", "NVARCHAR"));
    }

    [Fact]
    public void IsUnsetForMergeBySqlType_NullSqlType_ReturnsFalse()
    {
        // Conservative: unknown type hint -> don't overwrite (RESEARCH Assumption A2)
        Assert.False(MergePredicate.IsUnsetForMergeBySqlType("something", null));
    }

    [Fact]
    public void IsUnsetForMergeBySqlType_UnknownSqlType_ReturnsFalse()
    {
        // Conservative default for unknown data types
        Assert.False(MergePredicate.IsUnsetForMergeBySqlType("something", "unobtanium_type"));
    }

    [Fact]
    public void IsUnsetForMergeBySqlType_EmptyGuid_UniqueIdentifier_ReturnsTrue()
    {
        Assert.True(MergePredicate.IsUnsetForMergeBySqlType(Guid.Empty, "uniqueidentifier"));
    }

    [Fact]
    public void IsUnsetForMergeBySqlType_DateTimeMinValue_Datetime_ReturnsTrue()
    {
        Assert.True(MergePredicate.IsUnsetForMergeBySqlType(DateTime.MinValue, "datetime"));
    }

    [Fact]
    public void IsUnsetForMergeBySqlType_DecimalZero_Decimal_ReturnsTrue()
    {
        Assert.True(MergePredicate.IsUnsetForMergeBySqlType(0m, "decimal"));
    }

    [Fact]
    public void IsUnsetForMergeBySqlType_ZeroLong_BigInt_ReturnsTrue()
    {
        Assert.True(MergePredicate.IsUnsetForMergeBySqlType(0L, "bigint"));
    }

    [Fact]
    public void IsUnsetForMergeBySqlType_EmptyString_Xml_ReturnsTrue()
    {
        // XML columns follow the string rule per D-22/D-23 planning context.
        Assert.True(MergePredicate.IsUnsetForMergeBySqlType("", "xml"));
    }
}
