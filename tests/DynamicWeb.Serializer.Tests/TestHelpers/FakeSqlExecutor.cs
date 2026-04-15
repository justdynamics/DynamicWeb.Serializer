using System.Data;
using DynamicWeb.Serializer.Providers.SqlTable;
using Dynamicweb.Data;

namespace DynamicWeb.Serializer.Tests.TestHelpers;

/// <summary>
/// Minimal ISqlExecutor that maps SQL query substrings to canned DataTable results.
/// Used across multiple test classes to avoid duplicating fake infrastructure.
/// </summary>
internal sealed class FakeSqlExecutor : ISqlExecutor
{
    private readonly List<(string Substring, DataTable Result)> _mappings = new();

    public void AddMapping(string querySubstring, DataTable result) =>
        _mappings.Add((querySubstring, result));

    public IDataReader ExecuteReader(CommandBuilder command)
    {
        var sql = command.ToString() ?? string.Empty;
        foreach (var (substring, result) in _mappings)
            if (sql.Contains(substring, StringComparison.OrdinalIgnoreCase))
                return result.CreateDataReader();
        return new DataTable().CreateDataReader();
    }

    public int ExecuteNonQuery(CommandBuilder command) => 0;
}

/// <summary>
/// Shared test utility for creating single-column DataTables with string values.
/// </summary>
internal static class TestTableHelper
{
    public static DataTable CreateSingleColumnTable(string columnName, params string[] values)
    {
        var dt = new DataTable();
        dt.Columns.Add(columnName, typeof(string));
        foreach (var v in values)
            dt.Rows.Add(v);
        return dt;
    }
}
