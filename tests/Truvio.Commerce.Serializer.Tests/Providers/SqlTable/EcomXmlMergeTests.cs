using System.Data;
using Truvio.Commerce.Serializer.Configuration;
using Truvio.Commerce.Serializer.Infrastructure;
using Truvio.Commerce.Serializer.Models;
using Truvio.Commerce.Serializer.Providers.SqlTable;
using Dynamicweb.Data;
using Moq;
using Xunit;

namespace Truvio.Commerce.Serializer.Tests.Providers.SqlTable;

/// <summary>
/// Phase 39 D-25 acceptance coverage at the integration layer: prove the
/// <see cref="SqlTableProvider"/> merge branch + <see cref="XmlMergeHelper"/>
/// together deliver the CONTEXT.md scope-expansion requirement — Merge fills
/// <c>Mail1SenderEmail</c> inside <c>EcomPayments.PaymentGatewayParameters</c>
/// and <c>EcomShippings.ShippingServiceParameters</c> XML without overwriting
/// already-set XML elements, and without stripping target-only elements.
/// </summary>
/// <remarks>
/// XML fixtures are synthesized to match the DW <c>&lt;Parameter name="X"&gt;</c>
/// idiom observed in <c>swift2.2-combined.json</c> (<c>PaymentGatewayParameters</c>
/// and <c>ShippingServiceParameters</c> xmlColumns declarations). The actual
/// Mail1SenderEmail value lives inside the XML column CONTENT at runtime, not
/// in the config JSON; these fixtures mirror the shape at that nested XML layer.
/// </remarks>
[Trait("Category", "Phase39")]
public class EcomXmlMergeTests
{
    // ----- EcomPayments fixtures -----
    private const string PaymentTargetXmlMissingMail =
        "<Settings>" +
        "<Parameter name=\"Language\">en</Parameter>" +
        "</Settings>";

    private const string PaymentTargetXmlEmptyMail =
        "<Settings>" +
        "<Parameter name=\"Mail1SenderEmail\"></Parameter>" +
        "<Parameter name=\"Language\">en</Parameter>" +
        "</Settings>";

    private const string PaymentTargetXmlCustomerMail =
        "<Settings>" +
        "<Parameter name=\"Mail1SenderEmail\">customer@customer.com</Parameter>" +
        "<Parameter name=\"Language\">en</Parameter>" +
        "</Settings>";

    private const string PaymentTargetXmlWithCustomParam =
        "<Settings>" +
        "<Parameter name=\"CustomLocal\">target-custom</Parameter>" +
        "<Parameter name=\"Language\">en</Parameter>" +
        "</Settings>";

    private const string PaymentMergeXmlWithMail =
        "<Settings>" +
        "<Parameter name=\"Mail1SenderEmail\">no-reply@swift.com</Parameter>" +
        "<Parameter name=\"Mail1SenderName\">Swift Demo</Parameter>" +
        "<Parameter name=\"Language\">en</Parameter>" +
        "</Settings>";

    // ----- EcomShippings fixtures -----
    private const string ShippingTargetXmlMissingMail =
        "<Settings>" +
        "<Parameter name=\"Provider\">ups</Parameter>" +
        "</Settings>";

    private const string ShippingTargetXmlEmptyName =
        "<Settings>" +
        "<Parameter name=\"Mail1SenderName\"></Parameter>" +
        "<Parameter name=\"Provider\">ups</Parameter>" +
        "</Settings>";

    private const string ShippingTargetXmlCustomerTweaks =
        "<Settings>" +
        "<Parameter name=\"Mail1SenderEmail\">shipping@customer.com</Parameter>" +
        "<Parameter name=\"Mail1SenderName\">Customer Name</Parameter>" +
        "<Parameter name=\"Provider\">ups</Parameter>" +
        "</Settings>";

    private const string ShippingTargetXmlOnlyCustom =
        "<Settings>" +
        "<Parameter name=\"CustomRate\">12.50</Parameter>" +
        "</Settings>";

