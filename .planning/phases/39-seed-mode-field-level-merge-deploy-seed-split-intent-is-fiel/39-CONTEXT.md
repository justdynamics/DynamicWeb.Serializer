---
phase: 39
phase_name: seed-mode-field-level-merge
gathered: 2026-04-22
status: Ready for planning
supersedes: Phase 37 D-06 (row-level skip semantics for ConflictStrategy.DestinationWins)
---

# Phase 39: Seed mode field-level merge — Context

<domain>
## Phase Boundary

**What this phase delivers:** Convert Seed mode (`ConflictStrategy.DestinationWins`)
in `ContentDeserializer` and `SqlTableProvider` from row/page-level skip to field-level
merge, so Deploy YAML (source-wins, with excludeFields) + Seed YAML (destination-wins,
empty excludeFields) combine cleanly on both fresh and re-deploy targets.

**Acceptance (from ROADMAP):** On a target where Deploy has already run, running Seed
populates the excluded fields (Mail1SenderEmail, error strings, branding) without
overwriting any field already set on target. Customer tweaks survive re-deploys
intrinsically — no persisted marker required.

**In scope:**
- `ContentDeserializer.DeserializePage` UPDATE path (lines ~684–692): replace the
  whole-page skip with per-field merge across scalars, ItemFields, PropertyItem
  fields, sub-object DTOs (Seo, UrlSettings, NavigationSettings, Visibility), and
  recursion into gridrows/columns/paragraphs.
- `SqlTableProvider.DeserializeCoreLogic` (lines ~313–322): replace the whole-row
  skip with per-column merge driven by a live read of target row state.
- **XML-element merge for SQL-column-hosted XML payloads** (scope expansion
  2026-04-22) — `PaymentGatewayParameters` on `EcomPayments`,
  `ShippingServiceParameters` on `EcomShippings`, and any other XML columns
  surfaced by SqlTable discovery. Merge per XML leaf element so Deploy-excluded
  elements (e.g. `Mail1SenderEmail`, `Mail1SenderName`) fill on Seed even when
  the hosting column is non-empty after Deploy.
- Shared `IsUnsetForMerge` helper used by both providers.
- Live Swift 2.2 → CleanDB E2E gate under `strictMode: true` proving
  Deploy → tweak → Seed preservation **including Mail1SenderEmail fill via
  XML-element merge**.

**Out of scope (supersedes Phase 37 D-06):** The row-level skip that Phase 37-01
shipped. Phase 37's `37-CONTEXT.md` stays as historical; this phase's decisions
are the new source of truth for Seed semantics.

</domain>

<decisions>
## Implementation Decisions

### Unset Detection (the core merge rule)

- **D-01 (baseline unset rule):** A target column/property is "unset" — and
  therefore eligible for Seed to fill — when its value is **NULL OR the type's
  default**. Defaults per type: `""` for strings, `0` for int/decimal, `false`
  for bool, `DateTime.MinValue` for DateTime, `Guid.Empty` for Guid, empty
  JSON object/array for JSON columns.
- **D-02 (ItemFields are strings):** DW persists every ItemField (Text, Long,
  List, File, Link, Checkbox, Date, …) as a string via `ItemService`.
  Compare at the string layer — fill when target value is `null` or `""`.
  This is the DW-specific specialization of D-01, not a contradiction.
- **D-03 (PropertyItem fields):** Treat PropertyItem fields (Icon, SubmenuType)
  identically to ItemFields — same NULL-or-empty-string rule.
- **D-04 (sub-object DTOs):** Seo, UrlSettings, NavigationSettings, Visibility
  are NOT atomic for merge purposes. Apply the unset rule per-property inside
  each sub-object (e.g. fill `Seo.MetaTitle` only if target `MetaTitle` is
  NULL/empty, independently of `Seo.MetaDescription`).

### Merge Scope on Content

- **D-05 (scalar scope):** All non-identity scalars on `Page` participate in
  field-level merge. **Always source-wins (never merged):** `PageUniqueId`,
  `AreaId`, `ParentPageId` — these are identity/structure, not content.
  Every other scalar the deserializer currently writes on UPDATE — `MenuText`,
  `UrlName`, `Active`, `Sort`, `ItemType`, `LayoutTemplate`,
  `LayoutApplyToSubPages`, `IsFolder`, `TreeSection`, plus all ~30 Phase-23
  properties — goes through the unset rule.
