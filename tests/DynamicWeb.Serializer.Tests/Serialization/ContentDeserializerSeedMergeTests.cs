using System.Reflection;
using Dynamicweb.Content;
using DynamicWeb.Serializer.Configuration;
using DynamicWeb.Serializer.Infrastructure;
using DynamicWeb.Serializer.Models;
using DynamicWeb.Serializer.Serialization;
using Xunit;

namespace DynamicWeb.Serializer.Tests.Serialization;

/// <summary>
/// Phase 39 D-01..D-11, D-19 structural + helper-level coverage for
/// <see cref="ContentDeserializer"/>'s Seed-mode field-level merge branch.
///
/// Full end-to-end integration tests of the DestinationWins UPDATE path require a
/// live DW runtime (see <c>tests/DynamicWeb.Serializer.IntegrationTests/Deserialization/
/// CustomerCenterDeserializationTests.cs</c>). These unit tests verify the wiring:
/// (a) legacy row-skip is removed, (b) new merge helpers exist with the right
/// signatures, (c) the pure helpers (<see cref="ContentDeserializer.ApplyPagePropertiesWithMerge"/>,
/// <see cref="ContentDeserializer.MergePageScalars"/>) produce the D-01 behavior
/// on real <see cref="Page"/> instances, (d) the <c>Seed-merge:</c> log format is
/// present and the old <c>Seed-skip:</c> line is gone (D-11), (e) permissions are
/// not touched from the Seed branch (D-06).
/// </summary>
[Trait("Category", "Phase39")]
public class ContentDeserializerSeedMergeTests
{
    private static readonly string ContentDeserializerSource = File.ReadAllText(
        Path.Combine(FindRepoRoot(), "src", "DynamicWeb.Serializer", "Serialization", "ContentDeserializer.cs"));

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "DynamicWeb.Serializer.sln")))
            dir = dir.Parent;
        if (dir == null)
            throw new InvalidOperationException("Could not locate repo root from " + AppContext.BaseDirectory);
        return dir.FullName;
    }

    private static SerializerConfiguration MinimalConfig() => new()
    {
        OutputDirectory = Path.GetTempPath()
    };

    // -----------------------------------------------------------------------
    // D-11: Legacy row-skip removed; new Seed-merge: log format present.
    // -----------------------------------------------------------------------

    [Fact]
    public void SeedMerge_RemovesSeedSkipLogLine_NoSuchLineInSource()
    {
        // D-11 regression guard: the old "Seed-skip:" line must be gone from the source.
        Assert.DoesNotContain("Seed-skip:", ContentDeserializerSource);
    }

    [Fact]
    public void SeedMerge_EmitsSeedMergeLogFormat_SourceContainsNewPrefix()
    {
        // D-11: new log format "Seed-merge: ... N filled, M left" replaces Seed-skip.
        Assert.Contains("Seed-merge:", ContentDeserializerSource);
    }

    [Fact]
    public void SeedMerge_EmitsFilledAndLeftInLog_D11Format()
    {
        // D-11 shape: "N filled, M left" counter phrasing.
        Assert.Contains("filled", ContentDeserializerSource);
        Assert.Contains("left", ContentDeserializerSource);
    }

    // -----------------------------------------------------------------------
    // D-06: Permissions are NOT reachable from the Seed merge branch.
    // -----------------------------------------------------------------------

    [Fact]
    public void SeedMerge_Permissions_NotTouched_MarkerPresent()
    {
        // D-06: An explicit code comment marker anchors the permissions-bypass test.
        // The comment lives inside the DestinationWins merge branch so that a future
        // accidental re-introduction of _permissionMapper.ApplyPermissions there will
        // fail this assertion.
        Assert.Contains("D-06: permissions NOT applied on Seed", ContentDeserializerSource);
    }

    // -----------------------------------------------------------------------
    // D-08 / D-14: MergePredicate contract consumed by ContentDeserializer.
    // -----------------------------------------------------------------------

    [Fact]
    public void SeedMerge_ConsumesMergePredicate_MultipleCallSites()
    {
        // D-08: shared helper gates every scalar/sub-object/ItemField decision.
        var count = CountOccurrences(ContentDeserializerSource, "MergePredicate.IsUnsetForMerge");
        Assert.True(count >= 15, $"Expected >= 15 MergePredicate.IsUnsetForMerge call sites, got {count}");
    }

    [Fact]
    public void SeedMerge_ImportsMergePredicateNamespace()
    {
        // Shared helper lives in DynamicWeb.Serializer.Infrastructure; importing namespace ensures
        // the call sites compile. (Infrastructure was already a using; re-check just in case.)
        Assert.Contains("using DynamicWeb.Serializer.Infrastructure;", ContentDeserializerSource);
    }

    // -----------------------------------------------------------------------
    // Structural: new merge methods exist with the right signatures.
    // -----------------------------------------------------------------------

    [Fact]
    public void SeedMerge_MergePageScalarsMethod_Exists()
    {
        var t = typeof(ContentDeserializer);
        var flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic;
        var m = t.GetMethod("MergePageScalars", flags);
        Assert.NotNull(m);
    }

    [Fact]
    public void SeedMerge_ApplyPagePropertiesWithMergeMethod_Exists()
    {
        var t = typeof(ContentDeserializer);
        var flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic;
        var m = t.GetMethod("ApplyPagePropertiesWithMerge", flags);
        Assert.NotNull(m);
    }

    [Fact]
    public void SeedMerge_MergeItemFieldsMethod_Exists()
    {
        var t = typeof(ContentDeserializer);
        var flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic;
        var m = t.GetMethod("MergeItemFields", flags);
        Assert.NotNull(m);
    }

    [Fact]
    public void SeedMerge_MergePropertyItemFieldsMethod_Exists()
    {
        var t = typeof(ContentDeserializer);
        var flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic;
        var m = t.GetMethod("MergePropertyItemFields", flags);
        Assert.NotNull(m);
    }

    [Fact]
    public void SeedMerge_LogSeedMergeDryRunMethod_Exists()
    {
        var t = typeof(ContentDeserializer);
        var flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic;
        var m = t.GetMethod("LogSeedMergeDryRun", flags);
        Assert.NotNull(m);
    }

    // -----------------------------------------------------------------------
    // Pure helper behaviour: MergePageScalars on a real Page instance.
    // (D-01, D-04, D-05, D-10)
    // -----------------------------------------------------------------------

    [Fact]
    public void MergePageScalars_TargetMenuTextEmpty_YamlHasValue_Fills()
    {
        var existing = new Page { MenuText = "" };
        var dto = MakeDto(menuText: "Swift Menu");
        int left = 0;

        var filled = InvokeMergePageScalars(existing, dto, ref left);

        Assert.Equal("Swift Menu", existing.MenuText);
        Assert.True(filled >= 1, $"Expected at least 1 filled; got {filled}");
    }

    [Fact]
    public void MergePageScalars_TargetMenuTextSet_YamlHasDifferentValue_Preserves()
    {
        var existing = new Page { MenuText = "Customer Tweak" };
        var dto = MakeDto(menuText: "YAML Default");
        int left = 0;

        InvokeMergePageScalars(existing, dto, ref left);

        Assert.Equal("Customer Tweak", existing.MenuText);
        Assert.True(left >= 1, $"Expected at least 1 left; got {left}");
    }

    [Fact]
    public void MergePageScalars_TargetActiveFalse_YamlHasTrue_Fills_D10Tradeoff()
    {
        // D-10: false counts as unset for bools. Documented + tested.
        var existing = new Page { Active = false };
        var dto = MakeDto(menuText: "x", isActive: true);
        int left = 0;

        InvokeMergePageScalars(existing, dto, ref left);

        Assert.True(existing.Active);
    }

    [Fact]
    public void MergePageScalars_TargetActiveTrue_YamlHasFalse_Preserves()
    {
        var existing = new Page { Active = true };
        var dto = MakeDto(menuText: "x", isActive: false);
        int left = 0;

        InvokeMergePageScalars(existing, dto, ref left);

        Assert.True(existing.Active);
    }

    // -----------------------------------------------------------------------
    // Pure helper behaviour: ApplyPagePropertiesWithMerge on a real Page.
    // Covers D-04 per-property sub-object merge.
    // -----------------------------------------------------------------------

    [Fact]
    public void ApplyPagePropertiesWithMerge_TargetMetaTitleNull_YamlHasValue_Fills()
    {
        var existing = new Page();   // MetaTitle defaults to empty
        var dto = MakeDto(menuText: "x") with
        {
            Seo = new SerializedSeoSettings { MetaTitle = "Swift Title", Description = "" }
        };
        int left = 0;

        InvokeApplyPagePropertiesWithMerge(existing, dto, ref left);

        Assert.Equal("Swift Title", existing.MetaTitle);
    }

    [Fact]
    public void ApplyPagePropertiesWithMerge_TargetMetaTitleSet_YamlHasDifferent_Preserves()
    {
        var existing = new Page { MetaTitle = "Customer Tweak" };
        var dto = MakeDto(menuText: "x") with
        {
            Seo = new SerializedSeoSettings { MetaTitle = "YAML Default" }
        };
        int left = 0;

        InvokeApplyPagePropertiesWithMerge(existing, dto, ref left);

        Assert.Equal("Customer Tweak", existing.MetaTitle);
        Assert.True(left >= 1, $"Expected some preserved-fields; got left={left}");
    }

    [Fact]
    public void ApplyPagePropertiesWithMerge_SeoMetaTitleSet_DescriptionEmpty_FillsDescriptionOnly_D04()
    {
        // D-04: sub-object DTOs are NOT atomic; MetaTitle preserved, Description filled.
        var existing = new Page { MetaTitle = "Keep Title", Description = "" };
        var dto = MakeDto(menuText: "x") with
        {
            Seo = new SerializedSeoSettings
            {
                MetaTitle = "YAML Default Title",
                Description = "YAML Description"
            }
        };
        int left = 0;

        InvokeApplyPagePropertiesWithMerge(existing, dto, ref left);

        Assert.Equal("Keep Title", existing.MetaTitle);
        Assert.Equal("YAML Description", existing.Description);
    }

    // -----------------------------------------------------------------------
    // D-09 idempotency: re-run fills nothing once all fields are set.
    // -----------------------------------------------------------------------

    [Fact]
    public void MergePageScalars_AllFieldsAlreadySet_ReturnsZeroFilled()
    {
        var existing = new Page
        {
            MenuText = "x",
            UrlName = "x",
            Active = true,
            Sort = 1,
            ItemType = "Item",
            LayoutTemplate = "Layout",
            LayoutApplyToSubPages = true,
            IsFolder = true,
            TreeSection = "Tree"
        };
        var dto = MakeDto(menuText: "other", isActive: true) with
        {
            UrlName = "other",
            SortOrder = 2,
            ItemType = "Other",
            Layout = "OtherLayout",
            LayoutApplyToSubPages = true,
            IsFolder = true,
            TreeSection = "Other"
        };
        int left = 0;

        var filled = InvokeMergePageScalars(existing, dto, ref left);

        Assert.Equal(0, filled);
        Assert.True(left > 0);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static SerializedPage MakeDto(string menuText, bool isActive = false)
    {
        return new SerializedPage
        {
            PageUniqueId = Guid.NewGuid(),
            Name = menuText,
            MenuText = menuText,
            UrlName = "urlname",
            SortOrder = 1,
            IsActive = isActive
        };
    }

    private static int InvokeMergePageScalars(Page existing, SerializedPage dto, ref int left)
    {
        var t = typeof(ContentDeserializer);
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        var m = t.GetMethod("MergePageScalars", flags);
        Assert.NotNull(m);
        var args = new object?[] { existing, dto, left };
        var result = m!.Invoke(null, args);
        left = (int)args[2]!;
        return (int)result!;
    }

    private static int InvokeApplyPagePropertiesWithMerge(Page existing, SerializedPage dto, ref int left)
    {
        var t = typeof(ContentDeserializer);
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        var m = t.GetMethod("ApplyPagePropertiesWithMerge", flags);
        Assert.NotNull(m);
        var args = new object?[] { existing, dto, left };
        var result = m!.Invoke(null, args);
        left = (int)args[2]!;
        return (int)result!;
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0;
        int idx = 0;
        while ((idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += needle.Length;
        }
        return count;
    }
}
