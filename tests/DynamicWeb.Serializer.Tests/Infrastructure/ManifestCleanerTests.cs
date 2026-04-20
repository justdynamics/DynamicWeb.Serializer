using DynamicWeb.Serializer.Infrastructure;
using Xunit;

namespace DynamicWeb.Serializer.Tests.Infrastructure;

/// <summary>
/// Tests for ManifestCleaner — deletes files under a per-mode subfolder that were
/// not listed in the current run's manifest (Phase 37-01 Task 2, D-10/D-11/D-12).
/// </summary>
public class ManifestCleanerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ManifestCleaner _cleaner;

    public ManifestCleanerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ManifestCleanerTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _cleaner = new ManifestCleaner();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string CreateFile(string relative, string content = "x")
    {
        var full = Path.Combine(_tempDir, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
        return full;
    }

    [Fact]
    public void CleanStale_DeletesFilesNotInManifest()
    {
        var kept = CreateFile("kept.yml");
        var stale = CreateFile("stale.yml");

        var deleted = _cleaner.CleanStale(_tempDir, "deploy", new[] { kept });

        Assert.Equal(1, deleted);
        Assert.True(File.Exists(kept));
        Assert.False(File.Exists(stale));
    }

    [Fact]
    public void CleanStale_PreservesFilesInManifest()
    {
        var kept1 = CreateFile("a/one.yml");
        var kept2 = CreateFile("b/two.yml");

        var deleted = _cleaner.CleanStale(_tempDir, "deploy", new[] { kept1, kept2 });

        Assert.Equal(0, deleted);
        Assert.True(File.Exists(kept1));
        Assert.True(File.Exists(kept2));
    }

    [Fact]
    public void CleanStale_PreservesManifestJsonItself()
    {
        var kept = CreateFile("kept.yml");
        var manifestPath = Path.Combine(_tempDir, "deploy-manifest.json");
        File.WriteAllText(manifestPath, "{\"mode\":\"deploy\"}");

        var deleted = _cleaner.CleanStale(_tempDir, "deploy", new[] { kept });

        Assert.Equal(0, deleted);
        Assert.True(File.Exists(manifestPath));
    }

    [Fact]
    public void CleanStale_RemovesEmptyDirectoriesAfterFileDeletion()
    {
        var kept = CreateFile("kept.yml");
        var stale = CreateFile("nested/old/stale.yml");

        _cleaner.CleanStale(_tempDir, "deploy", new[] { kept });

        Assert.False(File.Exists(stale));
        Assert.False(Directory.Exists(Path.Combine(_tempDir, "nested", "old")));
        Assert.False(Directory.Exists(Path.Combine(_tempDir, "nested")));
    }

    [Fact]
    public void CleanStale_NonexistentRoot_ReturnsZero()
    {
        var missing = Path.Combine(_tempDir, "missing");

        var deleted = _cleaner.CleanStale(missing, "deploy", Array.Empty<string>());

        Assert.Equal(0, deleted);
    }

    [Fact]
    public void CleanStale_DeletesNonYamlFilesInModeRoot()
    {
        // D-12: serializer owns the folder. Stray files are stale by definition.
        var kept = CreateFile("kept.yml");
        var strayReadme = CreateFile("README.md", "stray");
        var strayGitkeep = CreateFile(".gitkeep", "stray");

        var deleted = _cleaner.CleanStale(_tempDir, "deploy", new[] { kept });

        Assert.Equal(2, deleted);
        Assert.True(File.Exists(kept));
        Assert.False(File.Exists(strayReadme));
        Assert.False(File.Exists(strayGitkeep));
    }

    [Fact]
    public void CleanStale_SymlinkEscapeAttempt_Rejected()
    {
        // T-37-01-01: cleaner must not follow a symlink out of modeRoot and delete files elsewhere.
        var outerFile = Path.Combine(_tempDir, "..", "OUTSIDE_" + Guid.NewGuid().ToString("N")[..8] + ".yml");
        outerFile = Path.GetFullPath(outerFile);
        File.WriteAllText(outerFile, "outside");

        var modeRoot = Path.Combine(_tempDir, "modeRoot");
        Directory.CreateDirectory(modeRoot);
        var innerKept = Path.Combine(modeRoot, "kept.yml");
        File.WriteAllText(innerKept, "in");

        var linkPath = Path.Combine(modeRoot, "escape.yml");
        try
        {
            File.CreateSymbolicLink(linkPath, outerFile);
        }
        catch (UnauthorizedAccessException)
        {
            // Windows developer mode not enabled — skip the test (symlink creation requires elevation).
            File.Delete(outerFile);
            return;
        }
        catch (IOException)
        {
            File.Delete(outerFile);
            return;
        }

        try
        {
            _cleaner.CleanStale(modeRoot, "deploy", new[] { innerKept });

            // Outer file must still exist — cleaner must NOT have followed the symlink.
            Assert.True(File.Exists(outerFile), "Cleaner followed a symlink outside modeRoot and deleted an outer file.");
        }
        finally
        {
            try { File.Delete(linkPath); } catch { }
            try { File.Delete(outerFile); } catch { }
        }
    }
}
