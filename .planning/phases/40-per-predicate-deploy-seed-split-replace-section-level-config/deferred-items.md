# Phase 40 Deferred Items

Out-of-scope discoveries during plan execution that are NOT auto-fixed
(per execute-plan.md SCOPE BOUNDARY rule). Logged for follow-up.

## From Plan 04 (docs/baseline rewrites)

### Legacy section-shape references in docs NOT in plan-04 scope

Plan 04's `files_modified` only lists `docs/baselines/Swift2.2-baseline.md` and
`docs/configuration.md`. Three additional doc files contain legacy
`deploy: { ... }` / `deploy.predicates` references that should also be
rewritten for consistency, but are out of scope for Plan 04:

- `docs/getting-started.md` lines 69, 81 — `"deploy": { ... }` / `"seed": { ... }` object example
- `docs/strict-mode.md` line 178 — example error message references `deploy.predicates`
- `docs/troubleshooting.md` lines 97, 149 — error message + JSON example reference legacy shape

These will produce stale-doc confusion if a user follows them after Phase 40 ships.
Suggest a follow-up plan in Phase 41 (or a Phase 40 hotfix plan if the team prefers
a single-shot phase closure) to sweep these three files into the new shape. Pattern
is mechanical and matches the rewrites already done in Plan 04 Task 3.

The legacy-shape error messages in strict-mode.md / troubleshooting.md are no
longer accurate either — Plan 01's ConfigLoader emits the new "Legacy section-level
shape detected" message instead. Updating them keeps the troubleshooting flow
useful for users hitting the rejection error.
