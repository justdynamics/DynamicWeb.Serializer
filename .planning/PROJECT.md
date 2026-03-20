# Dynamicweb.ContentSync

## What This Is

A DynamicWeb AppStore app that serializes and deserializes content trees to/from YAML files on disk, enabling content to be version-controlled and deployed alongside code. The DynamicWeb equivalent of Sitecore Unicorn — replacing manual sync with automated, developer-friendly content synchronization. Tested and verified with cross-environment sync (Swift 2.2 → Swift 2.1) including visual editor rendering, page properties, and grid row styling.

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

### Active

- [ ] Tested across multiple content trees beyond Customer Center
- [ ] Publishing to NuGet registry

### Out of Scope

- Real-time change detection via Notifications API — v2 feature
- DynamicWeb admin UI for configuration — v1 uses config file only
- Media/file serialization (images, documents) — content structure only for v1
- Partial/incremental sync — v1 does full sync only
- OAuth/licensing — open source app

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
- **Config approach**: Standalone config file for v1, not DynamicWeb admin UI
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

---
*Last updated: 2026-03-20 after Phase 6 completion (v1.1 gap closure)*
