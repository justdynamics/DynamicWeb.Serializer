---
status: partial
phase: 41-admin-ui-polish
source: [41-VERIFICATION.md]
started: 2026-05-01T17:39:11Z
updated: 2026-05-01T17:39:11Z
---

## Current Test

[awaiting human testing]

## Tests

### 1. D-01 — tree/list/edit-screen rename live render
expected: Open https://localhost:54035/Admin/ → Settings → System → Developer → Serialize. Tree node reads "Item Type Excludes" (not "Item Types"). List screen title reads "Item Type Excludes". Item edit screen reads "Item Type Excludes - {SystemName}".
result: [pending]

### 2. D-05 — eCom_CartV2 dual-list saved-exclusion merge
expected: Embedded XML → eCom_CartV2 → 'Excluded Elements' dual-list. Right-hand 'excluded' panel shows the 21 saved elements (not empty). Left-hand 'available' panel may be empty if live discovery returns 0 from DB — that is acceptable.
result: [pending]

### 3. D-13 — Mode dropdown live render on Deploy predicate
expected: Predicates → any saved Deploy predicate. Screen renders without error. Mode dropdown shows "Deploy" or "Seed" selected (no parens suffix).
result: [pending]

### 4. D-12 — Mode hint copy visible
expected: On the Mode field label, hover/inspect tooltip. Hint text appears: "Deploy = source-wins (YAML overwrites destination). Seed = destination-wins field-level merge (only fills empty destination fields)."
result: [pending]

### 5. D-08/D-10 — Sample XML editor visual
expected: Embedded XML → eCom_CartV2 → reference tab. "Sample XML from database" editor fills visible area vertically (~30 rows tall) and is read-only.
result: [pending]

## Summary

total: 5
passed: 0
issues: 0
pending: 5
skipped: 0
blocked: 0

## Gaps
