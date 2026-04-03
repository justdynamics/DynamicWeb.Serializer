using System.Data;
using System.Text;
using DynamicWeb.Serializer.Models;
using Dynamicweb.Data;

namespace DynamicWeb.Serializer.Providers.SqlTable;

/// <summary>
/// Outcome of writing a single row to the target SQL table.
/// </summary>
public enum WriteOutcome
{
    Created,
    Updated,
    Skipped,
    Failed
}

/// <summary>
/// Builds and executes MERGE upsert commands for SQL table deserialization.
/// Follows DW10 SqlDataItemWriter.BuildMergeCommand pattern (D-12/D-14)
/// with IDENTITY_INSERT handling and dry-run safety.
/// </summary>
public class SqlTableWriter
{
    private readonly ISqlExecutor _sqlExecutor;

    public SqlTableWriter(ISqlExecutor sqlExecutor) => _sqlExecutor = sqlExecutor;

    /// <summary>
    /// Build a parameterized MERGE command following the DW10 pattern exactly.
    /// Uses CommandBuilder {0} placeholders for SQL parameter safety.
    /// </summary>
    public CommandBuilder BuildMergeCommand(Dictionary<string, object?> row, TableMetadata metadata, HashSet<string>? notNullColumns = null)
    {
        var keyColumns = metadata.KeyColumns;
        var allColumns = metadata.AllColumns;

        // Determine which columns are present in the row data
        var itemColumns = allColumns
            .Where(col => row.ContainsKey(col))
            .ToList();

        // Identity insert required when identity column is also a key column
        var enableIdentityInsert = metadata.IdentityColumns
            .Any(ic => keyColumns.Contains(ic, StringComparer.OrdinalIgnoreCase));

        // Update columns: exclude key columns and identity columns (matching DW10 pattern)
        var updateColumns = itemColumns
            .Where(col => !keyColumns.Contains(col, StringComparer.OrdinalIgnoreCase)
                       && !metadata.IdentityColumns.Contains(col, StringComparer.OrdinalIgnoreCase))
            .ToList();

        // Insert columns: exclude identity columns unless identity insert is enabled
        var insertColumns = enableIdentityInsert
            ? itemColumns
            : itemColumns.Where(col => !metadata.IdentityColumns.Contains(col, StringComparer.OrdinalIgnoreCase)).ToList();

        var cb = new CommandBuilder();

        if (enableIdentityInsert)
        {
            cb.Add($"SET IDENTITY_INSERT [{metadata.TableName}] ON;");
        }

        cb.Add($"MERGE [{metadata.TableName}] AS target");
        cb.Add("USING (SELECT ");

        // Add parameterized values for each column
        var count = 0;
        foreach (var column in itemColumns)
        {
            if (count > 0)
            {
                cb.Add(",");
            }

            var value = row.TryGetValue(column, out var v) ? v ?? DBNull.Value : DBNull.Value;
            // Ensure NOT NULL key columns never get DBNull (use empty string for string types)
            if (value == DBNull.Value && keyColumns.Contains(column, StringComparer.OrdinalIgnoreCase))
                value = "";
            cb.Add("{0}", value);
            count++;
        }

        cb.Add(") AS source (");
        cb.Add(string.Join(",", itemColumns.Select(col => $"[{col}]")));
        cb.Add(")");

        // ON clause: match on key columns
        cb.Add("ON (");
        cb.Add(string.Join(" AND ", keyColumns.Select(col => $"target.[{col}] = source.[{col}]")));
        cb.Add(")");

        // WHEN MATCHED: update non-key, non-identity columns
        if (updateColumns.Count > 0)
        {
            cb.Add("WHEN MATCHED THEN UPDATE SET");
            cb.Add(string.Join(",", updateColumns.Select(col => $"[{col}] = source.[{col}]")));
        }

        // WHEN NOT MATCHED: insert all eligible columns
        // Wrap NOT NULL columns with ISNULL() to guard against parameter-level null leakage
        cb.Add("WHEN NOT MATCHED THEN INSERT (");
        cb.Add(string.Join(",", insertColumns.Select(col => $"[{col}]")));
        cb.Add(")");
        cb.Add("VALUES(");
        cb.Add(string.Join(",", insertColumns.Select(col =>
        {
            if (notNullColumns != null && notNullColumns.Contains(col))
                return $"ISNULL(source.[{col}], '')";
            return $"source.[{col}]";
        })));
        cb.Add(");");

        if (enableIdentityInsert)
        {
            cb.Add($"SET IDENTITY_INSERT [{metadata.TableName}] OFF;");
        }

        return cb;
    }

