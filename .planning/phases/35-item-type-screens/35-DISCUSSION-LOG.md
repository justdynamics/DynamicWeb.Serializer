# Phase 35: Item Type Screens - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-04-15
**Phase:** 35-item-type-screens
**Areas discussed:** Item Type Discovery, Field Discovery, Tree Node + Screen Pattern, Edit Screen Extras

---

## Item Type Discovery

| Option | Description | Selected |
|--------|-------------|----------|
| DW ItemType API | Use GetAllItemTypes() or similar DW service | ✓ |
| SQL query on ItemType table | Direct SQL, consistent with Phase 34 | |
| You decide | Claude picks | |

**User's choice:** DW ItemType API
**Notes:** Most idiomatic approach for DW10.

---

## Discovery Persistence

| Option | Description | Selected |
|--------|-------------|----------|
| Discover live on load | Always current, no scan needed | ✓ |
| Persist + rescan | Same as Embedded XML pattern | |
| You decide | | |

**User's choice:** Discover live on load
**Notes:** ItemType API is fast enough for live discovery.

---

## Field Discovery Per Type

| Option | Description | Selected |
|--------|-------------|----------|
| DW ItemType field API | Fields collection from ItemType object | ✓ |
| SQL from ItemTypeField table | Raw SQL query | |
| You decide | | |

**User's choice:** DW ItemType field API
**Notes:** None

---

## Tree Node + Screen Pattern

| Option | Description | Selected |
|--------|-------------|----------|
| Mirror XML pattern exactly | Flat list, expandable children, action menu | |
| No tree children | List screen only | |
| You decide | | |

**User's choice:** Other — Mirror XML pattern but organize tree children by category path
**Notes:** Item type categories use `/` separator (e.g., `Swift-v2/Utilities`). Tree renders nested folders. Example: `Item Types > Swift-v2 > Utilities > CartApps`.

---

## Uncategorized Item Types

| Option | Description | Selected |
|--------|-------------|----------|
| Show at root level | Directly under Item Types | |
| Group under 'Uncategorized' | Separate subfolder | ✓ |
| You decide | | |

**User's choice:** Group under 'Uncategorized'
**Notes:** Keeps tree clean.

---

## Edit Screen Extras

| Option | Description | Selected |
|--------|-------------|----------|
| Field list + category info | Name, category, field count + SelectMultiDual | ✓ |
| Field exclusion only | Minimal — just type name + SelectMultiDual | |
| Match XML pattern with sample | Field exclusion + field definitions as reference | |

**User's choice:** Field list + category info
**Notes:** None

---

## Claude's Discretion

- Exact DW API calls for discovery and field enumeration
- Category property extraction and parsing
- Tree node ID structure for nested folders
- Whether to show field data types in SelectMultiDual options

## Deferred Ideas

None
