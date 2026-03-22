using Dynamicweb.ContentSync.Models;
using Dynamicweb.Security.Permissions;
using Dynamicweb.Security.UserManagement;

namespace Dynamicweb.ContentSync.Serialization;

/// <summary>
/// Maps DynamicWeb page permissions to SerializedPermission DTOs.
/// Resolves role names directly and group names via AccessUser lookup.
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
                // Group: look up name via AccessUser
                string ownerName = ownerId;
                if (int.TryParse(ownerId, out var userId))
                {
                    var user = User.GetUserByID(userId);
                    if (user != null)
                    {
                        ownerName = user.Name ?? ownerId;
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
