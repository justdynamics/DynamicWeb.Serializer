# Phase 38: Production-Ready Baseline Hardening — Research

**Researched:** 2026-04-21
**Domain:** DynamicWeb YAML serializer — retroactive tests, strict-mode gap closure, data-loss fix, API polish, docs
**Confidence:** HIGH (all findings verified against current HEAD source; one MEDIUM item flagged for D.1 due to opaque DW CommandBase binding)

## Summary

Phase 38 is tightly-scoped follow-up work against a well-known codebase — every one of the 14 backlog items maps to a specific, already-identified file and a specific, already-described behavior. There is no new domain to learn: the stack is xUnit 2.9.3 + Moq 4.20.72 on .NET 8.0, the source of truth for every fix is the CONTEXT.md D-38-01..D-38-16 decisions, and the Phase 37 summaries describe exactly what each piece is supposed to do. Research effort was spent on (a) tracing the exact call sites that change for each backlog item, (b) confirming the C.1 root cause (file-overwrite bug in `FlatFileStore.DeduplicateFileName`), and (c) making the B.1/B.2/B.3/B.4 investigations deterministic.

**Primary recommendation:** Execute the phase in the four waves defined by D-38-01, following the exact file lists below. The biggest technical risks are (1) A.2 integration test DB access — `ContentDeserializer.CreateAreaFromProperties` currently calls `Database.ExecuteNonQuery` directly with no seam, and (2) D.1 query-param binding — DW `CommandBase` public-property binding behavior is undocumented in the NuGet XML docs, so the planner should budget an investigation task. The C.1 root cause is now fully diagnosed from code inspection alone — no experimental runs needed.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

#### Scope shape
- **D-38-01:** Single phase with wave-grouped execution (NOT split into 38a/38b/38c). Waves by theme: (1) quick wins D.1+D.2+E.1+E.2 (2) retroactive tests A.1+A.2+A.3+B.5 (3) investigations B.1+B.2+B.3+B.4+C.1 (4) tooling D.3. Planner is free to parallelize within a wave and must leave inter-wave dependencies explicit.
- **D-38-02:** Do not defer any of the 14 items. User explicitly wants the full scope.

#### Group A — retroactive tests + code hygiene
- **D-38-03 (A.3):** `AcknowledgedOrphanPageIds` lives ONLY on `ProviderPredicateDefinition` (per-predicate). Remove the duplicate on `ModeConfig` and any ConfigLoader/ConfigWriter plumbing that reads/writes the mode-level list. Rationale: the sweep runs per Content predicate; different Content areas may have different known-broken refs; per-predicate matches the existing `ExcludeFields` / `XmlColumns` pattern. Migration: if any existing config has `deploy.acknowledgedOrphanPageIds`, ConfigLoader logs a warning and drops it (no back-compat needed per the beta-product feedback memory).
- **D-38-04 (A.1):** TDD tests for `AcknowledgedOrphanPageIds` must cover at minimum: (a) malicious-ID rejection when NOT in the list (sweep throws), (b) acknowledged-ID warning path (sweep logs but returns success), (c) strict-still-fatal-for-unlisted (unlisted ID throws even when another ID is acknowledged), (d) threat-model entry in the plan's `<threat_model>` block explaining why acknowledging is safer than disabling the sweep wholesale.
- **D-38-05 (A.2):** IDENTITY_INSERT integration test for `Area` create. Test must fail if the `SET IDENTITY_INSERT [Area] ON/OFF` wrapping is removed from `ContentDeserializer.CreateAreaFromProperties`. Use a real test DB (in-memory SQL or a disposable SqlServer testcontainer) with an identity-seeded Area table.

#### Group B — strict-mode gap closure
- **D-38-06 (B.1/B.2):** The 3 missing Swift templates (1ColumnEmail, 2ColumnsEmail, Swift-v2_PageNoLayout.cshtml) are stale data from an older Swift version. **Swift is installed via git-clone from https://github.com/dynamicweb/Swift, NOT a nupkg.** The upstream repo does not ship these templates either — confirming they're orphan references in Swift 2.2's source data. Treatment: (a) document the finding in `docs/baselines/env-bucket.md` (templates are filesystem concerns, per-env), (b) extend `tools/swift22-cleanup/` with a SQL script that nulls paragraph/item field references to these three template names so the Swift 2.2 baseline no longer emits them, (c) NOT expand TEMPLATE-01 scope (still manifest-only per 37-05 D-19/D-20).
- **D-38-07 (B.3):** 3 schema-drift Area columns (`AreaHtmlType`, `AreaLayoutPhone`, `AreaLayoutTablet`). CleanDB schema is older than Swift 2.2 schema for these columns. Phase 37-02's `TargetSchemaCache` already tolerates missing target columns with warnings. Confirm the pattern still fires correctly (it does — the E2E saw the warnings). Action in 38: (a) verify CleanDB is on the latest DW version (upgrade if behind), (b) if the drift is legitimate (columns exist in Swift 2.2 but not in base DW), ensure the serializer does NOT escalate these in strict mode — add a `knownEnvSchemaDrift` allowlist or similar if needed. (c) If CleanDB is just behind, the fix is operational, not code.
- **D-38-08 (B.4):** FK re-enable warning on `EcomShopGroupRelation -> EcomShops.ShopId`. Investigation only: (a) does it fire on a production deploy to a fresh Azure SQL, or only after a manual purge like the one in `tools/purge-cleandb.sql`? (b) If purge-only, add a post-purge fix to `purge-cleandb.sql`. (c) If production, investigate whether the SqlTable write order needs adjustment so EcomShops is populated before EcomShopGroupRelation.
- **D-38-09 (B.5):** `BaselineLinkSweeper` is over-strict on paragraph anchors. `Default.aspx?ID=X#Y` currently treats `X` AND `Y` as page IDs. Fix: collect `SerializedPage.SourceParagraphIds` during sweep setup, validate `#Y` against that set (not against page SourceIds). Any anchor pointing at a paragraph NOT in the serialized tree is still an orphan. Both parts (page + anchor) must resolve. No new config flag — this is a correctness fix, not a user toggle.

#### Group C — data-loss investigation + fix
- **D-38-10 (C.1):** Root-cause the EcomProducts 2051 -> 582 drop. Primary hypothesis (user-provided): empty-name products filtered silently. Diagnosis plan: (a) add an integration test that emits N SqlTable rows and asserts `SerializeResult.RowsSerialized == filesWritten == N`. (b) Run the Swift 2.2 serialize with additional logging to identify which rows are dropped. (c) Read `SqlTableWriter` and `SqlTableProvider.Serialize` to find the empty-name filter. (d) Decide: either fix so empty-name rows serialize with a synthetic filename (preserving all rows), OR make the filter explicit and loud (warn every dropped row). User prefers fix over warn-only.

#### Group D — API/UX polish
- **D-38-11 (D.1):** `?mode=seed` query param on SerializerSerialize/SerializerDeserialize must bind to the command's `Mode` property. Currently only JSON body works. Fix: either add `[FromQuery]` binding or (more likely) DW's CommandBase framework already supports it via a different convention — investigate and apply.
- **D-38-12 (D.2):** HTTP status code bug. Serialize returns HTTP 400 even on 0-errors because the result message always contains `Errors: ` as a literal string (empty list serializes with a trailing header). Fix: return 200 when errors list is empty, regardless of the message format. Add a test that 0-error serialize returns 200.
- **D-38-13 (D.3):** SerializerSmoke is NOT part of the serializer library. Ship under `tools/smoke/` (new folder) as a standalone console/PowerShell script. Hits every active page post-deserialize via the DW host's public URL, reports status code bucket counts, excerpts 5xx bodies. Local-dev only; never deployed to customer sites. This intentionally rules out the admin-UI-command and scheduled-task options.

#### Group E — docs
- **D-38-14 (E.1):** Extend `docs/baselines/Swift2.2-baseline.md`. Add a new section "Pre-existing source-data bugs caught by Phase 37 validators" documenting: (a) the 3 column-name mistakes that SqlIdentifierValidator catches, (b) the 5 orphan page IDs that BaselineLinkSweeper catches (2026-04-21 all cleaned by `tools/swift22-cleanup/`), (c) the 267 orphan-area pages + 238 soft-deleted pages removed by the cleanup scripts, (d) the `acknowledgedOrphanPageIds: [15717]` note for the paragraph-anchor false-positive until B.5 closes.
- **D-38-15 (E.2):** Create `docs/baselines/env-bucket.md`. Content: what is NOT in the baseline and why. Covers (a) `/Files/GlobalSettings.config` including Friendly URL `/en-us/` routing (the key finding from the 2026-04-20 E2E); (b) Azure Key Vault secrets (payment gateway credentials, storage keys); (c) per-env AreaDomain / AreaCdnHost / GoogleTagManagerID; (d) the Swift templates filesystem (git clone, not serialized); (e) pointers to Azure App Service config pattern. Target audience: a new customer adopting the baseline who needs to know "what do I configure per environment".

#### After Phase 38 closes
- **D-38-16:** Restore `strictMode` default to ON for API/CLI (per Phase 37-04 D-16 original intent), OFF for admin UI. Remove `"strictMode": false` from `swift2.2-combined.json`. This is the final step of Phase 38 — gates on B.1-B.5 + C.1 all closing first.

### Claude's Discretion

- Test framework + in-memory/testcontainer choice for A.2 integration test (project already uses xUnit + Moq; pick whichever is cleaner).
- SQL cleanup for B.1/B.2 — identify affected ItemType_Swift-v2_* tables/columns by scanning for references to the 3 template names; pattern matches `tools/swift22-cleanup/01-null-orphan-page-refs.sql`.
- Logging verbosity on C.1 diagnosis run; user said "likely empty name" so don't over-engineer.
- Tool language for D.3 smoke (PowerShell, .NET console, bash) — pick whatever integrates smoothest with the existing tooling directory on Windows + SQL Server stack.
- Exact form of A.3 migration warning in ConfigLoader when legacy mode-level `acknowledgedOrphanPageIds` is seen.
- Whether `knownEnvSchemaDrift` (D-38-07 option) is a new config field or just a documented pattern using existing excludes.

