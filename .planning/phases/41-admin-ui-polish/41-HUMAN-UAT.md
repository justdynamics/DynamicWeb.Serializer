---
status: passed
phase: 41-admin-ui-polish
source: [41-VERIFICATION.md]
started: 2026-05-01T17:39:11Z
updated: 2026-05-01T20:12:00Z
---

## Current Test

[all passed]

## Tests

### 1. D-01 — tree/list/edit-screen rename live render
expected: Open https://localhost:54035/Admin/ → Settings → System → Developer → Serialize. Tree node reads "Item Type Excludes" (not "Item Types"). List screen title reads "Item Type Excludes". Item edit screen reads "Item Type Excludes - {SystemName}".
result: passed (2026-05-01)

### 2. D-05 — eCom_CartV2 dual-list saved-exclusion merge
expected: Embedded XML → eCom_CartV2 → 'Excluded Elements' dual-list. Right-hand 'excluded' panel shows the 21 saved elements (not empty). Left-hand 'available' panel may be empty if live discovery returns 0 from DB — that is acceptable.
result: passed (2026-05-01) — first pass FAILED live (Selected panel empty); root cause was framework SetValue overwriting editor.Value after GetEditor returns. Closed by promoting Model.ExcludedElements to List<string> (commit af78c2f). Re-verified passing.

### 3. D-06 — Item Type field-exclusion dual-list (companion to D-05)
expected: Item Type Excludes → any item type with saved field exclusions. Selected panel populated.
result: passed (2026-05-01) — same fix as D-05 (List<string> promotion).

### 4. D-13 — Mode dropdown live render on Deploy predicate
expected: Predicates → any saved Deploy predicate. Screen renders without error. Mode dropdown shows "Deploy" or "Seed" selected (no parens suffix).
result: passed (2026-05-01)

### 5. D-12 — Mode hint copy visible
expected: On the Mode field label, hover/inspect tooltip. Hint text appears: "Deploy = source-wins (YAML overwrites destination). Seed = destination-wins field-level merge (only fills empty destination fields)."
result: passed (2026-05-01)

### 6. D-08/D-10 — Sample XML editor visual
expected: Embedded XML → eCom_CartV2 → reference tab. "Sample XML from database" editor fills visible area vertically (~30 rows tall) and is read-only.
result: passed (2026-05-01)

### 7. D-14 — Tree node + list screen rename to "Embedded XML Excludes"
expected: Tree node reads "Embedded XML Excludes" (was "Embedded XML"); list screen title reads "Embedded XML Excludes" (was "Embedded XML Types"). Same parent rationale as D-01 — page manages exclusions, not types.
result: passed (2026-05-01) — added during live UAT; commit da55813.

## Summary

total: 7
passed: 7
issues: 0
pending: 0
skipped: 0
blocked: 0

## Gaps

None remaining. Two gaps surfaced during UAT (D-05/D-06 framework binding + D-14 rename) were closed inline within the same phase 41 — see VERIFICATION.md `gap_closure` section.
