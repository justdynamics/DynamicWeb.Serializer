---
phase: 38
slug: production-ready-baseline-hardening-retroactive-tests-for-37
status: verified
threats_open: 0
asvs_level: 1
created: 2026-04-21
---

# Phase 38 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| HTTP client → `/Admin/Api/SerializerSerialize`/`Deserialize` | Bearer-token-authenticated admin API; `Mode` now readable from query string in addition to JSON body. Same authZ gate as JSON body. | DeploymentMode enum, config payload |
| YAML baseline → ContentSerializer sweep | Baseline with unresolvable `Default.aspx?ID=N` refs can silently poison link integrity on deploy. BaselineLinkSweeper is the gate. | Serialized page IDs, paragraph IDs |
| `AcknowledgedOrphanPageIds` config entry → sweep bypass | Too-broad ack list can disable sweep protections. Per-predicate scope enforces surgical acks. | Config int list (now on ProviderPredicateDefinition only) |
| ContentDeserializer → Area SQL writer | `Area.AreaId` is identity column on fresh DBs; wrong wrapping regenerates IDs and breaks every FK. | SQL commands, identity-insert state |
| SQL cleanup script → Swift 2.2 source DB | Mutates ~77+ rows of source data. Must be reviewable, re-runnable, transactional. | Source-data string columns |
| FlatFileStore filename generation → filesystem | Filename collision = silent row loss. `SanitizeFileName` path-traversal guard preserved from Phase 37-05. | File paths |
| Local dev machine → SQL Server on localhost | Smoke tool uses Integrated Security — runs as current Windows identity. | Page IDs, URLs |
| Local dev machine → DW host over HTTPS | Smoke tool makes GET requests; `-SkipCertificateCheck` bypasses self-signed cert (localhost-only). | HTTP response bodies |
| Config file → runtime strict-mode behavior | Flipping default from OFF to ON escalates every existing warning into a fatal error. | Strict-mode setting |

---

## Threat Register

