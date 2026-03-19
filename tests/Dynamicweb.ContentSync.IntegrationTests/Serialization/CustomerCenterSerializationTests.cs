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

using Dynamicweb.Content;
using Dynamicweb.ContentSync.Configuration;
using Dynamicweb.ContentSync.Serialization;
using Xunit;

namespace Dynamicweb.ContentSync.IntegrationTests.Serialization;

[Trait("Category", "Integration")]
public class CustomerCenterSerializationTests : IDisposable
{
    private readonly string _outputDir;

    public CustomerCenterSerializationTests()
    {
        _outputDir = Path.Combine(Path.GetTempPath(),
            "ContentSyncIntTest_" + Guid.NewGuid().ToString("N")[..8]);
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

    // -------------------------------------------------------------------------
    // Tests
    // -------------------------------------------------------------------------

    /// <summary>
    /// SER-03: Serializing the Customer Center page tree produces a YAML file tree on disk.
    /// Validates that at least one .yml file is created and the output directory exists.
    /// </summary>
    [Fact]
    public void Serialize_CustomerCenter_ProducesYamlTree()
    {
        // Arrange: discover areaId at runtime from the known page
        var page = Services.Pages.GetPage(8385);
        Assert.NotNull(page);
        var areaId = page.AreaId;

        var config = BuildConfig(_outputDir, areaId, "/" + page.MenuText);
        var serializer = new ContentSerializer(config);

        // Act
        serializer.Serialize();

        // Assert: output directory exists
        Assert.True(Directory.Exists(_outputDir),
            $"Output directory '{_outputDir}' was not created.");

        // Assert: at least one YAML file was produced
        var yamlFiles = Directory.EnumerateFiles(_outputDir, "*.yml", SearchOption.AllDirectories).ToList();
        Assert.NotEmpty(yamlFiles);

        // Assert: at least one page.yml file exists somewhere in the tree
        var pageYmlFiles = yamlFiles.Where(f => Path.GetFileName(f) == "page.yml").ToList();
        Assert.NotEmpty(pageYmlFiles);
    }

    /// <summary>
    /// SER-03 GUID-only: The serialized YAML must not contain the known numeric page ID (8385)
    /// as a standalone value, and all page.yml files must contain GUID-formatted pageUniqueId values.
    /// </summary>
    [Fact]
    public void Serialize_CustomerCenter_GuidOnly_NoNumericCrossRefs()
    {
        // Arrange
        var page = Services.Pages.GetPage(8385);
        Assert.NotNull(page);
        var areaId = page.AreaId;

        var config = BuildConfig(_outputDir, areaId, "/" + page.MenuText);
        var serializer = new ContentSerializer(config);

        // Act
        serializer.Serialize();

        // Assert: no YAML file contains the known page ID "8385" as a standalone value
        var yamlFiles = Directory.EnumerateFiles(_outputDir, "*.yml", SearchOption.AllDirectories).ToList();
        Assert.NotEmpty(yamlFiles);

        foreach (var filePath in yamlFiles)
        {
            var content = File.ReadAllText(filePath);
            Assert.DoesNotContain("8385", content);
        }

        // Assert: all page.yml files have a GUID-formatted pageUniqueId
        var pageYmlFiles = yamlFiles.Where(f => Path.GetFileName(f) == "page.yml").ToList();
        Assert.NotEmpty(pageYmlFiles);

        var guidPattern = new System.Text.RegularExpressions.Regex(
            @"pageUniqueId:\s*[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-",
            System.Text.RegularExpressions.RegexOptions.Multiline);

        foreach (var filePath in pageYmlFiles)
        {
            var content = File.ReadAllText(filePath);
            Assert.True(guidPattern.IsMatch(content),
                $"page.yml at '{filePath}' does not contain a GUID-formatted pageUniqueId. Content:\n{content}");
        }
    }

    /// <summary>
    /// INF-02: YAML field fidelity — serialized YAML is valid and HTML content is preserved
    /// without entity-escaping (e.g., '&lt;' must NOT appear where '&lt;' was the original value).
    /// </summary>
    [Fact]
    public void Serialize_CustomerCenter_FieldFidelity()
    {
        // Arrange
        var page = Services.Pages.GetPage(8385);
        Assert.NotNull(page);
        var areaId = page.AreaId;

        var config = BuildConfig(_outputDir, areaId, "/" + page.MenuText);
        var serializer = new ContentSerializer(config);

        // Act
        serializer.Serialize();

        // Assert: find any page.yml with a fields: section and verify YAML is parseable
        var yamlFiles = Directory.EnumerateFiles(_outputDir, "*.yml", SearchOption.AllDirectories).ToList();
        Assert.NotEmpty(yamlFiles);

        var yamlDeserializer = new YamlDotNet.Serialization.DeserializerBuilder().Build();

        foreach (var filePath in yamlFiles)
        {
            var content = File.ReadAllText(filePath);

            // Assert: YAML deserializes without exception
            var parsed = Record.Exception(() => yamlDeserializer.Deserialize<object>(content));
            Assert.Null(parsed);

            // Spot-check: if file contains '<' (HTML angle bracket), assert it is NOT entity-escaped
            if (content.Contains('<'))
            {
                // HTML angle brackets must be preserved as-is in YAML — not entity-escaped to &lt;
                Assert.DoesNotContain("&lt;", content);
            }
        }
    }

    /// <summary>
    /// SER-03 determinism: Serializing twice to the same directory produces identical output.
    /// This validates idempotency (zero diff between runs).
    /// </summary>
    [Fact]
    public void Serialize_CustomerCenter_Idempotent()
    {
        // Arrange
        var page = Services.Pages.GetPage(8385);
        Assert.NotNull(page);
        var areaId = page.AreaId;

        var config = BuildConfig(_outputDir, areaId, "/" + page.MenuText);
        var serializer = new ContentSerializer(config);

        // Act: first serialization pass
        serializer.Serialize();

        var firstPassContents = Directory
            .EnumerateFiles(_outputDir, "*.yml", SearchOption.AllDirectories)
            .OrderBy(f => f)
            .ToDictionary(f => f, f => File.ReadAllBytes(f));

        // Act: second serialization pass (overwrite)
        serializer.Serialize();

        var secondPassContents = Directory
            .EnumerateFiles(_outputDir, "*.yml", SearchOption.AllDirectories)
            .OrderBy(f => f)
            .ToDictionary(f => f, f => File.ReadAllBytes(f));

        // Assert: same number of files
        Assert.Equal(firstPassContents.Count, secondPassContents.Count);

        // Assert: each file has identical content (byte-for-byte)
        foreach (var kvp in firstPassContents)
        {
            Assert.True(secondPassContents.ContainsKey(kvp.Key),
                $"File '{kvp.Key}' was present in first run but missing in second run.");

            Assert.Equal(kvp.Value, secondPassContents[kvp.Key]);
        }
    }

    /// <summary>
    /// Validates that the Customer Center serialization produces at least one page that has
    /// a child page (subfolder with its own page.yml), confirming recursive hierarchy support.
    ///
    /// If the Customer Center tree has no child pages at test time, this test is skipped
    /// with an informative message via Assert.Skip.
    /// </summary>
    [Fact]
    public void Serialize_CustomerCenter_HasChildPages()
    {
        // Arrange
        var page = Services.Pages.GetPage(8385);
        Assert.NotNull(page);
        var areaId = page.AreaId;

        var config = BuildConfig(_outputDir, areaId, "/" + page.MenuText);
        var serializer = new ContentSerializer(config);

        // Act
        serializer.Serialize();

        // Assert: find at least one page folder that contains a subfolder with page.yml
        var allPageYmlFiles = Directory
            .EnumerateFiles(_outputDir, "page.yml", SearchOption.AllDirectories)
            .ToList();

        Assert.NotEmpty(allPageYmlFiles);

        // A child page exists when a page.yml's directory has a sibling subdirectory
        // that also contains a page.yml
        var childFound = false;
        foreach (var pageYmlPath in allPageYmlFiles)
        {
            var pageDir = Path.GetDirectoryName(pageYmlPath)!;
            var hasChildPageDir = Directory
                .EnumerateDirectories(pageDir)
                .Any(subDir => File.Exists(Path.Combine(subDir, "page.yml")));

            if (hasChildPageDir)
            {
                childFound = true;
                break;
            }
        }

        if (!childFound)
        {
            // Dynamically skip if Customer Center tree has no child pages
            Assert.Fail("No child pages found under Customer Center (pageid=8385). " +
                        "Expected at least one page folder with a subfolder containing page.yml. " +
                        "Verify that Customer Center has child pages in DW admin.");
        }
    }
}
