// IMPORTANT: These integration tests require the DW runtime to be initialized.
// They CANNOT be run from a developer workstation directly via 'dotnet test'.
// Execution requires:
//   1. Build: dotnet build src/Dynamicweb.ContentSync/ -c Debug
//   2. Copy DLLs to Swift2.2 bin:
//        copy src\Dynamicweb.ContentSync\bin\Debug\net8.0\Dynamicweb.ContentSync.dll
//             C:\Projects\Solutions\swift.test.forsync\Swift2.2\Dynamicweb.Host.Suite\bin\Debug\net10.0\
//        copy src\Dynamicweb.ContentSync\bin\Debug\net8.0\YamlDotNet.dll
//             C:\Projects\Solutions\swift.test.forsync\Swift2.2\Dynamicweb.Host.Suite\bin\Debug\net10.0\
//   3. Start Swift2.2: cd C:\Projects\Solutions\swift.test.forsync\Swift2.2\Dynamicweb.Host.Suite && dotnet run
//   4. Run: dotnet test tests/Dynamicweb.ContentSync.IntegrationTests/ --filter "Category=Integration"
//
// Services.Pages, Services.Grids, etc. require DW host initialization (SQL connection, context) to function.
// Without a running DW instance, all these tests will fail with a NullReferenceException or connection error.

using System.Text.Json;
using Dynamicweb.Content;
using Dynamicweb.ContentSync.Configuration;
using Dynamicweb.ContentSync.ScheduledTasks;
using Dynamicweb.ContentSync.Serialization;
using Xunit;

namespace Dynamicweb.ContentSync.IntegrationTests.ScheduledTasks;

[Trait("Category", "Integration")]
[Collection("ScheduledTaskTests")]  // sequential — prevents log file contention
public class ScheduledTaskEndToEndTests : IDisposable
{
    private readonly string _taskOutputDir;
    private readonly string _directOutputDir;
    private readonly string _configPath;

    public ScheduledTaskEndToEndTests()
    {
        var baseName = "ContentSyncE2E_" + Guid.NewGuid().ToString("N")[..8];
        _taskOutputDir = Path.Combine(Path.GetTempPath(), baseName + "_task");
        _directOutputDir = Path.Combine(Path.GetTempPath(), baseName + "_direct");
        Directory.CreateDirectory(_taskOutputDir);
        Directory.CreateDirectory(_directOutputDir);
        _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ContentSync.config.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_taskOutputDir)) Directory.Delete(_taskOutputDir, recursive: true);
        if (Directory.Exists(_directOutputDir)) Directory.Delete(_directOutputDir, recursive: true);
        if (File.Exists(_configPath)) File.Delete(_configPath);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private void WriteConfig(string outputDir, int areaId, string pagePath)
    {
        var config = new
        {
            outputDirectory = outputDir,
            predicates = new[]
            {
                new { name = "CustomerCenter", areaId = areaId, path = pagePath, excludes = Array.Empty<string>() }
            }
        };
        File.WriteAllText(_configPath, JsonSerializer.Serialize(config));
    }

    private static void AssertDirectoryTreesEqual(string dirA, string dirB)
    {
        var filesA = Directory.EnumerateFiles(dirA, "*.yml", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(dirA, f)).OrderBy(f => f).ToList();
        var filesB = Directory.EnumerateFiles(dirB, "*.yml", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(dirB, f)).OrderBy(f => f).ToList();

        Assert.Equal(filesA, filesB);

        foreach (var rel in filesA)
        {
            var bytesA = File.ReadAllBytes(Path.Combine(dirA, rel));
            var bytesB = File.ReadAllBytes(Path.Combine(dirB, rel));
            Assert.True(bytesA.SequenceEqual(bytesB), $"File differs: {rel}");
        }
    }

    // -------------------------------------------------------------------------
    // Tests
    // -------------------------------------------------------------------------

    /// <summary>
    /// OPS-01: SerializeScheduledTask.Run() produces byte-identical YAML output to calling ContentSerializer directly.
    /// Verifies that the scheduled task entry point correctly discovers ContentSync.config.json (at BaseDirectory),
    /// loads it, and delegates to ContentSerializer — producing the same file tree.
    /// </summary>
    [Fact]
    public void SerializeScheduledTask_Run_ProducesSameOutputAsContentSerializer()
    {
        // Arrange: discover areaId from known page
        var page = Services.Pages.GetPage(8385);
        Assert.NotNull(page);
        var areaId = page.AreaId;
        var pagePath = "/" + page.MenuText;

        // 1. Run ContentSerializer directly
        var directConfig = new SyncConfiguration
        {
            OutputDirectory = _directOutputDir,
            Predicates = new List<PredicateDefinition>
            {
                new PredicateDefinition { Name = "CustomerCenter", AreaId = areaId, Path = pagePath, Excludes = new List<string>() }
            }
        };
        var serializer = new ContentSerializer(directConfig);
        serializer.Serialize();

        // 2. Write config for scheduled task pointing to task output dir
        WriteConfig(_taskOutputDir, areaId, pagePath);

        // 3. Run SerializeScheduledTask
        var task = new SerializeScheduledTask();
        var result = task.Run();

        // Assert
        Assert.True(result, "SerializeScheduledTask.Run() returned false");

        var taskFiles = Directory.EnumerateFiles(_taskOutputDir, "*.yml", SearchOption.AllDirectories).ToList();
        Assert.NotEmpty(taskFiles);

        AssertDirectoryTreesEqual(_directOutputDir, _taskOutputDir);
    }

    /// <summary>
    /// OPS-02: DeserializeScheduledTask.Run() completes successfully (returns true) after serializing
    /// the Customer Center tree to YAML. Verifies the scheduled task entry point discovers the config,
    /// and delegates to ContentDeserializer without errors.
    /// </summary>
    [Fact]
    public void DeserializeScheduledTask_Run_CompletesWithoutErrors()
    {
        // Arrange: serialize first to create YAML files
        var page = Services.Pages.GetPage(8385);
        Assert.NotNull(page);
        var areaId = page.AreaId;
        var pagePath = "/" + page.MenuText;

        // Serialize to create input files
        var config = new SyncConfiguration
        {
            OutputDirectory = _taskOutputDir,
            Predicates = new List<PredicateDefinition>
            {
                new PredicateDefinition { Name = "CustomerCenter", AreaId = areaId, Path = pagePath, Excludes = new List<string>() }
            }
        };
        var serializer = new ContentSerializer(config);
        serializer.Serialize();

        // Write config for scheduled task (same output dir — deserialize reads from it)
        WriteConfig(_taskOutputDir, areaId, pagePath);

        // Act: run DeserializeScheduledTask
        var task = new DeserializeScheduledTask();
        var result = task.Run();

        // Assert
        Assert.True(result, "DeserializeScheduledTask.Run() returned false — check ContentSync.log for errors");
    }
}
