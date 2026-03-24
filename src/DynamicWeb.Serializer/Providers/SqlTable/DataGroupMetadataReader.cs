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
    public virtual TableMetadata GetTableMetadata(ProviderPredicateDefinition predicate)
    {
        var tableName = predicate.Table
            ?? throw new InvalidOperationException("SqlTable predicate requires a Table name.");

        var keyColumns = QueryPrimaryKeyColumns(tableName);
        var identityColumns = QueryIdentityColumns(tableName);
        var allColumns = QueryAllColumns(tableName);

        return new TableMetadata
        {
            TableName = tableName,
            NameColumn = predicate.NameColumn ?? "",
            CompareColumns = predicate.CompareColumns ?? "",
            KeyColumns = keyColumns,
            IdentityColumns = identityColumns,
            AllColumns = allColumns
        };
    }

    private IReadOnlyList<string> QueryPrimaryKeyColumns(string tableName)
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

    private IReadOnlyList<string> QueryIdentityColumns(string tableName)
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

    private IReadOnlyList<string> QueryAllColumns(string tableName)
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
}
