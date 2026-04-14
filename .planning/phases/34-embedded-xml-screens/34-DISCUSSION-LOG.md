# Phase 34: Embedded XML Screens - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-04-14
**Phase:** 34-embedded-xml-screens
**Areas discussed:** Control Type, XML Type Discovery, Tree Node Structure, Element Discovery, Scope of Change

---

## Control Type

User specified upfront: multiselect lists instead of checkbox lists. Clarification requested.

| Option | Description | Selected |
|--------|-------------|----------|
| DW Select with AllowMultiSelect | Dropdown-style multiselect | |
| A different DW list control | User describes: left searchable list, right selected items | ✓ |

**User's choice:** `SelectMultiDual` from `Dynamicweb.CoreUI.Editors.Lists` — identified from DW's `ScreenPresetEditScreen` "visible fields" usage.
**Notes:** User wanted the dual-pane control with search, not a simple dropdown.

---

## XML Type Discovery

| Option | Description | Selected |
|--------|-------------|----------|
| Query live content DB rows | Parse actual XML blobs to extract distinct type names | ✓ |
| Query metadata columns | SELECT DISTINCT from typed columns (faster, less complete) | |
| You decide | Claude picks | |

**User's choice:** Query live content DB rows
**Notes:** None

---

## Discovery Persistence

| Option | Description | Selected |
|--------|-------------|----------|
| Persist to config | Scan writes to config, list reads from config | |
| Discover on screen load | Re-query every time (always current, slower) | |
| Persist + manual rescan | Save to config, Rescan button refreshes | ✓ |

**User's choice:** Persist + manual rescan
**Notes:** None

---

## Tree Node Structure

| Option | Description | Selected |
|--------|-------------|----------|
| Flat list under one node | All types in one list under "Embedded XML" | ✓ |
| Grouped by source | Sub-groups for "URL Providers" vs "Modules" | |
| You decide | Claude picks | |

**User's choice:** Flat list under one node
**Notes:** Mirrors existing Predicates pattern.

---

## Element Discovery

| Option | Description | Selected |
|--------|-------------|----------|
| Parse live XML from DB | Query actual XML blobs on edit screen load | ✓ |
| Fixed known list per type | Hardcoded element mappings | |
| Parse on first scan, cache | Extract during Scan and persist | |

**User's choice:** Parse live XML from DB
**Notes:** Heavier query but accurate — shows real elements present in the data.

---

## Scope of Change (Retroactive)

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, change Phase 33 too | Update PredicateEditScreen CheckboxLists to SelectMultiDual | ✓ |
| No, Phase 34 onward only | Keep Phase 33 CheckboxLists as-is | |
| Yes, and all future phases | Change Phase 33 AND establish as standard for Phases 35-37 | |

**User's choice:** Yes, change Phase 33 too
**Notes:** Establishes SelectMultiDual as consistent control. User separately confirmed this is the standard going forward.

---

## Claude's Discretion

- SQL query structure for XML blob extraction
- Config storage shape for discovered XML types
- Element caching within screen load
- Error handling for malformed XML

## Deferred Ideas

None