### Deferred Ideas (OUT OF SCOPE)

- **TEMPLATE-01 scope expansion** (capture email/grid-row/page-layout template *content* alongside the manifest) — explicitly deferred per D-38-06. If B.1/B.2 investigation reveals templates that should be serialized (customer-created vs Swift-shipped), file a Phase 39 item.
- **Validator upgrade for paragraph anchors in arbitrary other contexts** — B.5 covers `Default.aspx?ID=X#Y`. If `SelectedValue` in ButtonEditor JSON can ALSO carry `#anchor` suffixes, document and handle in a follow-up.
- **SerializerSmoke CI integration** — D-38-13 explicitly local-only. If a customer asks for CI smoke, build it then.
- **Automatic detection of stale Swift upstream drift** — if Swift 2.2 sources reference templates/pages that no longer exist in upstream Swift, could the serializer auto-detect and suggest cleanup? Out of scope for Phase 38.
</user_constraints>

<phase_requirements>
## Phase Requirements

REQUIREMENTS.md is not phase-indexed for Phase 38 (the file lists PAGE/ECOM/AREA/SCHEMA requirements owned by earlier phases). Phase 38 coverage is tracked via the 14 backlog items (A.1–A.3, B.1–B.5, C.1, D.1–D.3, E.1–E.2) plus closing step D-38-16. Mapping:

| Backlog ID | Description (from ROADMAP/CONTEXT) | Research Support |
|------------|-------------------------------------|------------------|
| A.1 | TDD unit tests for `AcknowledgedOrphanPageIds` | Per-predicate list exists on `ProviderPredicateDefinition.cs:95`; read path in `ContentSerializer.cs:87-121`; zero existing tests (grep confirms). Test fixture pattern in `BaselineLinkSweeperTests.cs` + use the per-predicate `AcknowledgedOrphanPageIds` round-trip path via `ContentProvider.BuildSerializerConfiguration`. |
| A.2 | TDD integration test for `IDENTITY_INSERT` wrapping on Area create | Fix site: `ContentDeserializer.cs:461-473` (`CreateAreaFromProperties`). Blocker: direct `Database.ExecuteNonQuery` call with no injection seam — requires a refactor-with-seam OR a live DW fixture. See "Open Questions — A.2 test strategy". |
| A.3 | Consolidate `AcknowledgedOrphanPageIds` to ONE location (ProviderPredicateDefinition) | Duplicate read sites: `ModeConfig.cs:46`, `ConfigLoader.cs:378/414/449/477`, `ConfigWriter.cs:53/76`, `ContentSerializer.cs:91/95/96`. Actively-read path is the mode-level one in ContentSerializer; predicate-level is piped INTO an inner Deploy mode via `ContentProvider.cs:224-234`. After A.3, ContentSerializer must read from `_configuration.Deploy.Predicates.SelectMany(p => p.AcknowledgedOrphanPageIds)` directly. |
| B.1/B.2 | SQL cleanup for 3 orphan templates | Pattern source: `tools/swift22-cleanup/01-null-orphan-page-refs.sql` (dynamic SQL over INFORMATION_SCHEMA). Affected tables: any `ItemType_Swift-v2_*` with string columns referencing `1ColumnEmail`, `2ColumnsEmail`, `Swift-v2_PageNoLayout.cshtml`. Swift 2.2 DB at port 54035. |
| B.3 | Schema drift on 3 Area columns | `TargetSchemaCache.LogMissingColumnOnce` already handles this (37-02); schema is YAML source (Swift 2.2) having 3 cols that CleanDB lacks. Resolution = upgrade CleanDB OR accept as drift. The serializer already skips these cleanly in lenient mode; in strict mode they escalate. |
| B.4 | FK re-enable warning EcomShopGroupRelation → EcomShops.ShopId | Write site: `SqlTableProvider.cs:241-243` (disable), `:329-331` (re-enable). In `swift2.2-combined.json`, EcomShops (line 86) is declared BEFORE EcomShopGroupRelation (line 95); the orchestrator's FK-ordering logic (`SerializerOrchestrator.cs:155-187`) re-sorts by `_fkResolver.GetDeserializationOrder`. Warning likely means that at the moment of the per-table FK-re-enable on EcomShopGroupRelation, there are rows with ShopIds pointing at ShopIds not YET in EcomShops (ordering race) OR that `tools/purge-cleandb.sql` already re-enables FKs with NOCHECK, leaving dangling state. |
| B.5 | BaselineLinkSweeper paragraph-anchor false-positive | `BaselineLinkSweeper.cs:33-34`: `InternalLinkPattern` regex already captures `#(\d+)` in group 4, but `CheckField` (line 118-123) only reads group 2 (the page ID) and ignores group 4. Fix: collect `SerializedParagraph.SourceParagraphId` into a second `HashSet<int>`; when a match has group-4 non-empty, validate anchor against paragraph set; fail only when NEITHER page nor paragraph resolves as expected. |
| C.1 | EcomProducts 2051 → 582 drop | **Root cause identified from code inspection:** `FlatFileStore.DeduplicateFileName` (`FlatFileStore.cs:120-134`) computes MD5 hash of `originalIdentity`. When N rows share the same identity (e.g., identical `ProductName` or all-empty `ProductName` = "" via `SqlTableReader.GenerateRowIdentity:58-62`), the MD5 hash is identical → all N rows produce the SAME `deduped = "_unnamed [abc123]"` filename → file overwrite. The dedup is correct for the rare case where two DISTINCT identities sanitize to the same string (different originals → different hashes), but WRONG for the common case of duplicate identities. 2051 → 582 = 1469 dropped ≈ consistent with many duplicate/empty `ProductName` values in Swift 2.2. |
| D.1 | `?mode=seed` query-param binding | Command: `SerializerSerializeCommand.cs:17` + `SerializerDeserializeCommand.cs:18`. Both extend `Dynamicweb.CoreUI.Data.CommandBase` with a public `string Mode` property. DW 10.8.4 Management API package XML docs do NOT document the binding convention; investigation + local test required. |
| D.2 | HTTP 400 on 0-error serialize | Status mapping: `SerializerSerializeCommand.cs:116`: `Status = result.HasErrors ? Error : Ok`. `result.HasErrors` is driven by `Errors.Count > 0 \|\| SerializeResults.Any(r => r.HasErrors)` (`SerializerOrchestrator.cs:361-364`). The literal string "Errors: " in the Summary appears only when `Errors.Count > 0` (`SerializerOrchestrator.cs:387-388`). The observed bug is most likely: serialize path is emitting a non-fatal error or a WARNING-escalated-to-error through strict mode. Fix approach in CONTEXT specifies returning 200 when errors.Count == 0 regardless of message format. |
| D.3 | Smoke tool `tools/smoke/` | New folder. Expected output buckets defined in CONTEXT §Specifics: 2xx count-only, 3xx capture final URL, 4xx first 500 chars, 5xx first 2000 chars + headers. Exit 0 iff no 5xx. |
| E.1 | Update `docs/baselines/Swift2.2-baseline.md` | Existing doc at `docs/baselines/Swift2.2-baseline.md` (253 lines, 3-bucket split documented). Add new section with the 3 column-name bugs + 5 orphan IDs + 267+238 cleanup counts + 15717 ack note. |
| E.2 | Create `docs/baselines/env-bucket.md` | New file. Covers GlobalSettings.config + Friendly URL (key E2E finding), Key Vault, per-env Area fields, Swift templates filesystem. |
| D-38-16 | Restore `strictMode` default ON | Remove `"strictMode": false` from `swift2.2-combined.json:5`. Final step; gates on all other items closing. |
</phase_requirements>

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| A.1/A.2 TDD tests | Test project (`DynamicWeb.Serializer.Tests`) | — | xUnit + Moq already established; new tests go next to existing BaselineLinkSweeperTests / ContentDeserializerAreaSchemaTests. |
| A.3 model consolidation | Models + Configuration | Serialization (ContentSerializer read path) | `AcknowledgedOrphanPageIds` is a data-model concern; only ContentSerializer reads it at sweep time. |
| B.1/B.2 SQL cleanup | Tools (`tools/swift22-cleanup/`) | — | Not a code concern; it's upstream data fixing, same bucket as the existing 01-null-orphan-page-refs.sql. |
| B.3 schema drift | Infrastructure (TargetSchemaCache) | Docs (env-bucket.md) | Serializer already tolerates it via warnings (37-02). Phase 38 work is documentation + possibly a config allowlist. |
| B.4 FK re-enable | Tools (`tools/purge-cleandb.sql`) | Providers/SqlTable (SqlTableProvider.cs) | Investigation first; likely a purge-tool fix since production fresh-deploy writes EcomShops before EcomShopGroupRelation per FK-ordering. |
| B.5 anchor sweep fix | Infrastructure (BaselineLinkSweeper) | Tests | Pure serializer-library correctness fix. Single file change + test fixture. |
| C.1 data-loss fix | Providers/SqlTable (FlatFileStore) | Tests (FlatFileStore integration) | Filename collision in `DeduplicateFileName`; fix the collision-enumeration algorithm. |
| D.1 query-param binding | AdminUI/Commands | — | CommandBase binding convention question, lives in the command files. |
| D.2 HTTP status | AdminUI/Commands | Providers (OrchestratorResult) | Status-mapping line in each command file. |
| D.3 smoke tool | Tools (`tools/smoke/` — new) | — | Explicit per D-38-13: separate tool, NOT part of the serializer library. |
| E.1/E.2 docs | Docs (`docs/baselines/`) | — | Pure markdown. |
| D-38-16 config flip | Configuration (swift2.2-combined.json) | — | One-line JSON edit after all gates closed. |

