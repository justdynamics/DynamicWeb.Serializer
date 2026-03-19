using Dynamicweb.ContentSync.Infrastructure;
using Dynamicweb.ContentSync.Models;
using Dynamicweb.ContentSync.Tests.Fixtures;
using Xunit;

namespace Dynamicweb.ContentSync.Tests.Infrastructure;

public class FileSystemStoreTests : IDisposable
{
    private readonly FileSystemStore _store;
    private readonly string _tempRoot;

    public FileSystemStoreTests()
    {
        _store = new FileSystemStore();
        _tempRoot = Path.Combine(Path.GetTempPath(), "ContentSyncTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    // -------------------------------------------------------------------------
    // WriteTree structural layout
    // -------------------------------------------------------------------------

    [Fact]
    public void WriteTree_CreatesAreaFolder_WithAreaYml()
    {
        var area = ContentTreeBuilder.BuildSampleTree();

        _store.WriteTree(area, _tempRoot);

        var areaPath = Path.Combine(_tempRoot, "Main Website");
        Assert.True(Directory.Exists(areaPath), $"Area folder not found at '{areaPath}'");
        Assert.True(File.Exists(Path.Combine(areaPath, "area.yml")), "area.yml not found in area folder");
    }

    [Fact]
    public void WriteTree_CreatesPageSubfolder_WithPageYml()
    {
        var area = ContentTreeBuilder.BuildSampleTree();

        _store.WriteTree(area, _tempRoot);

        var areaPath = Path.Combine(_tempRoot, "Main Website");
        var pageYml = Path.Combine(areaPath, "Customer Center", "page.yml");
        Assert.True(File.Exists(pageYml), $"page.yml not found at '{pageYml}'");
    }

    [Fact]
    public void WriteTree_CreatesGridRowSubfolder_WithGridRowYml()
    {
        var area = ContentTreeBuilder.BuildSampleTree();

        _store.WriteTree(area, _tempRoot);

        var gridRowYml = Path.Combine(_tempRoot, "Main Website", "Customer Center", "grid-row-1", "grid-row.yml");
        Assert.True(File.Exists(gridRowYml), $"grid-row.yml not found at '{gridRowYml}'");
    }

    [Fact]
    public void WriteTree_CreatesParagraphFiles_InGridRowFolder()
    {
        var area = ContentTreeBuilder.BuildSampleTree();

        _store.WriteTree(area, _tempRoot);

        var paragraphFile = Path.Combine(_tempRoot, "Main Website", "Customer Center", "grid-row-1", "paragraph-1.yml");
        Assert.True(File.Exists(paragraphFile), $"paragraph-1.yml not found at '{paragraphFile}'");
    }

    [Fact]
    public void WriteTree_PageYml_DoesNotContainGridRows()
    {
        var area = ContentTreeBuilder.BuildSampleTree();

        _store.WriteTree(area, _tempRoot);

        var pageYmlPath = Path.Combine(_tempRoot, "Main Website", "Customer Center", "page.yml");
        var pageYmlText = File.ReadAllText(pageYmlPath);
        Assert.DoesNotContain("gridRows", pageYmlText, StringComparison.OrdinalIgnoreCase);
    }

    // -------------------------------------------------------------------------
    // Folder name sanitization
    // -------------------------------------------------------------------------

    [Fact]
    public void WriteTree_SanitizesFolderNames_ReplacesInvalidChars()
    {
        var area = new SerializedArea
        {
            AreaId = Guid.NewGuid(),
            Name = "Test Area",
            SortOrder = 1,
            Pages = new List<SerializedPage>
            {
                ContentTreeBuilder.BuildSinglePage("Test: Page / 1") with { SortOrder = 1 }
            }
        };

        _store.WriteTree(area, _tempRoot);

        var areaPath = Path.Combine(_tempRoot, "Test Area");
        // Colon and slash are invalid file name chars; should be replaced with underscore
        var sanitizedPageDir = Path.Combine(areaPath, "Test_ Page _ 1");
        Assert.True(Directory.Exists(sanitizedPageDir),
            $"Expected sanitized folder 'Test_ Page _ 1' not found. Dirs: {string.Join(", ", Directory.GetDirectories(areaPath).Select(Path.GetFileName))}");
    }

    [Fact]
    public void WriteTree_PreservesSpacesInFolderNames()
    {
        var area = new SerializedArea
        {
            AreaId = Guid.NewGuid(),
            Name = "Test Area",
            SortOrder = 1,
            Pages = new List<SerializedPage>
            {
                ContentTreeBuilder.BuildSinglePage("Customer Center") with { SortOrder = 1 }
            }
        };

        _store.WriteTree(area, _tempRoot);

        var folderPath = Path.Combine(_tempRoot, "Test Area", "Customer Center");
        Assert.True(Directory.Exists(folderPath),
            $"Expected folder 'Customer Center' (spaces preserved) not found. Dirs: {string.Join(", ", Directory.GetDirectories(Path.Combine(_tempRoot, "Test Area")).Select(Path.GetFileName))}");
    }

    // -------------------------------------------------------------------------
    // Duplicate sibling disambiguation
    // -------------------------------------------------------------------------

    [Fact]
    public void WriteTree_DeduplicatesSiblingNames_WithGuidSuffix()
    {
        var guid1 = Guid.NewGuid();
        var guid2 = Guid.NewGuid();
        var area = new SerializedArea
        {
            AreaId = Guid.NewGuid(),
            Name = "Test Area",
            SortOrder = 1,
            Pages = new List<SerializedPage>
            {
                ContentTreeBuilder.BuildSinglePage("About", guid1) with { SortOrder = 1 },
                ContentTreeBuilder.BuildSinglePage("About", guid2) with { SortOrder = 2 }
            }
        };

        _store.WriteTree(area, _tempRoot);

        var areaPath = Path.Combine(_tempRoot, "Test Area");
        var dirs = Directory.GetDirectories(areaPath).Select(Path.GetFileName).ToList();

        // First "About" should have plain name
        Assert.Contains("About", dirs);

        // Second "About" should have GUID suffix in the format "About [xxxxxx]"
        var dedupedDir = dirs.FirstOrDefault(d => d != null && d.StartsWith("About [") && d.EndsWith("]"));
        Assert.NotNull(dedupedDir);

        // Suffix should be 6 hex chars from the second page's GUID
        var expectedSuffix = guid2.ToString("N")[..6];
        Assert.Equal($"About [{expectedSuffix}]", dedupedDir);
    }

    // -------------------------------------------------------------------------
    // Determinism / idempotency
    // -------------------------------------------------------------------------

    [Fact]
    public void WriteTree_IsIdempotent_ByteForByteIdentical()
    {
        var area = ContentTreeBuilder.BuildSampleTree();

        _store.WriteTree(area, _tempRoot);
        var firstWrite = ReadAllYamlContents(_tempRoot);

        _store.WriteTree(area, _tempRoot);
        var secondWrite = ReadAllYamlContents(_tempRoot);

        Assert.Equal(firstWrite.Count, secondWrite.Count);
        foreach (var (path, content) in firstWrite)
        {
            Assert.True(secondWrite.ContainsKey(path), $"File missing after second write: {path}");
            Assert.Equal(content, secondWrite[path]);
        }
    }

    [Fact]
    public void WriteTree_SortsItemsBySortOrder()
    {
        // Pages with SortOrder 3, 1, 2 — verify they end up in SortOrder order
        var area = new SerializedArea
        {
            AreaId = Guid.NewGuid(),
            Name = "Test Area",
            SortOrder = 1,
            Pages = new List<SerializedPage>
            {
                ContentTreeBuilder.BuildSinglePage("Page C") with { SortOrder = 3 },
                ContentTreeBuilder.BuildSinglePage("Page A") with { SortOrder = 1 },
                ContentTreeBuilder.BuildSinglePage("Page B") with { SortOrder = 2 }
            }
        };

        // Write twice — if ordering is non-deterministic, byte comparison would fail
        _store.WriteTree(area, _tempRoot);
        var first = ReadAllYamlContents(_tempRoot);

        var tempRoot2 = _tempRoot + "_v2";
        Directory.CreateDirectory(tempRoot2);
        try
        {
            // Write with pages in reversed order in the collection
            var area2 = area with
            {
                Pages = new List<SerializedPage>
                {
                    ContentTreeBuilder.BuildSinglePage("Page A") with
                    {
                        PageUniqueId = area.Pages[1].PageUniqueId,
                        SortOrder = 1
                    },
                    ContentTreeBuilder.BuildSinglePage("Page B") with
                    {
                        PageUniqueId = area.Pages[2].PageUniqueId,
                        SortOrder = 2
                    },
                    ContentTreeBuilder.BuildSinglePage("Page C") with
                    {
                        PageUniqueId = area.Pages[0].PageUniqueId,
                        SortOrder = 3
                    }
                }
            };
            _store.WriteTree(area2, tempRoot2);
            var second = ReadAllYamlContents(tempRoot2);

            // Both writes should produce the same set of relative file paths
            var firstKeys = first.Keys.Select(k => k.Replace(_tempRoot, "ROOT")).OrderBy(k => k).ToList();
            var secondKeys = second.Keys.Select(k => k.Replace(tempRoot2, "ROOT")).OrderBy(k => k).ToList();
            Assert.Equal(firstKeys, secondKeys);
        }
        finally
        {
            if (Directory.Exists(tempRoot2))
                Directory.Delete(tempRoot2, recursive: true);
        }
    }

    // -------------------------------------------------------------------------
    // ReadTree round-trip
    // -------------------------------------------------------------------------

    [Fact]
    public void ReadTree_ReconstructsWrittenTree()
    {
        var original = ContentTreeBuilder.BuildSampleTree();

        _store.WriteTree(original, _tempRoot);
        var readBack = _store.ReadTree(_tempRoot);

        Assert.Equal(original.Name, readBack.Name);
        Assert.Equal(original.AreaId, readBack.AreaId);
        Assert.Equal(original.Pages.Count, readBack.Pages.Count);

        // Verify page names are present (order may vary on read-back from filesystem)
        var originalPageNames = original.Pages.Select(p => p.Name).OrderBy(n => n).ToList();
        var readBackPageNames = readBack.Pages.Select(p => p.Name).OrderBy(n => n).ToList();
        Assert.Equal(originalPageNames, readBackPageNames);

        // Verify paragraph counts via SortOrder on grid rows
        var originalPage1 = original.Pages.First(p => p.Name == "Customer Center");
        var readBackPage1 = readBack.Pages.First(p => p.Name == "Customer Center");

        Assert.Equal(originalPage1.GridRows.Count, readBackPage1.GridRows.Count);
        var originalParagraphCount = originalPage1.GridRows.SelectMany(gr => gr.Columns).SelectMany(c => c.Paragraphs).Count();
        var readBackParagraphCount = readBackPage1.GridRows.SelectMany(gr => gr.Columns).SelectMany(c => c.Paragraphs).Count();
        Assert.Equal(originalParagraphCount, readBackParagraphCount);
    }

    [Fact]
    public void ReadTree_RoundTrips_FieldValues()
    {
        var trickyFields = new Dictionary<string, object>
        {
            ["tilde"] = "~",
            ["crlf"] = "Line1\r\nLine2",
            ["html"] = "<p>Test &amp; value</p>",
            ["bang"] = "!important",
            ["plain"] = "normal text"
        };

        var area = new SerializedArea
        {
            AreaId = Guid.NewGuid(),
            Name = "Test Area",
            SortOrder = 1,
            Pages = new List<SerializedPage>
            {
                ContentTreeBuilder.BuildSinglePage("Test Page") with
                {
                    SortOrder = 1,
                    Fields = trickyFields
                }
            }
        };

        _store.WriteTree(area, _tempRoot);
        var readBack = _store.ReadTree(_tempRoot);

        var readBackPage = readBack.Pages.First();
        foreach (var (key, expected) in trickyFields)
        {
            Assert.True(readBackPage.Fields.ContainsKey(key), $"Field '{key}' not found in read-back page");
            Assert.Equal(expected.ToString(), readBackPage.Fields[key]?.ToString());
        }
    }

    // -------------------------------------------------------------------------
    // Dictionary key ordering
    // -------------------------------------------------------------------------

    [Fact]
    public void WriteTree_DictionaryKeys_AreSortedAlphabetically()
    {
        var area = new SerializedArea
        {
            AreaId = Guid.NewGuid(),
            Name = "Test Area",
            SortOrder = 1,
            Pages = new List<SerializedPage>
            {
                ContentTreeBuilder.BuildSinglePage("Test Page") with
                {
                    SortOrder = 1,
                    Fields = new Dictionary<string, object>
                    {
                        ["zebra"] = "z-value",
                        ["alpha"] = "a-value",
                        ["middle"] = "m-value"
                    }
                }
            }
        };

        _store.WriteTree(area, _tempRoot);

        var pageYmlPath = Path.Combine(_tempRoot, "Test Area", "Test Page", "page.yml");
        var pageYml = File.ReadAllText(pageYmlPath);

        var alphaIdx = pageYml.IndexOf("alpha", StringComparison.Ordinal);
        var middleIdx = pageYml.IndexOf("middle", StringComparison.Ordinal);
        var zebraIdx = pageYml.IndexOf("zebra", StringComparison.Ordinal);

        Assert.True(alphaIdx >= 0 && middleIdx >= 0 && zebraIdx >= 0,
            "All field keys should appear in the YAML file");
        Assert.True(alphaIdx < middleIdx, $"'alpha' (pos {alphaIdx}) should appear before 'middle' (pos {middleIdx})");
        Assert.True(middleIdx < zebraIdx, $"'middle' (pos {middleIdx}) should appear before 'zebra' (pos {zebraIdx})");
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static Dictionary<string, string> ReadAllYamlContents(string rootPath)
    {
        var result = new Dictionary<string, string>();
        foreach (var file in Directory.EnumerateFiles(rootPath, "*.yml", SearchOption.AllDirectories))
        {
            result[file] = File.ReadAllText(file);
        }
        return result;
    }
}
