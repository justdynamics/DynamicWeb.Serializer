using System.Xml.Linq;
using Dynamicweb.ContentSync.Models;
using Dynamicweb.Data;

namespace Dynamicweb.ContentSync.Providers.SqlTable;

/// <summary>
/// Parses DataGroup XML files to extract table metadata.
/// Since DW's XmlDataGroupRepository is internal, we parse the XML directly via System.Xml.Linq.
/// XML files live at Files/System/Deployment/DataGroups/.
/// </summary>
public class DataGroupMetadataReader
{
    private readonly ISqlExecutor _sqlExecutor;
    private readonly string _dataGroupsPath;
    private readonly Dictionary<string, XDocument> _xmlCache = new(StringComparer.OrdinalIgnoreCase);

    public DataGroupMetadataReader(ISqlExecutor sqlExecutor, string dataGroupsPath)
    {
        _sqlExecutor = sqlExecutor;
        _dataGroupsPath = dataGroupsPath;
    }

    /// <summary>
    /// Get table metadata for a DataGroup by parsing the XML definition and querying schema.
    /// </summary>
    public virtual TableMetadata GetTableMetadata(string dataGroupId)
    {
        var (tableName, nameColumn, compareColumns) = FindDataGroupInXml(dataGroupId);

        var keyColumns = QueryPrimaryKeyColumns(tableName);
        var identityColumns = QueryIdentityColumns(tableName);
        var allColumns = QueryAllColumns(tableName);

        return new TableMetadata
        {
            TableName = tableName,
            NameColumn = nameColumn,
            CompareColumns = compareColumns,
            KeyColumns = keyColumns,
            IdentityColumns = identityColumns,
            AllColumns = allColumns
        };
    }

    private (string tableName, string nameColumn, string compareColumns) FindDataGroupInXml(string dataGroupId)
    {
        EnsureXmlsCached();

        foreach (var doc in _xmlCache.Values)
        {
            var dataGroup = doc.Descendants("DataGroup")
                .FirstOrDefault(dg => string.Equals(
                    dg.Attribute("Id")?.Value, dataGroupId, StringComparison.OrdinalIgnoreCase));

            if (dataGroup == null)
                continue;

            // Find first DataItemType that uses SqlDataItemProvider
            var dataItemType = dataGroup.Descendants("DataItemType")
                .FirstOrDefault(dit => dit.Attribute("ProviderTypeName")?.Value
                    ?.Contains("SqlDataItemProvider", StringComparison.OrdinalIgnoreCase) == true);

            if (dataItemType == null)
                throw new InvalidOperationException(
                    $"DataGroup '{dataGroupId}' has no SqlDataItemProvider DataItemType.");

            var parameters = dataItemType.Descendants("Parameter")
                .ToDictionary(
                    p => p.Attribute("Name")?.Value ?? "",
                    p => p.Attribute("Value")?.Value ?? "",
                    StringComparer.OrdinalIgnoreCase);

            var tableName = parameters.GetValueOrDefault("Table", "");
            if (string.IsNullOrEmpty(tableName))
                throw new InvalidOperationException(
                    $"DataGroup '{dataGroupId}' has no Table parameter.");

            var nameColumn = parameters.GetValueOrDefault("NameColumn", "");
            var compareColumns = parameters.GetValueOrDefault("CompareColumns", "");

            return (tableName, nameColumn, compareColumns);
        }

        throw new InvalidOperationException(
            $"DataGroup '{dataGroupId}' not found in {_dataGroupsPath}");
    }

    private void EnsureXmlsCached()
    {
        if (_xmlCache.Count > 0)
            return;

        if (!Directory.Exists(_dataGroupsPath))
            return;

        foreach (var xmlFile in Directory.EnumerateFiles(_dataGroupsPath, "*.xml", SearchOption.AllDirectories))
        {
            try
            {
                _xmlCache[xmlFile] = XDocument.Load(xmlFile);
            }
            catch
            {
                // Skip malformed XML files
            }
        }
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