## Standard Stack

### Core (already established — no new deps)

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| xUnit | 2.9.3 | Unit test framework | [VERIFIED: `tests/DynamicWeb.Serializer.Tests/DynamicWeb.Serializer.Tests.csproj:14`] established convention across 620 existing tests |
| Moq | 4.20.72 | Mocking framework | [VERIFIED: test csproj:19] standard for IDataReader/ISqlExecutor mocks, e.g. `SqlTableWriterTests` |
| YamlDotNet | (pinned via `FlatFileStore`) | YAML serialize/deserialize | [VERIFIED: `FlatFileStore.cs:5-6`] already used for all provider I/O |
| Dynamicweb.CoreUI | 10.8.4 | Admin-UI command/screen framework | [VERIFIED: `src/DynamicWeb.Serializer/DynamicWeb.Serializer.csproj`] hosts `CommandBase` extended by SerializerSerializeCommand / SerializerDeserializeCommand |
| Dynamicweb.Data | 10.8.4 | `CommandBuilder` + `Database.ExecuteNonQuery` | [VERIFIED: used throughout `ContentDeserializer.cs`, `SqlTableWriter.cs`] T-SQL composition with parameter binding |
| .NET | 8.0 | Target framework | [VERIFIED: all csproj files] both test projects use net8.0, src uses net8.0 |

### Supporting (for Phase 38 specifically)

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| PowerShell 7 | (pre-installed) | D.3 smoke tool language | Windows-native; matches tone of `tools/purge-cleandb.sql` + `tools/swift22-cleanup/*.sql` (sqlcmd-driven) |
| sqlcmd | (pre-installed with SQL Server) | Running B.1/B.2 SQL scripts | Same pattern as existing `tools/swift22-cleanup/` scripts |

### No new NuGet packages needed

All 14 backlog items use existing dependencies. Do NOT add Testcontainers, FluentAssertions, Polly, or HttpClient wrappers for D.3 — the project has deliberately stayed dependency-lean. For D.3, `Invoke-WebRequest` (PowerShell built-in) is the right primitive.

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Moq IDataReader fake for A.2 | SQL LocalDB Testcontainer | Testcontainer gives real IDENTITY semantics but adds a dependency + Docker requirement. NOT recommended — see A.2 strategy below. |
| PowerShell for D.3 | .NET console app | .NET console has stronger typing but needs a build step. PowerShell is immediately runnable on Windows; the tool is local-dev only (D-38-13). |
| JSON-body-only D.1 | Accept-as-is | User explicitly rejected — the doc comments say `?mode=seed` works and it must. |

**No installation needed.** All dependencies already resolved in `packages.lock.json` / `csproj` references.

## Architecture Patterns

### System Architecture — Phase 38 data flow

```
┌─────────────────────────────────────────────────────────────────────┐
│  Swift 2.2 (port 54035)                                              │
│                                                                       │
│  ┌────────────────┐    ┌──────────────────────────────┐             │
│  │ Serializer API │───▶│ ContentSerializer.Serialize │             │
│  └────────────────┘    │   ├─ Walk pages              │             │
│         ▲               │   ├─ Build allSerializedPages│            │
│         │ POST          │   ├─ BaselineLinkSweeper ★B.5│            │
│         │ ?mode=deploy  │   │     (+ ack check ★A.1/A.3)           │
│         │               │   └─ TemplateAssetManifest                │
│         │               └──────────────┬───────────────┘            │
│         │                              │                             │
│         │                              ▼                             │
│         │               ┌──────────────────────────────┐             │
│         │               │ SqlTableProvider.Serialize   │             │
│         │               │   ├─ ReadAllRows             │             │
│         │               │   ├─ GenerateRowIdentity     │             │
│         │               │   └─ FlatFileStore.WriteRow ★C.1         │
│         │               └──────────────────────────────┘             │
└─────────┼────────────────────────────────────────────────────────────┘
          │
          │ YAML tree (baselines/Swift2.2)
          ▼
┌─────────────────────────────────────────────────────────────────────┐
│  CleanDB (port 58217)                                                │
│                                                                       │
│  ┌────────────────┐    ┌──────────────────────────────┐             │
│  │ Deserialize    │───▶│ ContentDeserializer          │             │
│  │ ?mode=seed ★D.1│    │   ├─ TemplateAssetManifest ★B.1/B.2       │
│  └────────────────┘    │   │     validate (warn 3 missing)          │
│         │               │   ├─ CreateAreaFromProperties ★A.2         │
│         │               │   │     (SET IDENTITY_INSERT wrap)         │
│         │               │   ├─ TargetSchemaCache ★B.3                │
│         │               │   │     (skip 3 drift cols)                │
│         │               │   └─ SavePage                              │
│         │               └──────────────┬───────────────┘            │
│         │                              ▼                             │
│         │               ┌──────────────────────────────┐             │
│         │               │ SqlTableProvider.Deserialize │             │
│         │               │   ├─ FK disable/re-enable ★B.4            │
│         │               │   ├─ MERGE rows                            │
│         │               │   └─ Strict warnings accumulate            │
│         │               └──────────────┬───────────────┘            │
│         │                              ▼                             │
│         │               ┌──────────────────────────────┐             │
│         │               │ Result → HTTP status ★D.2    │             │
│         │               └──────────────────────────────┘             │
│         │                                                             │
│         └─────▶ tools/smoke ★D.3 hits every active page              │
└─────────────────────────────────────────────────────────────────────┘

★ = Phase 38 intervention point
```

### Recommended Project Structure (additions only — everything else exists)

```
tests/DynamicWeb.Serializer.Tests/
├── Infrastructure/
│   ├── BaselineLinkSweeperTests.cs       # existing — extend for B.5
│   └── BaselineLinkSweeperAckTests.cs    # NEW — A.1 TDD tests (3+ cases)
├── Providers/SqlTable/
│   └── FlatFileStoreDedupTests.cs        # NEW — C.1 regression test
├── Serialization/
│   └── ContentDeserializerIdentityInsertTests.cs  # NEW — A.2
└── AdminUI/
    └── SerializerCommandQueryBindingTests.cs      # NEW — D.1 + D.2

tools/
├── swift22-cleanup/
│   ├── 00-backup.sql                     # existing
│   ├── 01-null-orphan-page-refs.sql      # existing
│   ├── 05-null-orphan-template-refs.sql  # NEW — B.1/B.2
│   └── 99-verify.sql                     # existing
└── smoke/                                 # NEW — D.3
    ├── Test-BaselineFrontend.ps1          # main script
    └── README.md

docs/baselines/
├── Swift2.2-baseline.md                   # existing — extend for E.1
└── env-bucket.md                          # NEW — E.2
```

### Pattern 1: TDD test-seam via ctor injection (applies to A.1, A.2, C.1)

**What:** Tests drive-through production classes by injecting a fake collaborator rather than hitting a live DB.
**When to use:** Any test that would otherwise require `Database.ExecuteNonQuery` or a live Dynamicweb context.
**Example:** existing pattern already in use.

```csharp
// Source: SqlTableWriterTests.cs + SqlTableWriter(ISqlExecutor) ctor
// BaselineLinkSweeperTests.cs uses no seam — it's already pure.
[Fact]
public void Sweep_AcknowledgedId_LogsButDoesNotThrow()
{
    // Build a single Content predicate whose AcknowledgedOrphanPageIds = [15717]
    var predicate = new ProviderPredicateDefinition
    {
        Name = "Content", ProviderType = "Content", AreaId = 1, Path = "/",
        AcknowledgedOrphanPageIds = new List<int> { 15717 }
    };
    var logged = new List<string>();
    // ... construct tree with orphan ref 15717, invoke through ContentSerializer
    // Assert: no throw; logged contains "acknowledged orphan ID 15717"
}
```

### Pattern 2: Dynamic SQL cleanup via INFORMATION_SCHEMA (applies to B.1/B.2)

**What:** Iterate all `ItemType_Swift-v2_%` tables looking for string columns referencing target values; UPDATE to clear.
**When to use:** Data-cleanup scripts where the specific column is not deterministic across Swift versions.
**Example:** existing `tools/swift22-cleanup/01-null-orphan-page-refs.sql:59-67`.

```sql
-- Source: tools/swift22-cleanup/01-null-orphan-page-refs.sql
DECLARE @sql NVARCHAR(MAX) = N'';
SELECT @sql = @sql
    + N'UPDATE [' + c.TABLE_NAME + N'] SET [' + c.COLUMN_NAME + N'] = '''' '
    + N'WHERE CAST([' + c.COLUMN_NAME + N'] AS NVARCHAR(MAX)) LIKE ''%1ColumnEmail%'';' + CHAR(10)
FROM INFORMATION_SCHEMA.COLUMNS c
WHERE c.TABLE_NAME LIKE 'ItemType_Swift-v2_%'
  AND c.DATA_TYPE IN ('nvarchar', 'ntext', 'varchar', 'nchar');
EXEC sp_executesql @sql;
```

Adjust for each of the 3 orphan template names. The script belongs at `tools/swift22-cleanup/05-null-orphan-template-refs.sql`.

### Pattern 3: Filename collision algorithm (applies to C.1)

**Current buggy pattern:**

```csharp
// Source: FlatFileStore.cs:120-134
// BUG: for N rows with identical originalIdentity, `deduped` is the same for all N.
private static string DeduplicateFileName(string sanitized, string originalIdentity, HashSet<string>? usedNames)
{
    if (usedNames == null) return sanitized;
    if (usedNames.Add(sanitized)) return sanitized;  // 1st: "_unnamed"
    var hash = MD5(originalIdentity);                 // identical for all empty-name rows
    var deduped = $"{sanitized} [{hash[..6]}]";       // identical for all empty-name rows
    usedNames.Add(deduped);                           // HashSet.Add returns false; no error
    return deduped;                                    // returned N times → file overwrite
}
```

