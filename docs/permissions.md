# Permissions

The serializer preserves explicit permissions on **pages, grid rows, and
paragraphs** through the serialize / deserialize round-trip, using name-based
group resolution and a safety fallback that prevents accidental public
exposure of an entity whose group is missing on target.

## Table of contents

- [What gets serialized](#what-gets-serialized)
- [The YAML shape](#the-yaml-shape)
- [How permissions are restored](#how-permissions-are-restored)
- [The safety fallback](#the-safety-fallback)
- [Frontend visibility](#frontend-visibility)
- [Permission logging](#permission-logging)
- [Pre-create groups on target](#pre-create-groups-on-target)
- [Role vs group resolution](#role-vs-group-resolution)

## What gets serialized

Truvio.Commerce.Serializer serializes **explicit** permissions on pages,
grid rows, and paragraphs. An entity that carries no explicit override has
no `permissions` section in its YAML and is left alone on deserialize.

Permission evaluation on target is inheritance-based: a paragraph inherits
from its grid row, a grid row from its page, a page from its parent pages
and area. That inheritance chain stays DW-driven — the serializer captures
only the explicit overrides at each level, exactly the rows a target needs
to reproduce the same effective permissions.

Each explicit permission entry captures:

- **Owner** — the role or group name (e.g. `Anonymous`, `Marketing Team`).
- **Owner type** — `role` or `group`.
- **Permission level** — one of `none`, `read`, `edit`, `create`, `delete`, `all`.
- **SubName** — an optional scope qualifier (see below); absent for the
  common case.

Inherited permissions, default permissions, and the role system's
built-in implicit rules are all driven by the same DW machinery on
target — the serializer does not replicate them because they are not
explicit overrides.

## The YAML shape

A page, a grid row, and a paragraph each carry the same `permissions` list
shape. On a page:

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
    levelValue: 20
```

On a grid row (nested under the row, before its `columns`):

```yaml
permissions:
  - owner: AuthenticatedFrontend
    ownerType: role
    level: read
    levelValue: 4
```

On a paragraph (nested under the paragraph):

```yaml
permissions:
  - owner: Anonymous
    ownerType: role
    level: none
    levelValue: 1
```

`level` is the enum name; `levelValue` is the numeric value stored in
the DW permissions table. Both are emitted for readability — the
deserializer uses `level` as authoritative and ignores `levelValue`
mismatches.

### Scoped rules (`subName`)

A permission may carry a `subName` that scopes it to a sub-entity. DW uses
this for rules such as "all paragraphs on this page", stored on the page
identifier with a `Paragraph` sub-name:

```yaml
permissions:
  - owner: Anonymous
    ownerType: role
    subName: Paragraph
    level: none
    levelValue: 1
```

`subName` is omitted whenever the rule is unscoped, which is the common
case. It round-trips verbatim so a scoped rule is restored at exactly the
scope it was authored, never widened to the whole entity.

## How permissions are restored

Deserialization uses a **source-wins** (Replace) model. For every entity
with a `permissions` section in YAML:

1. **Existing explicit permissions on the target entity are removed first.**
   The serialized list is the complete source of truth; each existing
   explicit row is deleted so the target ends with exactly the entries the
   YAML describes and no residual denies drift in over successive runs.
2. **Each entry is resolved.** Roles resolve by name directly. Groups
   resolve by name against the target's user-group table (case-insensitive).
3. **Resolved entries are applied** at their exact identifier, including any
   `subName` scope. The target's DW permission machinery writes the
   resulting (owner-id, level) row.

In **Merge** mode the serializer never touches permissions on an existing
entity — the target's permissions are left exactly as they are.

Entities without a `permissions` section in YAML are untouched in either
mode. This preserves inherited permissions and any explicit permissions
added directly on the target.

## The safety fallback

If a group permission references a user group that does **not** exist on
the target environment, the serializer applies a defensive fallback on the
entity being restored (page, grid row, or paragraph):

1. The group permission is **skipped** — without a matching group ID, the
   permission cannot be written.
2. **`Anonymous` access is set to `None`** on that same entity. This
   prevents accidental public exposure of content that was meant to be
   group-restricted.
3. The fallback is **logged** as a warning naming the entity.

The page, grid row, or paragraph stays locked down to anonymous users while
the deserialize completes; an operator can create the group and
re-deserialize afterwards.

The fallback is deliberately conservative. An entity that loses its intended
group permissions is broken either way; falling back to "deny anonymous"
means the broken state is private rather than accidentally public.

## Frontend visibility

Grid row and paragraph permissions are frontend visibility controls, not
admin-only metadata. DW hides a grid row or paragraph from any visitor who
lacks `Read` on it when rendering the page. Round-tripping these permissions
therefore preserves the exact frontend visibility rules across
environments — a paragraph set to `Anonymous = None` on the source stays
hidden from anonymous visitors on the target.

## Permission logging

Every permission action is recorded in the run log:

- **Applied** — `owner=X ownerType=Role|Group level=Y applied to page {GUID}`
- **Skipped** — `Group 'X' not found on target — skipping permission for page {GUID}`
- **Safety fallback triggered** — the warning above

The log viewer (`Settings > Developer > Serialize > Log Viewer`) surfaces
these per-run; the Management API response's `Message` field includes the
cumulative count of skipped-and-fallback-triggered permissions.

## Pre-create groups on target

The recommended operational posture is to ensure user groups exist on
every environment **before** a baseline deserialize that references them.
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
  deserialize runs. Subsequent deserialize runs then apply permissions cleanly
  because the groups resolve by name.

If you adopt the pattern of serializing `AccessUser` + `AccessUserGroup`
tables via SqlTable predicates, confirm that:

- The group rows come *before* the permission-assignment deserialize.
  The ordering is predicate-list order in the Replace mode config, so
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
- [Troubleshooting](troubleshooting.md) — debugging missing-group deserialize runs