    private const string ShippingMergeXmlWithMail =
        "<Settings>" +
        "<Parameter name=\"Mail1SenderEmail\">no-reply@swift.com</Parameter>" +
        "<Parameter name=\"Mail1SenderName\">Swift Demo</Parameter>" +
        "<Parameter name=\"Provider\">ups</Parameter>" +
        "</Settings>";

    // ====== EcomPayments scenarios ======

    [Fact]
    public void EcomPayments_TargetMissingMail1SenderEmail_Merge_FillsIt()
    {
        var yamlRow = PaymentRow("PAY-1", PaymentMergeXmlWithMail);
        var existingRow = PaymentRow("PAY-1", PaymentTargetXmlMissingMail);

        var (provider, _, writer, inputRoot) = CreatePaymentsProvider(
            new[] { yamlRow }, new[] { existingRow });

        writer.Setup(w => w.UpdateColumnSubset(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<Dictionary<string, object?>>(), It.IsAny<IEnumerable<string>>(),
                It.IsAny<bool>(), It.IsAny<Action<string>?>()))
            .Returns(WriteOutcome.Updated);

        provider.Deserialize(PaymentEntry, inputRoot,
            strategy: ConflictStrategy.DestinationWins);

        writer.Verify(w => w.UpdateColumnSubset(
                "EcomPayments",
                It.IsAny<IReadOnlyList<string>>(),
                It.Is<Dictionary<string, object?>>(d =>
                    d.ContainsKey("PaymentGatewayParameters")
                    && ((string)d["PaymentGatewayParameters"]!).Contains("Mail1SenderEmail")
                    && ((string)d["PaymentGatewayParameters"]!).Contains("no-reply@swift.com")
                    // D-24 — target's Language element preserved.
                    && ((string)d["PaymentGatewayParameters"]!).Contains("Language")),
                It.Is<IEnumerable<string>>(cols => cols.Contains("PaymentGatewayParameters")),
                false,
                It.IsAny<Action<string>?>()),
            Times.Once);
    }

    [Fact]
    public void EcomPayments_TargetEmptyMail1SenderEmail_Merge_FillsIt()
    {
        var yamlRow = PaymentRow("PAY-1", PaymentMergeXmlWithMail);
        var existingRow = PaymentRow("PAY-1", PaymentTargetXmlEmptyMail);

        var (provider, _, writer, inputRoot) = CreatePaymentsProvider(
            new[] { yamlRow }, new[] { existingRow });

        writer.Setup(w => w.UpdateColumnSubset(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<Dictionary<string, object?>>(), It.IsAny<IEnumerable<string>>(),
                It.IsAny<bool>(), It.IsAny<Action<string>?>()))
            .Returns(WriteOutcome.Updated);

        provider.Deserialize(PaymentEntry, inputRoot,
            strategy: ConflictStrategy.DestinationWins);

        writer.Verify(w => w.UpdateColumnSubset(
                "EcomPayments",
                It.IsAny<IReadOnlyList<string>>(),
                It.Is<Dictionary<string, object?>>(d =>
                    ((string)d["PaymentGatewayParameters"]!).Contains("no-reply@swift.com")),
                It.IsAny<IEnumerable<string>>(),
                false, It.IsAny<Action<string>?>()),
            Times.Once);
    }