**Corrected pattern** — enumerate a monotonically increasing suffix until `usedNames` accepts:

```csharp
// Recommended fix for C.1
private static string DeduplicateFileName(string sanitized, string originalIdentity, HashSet<string>? usedNames)
{
    if (usedNames == null) return sanitized;
    if (usedNames.Add(sanitized)) return sanitized;

    // Same identity seen before — append numeric suffix until unique. This preserves
    // sort order of multiple empty-name / duplicate-name rows without relying on MD5.
    var hashPrefix = Convert.ToHexString(
        MD5.HashData(Encoding.UTF8.GetBytes(originalIdentity))).ToLowerInvariant()[..6];
    for (int n = 1; n < 100_000; n++)
    {
        var candidate = $"{sanitized} [{hashPrefix}-{n}]";
        if (usedNames.Add(candidate)) return candidate;
    }
    throw new InvalidOperationException(
        $"Exhausted 100000 filename variants for identity '{originalIdentity}' — refuse to silently drop rows.");
}
```

**Why this shape:** preserves deterministic output (first empty-name row → `_unnamed [abc123-1]`, second → `_unnamed [abc123-2]`, …), keeps DoS guard (fail loud at 100K rather than infinite loop), preserves current behavior for the distinct-originals-same-sanitize case (each gets a different hash prefix so they don't collide with the enumerated variants).

### Pattern 4: Paragraph-anchor regex + lookup (applies to B.5)

**Current bug — only page ID is validated, anchor ignored:**

```csharp
// Source: BaselineLinkSweeper.cs:33-34, 118-123
private static readonly Regex InternalLinkPattern = new(
    @"(Default\.aspx\?ID=)(\d+)(#(\d+))?",       // group 4 = paragraph anchor (optional)
    RegexOptions.IgnoreCase | RegexOptions.Compiled);

foreach (Match m in InternalLinkPattern.Matches(value))
{
    if (!int.TryParse(m.Groups[2].Value, out var id)) continue;
    if (validIds.Contains(id)) { resolved++; continue; }
    unresolved.Add(...);  // group 4 never examined — false-positive path
}
```

**Recommended fix:**

```csharp
// 1. Collect SerializedParagraph.SourceParagraphId into a second HashSet<int> during Sweep setup
private static void CollectSourceParagraphIds(IEnumerable<SerializedPage> pages, HashSet<int> acc)
{
    foreach (var p in pages)
    {
        foreach (var row in p.GridRows)
            foreach (var col in row.Columns)
                foreach (var para in col.Paragraphs)
                    if (para.SourceParagraphId.HasValue) acc.Add(para.SourceParagraphId.Value);
        CollectSourceParagraphIds(p.Children, acc);
    }
}

// 2. In CheckField, read group 4 if present and validate:
if (!validIds.Contains(id)) { unresolved.Add(...); continue; }
if (m.Groups[4].Success && int.TryParse(m.Groups[4].Value, out var paraId))
{
    if (!validParagraphIds.Contains(paraId))
    {
        // Option: emit an UnresolvedLink record with Kind="ParagraphAnchor"
        unresolved.Add(new UnresolvedLink(sourceIdent, fieldName, paraId, m.Value));
        continue;
    }
}
resolved++;
```

The `SerializedParagraph.SourceParagraphId` field already exists (`Models/SerializedParagraph.cs:6`) and is populated by `ContentMapper.cs:248`. No new model changes.

### Anti-Patterns to Avoid

- **Disabling the sweep wholesale for acknowledged orphans.** Current correct pattern (per-predicate whitelist) is surgical; removing the sweep entirely would regress Phase 37-05.
- **Changing `SerializeResult.HasErrors` semantics to fix D.2.** Don't overload the boolean — fix the specific status-mapping branch. `result.HasErrors` is used in 5+ places for other correctness checks.
- **Using Testcontainers/Docker for A.2.** Increases CI friction and test runtime; the project has intentionally avoided this. Prefer a test-seam refactor: introduce an `ISqlExecutor` injection point for `ContentDeserializer`'s Area write paths (mirrors `SqlTableWriter(ISqlExecutor)` pattern).
- **Auto-migrating legacy `deploy.acknowledgedOrphanPageIds` into per-predicate.** Per D-38-03: log a warning and DROP the list; do NOT silently rewrite into every predicate (would be lossy — which predicate owns which ID?).
- **Fixing C.1 in `SanitizeFileName` instead of `DeduplicateFileName`.** The sanitize step turning "" into "_unnamed" is correct and not the bug — the bug is the dedup algorithm assuming distinct originals.
- **Adding a new serializer config flag for B.5.** Per D-38-09: "no new config flag — this is a correctness fix, not a user toggle."

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Paragraph-ID lookup for B.5 | New paragraph-ID collector/index | Existing `SerializedParagraph.SourceParagraphId` + walk pattern from `InternalLinkResolver.CollectSourceParagraphIds` | Already built in `InternalLinkResolver.cs:161-187`; copy the walker shape. |
| Test DB for A.2 | LocalDB / Testcontainers | `ISqlExecutor` seam refactor (same shape as `SqlTableWriter`) | Zero new deps; matches existing pattern; makes the same test pattern apply to future Area-write regressions. |
| MD5 collision fix for C.1 | New hash algorithm or GUID suffixes | Monotonic counter `[hash-N]` | GUIDs make filenames non-deterministic across runs; counters preserve sort order and are trivially testable. |
| HTTP client for D.3 | `HttpClient` + custom retry logic | PowerShell `Invoke-WebRequest` | Built-in, no dependency, correct threat model for a local-only dev tool. |
| Query-string parsing for D.1 | Manual `Request.QueryString` walking | `CommandBase` public-property binding convention (investigate first) | DW is the framework owner; work WITH its convention. See Open Questions. |
| New BaselineLinkSweeper SweepResult shape | Backwards-incompatible change | Extend `UnresolvedLink` or add `UnresolvedParagraph` variant | Existing 13 tests passing; keep them green. |

**Key insight:** Every Phase 38 item has an existing mirror in Phase 37's codebase. Use existing patterns, don't invent new ones.

## Runtime State Inventory

Phase 38 is **not** a rename/refactor/migration phase — it's feature hardening. This section captures the non-code state that the phase MUST understand to execute correctly.

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| **Stored data** | Swift 2.2 DB at `C:\Projects\Solutions\swift.test.forsync\Swift2.2\Dynamicweb.Host.Suite` (port 54035, SQL Server `Swift-v2.2`) contains 77+ paragraph/item-field refs to 3 orphan template names (B.1/B.2) — identical shape to the 5 orphan page IDs already cleaned by `01-null-orphan-page-refs.sql`. | B.1/B.2: new cleanup SQL in `tools/swift22-cleanup/05-null-orphan-template-refs.sql`. |
| **Stored data** | CleanDB at port 58217, `Swift-CleanDB` catalog. Missing 3 Area columns (AreaHtmlType, AreaLayoutPhone, AreaLayoutTablet) that Swift 2.2 has. | B.3: either upgrade CleanDB schema (operational) OR document + add known-drift allowlist (code). |
| **Stored data** | `baselines/Swift2.2/_content/Swift 2/area.yml` lines 43/58/59 already contain the 3 drift columns as empty strings. | No action — YAML is source-of-truth and the serializer tolerates target-missing; see 37-02. |
| **Live service config** | `swift2.2-combined.json:5` has `"strictMode": false`. | D-38-16: flip to remove this line after all other items close. |
| **Live service config** | `swift2.2-combined.json:15` has `"acknowledgedOrphanPageIds": [15717]` on the Content predicate. | No removal during Phase 38 — stays until B.5 closes, then E.1 documents that it can be removed. |
| **OS-registered state** | None. Phase 38 touches no Windows Task Scheduler, launchd, pm2, or systemd state. | None. |
| **Secrets/env vars** | None directly changed. E.2 will document where per-env secrets live (Azure Key Vault). | None — documentation only. |
| **Build artifacts** | Test assemblies at `tests/*/bin/Debug/net8.0/`. | Rebuild after each task. |
| **External host state** | Swift 2.2 + CleanDB hosts need the serializer DLL deployed (see REPORT.md reproduce step 1). Any plan task that claims a live E2E run MUST rebuild + redeploy first. | Planner: include an explicit "deploy DLL" step in D-38-16 verification. |

**Why listed despite not being a rename phase:** B.1/B.2 and B.3 are data/schema concerns that look like code fixes but are actually data-cleanup + operational concerns. Listing them here prevents the planner from writing them as pure code tasks.

## Common Pitfalls

### Pitfall 1: A.2 test coupled to live DW context
**What goes wrong:** A naive A.2 integration test tries to call `ContentDeserializer.Deserialize()` end-to-end against a real DW stack. It fails in CI because `Dynamicweb.Data.Database.ExecuteNonQuery` requires a full DW context (web.config, provider, initialization).
**Why it happens:** `CreateAreaFromProperties` at `ContentDeserializer.cs:473` calls `Database.ExecuteNonQuery(cb)` with no injection seam. Existing `ContentDeserializerAreaSchemaTests` test only the schema-drift logic, not the actual SQL execution.
**How to avoid:** Introduce an `ISqlExecutor` parameter on `ContentDeserializer` (default = live `Database.ExecuteNonQuery` wrapper). Test with a Moq `ISqlExecutor` and assert the captured `CommandBuilder` text contains `SET IDENTITY_INSERT [Area] ON` and `SET IDENTITY_INSERT [Area] OFF`. Same shape as `SqlTableWriterTests` (which already does this for the MERGE case).
**Warning signs:** Any test that directly references `Dynamicweb.Data.Database` or tries to construct a real `CommandBuilder` without a mock executor.

### Pitfall 2: A.3 silent data loss on migration
**What goes wrong:** Per-predicate list silently overwrites `deploy.acknowledgedOrphanPageIds` (or vice versa) during ConfigLoader migration, losing IDs.
**Why it happens:** D-38-03 requires REMOVAL of the mode-level list. A "merge" implementation is tempting but ambiguous — which predicate inherits the mode-level IDs?
**How to avoid:** Per D-38-03: `ConfigLoader` sees `deploy.acknowledgedOrphanPageIds` → log the warning verbatim from CONTEXT §Specifics → drop the list entirely. Document in PLAN that any migration user must manually move IDs onto the right predicate. User has explicitly signed off via D-38-03.
**Warning signs:** ConfigLoader code that reads `raw.Deploy.AcknowledgedOrphanPageIds` and writes it back into `deploy.Predicates[0].AcknowledgedOrphanPageIds`.

### Pitfall 3: B.5 regex matches non-page `SelectedValue` with `#`
**What goes wrong:** The fix for `Default.aspx?ID=X#Y` accidentally also matches `SelectedValue` JSON with a `#` suffix, creating false negatives in other contexts.
**Why it happens:** Two regexes scan the same string fields — `InternalLinkPattern` (Default.aspx form) and `SelectedValuePattern` (JSON form, `SelectedValuePattern` at line 37-39 does NOT currently capture `#` suffixes). Extending only `InternalLinkPattern` is the correct surgical fix.
**How to avoid:** Scope the B.5 fix to `InternalLinkPattern` only. Leave `SelectedValuePattern` untouched. Document in the plan that ButtonEditor JSON `#anchor` suffixes (if they exist) are a deferred concern per CONTEXT §Deferred.
**Warning signs:** Changing `SelectedValuePattern` regex in the same commit as B.5.

### Pitfall 4: C.1 fix introduces non-determinism
**What goes wrong:** Using `Guid.NewGuid()` or a timestamp to guarantee filename uniqueness produces different YAML filenames on every run — breaks git-diff workflows.
**Why it happens:** The MD5-of-originalIdentity approach was chosen originally for determinism but broke for duplicate identities. Jumping to GUIDs/timestamps is a plausible-sounding but wrong overcorrection.
**How to avoid:** Use monotonic counter `[hash-N]` keyed on enumeration order within the usedNames collection. Sort input rows by primary key before write so the N is deterministic run-to-run. (Current code already iterates `rows` in DB read order — which for SQL Server without ORDER BY is _not_ deterministic. Consider adding `ORDER BY` to `SqlTableReader.ReadAllRows` as part of the C.1 fix if reproducibility across envs is a stated requirement; flag as a micro-decision for the planner.)
**Warning signs:** Any suggestion to use GUIDs, timestamps, or `DateTime.Now` in filename generation.

### Pitfall 5: D.1 investigation loops without a test
**What goes wrong:** Planner spends hours probing DW `CommandBase` binding behavior without writing a regression test that locks the fix in.
**Why it happens:** The DW 10.8.4 XML docs for `Dynamicweb.Management.Api` don't document the binding convention (grepped `Dynamicweb.Management.Api.xml` — only 1 ancillary mention of `DataController.CommandPartsRegex`). The codebase doesn't contain any existing query-param-binding examples on `CommandBase` to copy.
**How to avoid:** The plan's first task for D.1 should be "write a failing test that exercises both JSON body and `?mode=seed`" — let the fix shape itself as the simplest thing that passes. Likely first attempt: ensure `Mode` has a public setter (it does) + test via curl locally; if query binding doesn't work, fall back to reading `Dynamicweb.Context.Current.Request["mode"]` inside `Handle()` before the enum parse.
**Warning signs:** A D.1 task that decompiles `Dynamicweb.Management.Api.dll` before running a 2-line local curl test.

### Pitfall 6: B.4 "fix" actually masks a real production bug
**What goes wrong:** B.4 adds workarounds to `purge-cleandb.sql` without first confirming that production fresh-deploys don't hit the same issue.
**Why it happens:** The warning shows up after the test-only purge script runs, tempting a purge-only fix. But SqlTableProvider re-enables FKs per-table, per-run; a production fresh-deploy with EcomShopGroupRelation having rows from source but EcomShops not yet populated (despite FK ordering) would hit the same issue — and there's no purge in the production path.
**How to avoid:** Per D-38-08, the investigation MUST run a fresh-Azure-SQL deploy first (or equivalent: fully-empty CleanDB without running purge-cleandb.sql) to see whether the warning fires. If YES → code fix in SqlTableProvider. If NO → purge script fix only.
**Warning signs:** A B.4 task that jumps straight to editing `tools/purge-cleandb.sql` without the "production reproduction" step.

## Code Examples

### Example 1: A.1 test using ContentSerializer path

```csharp
// Source: pattern from BaselineLinkSweeperTests.cs (pure) + adapted for per-predicate ack
// Target file: tests/DynamicWeb.Serializer.Tests/Infrastructure/BaselineLinkSweeperAckTests.cs
[Fact]
public void ContentSerializer_AcknowledgedId_LogsWarningAndSucceeds()
{
    var logged = new List<string>();
    var config = new SerializerConfiguration
    {
        OutputDirectory = "X",
        Deploy = new ModeConfig
        {
            Predicates = new List<ProviderPredicateDefinition>
            {
                new() {
                    Name = "P", ProviderType = "Content", AreaId = 1, Path = "/",
                    AcknowledgedOrphanPageIds = new List<int> { 15717 }
                }
            }
        }
    };
    // Construct a fake SerializedPage tree containing a Default.aspx?ID=15717 orphan.
    // Use a fake IContentStore so Serialize doesn't touch the filesystem.
    // ... invoke serializer.Serialize() via ContentSerializer's ctor that takes a log callback
    Assert.Contains(logged, l => l.Contains("acknowledged orphan ID 15717"));
    // Assert: no InvalidOperationException thrown.
}

[Fact]
public void ContentSerializer_UnlistedId_Throws_EvenWhenOtherAcknowledged()
{
    // predicate.AcknowledgedOrphanPageIds = [15717]
    // tree contains refs to 15717 AND to 9999 (NOT acknowledged)
    Assert.Throws<InvalidOperationException>(() => serializer.Serialize());
}

[Fact]
public void ContentSerializer_NoAckListSet_Throws_ForAnyOrphan()
{
    // predicate.AcknowledgedOrphanPageIds is empty
    // tree contains ref to 15717
    Assert.Throws<InvalidOperationException>(() => serializer.Serialize());
}
```

**Note on test entry point:** ContentSerializer currently reads the ack list from `_configuration.Deploy.AcknowledgedOrphanPageIds` (mode-level). After A.3 it reads from `_configuration.Deploy.Predicates.SelectMany(p => p.AcknowledgedOrphanPageIds)`. The A.1 tests should be written AGAINST the post-A.3 API — run A.3 first in the same wave.

### Example 2: A.2 test using ISqlExecutor seam

```csharp
// Target file: tests/DynamicWeb.Serializer.Tests/Serialization/ContentDeserializerIdentityInsertTests.cs
[Fact]
public void CreateAreaFromProperties_WrapsInsertInIdentityInsertOnOff()
{
    var capturedCommands = new List<string>();
    var fakeExecutor = new Mock<ISqlExecutor>();
    fakeExecutor.Setup(e => e.ExecuteNonQuery(It.IsAny<CommandBuilder>()))
        .Callback<CommandBuilder>(cb => capturedCommands.Add(cb.ToString()));

    var deserializer = new ContentDeserializer(
        /* existing params */,
        sqlExecutor: fakeExecutor.Object);

    // Simulate Area-not-found path that triggers CreateAreaFromProperties.
    // ... invoke via a minimal SerializedArea fixture

    Assert.Contains(capturedCommands,
        c => c.Contains("SET IDENTITY_INSERT [Area] ON") && c.Contains("SET IDENTITY_INSERT [Area] OFF"));
}
```

The prerequisite is the `ISqlExecutor` refactor on `ContentDeserializer` (same seam as `SqlTableWriter(ISqlExecutor)` at `SqlTableWriter.cs:29`). This is a single-ctor-parameter change with a default that preserves the current production path.

### Example 3: B.5 paragraph anchor validation

```csharp
// Source: replacement for BaselineLinkSweeper.cs CheckField + Sweep
public SweepResult Sweep(List<SerializedPage> allPages)
{
    var validPageIds = new HashSet<int>();
    var validParagraphIds = new HashSet<int>();
    CollectSourceIds(allPages, validPageIds);
    CollectSourceParagraphIds(allPages, validParagraphIds);
    // ... (rest unchanged)
}

private static void CollectSourceParagraphIds(IEnumerable<SerializedPage> pages, HashSet<int> acc)
{
    foreach (var p in pages)
    {
        foreach (var row in p.GridRows)
            foreach (var col in row.Columns)
                foreach (var para in col.Paragraphs)
                    if (para.SourceParagraphId.HasValue) acc.Add(para.SourceParagraphId.Value);
        CollectSourceParagraphIds(p.Children, acc);
    }
}

// In CheckField:
foreach (Match m in InternalLinkPattern.Matches(value))
{
    if (!int.TryParse(m.Groups[2].Value, out var pageId)) continue;
    if (!validPageIds.Contains(pageId))
    {
        unresolved.Add(new UnresolvedLink(sourceIdent, fieldName, pageId, m.Value));
        continue;
    }
    // Page resolved — now validate the optional #paragraph anchor.
    if (m.Groups[4].Success && int.TryParse(m.Groups[4].Value, out var paraId)
        && !validParagraphIds.Contains(paraId))
    {
        unresolved.Add(new UnresolvedLink(sourceIdent, fieldName, paraId, m.Value));
        continue;
    }
    resolved++;
}
```

### Example 4: D.1 query-param binding investigation

```bash
# Step 1: confirm current behavior. Token acquired per REPORT.md.
TOKEN=$(curl -sk -X POST https://localhost:54035/Admin/TokenAuthentication/authenticate \
  -H "Content-Type: application/json" -d '{"username":"Administrator","password":"Administrator1"}' | jq -r .token)

# Baseline (works):
curl -sk -X POST "https://localhost:54035/Admin/Api/SerializerSerialize" \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"Mode":"seed"}'

# Test (currently fails per D-38-11):
curl -sk -X POST "https://localhost:54035/Admin/Api/SerializerSerialize?mode=seed" \
  -H "Authorization: Bearer $TOKEN"

# Step 2: if query-param doesn't bind, add a fallback inside Handle():
# (hypothetical — exact API TBD during investigation)
public override CommandResult Handle()
{
    if (string.IsNullOrEmpty(Mode) || Mode == "deploy")
    {
        // Fallback: read from DW's request context if query-string provided it.
        var fromQuery = Dynamicweb.Context.Current?.Request?["mode"];
        if (!string.IsNullOrEmpty(fromQuery)) Mode = fromQuery;
    }
    // ... existing logic
}
```

Note: `Dynamicweb.Context.Current` is the standard DW accessor but should be confirmed during Task-1 investigation. If `CommandBase` has a native `[FromQuery]` equivalent, prefer it.

### Example 5: D.3 smoke tool shape (PowerShell)

```powershell
# Source: tools/smoke/Test-BaselineFrontend.ps1 (NEW)
param(
    [string]$Host = 'https://localhost:58217',
    [string]$ConnectionString = 'Server=localhost\SQLEXPRESS;Database=Swift-CleanDB;Integrated Security=true;TrustServerCertificate=true',
    [int]$AreaId = 3,
    [string]$LangPrefix = '/en-us'
)

# Enumerate active pages via SQL (Friendly URL pattern from REPORT.md).
$rows = Invoke-Sqlcmd -ConnectionString $ConnectionString -Query @"
SELECT PageID, PageMenuText, PageUrlName
FROM Page
WHERE PageAreaID = $AreaId AND PageActive = 1
"@

$buckets = @{ '2xx' = @(); '3xx' = @(); '4xx' = @(); '5xx' = @() }
foreach ($r in $rows) {
    $url = "$Host$LangPrefix/$($r.PageUrlName)"
    try {
        $resp = Invoke-WebRequest -Uri $url -SkipCertificateCheck -MaximumRedirection 5
        $code = $resp.StatusCode
    } catch {
        $code = $_.Exception.Response.StatusCode.value__
    }
    $bucket = switch ($code) { {$_ -lt 300} {'2xx'}; {$_ -lt 400} {'3xx'}; {$_ -lt 500} {'4xx'}; default {'5xx'} }
    $entry = [PSCustomObject]@{ PageId=$r.PageID; Url=$url; Code=$code }
    if ($bucket -eq '5xx') { $entry | Add-Member -Name BodyExcerpt -Value ($resp.Content[0..2000] -join '') -MemberType NoteProperty }
    elseif ($bucket -eq '4xx') { $entry | Add-Member -Name BodyExcerpt -Value ($resp.Content[0..500] -join '') -MemberType NoteProperty }
    $buckets[$bucket] += $entry
}

Write-Host "Summary: 2xx=$($buckets['2xx'].Count) 3xx=$($buckets['3xx'].Count) 4xx=$($buckets['4xx'].Count) 5xx=$($buckets['5xx'].Count)"
if ($buckets['5xx'].Count -gt 0) {
    $buckets['5xx'] | ConvertTo-Json -Depth 3 | Write-Host
    exit 1
}
exit 0
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| MD5-of-identity-only dedup (broken for N>1 duplicates) | Monotonic-counter dedup `[hash-N]` | Phase 38 (C.1 fix) | Fixes 1469-row silent data loss in EcomProducts baseline. |
| Mode-level AcknowledgedOrphanPageIds | Per-predicate AcknowledgedOrphanPageIds only | Phase 38 (A.3) | Single source of truth; aligns with ExcludeFields/XmlColumns pattern. |
| BaselineLinkSweeper validates page ID only, ignores `#paragraph` | Validates both page + paragraph anchor | Phase 38 (B.5) | Removes the one remaining false-positive (ID 15717) from Swift 2.2 baseline. |
| Serializer exits HTTP 400 when message contains "Errors:" literal | Exits HTTP 200 when errors.Count == 0 | Phase 38 (D.2) | Correct HTTP semantics for CI pipelines. |
| strictMode: false on Swift 2.2 baseline (workaround) | strictMode default ON via D-16 entry-point precedence | Phase 38 (D-38-16) | Restores original Phase 37-04 intent. |

**Deprecated/outdated:**
- Post-Phase-38: any reference to `ModeConfig.AcknowledgedOrphanPageIds` in documentation. Replaced by per-predicate docs.
- Post-Phase-38: `swift2.2-combined.json` has no `acknowledgedOrphanPageIds` and no `strictMode: false` override (both removed).

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | DW `CommandBase` public-property binding does NOT bind query-string values by default for POST requests | D.1 section | Low — the CONTEXT stated this as observed behavior; if binding works we've written unnecessary code. |
| A2 | The FK re-enable warning on EcomShopGroupRelation fires ONLY after the aggressive `tools/purge-cleandb.sql` purge, NOT on a fresh Azure SQL deploy | Pitfall 6, B.4 | Medium — if it also fires in production, the fix site is SqlTableProvider (different tasks). D-38-08 says "investigation only" so this is expected to be resolved before coding. |
| A3 | The 3 orphan template names (1ColumnEmail, 2ColumnsEmail, Swift-v2_PageNoLayout.cshtml) appear ONLY in `ItemType_Swift-v2_*` string columns | B.1/B.2 pattern | Low — they could also appear in `Paragraph.ItemType` or `Page.Layout` columns. The dynamic-SQL cleanup pattern from 01-null-orphan-page-refs.sql already scans ItemType_Swift-v2_*; planner should verify by grepping Area-level layouts + Page.Layout if the 05- script doesn't fully clear the warnings. |
| A4 | The 1469-row drop in EcomProducts is attributable primarily to filename collision on duplicate/empty ProductName values (the C.1 hypothesis) | C.1 root cause | Medium — another plausible cause is `SkipOnUnchanged` (existing-checksum match) but that path is deserialize-only. The planner's first task under C.1 should VERIFY with the integration test before committing to the fix. |
| A5 | `tools/smoke/` as PowerShell is the lowest-friction tool language for Windows + SQL Server | D.3 | Low — if the planner's team strongly prefers .NET console or bash, either works. Per user discretion. |
| A6 | `SerializedParagraph.SourceParagraphId` is populated for every paragraph in a Swift 2.2 serialize run (not just some) | B.5 fix | Low — confirmed by `ContentMapper.cs:248` setting it unconditionally from `paragraph.ID`. |

**Confirmation plan for A1–A6:** A1 and A4 have explicit investigation tasks in the plan; A2 is a D-38-08 investigation; A3 will surface via the "verify after cleanup" step (mirroring `01-null-orphan-page-refs.sql:71-82`); A5 is user discretion; A6 is verified from code inspection alone.

## Open Questions

1. **A.2 test strategy: ISqlExecutor refactor vs live DW fixture.**
   - What we know: `ContentDeserializer.CreateAreaFromProperties:473` calls `Database.ExecuteNonQuery` directly. No existing `ISqlExecutor` parameter.
   - What's unclear: Whether to (a) introduce `ISqlExecutor` on ContentDeserializer (mirrors SqlTableWriter — ~20 LOC refactor), (b) test against a live CleanDB instance, or (c) add an in-memory SQLite adapter.
   - Recommendation: (a). Matches existing Phase 37 patterns; keeps CI test-time fast; the seam is useful for future Area-write tests.

2. **D.1 exact fix form.**
   - What we know: Current `Mode` property is a public-setter string on `CommandBase`. Only JSON body works today per REPORT.md.
   - What's unclear: Whether DW's `CommandBase` framework has a native `[FromQuery]` attribute, a convention for reading `Dynamicweb.Context.Current.Request["mode"]`, or requires manual investigation.
   - Recommendation: First task writes the failing test (curl with `?mode=seed`), second task applies whichever fix is shortest. If no native convention exists, fall back to `Dynamicweb.Context.Current.Request["mode"]` inside `Handle()` before the enum parse.

3. **B.3 resolution: upgrade CleanDB or add allowlist.**
   - What we know: 3 Area columns (AreaHtmlType, AreaLayoutPhone, AreaLayoutTablet) exist in Swift 2.2 but not CleanDB. Baselines YAML at `baselines/Swift2.2/_content/Swift 2/area.yml:43/58/59` has them as empty strings already.
   - What's unclear: Whether these columns are present in the current DW 10.8.4 NuGet schema — if yes, CleanDB is just on an older DW version (operational fix); if no, these are Swift-specific schema extensions (code allowlist fix).
   - Recommendation: Planner's first B.3 task queries the DW 10.8.4 schema definition (via `INFORMATION_SCHEMA` on a fresh 10.8.4 install, or by grepping the DW NuGet's SQL migration scripts) before deciding fix shape. If operational: document the CleanDB upgrade in env-bucket.md (E.2). If code: add `knownEnvSchemaDrift` config (new field on ModeConfig or Content predicate).

