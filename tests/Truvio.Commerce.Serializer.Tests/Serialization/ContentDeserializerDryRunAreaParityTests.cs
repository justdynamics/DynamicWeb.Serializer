using Truvio.Commerce.Serializer.Serialization;
using Xunit;

namespace Truvio.Commerce.Serializer.Tests.Serialization;

/// <summary>
/// AREA-04 dry-run parity (Foundry #563).
///
/// <para>
/// The dry run auto-created nothing and skipped a Content entry whose target area was absent,
/// logging <c>Warning: Area with ID {n} not found. Skipping entry</c>. Strict mode escalates
/// that warning, so a dry run used as a gate's go/no-go signal returned HTTP 400 on content the
/// real run deserialized cleanly: the real branch was guarded by <c>!_isDryRun</c> while the
/// skip branch was not.
/// </para>
///
/// <para>
/// The invariant these tests pin is not "the dry run creates areas" — it writes nothing. It is
/// that the dry run REPORTS the real run's outcome: it simulates exactly when the real run
/// creates, and skips exactly when the real run cannot create either. Both answers come from
/// <see cref="ContentDeserializer.ResolveMissingAreaAction"/>, which is why they cannot drift
/// apart again.
/// </para>
/// </summary>
[Trait("Category", "AREA-04")]
public class ContentDeserializerDryRunAreaParityTests
{
    [Fact]
    public void RealRun_WithSerializedAreaProperties_Creates()
    {
        Assert.Equal(
            MissingAreaAction.Create,
            ContentDeserializer.ResolveMissingAreaAction(isDryRun: false, areaPropertyCount: 12));
    }

    [Fact]
    public void DryRun_WithSerializedAreaProperties_SimulatesInsteadOfSkipping()
    {
        // The regression: this used to fall through to the skip branch, and strict mode
        // escalated the resulting warning into a failed dry run.
        Assert.Equal(
            MissingAreaAction.SimulateCreate,
            ContentDeserializer.ResolveMissingAreaAction(isDryRun: true, areaPropertyCount: 12));
    }

    [Fact]
    public void RealRun_WithNoAreaProperties_Skips()
    {
        // Nothing to create the area FROM, so the real run skips too.
        Assert.Equal(
            MissingAreaAction.Skip,
            ContentDeserializer.ResolveMissingAreaAction(isDryRun: false, areaPropertyCount: 0));
    }

    [Fact]
    public void DryRun_WithNoAreaProperties_Skips()
    {
        // Parity cuts both ways: where the real run cannot create, the dry run must not
        // pretend it would. Reporting a create here would be the mirror-image defect.
        Assert.Equal(
            MissingAreaAction.Skip,
            ContentDeserializer.ResolveMissingAreaAction(isDryRun: true, areaPropertyCount: 0));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(19)]
    [InlineData(43)]
    public void DryRunSkipsExactlyWhenRealRunSkips(int areaPropertyCount)
    {
        var real = ContentDeserializer.ResolveMissingAreaAction(isDryRun: false, areaPropertyCount);
        var dry = ContentDeserializer.ResolveMissingAreaAction(isDryRun: true, areaPropertyCount);

        Assert.Equal(real == MissingAreaAction.Skip, dry == MissingAreaAction.Skip);
    }

    [Fact]
    public void DryRunNeverAsksForARealCreate()
    {
        // A dry run writes nothing, so Create is the one answer it must never return.
        foreach (var count in new[] { 0, 1, 2, 100 })
        {
            Assert.NotEqual(
                MissingAreaAction.Create,
                ContentDeserializer.ResolveMissingAreaAction(isDryRun: true, count));
        }
    }

    [Fact]
    public void RealRunNeverSimulates()
    {
        foreach (var count in new[] { 0, 1, 2, 100 })
        {
            Assert.NotEqual(
                MissingAreaAction.SimulateCreate,
                ContentDeserializer.ResolveMissingAreaAction(isDryRun: false, count));
        }
    }

    [Fact]
    public void NegativePropertyCount_Skips()
    {
        // Defensive: a negative count is not a create signal in either run.
        Assert.Equal(
            MissingAreaAction.Skip,
            ContentDeserializer.ResolveMissingAreaAction(isDryRun: false, areaPropertyCount: -1));
        Assert.Equal(
            MissingAreaAction.Skip,
            ContentDeserializer.ResolveMissingAreaAction(isDryRun: true, areaPropertyCount: -1));
    }
}
