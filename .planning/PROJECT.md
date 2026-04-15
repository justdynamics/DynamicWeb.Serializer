# DynamicWeb.Serializer (formerly Dynamicweb.ContentSync)

## What This Is

A DynamicWeb AppStore app that serializes and deserializes database content to/from YAML files on disk, enabling full database state to be version-controlled and deployed alongside code. Started as a content-only sync tool (Sitecore Unicorn equivalent), now expanding to cover all DynamicWeb data groups — ecommerce settings, users, marketing, PIM, and more — via a pluggable provider architecture. Configurable via DynamicWeb admin UI (Settings > Database > Serialize).

## Core Value

Developers can reliably move DynamicWeb database state between environments through source control, with serialized YAML files as the single source of truth.

## Current Milestone: v0.4.0 Full Page Fidelity

**Goal:** Serialize and deserialize ALL page-level settings, area ItemType connections, and ecommerce navigation configuration so that deserialized pages are functionally identical to the source.

**Target features:**
- Expand SerializedPage DTO with all ~30 missing page properties (NavigationTag, ShortCut, UrlName, UrlDataProvider, SEO meta, SSL, permissions, visibility, URL inheritance, etc.)
- Serialize and deserialize PageNavigationSettings (ecommerce navigation config)
- Serialize and deserialize Area-level ItemType fields (header/footer/master connections)
- Handle EcomProductGroupField custom column schema sync
- Preserve original page timestamps during deserialization

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

- [x] Serialize explicit page permissions (roles and user groups) to YAML — v1.3
- [x] Deserialize permissions with name-based group resolution and safety fallback — v1.3
- [x] README documents permission handling including safety fallback — v1.3

### Active

- [x] Rename project to DynamicWeb.Serializer — v2.0 Phase 16
- [x] Pluggable provider architecture per data group — v2.0 Phase 13
- [x] SqlTableProvider for generic SQL table serialization — v2.0 Phase 13
- [x] Migrate existing ContentProvider into provider architecture — v2.0 Phase 14
- [x] Ecommerce settings serialization (~26 SQL tables) — v2.0 Phase 15
- [ ] Settings & Schema serialization (~25 items)
- [ ] Users, Marketing, PIM, Apps serialization (~30 tables)
- [x] Log viewer with guided advice — v2.0 Phase 16
- [x] Move deserialize to Asset management file detail page action — v2.0 Phase 16
- [x] Remove scheduled tasks (API commands replace them) — v2.0 Phase 16
- [x] Move admin UI from Settings > Content > Sync to Settings > Database > Serialize — v2.0 Phase 16
- [x] Predicate config multi-provider support (Content + SqlTable fields) — v2.0 Phase 18

- [x] Resolve internal page ID references (`Default.aspx?ID=NNN`) in ItemType fields during deserialization — v0.3.1
- [x] Re-tag Git from v1.0/v2.0 to 0.x pre-release versioning — v0.3.1

- [ ] Serialize all ~30 missing page properties (NavigationTag, ShortCut, UrlName, SEO, SSL, etc.) — v0.4.0
- [ ] Serialize PageNavigationSettings (ecommerce navigation config per page) — v0.4.0
- [ ] Serialize Area-level ItemType fields (header/footer/master connections) — v0.4.0
- [ ] Handle EcomProductGroupField custom column schema sync — v0.4.0
- [ ] Preserve original page timestamps during deserialization — v0.4.0

### Out of Scope

- Real-time change detection via Notifications API — complexity vs value
- Media/file serialization (images, documents) — files stay in git directly
- Partial/incremental sync — full sync only
- OAuth/licensing — open source app
- File-based data groups (24 items) — files already live in git, no need to serialize
- Publishing to NuGet registry — deferred to later milestone

## Context

**Current State (v1.3 shipped, starting v2.0):**
- ~3,743 LOC C# across 100+ files
- Tech stack: .NET 8.0, DynamicWeb 10.23.9 NuGet, YamlDotNet, System.IO.Compression
- Verified on Swift 2.2 → Swift 2.1 cross-environment sync including permissions
- Admin UI at Settings > Content > Sync with predicate management
- Management API commands for CI/CD integration
- 100+ commits over 5 days (2026-03-19 → 2026-03-23)

**v2.0 Data Group Analysis (from C:\temp\DataGroups):**
- 74 SQL-based (SqlDataItemProvider) — generic table dump, bulk of work
- ~20 Settings-based (SettingsDataItemProvider) — settings files
- ~5 Content-based — existing ContentProvider
- ~5 Schema-based (SchemaDataItemProvider) — DB table structure
- 24 File-based — EXCLUDED (files stay in git directly)
- 1 Forms — custom

**SqlTableProvider Pattern:**
From DataGroup XMLs, each SQL data item has: Table, NameColumn, CompareColumns.
Generic provider reads all rows, serializes to YAML, deserializes back by matching NameColumn or primary key.

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
| Pluggable provider architecture | Different data groups need different serialization strategies | — Pending |
| SqlTableProvider as generic handler | 74 of ~124 data groups use SqlDataItemProvider — one provider covers most | — Pending |
| Remove scheduled tasks | API commands are sufficient; scheduled tasks add complexity | — Pending |

## Evolution

This document evolves at phase transitions and milestone boundaries.

**After each phase transition** (via `/gsd:transition`):
1. Requirements invalidated? → Move to Out of Scope with reason
2. Requirements validated? → Move to Validated with phase reference
3. New requirements emerged? → Add to Active
4. Decisions to log? → Add to Key Decisions
5. "What This Is" still accurate? → Update if drifted

**After each milestone** (via `/gsd:complete-milestone`):
1. Full review of all sections
2. Core Value check — still the right priority?
3. Audit Out of Scope — reasons still valid?
4. Update Context with current state

## Shipped Milestones

- **v1.0 MVP** — Full serialize/deserialize pipeline, predicates, scheduled tasks (2026-03-19)
- **v1.1 Robustness** — Multi-column paragraphs, dry-run, validation (2026-03-20)
- **v1.2 Admin UI** — Settings screen, predicate management, serialize action, API commands (2026-03-22)
- **v1.3 Permissions** — Permission serialization/deserialization with safety fallback (2026-03-23)

---
*Last updated: 2026-04-03 — milestone v0.4.0 started (full page fidelity)*
