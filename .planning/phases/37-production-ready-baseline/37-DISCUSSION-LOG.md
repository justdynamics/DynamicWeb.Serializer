# Phase 37: Production-Ready Baseline — Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in 37-CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-04-20
**Phase:** 37-production-ready-baseline
**Areas discussed:** Seed mode • Registry overlap • Stale output cleanup • Plan 37-04 scope • Strict mode default • Template scope • Cross-env link resolution

**Context:** Discussion resumed from HANDOFF.json (paused 2026-04-17). Pre-work (ROADMAP.md, 4 phase plans, FINDINGS.md F-01..F-19, SEED-001, SEED-002) was already loaded. Seven gray areas had been pre-identified; user opted to discuss all seven.

---

## Seed mode (Area 1)

### 1a — Shape of the seed mechanism

| Option | Description | Selected |
|--------|-------------|----------|
| Per-predicate enum | Plan 37-01 choice: deserializeMode = SourceWins \| IfAbsent \| Skip per predicate | |
| Top-level seed: section | Separate seed: list in config runs first-deploy only | |
| Hash-based idempotence | Compare source hash to target's last-known source hash; preserve edits | |
| You decide | Claude picks | |
| **Other (user-authored)** | **Top-level Deploy / Seed split: separate predicate lists + exclusion configs + output folders per mode. Tree reflects the split. Serialize/Deserialize defaults to Deploy; Seed requires explicit flag.** | ✓ |

**User's choice:** Custom — top-level Deploy/Seed bifurcation.
**Notes:** User's phrasing: *"Deploy and seed should have their own top level predicate and exclusion config. This should be reflected in the tree. So Predicates are seperate for Deploy and seed visislbe in the top level config. Deploy will continue to use source-wins. Seed will need a default of destination-wins, so that already changed content has no risk of being overwritten. The Serialization folder for deploy and seed should be seperate, and the Serialize/Deserialze action should deault to deploy, seed can be triggered with a flag."*

### 1b — Default mode for Content predicates

**Answered via 1a:** Deploy default = source-wins (current behavior). Seed default = destination-wins (preserves customer-edited content on re-seed).

---

## Registry overlap — Runtime vs Credential (Area 2)

| Option | Description | Selected |
|--------|-------------|----------|
| Single registry with category tags | One ColumnsRegistry, each column tagged Runtime/Credential | |
| Credential wins | Drop from Runtime registry; credentials stricter | |
| Runtime wins | Drop from Credential registry; treat as env-config | |
| Keep both, split columns | Preserve two-registry split with explicit column assignment | |
| **Other (user-authored)** | **Drop the classification entirely. Single flat exclusion list. Env-config deployment is out of scope for Phase 37.** | ✓ |

**User's choice:** Scope reduction — no classification layer.
**Notes:** User's phrasing: *"I just want to exclude, there is no need for the serializer to classify data further. Config and per environment data will be handeled in a different way. for now, unless we find very important (and subject to change) items that must be part of this workflow, otherwise we can assume we only use yaml for a deploy and possibly a seed, not for env config deployment in this phase."*

Implication: Plan 37-03 (CredentialColumnsRegistry) significantly cut or deferred to v0.6.0. Plan 37-02 simplifies RuntimeColumnsRegistry to plain `excludeColumns`.

---

## Stale output cleanup (Area 3)

### 3a — Cleanup strategy

| Option | Description | Selected |
|--------|-------------|----------|
| Atomic dir swap | Write to SerializeRoot.new, rename | |
| **Files-written manifest** | **Track files emitted; delete non-manifest post-run** | **✓** |
| Leave cleanup to git | Overwrite only; user cleans manually | |

### 3b — SerializeRoot ownership

| Option | Description | Selected |
|--------|-------------|----------|
| **No — serializer owns it** | **Full ownership of output folder** | **✓** |
| Yes — may contain other files | Cleanup must preserve unknowns | |
| Unsure | Pick safer | |

**Notes:** User flagged that deploy/seed subfolders don't exist yet — creating them is part of this phase (ties to Area 1 D-03).

---

## Plan 37-04 scope triage (Area 4)

User pushed back on accepting 5 subsystems as-planned and asked to triage each against actual Swift 2.2 log evidence.

