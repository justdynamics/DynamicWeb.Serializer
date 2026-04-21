using DynamicWeb.Serializer.Providers.SqlTable;
using Xunit;

namespace DynamicWeb.Serializer.Tests.Providers.SqlTable;

/// <summary>
/// Phase 38 C.1 (D-38-10): regression tests for FlatFileStore.DeduplicateFileName.
/// Threat anchor: T-38-04 — silent data loss on duplicate/empty identity values.
/// Root cause: hash-of-identity approach emitted the same dedup filename N times
/// for N duplicate-identity rows, causing file overwrites (HashSet.Add return
/// value silently ignored).
/// Fix: monotonic counter [{hashPrefix}-N] preserves all rows.
/// Scope (W1): DeduplicateFileName only; ORDER BY in SqlTableReader is out of scope.
/// </summary>
[Trait("Category", "Phase38")]
public class FlatFileStoreDeduplicationTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FlatFileStore _store;

    public FlatFileStoreDeduplicationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(),
            "FlatFileStoreDedupTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _store = new FlatFileStore();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void WriteRow_MultipleEmptyIdentities_AllRowsPreserved()
    {
        // 5 rows, all with empty rowIdentity (simulating 5 empty-name EcomProducts).
        // Pre-fix: 1 file survives (overwrites). Post-fix: 5 files.
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var writtenFiles = new List<string>();

        for (int i = 0; i < 5; i++)
        {
            _store.WriteRow(
                outputRoot: _tempDir,
                tableName: "EcomProducts",
                rowIdentity: "",
                rowData: new Dictionary<string, object?>
                {
                    ["ProductId"] = $"P{i}",
                    ["ProductName"] = ""
                },
                usedNames: usedNames,
                writtenFiles: writtenFiles);
        }

        var files = Directory.GetFiles(
            Path.Combine(_tempDir, "_sql", "EcomProducts"), "*.yml");
        Assert.Equal(5, files.Length);
        Assert.Equal(5, writtenFiles.Count);
    }

    [Fact]
    public void WriteRow_SingleUniqueIdentity_NoCounterSuffix()
    {
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var writtenFiles = new List<string>();

        _store.WriteRow(
            outputRoot: _tempDir,
            tableName: "EcomProducts",
            rowIdentity: "ProductOne",
            rowData: new Dictionary<string, object?> { ["ProductId"] = "P1" },
            usedNames: usedNames,
            writtenFiles: writtenFiles);

        var files = Directory.GetFiles(
            Path.Combine(_tempDir, "_sql", "EcomProducts"), "*.yml");
        Assert.Single(files);
        Assert.DoesNotContain("[", Path.GetFileName(files[0])); // no counter suffix
    }

    [Fact]
    public void WriteRow_DuplicateNamedIdentities_AllPreserved()
    {
        // 3 rows with the same non-empty identity "Widget" should produce
        // Widget.yml, Widget [hash-1].yml, Widget [hash-2].yml
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var writtenFiles = new List<string>();

        for (int i = 0; i < 3; i++)
        {
            _store.WriteRow(
                outputRoot: _tempDir,
                tableName: "EcomProducts",
                rowIdentity: "Widget",
                rowData: new Dictionary<string, object?> { ["ProductId"] = $"P{i}" },
                usedNames: usedNames,
                writtenFiles: writtenFiles);
        }

        var files = Directory.GetFiles(
            Path.Combine(_tempDir, "_sql", "EcomProducts"), "*.yml");
        Assert.Equal(3, files.Length);
        Assert.Contains(files, f => Path.GetFileName(f) == "Widget.yml");
        Assert.Contains(files, f => Path.GetFileName(f).Contains("-1]"));
        Assert.Contains(files, f => Path.GetFileName(f).Contains("-2]"));
    }
}
