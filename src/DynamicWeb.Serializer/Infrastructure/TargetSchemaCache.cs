using System.Globalization;
using Dynamicweb.Data;

namespace DynamicWeb.Serializer.Infrastructure;

/// <summary>
/// Per-run cache of target table schema (columns + SQL data types) plus unified YAML → .NET
/// type coercion. One instance per deserialize run — shared across ContentDeserializer (Area
/// write path) and SqlTableProvider (MERGE path). Issues at most one INFORMATION_SCHEMA query
/// per table accessed. Extracted from commit f0bfbba's Area-only GetTargetAreaColumns +
/// CoerceForColumn, merged with SqlTableProvider.CoerceRowTypes' broader type coverage.
/// </summary>
public class TargetSchemaCache
{
    private readonly Dictionary<string, HashSet<string>> _columns =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, string>> _types =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<(string table, string column)> _loggedMissing = new();
    private readonly Func<string, (HashSet<string> cols, Dictionary<string, string> types)> _schemaLoader;

    /// <summary>
    /// Production: pass no loader — queries INFORMATION_SCHEMA.COLUMNS via
    /// <see cref="Dynamicweb.Data.Database"/>. Tests: pass a loader that returns fixture
    /// schema keyed by table name.
    /// </summary>
    public TargetSchemaCache(
        Func<string, (HashSet<string> cols, Dictionary<string, string> types)>? schemaLoader = null)
    {
        _schemaLoader = schemaLoader ?? DefaultLoader;
    }

    private static (HashSet<string>, Dictionary<string, string>) DefaultLoader(string tableName)
    {
        var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var types = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var cb = new CommandBuilder();
        // Parameterized via CommandBuilder placeholder — mitigates T-37-02-01 (SQL injection via tableName).
        cb.Add("SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = {0}", tableName);
        using var reader = Database.CreateDataReader(cb);
        while (reader.Read())
        {
            var name = reader.GetString(0);
            var type = reader.GetString(1);
            cols.Add(name);
            types[name] = type;
        }
        return (cols, types);
    }

    /// <summary>
    /// Returns the set of column names present on the target table, querying
    /// INFORMATION_SCHEMA on first access and caching for the remainder of the instance
    /// lifetime. Unknown/nonexistent tables return an empty set rather than throw.
    /// </summary>
    public IReadOnlySet<string> GetColumns(string tableName)
    {
        Ensure(tableName);
        return _columns[tableName];
    }

    /// <summary>
    /// Returns a dictionary of column name → SQL data type (e.g. "datetime", "nvarchar")
    /// for the target table, cached per instance. Unknown tables return an empty map.
    /// </summary>
    public IReadOnlyDictionary<string, string> GetColumnTypes(string tableName)
    {
        Ensure(tableName);
        return _types[tableName];
    }

    /// <summary>
    /// Coerce a YAML-parsed value into the CLR type the target SQL column expects.
    /// Covers the union of cases handled today by ContentDeserializer.CoerceForColumn (f0bfbba)
    /// and SqlTableProvider.CoerceRowTypes — datetime/datetime2/smalldatetime/date/datetimeoffset/
    /// bit/int/smallint/tinyint/bigint/decimal/numeric/money/smallmoney/float/real/uniqueidentifier.
    /// Null/DBNull inputs → DBNull.Value. Empty/whitespace strings on non-string columns →
    /// DBNull.Value. Unknown columns or unparseable strings pass through unchanged.
    /// </summary>
    public object? Coerce(string tableName, string columnName, object? value)
    {
        if (value is null || value is DBNull) return DBNull.Value;
        Ensure(tableName);
        if (!_types[tableName].TryGetValue(columnName, out var dataType)) return value;

        if (value is string s)
        {
            // Empty/whitespace string on non-string column → null (preserves f0bfbba + SqlTableProvider behavior)
            if (string.IsNullOrWhiteSpace(s) && !IsStringType(dataType)) return DBNull.Value;
            // Empty string on string column stays as-is (caller may want empty not null)
            if (string.IsNullOrEmpty(s)) return s;

            switch (dataType.ToLowerInvariant())
            {
                case "datetime":
                case "datetime2":
                case "smalldatetime":
                case "date":
                    if (DateTime.TryParse(s, CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                            out var dt))
                        return dt;
                    break;
                case "datetimeoffset":
                    if (DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture,
                            DateTimeStyles.RoundtripKind, out var dto))
                        return dto;
                    break;
                case "bit":
                    if (s.Equals("true", StringComparison.OrdinalIgnoreCase) || s == "1") return true;
                    if (s.Equals("false", StringComparison.OrdinalIgnoreCase) || s == "0") return false;
                    break;
                case "int":
                case "smallint":
                case "tinyint":
                    if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
                        return i;
                    break;
                case "bigint":
                    if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l))
                        return l;
                    break;
                case "decimal":
                case "numeric":
                case "money":
                case "smallmoney":
                    if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var dec))
                        return dec;
                    break;
                case "float":
                    if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                        return f;
                    break;
                case "real":
                    if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var r))
                        return r;
                    break;
                case "uniqueidentifier":
                    if (Guid.TryParse(s, out var g)) return g;
                    break;
                case "varbinary":
                case "binary":
                case "image":
                    // base64 round-trip (carried over from SqlTableProvider.CoerceRowTypes)
                    var probe = new byte[s.Length];
                    if (Convert.TryFromBase64String(s, probe, out _))
                        return Convert.FromBase64String(s);
                    break;
            }
            return value;
        }

        // Non-string incoming value: int → long for bigint (carried from SqlTableProvider.CoerceRowTypes)
        if (dataType.Equals("bigint", StringComparison.OrdinalIgnoreCase) && value is int intVal)
            return (long)intVal;

        return value;
    }

    /// <summary>
    /// First time a given (table, column) pair is encountered, log the missing-column
    /// warning and return true. Subsequent calls for the same pair return false without
    /// logging. Prevents log spam when dozens of rows share a missing column.
    /// </summary>
    public bool LogMissingColumnOnce(string tableName, string columnName, Action<string>? log)
    {
        if (!_loggedMissing.Add((tableName, columnName))) return false;
        log?.Invoke($"WARNING: source column [{tableName}].[{columnName}] not present on target schema — skipping");
        return true;
    }

    private static bool IsStringType(string sqlType) =>
        sqlType.ToLowerInvariant() is "nvarchar" or "varchar" or "nchar" or "char"
            or "ntext" or "text" or "xml";

    private void Ensure(string tableName)
    {
        if (_columns.ContainsKey(tableName)) return;
        var (cols, types) = _schemaLoader(tableName);
        _columns[tableName] = cols;
        _types[tableName] = types;
    }
}
