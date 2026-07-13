using Dynamicweb.Data;

namespace Truvio.Commerce.Serializer.Providers.SqlTable;

/// <summary>
/// Shared engine for column-backed custom-field schema sync. DW10 stores each custom
/// <c>EcomProductGroupField</c> as a physical column on <c>EcomGroups</c>, and each custom
/// <c>EcomProductField</c> as a physical column on <c>EcomProducts</c>. Deserializing the
/// field *definition* rows through a plain SqlTable predicate faithfully writes the rows but
/// does NOT create those columns, so every code path that enumerates the fields and reads
/// their backing column throws (and index builds silently produce 0 documents).
///
/// This routine replicates DW10's <c>UpdateTable()</c> behaviour:
///   - Reads field definitions from the definition table (<see cref="FieldTable"/>)
///   - Looks up each field's SQL type from <c>EcomFieldType</c>
///   - ALTERs the backing table (<see cref="TargetTable"/>) to add missing columns
///   - BIT columns get a NOT NULL DEFAULT ((0)) constraint
///
/// Concrete subclasses point the same routine at a specific (definition table, backing table)
/// pair: <see cref="EcomGroupFieldSchemaSync"/> and <see cref="EcomProductFieldSchemaSync"/>.
/// </summary>
public abstract class CustomColumnSchemaSync
{
    private readonly ISqlExecutor _sqlExecutor;

    protected CustomColumnSchemaSync(ISqlExecutor sqlExecutor)
    {
        _sqlExecutor = sqlExecutor ?? throw new ArgumentNullException(nameof(sqlExecutor));
    }

    /// <summary>Definition table holding one row per custom field (e.g. "EcomProductGroupField").</summary>
    protected abstract string FieldTable { get; }

    /// <summary>Column on <see cref="FieldTable"/> carrying the field's system name.</summary>
    protected abstract string SystemNameColumn { get; }

    /// <summary>Column on <see cref="FieldTable"/> carrying the EcomFieldType id.</summary>
    protected abstract string TypeIdColumn { get; }

    /// <summary>
    /// Backing table on which the custom column must physically exist (e.g. "EcomGroups" /
    /// "EcomProducts"). A compile-time constant per subclass — never operator input — so its
    /// interpolation into the schema-read/ALTER SQL introduces no injection surface.
    /// </summary>
    protected abstract string TargetTable { get; }

    /// <summary>
    /// Read all field definition rows, look up each field's SQL type from EcomFieldType, and
    /// ALTER the backing table to add any missing columns. Idempotent — existing columns are
    /// skipped. Emits <c>Schema sync: added column [...]</c> for each column created.
    /// </summary>
    public virtual void SyncSchema(Action<string>? log = null)
    {
        var existingColumns = GetExistingColumns();
        var fields = GetFields();

        if (fields.Count == 0)
        {
            log?.Invoke($"Schema sync: no {FieldTable} rows found — nothing to do.");
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

            var alterSql = $"ALTER TABLE [{TargetTable}] ADD [{systemName}] {sqlType}";
            if (string.Equals(sqlType, "BIT", StringComparison.OrdinalIgnoreCase))
                alterSql += " NOT NULL DEFAULT ((0))";

            var cb = new CommandBuilder();
            cb.Add(alterSql);
            _sqlExecutor.ExecuteNonQuery(cb);

            log?.Invoke($"Schema sync: added column [{systemName}] {sqlType} to {TargetTable}.");
        }
    }

    /// <summary>
    /// Emit a WARNING for every field definition whose backing column is still absent on
    /// <see cref="TargetTable"/> after a sync attempt (e.g. no EcomFieldType row resolved a
    /// SQL type). A definition without its column breaks every read of that field and silently
    /// zeroes index builds, so this must fail loudly: in strict mode the orchestrator's log
    /// wrapper escalates these WARNING lines to a cumulative failure. No-op when every
    /// definition has a backing column.
    /// </summary>
    public virtual void WarnMissingColumns(Action<string>? log = null)
    {
        var existingColumns = GetExistingColumns();
        var fields = GetFields();

        foreach (var (systemName, _) in fields)
        {
            if (!existingColumns.Contains(systemName))
                log?.Invoke(
                    $"WARNING: Schema sync: {FieldTable} defines field '{systemName}' but no backing " +
                    $"column [{systemName}] exists on {TargetTable} — reads of this field will fail and " +
                    "index builds will silently produce 0 documents. No EcomFieldType SQL type resolved " +
                    "for it; add the field type or the backing column before deserializing again.");
        }
    }

    /// <summary>Get all existing column names on the backing table via INFORMATION_SCHEMA.</summary>
    private HashSet<string> GetExistingColumns()
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var cb = new CommandBuilder();
        cb.Add($"SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{TargetTable}'");
        using var reader = _sqlExecutor.ExecuteReader(cb);
        while (reader.Read())
            columns.Add(reader.GetString(0));
        return columns;
    }

    /// <summary>Read all (SystemName, TypeID) pairs from the definition table.</summary>
    private List<(string SystemName, int TypeId)> GetFields()
    {
        var fields = new List<(string, int)>();
        var cb = new CommandBuilder();
        cb.Add($"SELECT {SystemNameColumn}, {TypeIdColumn} FROM {FieldTable}");
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
