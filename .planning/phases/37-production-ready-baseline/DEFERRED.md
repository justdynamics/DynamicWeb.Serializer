# Phase 37 Deferred Requirements

These requirements were reviewed during Phase 37 planning and consciously deferred out of scope. They are NOT implemented in this phase. Each entry cites the CONTEXT.md decision that deferred it.

## CRED-01 — Credential column registry (deferred to v0.6.0)

**Deferral decision:** D-07, D-08, D-09

**Why deferred:** User decided the serializer should not classify data by category (Runtime vs Credential). A single flat exclusion concept via existing `excludeColumns` / `excludeFields` per-predicate pattern is sufficient for Phase 37. Environment-specific config and credential deployment is a separate workflow, not a Phase 37 problem.

**What replaces it in Phase 37:** Plan 37-03 ships a *small* shared list of universally-excluded DW columns (runtime counters; see RUNTIME-COLS-01) — but it does NOT carry a credentials-registry concept, and users must list payment/gateway credentials manually via `excludeFields` until v0.6.0 ships the env-config workflow.

**Re-open trigger:** v0.6.0 "environment-specific config and credentials" milestone. At that point, a first-class env-config workflow (separate from the Deploy/Seed YAML pipeline) should handle credentials, and CRED-01 can land as part of that design.

**Documentation requirement:** README must note "Credentials in payment/shipping rows are NOT auto-excluded in v0.5.0 — list them in each predicate's `excludeFields` until v0.6.0 ships the env-config workflow."

---

## DIFF-01 — BaselineDiffWriter (deferred to v0.6.0)

**Deferral decision:** D-14

**Why deferred:** Pure observability / PR-review aid per F-19. No correctness impact, no safety impact. User prefers to ship the correctness/safety pieces (SEED-01/SEED-02, SCHEMA-02, CACHE-01, STRICT-01, TEMPLATE-01, LINK-02) first, then circle back to review UX.

**Re-open trigger:** v0.6.0 "review UX" themed milestone — BaselineDiffWriter fits naturally alongside PR-template automation, pre-commit GitHub Action for serialize-sweep, and similar CI/CD polish items.

**Documentation requirement:** None in Phase 37. ROADMAP.md should list DIFF-01 under v0.6.0 when that milestone opens.

---

## Other deferrals captured in CONTEXT.md `<deferred>`

These are tracked in `37-CONTEXT.md` already; recording here for audit:

- **Env-specific config / credentials deployment workflow** — to v0.6.0 (implied by D-09, separate from CRED-01)
- **Templates-as-files predicate** — out of scope for v0.5.0 (per D-19/D-20, manifest-only)
- **Hash-based idempotence for Seed** — considered and rejected in favor of Deploy/Seed structural split
- **Pre-commit GitHub Action for serialize-sweep** — v0.6.0+ CI story

None of these are requirements from ROADMAP.md §Phase 37. They are future-work markers only.