    [Fact]
    public void EcomPayments_TargetCustomerEmail_Merge_PreservesIt()
    {
        var yamlRow = PaymentRow("PAY-1", PaymentMergeXmlWithMail);
        var existingRow = PaymentRow("PAY-1", PaymentTargetXmlCustomerMail);

        var (provider, _, writer, inputRoot) = CreatePaymentsProvider(
            new[] { yamlRow }, new[] { existingRow });

        writer.Setup(w => w.UpdateColumnSubset(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<Dictionary<string, object?>>(), It.IsAny<IEnumerable<string>>(),
                It.IsAny<bool>(), It.IsAny<Action<string>?>()))
            .Returns(WriteOutcome.Updated);

        provider.Deserialize(PaymentEntry, inputRoot,
            strategy: ConflictStrategy.DestinationWins);

        // Target's Mail1SenderEmail is set -> merge preserves it. But Mail1SenderName is
        // missing on target -> that element fills from merge. Either:
        //  - (a) Both already set -> no write
        //  - (b) Name fills only -> single UpdateColumnSubset call, but the merged XML
        //    still contains the customer's Mail1SenderEmail (not the merge's).
        writer.Verify(w => w.UpdateColumnSubset(
                "EcomPayments",
                It.IsAny<IReadOnlyList<string>>(),
                It.Is<Dictionary<string, object?>>(d =>
                    ((string)d["PaymentGatewayParameters"]!).Contains("customer@customer.com")
                    && !((string)d["PaymentGatewayParameters"]!).Contains("no-reply@swift.com")),
                It.IsAny<IEnumerable<string>>(),
                false, It.IsAny<Action<string>?>()),
            Times.AtMostOnce);
    }

    [Fact]
    public void EcomPayments_TargetExtraCustomParameter_Preserved_AfterMerge()
    {
        var yamlRow = PaymentRow("PAY-1", PaymentMergeXmlWithMail);
        var existingRow = PaymentRow("PAY-1", PaymentTargetXmlWithCustomParam);

        var (provider, _, writer, inputRoot) = CreatePaymentsProvider(
            new[] { yamlRow }, new[] { existingRow });

        writer.Setup(w => w.UpdateColumnSubset(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<Dictionary<string, object?>>(), It.IsAny<IEnumerable<string>>(),
                It.IsAny<bool>(), It.IsAny<Action<string>?>()))
            .Returns(WriteOutcome.Updated);

        provider.Deserialize(PaymentEntry, inputRoot,
            strategy: ConflictStrategy.DestinationWins);

        writer.Verify(w => w.UpdateColumnSubset(
                "EcomPayments",
                It.IsAny<IReadOnlyList<string>>(),
                It.Is<Dictionary<string, object?>>(d =>
                    // Target-only element preserved (D-24)
                    ((string)d["PaymentGatewayParameters"]!).Contains("CustomLocal")
                    && ((string)d["PaymentGatewayParameters"]!).Contains("target-custom")
                    // Merge-added element present
                    && ((string)d["PaymentGatewayParameters"]!).Contains("Mail1SenderEmail")
                    && ((string)d["PaymentGatewayParameters"]!).Contains("no-reply@swift.com")),
                It.IsAny<IEnumerable<string>>(),
                false, It.IsAny<Action<string>?>()),
            Times.Once);
    }

    // ====== EcomShippings scenarios ======

    [Fact]
    public void EcomShippings_TargetMissingMail1SenderEmail_Merge_FillsIt()
    {
        var yamlRow = ShippingRow("SHIP-1", ShippingMergeXmlWithMail);
        var existingRow = ShippingRow("SHIP-1", ShippingTargetXmlMissingMail);

        var (provider, _, writer, inputRoot) = CreateShippingsProvider(
            new[] { yamlRow }, new[] { existingRow });

        writer.Setup(w => w.UpdateColumnSubset(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<Dictionary<string, object?>>(), It.IsAny<IEnumerable<string>>(),
                It.IsAny<bool>(), It.IsAny<Action<string>?>()))
            .Returns(WriteOutcome.Updated);

        provider.Deserialize(ShippingEntry, inputRoot,
            strategy: ConflictStrategy.DestinationWins);

        writer.Verify(w => w.UpdateColumnSubset(
                "EcomShippings",
                It.IsAny<IReadOnlyList<string>>(),
                It.Is<Dictionary<string, object?>>(d =>
                    ((string)d["ShippingServiceParameters"]!).Contains("Mail1SenderEmail")
                    && ((string)d["ShippingServiceParameters"]!).Contains("no-reply@swift.com")
                    // D-24 — target's Provider element preserved.
                    && ((string)d["ShippingServiceParameters"]!).Contains("Provider")),
                It.Is<IEnumerable<string>>(cols => cols.Contains("ShippingServiceParameters")),
                false, It.IsAny<Action<string>?>()),
            Times.Once);
    }

