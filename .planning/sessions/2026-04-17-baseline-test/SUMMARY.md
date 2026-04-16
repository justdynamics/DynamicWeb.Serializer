# Session Summary — 2026-04-17 Autonomous Baseline Test

## TL;DR

Built a deployment-safe Swift 2.2 baseline (17 predicates, 1570 YAML files),
ran it end-to-end against CleanDB (frontend verified rendering), fixed 2
real bugs in-flight, planted a full improvement phase (v0.5.0) with 4 plans
covering 19 findings. Everything committed atomic on `main`.

## Deliverables

### D1 — Swift 2.2 deployment baseline

| Artifact | Path | Size |
|----------|------|------|
| Config | `src/DynamicWeb.Serializer/Configuration/swift2.2-baseline.json` | 17 predicates |
| Reasoning doc | `docs/baselines/Swift2.2-baseline.md` | ~300 lines |
| Serialize output | `baselines/Swift2.2/` | 1570 YAML files |
| Survey findings | `.planning/sessions/2026-04-17-baseline-test/SURVEY.md` | Full DB state snapshot |

Verified via round-trip: Swift 2.2 (localhost:54035) serialized → CleanDB
(localhost:58217) deserialized → `GET /Default.aspx?ID=9643` returns HTTP 200
with the Swift v2 template rendering Sign-in page correctly.

### D2 — v0.5.0 "Production-Ready Baseline" phase plan

Phase 37 (4 plans, wave-ordered):

1. **37-01** — Seed content mode + SqlTable row filtering + cross-env link audit
2. **37-02** — Schema/type resilience broadened + stale cleanup + runtime-column registry
3. **37-03** — Credential registry + SQL identifier whitelist + secret hygiene
4. **37-04** — Cache invalidation rewrite + template manifest + strict mode + baseline diff

Promotes SEED-001 (strict mode) and SEED-002 (SQL identifier whitelist) to
shipped work.

## Findings (F-01 .. F-19)

Full detail in `FINDINGS.md`. Headline gaps:

| # | Severity | Finding |
|---|----------|---------|
| F-01 | DESIGN | Seed-content vs deployment-data split — the central missing capability |
| F-02 | DESIGN | SqlTable predicates need `where` filter for mixed-content tables (AccessUser etc.) |
| F-04 | P1 | Serializer doesn't clean stale output from prior-config runs |
| F-05 | P1 | Credential columns (PaymentGatewayMD5Key etc.) leak into baseline |
| F-10 | P1 | Most cache service type names resolve to nothing (silent skip) |
| F-12 | FIXED | Area schema drift between source/target hard-fails content deserialize |
| F-14 | FIXED | YAML→SQL type coercion for datetime/bool/int was brittle |
| F-15 | P1 | Missing template files break pages silently |
| F-17 | P1 | Cross-env page ID references in SqlTable columns not resolved |
| F-18 | P2 | FK re-enable fails on pre-existing contamination (SHOP19 orphan) |
| F-19 | P2 | 1570-file baseline PRs aren't human-reviewable |

## Commits landed this session

```
a7dd8d3 plan: Phase 37 Production-Ready Baseline (v0.5.0, 4 plans)
a5d3738 docs: expand baseline-test findings (F-04..F-19)
f0bfbba fix: tolerate Area schema drift between source and target DBs
d1be892 baselines: Swift 2.2 serialize output (1570 files, 1555 rows, 17 predicates)
9aa8421 docs: Swift 2.2 baseline config + reasoning doc + survey findings
a3d3140 fix: warn when CacheInvalidator missing but predicate declares service caches
```

## Bugs fixed in-flight

- **`a3d3140`** — SerializerOrchestrator silently skipped cache invalidation
  when `_cacheInvalidator == null`. Now warns with a clear message naming
  the predicate.

- **`f0bfbba`** — ContentDeserializer hard-failed when Area schema differed
  between source and target DBs (`"Invalid column name 'AreaHtmlType'"`).
  Added `GetTargetAreaColumns()` schema cache and `CoerceForColumn()` for
  type conversion of datetime/bool/int strings. Plan 37-02 broadens this
  pattern to all raw-SQL write paths.

## Environment state

- Swift 2.2 host: stopped. Config deployed: `wwwroot/Files/Serializer.config.json`
  is now the baseline config. Original is at
  `wwwroot/Files/Serializer.config.json.pre-baseline-test`.
- CleanDB host: stopped. Has baseline YAML deserialized. Frontend verified
  rendering before shutdown.
- Deployed DLL: `DynamicWeb.Serializer.dll` built with today's fixes
  (`f0bfbba`), copied to both hosts' `bin/Debug/net10.0/`.

## Suggested next steps

1. **Review Phase 37 plans** — particularly the deserialize-mode design in 37-01.
   Does the `source-wins | if-absent | skip` enum cover your real cases, or
   do you want hash-based idempotence instead?

2. **Restore Swift 2.2 original config?** — the baseline config is now live in
   the instance. If you want to keep using Swift 2.2 for manual testing, restore:
   `mv wwwroot/Files/Serializer.config.json.pre-baseline-test wwwroot/Files/Serializer.config.json`

3. **Execute Phase 37** — `/gsd-execute-phase 37` will run all 4 plans
   wave-ordered, or review plan-by-plan first.

4. **Run /gsd-ship** on the current state — bugs and docs are committed and
   mergeable.