- **D-06 (permissions skipped):** Seed **never** touches existing page
  permissions. `_permissionMapper.ApplyPermissions` is bypassed entirely on
  the Seed UPDATE path. Deploy retains list-replace semantics unchanged.
- **D-07 (recurse into children):** When a parent page exists on target,
  Seed walks into its gridrows → columns → paragraphs and applies the same
  merge rule at each level. Matched entities (by UniqueId) merge per the
  unset rule; unmatched entities are **created**. This delivers per-paragraph
  ItemField fills (where "error strings" typically live).
- **D-08 (shared helper, separate write paths):** One `IsUnsetForMerge(value,
  typeHint)` helper encodes the D-01 rule. `ContentDeserializer` and
  `SqlTableProvider` each keep their own write path (DW entity setters +
  `ItemService.WriteItemField` vs. SQL UPDATE). The rule is shared; the
  mechanics diverge because the target APIs do.

### Re-run Idempotency

- **D-09 (no persisted marker):** Idempotency is intrinsic. Before each
  per-field write, the merge code reads the current target value and applies
  the unset rule. No `seeded_fields` table, no "was-seeded-by" column, no
  config-side checksum. A field Seed filled on run 1 reads as "set" on run 2.
- **D-10 (type-default overwrite is accepted):** A customer who explicitly
  set `Active=false` (or any bool to its default, int to `0`, etc.) **will
  lose** that value on the next Seed if YAML has a non-default value. This
  is the deliberate tradeoff for D-01's broader fill rule. Mitigations:
  (a) the field was Deploy-excluded, so Deploy itself considered it
  Seed-tweakable; (b) customer can re-tweak after first Seed runs.
- **D-11 (new log format):** Replace the current `"Seed-skip: [identity]
  (already present)"` line with a per-row merge summary:
  `"Seed-merge: [identity] — N fields filled, M left (already set)"`.
  The `Skipped` counter on `WriteContext` / `ProviderDeserializeResult`
  is repurposed to mean "all fields already set, no writes issued".
- **D-12 (schema drift inheritance):** Missing target columns silently drop
  from the merge set — inherits the Phase 37-02 `TargetSchemaCache` behavior
  already in both providers. No new strict-mode escalation for Seed
  specifically.

### SqlTableProvider Write Mechanism

- **D-17 (read-then-narrowed-UPDATE):** For each Seed row with identity
  match on target: SELECT the current row, compute the column subset where
  target is unset per D-01, issue a targeted `UPDATE [table] SET col=@p1,
  col=@p2, … WHERE [identity_predicate]` with only those columns. Rows
  without identity match still flow through `_writer.WriteRow` (MERGE insert).
- **D-18 (checksum-skip stays first):** The existing `existingChecksums`
  unchanged-skip path runs BEFORE field-level merge. If YAML row checksum
  equals target checksum, nothing to fill — fast-path skip. Only when
  checksums differ does the merge path engage.

### Test Strategy

- **D-13 (test shape):** Unit tests on the shared `IsUnsetForMerge` helper
  covering every type in D-01 (NULL, `""`, `0`, `false`, `DateTime.MinValue`,
  `Guid.Empty`, and positive "is-set" cases per type). Integration tests
  per provider covering the acceptance scenario: write a row/page with some
  fields set on target, run Seed YAML with different subset, assert per-field
  state matches expected merge outcome.
- **D-14 (TDD discipline):** Two plans, one per provider, each with
  RED/GREEN gates. Plan 39-01: Content merge (the shared helper + unit tests
  land here, reused by 39-02). Plan 39-02: SqlTable merge. Optional Plan
  39-03: live E2E gate.
- **D-15 (live E2E required):** Closure gate is a live Swift 2.2 → CleanDB
  round-trip under `strictMode: true` extending
  `tools/e2e/full-clean-roundtrip.ps1` with a Deploy → tweak → Seed
  sub-pipeline. Assert: (a) Mail1SenderEmail + other Seed-YAML-only fields
  are populated on target after Seed; (b) fields manually tweaked between
  Deploy and Seed are preserved byte-for-byte.