    /// <summary>
    /// Write a single row to the target table via MERGE upsert.
    /// In dry-run mode, checks existence but does NOT execute any SQL writes.
    /// </summary>
    public virtual WriteOutcome WriteRow(Dictionary<string, object?> row, TableMetadata metadata, bool isDryRun, Action<string>? log = null, HashSet<string>? notNullColumns = null)
    {
        try
        {
            // Check if row already exists to determine Created vs Updated
            var exists = RowExistsInTarget(metadata, row);

            if (isDryRun)
            {
                // Dry-run: report what would happen without executing MERGE
                return exists ? WriteOutcome.Updated : WriteOutcome.Created;
            }

            // Execute MERGE upsert
            var cb = BuildMergeCommand(row, metadata, notNullColumns);
            _sqlExecutor.ExecuteNonQuery(cb);

            return exists ? WriteOutcome.Updated : WriteOutcome.Created;
        }
        catch (Exception ex)
        {
            // Log the failing column values for debugging
            if (ex.Message.Contains("does not allow nulls"))
            {
                var nullCols = row.Where(kv => kv.Value is null || kv.Value == DBNull.Value)
                    .Select(kv => kv.Key).ToList();
                var missingCols = metadata.AllColumns.Where(c => !row.ContainsKey(c)).ToList();
                log?.Invoke($"    ERROR [{metadata.TableName}]: {ex.Message} | NullCols=[{string.Join(",", nullCols)}] MissingCols=[{string.Join(",", missingCols)}]");
            }
            else
            {
                log?.Invoke($"    ERROR [{metadata.TableName}]: {ex.Message}");
            }
            return WriteOutcome.Failed;
        }
    }

    /// <summary>
    /// Check whether a row with the given key values exists in the target table.
    /// Used for both dry-run reporting and Created/Updated determination.
    /// </summary>
    /// <summary>
    /// Create a table from column definitions stored in serialized metadata.
    /// Used when the target database is missing a table that exists in the source.
    /// </summary>
    public void CreateTableFromMetadata(TableMetadata metadata)
    {
        if (metadata.ColumnDefinitions.Count == 0)
            throw new InvalidOperationException(
                $"Cannot create table [{metadata.TableName}]: no column definitions in metadata. " +
                "Re-serialize from the source to capture column schema.");

        var sb = new StringBuilder();
        sb.AppendLine($"CREATE TABLE [{metadata.TableName}] (");

        for (int i = 0; i < metadata.ColumnDefinitions.Count; i++)
        {
            var col = metadata.ColumnDefinitions[i];
            sb.Append($"  [{col.Name}] {MapColumnType(col)}");
            if (col.IsIdentity)
                sb.Append(" IDENTITY(1,1)");
            sb.Append(col.IsNullable ? " NULL" : " NOT NULL");
            if (i < metadata.ColumnDefinitions.Count - 1)
                sb.Append(',');
            sb.AppendLine();
        }

        // Add primary key constraint
        if (metadata.KeyColumns.Count > 0)
        {
            sb.Append($"  ,CONSTRAINT [PK_{metadata.TableName}] PRIMARY KEY CLUSTERED (");
            sb.Append(string.Join(", ", metadata.KeyColumns.Select(k => $"[{k}]")));
            sb.AppendLine(")");
        }

        sb.AppendLine(")");

        var cb = new CommandBuilder();
        cb.Add(sb.ToString());
        _sqlExecutor.ExecuteNonQuery(cb);
    }

    private static string MapColumnType(ColumnDefinition col)
    {
        return col.DataType.ToLowerInvariant() switch
        {
            "nvarchar" or "nchar" => col.MaxLength == -1
                ? $"{col.DataType}(max)"
                : $"{col.DataType}({col.MaxLength})",
            "varchar" or "char" => col.MaxLength == -1
                ? $"{col.DataType}(max)"
                : $"{col.DataType}({col.MaxLength})",
            "varbinary" => col.MaxLength == -1
                ? "varbinary(max)"
                : $"varbinary({col.MaxLength})",
            "decimal" or "numeric" => $"{col.DataType}({col.Precision},{col.Scale})",
            "float" => col.Precision > 0 ? $"float({col.Precision})" : "float",
            "datetime2" or "datetimeoffset" or "time" => col.Scale > 0
                ? $"{col.DataType}({col.Scale})"
                : col.DataType,
            _ => col.DataType
        };
    }

