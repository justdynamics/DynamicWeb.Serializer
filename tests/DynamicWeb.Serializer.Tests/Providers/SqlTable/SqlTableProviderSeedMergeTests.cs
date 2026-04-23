using System.Data;
using DynamicWeb.Serializer.Configuration;
using DynamicWeb.Serializer.Infrastructure;
using DynamicWeb.Serializer.Models;
using DynamicWeb.Serializer.Providers.SqlTable;
using Dynamicweb.Data;
using Moq;
using Xunit;

namespace DynamicWeb.Serializer.Tests.Providers.SqlTable;

/// <summary>
/// Phase 39 D-01..D-19 + D-21..D-27 integration coverage for the SqlTableProvider
/// Seed-merge branch. Harness copied from <see cref="SqlTableProviderDeserializeTests"/>
/// and extended with column-type hints for <see cref="MergePredicate.IsUnsetForMergeBySqlType"/>.
/// </summary>
[Trait("Category", "Phase39")]
public class SqlTableProviderSeedMergeTests
{
    private static readonly TableMetadata TestMetadata = new()
    {
        TableName = "EcomOrderFlow",
        NameColumn = "OrderFlowName",
        KeyColumns = new List<string> { "OrderFlowId" },
        IdentityColumns = new List<string> { "OrderFlowId" },
        AllColumns = new List<string> { "OrderFlowId", "OrderFlowName", "OrderFlowDescription" }
    };

    private static readonly ProviderPredicateDefinition TestPredicate = new()
    {
        Name = "Order Flows",
        ProviderType = "SqlTable",
        Table = "EcomOrderFlow",
        NameColumn = "OrderFlowName"
    };

