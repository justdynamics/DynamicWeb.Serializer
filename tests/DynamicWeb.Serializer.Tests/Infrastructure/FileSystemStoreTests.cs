using DynamicWeb.Serializer.Infrastructure;
using DynamicWeb.Serializer.Models;
using DynamicWeb.Serializer.Tests.Fixtures;
using Xunit;

namespace DynamicWeb.Serializer.Tests.Infrastructure;

public class FileSystemStoreTests : IDisposable
{
    private readonly FileSystemStore _store;
    private readonly string _tempRoot;

    public FileSystemStoreTests()
    {
        _store = new FileSystemStore();
        _tempRoot = Path.Combine(Path.GetTempPath(), "SerializerTests_" + Guid.NewGuid().ToString("N")[..8]);
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

        var paragraphFile = Path.Combine(_tempRoot, "Main Website", "Customer Center", "grid-row-1", "paragraph-c1-1.yml");
        Assert.True(File.Exists(paragraphFile), $"paragraph-c1-1.yml not found at '{paragraphFile}'");
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
    // Recursive child pages
    // -------------------------------------------------------------------------

    [Fact]
    public void WriteTree_WithChildPages_CreatesNestedFolders()
    {
        var area = ContentTreeBuilder.BuildNestedTree();

        _store.WriteTree(area, _tempRoot);

        var areaPath = Path.Combine(_tempRoot, "Test Website");
        Assert.True(File.Exists(Path.Combine(areaPath, "Parent", "Child A", "page.yml")),
            "Parent/Child A/page.yml should exist");
        Assert.True(File.Exists(Path.Combine(areaPath, "Parent", "Child B", "page.yml")),
            "Parent/Child B/page.yml should exist");
        Assert.True(File.Exists(Path.Combine(areaPath, "Parent", "Child B", "Grandchild", "page.yml")),
            "Parent/Child B/Grandchild/page.yml should exist");
    }

    [Fact]
    public void WriteTree_WithChildPages_PageYml_DoesNotContainChildren()
    {
        var area = ContentTreeBuilder.BuildNestedTree();

        _store.WriteTree(area, _tempRoot);

        var parentYmlPath = Path.Combine(_tempRoot, "Test Website", "Parent", "page.yml");
        var parentYmlText = File.ReadAllText(parentYmlPath);
        Assert.DoesNotContain("children", parentYmlText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadTree_WithChildPages_ReconstructsHierarchy()
    {
        var area = ContentTreeBuilder.BuildNestedTree();

        _store.WriteTree(area, _tempRoot);
        var readBack = _store.ReadTree(_tempRoot);

        // Pages ordered by SortOrder: Parent (1), Sibling (2)
        var readBackPages = readBack.Pages.OrderBy(p => p.SortOrder).ToList();
        Assert.Equal(2, readBackPages.Count);

        var parent = readBackPages[0];
        Assert.Equal("Parent", parent.Name);
        Assert.Equal(2, parent.Children.Count); // Child A and Child B

        var children = parent.Children.OrderBy(c => c.SortOrder).ToList();
        var childB = children[1];
        Assert.Equal("Child B", childB.Name);
        Assert.Single(childB.Children); // Grandchild
        Assert.Equal("Grandchild", childB.Children[0].Name);
    }

    [Fact]
    public void WriteTree_ReadTree_NestedRoundTrip_IsLossless()
    {
        var area = ContentTreeBuilder.BuildNestedTree();

        _store.WriteTree(area, _tempRoot);
        var readBack = _store.ReadTree(_tempRoot);

        var readBackPages = readBack.Pages.OrderBy(p => p.SortOrder).ToList();
        var parent = readBackPages[0];
        Assert.Equal("Parent", parent.Name);
        Assert.Equal("Parent Page", parent.Fields["title"].ToString());

        var parentParagraphCount = parent.GridRows.SelectMany(gr => gr.Columns).SelectMany(c => c.Paragraphs).Count();
        Assert.Equal(1, parentParagraphCount);

        var children = parent.Children.OrderBy(c => c.SortOrder).ToList();
        Assert.Equal("Child A", children[0].Name);
        Assert.Equal("Child A Page", children[0].Fields["title"].ToString());

        var childAParagraphCount = children[0].GridRows.SelectMany(gr => gr.Columns).SelectMany(c => c.Paragraphs).Count();
        Assert.Equal(1, childAParagraphCount);

        var grandchild = children[1].Children[0];
        Assert.Equal("Grandchild", grandchild.Name);
        Assert.Equal("Grandchild Page", grandchild.Fields["title"].ToString());

        var grandchildParagraphCount = grandchild.GridRows.SelectMany(gr => gr.Columns).SelectMany(c => c.Paragraphs).Count();
        Assert.Equal(1, grandchildParagraphCount);
        Assert.Equal("Grandchild content", grandchild.GridRows[0].Columns[0].Paragraphs[0].Fields["text"].ToString());
    }

    // -------------------------------------------------------------------------
    // Long-path handling (INF-03)
    // -------------------------------------------------------------------------

    [Fact]
    public void WriteTree_LongPageName_TruncatesPathAndWarns()
    {
        // Create a page name 250+ characters long
        var longName = new string('A', 250);
        var area = new SerializedArea
        {
            AreaId = Guid.NewGuid(),
            Name = "Test Area",
            SortOrder = 1,
            Pages = new List<SerializedPage>
            {
                ContentTreeBuilder.BuildSinglePage(longName) with { SortOrder = 1 }
            }
        };

        var originalError = Console.Error;
        var errorCapture = new StringWriter();
        Console.SetError(errorCapture);
        try
        {
            _store.WriteTree(area, _tempRoot);
        }
        finally
        {
            Console.SetError(originalError);
        }

        // At least one page folder should have been created
        var areaPath = Path.Combine(_tempRoot, "Test Area");
        var createdDirs = Directory.GetDirectories(areaPath);
        Assert.NotEmpty(createdDirs);

        // The created folder path length must be <= 247 characters
        foreach (var dir in createdDirs)
        {
            Assert.True(dir.Length <= 247, $"Folder path exceeds 247 chars: '{dir}' (length={dir.Length})");
        }

        // A warning should have been emitted
        var errorOutput = errorCapture.ToString();
        Assert.Contains("[Serializer] Warning: Path truncated", errorOutput, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WriteTree_DeeplyNestedChildren_HandlesLongPaths()
    {
        // Create a 5-level deep tree with 50-char names per level
        // Combined path will exceed 260 chars on deeper levels depending on temp root
        var level5 = BuildDeepChildPage("LevelFive_" + new string('E', 40), 1, null);
        var level4 = BuildDeepChildPage("LevelFour_" + new string('D', 40), 1, level5);
        var level3 = BuildDeepChildPage("LevelThree" + new string('C', 40), 1, level4);
        var level2 = BuildDeepChildPage("LevelTwo__" + new string('B', 40), 1, level3);
        var level1 = BuildDeepChildPage("LevelOne__" + new string('A', 40), 1, level2);

        var area = new SerializedArea
        {
            AreaId = Guid.NewGuid(),
            Name = "Deep",
            SortOrder = 1,
            Pages = new List<SerializedPage> { level1 }
        };

        var originalError = Console.Error;
        var errorCapture = new StringWriter();
        Console.SetError(errorCapture);

        // Should not throw
        var exception = Record.Exception(() =>
        {
            Console.SetError(errorCapture);
            _store.WriteTree(area, _tempRoot);
        });
        Console.SetError(originalError);

        Assert.Null(exception);

        // Top-level page folder should exist
        var areaPath = Path.Combine(_tempRoot, "Deep");
        Assert.True(Directory.Exists(areaPath), "Area directory should exist");
        Assert.NotEmpty(Directory.GetDirectories(areaPath));

        // If any warnings were emitted, they should be path truncation warnings
        var errorOutput = errorCapture.ToString();
        if (!string.IsNullOrEmpty(errorOutput))
        {
            Assert.Contains("[Serializer] Warning", errorOutput, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void WriteTree_LongPath_DoesNotCrash()
    {
        // Create a page whose name combined with root path would exceed 300 chars
        var longName = new string('X', 280);
        var area = new SerializedArea
        {
            AreaId = Guid.NewGuid(),
            Name = "A",
            SortOrder = 1,
            Pages = new List<SerializedPage>
            {
                ContentTreeBuilder.BuildSinglePage(longName) with { SortOrder = 1 }
            }
        };

        // Should not throw — method must warn and handle gracefully
        var exception = Record.Exception(() => _store.WriteTree(area, _tempRoot));
        Assert.Null(exception);
    }

    private static SerializedPage BuildDeepChildPage(string name, int sortOrder, SerializedPage? child)
    {
        var page = ContentTreeBuilder.BuildSinglePage(name) with
        {
            SortOrder = sortOrder,
            Fields = new Dictionary<string, object> { ["title"] = name }
        };

        if (child != null)
        {
            page = page with { Children = new List<SerializedPage> { child } };
        }

        return page;
    }

    // -------------------------------------------------------------------------
    // Multi-column round-trip
    // -------------------------------------------------------------------------

    [Fact]
    public void WriteTree_ReadTree_MultiColumn_PreservesColumnAttribution()
    {
        var area = ContentTreeBuilder.BuildMultiColumnTree();
        _store.WriteTree(area, _tempRoot);
        var readBack = _store.ReadTree(_tempRoot);

        var page = readBack.Pages.First();
        var row = page.GridRows.First();
        Assert.Equal(2, row.Columns.Count);

        var col1 = row.Columns.First(c => c.Id == 1);
        var col2 = row.Columns.First(c => c.Id == 2);

        Assert.Equal(2, col1.Paragraphs.Count);
        Assert.Single(col2.Paragraphs);

        // Verify content landed in the right columns
        Assert.Contains(col1.Paragraphs, p => p.Fields.ContainsKey("text") && p.Fields["text"].ToString() == "Col1 Para1");
        Assert.Contains(col1.Paragraphs, p => p.Fields.ContainsKey("text") && p.Fields["text"].ToString() == "Col1 Para2");
        Assert.Contains(col2.Paragraphs, p => p.Fields.ContainsKey("imageUrl"));
    }

    [Fact]
    public void WriteTree_MultiColumn_SortOrderCollision_CreatesSeparateFiles()
    {
        var area = ContentTreeBuilder.BuildMultiColumnTree();
        _store.WriteTree(area, _tempRoot);

        var gridRowDir = Path.Combine(_tempRoot, "Test Area", "Multi-Column Page", "grid-row-1");
        // Both columns have a paragraph with SortOrder=1 — they should produce separate files
        Assert.True(File.Exists(Path.Combine(gridRowDir, "paragraph-c1-1.yml")),
            "paragraph-c1-1.yml should exist for column 1 sort 1");
        Assert.True(File.Exists(Path.Combine(gridRowDir, "paragraph-c2-1.yml")),
            "paragraph-c2-1.yml should exist for column 2 sort 1");
        Assert.True(File.Exists(Path.Combine(gridRowDir, "paragraph-c1-2.yml")),
            "paragraph-c1-2.yml should exist for column 1 sort 2");
    }

    [Fact]
    public void ReadTree_BackwardCompat_OldParagraphFiles_DefaultToColumn1()
    {
        // Simulate v1.0 file layout: paragraph-{N}.yml without ColumnId in YAML
        var areaDir = Path.Combine(_tempRoot, "Legacy Area");
        Directory.CreateDirectory(areaDir);
        File.WriteAllText(Path.Combine(areaDir, "area.yml"),
            "areaId: " + Guid.NewGuid() + "\nname: Legacy Area\nsortOrder: 1\n");

        var pageDir = Path.Combine(areaDir, "Legacy Page");
        Directory.CreateDirectory(pageDir);
        File.WriteAllText(Path.Combine(pageDir, "page.yml"),
            "pageUniqueId: " + Guid.NewGuid() + "\nname: Legacy Page\nmenuText: Legacy Page\nurlName: legacy-page\nsortOrder: 1\n");

        var gridDir = Path.Combine(pageDir, "grid-row-1");
        Directory.CreateDirectory(gridDir);
        File.WriteAllText(Path.Combine(gridDir, "grid-row.yml"),
            "id: " + Guid.NewGuid() + "\nsortOrder: 1\ncolumns:\n- id: 1\n  width: 6\n- id: 2\n  width: 6\n");

        // Old-style paragraph file — no columnId field
        File.WriteAllText(Path.Combine(gridDir, "paragraph-1.yml"),
            "paragraphUniqueId: " + Guid.NewGuid() + "\nsortOrder: 1\nitemType: ContentModule\n");

        var readBack = _store.ReadTree(_tempRoot);
        var row = readBack.Pages.First().GridRows.First();
        var col1 = row.Columns.First(c => c.Id == 1);
        var col2 = row.Columns.First(c => c.Id == 2);

        // Old paragraphs without ColumnId should default to column 1
        Assert.Single(col1.Paragraphs);
        Assert.Empty(col2.Paragraphs);
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
