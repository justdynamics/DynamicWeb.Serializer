using System.Text.Json;
using DynamicWeb.Serializer.Infrastructure;
using Xunit;

namespace DynamicWeb.Serializer.Tests.Infrastructure;

/// <summary>
/// Tests for ManifestWriter — emits {mode}-manifest.json listing every YAML file
/// written by a serialize run (Phase 37-01 Task 2, D-10/D-11).
/// </summary>
public class ManifestWriterTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ManifestWriter _writer;

    public ManifestWriterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ManifestWriterTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _writer = new ManifestWriter();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void Write_ProducesJsonListingEveryFile()
    {
        var files = new[]
        {
            Path.Combine(_tempDir, "a.yml"),
            Path.Combine(_tempDir, "nested", "b.yml")
        };
        foreach (var f in files) { Directory.CreateDirectory(Path.GetDirectoryName(f)!); File.WriteAllText(f, "x"); }

        _writer.Write(_tempDir, "deploy", files);

        var manifestPath = Path.Combine(_tempDir, "deploy-manifest.json");
        Assert.True(File.Exists(manifestPath));
        using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var root = doc.RootElement;
        Assert.Equal("deploy", root.GetProperty("mode").GetString());
        var fileList = root.GetProperty("files").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains("a.yml", fileList);
        Assert.Contains("nested/b.yml", fileList);
    }

    [Fact]
    public void Read_RoundTripsManifest()
    {
        var files = new[]
        {
            Path.Combine(_tempDir, "x.yml"),
            Path.Combine(_tempDir, "y.yml")
        };
        foreach (var f in files) File.WriteAllText(f, "x");

        _writer.Write(_tempDir, "seed", files);

        var manifest = _writer.Read(_tempDir, "seed");

        Assert.NotNull(manifest);
        Assert.Equal("seed", manifest!.Mode);
        Assert.Equal(2, manifest.Files.Count);
        Assert.Contains("x.yml", manifest.Files);
        Assert.Contains("y.yml", manifest.Files);
    }

    [Fact]
    public void Write_FilesSortedAlphabetically()
    {
        var files = new[]
        {
            Path.Combine(_tempDir, "z.yml"),
            Path.Combine(_tempDir, "a.yml"),
            Path.Combine(_tempDir, "m.yml")
        };
        foreach (var f in files) File.WriteAllText(f, "x");

        _writer.Write(_tempDir, "deploy", files);

        var manifest = _writer.Read(_tempDir, "deploy");
        Assert.NotNull(manifest);
        Assert.Equal(new[] { "a.yml", "m.yml", "z.yml" }, manifest!.Files);
    }

    [Fact]
    public void Write_UsesForwardSlashesForCrossPlatform()
    {
        var nestedFile = Path.Combine(_tempDir, "a", "b", "c.yml");
        Directory.CreateDirectory(Path.GetDirectoryName(nestedFile)!);
        File.WriteAllText(nestedFile, "x");

        _writer.Write(_tempDir, "deploy", new[] { nestedFile });

        var json = File.ReadAllText(Path.Combine(_tempDir, "deploy-manifest.json"));
        Assert.Contains("a/b/c.yml", json);
        Assert.DoesNotContain("a\\\\b\\\\c.yml", json);
    }
}
