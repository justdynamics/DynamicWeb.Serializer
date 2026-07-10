using Truvio.Commerce.Serializer.Models;
using Truvio.Commerce.Serializer.Serialization;
using Xunit;

namespace Truvio.Commerce.Serializer.Tests.Serialization;

/// <summary>
/// Covers the deferred-permissions ledger that closes the groups-after-content ordering trap:
/// a tile/page permission referencing a group not yet on target is recorded here at content-write
/// time and re-applied at end-of-run once the group-creating predicate has executed. The DW-bound
/// re-apply itself (PermissionService/UserGroups) is proven by the live Wave 0.3 E2E; these tests
/// pin the persistence contract the trap fix depends on.
/// </summary>
public class DeferredPermissionLedgerTests : IDisposable
{
    private readonly string _modeRoot;

    public DeferredPermissionLedgerTests()
    {
        _modeRoot = Path.Combine(Path.GetTempPath(), "dpl-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_modeRoot);
    }

    public void Dispose()
    {
        try { Directory.Delete(_modeRoot, recursive: true); } catch { /* best-effort cleanup */ }
    }

    private static DeferredPermissionRecord SampleRecord(int entityId = 42, string entityName = "Paragraph")
        => new(entityId, entityName, new List<SerializedPermission>
        {
            new() { Owner = "Customers", OwnerType = "group", OwnerId = "1325", Level = "all", LevelValue = 1364 },
            new() { Owner = "CSR",       OwnerType = "group", OwnerId = "1292", Level = "none", LevelValue = 1 },
            new() { Owner = "Anonymous", OwnerType = "role",  Level = "none", LevelValue = 1 },
        });

    [Fact]
    public void Read_MissingFile_ReturnsEmpty()
    {
        Assert.Empty(DeferredPermissionLedger.Read(_modeRoot));
    }

    [Fact]
    public void Append_ThenRead_RoundTripsEntityAndFullList()
    {
        DeferredPermissionLedger.Append(_modeRoot, new[] { SampleRecord() });

        var read = DeferredPermissionLedger.Read(_modeRoot);
        var rec = Assert.Single(read);
        Assert.Equal(42, rec.EntityId);
        Assert.Equal("Paragraph", rec.EntityName);
        // The WHOLE intended list is recorded (not just the unresolvable entries) so the
        // end-of-run re-apply converges on the intended state via the clear-loop.
        Assert.Equal(3, rec.Permissions.Count);
        Assert.Equal("Customers", rec.Permissions[0].Owner);
        Assert.Equal("all", rec.Permissions[0].Level);
        Assert.Equal("Anonymous", rec.Permissions[2].Owner);
        Assert.Equal("role", rec.Permissions[2].OwnerType);
    }

    [Fact]
    public void Append_PreservesSubNameScope()
    {
        var scoped = new DeferredPermissionRecord(7, "Page", new List<SerializedPermission>
        {
            new() { Owner = "Marketing", OwnerType = "group", SubName = "Paragraph", Level = "none", LevelValue = 1 },
        });
        DeferredPermissionLedger.Append(_modeRoot, new[] { scoped });

        var rec = Assert.Single(DeferredPermissionLedger.Read(_modeRoot));
        Assert.Equal("Paragraph", rec.Permissions[0].SubName);
    }

    [Fact]
    public void Append_Accumulates_AcrossMultipleCalls()
    {
        DeferredPermissionLedger.Append(_modeRoot, new[] { SampleRecord(1, "Paragraph") });
        DeferredPermissionLedger.Append(_modeRoot, new[] { SampleRecord(2, "GridRow") });

        var read = DeferredPermissionLedger.Read(_modeRoot);
        Assert.Equal(2, read.Count);
        Assert.Contains(read, r => r.EntityId == 1 && r.EntityName == "Paragraph");
        Assert.Contains(read, r => r.EntityId == 2 && r.EntityName == "GridRow");
    }

    [Fact]
    public void Append_EmptyList_WritesNoFile()
    {
        DeferredPermissionLedger.Append(_modeRoot, Array.Empty<DeferredPermissionRecord>());
        Assert.False(File.Exists(Path.Combine(_modeRoot, DeferredPermissionLedger.FileName)));
    }

    [Fact]
    public void Delete_RemovesLedgerFile()
    {
        DeferredPermissionLedger.Append(_modeRoot, new[] { SampleRecord() });
        Assert.True(File.Exists(Path.Combine(_modeRoot, DeferredPermissionLedger.FileName)));

        DeferredPermissionLedger.Delete(_modeRoot);
        Assert.False(File.Exists(Path.Combine(_modeRoot, DeferredPermissionLedger.FileName)));
        Assert.Empty(DeferredPermissionLedger.Read(_modeRoot));
    }

    [Fact]
    public void Finalize_EmptyLedger_IsNoOp_DoesNotThrow()
    {
        // No file present — the common case (groups pre-seeded, or no group permissions).
        // Must not touch DW services and must not throw.
        var ex = Record.Exception(() => DeferredPermissionLedger.Finalize(_modeRoot, log: null));
        Assert.Null(ex);
    }
}
