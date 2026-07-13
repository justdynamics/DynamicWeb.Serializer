using System.Data;
using Truvio.Commerce.Serializer.Providers.SqlTable;
using Dynamicweb.Data;
using Moq;
using Xunit;

namespace Truvio.Commerce.Serializer.Tests.Providers;

/// <summary>
/// LRN-hosted-publish-01: column-backed product fields. EcomProductField definition rows are
/// column-backed on EcomProducts; the schema sync must add the missing columns (typed from
/// EcomFieldType) and warn loudly when a definition has no backing column.
/// </summary>
public class EcomProductFieldSchemaSyncTests
{
    /// <summary>
    /// Mock ISqlExecutor routing ExecuteReader by SQL content and capturing ExecuteNonQuery.
    /// Mirrors the EcomGroupFieldSchemaSync test helper, pointed at EcomProductField/EcomProducts.
    /// </summary>
    private static (Mock<ISqlExecutor> Executor, List<string> ExecutedSql) CreateMockExecutor(
        List<(string SystemName, int TypeId)>? fields = null,
        Dictionary<int, string>? fieldTypes = null,
        HashSet<string>? existingColumns = null)
    {
        var mockExecutor = new Mock<ISqlExecutor>();
        var executedSql = new List<string>();

        mockExecutor.Setup(x => x.ExecuteNonQuery(It.IsAny<CommandBuilder>()))
            .Returns((CommandBuilder cb) =>
            {
                executedSql.Add(cb.ToString());
                return 1;
            });

        var columnsTable = new DataTable();
        columnsTable.Columns.Add("COLUMN_NAME", typeof(string));
        foreach (var col in existingColumns ?? new HashSet<string>())
            columnsTable.Rows.Add(col);

        var fieldsTable = new DataTable();
        fieldsTable.Columns.Add("ProductFieldSystemName", typeof(string));
        fieldsTable.Columns.Add("ProductFieldTypeID", typeof(int));
        foreach (var (name, typeId) in fields ?? new List<(string, int)>())
            fieldsTable.Rows.Add(name, typeId);

        var fieldTypeTables = new Dictionary<int, DataTable>();
        foreach (var (typeId, sqlType) in fieldTypes ?? new Dictionary<int, string>())
        {
            var dt = new DataTable();
            dt.Columns.Add("FieldTypeDBSQL", typeof(string));
            dt.Rows.Add(sqlType);
            fieldTypeTables[typeId] = dt;
        }

        var emptyFieldTypeTable = new DataTable();
        emptyFieldTypeTable.Columns.Add("FieldTypeDBSQL", typeof(string));

        mockExecutor.Setup(x => x.ExecuteReader(It.IsAny<CommandBuilder>()))
            .Returns((CommandBuilder cb) =>
            {
                var sql = cb.ToString();

                if (sql.Contains("INFORMATION_SCHEMA.COLUMNS"))
                    return columnsTable.CreateDataReader();

                if (sql.Contains("EcomProductField"))
                    return fieldsTable.CreateDataReader();

                if (sql.Contains("EcomFieldType"))
                {
                    foreach (var (typeId, table) in fieldTypeTables)
                        if (sql.Contains(typeId.ToString()))
                            return table.CreateDataReader();
                    return emptyFieldTypeTable.CreateDataReader();
                }

                return emptyFieldTypeTable.CreateDataReader();
            });

        return (mockExecutor, executedSql);
    }

    [Fact]
    public void SyncSchema_AddsMissingColumn_OnEcomProducts_WithCorrectSqlType()
    {
        var (executor, executedSql) = CreateMockExecutor(
            fields: new List<(string, int)> { ("ABV", 5) },
            fieldTypes: new Dictionary<int, string> { { 5, "NVARCHAR(255)" } },
            existingColumns: new HashSet<string> { "ProductID", "ProductName" });

        var sync = new EcomProductFieldSchemaSync(executor.Object);
        var logs = new List<string>();
        sync.SyncSchema(msg => logs.Add(msg));

        Assert.Single(executedSql);
        Assert.Contains("ALTER TABLE [EcomProducts] ADD [ABV] NVARCHAR(255)", executedSql[0]);
        Assert.Contains(logs, l => l.Contains("added column [ABV]") && l.Contains("EcomProducts"));
    }