- **D-16 (Phase 37 D-06 treatment):** Leave `37-CONTEXT.md` untouched as the
  historical record. Phase 39 CONTEXT.md (this file) explicitly supersedes
  D-06 in its `supersedes:` frontmatter. No edits to archived phase
  artifacts.

### Observability

- **D-19 (dry-run per-field diff):** Dry-run mode prints per-field entries
  for every would-be fill:
  `"  would fill [col=MetaTitle]: target=NULL → seed='DW Swift 2.2'"`.
  Customers can audit exactly which fields Seed would touch before running
  for real. Matches existing Deploy dry-run verbosity.

### Scope Guardrail

- **D-20 (no admin UI changes):** Phase 39 is purely a deserialization-behavior
  change. Admin UI Deploy/Seed screens from Phase 37-01.1 stay as-is. No new
  buttons, no new tree nodes, no log-viewer overhaul. Telemetry/merge-preview
  UI ideas go to deferred.

### XML-Element Merge (Scope Expansion — 2026-04-22)

Added after RESEARCH.md surfaced that the acceptance scenario (fill
`Mail1SenderEmail` on Seed) is not deliverable via column-level merge alone —
`Mail1SenderEmail` is an XML leaf inside `PaymentGatewayParameters`
(`EcomPayments`) and `ShippingServiceParameters` (`EcomShippings`). User
approved expanding Phase 39 to add an XML-element merge layer so the headline
acceptance criterion is actually met.

- **D-21 (XML column inventory):** The SqlTable provider must identify which
  columns on in-scope tables hold XML payloads eligible for element-level
  merge. Minimum set the phase MUST cover: `EcomPayments.PaymentGatewayParameters`,
  `EcomShippings.ShippingServiceParameters`. Planner/researcher survey other
  SqlTable-participating tables for XML columns and include any Deploy-excludes
  an XML leaf. Discovery mechanism is planner's discretion (schema probe,
  per-config manifest, or declarative annotation on the config entry) —
  whatever fits the existing SqlTable config shape.
- **D-22 (XML-element unset rule):** An XML leaf element on target is "unset"
  — and therefore eligible for Seed to fill — when (a) the element is absent
  from the XML document, OR (b) the element is present with NULL / empty /
  whitespace-only text content. This mirrors D-01 applied at the element
  level rather than the column level. Attributes follow the same rule.
- **D-23 (XML merge mechanism):** For each Seed row where an XML column
  participates, parse both the YAML-sourced XML and the current target XML,
  walk the element tree, apply D-22 per leaf to decide fills, produce a merged
  XML document, and write it back as part of the narrowed-UPDATE column
  subset (D-17). The merged XML becomes the new column value. No partial-XML
  write tricks — one column-level write of the fully-merged document.
- **D-24 (preserve target-only elements):** Any XML element present on target
  but absent from Seed YAML stays on target untouched. Seed never strips XML
  elements; it only fills unset ones. Deploy's XML-element excludes already
  govern what Seed YAML contains vs. omits.
- **D-25 (test coverage for XML merge):** Unit tests on an `XmlMergeHelper`
  (or equivalent) covering: element-missing fills, element-present-empty
  fills, element-present-set skips, attribute-level merge, target-only
  preservation, nested-element merge, whitespace handling. Integration tests
  on SqlTableProvider round-trip covering `PaymentGatewayParameters` and
  `ShippingServiceParameters`. The D-15 E2E gate asserts `Mail1SenderEmail`
  appears in EcomPayments after Deploy → Seed.
- **D-26 (dry-run XML-element diff):** Extend D-19's per-field diff to XML
  columns — instead of `target=NULL → seed='DW...'` at the column level,
  emit per-element lines: `"  would fill [col=PaymentGatewayParameters,
  element=Mail1SenderEmail]: target=<missing> → seed='no-reply@swift.com'"`.