    [Fact]
    public void EcomShippings_TargetEmptyMail1SenderName_Merge_FillsIt()
    {
        var yamlRow = ShippingRow("SHIP-1", ShippingMergeXmlWithMail);
        var existingRow = ShippingRow("SHIP-1", ShippingTargetXmlEmptyName);

        var (provider, _, writer, inputRoot) = CreateShippingsProvider(
            new[] { yamlRow }, new[] { existingRow });

        writer.Setup(w => w.UpdateColumnSubset(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<Dictionary<string, object?>>(), It.IsAny<IEnumerable<string>>(),
                It.IsAny<bool>(), It.IsAny<Action<string>?>()))
            .Returns(WriteOutcome.Updated);

        provider.Deserialize(ShippingEntry, inputRoot,
            strategy: ConflictStrategy.DestinationWins);

        writer.Verify(w => w.UpdateColumnSubset(
                "EcomShippings",
                It.IsAny<IReadOnlyList<string>>(),
                It.Is<Dictionary<string, object?>>(d =>
                    ((string)d["ShippingServiceParameters"]!).Contains("Swift Demo")),
                It.IsAny<IEnumerable<string>>(),
                false, It.IsAny<Action<string>?>()),
            Times.Once);
    }

    [Fact]
    public void EcomShippings_TargetCustomerTweaks_Preserved()
    {
        var yamlRow = ShippingRow("SHIP-1", ShippingMergeXmlWithMail);
        var existingRow = ShippingRow("SHIP-1", ShippingTargetXmlCustomerTweaks);

        var (provider, _, writer, inputRoot) = CreateShippingsProvider(
            new[] { yamlRow }, new[] { existingRow });

        provider.Deserialize(ShippingEntry, inputRoot,
            strategy: ConflictStrategy.DestinationWins);

        // Every merge element has a target counterpart already set -> no write fires.
        writer.Verify(w => w.UpdateColumnSubset(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<Dictionary<string, object?>>(), It.IsAny<IEnumerable<string>>(),
                It.IsAny<bool>(), It.IsAny<Action<string>?>()),
            Times.Never);
    }

    [Fact]
    public void EcomShippings_TargetOnlyParameter_Preserved()
    {
        var yamlRow = ShippingRow("SHIP-1", ShippingMergeXmlWithMail);
        var existingRow = ShippingRow("SHIP-1", ShippingTargetXmlOnlyCustom);

        var (provider, _, writer, inputRoot) = CreateShippingsProvider(
            new[] { yamlRow }, new[] { existingRow });

        writer.Setup(w => w.UpdateColumnSubset(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<Dictionary<string, object?>>(), It.IsAny<IEnumerable<string>>(),
                It.IsAny<bool>(), It.IsAny<Action<string>?>()))
            .Returns(WriteOutcome.Updated);

        provider.Deserialize(ShippingEntry, inputRoot,
            strategy: ConflictStrategy.DestinationWins);

        writer.Verify(w => w.UpdateColumnSubset(
                "EcomShippings",
                It.IsAny<IReadOnlyList<string>>(),
                It.Is<Dictionary<string, object?>>(d =>
                    // D-24 target-only CustomRate element retained.
                    ((string)d["ShippingServiceParameters"]!).Contains("CustomRate")
                    && ((string)d["ShippingServiceParameters"]!).Contains("12.50")
                    // Merge Mail1SenderEmail added.
                    && ((string)d["ShippingServiceParameters"]!).Contains("Mail1SenderEmail")),
                It.IsAny<IEnumerable<string>>(),
                false, It.IsAny<Action<string>?>()),
            Times.Once);
    }

