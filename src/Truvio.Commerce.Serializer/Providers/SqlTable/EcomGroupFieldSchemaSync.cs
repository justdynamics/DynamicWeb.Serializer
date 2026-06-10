using Dynamicweb.Data;

namespace Truvio.Commerce.Serializer.Providers.SqlTable;

/// <summary>
/// Ensures custom columns defined in EcomProductGroupField exist on the EcomGroups table.
/// Replicates DW10 ProductGroupFieldRepository.UpdateTable() behavior:
///   - Reads field definitions from EcomProductGroupField
///   - Looks up SQL type from EcomFieldType
///   - ALTERs EcomGroups to add missing columns
///   - BIT columns get NOT NULL DEFAULT ((0)) constraint
/// </summary>
public class EcomGroupFieldSchemaSync
{
    private readonly ISqlExecutor _sqlExecutor;

    public EcomGroupFieldSchemaSync(ISqlExecutor sqlExecutor)
    {
        _sqlExecutor = sqlExecutor ?? throw new ArgumentNullException(nameof(sqlExecutor));
    }

    /// <summary>
    /// Read all EcomProductGroupField rows, look up each field's SQL type
    /// from EcomFieldType, and ALTER TABLE EcomGroups to add missing columns.
    /// </summary>
    public virtual void SyncSchema(Action<string>? log = null)
    {
        var existingColumns = GetExistingColumns();
        var fields = GetProductGroupFields();

        if (fields.Count == 0)
        {
            log?.Invoke("Schema sync: no EcomProductGroupField rows found — nothing to do.");
            return;
        }

        foreach (var (systemName, typeId) in fields)
        {
            if (existingColumns.Contains(systemName))
            {
                log?.Invoke($"Schema sync: column [{systemName}] already exists — skipped.");
                continue;
            }

            var sqlType = GetFieldTypeSql(typeId);
            if (sqlType == null)
            {
                log?.Invoke($"Schema sync: no EcomFieldType found for TypeID={typeId} (field '{systemName}') — skipped.");
                continue;
            }

            var alterSql = $"ALTER TABLE [EcomGroups] ADD [{systemName}] {sqlType}";
            if (string.Equals(sqlType, "BIT", StringComparison.OrdinalIgnoreCase))
                alterSql += " NOT NULL DEFAULT ((0))";

            var cb = new CommandBuilder();
            cb.Add(alterSql);
            _sqlExecutor.ExecuteNonQuery(cb);

            log?.Invoke($"Schema sync: added column [{systemName}] {sqlType} to EcomGroups.");
        }
    }

    /// <summary>
    /// Get all existing column names on EcomGroups via INFORMATION_SCHEMA.
    /// </summary>
    private HashSet<string> GetExistingColumns()
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var cb = new CommandBuilder();
        cb.Add("SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'EcomGroups'");
        using var reader = _sqlExecutor.ExecuteReader(cb);
        while (reader.Read())
            columns.Add(reader.GetString(0));
        return columns;
    }

    /// <summary>
    /// Read all (SystemName, TypeID) pairs from EcomProductGroupField.
    /// </summary>
    private List<(string SystemName, int TypeId)> GetProductGroupFields()
    {
        var fields = new List<(string, int)>();
        var cb = new CommandBuilder();
        cb.Add("SELECT ProductGroupFieldSystemName, ProductGroupFieldTypeID FROM EcomProductGroupField");
        using var reader = _sqlExecutor.ExecuteReader(cb);
        while (reader.Read())
        {
            var name = reader.GetString(0);
            var typeId = reader.GetInt32(1);
            fields.Add((name, typeId));
        }
        return fields;
    }

    /// <summary>
    /// Look up the SQL type string for a given FieldTypeID from EcomFieldType.
    /// Returns null if not found.
    /// </summary>
    private string? GetFieldTypeSql(int typeId)
    {
        var cb = new CommandBuilder();
        cb.Add($"SELECT FieldTypeDBSQL FROM EcomFieldType WHERE FieldTypeID = {typeId}");
        using var reader = _sqlExecutor.ExecuteReader(cb);
        return reader.Read() ? reader.GetString(0) : null;
    }
}
