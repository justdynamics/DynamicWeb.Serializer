using System.Data;
using Dynamicweb.Data;

namespace DynamicWeb.Serializer.Providers.SqlTable;

/// <summary>
/// Testable abstraction over Dynamicweb.Data.Database static calls.
/// Production code uses DwSqlExecutor; tests inject a mock.
/// </summary>
public interface ISqlExecutor
{
    /// <summary>Execute a query and return a data reader.</summary>
    IDataReader ExecuteReader(CommandBuilder command);

    /// <summary>Execute a non-query command and return rows affected.</summary>
    int ExecuteNonQuery(CommandBuilder command);
}