| Threat ID | Category | Component | Disposition | Mitigation | Status |
|-----------|----------|-----------|-------------|------------|--------|
| T-38-D1-01 | Tampering | `SerializerSerializeCommand.Handle()` query-param `?mode=X` | mitigate | `Enum.TryParse<DeploymentMode>` gate at `SerializerSerializeCommand.cs:41` + re-validation at `:60` after query-param fallback. Test 3 `Handle_InvalidMode_ReturnsInvalid` in `SerializerSerializeCommandTests.cs`. | closed |
| T-38-D1-02 | EoP | Admin API token surface | accept | Query-param fallback inherits bearer-token authZ (no new entry point). Documented in 38-01-PLAN.md threat_model + 38-01-SUMMARY.md. | closed |
| T-38-D2-01 | Information Disclosure | `result.Errors` list in response Message | accept | Cross-references Phase 37-04 T-37-04-02 prior acceptance of bearer-token-gated error surface. | closed |
| T-38-E2-01 | Information Disclosure | `docs/baselines/env-bucket.md` | mitigate | Lists only generic categories (payment gateway API keys, storage keys). Key Vault refs use syntax placeholder `@Microsoft.KeyVault(VaultName=<vault>;SecretName=<name>)`. Zero hardcoded secrets. | closed |
| T-38-01 | Repudiation | A.3 ConfigLoader migration path | mitigate | `ConfigLoader.cs:334` + `:341` emit `[Serializer] WARNING: deploy.acknowledgedOrphanPageIds ...` (and `seed.` variant) and DROP legacy values. Tests `Load_LegacyModeLevelAckList_LogsWarningAndDrops` + seed variant in ConfigLoaderTests.cs assert warning fires + values dropped. | closed |
| T-38-02 | EoP | A.1 ack-list as sweep bypass (malicious-ID path) | mitigate | Canonical home `ProviderPredicateDefinition.cs:95` (single occurrence); `ModeConfig.cs` zero matches (field removed); `ContentSerializer.cs:91-92` per-predicate `SelectMany(p => p.AcknowledgedOrphanPageIds)`. Test `Sweep_UnlistedOrphanId_Throws_EvenWhenOtherAcknowledged` in BaselineLinkSweeperAcknowledgmentTests.cs regression-guards unlisted-IDs-still-fail. | closed |
| T-38-03 | Integrity | A.2 IDENTITY_INSERT wrapping on Area create | mitigate | `ContentDeserializer.cs:481` `SET IDENTITY_INSERT [Area] ON` + `:488` `SET IDENTITY_INSERT [Area] OFF`. Test `AreaIdentityInsertTests.cs:105` ordered regex `RegexOptions.Singleline \| RegexOptions.IgnoreCase` validates ON→INSERT→OFF sequence. | closed |
| T-38-B5-01 | Tampering | B.5 paragraph-anchor regex scope | accept | `SelectedValuePattern` loop at `BaselineLinkSweeper.cs:167` intentionally unchanged per Plan-02 Pitfall 3; deferred to Phase 38.1 (item B.5.1). Documented in 38-02-SUMMARY.md key-decisions + 38-05-SUMMARY.md deferrals. | closed |
| T-38-B5-02 | Integrity | B.5 `validParagraphIds` collection walker | mitigate | `BaselineLinkSweeper.cs:48-49` + `:77` `CollectSourceParagraphIds` walker mirrors `InternalLinkResolver.CollectSourceIds`. 4 Facts in BaselineLinkSweeperParagraphAnchorTests.cs lock resolve/unresolve semantics. | closed |
| T-38-04 | Integrity / DoS | C.1 filename dedup algorithm | mitigate | `FlatFileStore.cs:138` monotonic-counter `for (int n = 1; n < 100_000; n++)`; `:145` throws on exhaustion. 3 Facts in FlatFileStoreDeduplicationTests.cs (multi-empty / single-unique / duplicate-named). Live E2E 2051→2051 preserved. | closed |
| T-38-B12-01 | Tampering | Dynamic SQL in `05-null-stale-template-refs.sql` | mitigate | `SET XACT_ABORT ON` + `BEGIN TRAN` wrapper. Template names hardcoded (`1ColumnEmail`, `2ColumnsEmail`, `Swift-v2_PageNoLayout.cshtml`). Bracket-escaped identifiers via INFORMATION_SCHEMA pattern. Mirrors Phase 37-accepted `01-null-orphan-page-refs.sql` shape. | closed |
| T-38-C1-01 | Information Disclosure | Filename leaking identity text | accept | Within trust boundary; Phase 37-05 T-37-05-01 path-traversal guard in `SanitizeFileName` preserved per Plan-03 scope note (W1). | closed |
| T-38-B4-01 | Availability | EcomShopGroupRelation FK write-order | investigate→resolved | 38-03-b4-investigation.md identifies root cause as source-data SHOP19 orphan (not write-order race). Fix deferred to Phase 38.1 via new `06-delete-orphan-ecomshopgrouprelation.sql` per escalation rule. Documented in 38-03-SUMMARY.md + deferred-items.md. | closed |
| T-38-B3-01 | Integrity | Schema-drift silent-skip in strict mode | investigate→resolved | 38-03-b3-investigation.md outcome-A: 3 Area columns are legacy DW core dropped from DW 10.24.7. Resolution = DW NuGet version alignment (customer-ops). env-bucket.md documents upgrade path; no code allowlist added. | closed |
| T-38-D3-01 | Tampering | Runaway redirect chain to arbitrary host | mitigate | `Test-BaselineFrontend.ps1:141` `-MaximumRedirection 5` caps redirect chase. `$HostUrl` is a local parameter, not sourced from env/config. | closed |
| T-38-D3-02 | Information Disclosure | Body excerpts in terminal output | accept | Local-dev only per D-38-13; body content not more sensitive than dev's browser access. Documented in 38-04-PLAN.md threat_model + tools/smoke/README.md:9 banner. | closed |
| T-38-D3-03 | EoP | Tool running against production | mitigate | `tools/smoke/README.md:9` `> **LOCAL-DEV ONLY.** ... NEVER deploy to customer sites`. Defaults to `https://localhost:58217` + Integrated Security. | closed |
| T-38-D3-04 | DoS | Unbounded page enumeration hammering target | mitigate | `Test-BaselineFrontend.ps1:143` `-TimeoutSec 30`; sequential `foreach` loop (not `ForEach-Object -Parallel`). 80-page baseline caps total runtime to ~40 min worst case. | closed |
| T-38-16-01 | Availability | strictMode flip without gate | mitigate (user override) | `swift2.2-combined.json` has zero matches for `strictMode.*false` or `acknowledgedOrphanPageIds`. 38-05-SUMMARY.md documents `gated-closed-on-38.1` disposition with 4 enumerated deferrals. 38-VERIFICATION.md explicitly records "user-approved gated-closed-on-38.1 disposition for SC 1 is treated as an override". See Accepted Risks Log below. | closed |
| T-38-16-02 | Integrity | `acknowledgedOrphanPageIds` removal premature | mitigate | 38-05-SUMMARY.md confirms B.5.1 SelectedValue paragraph-ID latent dependency surfaced when ack workaround was removed (Task 2 exercised the full path as intended). Plan-02 Pitfall 3 deferral propagated correctly. Mitigation fulfilled its surfacing purpose. | closed |
| T-38-16-03 | Repudiation | Commit without live E2E proof | mitigate | Commits `2c70fc2` (Task 1 config) + `177487c` (Task 2 E2E results) + `a787893` (Task 3 SUMMARY with `gated-closed-on-38.1` tag) record observed outcomes: B.5.1 root cause, B.4/B.3/GridRow deferrals, Phase 38 15/15 code-complete status. | closed |

*Status: open · closed*
*Disposition: mitigate (implementation required) · accept (documented risk) · investigate→resolved (classification after checkpoint outcome)*

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| AR-38-01 | T-38-D1-02 | Query-param `?mode=X` inherits same bearer-token authZ as JSON body; no new entry point. | Phase 38 planning (D-38-11) | 2026-04-21 |
| AR-38-02 | T-38-D2-01 | Bearer-token-gated error surface already accepted in Phase 37-04 T-37-04-02. | Phase 37-04 (prior acceptance) | 2026-04-21 |
| AR-38-03 | T-38-B5-01 | `SelectedValuePattern` paragraph-ID validation (B.5.1) intentionally scoped out of Phase 38; deferred to Phase 38.1 (~10 LOC). | User (Plan-02 Pitfall 3) | 2026-04-21 |
| AR-38-04 | T-38-C1-01 | Filename contains sanitized column values from within trust boundary; path-traversal guard from Phase 37-05 T-37-05-01 preserved. | Phase 37-05 (prior acceptance) | 2026-04-21 |
| AR-38-05 | T-38-D3-02 | Smoke tool is LOCAL-DEV ONLY; response bodies no more sensitive than dev's browser access. | D-38-13 | 2026-04-21 |
| AR-38-06 | T-38-16-01 | D-38-16 code change (strictMode default restored) committed despite final E2E surfacing 4 deferred items (B.5.1, B.4, B.3 wider, 142 GridRow NOT NULL). Real live-strict-mode pass gated on Phase 38.1 source-data cleanup. Recorded as `gated-closed-on-38.1` disposition. | User (during Phase 38 checkpoint) | 2026-04-21 |

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-04-21 | 21 | 21 | 0 | gsd-security-auditor (sonnet) |

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / investigate→resolved)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-04-21
