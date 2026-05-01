# Phase 41: Admin-UI polish + cross-page consistency — Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-01
**Phase:** 41-admin-ui-polish
**Areas discussed:** Item Types rename scope, Empty baseline excludes, Sample XML editor sizing, Mode dropdown fix

---

## Item Types rename scope

| Option | Description | Selected |
|--------|-------------|----------|
| Tree node only | Just `SerializerSettingsNodeProvider.cs:75` | |
| Tree + list + breadcrumb | Tree node + list/edit screen titles + breadcrumb segments — consistent UX everywhere user sees the term | ✓ |
| Full sweep | Tree, list, breadcrumb, plus user-facing comment / tooltip / help text — most consistent, biggest blast radius | |

**User's choice:** Tree + list + breadcrumb (Recommended)
**Notes:** Internal C# identifier `ItemTypesNodeId` stays as-is to avoid test-name churn (D-02 rationale).

---

## Empty Phase 40 baseline excludes

| Option | Description | Selected |
|--------|-------------|----------|
| Investigate first, then decide | Diff post-Phase-40 swift2.2-combined.json against pre-Phase-40 swift2.2-baseline.json (commit c5d9a8c~) — restore if lost, document if intentional | ✓ |
| Restore from git history | Pull pre-Phase-40 excludeFieldsByItemType set, port to flat shape, ship | |
| Curate from scratch | Fresh exclusion set from Swift 2.2 ItemType analysis — higher quality, slower | |
| Accept empty + document | If genuinely intentional, document in docs/baselines/Swift2.2-baseline.md and move on | |

**User's choice:** Investigate first, then decide (Recommended)
**Notes:** Audit drives the choice. Executor autonomous as long as audit produces a clear answer; checkpoint only if it doesn't.

---

## Sample XML editor sizing

| Option | Description | Selected |
|--------|-------------|----------|
| Tall WithRows, fixed | `WithRows(40)` or similar — predictable height, doesn't fight CSS | |
| Fill the tab via CSS | height: 100% / flex-grow on the editor — adapts to any screen size | ✓ |
| Make it a Code editor (read-only) | Switch from Textarea to a CoreUI Code/Monaco-style editor with syntax highlighting | |

**User's choice:** Fill the tab via CSS (Recommended)
**Notes:** If switching to a Code editor lands cleanly AND gives syntax highlighting / line numbers as a side benefit, take it (D-09). Pragmatic fallback to large Textarea is acceptable.

---

## Mode dropdown fix

| Option | Description | Selected |
|--------|-------------|----------|
| Drop the parenthetical suffix entirely | Options become "Deploy" / "Seed". Cleanest. | |
| Drop suffix + tooltip on the select | Options "Deploy" / "Seed" + ⓘ tooltip with conflict-strategy explanation | ✓ |
| Drop suffix + static help text below | Options + descriptive paragraph rendered under the select | |
| Investigate root cause first | Maybe the error isn't the parens — could be Select binding or WithReloadOnChange | |

**User's choice:** Drop suffix + tooltip on the select (Recommended)
**Notes:** Researcher still confirms actual screen-error root cause matches the parens hypothesis. If different (e.g., Select value/label binding), fix follows actual cause — but the label cleanup + tooltip are the desired UX regardless (D-13).

---

## Claude's Discretion

- Exact copy text for renamed UI labels and the tooltip wording — researcher / planner picks final wording within constraints
- Choice between Code editor and large Textarea for Sample XML — pick the one that drops in cleaner; tradeoff explained in plan
- Plan splitting strategy (one plan per fix vs grouped by file area) — planner's call

## Deferred Ideas

- Help text below the select alternative — rejected in favor of tooltip; revisit only if user feedback says tooltip is missed
- Code editor for all XML/JSON admin fields — broader UX phase if pursued
- Auto-formatting the sample XML on render — small future polish
- DependencyResolverException integration-test environment fix — separate test-infra phase
