# Phase 39: Seed mode field-level merge — Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution
> agents. Decisions are captured in `39-CONTEXT.md` — this log preserves the
> alternatives considered so future phases can see why a path was or was not
> taken.

**Date:** 2026-04-22
**Phase:** 39-seed-mode-field-level-merge
**Areas discussed:** Unset detection per type, Merge scope on Content pages,
Re-run idempotency, Test migration, SQL write mechanism, Checksum interaction,
Dry-run, Admin UI scope

---

## Unset Detection per Type

### D-01: Baseline unset rule

| Option | Description | Selected |
|--------|-------------|----------|
| NULL only (strict) | Fill only when target IS NULL. Empty string / 0 / false / DateTime default all count as "set". | |
| NULL + type default | Fill when NULL OR empty string OR 0/false/DateTime.MinValue/Guid.Empty. | ✓ |
| NULL + empty string only (hybrid) | Strings fill on NULL or ''. Bools/ints/dates always "set". | |
| Column-type-registry driven | Per-column rule declared in a registry. | |

**User's choice:** NULL + type default.
**Notes:** User accepted the broader fill rule, incorporating that an
"empty string" or "0" / "false" / default date is typically an uninitialized
value in DW schemas rather than an explicit customer choice. Tradeoff
(customer-set defaults may be overwritten) is addressed in D-10.

### D-02: ItemField treatment

| Option | Description | Selected |
|--------|-------------|----------|
| Treat as string (NULL or '' = unset) | DW persists every ItemField as a string via ItemService; compare at the string layer. | ✓ |
| Decode by SystemName + typeid | Read ItemField definition, apply type-specific unset rules. | |
| Skip entire field-set if any field present on target | Coarser: if any target ItemField value, don't touch ItemFields at all. | |

**User's choice:** Treat as string.

### D-03: PropertyItem treatment

| Option | Description | Selected |
|--------|-------------|----------|
| Same rule as ItemFields | Uniform NULL-or-empty-string check. | ✓ |
| Never merge PropertyItem | Skip Icon / SubmenuType in Seed entirely. | |
| Always overwrite PropertyItem | Brand defaults seeded unconditionally. | |

**User's choice:** Same rule as ItemFields.

### D-04: Sub-object DTO behavior

| Option | Description | Selected |
|--------|-------------|----------|
| Merge per-field inside each sub-object | Unset rule applies recursively through Seo / UrlSettings / NavigationSettings / Visibility. | ✓ |
| Sub-object is atomic | Skip whole object if any field set. | |
| Sub-object is always source-wins | Ignore merge rule for sub-objects. | |

**User's choice:** Merge per-field inside each sub-object.

---

## Merge Scope on Content

### D-05: Scalar scope

| Option | Description | Selected |
|--------|-------------|----------|
| All non-identity scalars merge | PageUniqueId/AreaId/ParentPageId source-wins; everything else merges. | ✓ |
| Structural-only scalars | Only MenuText/Active/Sort/UrlName/Layout merge; branding/SEO-ish ones don't. | |
| Same as option 1 but explicit | Cosmetic variant. | |

**User's choice:** All non-identity scalars merge.

### D-06: Permissions

| Option | Description | Selected |
|--------|-------------|----------|
| Skip permissions entirely in Seed | Leave existing page permissions untouched. | ✓ |
| Fill only if target has no explicit permissions | Treat permission set as one atomic field. | |
| Merge per-role (union add) | Add missing roles; never remove. | |

**User's choice:** Skip permissions entirely.

### D-07: Children (gridrows, paragraphs)

| Option | Description | Selected |
|--------|-------------|----------|
| Recurse with same rule | Walk into gridrows/columns/paragraphs, merge at each level, create missing. | ✓ |
| Skip all children if parent exists | Only fill page-level fields. | |
| Create-only recursion | Add missing children; never mutate existing fields. | |

**User's choice:** Recurse with same rule.

### D-08: Provider alignment

| Option | Description | Selected |
|--------|-------------|----------|
| Shared IsUnsetForMerge helper + provider-specific write paths | Single rule helper; each provider keeps its own write mechanics. | ✓ |
| Independent implementations | Documented in both places but not mechanically shared. | |
| Full shared merge engine | One engine handling target-read + compare + write across both. | |

**User's choice:** Shared helper + provider-specific write paths.

---

## Re-run Idempotency

### D-09: "Has this been set" mechanism

| Option | Description | Selected |
|--------|-------------|----------|
| Read target state live, no marker | Compare against current target value at write time; no persisted marker. | ✓ |
| Persisted per-field marker | Track which fields Seed has written in a side table. | |
| Checksum-based skip (SqlTable-only) | Reuse existingChecksums for row-level equality detection. | |

**User's choice:** Live read, no marker.

### D-10: Type-default overwrite tradeoff

| Option | Description | Selected |
|--------|-------------|----------|
| Yes — false/0/default reads as unset, Seed may fill | Consistent with D-01. Accepted tradeoff. | ✓ |
| No — bools/ints always set | Contradicts D-01. | |
| No — document as known limitation | Ship broad rule with explicit documentation. | |