4. **C.1 scope: also add ORDER BY to SqlTableReader?**
   - What we know: Current `SqlTableReader.ReadAllRows` does `SELECT * FROM [{table}]` with no ORDER BY. Row order is non-deterministic on SQL Server.
   - What's unclear: Whether run-to-run determinism of YAML filenames across environments is a hard requirement for the baseline workflow, or nice-to-have.
   - Recommendation: Scope the C.1 fix tightly to DeduplicateFileName; add `ORDER BY [key columns]` to ReadAllRows only if it's free (one-line change) and doesn't regress any existing tests. Mark as a "quality improvement" in the PLAN not a requirement.

5. **D.2 real root cause.**
   - What we know: User's description (CONTEXT D-38-12) says the bug is the literal `"Errors: "` string being present. Code inspection shows the literal only appears when `Errors.Count > 0`.
   - What's unclear: Whether the observed HTTP 400 came from a different code path (e.g., a framework-level middleware that scans the response body for "Errors:"), or whether the description is imprecise and the actual trigger is `result.HasErrors` becoming true via a non-fatal path.
   - Recommendation: Plan's first D.2 task reproduces the HTTP 400 via curl against a clean CleanDB + empty Serializer state; log the full orchestrator result to determine whether `Errors.Count > 0` OR whether a framework layer is interposing. Fix should be the specific condition that produced the status flip.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 8 SDK | all code tasks | ✓ (presumed — csproj target framework) | 8.0 | — |
