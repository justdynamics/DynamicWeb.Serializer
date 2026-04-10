# Phase 33: SqlTable Column Pickers - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-04-10
**Phase:** 33-sqltable-column-pickers
**Areas discussed:** Column discovery, CheckboxList UX, Save/load flow

---

## Column Discovery

### When to fetch schema

| Option | Description | Selected |
|--------|-------------|----------|
| On screen load (Recommended) | Query INFORMATION_SCHEMA.COLUMNS when predicate edit screen opens | ✓ |
| On table name change (dynamic) | Use WithReloadOnChange() on Table field | |
| Both — load + reload on change | Fetch on load AND reload when Table changes | |

**User's choice:** On screen load (Recommended)

### Missing table handling

| Option | Description | Selected |
|--------|-------------|----------|
| Fall back to textarea | Show original Textarea if no columns found | |
| Show empty CheckboxList + warning (Selected) | Empty list with "Table not found" message | ✓ |
| You decide | Claude picks | |

**User's choice:** Show empty CheckboxList + warning

---

## CheckboxList UX

### Pre-check behavior

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, pre-check existing (Recommended) | Checked = excluded, existing exclusions pre-checked | ✓ |
| Inverted — check means include | Checked = included, unchecked = excluded | |
| You decide | Claude picks | |

**User's choice:** Yes, pre-check existing (Recommended)

### Data type display

| Option | Description | Selected |
|--------|-------------|----------|
| No, just column names (Recommended) | Simple list of column names only | ✓ |
| Yes, show types | Show "ColumnName (nvarchar)" format | |

**User's choice:** No, just column names (Recommended)

---

## Save/Load Flow

| Option | Description | Selected |
|--------|-------------|----------|
| Same string list, different source (Recommended) | Model stays List<string>, only editor changes | ✓ |
| You decide | Claude picks | |

**User's choice:** Same string list, different source (Recommended)

---

## Claude's Discretion

- SQL schema query placement (model load vs screen vs helper)
- Column list caching within request lifecycle
- Test approach

## Deferred Ideas

None — discussion stayed within phase scope
