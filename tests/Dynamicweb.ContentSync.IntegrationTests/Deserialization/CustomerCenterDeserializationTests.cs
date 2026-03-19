// IMPORTANT: These integration tests require the DW runtime to be initialized.
// They CANNOT be run from a developer workstation directly via 'dotnet test'.
// Execution requires:
//   1. Build: dotnet build src/Dynamicweb.ContentSync/ -c Debug
//   2. Copy DLLs to Swift2.1 bin:
//        copy src\Dynamicweb.ContentSync\bin\Debug\net8.0\Dynamicweb.ContentSync.dll
//             C:\Projects\Solutions\swift.test.forsync\Swift2.1\Dynamicweb.Host.Suite\bin\Debug\net8.0\
//        copy src\Dynamicweb.ContentSync\bin\Debug\net8.0\YamlDotNet.dll
//             C:\Projects\Solutions\swift.test.forsync\Swift2.1\Dynamicweb.Host.Suite\bin\Debug\net8.0\
//   3. Start Swift2.1: cd C:\Projects\Solutions\swift.test.forsync\Swift2.1\Dynamicweb.Host.Suite && dotnet run
//   4. Run: dotnet test tests/Dynamicweb.ContentSync.IntegrationTests/ --filter "Category=Integration"

using Dynamicweb.Content;
using Dynamicweb.ContentSync.Configuration;
using Dynamicweb.ContentSync.Serialization;
using Xunit;

namespace Dynamicweb.ContentSync.IntegrationTests.Deserialization;

[Trait("Category", "Integration")]
public class CustomerCenterDeserializationTests : IDisposable
{
    private readonly string _outputDir;

    public CustomerCenterDeserializationTests()
    {
        _outputDir = Path.Combine(Path.GetTempPath(),
            "ContentSyncDeserTest_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_outputDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_outputDir))
            Directory.Delete(_outputDir, recursive: true);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static SyncConfiguration BuildConfig(string outputDir, int areaId, string pagePath)
    {
        return new SyncConfiguration
        {
            OutputDirectory = outputDir,
            Predicates = new List<PredicateDefinition>
            {
                new PredicateDefinition
                {
                    Name = "CustomerCenter",
                    AreaId = areaId,
                    Path = pagePath,
                    Excludes = new List<string>()
                }
            }
        };
    }

    private (SyncConfiguration config, int areaId) SerializeCustomerCenter()
    {
        var page = Services.Pages.GetPage(8385);
        Assert.NotNull(page);
        var areaId = page.AreaId;
        var config = BuildConfig(_outputDir, areaId, "/" + page.MenuText);
        var serializer = new ContentSerializer(config);
        serializer.Serialize();
        return (config, areaId);
    }

    // -------------------------------------------------------------------------
    // Tests
    // -------------------------------------------------------------------------

    /// <summary>
    /// DES-01: Deserialize roundtrip creates expected structure.
    /// Serializes the Customer Center tree to YAML, then deserializes back
    /// into the same DW instance. Since the same GUIDs already exist, items
    /// should be updated (not created from scratch).
    /// </summary>
    [Fact]
    public void Deserialize_CustomerCenter_CompletesWithoutErrors()
    {
        // Arrange: serialize first to produce YAML files
        var (config, areaId) = SerializeCustomerCenter();

        // Act: deserialize back into the same DW instance (UPDATE path — same GUIDs exist)
        var deserializer = new ContentDeserializer(config, log: msg => { });
        var result = deserializer.Deserialize();

        // Assert: no errors, some items updated (same GUIDs already exist)
        Assert.False(result.HasErrors, $"Deserialization had errors: {string.Join("; ", result.Errors)}");
        Assert.True(result.Updated > 0 || result.Created > 0,
            $"Expected some items to be created or updated. Result: {result.Summary}");
    }

    /// <summary>
    /// DES-02: GUID identity resolution works — items are matched and updated in place.
    /// Running deserialization twice should be idempotent: second run has zero failures,
    /// zero creates (GUID already exists), and at least some updates.
    /// </summary>
    [Fact]
    public void Deserialize_CustomerCenter_GuidIdentity_UpdatesInPlace()
    {
        // Arrange
        var (config, areaId) = SerializeCustomerCenter();

        // Act: deserialize twice — second run should still succeed (idempotent)
        var deserializer1 = new ContentDeserializer(config, log: msg => { });
        var result1 = deserializer1.Deserialize();

        var deserializer2 = new ContentDeserializer(config, log: msg => { });
        var result2 = deserializer2.Deserialize();

        // Assert: second run has zero failures, items matched by GUID
        Assert.False(result2.HasErrors, $"Second deserialization had errors: {string.Join("; ", result2.Errors)}");
        // Second run should have updates (same items matched by GUID), zero creates
        Assert.Equal(0, result2.Created);
        Assert.True(result2.Updated > 0, "Expected updates on second run (GUID match)");
    }

    /// <summary>
    /// DES-04: Dry-run reports changes without writing to DW.
    /// Verifies that dry-run produces results, logs [DRY-RUN] messages, and has no errors.
    /// </summary>
    [Fact]
    public void Deserialize_DryRun_ReportsChangesWithoutWriting()
    {
        // Arrange
        var (config, areaId) = SerializeCustomerCenter();
        var logMessages = new List<string>();

        // Act: dry-run deserialization
        var deserializer = new ContentDeserializer(config, log: msg => logMessages.Add(msg), isDryRun: true);
        var result = deserializer.Deserialize();

        // Assert: dry-run produces results but writes nothing
        Assert.True(result.Created + result.Updated + result.Skipped > 0,
            $"Dry-run reported zero items. Result: {result.Summary}");

        // Assert: log messages contain [DRY-RUN] prefix
        Assert.Contains(logMessages, m => m.Contains("[DRY-RUN]"));

        // Assert: no actual errors (dry-run doesn't write, so no write failures)
        Assert.False(result.HasErrors, $"Dry-run had errors: {string.Join("; ", result.Errors)}");
    }

    /// <summary>
    /// DES-02: Validates that setting page.UniqueId before SavePage() preserves the GUID in DW's database.
    /// This is the critical assumption underlying the entire identity resolution strategy.
    /// If DW overwrites UniqueId on insert, this test will fail and the strategy must be reconsidered.
    /// </summary>
    [Fact]
    public void Verify_PageUniqueId_PreservedOnInsert()
    {
        // Arrange: create a page with a known GUID
        var knownGuid = Guid.NewGuid();
        var page = Services.Pages.GetPage(8385);
        Assert.NotNull(page);

        var testPage = new Page();
        testPage.UniqueId = knownGuid;
        testPage.AreaId = page.AreaId;
        testPage.ParentPageId = page.ID;
        testPage.MenuText = "ContentSync_GuidTest_" + knownGuid.ToString("N")[..8];
        testPage.Active = false; // Inactive so it doesn't appear in navigation

        try
        {
            // Act
            var saved = Services.Pages.SavePage(testPage);

            // Assert: re-fetch and verify GUID is preserved
            var refetched = Services.Pages.GetPage(saved.ID);
            Assert.NotNull(refetched);
            Assert.Equal(knownGuid, refetched.UniqueId);
        }
        finally
        {
            // Cleanup: delete the test page
            try
            {
                var cleanup = Services.Pages.GetPagesByParentID(page.ID)
                    .FirstOrDefault(p => p.MenuText?.StartsWith("ContentSync_GuidTest_") == true);
                if (cleanup != null)
                    Services.Pages.DeletePage(cleanup.ID);
            }
            catch { /* best-effort cleanup */ }
        }
    }
}