| SQL Server (Swift 2.2) | B.1/B.2 data cleanup, C.1 diagnosis, D.1 live test, D.3 | ✓ | localhost, port 54035 | — |
| SQL Server (CleanDB) | A.2 integration test (if live), B.3/B.4 investigation, D.3 target | ✓ | localhost, port 58217 | — |
| Swift 2.2 host running | C.1 diagnosis serialize run, D.1 investigation | ✓ (start manually if stopped) | port 54035 | — |
| CleanDB host running | D.3 smoke verification | ✓ (start manually if stopped) | port 58217 | — |
| PowerShell 7 | D.3 tool | ✓ (Windows built-in) | 7.x | `bash` + `curl` (second choice) |
| `Invoke-Sqlcmd` module | D.3 page enumeration | — need verify | — | `sqlcmd.exe` CLI |
| `curl` + `jq` | manual E2E verification | ✓ (used in REPORT.md) | — | — |
| xUnit runner | all TDD tasks | ✓ (pre-installed with VS Test Platform) | — | — |
| git | all | ✓ | — | — |
| `dotnet build` | all code tasks | ✓ | — | — |

**Missing dependencies with no fallback:** None.

**Missing dependencies with fallback:** `Invoke-Sqlcmd` module — if not installed, D.3 script can fall back to `sqlcmd.exe -Q` + parse output manually.

