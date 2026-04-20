using DynamicWeb.Serializer.Models;
using Dynamicweb.Data;

namespace DynamicWeb.Serializer.Providers.SqlTable;

/// <summary>
/// Builds TableMetadata from predicate config fields + live DB schema introspection.
/// Table name, name column, and compare columns come from the predicate config.
/// Primary keys, identity columns, and all columns are queried from the database at runtime.
/// </summary>
public class DataGroupMetadataReader
{
    private readonly ISqlExecutor _sqlExecutor;

    public DataGroupMetadataReader(ISqlExecutor sqlExecutor)
    {
        _sqlExecutor = sqlExecutor;
    }

    /// <summary>
    /// Build table metadata from predicate config + live schema queries.
    /// </summary>
    /// <summary>
    /// Query column data types for type coercion during deserialization.
    /// Returns a dictionary mapping column name → SQL data type name.
    /// </summary>
    public virtual Dictionary<string, string> GetColumnTypes(string tableName)
    {
        var types = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var cb = new CommandBuilder();
        cb.Add($"SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}'");

        using var reader = _sqlExecutor.ExecuteReader(cb);
        while (reader.Read())
        {
            var name = reader["COLUMN_NAME"].ToString()!;
            var type = reader["DATA_TYPE"].ToString()!;
            types[name] = type;
        }

        return types;
    }

    /// <summary>
    /// Query which columns are NOT NULL (cannot receive null values).
    /// </summary>
    public virtual HashSet<string> GetNotNullColumns(string tableName)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var cb = new CommandBuilder();
        cb.Add($"SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND IS_NULLABLE = 'NO'");

        using var reader = _sqlExecutor.ExecuteReader(cb);
        while (reader.Read())
        {
            columns.Add(reader["COLUMN_NAME"].ToString()!);
        }

        return columns;
    }

    public virtual TableMetadata GetTableMetadata(ProviderPredicateDefinition predicate, bool includeColumnDefinitions = false)
    {
        var tableName = predicate.Table
            ?? throw new InvalidOperationException("SqlTable predicate requires a Table name.");

        var keyColumns = QueryPrimaryKeyColumns(tableName);
        var identityColumns = QueryIdentityColumns(tableName);
        var allColumns = QueryAllColumns(tableName);
        var columnDefinitions = includeColumnDefinitions
            ? QueryColumnDefinitions(tableName)
            : (List<ColumnDefinition>)[];

        return new TableMetadata
        {
            TableName = tableName,
            NameColumn = predicate.NameColumn ?? "",
            CompareColumns = predicate.CompareColumns ?? "",
            KeyColumns = keyColumns,
            IdentityColumns = identityColumns,
            AllColumns = allColumns,
            ColumnDefinitions = columnDefinitions
        };
    }

    /// <summary>
    /// Check whether a table exists in the current database.
    /// </summary>
    public virtual bool TableExists(string tableName)
    {
        var cb = new CommandBuilder();
        cb.Add($"SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '{tableName}'");

        using var reader = _sqlExecutor.ExecuteReader(cb);
        return reader.Read();
    }

    private List<string> QueryPrimaryKeyColumns(string tableName)
    {
        var columns = new List<string>();
        var cb = new CommandBuilder();
        cb.Add($"sp_pkeys @table_name = '{tableName}'");

        using var reader = _sqlExecutor.ExecuteReader(cb);
        while (reader.Read())
        {
            var colName = reader["COLUMN_NAME"]?.ToString();
            if (!string.IsNullOrEmpty(colName))
                columns.Add(colName);
        }

        columns.Sort(StringComparer.OrdinalIgnoreCase);
        return columns;
    }

    private List<string> QueryIdentityColumns(string tableName)
    {
        var columns = new List<string>();
        var cb = new CommandBuilder();
        cb.Add($"SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}'");
        cb.Add(" AND COLUMNPROPERTY(OBJECT_ID(TABLE_SCHEMA + '.' + TABLE_NAME), COLUMN_NAME, 'IsIdentity') = 1");

        using var reader = _sqlExecutor.ExecuteReader(cb);
        while (reader.Read())
        {
            var colName = reader["COLUMN_NAME"]?.ToString();
            if (!string.IsNullOrEmpty(colName))
                columns.Add(colName);
        }

        return columns;
    }

    private List<string> QueryAllColumns(string tableName)
    {
        var columns = new List<string>();
        var cb = new CommandBuilder();
        cb.Add($"SELECT TOP 0 * FROM [{tableName}]");

        using var reader = _sqlExecutor.ExecuteReader(cb);
        for (int i = 0; i < reader.FieldCount; i++)
        {
            columns.Add(reader.GetName(i));
        }

        return columns;
    }

    private List<ColumnDefinition> QueryColumnDefinitions(string tableName)
    {
        var columns = new List<ColumnDefinition>();
        var cb = new CommandBuilder();
        cb.Add($@"
            SELECT c.COLUMN_NAME, c.DATA_TYPE,
                   ISNULL(c.CHARACTER_MAXIMUM_LENGTH, 0) AS MaxLength,
                   ISNULL(c.NUMERIC_PRECISION, 0) AS [Precision],
                   ISNULL(c.NUMERIC_SCALE, 0) AS Scale,
                   CASE WHEN c.IS_NULLABLE = 'YES' THEN 1 ELSE 0 END AS IsNullable,
                   ISNULL(COLUMNPROPERTY(OBJECT_ID(c.TABLE_SCHEMA + '.' + c.TABLE_NAME), c.COLUMN_NAME, 'IsIdentity'), 0) AS IsIdentity
            FROM INFORMATION_SCHEMA.COLUMNS c
            WHERE c.TABLE_NAME = '{tableName}'
            ORDER BY c.ORDINAL_POSITION");

        using var reader = _sqlExecutor.ExecuteReader(cb);
        while (reader.Read())
        {
            columns.Add(new ColumnDefinition
            {
                Name = reader["COLUMN_NAME"].ToString()!,
                DataType = reader["DATA_TYPE"].ToString()!,
                MaxLength = Convert.ToInt32(reader["MaxLength"]),
                Precision = Convert.ToInt32(reader["Precision"]),
                Scale = Convert.ToInt32(reader["Scale"]),
                IsNullable = Convert.ToInt32(reader["IsNullable"]) == 1,
                IsIdentity = Convert.ToInt32(reader["IsIdentity"]) == 1
            });
        }

        return columns;
    }
}
