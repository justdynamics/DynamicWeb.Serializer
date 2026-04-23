using System;

namespace DynamicWeb.Serializer.Infrastructure;

/// <summary>
/// Per-value predicate used by Seed-mode deserializers (ContentDeserializer +
/// SqlTableProvider) to decide whether a target value is "unset" — NULL, DBNull,
/// or the type's default (empty string, 0, false, DateTime.MinValue, Guid.Empty).
/// Unset values are eligible to be filled from Seed YAML per Phase 39 D-01..D-03.
/// </summary>
/// <remarks>
/// D-10 tradeoff: a customer who explicitly set a bool to false or an int to 0 is
/// indistinguishable from "unset" under this rule. Documented and accepted in
/// 39-CONTEXT.md D-10.
/// </remarks>
public static class MergePredicate
{
    /// <summary>
    /// Object-typed entry point. Used by provider code holding a value of unknown static
    /// type (e.g. Dictionary&lt;string, object&gt; values from YAML). The caller passes the
    /// expected CLR type to drive default-value comparison; nullable wrappers are unwrapped
    /// automatically.
    /// </summary>
    public static bool IsUnsetForMerge(object? value, Type type)
    {
        if (value is null || value is DBNull) return true;

        // Unwrap Nullable<T>
        var underlying = Nullable.GetUnderlyingType(type) ?? type;

        if (underlying == typeof(string))   return string.IsNullOrEmpty((string)value);
        if (underlying == typeof(int))      return (int)value == 0;
        if (underlying == typeof(long))     return (long)value == 0L;
        if (underlying == typeof(decimal))  return (decimal)value == 0m;
        if (underlying == typeof(double))   return (double)value == 0d;
        if (underlying == typeof(float))    return (float)value == 0f;
        if (underlying == typeof(short))    return (short)value == 0;
        if (underlying == typeof(byte))     return (byte)value == 0;
        if (underlying == typeof(bool))     return !(bool)value;
        if (underlying == typeof(DateTime)) return (DateTime)value == DateTime.MinValue;
        if (underlying == typeof(Guid))     return (Guid)value == Guid.Empty;

        // Enums: compare against default (0)
        if (underlying.IsEnum) return Convert.ToInt64(value) == 0L;

        // Unknown reference type: non-null is considered "set"
        return false;
    }

    // Per-type convenience overloads — D-02 string rule lives here.
    public static bool IsUnsetForMerge(string? value)    => string.IsNullOrEmpty(value);
    public static bool IsUnsetForMerge(int value)        => value == 0;
    public static bool IsUnsetForMerge(long value)       => value == 0L;
    public static bool IsUnsetForMerge(decimal value)    => value == 0m;
    public static bool IsUnsetForMerge(double value)     => value == 0d;
    public static bool IsUnsetForMerge(float value)      => value == 0f;
    public static bool IsUnsetForMerge(bool value)       => !value;
    public static bool IsUnsetForMerge(DateTime value)   => value == DateTime.MinValue;
    public static bool IsUnsetForMerge(DateTime? value)  => !value.HasValue || value.Value == DateTime.MinValue;
    public static bool IsUnsetForMerge(Guid value)       => value == Guid.Empty;

    /// <summary>
    /// SQL-DATA_TYPE-aware overload. Maps INFORMATION_SCHEMA.COLUMNS.DATA_TYPE
    /// strings (nvarchar, varchar, int, bigint, decimal, numeric, float, real,
    /// bit, datetime, datetime2, date, uniqueidentifier, xml) to the D-01 rule.
    /// </summary>
    /// <remarks>
    /// Unknown or null sqlDataType returns false (conservative: don't overwrite
    /// when we don't know the type). Per 39-CONTEXT.md D-12 / RESEARCH Open
    /// Question 2.
    /// </remarks>
    public static bool IsUnsetForMergeBySqlType(object? value, string? sqlDataType)
    {
        if (value is null || value is DBNull) return true;
        if (string.IsNullOrEmpty(sqlDataType)) return false;

        switch (sqlDataType.ToLowerInvariant())
        {
            case "nvarchar":
            case "varchar":
            case "nchar":
            case "char":
            case "text":
            case "ntext":
            case "xml":
                return value is string s && string.IsNullOrEmpty(s);

            case "int":
            case "smallint":
            case "tinyint":
                return Convert.ToInt64(value) == 0L;

            case "bigint":
                return Convert.ToInt64(value) == 0L;

            case "decimal":
            case "numeric":
            case "money":
            case "smallmoney":
                return Convert.ToDecimal(value) == 0m;

            case "float":
            case "real":
                return Convert.ToDouble(value) == 0d;

            case "bit":
                return Convert.ToBoolean(value) == false;

            case "datetime":
            case "datetime2":
            case "smalldatetime":
            case "date":
                return (DateTime)value == DateTime.MinValue;

            case "uniqueidentifier":
                return (Guid)value == Guid.Empty;

            default:
                return false;   // unknown SQL type — conservative: "not unset"
        }
    }
}
