using Dynamicweb.Data;

namespace DynamicWeb.Serializer.Configuration;

/// <summary>
/// Validates SQL identifiers (table / column names) against INFORMATION_SCHEMA.
/// Identifiers cannot be parameterized in T-SQL (they're spliced as text), so the
/// only defense against "'; DROP TABLE X;--" as a table name is allowlisting.
/// Per SEED-002. One INFORMATION_SCHEMA query per table lifetime of the instance.
/// </summary>
public class SqlIdentifierValidator
{
    private readonly Dictionary<string, HashSet<string>> _tableColumns =
        new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string>? _tableNames;
    private readonly Func<HashSet<string>> _tableLoader;
    private readonly Func<string, HashSet<string>> _columnLoader;

    /// <summary>Production ctor — uses Database.CreateDataReader against INFORMATION_SCHEMA.</summary>
    public SqlIdentifierValidator()
    {
        _tableLoader = DefaultTableLoader;
        _columnLoader = DefaultColumnLoader;
    }

    /// <summary>Test ctor — inject fixture loaders to exercise validation without a live DB.</summary>
    public SqlIdentifierValidator(
        Func<HashSet<string>> tableLoader,
        Func<string, HashSet<string>> columnLoader)
    {
        _tableLoader = tableLoader;
        _columnLoader = columnLoader;
    }

    /// <summary>
    /// Validate a table name exists in INFORMATION_SCHEMA.TABLES. Throws on mismatch.
    /// </summary>
    public void ValidateTable(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            throw new InvalidOperationException("Empty table name is not a valid identifier.");

        EnsureTableNames();
        if (!_tableNames!.Contains(tableName))
            throw new InvalidOperationException(
                $"Table identifier not in INFORMATION_SCHEMA: '{tableName}'. " +
                "Check the 'table' value in your predicate config.");
    }

    /// <summary>
    /// Validate a column exists on the given table. Assumes <see cref="ValidateTable"/> was
    /// called first — if the table is not in the column cache it is loaded on demand.
    /// </summary>
    public void ValidateColumn(string tableName, string columnName)
    {
        if (string.IsNullOrWhiteSpace(columnName))
            throw new InvalidOperationException("Empty column name is not a valid identifier.");

        var cols = GetColumns(tableName);
        if (!cols.Contains(columnName))
            throw new InvalidOperationException(
                $"Column identifier not in INFORMATION_SCHEMA: '[{tableName}].[{columnName}]'. " +
                "Check exclude/include/where fields in your predicate config.");
    }

    /// <summary>
    /// Returns the cached column set for a table. Loads from INFORMATION_SCHEMA on first call
    /// and reuses the cached set on subsequent calls.
    /// </summary>
    public HashSet<string> GetColumns(string tableName)
    {
        if (!_tableColumns.TryGetValue(tableName, out var cols))
        {
            cols = _columnLoader(tableName);
            _tableColumns[tableName] = cols;
        }
        return cols;
    }

    private void EnsureTableNames()
    {
        _tableNames ??= _tableLoader();
    }

    private static HashSet<string> DefaultTableLoader()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var cb = new CommandBuilder();
        cb.Add("SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'");
        using var reader = Database.CreateDataReader(cb);
        while (reader.Read()) set.Add(reader.GetString(0));
        return set;
    }

    private static HashSet<string> DefaultColumnLoader(string tableName)
    {
        var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var cb = new CommandBuilder();
        // Parameterized via CommandBuilder {0} placeholder to block injection through tableName itself.
        cb.Add("SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = {0}", tableName);
        using var reader = Database.CreateDataReader(cb);
        while (reader.Read()) cols.Add(reader.GetString(0));
        return cols;
    }
}