## Validation Architecture

Per `.planning/config.json`, `nyquist_validation: true`. This section is required.

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 (+ Moq 4.20.72) |
| Config file | none — test discovery via `tests/*/bin/Debug/net8.0/*.dll` + `Microsoft.NET.Test.Sdk` |
| Quick run command | `dotnet test tests/DynamicWeb.Serializer.Tests/DynamicWeb.Serializer.Tests.csproj --filter "FullyQualifiedName~<fixture>" --nologo` |
| Full suite command | `dotnet test --nologo` (runs both .Tests and .IntegrationTests projects) |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| A.1 (ack-acknowledged) | Acknowledged ID logs warning, doesn't throw | unit | `dotnet test --filter "FullyQualifiedName~BaselineLinkSweeperAckTests.Acknowledged"` | ❌ Wave 0 |
| A.1 (ack-unlisted) | Unlisted ID throws even when another ID is acknowledged | unit | `dotnet test --filter "FullyQualifiedName~BaselineLinkSweeperAckTests.Unlisted"` | ❌ Wave 0 |
| A.1 (ack-empty) | Empty ack list → any orphan throws | unit | `dotnet test --filter "FullyQualifiedName~BaselineLinkSweeperAckTests.EmptyAck"` | ❌ Wave 0 |
| A.2 (identity-insert) | Area create wraps INSERT in SET IDENTITY_INSERT ON/OFF | unit (via ISqlExecutor mock) | `dotnet test --filter "FullyQualifiedName~ContentDeserializerIdentityInsertTests"` | ❌ Wave 0 |
| A.3 (consolidated-field) | ModeConfig.AcknowledgedOrphanPageIds is removed — build succeeds with no references | build + grep-absence | `dotnet build && ! grep -rn "ModeConfig\.AcknowledgedOrphanPageIds\|mode.AcknowledgedOrphanPageIds" src/` | N/A (grep) |
| A.3 (legacy-warning) | ConfigLoader logs migration warning when deploy.acknowledgedOrphanPageIds present | unit | `dotnet test --filter "FullyQualifiedName~ConfigLoaderTests.LegacyModeLevelAckList"` | ❌ Wave 0 |
| B.1/B.2 | Post-cleanup re-serialize of Swift 2.2 emits no template-missing warnings for the 3 names | manual E2E + log grep | `sqlcmd -i tools/swift22-cleanup/05-null-orphan-template-refs.sql && <re-serialize> && ! grep -E '1ColumnEmail\|2ColumnsEmail\|Swift-v2_PageNoLayout' <logfile>` | ❌ Wave 0 |
| B.3 | TargetSchemaCache still skips the 3 drift cols without escalation, OR the allowlist prevents escalation | unit | existing `TargetSchemaCacheTests.LogMissingColumnOnce_AreaHtmlType` + new allowlist test | ✓ (existing) + ❌ (new) |
| B.4 (production) | Fresh Azure SQL (or post-purge with no EcomShopGroupRelation rows) deserialize completes without the FK warning | manual E2E + log grep | `<purge>;<deserialize>; ! grep 'FK.*EcomShopGroupRelation' <logfile>` | Manual |
| B.5 | Paragraph-anchor `Default.aspx?ID=X#Y` validates both parts | unit | `dotnet test --filter "FullyQualifiedName~BaselineLinkSweeperTests.ParagraphAnchor"` | ❌ Wave 0 |
| C.1 (assertion) | SerializeResult.RowsSerialized == WrittenFiles.Count == source row count for N-duplicates input | unit (mocked reader) | `dotnet test --filter "FullyQualifiedName~FlatFileStoreDedupTests.MultipleDuplicates_AllRowsPreserved"` | ❌ Wave 0 |
| C.1 (E2E) | Swift 2.2 EcomProducts serialize produces 2051 files | manual E2E + file count | `<serialize EcomProducts only>; ls baselines/Swift2.2/_sql/EcomProducts/*.yml \| wc -l # expect 2051` | Manual |
| D.1 | curl ?mode=seed == curl -d '{"Mode":"seed"}' | integration (manual curl) + unit | `dotnet test --filter "FullyQualifiedName~SerializerCommandQueryBindingTests"` | ❌ Wave 0 |
| D.2 | 0-error serialize returns HTTP 200 | integration (curl + status assertion) | `STATUS=$(curl -sk -o /dev/null -w '%{http_code}' ... ); [ "$STATUS" = "200" ]` | Manual |
| D.3 | Smoke tool exits 0 on clean CleanDB, non-zero on 5xx | integration | `pwsh tools/smoke/Test-BaselineFrontend.ps1; echo $?` | ❌ Wave 0 |
| E.1 | Swift2.2-baseline.md has new section | doc presence | `grep -q 'Pre-existing source-data bugs' docs/baselines/Swift2.2-baseline.md` | ✓ (file) + ❌ (section) |
| E.2 | env-bucket.md exists with all 5 sections | doc presence | `grep -qE 'GlobalSettings\.config\|Key Vault\|AreaDomain\|Swift templates\|App Service' docs/baselines/env-bucket.md` | ❌ Wave 0 |
| D-38-16 | Swift 2.2 → CleanDB round-trip with strictMode ON completes cleanly | manual E2E + log grep | `<full E2E per REPORT.md with strictMode removed>; ! grep 'CumulativeStrictModeException' <logfile>` | Manual |

### Sampling Rate

- **Per task commit:** `dotnet test tests/DynamicWeb.Serializer.Tests/DynamicWeb.Serializer.Tests.csproj --filter "<task fixture>" --nologo` (typically <5s)
- **Per wave merge:** `dotnet test --nologo` (full suite, currently 620 tests, ~30–60s)
- **Phase gate:** Full suite green + Swift 2.2 → CleanDB E2E from REPORT.md reproduce section completes without the 7 escalated warnings (B.1–B.5 closed) AND EcomProducts count matches source (C.1 closed)

### Wave 0 Gaps

- [ ] `tests/DynamicWeb.Serializer.Tests/Infrastructure/BaselineLinkSweeperAckTests.cs` — covers A.1 (3+ tests)
- [ ] `tests/DynamicWeb.Serializer.Tests/Serialization/ContentDeserializerIdentityInsertTests.cs` — covers A.2
- [ ] `tests/DynamicWeb.Serializer.Tests/Configuration/ConfigLoaderTests.cs` — extend for A.3 legacy-warning test
- [ ] `tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/FlatFileStoreDedupTests.cs` — covers C.1 (multiple-duplicates regression test)
- [ ] `tests/DynamicWeb.Serializer.Tests/AdminUI/SerializerCommandQueryBindingTests.cs` — covers D.1 + D.2
- [ ] Extend existing `tests/DynamicWeb.Serializer.Tests/Infrastructure/BaselineLinkSweeperTests.cs` for B.5 paragraph-anchor case
- [ ] Extend existing `tests/DynamicWeb.Serializer.Tests/Infrastructure/TargetSchemaCacheTests.cs` for B.3 allowlist (if D-38-07 resolves to code fix)
- [ ] No framework install needed — xUnit + Moq already present

## Security Domain