- **D-27 (shared XML-merge placement — Claude's Discretion):** Whether the
  XML-merge helper lives alongside `IsUnsetForMerge` in the same utility
  namespace, or in a sibling XML-specific type, is planner's call. It serves
  only `SqlTableProvider`'s write path — `ContentDeserializer` does not need
  it (DW content tables don't hold Deploy-relevant XML payloads).

### Claude's Discretion

- Exact shape of the `IsUnsetForMerge(value, typeHint)` signature — could be
  overloads per type, could be object + Type, could be a small visitor.
  Planner picks the C# idiom.
- Exact SQL for the narrowed UPDATE (parameterized vs. inlined column list,
  batching across multiple rows in one statement, etc.) — planner picks what
  fits `SqlTableWriter`'s existing shape.
- Whether the shared helper lives in `Configuration/`, `Providers/`, or a
  new `Merge/` namespace — planner chooses based on assembly layering.
- Phrasing and format of the new log lines, as long as D-11 / D-19 content
  is present.

### Folded Todos

None — no pending todos matched Phase 39 scope.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents (researcher, planner) MUST read these before planning or implementing.**

### Phase 37 Seed-mode history (superseded here)
- `.planning/phases/37-production-ready-baseline/37-CONTEXT.md` §D-01, §D-06 —
  Deploy/Seed config split and the (now-superseded) row-level skip decision.
- `.planning/phases/37-production-ready-baseline/37-01-PLAN.md` — original
  Seed-skip implementation landed by Phase 37-01.

### Implementation files to modify
- `src/DynamicWeb.Serializer/Serialization/ContentDeserializer.cs` — lines
  ~684–692 are the current `ConflictStrategy.DestinationWins` skip block in
  the `DeserializePage` UPDATE path; the merge replaces it.
- `src/DynamicWeb.Serializer/Providers/SqlTable/SqlTableProvider.cs` — lines
  ~313–322 are the current Seed-skip block; the merge replaces it (but
  preserve the checksum-skip at lines ~304–311 per D-18).
- `src/DynamicWeb.Serializer/Providers/SqlTable/SqlTableWriter.cs` — extend
  with a narrowed-UPDATE path (D-17); existing `WriteRow` stays for the
  non-merge cases.
- `src/DynamicWeb.Serializer/Configuration/ConflictStrategy.cs` — enum stays;
  only the **semantics** change.

### Config this phase unblocks
- `src/DynamicWeb.Serializer/Configuration/swift2.2-combined.json` — the
  combined Deploy+Seed predicate baseline that Phase 39 makes correct.

### XML-element merge targets (D-21 minimum set)
- `EcomPayments.PaymentGatewayParameters` — hosts `Mail1SenderEmail`,
  `Mail1SenderName`, and other Deploy-excluded mail-config leaves.
- `EcomShippings.ShippingServiceParameters` — same family of excluded leaves.
- Planner/researcher: survey the SqlTable config for any additional table+
  column pairs where Deploy `excludeFields` references an XML leaf. Any such
  column joins the D-21 inventory.

### Supporting infrastructure (already shipped, reused here)
- `src/DynamicWeb.Serializer/Schema/TargetSchemaCache.cs` (Phase 37-02) —
  schema-drift tolerance source of truth for D-12.
- `src/DynamicWeb.Serializer/Providers/Content/PagePropertyMapper.cs`
  (Phase 23) — the ~30 page properties that merge per D-05.
- `src/DynamicWeb.Serializer/Permissions/PermissionMapper.cs` (Phase 11/12)
  — bypassed on Seed UPDATE per D-06.
- `tools/e2e/full-clean-roundtrip.ps1` (Phase 38.1) — the pipeline extended
  for D-15's Deploy → tweak → Seed sub-run.

### Project-level context
- `.planning/PROJECT.md` Key Decisions — "Source-wins conflict strategy"
  row predates the Deploy/Seed split; Phase 39 formalizes Seed as
  "destination-wins via field-level merge". Planner may propose an update
  row on completion (not in Phase 39 scope itself).
- `docs/baselines/Swift2.2-baseline.md` — DEPLOYMENT / SEED / NOT-SERIALIZED
  three-bucket framing is the mental model this phase makes operational.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets

- **`existingChecksums` dict in `SqlTableProvider.DeserializeCoreLogic`** — already
  built before the Seed-skip block; keep it for the D-18 checksum fast-path.
- **`TargetSchemaCache`** — already tolerates missing columns on writes (Area
  + SqlTable paths). D-12 inherits this; no new schema-drift logic needed.
- **`SaveItemFields(itemType, itemId, fields, excludeFields)`** (ContentDeserializer)
  — current API writes the full field dict source-wins. Phase 39 needs either
  (a) a sibling `MergeItemFields` that reads current values first and filters,
  or (b) an extended overload with a predicate. Planner picks.
- **`SavePropertyItemFields`** (same file) — same shape, same extension pattern.
- **Dry-run path in `DeserializePage`** already logs per-property changes via
  `LogDryRunPageUpdate` — D-19's per-field diff extends this pattern to the
  merge case.

### Established Patterns

- **Mapper-per-domain** (PagePropertyMapper, PermissionMapper, ContentMapper):
  the shared `IsUnsetForMerge` helper fits naturally alongside these as a
  pure static utility, not a service.
- **`ConflictStrategy` threaded via `_conflictStrategy` field on
  ContentDeserializer + `strategy` parameter on SqlTableProvider**: no new
  plumbing — the merge branch fires under `== DestinationWins`.
- **Phase 37-03 RuntimeExcludes precedent** (small flat curated map) — if the
  merge helper ever needs type hints per column (it probably doesn't with
  D-02), this is the analogous shape.

### Integration Points

- **ContentDeserializer** — the merge branch replaces the 6-line skip block
  at ~684–692 with a ~20–40 line merge routine that calls into reused code
  for scalars / ItemFields / PropertyItem / sub-objects. Recursion into
  gridrows/paragraphs already exists (per D-07) — it's the field-write calls
  inside each level that need the merge predicate.
- **SqlTableProvider + SqlTableWriter** — two extension points: (i) the
  provider's Seed branch (replace row-skip with merge-plan construction);
  (ii) the writer (new `UpdateColumnSubset(table, identityPredicate,
  columns)` alongside existing `WriteRow`).
- **Logging / counters** — `ProviderDeserializeResult.Skipped` gets
  repurposed; callers (orchestrator, admin UI log viewer) need no change
  because the integer shape is preserved.

</code_context>

<specifics>
## Specific Ideas

- The acceptance scenario hinges on `Mail1SenderEmail`, `Mail1SenderName`,
  error-page strings, and branding text — all living in the swift2.2-combined
  Seed predicate's YAML and excluded from Deploy. These are the concrete
  round-trip fixtures for D-15's E2E gate.
- Paragraph-level "error strings" (from the Swift error pages) are a
  motivating example for D-07 recursion — they live several levels below the
  page scalar layer.
- The log-line pivot to `"Seed-merge: N filled, M left"` (D-11) should also
  appear in the `SerializerSerialize` / `SerializerDeserialize` admin-UI
  response summary, not just the per-row log.

</specifics>

<deferred>
## Deferred Ideas

- **Admin UI "Preview Seed merge" action** — a one-click dry-run with a
  field-level diff report surfaced in the UI. Real feature value but clearly
  new capability (own phase). Noted D-20.
- **Log-viewer highlight for seed-merge lines** — counter badge on merge
  summary rows in the Phase 16 log viewer. Also a UI polish phase.
- **Per-field seeded marker** (rejected via D-09 for Phase 39) — could be
  reconsidered in a later milestone if customers report confusion between
  "field was never seeded" vs. "customer cleared to default". Would need
  schema additions and migration tooling.
- **PROJECT.md Key Decisions table update** — flip or add a row for
  "Deploy = source-wins, Seed = field-level merge" once Phase 39 ships.
  Intentionally not in Phase 39 itself per GSD convention (decision-table
  updates happen at milestone transitions).

### Reviewed Todos (not folded)
None — no pending todos matched Phase 39 scope.

</deferred>

---

*Phase: 39-seed-mode-field-level-merge*
*Context gathered: 2026-04-22*
*Supersedes: Phase 37 §D-06*
