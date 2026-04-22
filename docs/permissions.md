# Permissions

The serializer preserves explicit page permissions through the serialize /
deserialize round-trip, using name-based group resolution and a safety
fallback that prevents accidental public exposure of pages whose group is
missing on target.

## Table of contents

- [What gets serialized](#what-gets-serialized)
- [The YAML shape](#the-yaml-shape)
- [How permissions are restored](#how-permissions-are-restored)
- [The safety fallback](#the-safety-fallback)
- [Permission logging](#permission-logging)
- [Pre-create groups on target](#pre-create-groups-on-target)
- [Role vs group resolution](#role-vs-group-resolution)

## What gets serialized

DynamicWeb.Serializer serializes **explicit** page permissions only.
Pages that inherit from their parent (no explicit overrides) have no
`permissions` section in their YAML and are left alone on deserialize.

Each explicit permission entry captures:

- **Owner** — the role or group name (e.g. `Anonymous`, `Marketing Team`).
- **Owner type** — `Role` or `Group`.
- **Permission level** — one of `None`, `Read`, `Edit`, `Create`, `Delete`, `All`.

Inherited permissions, default permissions, and the role system's
built-in implicit rules are all driven by the same DW machinery on
target — the serializer does not replicate them because they are not
per-page overrides.

## The YAML shape

```yaml
permissions:
  - owner: Anonymous
    ownerType: role
    level: none
    levelValue: 1
  - owner: AuthenticatedFrontend
    ownerType: role
    level: read
    levelValue: 4
  - owner: Marketing Team
    ownerType: group
    level: edit
    levelValue: 8
```

`level` is the enum name; `levelValue` is the numeric value stored in
the DW permissions table. Both are emitted for readability — the
deserializer uses `level` as authoritative and ignores `levelValue`
mismatches.

## How permissions are restored

Deserialization uses a **source-wins** model. For every page with a
`permissions` section in YAML:

1. **Existing explicit permissions on the target page are cleared first.**
   The serialized list is the complete source of truth — adding entries
   to the target without removing the old ones would drift over time.
2. **Each entry is resolved.** Roles resolve by name directly. Groups
   resolve by name against the target's user-group table (case-insensitive).
3. **Resolved entries are applied.** The target's DW permission machinery
   writes the resulting (owner-type, owner-id, level) triple.

Pages without a `permissions` section in YAML are untouched. This
preserves inherited permissions and pages whose explicit permissions
pre-date the baseline adoption.

## The safety fallback

If a group permission references a user group that does **not** exist on
the target environment, the serializer applies a defensive fallback:

1. The group permission is **skipped** — without a matching group ID, the
   permission cannot be written.
2. **`Anonymous` access is set to `None`** on that page. This prevents
   accidental public exposure of a page that was meant to be
   group-restricted.
3. The fallback is **logged** as a warning:

   ```
   WARNING: Permission fallback — group 'Marketing Team' not found on target;
            Anonymous set to None on page {GUID} to prevent exposure
   ```

Under strict mode, the warning escalates. Under lenient mode, the page
stays locked down to anonymous users while the deploy completes; an
operator can create the group and re-deserialize afterwards.

The fallback is deliberately conservative. A page that loses its intended
group permissions is broken either way; falling back to "deny anonymous"
means the broken state is private rather than accidentally public.

## Permission logging

Every permission action is recorded in the run log:

- **Applied** — `owner=X ownerType=Role|Group level=Y applied to page {GUID}`
- **Skipped** — `Group 'X' not found on target — skipping permission for page {GUID}`
- **Safety fallback triggered** — the warning above

The log viewer (`Settings > Database > Serialize > Log Viewer`) surfaces
these per-run; the Management API response's `Message` field includes the
cumulative count of skipped-and-fallback-triggered permissions.

## Pre-create groups on target

The recommended operational posture is to ensure user groups exist on
every environment **before** a baseline deploy that references them.
Group creation is not in the baseline's scope because groups are often
coupled to per-environment identity-provider syncs, impersonation-chain
policies, or customer-managed membership lists.

Two practical patterns:

- **Document the required groups** alongside the baseline. When the
  Swift 2.2 Content predicate is adopted, the operator creates
  `Marketing Team`, `Customer Service`, etc. on every target env as a
  one-time bootstrap.
- **Run a bootstrap script** after the first DW install on a new env
  that creates the documented groups before the first serializer
  deserialize runs. Subsequent deploys then apply permissions cleanly
  because the groups resolve by name.

If you adopt the pattern of serializing `AccessUser` + `AccessUserGroup`
tables via SqlTable predicates, confirm that:

- The group rows come *before* the permission-assignment deserialize.
  The ordering is predicate-list order in the Deploy mode config, so
  put `AccessUser` / `AccessUserGroup` predicates first.
- The predicates use appropriate `excludeFields` to strip
  environment-specific columns like `AccessUserLastLoginDate`.

See [`configuration.md`](configuration.md#sqltable-predicate-fields) for
the predicate field reference and [`sql-tables.md`](sql-tables.md) for
`WHERE` clause usage to filter `AccessUser` rows by type.

## Role vs group resolution

**Roles** are built-in DW-system identities that exist on every DW install
with the same name:

- `Administrator`
- `AuthenticatedBackend`
- `AuthenticatedFrontend`
- `Anonymous`

Role permissions resolve by name directly — no lookup is needed because
role names are identical across environments.

**Groups** are customer-defined identities. Each group has a numeric ID
that differs between environments and a name that is (by convention) the
same. The serializer resolves groups by name:

```csharp
// Pseudocode of the resolution path
var targetGroup = userGroupService.GetByName(entry.Owner);
if (targetGroup == null)
{
    // Safety fallback: log, skip this entry, force Anonymous=None
}
else
{
    ApplyPermission(page, ownerType: Group, ownerId: targetGroup.Id, level);
}
```

Case-insensitive matching is used; `Marketing Team`, `marketing team`,
and `MARKETING TEAM` all resolve to the same target group. If you have
multiple groups with identically spelled names on different target
environments (typically a data-hygiene issue), rename them before the
baseline is adopted.

## See also

- [Concepts](concepts.md) — where permissions fit in the deserialize flow
- [Configuration](configuration.md) — permission-related predicate fields
- [Strict mode](strict-mode.md) — how the safety-fallback warning escalates
- [Troubleshooting](troubleshooting.md) — debugging missing-group deploys