`security_enforcement` is not explicitly set in `.planning/config.json`; treating as enabled. Phase 38 touches a thin attack surface (tests, SQL cleanup scripts, filename generation, API binding, docs). Scope the ASVS review accordingly.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | Phase 38 does not alter admin-UI auth / API token flows. |
| V3 Session Management | no | No session changes. |
| V4 Access Control | no | No permission model changes. |
| V5 Input Validation | **yes** | D.1 query-param binding introduces a new input channel. Validate `mode` values via the same `Enum.TryParse<DeploymentMode>` gate already in Handle() — applies regardless of binding source. |
| V6 Cryptography | no | MD5 in C.1 fix is filename-dedup (non-security); not introducing new cryptographic primitives. |
| V7 Error Handling | **yes** | D.2 HTTP status correctness is an error-reporting concern. Fix must not leak internal state in the message beyond what Phase 37 already approved. |
| V8 Data Protection | no | No credential-handling changes. |
| V9 Communication | no | No TLS / HTTP-header changes. |
| V10 Malicious Code | no | No code-loading changes. |
| V11 Business Logic | **yes** | A.1 threat-model entry (per D-38-04) on why acknowledging orphans is safer than disabling the sweep is required in PLAN.md. |
| V12 File & Resource | **yes** | C.1 fix changes filename generation. Preserve `SanitizeFileName` path-traversal guards (see T-37-05-01 in 37-05-SUMMARY). |
| V13 API & Web Service | **yes** | D.1 adds query-string binding to the admin API; ensure the same authZ gate applies (already gated by `Bearer` token per REPORT.md). |
| V14 Configuration | **yes** | D-38-16 flips `strictMode` default to ON — verify E.1 and E.2 docs match the new default. |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Query-param injection via `?mode=X` (D.1) | Tampering | `Enum.TryParse<DeploymentMode>` at start of `Handle()` (already present; don't remove). |
| Mode-level AcknowledgedOrphanPageIds silent drop during A.3 migration | Repudiation | Log the explicit warning from CONTEXT §Specifics; user has signed off (D-38-03). |
| Filename collision hiding rows (C.1) | Denial of Service / Integrity | Monotonic-counter dedup with explicit 100K cap and throw on exhaustion (see Pattern 3). |
| Sweep bypass via overly-broad ack list (A.1 threat) | Elevation of Privilege (baseline integrity) | Per-predicate scope (not mode-level) + each acknowledged ID must be listed explicitly. Threat-model entry per D-38-04. |
| Dynamic SQL in cleanup script (B.1/B.2) | Tampering | Dynamic SQL uses `INFORMATION_SCHEMA` + bracket-escaped identifiers; the 3 template-name values are hardcoded in the script (not user input). Matches mitigation in 01-null-orphan-page-refs.sql. |
| Path traversal via template name in B.1/B.2 cleanup | Tampering | Hardcoded template names; no user-supplied data in the generated SQL. |
| D.3 smoke tool following redirects to arbitrary hosts | Tampering | `-MaximumRedirection 5` + `$Host` is a local parameter; tool is local-dev-only per D-38-13. |
| HTTP 400 leaking error list (D.2) | Information Disclosure | 37-04 T-37-04-02 already accepted the bearer-token-gated error surface as mitigated; no new disclosure from D.2 fix. |

## Project Constraints (from CLAUDE.md)

`./CLAUDE.md` does NOT exist in the working directory. Constraints inferred from project patterns + memory:

- **No backward compat required** (beta product, per `memory/feedback_no_backcompat.md` referenced in Phase 37 CONTEXT) — A.3 drops mode-level ack without migration; D-38-16 removes config fields without graceful-degradation shim.
- **Content tables MUST use ContentProvider, NOT SqlTable** (per `memory/feedback_content_not_sql.md`, referenced in 37 CONTEXT). Phase 38 does not expand SqlTable's reach into Area/Page/Paragraph — all B.3 and B.5 work stays within ContentProvider paths.
- **Existing test convention:** xUnit + Moq, no FluentAssertions, tests colocated under `tests/DynamicWeb.Serializer.Tests/<Area>/`. Integration tests (if any) under `tests/DynamicWeb.Serializer.IntegrationTests/`.
- **No new NuGet packages in this phase.** The two test csproj files should remain unchanged except for test file additions.
- **SQL identifiers: bracket-escape, validate against INFORMATION_SCHEMA** — Phase 37-03 pattern applies to any new SQL. Phase 38's SQL cleanup scripts follow this (hardcoded names, no user input).
- **DW `Database.ExecuteNonQuery` + `CommandBuilder`** for all T-SQL. Don't introduce Dapper/EF Core in tests (use Moq `ISqlExecutor`).
- **Admin-UI `CommandBase` conventions:** public properties for parameters, `Handle()` returns `CommandResult`, `ResultType.Invalid/Error/Ok` map to HTTP via framework. D.2 works WITH this convention.

## Sources

### Primary (HIGH confidence — verified via code inspection)

- `src/DynamicWeb.Serializer/Infrastructure/BaselineLinkSweeper.cs` (fully read, all 137 lines)
- `src/DynamicWeb.Serializer/Models/ProviderPredicateDefinition.cs` (fully read)
- `src/DynamicWeb.Serializer/Configuration/ModeConfig.cs` (fully read)
- `src/DynamicWeb.Serializer/Configuration/ConfigLoader.cs` (fully read)
- `src/DynamicWeb.Serializer/Configuration/ConfigWriter.cs` (fully read)
- `src/DynamicWeb.Serializer/Configuration/SerializerConfiguration.cs` (fully read)
- `src/DynamicWeb.Serializer/Configuration/swift2.2-combined.json` (fully read)
- `src/DynamicWeb.Serializer/Providers/SqlTable/SqlTableWriter.cs` (fully read)
- `src/DynamicWeb.Serializer/Providers/SqlTable/SqlTableProvider.cs` (fully read)
- `src/DynamicWeb.Serializer/Providers/SqlTable/SqlTableReader.cs` (fully read)
- `src/DynamicWeb.Serializer/Providers/SqlTable/FlatFileStore.cs` (fully read — **C.1 root cause confirmed in :120-134**)
- `src/DynamicWeb.Serializer/Providers/Content/ContentProvider.cs` (lines 200-237 read — A.3 piping path)
- `src/DynamicWeb.Serializer/Providers/SerializerOrchestrator.cs` (lines 130-190, 340-393 read)
- `src/DynamicWeb.Serializer/Serialization/ContentSerializer.cs` (lines 1-160 read)
- `src/DynamicWeb.Serializer/Serialization/ContentDeserializer.cs` (lines 430-510 read — A.2 fix site)
- `src/DynamicWeb.Serializer/AdminUI/Commands/SerializerSerializeCommand.cs` (fully read)
- `src/DynamicWeb.Serializer/AdminUI/Commands/SerializerDeserializeCommand.cs` (fully read)
- `tools/swift22-cleanup/01-null-orphan-page-refs.sql` (fully read — B.1/B.2 pattern source)
- `tools/purge-cleandb.sql` (fully read — B.4 context)
- `docs/baselines/Swift2.2-baseline.md` (fully read — E.1 target)
- `.planning/phases/38-production-ready-baseline-hardening-retroactive-tests-for-37/38-CONTEXT.md` (all 16 decisions)
- `.planning/sessions/2026-04-20-e2e-baseline-roundtrip/REPORT.md` (all 14 items + reproduce recipe)
- `.planning/phases/37-production-ready-baseline/37-CONTEXT.md` (D-01..D-24)
- `.planning/phases/37-production-ready-baseline/37-{01,02,03,04,05}-SUMMARY.md` (architectural context)
- `.planning/ROADMAP.md` (Phase 38 full backlog)
- `tests/DynamicWeb.Serializer.Tests/DynamicWeb.Serializer.Tests.csproj` (xUnit 2.9.3, Moq 4.20.72, net8.0)
- `tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/SqlTableWriterTests.cs` (test pattern reference)
- `tests/DynamicWeb.Serializer.Tests/Infrastructure/BaselineLinkSweeperTests.cs` (test pattern reference)
- `git log --oneline` HEAD shows `5333e88 fix(37-follow-up): wrap Area INSERT in SET IDENTITY_INSERT` + `7496fe2 feat(37-follow-up): per-predicate AcknowledgedOrphanPageIds bypass` — these are the A.1/A.2 subject commits.

### Secondary (MEDIUM confidence — NuGet XML doc cross-check)

- `~/.nuget/packages/dynamicweb.coreui/10.1.2/lib/net8.0/Dynamicweb.CoreUI.xml` grepped for `CommandBase` — only ancillary mentions (4 `ShowMessageGenerateCommandBase` references); no binding documentation.
- `~/.nuget/packages/dynamicweb.management.api/10.8.4/lib/net8.0/Dynamicweb.Management.Api.xml` grepped for `FromQuery/FromBody/Command` — one regex-helper mention; no public binding API documented.

### Tertiary (LOW confidence — requires live verification)

- D.1 exact binding convention for DW CommandBase public properties — marked as investigation task.
- B.4 whether the FK warning fires on production fresh Azure SQL vs only post-purge — marked as investigation per D-38-08.

## Metadata

**Confidence breakdown:**

- Standard stack: HIGH — all dependencies are pre-existing in csproj files and verified.
- Architecture: HIGH — all intervention points located in source; no structural uncertainty.
- Pitfalls: HIGH — every pitfall derives from a specific line of source code or REPORT.md finding.
- A.2 test strategy: MEDIUM — requires a small refactor choice (ISqlExecutor seam) that is well-precedented in 37-05's Rule 3 blocker handling.
- B.3 resolution: MEDIUM — depends on an external fact (whether DW 10.8.4 has AreaHtmlType etc.).
- C.1 root cause: HIGH — confirmed from code inspection; the 1469 count is consistent with duplicate ProductName values.
- D.1 binding fix: MEDIUM — first-attempt approach is clear but confirmation requires a live curl test.
- D.2 fix shape: MEDIUM — per-CONTEXT the fix is to return 200 on errors.Count == 0, but the exact trigger for the observed 400 needs reproduction.

**Research date:** 2026-04-21
**Valid until:** 2026-05-21 (30 days — stable codebase, no fast-moving external deps). Re-verify B.3 and D.1 if CleanDB DW version or DW CoreUI NuGet version changes before execution.
