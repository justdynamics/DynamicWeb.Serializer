# Phase 12: Permission Deserialization + Docs - Context

**Gathered:** 2026-03-23
**Status:** Ready for planning

<domain>
## Phase Boundary

Extend ContentSync deserialization to restore explicit page permissions from YAML. Resolve roles by name (always available), resolve groups by group name on the target (may differ in ID). Safety fallback: if a group is missing on target, set Anonymous to None to prevent accidental exposure. Log all permission actions. Document the behavior in README.

</domain>

<decisions>
## Implementation Decisions

### Permission Restoration
- **D-01:** After deserializing a page (create or update), read the `permissions` section from YAML and apply via `PermissionService.SetPermission()`
- **D-02:** Before applying serialized permissions, clear any existing explicit permissions on the page (source-wins model — serialized state is truth)
- **D-03:** Role permissions restored by matching the role name string directly (Anonymous, AuthenticatedFrontend, etc.) — role names are identical across all DW environments
- **D-04:** Group permissions resolved by searching all groups on target by name via `UserManagementServices.UserGroups.GetGroups()` and matching by `group.Name`

### Safety Fallback
- **D-05:** If a serialized group permission references a group name that doesn't exist on the target, set `Anonymous = PermissionLevel.None` on that page as a safety fallback
- **D-06:** The safety fallback is applied once per page (not per missing group) — if any group is unresolvable, Anonymous gets denied
- **D-07:** Safety fallback is logged as a warning: "Group '{name}' not found on target. Setting Anonymous=None on page {id} as safety fallback."

### Logging
- **D-08:** Log every permission action: "Applied {role/group} = {level} on page {id}" for success
- **D-09:** Log skipped permissions: "Skipped permission for '{owner}' — group not found on target"
- **D-10:** Log safety fallback trigger separately from skip

### No-Permission Pages
- **D-11:** Pages without a `permissions` section in YAML get no permission changes — existing target permissions (including inherited) are left untouched

### Documentation
- **D-12:** Add a "Permissions" section to README covering: what gets serialized, how roles/groups are resolved, the safety fallback behavior, and what happens when groups are missing

### Claude's Discretion
- Where in the deserialize pipeline to hook permission restoration (after page save, before or after item fields)
- Whether to batch group name lookups or resolve per-page
- Exact wording of README permission section
- Whether to use a PermissionDeserializer helper class or inline in ContentDeserializer

</decisions>

<specifics>
## Specific Ideas

- `PermissionService.SetPermission(ownerId, identifier, level)` is the write API — identifier is `new PermissionEntityIdentifier(pageId.ToString(), "Page")`
- Clear existing permissions via `PermissionService.SetPermissionsByQuery(query, PermissionLevel.None)` or query + delete
- Group name matching should be case-insensitive
- Cache the group name → ID mapping once at start of deserialization (avoid repeated lookups)

</specifics>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### DW Permission System (write path)
- `C:\Projects\temp\dw10source\src\Core\Dynamicweb.Core\Security\Permissions\PermissionService.cs` — SetPermission, GetPermissionsByQuery, SetPermissionsByQuery
- `C:\Projects\temp\dw10source\src\Core\Dynamicweb.Core\Security\Permissions\PermissionLevel.cs` — None=1, Read=4, Edit=20, Create=84, Delete=340, All=1364
- `C:\Projects\temp\dw10source\src\Core\Dynamicweb.Core\Security\Permissions\PermissionEntityIdentifier.cs` — Key + Name + SubName
- `C:\Projects\temp\dw10source\src\Core\Dynamicweb.Core\Security\UserManagement\UserRoles\UserRole.cs` — Built-in role names

### Existing ContentSync (extend these)
- `src/Dynamicweb.ContentSync/Serialization/ContentDeserializer.cs` — Main deserialization pipeline, hook permission restore here
- `src/Dynamicweb.ContentSync/Serialization/PermissionMapper.cs` — Phase 11's serialization mapper (can extend for deserialization)
- `src/Dynamicweb.ContentSync/Models/SerializedPermission.cs` — DTO with Owner, OwnerType, OwnerId, Level, LevelValue
- `src/Dynamicweb.ContentSync/Models/SerializedPage.cs` — Has Permissions list

### Group Lookup
- `UserManagementServices.UserGroups.GetGroups()` — returns all groups, filter by Name for matching
- `UserManagementServices.UserGroups.GetGroupById(id)` — used in Phase 11 for serialization

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `PermissionMapper.IsRole()`: Already distinguishes roles from groups
- `PermissionMapper.GetLevelName()`: Converts enum to string (need reverse for deserialization)
- `SerializedPermission.LevelValue`: Numeric value is authoritative for deserialization — cast directly to PermissionLevel

### Established Patterns
- ContentDeserializer has create and update paths — permissions should be applied after page save in both paths
- Source-wins model: serialized state overrides target state
- Logging via `Action<string>? _log` callback

### Integration Points
- ContentDeserializer.DeserializePageSafe() — after page is saved, apply permissions
- PermissionService — instantiate once per deserialization run, reuse
- Group name cache — build once from UserManagementServices.UserGroups.GetGroups() at start

</code_context>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 12-permission-deserialization-docs*
*Context gathered: 2026-03-23 via --auto*
