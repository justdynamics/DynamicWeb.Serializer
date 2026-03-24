using System.Data;
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
    public CommandBuilder BuildMergeCommand(Dictionary<string, object?> row, TableMetadata metadata)
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
        cb.Add("WHEN NOT MATCHED THEN INSERT (");
        cb.Add(string.Join(",", insertColumns.Select(col => $"[{col}]")));
        cb.Add(")");
        cb.Add("VALUES(");
        cb.Add(string.Join(",", insertColumns.Select(col => $"source.[{col}]")));
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
    public virtual WriteOutcome WriteRow(Dictionary<string, object?> row, TableMetadata metadata, bool isDryRun)
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
            var cb = BuildMergeCommand(row, metadata);
            _sqlExecutor.ExecuteNonQuery(cb);

            return exists ? WriteOutcome.Updated : WriteOutcome.Created;
        }
        catch (Exception)
        {
            return WriteOutcome.Failed;
        }
    }

    /// <summary>
    /// Check whether a row with the given key values exists in the target table.
    /// Used for both dry-run reporting and Created/Updated determination.
    /// </summary>
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
