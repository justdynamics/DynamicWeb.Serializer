using System.Data;
using Dynamicweb.Data;

namespace DynamicWeb.Serializer.Providers.SqlTable;

/// <summary>
/// Production implementation of ISqlExecutor wrapping Dynamicweb.Data.Database static API.
/// </summary>
public class DwSqlExecutor : ISqlExecutor
{
    public IDataReader ExecuteReader(CommandBuilder command)
        => Database.CreateDataReader(command);

    public int ExecuteNonQuery(CommandBuilder command)
        => Database.ExecuteNonQuery(command);
}