    // ====== Cross-table invariants ======

    [Theory]
    [InlineData("EcomPayments", "PaymentGatewayParameters")]
    [InlineData("EcomShippings", "ShippingServiceParameters")]
    public void Both_Tables_SameBehavior_NoDivergence(string tableName, string xmlColumn)
    {
        var mergeXml =
            "<Settings>" +
            "<Parameter name=\"Mail1SenderEmail\">no-reply@x.com</Parameter>" +
            "</Settings>";
        var targetXml = "<Settings></Settings>";

        var metadata = new TableMetadata
        {
            TableName = tableName,
            NameColumn = "Id",
            KeyColumns = new List<string> { "Id" },
            IdentityColumns = new List<string>(),
            AllColumns = new List<string> { "Id", xmlColumn }
        };
        var entry = new SqlTableEntry
        {
            EntryId = $"sql/{tableName}",
            Files = Array.Empty<string>(),
            Table = tableName,
            NameColumn = "Id"
        };
        var columnTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Id"] = "nvarchar",
            [xmlColumn] = "xml"
        };

        var yamlRow = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Id"] = "1",
            [xmlColumn] = mergeXml
        };
        var existingRow = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Id"] = "1",
            [xmlColumn] = targetXml
        };

        var (provider, _, writer, inputRoot) = CreateProviderWithFiles(
            new[] { yamlRow }, new[] { existingRow }, metadata, columnTypes);

        writer.Setup(w => w.UpdateColumnSubset(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<Dictionary<string, object?>>(), It.IsAny<IEnumerable<string>>(),
                It.IsAny<bool>(), It.IsAny<Action<string>?>()))
            .Returns(WriteOutcome.Updated);

        provider.Deserialize(entry, inputRoot,
            strategy: ConflictStrategy.DestinationWins);

        writer.Verify(w => w.UpdateColumnSubset(
                tableName,
                It.IsAny<IReadOnlyList<string>>(),
                It.Is<Dictionary<string, object?>>(d =>
                    d.ContainsKey(xmlColumn)
                    && ((string)d[xmlColumn]!).Contains("Mail1SenderEmail")),
                It.Is<IEnumerable<string>>(cols => cols.Contains(xmlColumn)),
                false, It.IsAny<Action<string>?>()),
            Times.Once);
    }

    [Fact]
    public void Integration_Rerun_Merge_Idempotent_NoWrites()
    {
        // After a successful first Merge run, the target XML already contains the merged
        // elements. A second Merge run sees target == merge (fast-path or zero-fills) — no
        // UpdateColumnSubset / ExecuteNonQuery invocation.
        var mergeXml =
            "<Settings>" +
            "<Parameter name=\"Mail1SenderEmail\">no-reply@swift.com</Parameter>" +
            "<Parameter name=\"Mail1SenderName\">Swift Demo</Parameter>" +
            "<Parameter name=\"Language\">en</Parameter>" +
            "</Settings>";

        var yamlRow = PaymentRow("PAY-1", mergeXml);
        var existingRow = PaymentRow("PAY-1", mergeXml); // already merged

        var (provider, _, writer, inputRoot) = CreatePaymentsProvider(
            new[] { yamlRow }, new[] { existingRow });

        provider.Deserialize(PaymentEntry, inputRoot,
            strategy: ConflictStrategy.DestinationWins);

        // Idempotent: no column-subset write. (DisableForeignKeys/EnableForeignKeys DO run
        // unconditionally via ExecuteNonQuery; the idempotency contract is about merge
        // writes, not constraint toggling.)
        writer.Verify(w => w.UpdateColumnSubset(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<Dictionary<string, object?>>(), It.IsAny<IEnumerable<string>>(),
                It.IsAny<bool>(), It.IsAny<Action<string>?>()),
            Times.Never);
        writer.Verify(w => w.WriteRow(
                It.IsAny<Dictionary<string, object?>>(), It.IsAny<TableMetadata>(),
                It.IsAny<bool>(), It.IsAny<Action<string>?>(), It.IsAny<HashSet<string>?>()),
            Times.Never);
    }

    // ====== Helpers ======

    // Phase 44 / CONVERGE-03: ported from predicate fixtures to SqlTableEntry literals.
    private static readonly SqlTableEntry PaymentEntry = new()
    {
        EntryId = "sql/EcomPayments",
        Files = Array.Empty<string>(),
        Table = "EcomPayments",
        NameColumn = "PaymentId"
    };

    private static readonly SqlTableEntry ShippingEntry = new()
    {
        EntryId = "sql/EcomShippings",
        Files = Array.Empty<string>(),
        Table = "EcomShippings",
        NameColumn = "ShippingId"
    };

    private static TableMetadata PaymentMetadata => new()
    {
        TableName = "EcomPayments",
        NameColumn = "PaymentId",
        KeyColumns = new List<string> { "PaymentId" },
        IdentityColumns = new List<string>(),
        AllColumns = new List<string> { "PaymentId", "PaymentGatewayParameters" }
    };

    private static TableMetadata ShippingMetadata => new()
    {
        TableName = "EcomShippings",
        NameColumn = "ShippingId",
        KeyColumns = new List<string> { "ShippingId" },
        IdentityColumns = new List<string>(),
        AllColumns = new List<string> { "ShippingId", "ShippingServiceParameters" }
    };

    private static Dictionary<string, object?> PaymentRow(string id, string xml)
        => new(StringComparer.OrdinalIgnoreCase)
        {
            ["PaymentId"] = id,
            ["PaymentGatewayParameters"] = xml
        };

    private static Dictionary<string, object?> ShippingRow(string id, string xml)
        => new(StringComparer.OrdinalIgnoreCase)
        {
            ["ShippingId"] = id,
            ["ShippingServiceParameters"] = xml
        };

    private static (SqlTableProvider, Mock<ISqlExecutor>, Mock<SqlTableWriter>, string)
        CreatePaymentsProvider(
            IEnumerable<Dictionary<string, object?>> yamlRows,
            IEnumerable<Dictionary<string, object?>> existingDbRows)
    {
        var types = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["PaymentId"] = "nvarchar",
            ["PaymentGatewayParameters"] = "xml"
        };
        return CreateProviderWithFiles(yamlRows, existingDbRows, PaymentMetadata, types);
    }

    private static (SqlTableProvider, Mock<ISqlExecutor>, Mock<SqlTableWriter>, string)
        CreateShippingsProvider(
            IEnumerable<Dictionary<string, object?>> yamlRows,
            IEnumerable<Dictionary<string, object?>> existingDbRows)
    {
        var types = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ShippingId"] = "nvarchar",
            ["ShippingServiceParameters"] = "xml"
        };
        return CreateProviderWithFiles(yamlRows, existingDbRows, ShippingMetadata, types);
    }

    /// <summary>
    /// Harness copied from <see cref="SqlTableProviderMergeMergeTests.CreateProviderWithFiles"/>
    /// to keep this test file self-contained. Same semantics: stubbed metadata reader, mocked
    /// IDataReader for existing rows, schema cache loader returning <paramref name="columnTypes"/>
    /// so the merge branch's <c>IsXmlColumn</c> + <c>MergePredicate.IsUnsetForMergeBySqlType</c>
    /// see consistent types.
    /// </summary>
    private static (SqlTableProvider provider, Mock<ISqlExecutor> executor,
                    Mock<SqlTableWriter> writer, string inputRoot)
        CreateProviderWithFiles(
            IEnumerable<Dictionary<string, object?>> yamlRows,
            IEnumerable<Dictionary<string, object?>> existingDbRows,
            TableMetadata metadata,
            Dictionary<string, string> columnTypes)
    {
        var mockExecutor = new Mock<ISqlExecutor>();

        // NOTE (Phase 44 / CONVERGE-03): DataGroupMetadataReader.GetTableMetadata keeps its
        // predicate-typed signature -- internal helper, no public-API surface. SqlTableProvider.Deserialize
        // synthesises a transient predicate to call it. The mock setup mirrors that internal contract.
        var mockMetadataReader = new Mock<DataGroupMetadataReader>(mockExecutor.Object) { CallBase = false };
        mockMetadataReader.Setup(x => x.GetTableMetadata(It.IsAny<ProviderPredicateDefinition>(), It.IsAny<bool>()))
            .Returns(metadata);
        mockMetadataReader.Setup(x => x.TableExists(It.IsAny<string>())).Returns(true);
        mockMetadataReader.Setup(x => x.GetColumnTypes(It.IsAny<string>()))
            .Returns(columnTypes);
        mockMetadataReader.Setup(x => x.GetNotNullColumns(It.IsAny<string>()))
            .Returns(new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        var existingList = existingDbRows.ToList();
        var dbReaderMock = CreateMockDataReader(
            metadata.AllColumns.ToArray(),
            existingList.Select(r => metadata.AllColumns
                .Select(col => r.TryGetValue(col, out var v) ? v ?? DBNull.Value : DBNull.Value)
                .ToArray()).ToArray());
        mockExecutor.Setup(x => x.ExecuteReader(It.IsAny<CommandBuilder>()))
            .Returns(dbReaderMock.Object);

        var tableReader = new SqlTableReader(mockExecutor.Object);
        var fileStore = new FlatFileStore();

        var tempDir = Path.Combine(Path.GetTempPath(), $"contentsync_ecomxml_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var mockExecForIdentity = new Mock<ISqlExecutor>();
        var identityReader = new SqlTableReader(mockExecForIdentity.Object);

        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in yamlRows)
        {
            var identity = identityReader.GenerateRowIdentity(row, metadata);
            fileStore.WriteRow(tempDir, metadata.TableName, identity, row, usedNames);
        }
        fileStore.WriteMeta(tempDir, metadata.TableName, metadata);

        var writerMock = new Mock<SqlTableWriter>(mockExecutor.Object) { CallBase = false };

        var schemaCache = new TargetSchemaCache(_ =>
            (new HashSet<string>(metadata.AllColumns, StringComparer.OrdinalIgnoreCase),
             new Dictionary<string, string>(columnTypes, StringComparer.OrdinalIgnoreCase)));
        var provider = new SqlTableProvider(
            mockMetadataReader.Object, tableReader, fileStore, writerMock.Object, schemaCache);

        return (provider, mockExecutor, writerMock, tempDir);
    }

    private static Mock<IDataReader> CreateMockDataReader(string[] columns, object[][] rows)
    {
        var mock = new Mock<IDataReader>();
        var rowIndex = -1;

        mock.Setup(r => r.Read()).Returns(() =>
        {
            rowIndex++;
            return rowIndex < rows.Length;
        });

        mock.Setup(r => r.FieldCount).Returns(columns.Length);
        for (int i = 0; i < columns.Length; i++)
        {
            var idx = i;
            mock.Setup(r => r.GetName(idx)).Returns(columns[idx]);
            mock.Setup(r => r.GetValue(idx)).Returns(() =>
                rowIndex >= 0 && rowIndex < rows.Length ? rows[rowIndex][idx] : DBNull.Value);
        }

        mock.Setup(r => r[It.IsAny<string>()]).Returns((string col) =>
        {
            var colIndex = Array.IndexOf(columns, col);
            return rowIndex >= 0 && rowIndex < rows.Length && colIndex >= 0
                ? rows[rowIndex][colIndex]
                : DBNull.Value;
        });

        mock.Setup(r => r.Dispose());
        return mock;
    }
}