**Triage table presented:**

| # | Subsystem | Addresses | Evidence in test log | Decision |
|---|-----------|-----------|---------------------|----------|
| 1 | DwCacheServiceRegistry | F-10 | ~5-6 "Cache type not found" warnings | **Keep** |
| 2 | TemplateAssetManifest | F-15 | 3+ "template not found" warnings | **Keep** |
| 3 | StrictMode --strict | SEED-001 | Meta-feature | **Keep** |
| 4 | LINK-02 | F-07, F-17 | 10+ "Unresolvable page ID" warnings | **Keep** |
| 5 | BaselineDiffWriter | F-19 | Observability only | **Defer to v0.6.0** |

### 4a — Subsystems to keep (multiSelect)

**User's choice:** #1 + #2 + #3 + #4 (all except BaselineDiffWriter).

### 4b — Re-verify against Swift 2.2 host?

| Option | Description | Selected |
|--------|-------------|----------|
| **Log evidence is enough** | **Trust 2026-04-17 test log; nothing since has touched these paths** | **✓** |
| Spin up and re-run | Verify reproduction | |
| Selective re-verify | Ask per-claim | |

---

## Strict mode default (Area 5)

| Option | Description | Selected |
|--------|-------------|----------|
| Opt-in (SEED-001 spec) | Default off; --strict flag turns on | |
| **Default-on for API/CLI, opt-out for admin UI** | **Entry-point-split default** | **✓** |
| Default-on everywhere | Most aggressive | |

**Rationale:** CI/CD is the v0.5.0 target audience; API/CLI must fail pipelines. Admin UI devs experimenting want lenient default.

---

## Template asset scope (Area 6)

| Option | Description | Selected |
|--------|-------------|----------|
| **Manifest-only (plan's choice)** | **Scan + validate; templates ship via code deploy** | **✓** |
| Serialize templates too | Add Templates predicate; write cshtml/json into baseline | |
| Manifest + optional predicate | Hybrid | |

---

## Cross-env link resolution (Area 7)

### 7a — When/where to resolve SqlTable string columns

| Option | Description | Selected |
|--------|-------------|----------|
| At write, all varchar columns (plan choice) | Scan every varchar cell on MERGE/INSERT | |
| At write, per-predicate opt-in | Config declares columns | |
| **Pre-commit sweep** | **Validate at baseline creation; fail serialize if unresolvable** | **✓ (initially)** |

### 7b — Behavior on unresolved at deserialize

| Option | Description | Selected |
|--------|-------------|----------|
| **Warn + continue, strict = fail** | **Lenient default; strict escalates** | **✓** |
| Always fail | Any stale link blocks deploy | |
| Warn + null out | Blank broken link | |

### Clarification round — 7 scope

Claude flagged that pre-commit sweep (7a) and at-deserialize resolution address *different* problems:
- Sweep catches excluded-page refs at serialize time (baseline self-consistency).
- At-deserialize resolution handles source→target ID drift at deploy time.

| Option | Description | Selected |
|--------|-------------|----------|
| **Both — sweep at serialize + resolve at deserialize** | **Two complementary passes** | **✓** |
| Sweep only — defer cross-env drift | Accept broken links in v0.5.0 | |
| At-deserialize only (plan's original) | Skip sweep | |
| Rethink | Reconsider 7a | |

**Final:** Both passes in scope. 7b policy applies to the at-deserialize pass.

---

## Claude's Discretion

- Admin-UI tree wording for Deploy/Seed split — planner or UI-phase
- Exact Seed-trigger flag name (`--seed`, `--mode seed`, etc.)
- Manifest file shape (JSON sidecar vs `.serializer-manifest`)
- Cache registry keying (short-name vs full type name)
- Initial column set for LINK-02 at-deserialize pass

## Deferred Ideas (summary; full list in CONTEXT.md)

- Env-specific config / credentials deployment workflow → v0.6.0+
- CredentialColumnsRegistry concept → pending env-config workflow
- BaselineDiffWriter → v0.6.0 review UX
- Templates-as-files predicate → scope creep; revisit only if demanded
- Hash-based idempotence for Seed → rejected in favor of explicit split
- Pre-commit GitHub Action for serialize-sweep → v0.6.0 CI story