    /// <summary>
    /// For tables without primary keys: truncate the table and insert all rows.
    /// </summary>
    public void TruncateAndInsertAll(List<Dictionary<string, object?>> rows, TableMetadata metadata, Action<string>? log = null)
    {
        // Truncate existing data
        var truncateCb = new CommandBuilder();
        truncateCb.Add($"DELETE FROM [{metadata.TableName}]");
        _sqlExecutor.ExecuteNonQuery(truncateCb);

        // Check for identity column
        var hasIdentity = metadata.IdentityColumns.Count > 0;

        foreach (var row in rows)
        {
            var itemColumns = metadata.AllColumns
                .Where(col => row.ContainsKey(col))
                .ToList();

            // Exclude identity columns from INSERT unless we need to preserve IDs
            var insertColumns = hasIdentity ? itemColumns : itemColumns;

            var cb = new CommandBuilder();
            if (hasIdentity)
                cb.Add($"SET IDENTITY_INSERT [{metadata.TableName}] ON;");

            cb.Add($"INSERT INTO [{metadata.TableName}] (");
            cb.Add(string.Join(",", insertColumns.Select(col => $"[{col}]")));
            cb.Add(") VALUES (");

            for (int i = 0; i < insertColumns.Count; i++)
            {
                if (i > 0) cb.Add(",");
                var value = row.TryGetValue(insertColumns[i], out var v) ? v ?? DBNull.Value : DBNull.Value;
                cb.Add("{0}", value);
            }

            cb.Add(")");

            if (hasIdentity)
                cb.Add($";SET IDENTITY_INSERT [{metadata.TableName}] OFF;");

            _sqlExecutor.ExecuteNonQuery(cb);
        }

        log?.Invoke($"  Truncate+insert: {rows.Count} rows inserted into [{metadata.TableName}]");
    }

    /// <summary>
    /// Disable all foreign key constraints on a table. Used during bulk deserialization
    /// to prevent FK ordering issues, then re-enabled after all tables are processed.
    /// </summary>
    public void DisableForeignKeys(string tableName)
    {
        var cb = new CommandBuilder();
        cb.Add($"ALTER TABLE [{tableName}] NOCHECK CONSTRAINT ALL");
        _sqlExecutor.ExecuteNonQuery(cb);
    }

    /// <summary>
    /// Re-enable all foreign key constraints on a table.
    /// </summary>
    public void EnableForeignKeys(string tableName)
    {
        var cb = new CommandBuilder();
        cb.Add($"ALTER TABLE [{tableName}] WITH CHECK CHECK CONSTRAINT ALL");
        _sqlExecutor.ExecuteNonQuery(cb);
    }

    public bool RowExistsInTarget(TableMetadata metadata, Dictionary<string, object?> row)
    {
        var cb = new CommandBuilder();
        cb.Add($"SELECT 1 FROM [{metadata.TableName}] WHERE ");

        var conditions = new List<string>();
        foreach (var keyCol in metadata.KeyColumns)
        {
            var value = row.TryGetValue(keyCol, out var v) ? v ?? DBNull.Value : DBNull.Value;
            // Build each condition with parameterized value
            var condCb = new CommandBuilder();
            condCb.Add($"[{keyCol}] = ");
            condCb.Add("{0}", value);
            conditions.Add($"[{keyCol}] = {{0}}");
        }

        // Rebuild as single command with all parameters
        cb = new CommandBuilder();
        cb.Add($"SELECT 1 FROM [{metadata.TableName}] WHERE ");

        for (int i = 0; i < metadata.KeyColumns.Count; i++)
        {
            if (i > 0) cb.Add(" AND ");
            var keyCol = metadata.KeyColumns[i];
            var value = row.TryGetValue(keyCol, out var v) ? v ?? DBNull.Value : DBNull.Value;
            cb.Add($"[{keyCol}] = ");
            cb.Add("{0}", value);
        }

        using var reader = _sqlExecutor.ExecuteReader(cb);
        return reader.Read();
    }
}
