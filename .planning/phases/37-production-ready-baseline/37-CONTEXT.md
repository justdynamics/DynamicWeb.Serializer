# Phase 37: Production-Ready Baseline — Context

**Gathered:** 2026-04-20
**Status:** Ready for planning (existing plans 37-01..37-04 need revision)

<domain>
## Phase Boundary

Deliver v0.5.0 — the YAML serializer is safe to run in an Azure CI/CD pipeline against both:
1. **Fresh target** (CleanDB) — deploys deployment-data (shop structure, payments, currencies) via `source-wins`
2. **Live target with customer edits** — deploys deployment-data without wiping customer-edited page content

**In scope:** Deploy/Seed config bifurcation, exclusion handling, stale-output cleanup, cache invalidation, cross-env link resolution, strict mode, template reference validation.

**Out of scope:** Env-specific config / credentials deployment (separate future workflow), baseline-diff review UX, serializing template files themselves.

</domain>

<decisions>
## Implementation Decisions

### Deploy / Seed Bifurcation (Area 1)

- **D-01:** Config gets two top-level sections: `Deploy` and `Seed`. Each has its own predicates list and exclusion config. No per-predicate `deserializeMode` enum — the split lives at config structure level, not per predicate.
- **D-02:** Admin UI tree reflects the split — Deploy and Seed are visible as distinct top-level nodes with their own predicate children.
- **D-03:** Separate serialize/deserialize output folders per mode (e.g. `Files/System/<OutputDirectory>/deploy/` and `Files/System/<OutputDirectory>/seed/`). These subfolders do not exist today — creating them is part of this phase.
- **D-04:** Serialize / Deserialize default action = **Deploy**. Seed requires an explicit flag (CLI flag, API parameter, or admin UI toggle).
- **D-05:** Deploy default mode = **source-wins** (YAML authoritative; preserves current v0.4.x behavior).
- **D-06:** Seed default mode = **destination-wins** (preserves customer-edited target content; don't overwrite on re-seed).

### Exclusion Model (Area 2)

- **D-07:** Drop the planned Runtime-vs-Credential classification entirely. Single flat exclusion concept — columns are simply excluded from serialization, no category tag.
- **D-08:** Exclusion applies per-predicate via existing `excludeColumns` / `excludeFields` / `excludeXmlElements` pattern. A small shared list of DW-universal excludes (e.g. runtime counters) may be auto-applied, but no class-level Runtime/Credential split.
- **D-09:** Environment-specific config and credential deployment is **out of scope for Phase 37**. YAML is only for Deploy + Seed. A separate workflow (future phase) handles env config.

### Stale Output Cleanup (Area 3)

- **D-10:** Serializer writes a **files-written manifest** per run, listing every YAML file emitted. After the run, any file in the output folder not in the manifest is deleted.
- **D-11:** Manifest applies independently per mode folder (Deploy manifest for Deploy folder, Seed manifest for Seed folder).
- **D-12:** Serializer owns the output folder. Non-serializer files in the managed folder are not expected; cleanup is not conservative about unknown files.

### Plan 37-04 Scope Triage (Area 4)

- **D-13:** Phase 37 keeps four of the originally-bundled subsystems:
  - **#1** DwCacheServiceRegistry (F-10) — cache invalidation post-deserialize
  - **#2** TemplateAssetManifest (F-15) — validation-only manifest
  - **#3** StrictMode `--strict` (SEED-001) — hard-fail on warnings
  - **#4** LINK-02 cross-env link resolution (F-07, F-17)
- **D-14:** **BaselineDiffWriter (F-19) is deferred to v0.6.0.** Pure observability / PR-review aid — no correctness impact.
- **D-15:** Plan split between 37-04 and any new 37-0X is the **planner's call** now that scope is clear. Area-2 reductions already shrink 37-03; area-4 drop of BaselineDiff + area-6 manifest-only framing keep the remaining work focused.

### Strict Mode Default (Area 5)

- **D-16:** `--strict` defaults differ by **entry point**:
  - API endpoints and CLI → default **ON** (CI/CD is the target audience; warnings must fail pipelines)
  - Admin UI → default **OFF** (devs experimenting iteratively; lenient is friendlier)
- **D-17:** Both paths can override the default via explicit flag/parameter.
- **D-18:** Strict mode escalates: unresolved links, missing templates, unresolvable cache-service names, and any future WARNING become hard failures.

### Template Asset Scope (Area 6)

- **D-19:** TemplateAssetManifest is **manifest-only**. It records which cshtml/json files the baseline references and validates their presence on target pre-deserialize. It does NOT serialize template file content into the baseline.
- **D-20:** Templates ship via code deploy, not via the YAML baseline.
- **D-21:** Under `--strict` (D-18), missing-template is a hard failure with source location. Otherwise, it's a WARNING.

### Cross-Environment Link Resolution (Area 7)

- **D-22:** LINK-02 runs **two complementary passes**:
  1. **Pre-commit sweep at serialize time** — scan every `Default.aspx?ID=N` reference in the YAML tree and validate it resolves to a page also in the baseline. Excluded-page refs and orphans caught before commit. Sweep failure aborts serialize.
  2. **At-deserialize resolution** — extend InternalLinkResolver to cover SqlTable string columns. Build source→target page-ID map during deserialize and rewrite references accordingly.
- **D-23:** Behavior when a link can't be resolved at deserialize:
  - Lenient (admin UI default): WARNING logged, row written as-is (broken link preserved)
  - Strict (API/CLI default, or explicit `--strict`): hard failure with source field location
- **D-24:** Pattern extends to other cross-env ID references as they're found (AreaId, CategoryId, etc.) — planner scopes the initial column set.

### Claude's Discretion

- Exact admin-UI wording for the Deploy/Seed tree split — planner or UI-phase decides.
- Exact naming of the explicit Seed flag (`--seed`, `--mode seed`, etc.) — planner's call.
- Exact shape of the files-written manifest (JSON sidecar vs `.serializer-manifest` file) — planner's call.
- Whether cache-registry maps by short name or full type name — planner's call given F-10 evidence.
- Initial set of columns scanned by LINK-02 at-deserialize (D-24) — planner scopes from F-07/F-17.

### Folded Todos

None — no pending todos matched Phase 37 scope.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase-Driving Findings
- `.planning/sessions/2026-04-17-baseline-test/FINDINGS.md` — F-01..F-19, the evidence base for every decision above. F-01 (seed/deploy split), F-04 (stale output), F-07/F-17 (link resolution), F-10 (cache registry), F-15 (templates).

### Existing Phase 37 Plans (treat as drafts; revise per these decisions)
- `.planning/phases/37-production-ready-baseline/37-01-PLAN.md` — was: per-predicate deserializeMode enum. Now: top-level Deploy/Seed split (D-01..D-06).
- `.planning/phases/37-production-ready-baseline/37-02-PLAN.md` — was: RuntimeColumnsRegistry. Now: simplified to flat excludeColumns per D-07/D-08. Includes manifest cleanup (D-10..D-12).
- `.planning/phases/37-production-ready-baseline/37-03-PLAN.md` — was: CredentialColumnsRegistry. Now: significantly cut or deferred to v0.6.0 per D-09.
- `.planning/phases/37-production-ready-baseline/37-04-PLAN.md` — was: 5 bundled subsystems. Now: 4 subsystems (drop BaselineDiff per D-14), planner re-splits per D-15.

### Seeds / Specs
- `.planning/seeds/SEED-001-strict-mode-deserialize.md` — strict mode spec; entry-point default split (D-16) modifies the opt-in-only framing.
- `.planning/seeds/SEED-002-sql-identifier-whitelist.md` — SQL identifier whitelist for any future `where` clause support; not in Phase 37 scope but referenced for future-proofing.

### Project-Level
- `.planning/PROJECT.md` — project vision, constraints.
- `.planning/ROADMAP.md` — v0.5.0 milestone goal, phase positioning.

### Memory (cross-session feedback)
- `memory/feedback_no_backcompat.md` — no backward compat required (0.x beta). Applied to D-05/D-06 (flip Seed default from SourceWins without backcompat concern), D-07 (drop registry split without migration path).
- `memory/feedback_content_not_sql.md` — Content tables must use ContentProvider, not SqlTable. Affects D-22/D-23 scope of at-deserialize resolution.
- `memory/project_baseline_workflow.md` — Swift 2.2 baseline + three-bucket split (DEPLOYMENT/SEED/ENVIRONMENT) is the canonical deployment pattern. D-01..D-06 formalize the DEPLOYMENT/SEED bucket split; ENVIRONMENT is the deferred bucket per D-09.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **InternalLinkResolver** (existing, from v0.3.1) — currently resolves `Default.aspx?ID=N` in ContentProvider page-item fields. D-22 pass 2 extends it to SqlTable string columns.
- **ContentProvider** — handles Area/Page/Paragraph/Item writes through DW C# APIs (SavePage, ItemService). Its write path is where at-deserialize link resolution hooks in.
- **SqlTableProvider / SqlTableWriter** — raw-SQL MERGE/INSERT. Post-D-22, needs a link-resolution pass on configured string columns before parameter binding.
- **DataGroupMetadataReader** — schema introspection. D-10 manifest writer plugs alongside existing file-emission path.
- **FlatFileStore** — YAML file I/O. Manifest sidecar (D-10) lives here.
- **ConfigLoader / ConfigWriter / SerializerConfiguration** — existing config model. D-01/D-02 adds top-level Deploy/Seed sections alongside current `Predicates` list.

### Established Patterns
- **Admin UI tree** — existing predicate tree renders `Predicates`. D-02 splits it into Deploy vs Seed parent nodes. Uses existing EditorBase / Dynamicweb.CoreUI patterns.
- **Exclusion fields** — `excludeFields`, `excludeXmlElements`, `ExcludeAreaColumns` (commit `c1658ed`). D-08 extends this consistent per-predicate pattern rather than introducing a registry.
- **Schema-tolerant writes** — `f0bfbba` added target-missing-column tolerance + type coercion for Area. Per F-12 / F-14 follow-up, pattern needs extension to Page/Paragraph/ItemType write paths (separate plan concern; touches D-22 pass 2).
- **Cache invalidation** — AreaService cache clear pattern (`project_dw_area_cache.md`). D-13 #1 generalizes this to a service registry.

### Integration Points
- **Config file** (`Serializer.config.json`) — new top-level sections Deploy / Seed (D-01). Backcompat: old flat `Predicates` can migrate to `Deploy.Predicates` at load time (acceptable per no-backcompat memory — or just break and rewrite).
- **SaveSerializerSettingsCommand** — needs to write the new structure.
- **Serialize / Deserialize entry points** (CLI + API + admin UI) — accept new mode flag (D-04). Strict-mode default varies by entry point (D-16).
- **Admin UI screens** — `SerializerSettingsScreen` tree updates for Deploy/Seed split.

</code_context>

<specifics>
## Specific Ideas

- User was explicit on Area 1 config shape: **"Deploy and seed should have their own top level predicate and exclusion config. This should be reflected in the tree. So Predicates are separate for Deploy and seed visible in the top level config."** — structural split at config layer, not a field on predicates.
- User was explicit on Area 2 scope reduction: **"I just want to exclude, there is no need for the serializer to classify data further. Config and per environment data will be handled in a different way."** — stay simple now; env-config is a different workflow.
- User wanted evidence-based triage on Area 4: **"I'm not sure all of these changes are needed, I want to discuss if we need any of these at all, and also check this based on actual data in the swift2.2 host."** — subsystems are justified via FINDINGS entries, not plan inertia. BaselineDiff dropped because F-19 is observability-only.
- User comfortable trusting existing log evidence (did not need re-run of Swift 2.2 host).

</specifics>

<deferred>
## Deferred Ideas

### To v0.6.0 or later
- **Env-specific config / credentials deployment workflow** — out of scope for Phase 37; separate future phase. Implied by D-09.
- **CredentialColumnsRegistry** as a first-class concept — deferred pending the env-config workflow above.
- **BaselineDiffWriter** — pure observability / PR review aid (F-19). Defer to v0.6.0 "review UX" themed milestone.
- **Templates-as-files predicate** — scope creep out of F-15. Keep manifest-only for v0.5.0; revisit if customer workflow demands templates in baseline.
- **Hash-based idempotence for Seed** — considered and rejected in favor of the explicit Deploy/Seed split. Could still be valuable for mixed-content predicates (future if needed).
- **Pre-commit GitHub Action** that runs serialize-sweep (D-22 pass 1) — CI automation, not needed for v0.5.0; fits v0.6.0 CI/CD story.

### Reviewed Todos (not folded)
None — no todos were matched.

</deferred>

---

*Phase: 37-production-ready-baseline*
*Context gathered: 2026-04-20*
