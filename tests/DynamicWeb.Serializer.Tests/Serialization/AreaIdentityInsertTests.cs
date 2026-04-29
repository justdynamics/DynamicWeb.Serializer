using System.Text.RegularExpressions;
using DynamicWeb.Serializer.Configuration;
using DynamicWeb.Serializer.Infrastructure;
using DynamicWeb.Serializer.Models;
using DynamicWeb.Serializer.Providers.SqlTable;
using DynamicWeb.Serializer.Serialization;
using Dynamicweb.Data;
using Moq;
using Xunit;

namespace DynamicWeb.Serializer.Tests.Serialization;

/// <summary>
/// Phase 38 A.2 (D-38-05): regression guard for commit 5333e88, which wrapped
/// Area INSERTs in SET IDENTITY_INSERT [Area] ON/OFF so that explicit AreaID writes
/// succeed on a fresh target where Area.AreaId is an identity column.
///
/// Threat anchor: T-38-03 (FK integrity — removing the wrapping regenerates AreaIds
/// on target, breaking every predicate.areaId reference).
///
/// Per checker warning W3: the assertion validates the ORDERED sequence
/// SET IDENTITY_INSERT ON -> INSERT -> SET IDENTITY_INSERT OFF, not just substring
/// presence. Failure modes caught: wrapping removed entirely, OFF emitted BEFORE
/// INSERT (race), INSERT emitted without wrapping, split-across-CommandBuilders
/// emission in wrong order.
///
/// Uses an in-memory TargetSchemaCache with a fixture loader so no live DW DB is
/// touched — the test hook drives the private CreateAreaFromProperties method via
/// the internal InvokeCreateAreaFromPropertiesForTest forwarder (InternalsVisibleTo
/// on DynamicWeb.Serializer.csproj line 34).
/// </summary>
[Trait("Category", "Phase38")]
public class AreaIdentityInsertTests
{
    private static TargetSchemaCache MakeAreaSchemaCache()
    {
        // Fixture loader: "Area" table has the columns the test uses. Unknown columns
        // are silently dropped by the deserializer's schema-drift tolerance.
        return new TargetSchemaCache(tableName =>
        {
            var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "AreaID", "AreaName", "AreaSort", "AreaUniqueId"
            };
            var types = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["AreaID"] = "int",
                ["AreaName"] = "nvarchar",
                ["AreaSort"] = "int",
                ["AreaUniqueId"] = "uniqueidentifier"
            };
            return (cols, types);
        });
    }

    // Phase 40 D-02 / D-04: flat shape — DeployOutputSubfolder / SeedOutputSubfolder are
    // top-level scalars; Predicates is empty for this test (it drives the area-create path
    // through the InvokeCreateAreaFromPropertiesForTest test hook, which does not iterate
    // predicates). Mode-defaulting is irrelevant here.
    private static SerializerConfiguration MakeMinimalConfig() => new()
    {
        OutputDirectory = "X",
        DeployOutputSubfolder = "deploy",
        SeedOutputSubfolder = "seed",
        Predicates = new List<ProviderPredicateDefinition>()
    };

    private static SerializedArea MakeSerializedArea() => new()
    {
        AreaId = Guid.NewGuid(),
        Name = "TestArea",
        SortOrder = 1
    };

    [Fact]
    public void CreateAreaFromProperties_WrapsInsertInOrderedIdentityInsert()
    {
        var capturedCommands = new List<string>();
        var executor = new Mock<ISqlExecutor>();
        executor.Setup(e => e.ExecuteNonQuery(It.IsAny<CommandBuilder>()))
                .Callback<CommandBuilder>(cb => capturedCommands.Add(cb.ToString()))
                .Returns(1);

        var deserializer = new ContentDeserializer(
            configuration: MakeMinimalConfig(),
            store: null,
            log: null,
            isDryRun: false,
            filesRoot: null,
            conflictStrategy: ConflictStrategy.SourceWins,
            schemaCache: MakeAreaSchemaCache(),
            sqlExecutor: executor.Object);

        var area = MakeSerializedArea();

        // Drive the Area-create path through the internal test hook
        // (W2: deterministic, single approach — no reflection, no open-endedness).
        deserializer.InvokeCreateAreaFromPropertiesForTest(
            areaId: 42, area: area, excludeFields: null);

        // Concatenate captured command text so the ordered regex can match across
        // both a single multi-statement CommandBuilder and a split-across-CommandBuilders
        // emission. Emission order is preserved by List<string> append order.
        var combined = string.Join("\n", capturedCommands);

        // W3: validate ORDERED sequence, not just substring presence.
        // SET IDENTITY_INSERT [Area] ON  ...  INSERT INTO ... [Area] ...  SET IDENTITY_INSERT [Area] OFF
        var orderedPattern = new Regex(
            @"SET\s+IDENTITY_INSERT\s+\[Area\]\s+ON.*?INSERT\s+INTO\s+\[?Area\]?.*?SET\s+IDENTITY_INSERT\s+\[Area\]\s+OFF",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        Assert.True(orderedPattern.IsMatch(combined),
            "Area-create must emit SET IDENTITY_INSERT ON -> INSERT -> SET IDENTITY_INSERT OFF in order. " +
            "Captured text:\n" + combined);

        // Sanity check: the executor was actually invoked.
        Assert.NotEmpty(capturedCommands);
    }

    [Fact]
    public void UpdateAreaFromProperties_DoesNotUseIdentityInsert()
    {
        // Guard against accidental over-application of the fix — the UPDATE path
        // (existing area) does NOT need IDENTITY_INSERT wrapping because the PK
        // is unchanged.
        var capturedCommands = new List<string>();
        var executor = new Mock<ISqlExecutor>();
        executor.Setup(e => e.ExecuteNonQuery(It.IsAny<CommandBuilder>()))
                .Callback<CommandBuilder>(cb => capturedCommands.Add(cb.ToString()))
                .Returns(1);

        var deserializer = new ContentDeserializer(
            configuration: MakeMinimalConfig(),
            store: null, log: null, isDryRun: false, filesRoot: null,
            conflictStrategy: ConflictStrategy.SourceWins,
            schemaCache: MakeAreaSchemaCache(),
            sqlExecutor: executor.Object);

        // WriteAreaProperties takes a Dictionary of properties to UPDATE.
        var properties = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["AreaName"] = "UpdatedName"
        };

        deserializer.InvokeUpdateAreaFromPropertiesForTest(
            areaId: 7,
            properties: properties,
            excludeFields: null,
            excludeAreaColumns: null);

        var combined = string.Join("\n", capturedCommands);
        Assert.DoesNotContain("IDENTITY_INSERT", combined, StringComparison.OrdinalIgnoreCase);
        // Sanity: UPDATE statement was emitted.
        Assert.Contains("UPDATE", combined, StringComparison.OrdinalIgnoreCase);
    }
}
