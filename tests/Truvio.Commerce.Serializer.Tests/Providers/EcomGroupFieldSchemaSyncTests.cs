using System.Data;
using Truvio.Commerce.Serializer.Providers.SqlTable;
using Dynamicweb.Data;
using Moq;
using Xunit;

namespace Truvio.Commerce.Serializer.Tests.Providers;

[Trait("Category", "Phase25")]
public class EcomGroupFieldSchemaSyncTests
{
    /// <summary>
    /// Helper: creates a mock ISqlExecutor that responds to specific SQL patterns
    /// with configured DataTable results, and tracks ExecuteNonQuery calls.
    /// </summary>
    private static (Mock<ISqlExecutor> Executor, List<string> ExecutedSql) CreateMockExecutor(
        List<(string SystemName, int TypeId)>? fields = null,
        Dictionary<int, string>? fieldTypes = null,
        HashSet<string>? existingColumns = null)
    {
        var mockExecutor = new Mock<ISqlExecutor>();
        var executedSql = new List<string>();

        // Track all ExecuteNonQuery calls (ALTER TABLE etc.)
        mockExecutor.Setup(x => x.ExecuteNonQuery(It.IsAny<CommandBuilder>()))
            .Returns((CommandBuilder cb) =>
            {
                executedSql.Add(cb.ToString());
                return 1;
            });

        // Build response tables
        var columnsTable = new DataTable();
        columnsTable.Columns.Add("COLUMN_NAME", typeof(string));
        foreach (var col in existingColumns ?? new HashSet<string>())
            columnsTable.Rows.Add(col);

        var fieldsTable = new DataTable();
        fieldsTable.Columns.Add("ProductGroupFieldSystemName", typeof(string));
        fieldsTable.Columns.Add("ProductGroupFieldTypeID", typeof(int));
        foreach (var (name, typeId) in fields ?? new List<(string, int)>())
            fieldsTable.Rows.Add(name, typeId);

        // Build per-typeId lookup tables
        var fieldTypeTables = new Dictionary<int, DataTable>();
        foreach (var (typeId, sqlType) in fieldTypes ?? new Dictionary<int, string>())
        {
            var dt = new DataTable();
            dt.Columns.Add("FieldTypeDBSQL", typeof(string));
            dt.Rows.Add(sqlType);
            fieldTypeTables[typeId] = dt;
        }

        // Empty FieldType result for unknown type IDs
        var emptyFieldTypeTable = new DataTable();
        emptyFieldTypeTable.Columns.Add("FieldTypeDBSQL", typeof(string));

        // Route ExecuteReader based on SQL content
        var readerCallIndex = 0;
        mockExecutor.Setup(x => x.ExecuteReader(It.IsAny<CommandBuilder>()))
            .Returns((CommandBuilder cb) =>
            {
                var sql = cb.ToString();

                if (sql.Contains("INFORMATION_SCHEMA.COLUMNS"))
                    return columnsTable.CreateDataReader();

                if (sql.Contains("EcomProductGroupField"))
                    return fieldsTable.CreateDataReader();

                if (sql.Contains("EcomFieldType"))
                {
                    // Extract typeId from SQL
                    foreach (var (typeId, table) in fieldTypeTables)
                    {
                        if (sql.Contains(typeId.ToString()))
                            return table.CreateDataReader();
                    }
                    return emptyFieldTypeTable.CreateDataReader();
                }

                return emptyFieldTypeTable.CreateDataReader();
            });

        return (mockExecutor, executedSql);
    }

    [Fact]
    public void SyncSchema_AddsMissingColumn_WithCorrectSqlType()
    {
        var (executor, executedSql) = CreateMockExecutor(
            fields: new List<(string, int)> { ("ProductGroupNavigationImage", 5) },
            fieldTypes: new Dictionary<int, string> { { 5, "NVARCHAR(255)" } },
            existingColumns: new HashSet<string> { "GroupID", "GroupName" });

        var sync = new EcomGroupFieldSchemaSync(executor.Object);
        sync.SyncSchema();

        Assert.Single(executedSql);
        Assert.Contains("ALTER TABLE [EcomGroups] ADD [ProductGroupNavigationImage] NVARCHAR(255)", executedSql[0]);
    }

    [Fact]
    public void SyncSchema_SkipsExistingColumn_NoAlterTableExecuted()
    {
        var (executor, executedSql) = CreateMockExecutor(
            fields: new List<(string, int)> { ("ProductGroupNavigationImage", 5) },
            fieldTypes: new Dictionary<int, string> { { 5, "NVARCHAR(255)" } },
            existingColumns: new HashSet<string> { "GroupID", "ProductGroupNavigationImage" });

        var sync = new EcomGroupFieldSchemaSync(executor.Object);
        var logs = new List<string>();
        sync.SyncSchema(msg => logs.Add(msg));

        Assert.Empty(executedSql);
        Assert.Contains(logs, l => l.Contains("already exists"));
    }

    [Fact]
    public void SyncSchema_BitColumn_GetsNotNullDefaultConstraint()
    {
        var (executor, executedSql) = CreateMockExecutor(
            fields: new List<(string, int)> { ("GroupIsActive", 3) },
            fieldTypes: new Dictionary<int, string> { { 3, "BIT" } },
            existingColumns: new HashSet<string>());

        var sync = new EcomGroupFieldSchemaSync(executor.Object);
        sync.SyncSchema();

        Assert.Single(executedSql);
        Assert.Contains("ALTER TABLE [EcomGroups] ADD [GroupIsActive] BIT NOT NULL DEFAULT ((0))", executedSql[0]);
    }

    [Fact]
    public void SyncSchema_MissingFieldType_SkipsGracefully()
    {
        // Field has TypeID=99 but no matching EcomFieldType row
        var (executor, executedSql) = CreateMockExecutor(
            fields: new List<(string, int)> { ("OrphanField", 99) },
            fieldTypes: new Dictionary<int, string>(), // no type 99
            existingColumns: new HashSet<string>());

        var sync = new EcomGroupFieldSchemaSync(executor.Object);
        var logs = new List<string>();
        sync.SyncSchema(msg => logs.Add(msg));

        Assert.Empty(executedSql);
        Assert.Contains(logs, l => l.Contains("no EcomFieldType found") && l.Contains("99"));
    }

    [Fact]
    public void SyncSchema_EmptyProductGroupFieldTable_IsNoOp()
    {
        var (executor, executedSql) = CreateMockExecutor(
            fields: new List<(string, int)>(), // empty
            fieldTypes: new Dictionary<int, string>(),
            existingColumns: new HashSet<string>());

        var sync = new EcomGroupFieldSchemaSync(executor.Object);
        var logs = new List<string>();
        sync.SyncSchema(msg => logs.Add(msg));

        Assert.Empty(executedSql);
        Assert.Contains(logs, l => l.Contains("nothing to do"));
    }
}
