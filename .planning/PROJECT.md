# Dynamicweb.ContentSync

## What This Is

A DynamicWeb AppStore app that serializes and deserializes content trees to/from YAML files on disk, enabling content to be version-controlled and deployed alongside code. The DynamicWeb equivalent of Sitecore Unicorn — replacing manual sync with automated, developer-friendly content synchronization. Tested and verified with cross-environment sync (Swift 2.2 → Swift 2.1) including visual editor rendering, page properties, and grid row styling. Configurable via DynamicWeb admin UI (Settings > Content > Sync) with query-based predicate management and ad-hoc serialize/deserialize via content tree context menus.

## Core Value

Developers can reliably move content between DynamicWeb environments through source control, with serialized YAML files as the single source of truth.

## Requirements

### Validated

- [x] Serialize full content trees (Area > Pages > Grids > Rows > Paragraphs) to YAML files on disk — v1.0
- [x] Mirror-tree file layout: folder structure reflects content hierarchy with .yml files per item — v1.0
- [x] Unicorn-style predicate configuration to define which content trees to include/exclude — v1.0
- [x] PageUniqueId (GUID) as canonical identity — match on GUID, not numeric ID — v1.0
- [x] Source-wins conflict strategy: serialized files always overwrite target DB on deserialize — v1.0
- [x] Deserialize YAML files back into a DynamicWeb database — v1.0
- [x] New numeric IDs assigned on deserialize when GUID doesn't exist in target — v1.0
- [x] Configurable via standalone config file (not DynamicWeb admin UI) — v1.0
- [x] Two scheduled tasks: one for full serialization, one for full deserialization — v1.0
- [x] Structured as a DynamicWeb AppStore app (NuGet package with `dynamicweb-app-store` tag) — v1.0
- [x] Comprehensive error handling and logging — v1.0
- [x] Cross-environment visual editor fidelity (grid rows, page properties, icons, spacing) — v1.0
- [x] Multi-column paragraph attribution preserved on round-trip — v1.1
- [x] Dry-run mode reports PropertyFields changes (Icon, SubmenuType) — v1.1
- [x] OutputDirectory validated at config-load and deserialize time — v1.1
- [x] Admin UI settings screen at Settings > Content > Sync — v1.2
- [x] Predicate management CRUD with page picker and area selector — v1.2
- [x] Ad-hoc serialize action on page edit screen with zip download — v1.2
- [x] Deserialize scheduled task with folder and zip modes — v1.2
- [x] Management API commands (ContentSyncSerialize/ContentSyncDeserialize) — v1.2
- [x] Config file as source of truth with admin UI as management layer — v1.2

### Active

- [ ] Serialize explicit page permissions (roles and user groups) to YAML alongside page data
- [ ] Deserialize permissions on target — roles restored directly, groups resolved by name
- [ ] Safety fallback: deny Anonymous access when a referenced user group is missing on target
- [ ] Document permission handling in README

### Out of Scope

- Real-time change detection via Notifications API — v2 feature
- Media/file serialization (images, documents) — content structure only
- Partial/incremental sync — full sync only
- OAuth/licensing — open source app
- Publishing to NuGet registry — deferred to later milestone
- Tested across multiple content trees — deferred to later milestone

## Context

**Current State (v1.0 shipped):**
- 2,194 LOC C# across 85 files
- Tech stack: .NET 8.0, DynamicWeb 10.23.9 NuGet, YamlDotNet
- Verified on Swift 2.2 → Swift 2.1 cross-environment sync
- 71 commits over 2 days (2026-03-19 → 2026-03-20)

**DynamicWeb Content Hierarchy:**
- Website (Area) → Pages → Grid → Rows → Columns → Paragraphs
- Pages use numeric IDs as primary keys but also have PageUniqueId (GUID)
- Item Types extend pages, paragraphs, and grid rows with custom fields
- Pages have PropertyItem fields (Icon, SubmenuType) separate from Item fields

**Test Environment:**
- Two pre-configured DynamicWeb instances at `C:\Projects\Solutions\swift.test.forsync`
- Test case: serialize "Customer Center" content tree from Swift 2.2, deserialize into Swift 2.1
- Both instances started with `dotnet run` on ports 5000/5001

## Constraints

- **Tech stack**: .NET 8.0+, DynamicWeb 10.2+ APIs — must be a valid AppStore app
- **Serialization format**: YAML — chosen for readability and git-friendly diffs
- **Config approach**: Config file as source of truth, admin UI as management layer (both coexist)
- **Sync model**: Full sync only for v1 (no incremental/delta sync)
- **Conflict resolution**: Source (files) always wins — no merge logic in v1

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| YAML over JSON/XML | More readable, cleaner git diffs, less verbose than XML | ✓ Good |
| GUID as canonical identity | Numeric IDs differ between environments; GUIDs are stable | ✓ Good |
| Source-wins conflict strategy | Simplest model, matches Unicorn default, files are truth | ✓ Good |
| Mirror-tree file layout | Intuitive mapping between disk and content tree, easy to navigate | ✓ Good |
| Config file over admin UI | Faster to implement, config lives in source control alongside content | ✓ Good |
| Full sync via scheduled tasks | Simpler than incremental, notifications deferred to v2 | ✓ Good |
| Two separate scheduled tasks | Separation of concerns: serialize and deserialize are distinct operations | ✓ Good |
| DoubleQuoted for CRLF strings | YAML Literal style normalizes \r\n to \n; DoubleQuoted preserves exactly | ✓ Good |
| Services.Xxx static accessor | Canonical DW10 pattern over new XxxService() | ✓ Good |
| Source-wins null-out for item fields | Fields absent from YAML explicitly cleared to prevent stale target data | ✓ Good |
| PropertyItem serialization | Page properties (Icon, SubmenuType) are separate from Item fields | ✓ Good |
| GridRow visual properties | TopSpacing, BottomSpacing, ContainerWidth etc. needed for visual editor | ✓ Good |

## Current Milestone: v1.3 Permissions

**Goal:** Serialize and deserialize page-level permissions so that access controls are preserved across environments, with a safety fallback for missing user groups.

**Target features:**
- Serialize explicit page permissions (roles and user groups) to YAML alongside page data
- Deserialize permissions on target — restore role-based permissions directly, resolve group permissions by group name
- Safety fallback: if a referenced user group doesn't exist on the target, deny Anonymous access to prevent accidental exposure
- Document permission handling behavior in README

---
*Last updated: 2026-03-22 after v1.3 milestone start*
