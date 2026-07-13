using Truvio.Commerce.Serializer.Serialization;
using Xunit;

namespace Truvio.Commerce.Serializer.Tests.Serialization;

/// <summary>
/// LRN-hosted-publish-05: derive-on-save repair decision logic. LogoWidth is the canary —
/// authored "200" overwritten by the platform to the image's natural width must be flagged
/// for repair; a correctly-persisted value must not.
/// </summary>
public class DerivedFieldRepairTests
{
    [Fact]
    public void Compute_DerivedFieldOverwritten_FlaggedForRepair()
    {
        var authored = new Dictionary<string, object?> { ["LogoWidth"] = "200" };
        var persisted = new Dictionary<string, object?> { ["LogoWidth"] = 1405 };

        var repairs = DerivedFieldRepair.Compute(authored, persisted);

        Assert.True(repairs.ContainsKey("LogoWidth"));
        Assert.Equal("200", repairs["LogoWidth"]);
    }

    [Fact]
    public void Compute_ValuePersistedCorrectly_NoRepair()
    {
        // "200" authored, persisted as int 200 — string-normalized equal, so no repair.
        var authored = new Dictionary<string, object?> { ["LogoWidth"] = "200" };
        var persisted = new Dictionary<string, object?> { ["LogoWidth"] = 200 };

        var repairs = DerivedFieldRepair.Compute(authored, persisted);

        Assert.Empty(repairs);
    }

    [Fact]
    public void Compute_EmptyAuthoredValue_NeverRepaired()
    {
        var authored = new Dictionary<string, object?>
        {
            ["Blank"] = "",
            ["WhitespaceOnly"] = "   ",
            ["Null"] = null
        };
        var persisted = new Dictionary<string, object?>
        {
            ["Blank"] = "computed",
            ["WhitespaceOnly"] = "computed",
            ["Null"] = "computed"
        };

        var repairs = DerivedFieldRepair.Compute(authored, persisted);

        Assert.Empty(repairs);
    }

    [Fact]
    public void Compute_PersistedMissingEntirely_NonEmptyAuthored_Repaired()
    {
        var authored = new Dictionary<string, object?> { ["LogoWidth"] = "200" };
        var persisted = new Dictionary<string, object?>(); // field did not persist at all

        var repairs = DerivedFieldRepair.Compute(authored, persisted);

        Assert.True(repairs.ContainsKey("LogoWidth"));
    }

    [Fact]
    public void Compute_MultipleFields_OnlyDivergentNonEmptyRepaired()
    {
        var authored = new Dictionary<string, object?>
        {
            ["LogoWidth"] = "200",     // overwritten -> repair
            ["Title"] = "Acme",        // stable -> keep
            ["Caption"] = ""           // empty -> ignore
        };
        var persisted = new Dictionary<string, object?>
        {
            ["LogoWidth"] = "1405",
            ["Title"] = "Acme",
            ["Caption"] = "auto"
        };

        var repairs = DerivedFieldRepair.Compute(authored, persisted);

        Assert.Single(repairs);
        Assert.True(repairs.ContainsKey("LogoWidth"));
    }

    [Fact]
    public void Compute_TrimmedAndCaseInsensitiveMatch_NoRepair()
    {
        var authored = new Dictionary<string, object?> { ["Flag"] = "true" };
        var persisted = new Dictionary<string, object?> { ["Flag"] = "  True  " };

        var repairs = DerivedFieldRepair.Compute(authored, persisted);

        Assert.Empty(repairs);
    }
}