    [Fact]
    public void SyncSchema_SkipsExistingColumn_NoAlterTableExecuted()
    {
        var (executor, executedSql) = CreateMockExecutor(
            fields: new List<(string, int)> { ("BeerStyle", 5) },
            fieldTypes: new Dictionary<int, string> { { 5, "NVARCHAR(255)" } },
            existingColumns: new HashSet<string> { "ProductID", "BeerStyle" });

        var sync = new EcomProductFieldSchemaSync(executor.Object);
        var logs = new List<string>();
        sync.SyncSchema(msg => logs.Add(msg));

        Assert.Empty(executedSql);
        Assert.Contains(logs, l => l.Contains("already exists"));
    }

    [Fact]
    public void SyncSchema_BitColumn_GetsNotNullDefaultConstraint()
    {
        var (executor, executedSql) = CreateMockExecutor(
            fields: new List<(string, int)> { ("IsOrganic", 3) },
            fieldTypes: new Dictionary<int, string> { { 3, "BIT" } },
            existingColumns: new HashSet<string>());

        var sync = new EcomProductFieldSchemaSync(executor.Object);
        sync.SyncSchema();

        Assert.Single(executedSql);
        Assert.Contains("ALTER TABLE [EcomProducts] ADD [IsOrganic] BIT NOT NULL DEFAULT ((0))", executedSql[0]);
    }

    [Fact]
    public void SyncSchema_MissingFieldType_SkipsGracefully()
    {
        var (executor, executedSql) = CreateMockExecutor(
            fields: new List<(string, int)> { ("OrphanField", 99) },
            fieldTypes: new Dictionary<int, string>(),
            existingColumns: new HashSet<string>());

        var sync = new EcomProductFieldSchemaSync(executor.Object);
        var logs = new List<string>();
        sync.SyncSchema(msg => logs.Add(msg));

        Assert.Empty(executedSql);
        Assert.Contains(logs, l => l.Contains("no EcomFieldType found") && l.Contains("99"));
    }

    [Fact]
    public void SyncSchema_EmptyProductFieldTable_IsNoOp()
    {
        var (executor, executedSql) = CreateMockExecutor(
            fields: new List<(string, int)>(),
            fieldTypes: new Dictionary<int, string>(),
            existingColumns: new HashSet<string>());

        var sync = new EcomProductFieldSchemaSync(executor.Object);
        var logs = new List<string>();
        sync.SyncSchema(msg => logs.Add(msg));

        Assert.Empty(executedSql);
        Assert.Contains(logs, l => l.Contains("nothing to do"));
    }

    [Fact]
    public void WarnMissingColumns_EmitsStrictEscalatableWarning_WhenColumnAbsent()
    {
        // Field defined, column absent on EcomProducts (e.g. no EcomFieldType resolved a type).
        var (executor, _) = CreateMockExecutor(
            fields: new List<(string, int)> { ("ABV", 99) },
            fieldTypes: new Dictionary<int, string>(),
            existingColumns: new HashSet<string> { "ProductID" });

        var sync = new EcomProductFieldSchemaSync(executor.Object);
        var logs = new List<string>();
        sync.WarnMissingColumns(msg => logs.Add(msg));

        var warn = Assert.Single(logs);
        // Must start with WARNING so the orchestrator's escalator wrapper elevates it in strict mode.
        Assert.StartsWith("WARNING", warn);
        Assert.Contains("ABV", warn);
        Assert.Contains("EcomProducts", warn);
    }

    [Fact]
    public void WarnMissingColumns_NoWarning_WhenAllColumnsPresent()
    {
        var (executor, _) = CreateMockExecutor(
            fields: new List<(string, int)> { ("ABV", 5) },
            fieldTypes: new Dictionary<int, string> { { 5, "NVARCHAR(255)" } },
            existingColumns: new HashSet<string> { "ProductID", "ABV" });

        var sync = new EcomProductFieldSchemaSync(executor.Object);
        var logs = new List<string>();
        sync.WarnMissingColumns(msg => logs.Add(msg));

        Assert.DoesNotContain(logs, l => l.StartsWith("WARNING"));
    }
}
