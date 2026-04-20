using DynamicWeb.Serializer.Infrastructure;
using Xunit;

namespace DynamicWeb.Serializer.Tests.Infrastructure;

public class TemplateAssetManifestTests : IDisposable
{
    private readonly string _tempRoot;

    public TemplateAssetManifestTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "TemplateManifest_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true); }
        catch { /* best effort */ }
    }

    [Fact]
    public void Write_ProducesYamlListingReferences()
    {
        var manifest = new TemplateAssetManifest();
        var refs = new List<TemplateReference>
        {
            new() { Kind = "page-layout", Path = "Swift-v2/Swift-v2_Page.cshtml",
                ReferencedBy = new() { "/Home", "/Shopping cart" } },
            new() { Kind = "grid-row", Path = "1ColumnEmail",
                ReferencedBy = new() { "/Newsletter/Welcome" } }
        };

        manifest.Write(_tempRoot, refs);

        var manifestPath = Path.Combine(_tempRoot, TemplateAssetManifest.ManifestFileName);
        Assert.True(File.Exists(manifestPath));

        var content = File.ReadAllText(manifestPath);
        Assert.Contains("Swift-v2/Swift-v2_Page.cshtml", content);
        Assert.Contains("page-layout", content);
        Assert.Contains("1ColumnEmail", content);
        Assert.Contains("grid-row", content);
        Assert.Contains("/Home", content);
        Assert.Contains("/Newsletter/Welcome", content);
    }

    [Fact]
    public void Read_RoundTripsManifest()
    {
        var manifest = new TemplateAssetManifest();
        var refs = new List<TemplateReference>
        {
            new() { Kind = "page-layout", Path = "A/B.cshtml",
                ReferencedBy = new() { "/X", "/Y" } }
        };
        manifest.Write(_tempRoot, refs);

        var readBack = manifest.Read(_tempRoot);

        Assert.NotNull(readBack);
        Assert.Single(readBack);
        Assert.Equal("page-layout", readBack![0].Kind);
        Assert.Equal("A/B.cshtml", readBack[0].Path);
        Assert.Equal(2, readBack[0].ReferencedBy.Count);
        Assert.Contains("/X", readBack[0].ReferencedBy);
    }

    [Fact]
    public void Read_NoManifest_ReturnsNull()
    {
        var manifest = new TemplateAssetManifest();
        Assert.Null(manifest.Read(_tempRoot));
    }

    [Fact]
    public void Validate_ExistingFiles_ZeroMissing()
    {
        // Arrange: create files under filesRoot that match the references
        var filesRoot = Path.Combine(_tempRoot, "Files");
        var designDir = Path.Combine(filesRoot, "Templates", "Designs", "Swift-v2");
        Directory.CreateDirectory(designDir);
        File.WriteAllText(Path.Combine(designDir, "Swift-v2_Page.cshtml"), "layout");

        var rowDefDir = Path.Combine(designDir, "Grid", "Page", "RowDefinitions");
        Directory.CreateDirectory(rowDefDir);
        File.WriteAllText(Path.Combine(rowDefDir, "1ColumnEmail.json"), "{}");

        var itemDir = Path.Combine(filesRoot, "System", "Items");
        Directory.CreateDirectory(itemDir);
        File.WriteAllText(Path.Combine(itemDir, "ItemType_BlogPost.xml"), "<Item/>");

        var refs = new List<TemplateReference>
        {
            new() { Kind = "page-layout", Path = "Swift-v2_Page.cshtml", ReferencedBy = new() { "/Home" } },
            new() { Kind = "grid-row", Path = "1ColumnEmail", ReferencedBy = new() { "/Home" } },
            new() { Kind = "item-type", Path = "BlogPost", ReferencedBy = new() { "/Blog/First" } }
        };

        var escalated = new List<string>();
        var escalator = new StrictModeEscalator(strict: false, log: escalated.Add);

        var missing = new TemplateAssetManifest().Validate(filesRoot, refs, escalator);

        Assert.Equal(0, missing);
        Assert.Empty(escalated);
    }

    [Fact]
    public void Validate_MissingPageLayout_EscalatesOnce()
    {
        var filesRoot = Path.Combine(_tempRoot, "Files");
        Directory.CreateDirectory(Path.Combine(filesRoot, "Templates", "Designs", "SomeDesign"));

        var refs = new List<TemplateReference>
        {
            new() { Kind = "page-layout", Path = "Missing_Page.cshtml",
                ReferencedBy = new() { "/Home", "/About" } }
        };

        var escalated = new List<string>();
        var escalator = new StrictModeEscalator(strict: true, log: escalated.Add);

        var missing = new TemplateAssetManifest().Validate(filesRoot, refs, escalator);

        Assert.Equal(1, missing);
        Assert.Single(escalated);
        Assert.Contains("Missing_Page.cshtml", escalated[0]);
        Assert.Contains("page-layout", escalated[0]);
        Assert.Contains("/Home", escalated[0]);
    }

    [Fact]
    public void Validate_MissingGridRowDefinition_EscalatesOnce()
    {
        var filesRoot = Path.Combine(_tempRoot, "Files");
        Directory.CreateDirectory(Path.Combine(filesRoot, "Templates", "Designs", "SomeDesign"));

        var refs = new List<TemplateReference>
        {
            new() { Kind = "grid-row", Path = "NonExistentRow", ReferencedBy = new() { "/Page1" } }
        };

        var escalated = new List<string>();
        var escalator = new StrictModeEscalator(strict: true, log: escalated.Add);

        var missing = new TemplateAssetManifest().Validate(filesRoot, refs, escalator);

        Assert.Equal(1, missing);
        Assert.Single(escalated);
        Assert.Contains("NonExistentRow", escalated[0]);
        Assert.Contains("grid-row", escalated[0]);
    }

    [Fact]
    public void Validate_ReferencedByListIsTruncatedAt5()
    {
        var filesRoot = Path.Combine(_tempRoot, "Files");
        var refs = new List<TemplateReference>
        {
            new() { Kind = "page-layout", Path = "Missing.cshtml",
                ReferencedBy = Enumerable.Range(1, 20).Select(i => $"/page{i}").ToList() }
        };

        var escalated = new List<string>();
        var escalator = new StrictModeEscalator(strict: true, log: escalated.Add);

        new TemplateAssetManifest().Validate(filesRoot, refs, escalator);

        Assert.Single(escalated);
        var msg = escalated[0];
        // Should mention the first few but not all 20
        Assert.Contains("/page1", msg);
        Assert.Contains("20 total", msg);
        // Should NOT list /page20 explicitly — it's past the cap
        Assert.DoesNotContain("/page20", msg);
    }

    [Fact]
    public void Validate_NoDesignDir_AllPageLayoutsMissing()
    {
        // filesRoot exists but no Templates/Designs — all refs should be missing
        var filesRoot = Path.Combine(_tempRoot, "Files");
        Directory.CreateDirectory(filesRoot);

        var refs = new List<TemplateReference>
        {
            new() { Kind = "page-layout", Path = "X.cshtml", ReferencedBy = new() { "/Y" } }
        };

        var escalated = new List<string>();
        var escalator = new StrictModeEscalator(strict: true, log: escalated.Add);

        var missing = new TemplateAssetManifest().Validate(filesRoot, refs, escalator);
        Assert.Equal(1, missing);
    }

    [Fact]
    public void Validate_PathTraversalAttempt_Refused()
    {
        // T-37-05-01: reject any path containing '..' or backslash or absolute prefix.
        var filesRoot = Path.Combine(_tempRoot, "Files");
        Directory.CreateDirectory(filesRoot);

        var refs = new List<TemplateReference>
        {
            new() { Kind = "page-layout", Path = "../../etc/passwd", ReferencedBy = new() { "/evil" } }
        };

        var escalated = new List<string>();
        var escalator = new StrictModeEscalator(strict: true, log: escalated.Add);

        var missing = new TemplateAssetManifest().Validate(filesRoot, refs, escalator);

        // The reference is rejected (counted as missing or as a separate error — implementation
        // may classify; here we require at least one escalation and a non-zero missing count).
        Assert.True(missing >= 1);
        Assert.NotEmpty(escalated);
        // Escalation message should mention the rejected reference.
        Assert.Contains(escalated, m => m.Contains("..", StringComparison.Ordinal)
                                     || m.Contains("invalid", StringComparison.OrdinalIgnoreCase)
                                     || m.Contains("traversal", StringComparison.OrdinalIgnoreCase)
                                     || m.Contains("Missing", StringComparison.OrdinalIgnoreCase));
    }
}
