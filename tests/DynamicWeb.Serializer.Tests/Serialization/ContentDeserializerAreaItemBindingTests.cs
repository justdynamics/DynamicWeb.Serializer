using Xunit;

namespace DynamicWeb.Serializer.Tests.Serialization;

/// <summary>
/// Regression coverage for the greenfield Area-Item binding bug.
///
/// When deserializing into a target where the Area row does not yet exist (or has
/// no Item row), <c>ContentDeserializer</c> creates the Item and writes back
/// <c>targetArea.ItemId</c> — but until this fix, it never wrote
/// <c>targetArea.ItemType</c>. The Area row ended up with <c>AreaItemId</c> set
/// but <c>AreaItemType</c> blank, so DW could not bind the Area to its Master
/// Item, and the downstream <c>ResolveLinksInArea</c> guard
/// (<c>!string.IsNullOrEmpty(targetArea.ItemType)</c>) silently skipped link
/// remapping for area-level item fields (HeaderDesktop / Footer / Favicon, etc.).
///
/// The bug never surfaced in CI/E2E because all round-trip test targets had
/// pre-existing Area rows with <c>AreaItemType</c> already populated from a
/// prior install — the Item-creation branch was never exercised for a fresh
/// Area. First observed live on 2026-04-23 deserializing the DAP into a
/// greenfield pim.carriageservices DB.
/// </summary>
public class ContentDeserializerAreaItemBindingTests
{
    private static readonly string Source = File.ReadAllText(
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

    [Fact]
    public void AreaItem_Binding_Sets_BothItemIdAndItemType_BeforeSaveArea()
    {
        // Both assignments must appear adjacent and before the SaveArea call inside
        // the new-Item branch. If a refactor splits or reorders them and forgets
        // ItemType, the symptom (blank AreaItemType, no header/footer rendering) returns.
        var idx = Source.IndexOf("targetArea.ItemId = targetAreaItemId;", StringComparison.Ordinal);
        Assert.True(idx > 0, "Expected 'targetArea.ItemId = targetAreaItemId;' assignment in ContentDeserializer.");

        var window = Source.Substring(idx, Math.Min(400, Source.Length - idx));
        Assert.Contains("targetArea.ItemType = area.ItemType;", window);

        var saveIdx = window.IndexOf("Services.Areas.SaveArea(targetArea);", StringComparison.Ordinal);
        var typeIdx = window.IndexOf("targetArea.ItemType = area.ItemType;", StringComparison.Ordinal);
        Assert.True(saveIdx > typeIdx, "ItemType assignment must precede SaveArea call.");
    }

    [Fact]
    public void AreaItem_Binding_HasRepairBranch_ForExistingItemWithStaleType()
    {
        // Targets the idempotency case: a previous (pre-fix) deserialize left
        // AreaItemId set but AreaItemType blank. On re-deserialize, the
        // create-Item branch is skipped (Item already exists), so a separate
        // repair branch must update AreaItemType to match the YAML.
        Assert.Contains("Repaired area binding:", Source);
        Assert.Contains("else if (targetArea.ItemType != area.ItemType)", Source);
    }
}
