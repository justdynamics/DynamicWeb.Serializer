# Phase 36: Area Screens - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-04-15
**Phase:** 36-area-screens
**Areas discussed:** Area Discovery, Column Discovery, Config Storage, Tree/Screen Pattern

---

## Area Discovery

| Option | Description | Selected |
|--------|-------------|----------|
| DW Area API (Services.Areas) | Idiomatic, consistent with existing usage | ✓ |
| SQL query | Direct DB access | |
| You decide | | |

**User's choice:** DW Area API (Services.Areas)

---

## Column Discovery

| Option | Description | Selected |
|--------|-------------|----------|
| SQL INFORMATION_SCHEMA | Same as Phase 33 pattern | |
| DW Area API properties | Reflect on Area object | ✓ |
| You decide | | |

**User's choice:** DW Area API properties

---

## Config Storage (Initial)

| Option | Description | Selected |
|--------|-------------|----------|
| New top-level dictionary | excludeColumnsByArea on SerializerConfiguration | |
| Extend existing predicate | Per-predicate storage | ✓ (after pattern change) |
| You decide | | |

**User's choice:** Initially "New top-level dictionary", then changed to per-predicate after deciding to integrate into predicates.

---

## Tree/Screen Pattern (Major Scope Change)

| Option | Description | Selected |
|--------|-------------|----------|
| Mirror Phase 35 exactly | Separate tree node, list/edit screens | |
| Simpler — no tree children | List screen only | |
| Other | User changed mind | ✓ |

**User's choice:** "Changed my mind, make it part of predicates" — No separate Areas tree node. Area column exclusions are a SelectMultiDual section on the Content predicate edit screen.
**Notes:** Major simplification from original ROADMAP.md scope. Requirements still met through predicate UI.

---

## Predicate Integration

| Option | Description | Selected |
|--------|-------------|----------|
| New SelectMultiDual section | Add area columns selector to Content predicates | ✓ |
| Read-only summary + link | Show exclusions with link to separate screen | |
| You decide | | |

**User's choice:** New SelectMultiDual section

---

## Final Storage Decision

| Option | Description | Selected |
|--------|-------------|----------|
| Per-predicate | excludeAreaColumns on ProviderPredicateDefinition | ✓ |
| Top-level dictionary | Global excludeColumnsByArea | |

**User's choice:** Per-predicate — each Content predicate can have different area column exclusions

---

## Claude's Discretion

- Area property enumeration approach
- Section placement on predicate edit screen
- Area context display (name/ID)
- Handling missing/invalid AreaId

## Deferred Ideas

None
