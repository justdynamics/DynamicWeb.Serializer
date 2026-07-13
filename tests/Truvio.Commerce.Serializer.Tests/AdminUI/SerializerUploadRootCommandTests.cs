using System.IO.Compression;
using Truvio.Commerce.Serializer.AdminUI.Commands;
using Dynamicweb.CoreUI.Data;
using Xunit;

namespace Truvio.Commerce.Serializer.Tests.AdminUI;

/// <summary>
/// LRN-hosted-publish-02: bulk SerializeRoot upload. Exercises the testable core
/// (<c>ExtractReplacingTree</c>) against real zips + temp dirs — replace-existing semantics,
/// file count, nested paths, and zip-slip defence — plus the mode-gate on <c>Handle</c>.
/// </summary>
public class SerializerUploadRootCommandTests : IDisposable
{
    private readonly string _work;

    public SerializerUploadRootCommandTests()
    {
        _work = Path.Combine(Path.GetTempPath(), "truvio-uploadroot-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_work);
    }

    public void Dispose()
    {
        try { Directory.Delete(_work, recursive: true); } catch { /* best effort */ }
    }

    private string MakeZip(string name, Action<ZipArchive> build)
    {
        var zipPath = Path.Combine(_work, name);
        using var fs = new FileStream(zipPath, FileMode.Create);
        using var archive = new ZipArchive(fs, ZipArchiveMode.Create);
        build(archive);
        return zipPath;
    }

    private static void AddEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(content);
    }

    [Fact]
    public void ExtractReplacingTree_ExtractsAllFiles_PreservingNestedPaths_ReturnsCount()
    {
        var zip = MakeZip("payload.zip", a =>
        {
            AddEntry(a, "replace-manifest.json", "{}");
            AddEntry(a, "_content/Area/page.yml", "menu: Home");
            AddEntry(a, "_sql/EcomProducts/rows.yml", "rows: []");
        });

        var target = Path.Combine(_work, "SerializeRoot", "replace");

        var count = SerializerUploadRootCommand.ExtractReplacingTree(zip, target);

        Assert.Equal(3, count);
        Assert.True(File.Exists(Path.Combine(target, "replace-manifest.json")));
        Assert.True(File.Exists(Path.Combine(target, "_content", "Area", "page.yml")));
        Assert.True(File.Exists(Path.Combine(target, "_sql", "EcomProducts", "rows.yml")));
    }

    [Fact]
    public void ExtractReplacingTree_ReplacesExistingTree_StaleFilesRemoved()
    {
        var target = Path.Combine(_work, "SerializeRoot", "replace");
        Directory.CreateDirectory(Path.Combine(target, "_content", "OldArea"));
        File.WriteAllText(Path.Combine(target, "_content", "OldArea", "stale.yml"), "old");
        File.WriteAllText(Path.Combine(target, "stale-manifest.json"), "old");

        var zip = MakeZip("payload.zip", a => AddEntry(a, "replace-manifest.json", "{}"));

        var count = SerializerUploadRootCommand.ExtractReplacingTree(zip, target);

        Assert.Equal(1, count);
        Assert.True(File.Exists(Path.Combine(target, "replace-manifest.json")));
        Assert.False(File.Exists(Path.Combine(target, "stale-manifest.json")));
        Assert.False(Directory.Exists(Path.Combine(target, "_content", "OldArea")));
    }

    [Fact]
    public void ExtractReplacingTree_DirectoryEntries_NotCountedAsFiles()
    {
        var zip = MakeZip("payload.zip", a =>
        {
            a.CreateEntry("_content/");        // explicit directory entry
            AddEntry(a, "_content/page.yml", "x");
        });

        var target = Path.Combine(_work, "SerializeRoot", "merge");

        var count = SerializerUploadRootCommand.ExtractReplacingTree(zip, target);

        Assert.Equal(1, count);
        Assert.True(File.Exists(Path.Combine(target, "_content", "page.yml")));
    }

    [Fact]
    public void ExtractReplacingTree_ZipSlipEntry_Rejected()
    {
        var zip = MakeZip("evil.zip", a =>
        {
            AddEntry(a, "ok.yml", "fine");
            AddEntry(a, "../../escape.txt", "pwned");
        });

        var target = Path.Combine(_work, "SerializeRoot", "replace");

        var ex = Assert.Throws<InvalidOperationException>(
            () => SerializerUploadRootCommand.ExtractReplacingTree(zip, target));
        Assert.Contains("zip-slip", ex.Message);

        // The escaping file must NOT have been written outside the target.
        Assert.False(File.Exists(Path.Combine(_work, "escape.txt")));
    }

    [Fact]
    public void Handle_InvalidMode_ReturnsInvalid_BeforeAnyFileWork()
    {
        var cmd = new SerializerUploadRootCommand { Mode = "bogus", FilePath = "/Files/x.zip" };
        var result = cmd.Handle();
        Assert.Equal(CommandResult.ResultType.Invalid, result.Status);
        Assert.Contains("Invalid mode", result.Message ?? string.Empty);
    }
}