**User's choice:** Yes — accepted tradeoff.

### D-11: Log output format

| Option | Description | Selected |
|--------|-------------|----------|
| "Seed-merge: X filled, Y left" per row | Per-row merge-outcome summary. | ✓ |
| Silent unless a field was written | Log only on non-empty fills. | |
| Per-field debug log under verbose | Summary at info, per-field only in verbose. | |

**User's choice:** Per-row merge summary.

### D-12: Schema drift handling

| Option | Description | Selected |
|--------|-------------|----------|
| Inherit Phase 37-02 behavior — silently drop missing columns | Same tolerance already in place. | ✓ |
| Strict-mode-aware: warn in strict, silent otherwise | New STRICT-01-style escalation. | |
| Hard fail on any missing column | Refuses ambiguity. | |

**User's choice:** Inherit Phase 37-02.

---

## Test Migration Strategy

### D-13: Test coverage shape

| Option | Description | Selected |
|--------|-------------|----------|
| Unit tests on helper + integration per provider | Shared IsUnsetForMerge + per-provider acceptance coverage. | ✓ |
| Integration tests only | Skip helper units, go straight to end-to-end per provider. | |
| Unit tests only | Mock DW APIs and SQL; in-process only. | |

**User's choice:** Unit + integration.

### D-14: TDD discipline

| Option | Description | Selected |
|--------|-------------|----------|
| One plan per provider with RED/GREEN gates | Plan 39-01 (Content) + Plan 39-02 (SqlTable). | ✓ |
| Single plan, tests + implementation together | One plan covers both. | |
| Three plans: helper + each provider | Decoupled, adds a wave. | |

**User's choice:** Per-provider plans with TDD gates.

### D-15: Live E2E gate

| Option | Description | Selected |
|--------|-------------|----------|
| Live E2E against Swift 2.2 → CleanDB under strictMode:true | Extend full-clean-roundtrip.ps1 with Deploy → tweak → Seed sub-pipeline. | ✓ |
| Integration tests with real DB suffice | Skip live CleanDB. | |
| Both — integration + separate E2E closure | Phase 38 → 38.1 style split. | |

**User's choice:** Live E2E required.

### D-16: Phase 37 D-06 treatment

| Option | Description | Selected |
|--------|-------------|----------|
| Leave D-06 historical, supersede in Phase 39 | No edits to closed-phase artifacts. | ✓ |
| Edit 37-CONTEXT.md with "superseded" note | Minor traceable edit. | |
| PROJECT.md Key Decisions update only | Skip per-phase edits. | |

**User's choice:** Leave historical, supersede in Phase 39.

---

## SQL Write Mechanism (SqlTableProvider)

### D-17: Write mechanism

| Option | Description | Selected |
|--------|-------------|----------|
| Load target row, filter YAML columns in memory, narrowed UPDATE | SELECT target, compute unset subset, targeted UPDATE. | ✓ |
| Per-column UPDATE WHERE col IS NULL/empty | N writes per row, push unset-check into SQL. | |
| Single CASE-based UPDATE | One UPDATE with CASE WHEN col IS NULL THEN @val ELSE col END. | |

**User's choice:** Read-then-narrowed-UPDATE.

### D-18: Checksum-skip interaction

| Option | Description | Selected |
|--------|-------------|----------|
| Keep checksum-skip as first-level optimization | Checksum-equal rows skip before merge path engages. | ✓ |
| Drop checksum-skip in Seed mode | Always enter merge path; may yield zero updates. | |
| Split by mode — Deploy uses checksum, Seed always merges | Explicit mode separation. | |

**User's choice:** Keep checksum-skip first.

---

## Dry-run and Admin UI

### D-19: Dry-run output

| Option | Description | Selected |
|--------|-------------|----------|
| Per-field diff: "would fill col=X (target was NULL)" | Full audit before real run. | ✓ |
| Per-row summary only | Count per row. | |
| Same as Deploy dry-run | Reuse existing printer without merge awareness. | |

**User's choice:** Per-field diff.

### D-20: Admin UI scope

| Option | Description | Selected |
|--------|-------------|----------|
| No UI changes | Pure deserialization-behavior change. | ✓ |
| Merge-summary surface on log viewer | Counter badge in Phase 16 viewer. | |
| "Preview Seed merge" action | New button, new capability. | |

**User's choice:** No UI changes.

---

## Claude's Discretion (items user deferred to implementation)

- Exact signature of `IsUnsetForMerge` helper.
- Exact SQL for narrowed UPDATE (parameter style, batching).
- Namespace placement of the shared helper.
- Log-line phrasing as long as D-11 / D-19 content is present.

## Deferred Ideas (from scope-creep catches during discussion)

- Admin UI "Preview Seed merge" action — own phase.
- Log-viewer highlight for seed-merge lines — own phase.
- Per-field seeded marker — rejected via D-09; reconsider in future milestone
  if customers report confusion.
- PROJECT.md Key Decisions table update for Seed = field-level merge —
  milestone-transition task, not in Phase 39 itself.