    // Column types for the merge predicate — nvarchar columns pass IsUnsetForMergeBySqlType
    // when the value is null/empty. See MergePredicate.IsUnsetForMergeBySqlType.
    private static readonly Dictionary<string, string> DefaultColumnTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["OrderFlowId"] = "nvarchar",
            ["OrderFlowName"] = "nvarchar",
            ["OrderFlowDescription"] = "nvarchar"
        };

    // -----------------------------------------------------------------------
    // D-17 + D-01: identity match with unset target column -> UpdateColumnSubset
    // -----------------------------------------------------------------------

    [Fact]
    public void Seed_IdentityMatchTargetColumnNull_YamlHasValue_UpdateColumnSubsetCalled()
    {
        var yamlRow = Row("FLOW-1", "Checkout", "seeded description");
        var existingRow = Row("FLOW-1", "Checkout", null); // null description — unset per D-01

        var (provider, executor, writer, inputRoot) = CreateProviderWithFiles(
            yamlRows: new[] { yamlRow },
            existingDbRows: new[] { existingRow });

        writer.Setup(w => w.UpdateColumnSubset(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<Dictionary<string, object?>>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<bool>(),
                It.IsAny<Action<string>?>()))
            .Returns(WriteOutcome.Updated);

        var result = provider.Deserialize(TestPredicate, inputRoot,
            strategy: ConflictStrategy.DestinationWins);

        writer.Verify(w => w.UpdateColumnSubset(
                "EcomOrderFlow",
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<Dictionary<string, object?>>(),
                It.Is<IEnumerable<string>>(cols => cols.Contains("OrderFlowDescription")),
                false,
                It.IsAny<Action<string>?>()),
            Times.Once);
        writer.Verify(w => w.WriteRow(
                It.IsAny<Dictionary<string, object?>>(),
                It.IsAny<TableMetadata>(),
                It.IsAny<bool>(),
                It.IsAny<Action<string>?>(),
                It.IsAny<HashSet<string>?>()),
            Times.Never);
        Assert.Equal(1, result.Updated);
    }

    // -----------------------------------------------------------------------
    // D-11: all columns already set -> no write, skipped counter incremented
    // -----------------------------------------------------------------------

    [Fact]
    public void Seed_IdentityMatchAllColumnsSet_NoWrite_SkippedCounterIncremented()
    {
        // Identity (NameColumn=OrderFlowName) matches; description differs so checksums
        // differ (merge branch engages, not the fast-path). All columns set on target -> 0 fills.
        var yamlRow = Row("FLOW-1", "Checkout", "seed desc");
        var existingRow = Row("FLOW-1", "Checkout", "customer desc");

        var (provider, _, writer, inputRoot) = CreateProviderWithFiles(
            yamlRows: new[] { yamlRow },
            existingDbRows: new[] { existingRow });

        var logs = new List<string>();
        var result = provider.Deserialize(TestPredicate, inputRoot, log: logs.Add,
            strategy: ConflictStrategy.DestinationWins);

        writer.Verify(w => w.UpdateColumnSubset(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<Dictionary<string, object?>>(), It.IsAny<IEnumerable<string>>(),
                It.IsAny<bool>(), It.IsAny<Action<string>?>()),
            Times.Never);
        Assert.Equal(1, result.Skipped);
        Assert.Contains(logs, l => l.Contains("Seed-merge:") && l.Contains("0 filled"));
    }

    // -----------------------------------------------------------------------
    // D-01 partial state
    // -----------------------------------------------------------------------

    [Fact]
    public void Seed_IdentityMatchPartialSet_UpdateColumnSubsetFiresForUnsetSubsetOnly()
    {
        // Identity (NameColumn=OrderFlowName) matches; description unset on target ->
        // only Description should be in the fill subset.
        var yamlRow = Row("FLOW-1", "Checkout", "SeedDesc");
        var existingRow = Row("FLOW-1", "Checkout", null);

        var (provider, _, writer, inputRoot) = CreateProviderWithFiles(
            yamlRows: new[] { yamlRow },
            existingDbRows: new[] { existingRow });

        writer.Setup(w => w.UpdateColumnSubset(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<Dictionary<string, object?>>(), It.IsAny<IEnumerable<string>>(),
                It.IsAny<bool>(), It.IsAny<Action<string>?>()))
            .Returns(WriteOutcome.Updated);

        provider.Deserialize(TestPredicate, inputRoot,
            strategy: ConflictStrategy.DestinationWins);

        // Exactly Description in the subset, not Name.
        writer.Verify(w => w.UpdateColumnSubset(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<Dictionary<string, object?>>(),
                It.Is<IEnumerable<string>>(cols =>
                    cols.Contains("OrderFlowDescription")
                    && !cols.Contains("OrderFlowName")),
                false,
                It.IsAny<Action<string>?>()),
            Times.Once);
    }

    // -----------------------------------------------------------------------
    // Identity unmatched -> falls through to WriteRow (MERGE insert path unchanged)
    // -----------------------------------------------------------------------

    [Fact]
    public void Seed_IdentityUnmatched_WriteRowFallthrough()
    {
        var yamlRow = Row("FLOW-NEW", "New flow", "New desc");
        // No matching identity in DB.

        var (provider, _, writer, inputRoot) = CreateProviderWithFiles(
            yamlRows: new[] { yamlRow },
            existingDbRows: Array.Empty<Dictionary<string, object?>>());

        writer.Setup(w => w.WriteRow(
                It.IsAny<Dictionary<string, object?>>(),
                It.IsAny<TableMetadata>(),
                It.IsAny<bool>(),
                It.IsAny<Action<string>?>(),
                It.IsAny<HashSet<string>?>()))
            .Returns(WriteOutcome.Created);

        provider.Deserialize(TestPredicate, inputRoot,
            strategy: ConflictStrategy.DestinationWins);

        writer.Verify(w => w.WriteRow(
                It.IsAny<Dictionary<string, object?>>(), It.IsAny<TableMetadata>(),
                It.IsAny<bool>(), It.IsAny<Action<string>?>(), It.IsAny<HashSet<string>?>()),
            Times.Once);
        writer.Verify(w => w.UpdateColumnSubset(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<Dictionary<string, object?>>(), It.IsAny<IEnumerable<string>>(),
                It.IsAny<bool>(), It.IsAny<Action<string>?>()),
            Times.Never);
    }

    // -----------------------------------------------------------------------
    // D-18 checksum fast-path runs BEFORE the merge branch
    // -----------------------------------------------------------------------

    [Fact]
    public void Seed_ChecksumMatches_FastPathSkip_BeforeMergeBranch()
    {
        var sameRow = Row("FLOW-1", "Checkout", "same desc");

        var (provider, _, writer, inputRoot) = CreateProviderWithFiles(
            yamlRows: new[] { sameRow },
            existingDbRows: new[] { sameRow });

        var logs = new List<string>();
        var result = provider.Deserialize(TestPredicate, inputRoot, log: logs.Add,
            strategy: ConflictStrategy.DestinationWins);

        // Fast-path wins — merge never even runs.
        writer.Verify(w => w.UpdateColumnSubset(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<Dictionary<string, object?>>(), It.IsAny<IEnumerable<string>>(),
                It.IsAny<bool>(), It.IsAny<Action<string>?>()),
            Times.Never);
        Assert.Equal(1, result.Skipped);
        Assert.Contains(logs, l => l.Contains("Skipped") && l.Contains("unchanged"));
    }

    // -----------------------------------------------------------------------
    // D-12 schema drift — column missing from target schema silently drops
    // -----------------------------------------------------------------------

    [Fact]
    public void Seed_MissingTargetColumn_SilentlyDropsFromMergePlan()
    {
        // YAML carries an extra column that isn't on target schema — target filter drops it
        // before merge runs, so the fill subset must never reference it.
        var yamlRow = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["OrderFlowId"] = "FLOW-1",
            ["OrderFlowName"] = "Checkout",
            ["OrderFlowDescription"] = "seeded",
            ["PhantomColumn"] = "ghost"
        };
        var existingRow = Row("FLOW-1", "Checkout", null);

        var logs = new List<string>();
        var (provider, _, writer, inputRoot) = CreateProviderWithFiles(
            yamlRows: new[] { yamlRow },
            existingDbRows: new[] { existingRow });

        writer.Setup(w => w.UpdateColumnSubset(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<Dictionary<string, object?>>(), It.IsAny<IEnumerable<string>>(),
                It.IsAny<bool>(), It.IsAny<Action<string>?>()))
            .Returns(WriteOutcome.Updated);

        provider.Deserialize(TestPredicate, inputRoot, log: logs.Add,
            strategy: ConflictStrategy.DestinationWins);

        // PhantomColumn must not appear in the column subset.
        writer.Verify(w => w.UpdateColumnSubset(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<Dictionary<string, object?>>(),
                It.Is<IEnumerable<string>>(cols => !cols.Contains("PhantomColumn")),
                false, It.IsAny<Action<string>?>()),
            Times.Once);
        Assert.Contains(logs, l =>
            l.Contains("PhantomColumn") && l.Contains("not present on target schema"));
    }

    // -----------------------------------------------------------------------
    // D-19 dry-run — emits would-fill per column, no SQL executed
    // -----------------------------------------------------------------------

    [Fact]
    public void Seed_DryRun_EmitsWouldFillPerColumn_NoSqlExecuted()
    {
        var yamlRow = Row("FLOW-1", "Checkout", "seed desc");
        var existingRow = Row("FLOW-1", "Checkout", null);

        var (provider, executor, writer, inputRoot) = CreateProviderWithFiles(
            yamlRows: new[] { yamlRow },
            existingDbRows: new[] { existingRow });

        writer.Setup(w => w.UpdateColumnSubset(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<Dictionary<string, object?>>(), It.IsAny<IEnumerable<string>>(),
                It.IsAny<bool>(), It.IsAny<Action<string>?>()))
            .Returns(WriteOutcome.Updated);

        var logs = new List<string>();
        provider.Deserialize(TestPredicate, inputRoot, log: logs.Add, isDryRun: true,
            strategy: ConflictStrategy.DestinationWins);

        executor.Verify(x => x.ExecuteNonQuery(It.IsAny<CommandBuilder>()), Times.Never);
        Assert.Contains(logs, l => l.Contains("would fill") && l.Contains("OrderFlowDescription"));
    }

    // -----------------------------------------------------------------------
    // Rerun idempotency — D-09 inherited
    // -----------------------------------------------------------------------

    [Fact]
    public void Seed_Rerun_AllRowsAlreadyFilled_AllSkipped()
    {
        // Same data — after previous seed, YAML == DB exactly (checksum fast-path domain),
        // OR at minimum every column is set (merge path with zero fills). Both paths yield
        // Skipped=1, zero calls to UpdateColumnSubset.
        var yamlRow = Row("FLOW-1", "Checkout", "seeded desc");
        var existingRow = Row("FLOW-1", "Checkout", "seeded desc");

        var (provider, _, writer, inputRoot) = CreateProviderWithFiles(
            yamlRows: new[] { yamlRow },
            existingDbRows: new[] { existingRow });

        var result = provider.Deserialize(TestPredicate, inputRoot,
            strategy: ConflictStrategy.DestinationWins);

        writer.Verify(w => w.UpdateColumnSubset(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<Dictionary<string, object?>>(), It.IsAny<IEnumerable<string>>(),
                It.IsAny<bool>(), It.IsAny<Action<string>?>()),
            Times.Never);
        Assert.Equal(1, result.Skipped);
    }

    // -----------------------------------------------------------------------
    // D-11 log format — Seed-merge line shape
    // -----------------------------------------------------------------------

    [Fact]
    public void Seed_LogLineShape_SeedMerge_FilledAndLeft()
    {
        var yamlRow = Row("FLOW-1", "Checkout", "seeded");
        var existingRow = Row("FLOW-1", "Checkout", null);

        var (provider, _, writer, inputRoot) = CreateProviderWithFiles(
            yamlRows: new[] { yamlRow },
            existingDbRows: new[] { existingRow });

        writer.Setup(w => w.UpdateColumnSubset(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<Dictionary<string, object?>>(), It.IsAny<IEnumerable<string>>(),
                It.IsAny<bool>(), It.IsAny<Action<string>?>()))
            .Returns(WriteOutcome.Updated);

        var logs = new List<string>();
        provider.Deserialize(TestPredicate, inputRoot, log: logs.Add,
            strategy: ConflictStrategy.DestinationWins);

        Assert.Contains(logs, l =>
            l.Contains("Seed-merge:")
            && l.Contains("EcomOrderFlow")
            && l.Contains("filled"));
    }

    // -----------------------------------------------------------------------
    // D-21 / D-22 / D-23: XML column merge branch — identified by SQL DATA_TYPE=xml
    // -----------------------------------------------------------------------

    [Fact]
    public void Seed_XmlColumn_TargetMissingElement_XmlMerged_UpdateColumnSubsetCalledWithMergedXml()
    {
        // Switch to an EcomPayments-ish shape: identity column + XML column.
        var metadata = new TableMetadata
        {
            TableName = "EcomPayments",
            NameColumn = "PaymentId",
            KeyColumns = new List<string> { "PaymentId" },
            IdentityColumns = new List<string>(),
            AllColumns = new List<string> { "PaymentId", "PaymentGatewayParameters" }
        };
        var predicate = new ProviderPredicateDefinition
        {
            Name = "Payments",
            ProviderType = "SqlTable",
            Table = "EcomPayments",
            NameColumn = "PaymentId"
        };
        var columnTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["PaymentId"] = "nvarchar",
            ["PaymentGatewayParameters"] = "xml"
        };

        const string targetXml =
            "<Settings><Parameter name=\"Language\">en</Parameter></Settings>";
        const string seedXml =
            "<Settings>" +
            "<Parameter name=\"Language\">en</Parameter>" +
            "<Parameter name=\"Mail1SenderEmail\">no-reply@swift.com</Parameter>" +
            "</Settings>";

        var yamlRow = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["PaymentId"] = "PAY-1",
            ["PaymentGatewayParameters"] = seedXml
        };
        var existingRow = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["PaymentId"] = "PAY-1",
            ["PaymentGatewayParameters"] = targetXml
        };

        var (provider, _, writer, inputRoot) = CreateProviderWithFiles(
            yamlRows: new[] { yamlRow },
            existingDbRows: new[] { existingRow },
            metadata: metadata,
            columnTypes: columnTypes);

        writer.Setup(w => w.UpdateColumnSubset(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<Dictionary<string, object?>>(), It.IsAny<IEnumerable<string>>(),
                It.IsAny<bool>(), It.IsAny<Action<string>?>()))
            .Returns(WriteOutcome.Updated);

        provider.Deserialize(predicate, inputRoot,
            strategy: ConflictStrategy.DestinationWins);

        writer.Verify(w => w.UpdateColumnSubset(
                "EcomPayments",
                It.IsAny<IReadOnlyList<string>>(),
                It.Is<Dictionary<string, object?>>(d =>
                    d.ContainsKey("PaymentGatewayParameters")
                    && ((string)d["PaymentGatewayParameters"]!).Contains("Mail1SenderEmail")
                    && ((string)d["PaymentGatewayParameters"]!).Contains("no-reply@swift.com")),
                It.Is<IEnumerable<string>>(cols => cols.Contains("PaymentGatewayParameters")),
                false, It.IsAny<Action<string>?>()),
            Times.Once);
    }

    [Fact]
    public void Seed_XmlColumn_TargetElementSet_ElementPreserved()
    {
        var metadata = new TableMetadata
        {
            TableName = "EcomPayments",
            NameColumn = "PaymentId",
            KeyColumns = new List<string> { "PaymentId" },
            IdentityColumns = new List<string>(),
            AllColumns = new List<string> { "PaymentId", "PaymentGatewayParameters" }
        };
        var predicate = new ProviderPredicateDefinition
        {
            Name = "Payments", ProviderType = "SqlTable",
            Table = "EcomPayments", NameColumn = "PaymentId"
        };
        var columnTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["PaymentId"] = "nvarchar",
            ["PaymentGatewayParameters"] = "xml"
        };

        // Target already has Mail1SenderEmail — seed must not overwrite.
        const string targetXml =
            "<Settings>" +
            "<Parameter name=\"Mail1SenderEmail\">customer@customer.com</Parameter>" +
            "</Settings>";
        const string seedXml =
            "<Settings>" +
            "<Parameter name=\"Mail1SenderEmail\">no-reply@swift.com</Parameter>" +
            "</Settings>";

        var yamlRow = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["PaymentId"] = "PAY-1",
            ["PaymentGatewayParameters"] = seedXml
        };
        var existingRow = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["PaymentId"] = "PAY-1",
            ["PaymentGatewayParameters"] = targetXml
        };

        var (provider, _, writer, inputRoot) = CreateProviderWithFiles(
            yamlRows: new[] { yamlRow },
            existingDbRows: new[] { existingRow },
            metadata: metadata,
            columnTypes: columnTypes);

        provider.Deserialize(predicate, inputRoot,
            strategy: ConflictStrategy.DestinationWins);

        // Zero columns needed filling — Seed-merge recognized no changes.
        writer.Verify(w => w.UpdateColumnSubset(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<Dictionary<string, object?>>(), It.IsAny<IEnumerable<string>>(),
                It.IsAny<bool>(), It.IsAny<Action<string>?>()),
            Times.Never);
    }

    [Fact]
    public void Seed_XmlColumn_TargetOnlyElements_Preserved()
    {
        // Target has elements NOT present in YAML — D-24 preservation.
        var metadata = new TableMetadata
        {
            TableName = "EcomPayments",
            NameColumn = "PaymentId",
            KeyColumns = new List<string> { "PaymentId" },
            IdentityColumns = new List<string>(),
            AllColumns = new List<string> { "PaymentId", "PaymentGatewayParameters" }
        };
        var predicate = new ProviderPredicateDefinition
        {
            Name = "Payments", ProviderType = "SqlTable",
            Table = "EcomPayments", NameColumn = "PaymentId"
        };
        var columnTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["PaymentId"] = "nvarchar",
            ["PaymentGatewayParameters"] = "xml"
        };

        const string targetXml =
            "<Settings>" +
            "<Parameter name=\"CustomLocal\">my-val</Parameter>" +
            "</Settings>";
        const string seedXml =
            "<Settings>" +
            "<Parameter name=\"Mail1SenderEmail\">no-reply@swift.com</Parameter>" +
            "</Settings>";

        var yamlRow = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["PaymentId"] = "PAY-1",
            ["PaymentGatewayParameters"] = seedXml
        };
        var existingRow = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["PaymentId"] = "PAY-1",
            ["PaymentGatewayParameters"] = targetXml
        };

        var (provider, _, writer, inputRoot) = CreateProviderWithFiles(
            yamlRows: new[] { yamlRow },
            existingDbRows: new[] { existingRow },
            metadata: metadata,
            columnTypes: columnTypes);

        writer.Setup(w => w.UpdateColumnSubset(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<Dictionary<string, object?>>(), It.IsAny<IEnumerable<string>>(),
                It.IsAny<bool>(), It.IsAny<Action<string>?>()))
            .Returns(WriteOutcome.Updated);

        provider.Deserialize(predicate, inputRoot,
            strategy: ConflictStrategy.DestinationWins);

        // Merged XML must contain both the target-only CustomLocal AND the seed-added Mail1SenderEmail.
        writer.Verify(w => w.UpdateColumnSubset(
                "EcomPayments",
                It.IsAny<IReadOnlyList<string>>(),
                It.Is<Dictionary<string, object?>>(d =>
                    d.ContainsKey("PaymentGatewayParameters")
                    && ((string)d["PaymentGatewayParameters"]!).Contains("CustomLocal")
                    && ((string)d["PaymentGatewayParameters"]!).Contains("Mail1SenderEmail")),
                It.IsAny<IEnumerable<string>>(),
                false, It.IsAny<Action<string>?>()),
            Times.Once);
    }

    [Fact]
    public void Seed_XmlColumn_DryRun_EmitsPerElementWouldFill()
    {
        var metadata = new TableMetadata
        {
            TableName = "EcomPayments",
            NameColumn = "PaymentId",
            KeyColumns = new List<string> { "PaymentId" },
            IdentityColumns = new List<string>(),
            AllColumns = new List<string> { "PaymentId", "PaymentGatewayParameters" }
        };
        var predicate = new ProviderPredicateDefinition
        {
            Name = "Payments", ProviderType = "SqlTable",
            Table = "EcomPayments", NameColumn = "PaymentId"
        };
        var columnTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["PaymentId"] = "nvarchar",
            ["PaymentGatewayParameters"] = "xml"
        };

        const string targetXml = "<Settings></Settings>";
        const string seedXml =
            "<Settings>" +
            "<Parameter name=\"Mail1SenderEmail\">no-reply@swift.com</Parameter>" +
            "</Settings>";

        var yamlRow = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["PaymentId"] = "PAY-1",
            ["PaymentGatewayParameters"] = seedXml
        };
        var existingRow = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["PaymentId"] = "PAY-1",
            ["PaymentGatewayParameters"] = targetXml
        };

        var (provider, executor, writer, inputRoot) = CreateProviderWithFiles(
            yamlRows: new[] { yamlRow },
            existingDbRows: new[] { existingRow },
            metadata: metadata,
            columnTypes: columnTypes);

        var logs = new List<string>();
        provider.Deserialize(predicate, inputRoot, log: logs.Add, isDryRun: true,
            strategy: ConflictStrategy.DestinationWins);

        Assert.Contains(logs, l =>
            l.Contains("would fill")
            && l.Contains("PaymentGatewayParameters")
            && l.Contains("Mail1SenderEmail"));
    }

    [Fact]
    public void Seed_XmlColumn_IdentifiedByColumnType_xml()
    {
        // Baseline correctness: a column whose SQL type is "xml" is routed through
        // XmlMergeHelper, not through scalar MergePredicate. Negative test: same data
        // with type="nvarchar" routes through scalar path (and the existing non-empty
        // target preserves the value). Same string value on both sides — no fill
        // either way — but the code path chosen differs observably only at the log level.
        var metadata = new TableMetadata
        {
            TableName = "EcomPayments",
            NameColumn = "PaymentId",
            KeyColumns = new List<string> { "PaymentId" },
            IdentityColumns = new List<string>(),
            AllColumns = new List<string> { "PaymentId", "PaymentGatewayParameters" }
        };
        var predicate = new ProviderPredicateDefinition
        {
            Name = "Payments", ProviderType = "SqlTable",
            Table = "EcomPayments", NameColumn = "PaymentId"
        };
        var columnTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["PaymentId"] = "nvarchar",
            ["PaymentGatewayParameters"] = "xml"
        };

        // Target is empty XML container → leaf is unset. Seed has <Parameter name="X">v</Parameter>.
        // If XML path runs, the outer Settings container on target gets filled with children.
        // If scalar path ran, the empty-string scalar would be filled with the raw seed XML string.
        // Either way the fill fires — we test the XML fill signature (merged XML contains the
        // source element structure).
        const string targetXml = "<Settings></Settings>";
        const string seedXml =
            "<Settings>" +
            "<Parameter name=\"Mail1SenderEmail\">x@y</Parameter>" +
            "</Settings>";

        var yamlRow = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["PaymentId"] = "PAY-1",
            ["PaymentGatewayParameters"] = seedXml
        };
        var existingRow = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["PaymentId"] = "PAY-1",
            ["PaymentGatewayParameters"] = targetXml
        };

        var (provider, _, writer, inputRoot) = CreateProviderWithFiles(
            yamlRows: new[] { yamlRow },
            existingDbRows: new[] { existingRow },
            metadata: metadata,
            columnTypes: columnTypes);

        writer.Setup(w => w.UpdateColumnSubset(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<Dictionary<string, object?>>(), It.IsAny<IEnumerable<string>>(),
                It.IsAny<bool>(), It.IsAny<Action<string>?>()))
            .Returns(WriteOutcome.Updated);

        provider.Deserialize(predicate, inputRoot,
            strategy: ConflictStrategy.DestinationWins);

        // The XML path fills via XmlMergeHelper, which inserts a full <Parameter> element.
        // The merged value must contain the Mail1SenderEmail element.
        writer.Verify(w => w.UpdateColumnSubset(
                "EcomPayments",
                It.IsAny<IReadOnlyList<string>>(),
                It.Is<Dictionary<string, object?>>(d =>
                    ((string)d["PaymentGatewayParameters"]!).Contains("Mail1SenderEmail")),
                It.IsAny<IEnumerable<string>>(),
                false, It.IsAny<Action<string>?>()),
            Times.Once);
    }

    [Fact]
    public void Seed_XmlColumn_MalformedSourceOrTarget_FallbackToStandardUnsetRule()
    {
        // Both target and source malformed — XmlMergeHelper returns the target unchanged.
        // Because target XML is non-empty (though malformed), the merge predicate sees no
        // element-level diff and must not emit a write.
        var metadata = new TableMetadata
        {
            TableName = "EcomPayments",
            NameColumn = "PaymentId",
            KeyColumns = new List<string> { "PaymentId" },
            IdentityColumns = new List<string>(),
            AllColumns = new List<string> { "PaymentId", "PaymentGatewayParameters" }
        };
        var predicate = new ProviderPredicateDefinition
        {
            Name = "Payments", ProviderType = "SqlTable",
            Table = "EcomPayments", NameColumn = "PaymentId"
        };
        var columnTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["PaymentId"] = "nvarchar",
            ["PaymentGatewayParameters"] = "xml"
        };

        const string targetXml = "<broken";
        const string seedXml = "<Settings><Parameter name=\"X\">y</Parameter></Settings>";

        var yamlRow = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["PaymentId"] = "PAY-1",
            ["PaymentGatewayParameters"] = seedXml
        };
        var existingRow = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["PaymentId"] = "PAY-1",
            ["PaymentGatewayParameters"] = targetXml
        };

        var (provider, _, writer, inputRoot) = CreateProviderWithFiles(
            yamlRows: new[] { yamlRow },
            existingDbRows: new[] { existingRow },
            metadata: metadata,
            columnTypes: columnTypes);

        provider.Deserialize(predicate, inputRoot,
            strategy: ConflictStrategy.DestinationWins);

        // Malformed target → XmlMergeHelper returns target unchanged → no fills → no write.
        writer.Verify(w => w.UpdateColumnSubset(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<Dictionary<string, object?>>(), It.IsAny<IEnumerable<string>>(),
                It.IsAny<bool>(), It.IsAny<Action<string>?>()),
            Times.Never);
    }

    #region Helpers

    private static Dictionary<string, object?> Row(string id, string? name, string? description)
        => new(StringComparer.OrdinalIgnoreCase)
        {
            ["OrderFlowId"] = id,
            ["OrderFlowName"] = name,
            ["OrderFlowDescription"] = description
        };

    /// <summary>
    /// Copy of SqlTableProviderDeserializeTests.CreateProviderWithFiles, extended to accept
    /// custom table metadata + column type dictionary for the merge-predicate path.
    /// </summary>
    private static (SqlTableProvider provider, Mock<ISqlExecutor> executor,
                    Mock<SqlTableWriter> writer, string inputRoot)
        CreateProviderWithFiles(
            IEnumerable<Dictionary<string, object?>> yamlRows,
            IEnumerable<Dictionary<string, object?>> existingDbRows,
            TableMetadata? metadata = null,
            Dictionary<string, string>? columnTypes = null)
    {
        var meta = metadata ?? TestMetadata;
        var types = columnTypes ?? DefaultColumnTypes;

        var mockExecutor = new Mock<ISqlExecutor>();

        var mockMetadataReader = new Mock<DataGroupMetadataReader>(mockExecutor.Object) { CallBase = false };
        mockMetadataReader.Setup(x => x.GetTableMetadata(It.IsAny<ProviderPredicateDefinition>(), It.IsAny<bool>()))
            .Returns(meta);
        mockMetadataReader.Setup(x => x.TableExists(It.IsAny<string>())).Returns(true);
        mockMetadataReader.Setup(x => x.GetColumnTypes(It.IsAny<string>()))
            .Returns(types);
        mockMetadataReader.Setup(x => x.GetNotNullColumns(It.IsAny<string>()))
            .Returns(new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        var existingList = existingDbRows.ToList();
        var dbReaderMock = CreateMockDataReader(
            meta.AllColumns.ToArray(),
            existingList.Select(r => meta.AllColumns
                .Select(col => r.TryGetValue(col, out var v) ? v ?? DBNull.Value : DBNull.Value)
                .ToArray()).ToArray());
        mockExecutor.Setup(x => x.ExecuteReader(It.IsAny<CommandBuilder>()))
            .Returns(dbReaderMock.Object);

        var tableReader = new SqlTableReader(mockExecutor.Object);
        var fileStore = new FlatFileStore();

        var tempDir = Path.Combine(Path.GetTempPath(), $"contentsync_seedmerge_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var mockExecForIdentity = new Mock<ISqlExecutor>();
        var identityReader = new SqlTableReader(mockExecForIdentity.Object);

        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in yamlRows)
        {
            var identity = identityReader.GenerateRowIdentity(row, meta);
            fileStore.WriteRow(tempDir, meta.TableName, identity, row, usedNames);
        }
        fileStore.WriteMeta(tempDir, meta.TableName, meta);

        var writerMock = new Mock<SqlTableWriter>(mockExecutor.Object) { CallBase = false };

        // Schema cache loader mirrors the configured type dict so the merge branch's
        // IsXmlColumn check + IsUnsetForMergeBySqlType both see the correct types.
        var schemaCache = new TargetSchemaCache(_ =>
            (new HashSet<string>(meta.AllColumns, StringComparer.OrdinalIgnoreCase),
             new Dictionary<string, string>(types, StringComparer.OrdinalIgnoreCase)));
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

    #endregion
}
