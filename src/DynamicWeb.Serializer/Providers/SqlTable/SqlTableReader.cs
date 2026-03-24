using System.Security.Cryptography;
using System.Text;
using DynamicWeb.Serializer.Models;
using Dynamicweb.Data;

namespace DynamicWeb.Serializer.Providers.SqlTable;

/// <summary>
/// Reads all rows from a SQL table via ISqlExecutor and provides identity resolution
/// and checksum calculation following DW Deployment tool patterns.
/// </summary>
public class SqlTableReader
{
    private const string IdentitySeparator = "$$";
    private readonly ISqlExecutor _sqlExecutor;

    public SqlTableReader(ISqlExecutor sqlExecutor) => _sqlExecutor = sqlExecutor;

    /// <summary>
    /// Read all rows from the specified table, mapping DBNull to null.
    /// </summary>
    public IEnumerable<Dictionary<string, object?>> ReadAllRows(string tableName)
    {
        var cb = new CommandBuilder();
        cb.Add($"SELECT * FROM [{tableName}]");

        using var reader = _sqlExecutor.ExecuteReader(cb);
        while (reader.Read())
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var value = reader.GetValue(i);
                row[reader.GetName(i)] = value == DBNull.Value ? null : value;
            }

            yield return row;
        }
    }

    /// <summary>
    /// Generate a row identity string following DW Deployment tool patterns (D-10/D-11).
    /// If NameColumn is set, use its value. Otherwise, use composite PK with $$ separator.
    /// Key columns are sorted alphabetically (OrdinalIgnoreCase).
    /// </summary>
    public string GenerateRowIdentity(Dictionary<string, object?> row, TableMetadata metadata)
    {
        if (!string.IsNullOrEmpty(metadata.NameColumn))
        {
            return row.TryGetValue(metadata.NameColumn, out var nameValue)
                ? nameValue?.ToString()?.Trim() ?? ""
                : "";
        }

        // Composite PK: sort key columns alphabetically, join values with $$
        var sortedKeys = metadata.KeyColumns
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase);

        var parts = sortedKeys.Select(key =>
            row.TryGetValue(key, out var val) ? val?.ToString()?.Trim() ?? "" : "");

        return string.Join(IdentitySeparator, parts);
    }

    /// <summary>
    /// Calculate MD5 checksum for change detection (D-15).
    /// Uses CompareColumns if specified, otherwise all columns except identity columns.
    /// </summary>
    public string CalculateChecksum(Dictionary<string, object?> row, TableMetadata metadata)
    {
        IEnumerable<string> columnsToUse;

        if (!string.IsNullOrEmpty(metadata.CompareColumns))
        {
            columnsToUse = metadata.CompareColumns
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(c => c.Trim());
        }
        else
        {
            var identitySet = new HashSet<string>(metadata.IdentityColumns, StringComparer.OrdinalIgnoreCase);
            columnsToUse = metadata.AllColumns.Where(c => !identitySet.Contains(c));
        }

        var sortedColumns = columnsToUse.OrderBy(c => c, StringComparer.OrdinalIgnoreCase);

        var sb = new StringBuilder();
        var first = true;
        foreach (var col in sortedColumns)
        {
            if (!first) sb.Append('|');
            first = false;
            var value = row.TryGetValue(col, out var v) ? NormalizeValue(v) : "";
            sb.Append(col.ToUpperInvariant());
            sb.Append('=');
            sb.Append(value);
        }

        return Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(sb.ToString()))).ToLowerInvariant();
    }

    /// <summary>
    /// Normalize a value to a stable string for checksum comparison.
    /// Handles type differences between DB reads (C# bool True) and YAML reads (string "true").
    /// </summary>
    private static string NormalizeValue(object? v)
    {
        if (v is null) return "";
        if (v is bool b) return b ? "true" : "false";
        // Whitespace-only strings normalize to empty (matches DW Deployment tool)
        var s = v.ToString() ?? "";
        return string.IsNullOrWhiteSpace(s) ? "" : s;
    }
}
