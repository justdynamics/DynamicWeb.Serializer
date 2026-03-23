using Dynamicweb.ContentSync.Models;
using Dynamicweb.Security.Permissions;
using Dynamicweb.Security.UserManagement;

namespace Dynamicweb.ContentSync.Serialization;

/// <summary>
/// Maps DynamicWeb page permissions to/from SerializedPermission DTOs.
/// Serialization: resolves role names directly and group names via AccessUser lookup.
/// Deserialization: restores permissions by role name, resolves groups by name on target,
/// and applies a safety fallback (Anonymous=None) when groups are missing.
/// </summary>
public class PermissionMapper
{
    private static readonly HashSet<string> RoleIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "Anonymous",
        "AuthenticatedBackend",
        "AuthenticatedFrontend",
        "Administrator"
    };

    private readonly Action<string>? _log;
    private Dictionary<string, int>? _groupNameCache;

    public PermissionMapper(Action<string>? log = null)
    {
        _log = log;
    }

    private void Log(string message) => _log?.Invoke(message);

    /// <summary>
    /// Returns true if the given ownerId is one of the 4 built-in DW roles.
    /// </summary>
    public static bool IsRole(string ownerId) => RoleIds.Contains(ownerId);

    /// <summary>
    /// Converts a PermissionLevel enum value to a human-readable name.
    /// </summary>
    public static string GetLevelName(PermissionLevel level) => level switch
    {
        PermissionLevel.None => "none",
        PermissionLevel.Read => "read",
        PermissionLevel.Edit => "edit",
        PermissionLevel.Create => "create",
        PermissionLevel.Delete => "delete",
        PermissionLevel.All => "all",
        _ => level.ToString().ToLowerInvariant()
    };

    /// <summary>
    /// Reverses GetLevelName: parses a human-readable level name back to PermissionLevel.
    /// Throws ArgumentException for unrecognized names (caller should fall back to LevelValue).
    /// </summary>
    public static PermissionLevel ParseLevelName(string levelName) => levelName.ToLowerInvariant() switch
    {
        "none" => PermissionLevel.None,
        "read" => PermissionLevel.Read,
        "edit" => PermissionLevel.Edit,
        "create" => PermissionLevel.Create,
        "delete" => PermissionLevel.Delete,
        "all" => PermissionLevel.All,
        _ => throw new ArgumentException($"Unknown permission level name: '{levelName}'", nameof(levelName))
    };

    // -------------------------------------------------------------------------
    // Deserialization — apply permissions from YAML to target page
    // -------------------------------------------------------------------------

    /// <summary>
    /// Restores permissions on a page from serialized data.
    /// Roles are matched by name directly. Groups are resolved by name on the target.
    /// If any group is unresolvable, Anonymous is set to None as a safety fallback.
    /// </summary>
    public void ApplyPermissions(int pageId, List<SerializedPermission> permissions)
    {
        if (permissions == null || permissions.Count == 0)
            return;

        var permissionService = new PermissionService();
        var identifier = new PermissionEntityIdentifier(pageId.ToString(), "Page");

        // Clear existing explicit permissions (source-wins model)
        var query = new PermissionQuery { Key = pageId.ToString(), Name = "Page" };
        var existing = permissionService.GetPermissionsByQuery(query);
        foreach (var perm in existing)
        {
            permissionService.SetPermission(perm.OwnerId, identifier, PermissionLevel.None);
        }

        // Build group name cache lazily (reused across pages in same run)
        var groupCache = BuildGroupNameCache();

        bool anyGroupUnresolvable = false;
        string? lastUnresolvableGroup = null;

        foreach (var perm in permissions)
        {
            // Determine permission level: try name first, fall back to LevelValue
            PermissionLevel level;
            try
            {
                level = ParseLevelName(perm.Level);
            }
            catch (ArgumentException)
            {
                level = (PermissionLevel)perm.LevelValue;
            }

            if (string.Equals(perm.OwnerType, "role", StringComparison.OrdinalIgnoreCase))
            {
                permissionService.SetPermission(perm.Owner, identifier, level);
                Log($"Applied {perm.Owner} = {perm.Level} on page {pageId}");
            }
            else if (string.Equals(perm.OwnerType, "group", StringComparison.OrdinalIgnoreCase))
            {
                if (groupCache.TryGetValue(perm.Owner, out var groupId))
                {
                    permissionService.SetPermission(groupId.ToString(), identifier, level);
                    Log($"Applied {perm.Owner} (group ID={groupId}) = {perm.Level} on page {pageId}");
                }
                else
                {
                    Log($"Skipped permission for '{perm.Owner}' -- group not found on target");
                    anyGroupUnresolvable = true;
                    lastUnresolvableGroup = perm.Owner;
                }
            }
        }

        // Safety fallback: if any group was unresolvable, deny Anonymous access
        if (anyGroupUnresolvable)
        {
            permissionService.SetPermission("Anonymous", identifier, PermissionLevel.None);
            Log($"Group '{lastUnresolvableGroup}' not found on target. Setting Anonymous=None on page {pageId} as safety fallback.");
        }
    }

    /// <summary>
    /// Builds a case-insensitive dictionary mapping group names to group IDs.
    /// Cached on first call and reused across pages in the same deserialization run.
    /// </summary>
    private Dictionary<string, int> BuildGroupNameCache()
    {
        if (_groupNameCache != null)
            return _groupNameCache;

        _groupNameCache = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var groups = UserManagementServices.UserGroups.GetGroups();
        foreach (var group in groups)
        {
            if (!string.IsNullOrEmpty(group.Name) && !_groupNameCache.ContainsKey(group.Name))
            {
                _groupNameCache[group.Name] = group.ID;
            }
        }

        Log($"Built group name cache: {_groupNameCache.Count} group(s)");
        return _groupNameCache;
    }

    /// <summary>
    /// Queries DW PermissionService for explicit page permissions and maps them to DTOs.
    /// Returns an empty list if no explicit permissions are set.
    /// </summary>
    public List<SerializedPermission> MapPermissions(int pageId)
    {
        var permissionService = new PermissionService();
        var query = new PermissionQuery
        {
            Key = pageId.ToString(),
            Name = "Page"
        };

        var permissions = permissionService.GetPermissionsByQuery(query);
        var result = new List<SerializedPermission>();

        foreach (var permission in permissions)
        {
            var ownerId = permission.OwnerId;

            if (IsRole(ownerId))
            {
                result.Add(new SerializedPermission
                {
                    Owner = ownerId,
                    OwnerType = "role",
                    OwnerId = null,
                    Level = GetLevelName(permission.Level),
                    LevelValue = (int)permission.Level
                });
            }
            else
            {
                // Group: look up name via UserGroups service
                string ownerName = ownerId;
                if (int.TryParse(ownerId, out var groupId))
                {
                    var group = UserManagementServices.UserGroups.GetGroupById(groupId);
                    if (group != null)
                    {
                        ownerName = !string.IsNullOrEmpty(group.Name) ? group.Name : ownerId;
                        Log($"Resolved group ID {ownerId} to name '{ownerName}'");
                    }
                    else
                    {
                        Log($"Warning: Group ID {ownerId} not found (deleted group). Using raw ID as owner name.");
                    }
                }

                result.Add(new SerializedPermission
                {
                    Owner = ownerName,
                    OwnerType = "group",
                    OwnerId = ownerId,
                    Level = GetLevelName(permission.Level),
                    LevelValue = (int)permission.Level
                });
            }
        }

        Log($"Page {pageId}: {result.Count} permission(s) mapped");
        return result;
    }
}
