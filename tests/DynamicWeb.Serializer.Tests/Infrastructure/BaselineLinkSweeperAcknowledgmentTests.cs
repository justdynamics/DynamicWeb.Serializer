using DynamicWeb.Serializer.Configuration;
using DynamicWeb.Serializer.Infrastructure;
using DynamicWeb.Serializer.Models;
using Xunit;

namespace DynamicWeb.Serializer.Tests.Infrastructure;

/// <summary>
/// Phase 38 A.1 (D-38-04): retroactive tests locking in the per-predicate
/// AcknowledgedOrphanPageIds bypass introduced in commit 7496fe2 and consolidated
/// under Task 1 of this plan (A.3 / D-38-03).
///
/// Tests drive the <see cref="BaselineLinkSweeper"/> directly and compose with the
/// post-A.3 filter the <see cref="ContentSerializer"/> now uses (Phase 40 D-01 flat shape):
///   <c>HashSet&lt;int&gt;(config.Predicates.Where(p =&gt; p.Mode == DeploymentMode.Deploy)
///                              .SelectMany(p =&gt; p.AcknowledgedOrphanPageIds))</c>.
/// This isolates the unit under test from needing a full ContentSerializer.Serialize
/// end-to-end (which would require IContentStore fake + DW context).
///
/// Threat anchor: T-38-02 (malicious-ID via ack list) — an unlisted orphan MUST
/// still fail even when another orphan is acknowledged on the same predicate.
/// The ack list is surgical, not a sweep-disable toggle.
/// </summary>
[Trait("Category", "Phase38")]
public class BaselineLinkSweeperAcknowledgmentTests
{
    private static SerializedPage MakePageWithShortcutToId(int sourceId, int shortcutTargetId)
    {
        return new SerializedPage
        {
            PageUniqueId = Guid.NewGuid(),
            SourcePageId = sourceId,
            Name = $"P{sourceId}",
            MenuText = $"P{sourceId}",
            UrlName = $"p{sourceId}",
            SortOrder = 1,
            ShortCut = $"Default.aspx?ID={shortcutTargetId}",
            Fields = new Dictionary<string, object>(),
            PropertyFields = new Dictionary<string, object>(),
            GridRows = new List<SerializedGridRow>(),
            Children = new List<SerializedPage>()
        };
    }

    // Phase 40 D-01 / D-02: flat shape — Predicates is a single list with explicit per-item Mode.
    // The original fixture put a Deploy-mode Content predicate (carrying the ack list) and zero Seed
    // predicates. The unit-under-test composition is preserved: `config.Predicates.Where(p =>
    // p.Mode == DeploymentMode.Deploy).SelectMany(p => p.AcknowledgedOrphanPageIds)` reproduces
    // the legacy section-level Deploy.Predicates.SelectMany(...) semantics exactly.
    private static SerializerConfiguration ConfigWithPredicateAck(List<int> ackList)
    {
        return new SerializerConfiguration
        {
            OutputDirectory = "X",
            Predicates = new List<ProviderPredicateDefinition>
            {
                new() {
                    Name = "Content",
                    Mode = DeploymentMode.Deploy,
                    ProviderType = "Content",
                    AreaId = 1,
                    Path = "/",
                    AcknowledgedOrphanPageIds = ackList
                }
            }
        };
    }

    [Fact]
    public void Sweep_UnacknowledgedOrphanId_Throws()
    {
        // No ack list. Tree has a ShortCut to 9999 but no page with SourcePageId=9999.
        // Phase 37-05 sweep must fail serialize.
        var page = MakePageWithShortcutToId(sourceId: 100, shortcutTargetId: 9999);
        var pages = new List<SerializedPage> { page };
        var config = ConfigWithPredicateAck(new List<int>()); // empty

        var sweeper = new BaselineLinkSweeper();
        var sweepResult = sweeper.Sweep(pages);

        // Direct sweep result assertion — unresolved includes 9999.
        Assert.Single(sweepResult.Unresolved);
        Assert.Equal(9999, sweepResult.Unresolved[0].UnresolvablePageId);

        // Compose with the ContentSerializer filter logic (post-A.3): verify
        // that an empty ack list does not filter 9999 out, so the serializer
        // would throw on this sweep result.
        // Phase 40 D-01: flat list — filter by Mode to preserve the legacy Deploy-only composition.
        var ack = new HashSet<int>(
            config.Predicates.Where(p => p.Mode == DeploymentMode.Deploy)
                             .SelectMany(p => p.AcknowledgedOrphanPageIds));
        var fatal = sweepResult.Unresolved.Where(u => !ack.Contains(u.UnresolvablePageId)).ToList();
        Assert.Single(fatal); // fails serialize
    }

    [Fact]
    public void Sweep_AcknowledgedOrphanId_IsFilteredFromFatal()
    {
        // Ack list = [15717]. Tree has a ShortCut to 15717 but no page with SourcePageId=15717.
        // Sweep still reports it as unresolved, but the A.3 filter moves it to
        // the "acknowledged orphan" bucket (warning, not fatal).
        var page = MakePageWithShortcutToId(sourceId: 100, shortcutTargetId: 15717);
        var pages = new List<SerializedPage> { page };
        var config = ConfigWithPredicateAck(new List<int> { 15717 });

        var sweepResult = new BaselineLinkSweeper().Sweep(pages);
        Assert.Single(sweepResult.Unresolved);

        // Phase 40 D-01: flat list — filter by Mode to preserve the legacy Deploy-only composition.
        var ack = new HashSet<int>(
            config.Predicates.Where(p => p.Mode == DeploymentMode.Deploy)
                             .SelectMany(p => p.AcknowledgedOrphanPageIds));
        var fatal = sweepResult.Unresolved.Where(u => !ack.Contains(u.UnresolvablePageId)).ToList();
        Assert.Empty(fatal); // no fatal unresolved → serialize succeeds

        // Acknowledged bucket contains the one 15717.
        var acknowledged = sweepResult.Unresolved.Where(u => ack.Contains(u.UnresolvablePageId)).ToList();
        Assert.Single(acknowledged);
        Assert.Equal(15717, acknowledged[0].UnresolvablePageId);
    }

    [Fact]
    public void Sweep_UnlistedOrphanId_Throws_EvenWhenOtherAcknowledged()
    {
        // Threat T-38-02: ack = [15717], tree has refs to BOTH 15717 AND 9999.
        // The 9999 (unlisted) must still cause serialize to fail — the ack list
        // is surgical, not a sweep disable.
        var page1 = MakePageWithShortcutToId(sourceId: 100, shortcutTargetId: 15717);
        var page2 = MakePageWithShortcutToId(sourceId: 200, shortcutTargetId: 9999);
        var pages = new List<SerializedPage> { page1, page2 };
        var config = ConfigWithPredicateAck(new List<int> { 15717 });

        var sweepResult = new BaselineLinkSweeper().Sweep(pages);
        Assert.Equal(2, sweepResult.Unresolved.Count);

        // Phase 40 D-01: flat list — filter by Mode to preserve the legacy Deploy-only composition.
        var ack = new HashSet<int>(
            config.Predicates.Where(p => p.Mode == DeploymentMode.Deploy)
                             .SelectMany(p => p.AcknowledgedOrphanPageIds));
        var fatal = sweepResult.Unresolved.Where(u => !ack.Contains(u.UnresolvablePageId)).ToList();
        Assert.Single(fatal);
        Assert.Equal(9999, fatal[0].UnresolvablePageId);
    }
}
