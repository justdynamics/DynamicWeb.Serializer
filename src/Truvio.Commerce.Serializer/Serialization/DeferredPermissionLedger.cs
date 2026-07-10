using System.Text.Json;
using Truvio.Commerce.Serializer.Models;

namespace Truvio.Commerce.Serializer.Serialization;

/// <summary>
/// One deferred permission application: the full intended permission list for a content
/// entity (page, grid row, or paragraph) whose set could not be applied cleanly at
/// content-write time because at least one referenced group did not yet exist on target.
/// </summary>
public sealed record DeferredPermissionRecord(
    int EntityId,
    string EntityName,
    List<SerializedPermission> Permissions);

/// <summary>
/// Persistence + finalization for cross-entry deferred permissions — the permission analogue
/// of <see cref="DeferredLinkLedger"/>.
///
/// The ordering trap: content pages/rows/paragraphs are deserialized BEFORE the SqlTable
/// predicate that creates the customer user groups (LINK-02 forces Content first whenever any
/// SqlTable predicate opts into link resolution — the base's <c>UrlPath</c> predicate does).
/// A tile permission referencing a group ("Customers", "CSR") therefore resolves against a
/// target where that group does not exist yet: the grant is skipped and an <c>Anonymous=None</c>
/// safety deny is written instead. There is no other re-apply pass, so the intended per-role
/// visibility would be lost on a clean deserialize.
///
/// When <see cref="PermissionMapper.ApplyPermissions"/> hits an unresolvable group it records the
/// entity's WHOLE intended permission list here (keeping the interim Anonymous=None deny as the
/// safe state for the remainder of the run). At end-of-run — after every entry, including the
/// group-creating SqlTable predicate, has executed — <see cref="Finalize"/> re-applies each
/// recorded list with a FRESH group cache. Re-running the full list (not just the previously
/// unresolvable entries) makes the clear-loop wipe the interim fallback and converge on exactly
/// the state a second clean deserialize would produce (LRN-rowperm-03).
///
/// The ledger file (<c>deferred-permissions.json</c>) lives in the mode root next to the
/// manifest, mirroring <see cref="DeferredLinkLedger"/>'s modeRoot placement so the Append site
/// (ContentDeserializer) and the Finalize site (SerializerOrchestrator) agree on the path.
/// </summary>
public static class DeferredPermissionLedger
{
    public const string FileName = "deferred-permissions.json";
    private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };

    public static void Append(string modeRoot, IReadOnlyList<DeferredPermissionRecord> records)
    {
        if (records.Count == 0) return;
        var path = Path.Combine(modeRoot, FileName);
        var all = Read(modeRoot);
        all.AddRange(records);
        File.WriteAllText(path, JsonSerializer.Serialize(all, _json));
    }

    public static List<DeferredPermissionRecord> Read(string modeRoot)
    {
        var path = Path.Combine(modeRoot, FileName);
        if (!File.Exists(path)) return new List<DeferredPermissionRecord>();
        try
        {
            return JsonSerializer.Deserialize<List<DeferredPermissionRecord>>(File.ReadAllText(path))
                   ?? new List<DeferredPermissionRecord>();
        }
        catch { return new List<DeferredPermissionRecord>(); }
    }

    public static void Delete(string modeRoot)
    {
        var path = Path.Combine(modeRoot, FileName);
        if (File.Exists(path)) File.Delete(path);
    }

    /// <summary>
    /// Re-applies every recorded entity's permission list against a FRESH group cache — by which
    /// point the group-creating predicates have run — then deletes the ledger. A fresh
    /// <see cref="PermissionMapper"/> is used deliberately: the mapper caches the group-name→id
    /// map on first apply, so a mapper instance from content-write time is frozen without the
    /// late-created groups. An entity whose group is STILL unresolvable (e.g. a consumer that
    /// never ships the group) keeps the conservative Anonymous=None deny and logs a warning —
    /// no re-defer, no loop.
    /// </summary>
    public static void Finalize(string modeRoot, Action<string>? log)
    {
        var records = Read(modeRoot);
        if (records.Count == 0) return;

        var mapper = new PermissionMapper(log);
        int reapplied = 0;
        foreach (var record in records)
        {
            try
            {
                mapper.ApplyPermissions(record.EntityId, record.EntityName, record.Permissions);
                reapplied++;
            }
            catch (Exception ex)
            {
                log?.Invoke($"WARNING: deferred permission re-apply failed for {record.EntityName} {record.EntityId}: {ex.Message}");
            }
        }
        log?.Invoke($"Deferred permission finalization ({Path.GetFileName(modeRoot)}): {reapplied}/{records.Count} entity permission set(s) re-applied after group creation.");
        Delete(modeRoot);
    }
}
