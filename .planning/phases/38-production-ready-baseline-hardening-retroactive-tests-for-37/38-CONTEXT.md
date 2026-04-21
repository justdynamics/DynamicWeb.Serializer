# Phase 38: Production-Ready Baseline Hardening - Context

**Gathered:** 2026-04-21
**Status:** Ready for planning
**Source:** Interactive discuss-phase after Phase 37 autonomous E2E round-trip (`.planning/sessions/2026-04-20-e2e-baseline-roundtrip/REPORT.md`) + Swift 2.2 data cleanup (`tools/swift22-cleanup/`)

<domain>
## Phase Boundary

Phase 38 closes everything Phase 37's autonomous E2E round-trip surfaced but intentionally did NOT fix. Phase 37 was about shipping the baseline machinery (Deploy/Seed split, strict mode, template manifest, link resolution, SQL identifier validation). The E2E proved it works end-to-end on live Swift 2.2 → CleanDB, and in proving it, exposed 14 discrete follow-up items across five themes: retroactive tests for two 37 inline code fixes, strict-mode data/template gaps that forced `strictMode: false`, one known data-loss path (EcomProducts 2051 → 582), API/UX polish, and documentation.

**What's in:** the 14 backlog items from ROADMAP.md (Groups A, B, C, D, E + B.5 added 2026-04-21 after Swift 2.2 cleanup).
**What's out:** new product features, new provider types, anything Phase 39 or later.

**Success state (what "done" looks like):**
1. Swift 2.2 → CleanDB round-trip runs with `strictMode: true` (API default) without escalated warnings.
2. The two 37 inline fixes (`AcknowledgedOrphanPageIds`, `IDENTITY_INSERT` on Area) have unit/integration test coverage that catches regression.
3. EcomProducts preserves all 2051 rows end-to-end OR silently-filtered rows are documented with a test proving the filter criterion.
4. `swift2.2-combined.json` is the canonical baseline: no `acknowledgedOrphanPageIds`, no `strictMode: false` override, no bug workarounds.
5. A local-only smoke-test tool (separate from the serializer library) lives under `tools/` for repeatable round-trip verification.
6. `docs/baselines/Swift2.2-baseline.md` documents pre-existing source-data bugs; new `docs/baselines/env-bucket.md` covers per-env config (Friendly URL, GlobalSettings, Key Vault).

</domain>

<decisions>
## Implementation Decisions (locked)

### Scope shape

- **D-38-01:** Single phase with wave-grouped execution (NOT split into 38a/38b/38c). Waves by theme: (1) quick wins D.1+D.2+E.1+E.2 (2) retroactive tests A.1+A.2+A.3+B.5 (3) investigations B.1+B.2+B.3+B.4+C.1 (4) tooling D.3. Planner is free to parallelize within a wave and must leave inter-wave dependencies explicit.
- **D-38-02:** Do not defer any of the 14 items. User explicitly wants the full scope.

### Group A - retroactive tests + code hygiene

- **D-38-03 (A.3):** `AcknowledgedOrphanPageIds` lives ONLY on `ProviderPredicateDefinition` (per-predicate). Remove the duplicate on `ModeConfig` and any ConfigLoader/ConfigWriter plumbing that reads/writes the mode-level list. Rationale: the sweep runs per Content predicate; different Content areas may have different known-broken refs; per-predicate matches the existing `ExcludeFields` / `XmlColumns` pattern. Migration: if any existing config has `deploy.acknowledgedOrphanPageIds`, ConfigLoader logs a warning and drops it (no back-compat needed per the beta-product feedback memory).
- **D-38-04 (A.1):** TDD tests for `AcknowledgedOrphanPageIds` must cover at minimum: (a) malicious-ID rejection when NOT in the list (sweep throws), (b) acknowledged-ID warning path (sweep logs but returns success), (c) strict-still-fatal-for-unlisted (unlisted ID throws even when another ID is acknowledged), (d) threat-model entry in the plan's `<threat_model>` block explaining why acknowledging is safer than disabling the sweep wholesale.
- **D-38-05 (A.2):** IDENTITY_INSERT integration test for `Area` create. Test must fail if the `SET IDENTITY_INSERT [Area] ON/OFF` wrapping is removed from `ContentDeserializer.CreateAreaFromProperties`. Use a real test DB (in-memory SQL or a disposable SqlServer testcontainer) with an identity-seeded Area table.

### Group B - strict-mode gap closure

- **D-38-06 (B.1/B.2):** The 3 missing Swift templates (1ColumnEmail, 2ColumnsEmail, Swift-v2_PageNoLayout.cshtml) are stale data from an older Swift version. **Swift is installed via git-clone from https://github.com/dynamicweb/Swift, NOT a nupkg.** The upstream repo does not ship these templates either - confirming they're orphan references in Swift 2.2's source data. Treatment: (a) document the finding in `docs/baselines/env-bucket.md` (templates are filesystem concerns, per-env), (b) extend `tools/swift22-cleanup/` with a SQL script that nulls paragraph/item field references to these three template names so the Swift 2.2 baseline no longer emits them, (c) NOT expand TEMPLATE-01 scope (still manifest-only per 37-05 D-19/D-20).
- **D-38-07 (B.3):** 3 schema-drift Area columns (`AreaHtmlType`, `AreaLayoutPhone`, `AreaLayoutTablet`). CleanDB schema is older than Swift 2.2 schema for these columns. Phase 37-02's `TargetSchemaCache` already tolerates missing target columns with warnings. Confirm the pattern still fires correctly (it does - the E2E saw the warnings). Action in 38: (a) verify CleanDB is on the latest DW version (upgrade if behind), (b) if the drift is legitimate (columns exist in Swift 2.2 but not in base DW), ensure the serializer does NOT escalate these in strict mode - add a `knownEnvSchemaDrift` allowlist or similar if needed. (c) If CleanDB is just behind, the fix is operational, not code.
- **D-38-08 (B.4):** FK re-enable warning on `EcomShopGroupRelation -> EcomShops.ShopId`. Investigation only: (a) does it fire on a production deploy to a fresh Azure SQL, or only after a manual purge like the one in `tools/purge-cleandb.sql`? (b) If purge-only, add a post-purge fix to `purge-cleandb.sql`. (c) If production, investigate whether the SqlTable write order needs adjustment so EcomShops is populated before EcomShopGroupRelation.
- **D-38-09 (B.5):** `BaselineLinkSweeper` is over-strict on paragraph anchors. `Default.aspx?ID=X#Y` currently treats `X` AND `Y` as page IDs. Fix: collect `SerializedPage.SourceParagraphIds` during sweep setup, validate `#Y` against that set (not against page SourceIds). Any anchor pointing at a paragraph NOT in the serialized tree is still an orphan. Both parts (page + anchor) must resolve. No new config flag - this is a correctness fix, not a user toggle.

### Group C - data-loss investigation + fix

- **D-38-10 (C.1):** Root-cause the EcomProducts 2051 -> 582 drop. Primary hypothesis (user-provided): empty-name products filtered silently. Diagnosis plan: (a) add an integration test that emits N SqlTable rows and asserts `SerializeResult.RowsSerialized == filesWritten == N`. (b) Run the Swift 2.2 serialize with additional logging to identify which rows are dropped. (c) Read `SqlTableWriter` and `SqlTableProvider.Serialize` to find the empty-name filter. (d) Decide: either fix so empty-name rows serialize with a synthetic filename (preserving all rows), OR make the filter explicit and loud (warn every dropped row). User prefers fix over warn-only.

### Group D - API/UX polish

- **D-38-11 (D.1):** `?mode=seed` query param on SerializerSerialize/SerializerDeserialize must bind to the command's `Mode` property. Currently only JSON body works. Fix: either add `[FromQuery]` binding or (more likely) DW's CommandBase framework already supports it via a different convention - investigate and apply.
- **D-38-12 (D.2):** HTTP status code bug. Serialize returns HTTP 400 even on 0-errors because the result message always contains `Errors: ` as a literal string (empty list serializes with a trailing header). Fix: return 200 when errors list is empty, regardless of the message format. Add a test that 0-error serialize returns 200.
- **D-38-13 (D.3):** SerializerSmoke is NOT part of the serializer library. Ship under `tools/smoke/` (new folder) as a standalone console/PowerShell script. Hits every active page post-deserialize via the DW host's public URL, reports status code bucket counts, excerpts 5xx bodies. Local-dev only; never deployed to customer sites. This intentionally rules out the admin-UI-command and scheduled-task options.

### Group E - docs

- **D-38-14 (E.1):** Extend `docs/baselines/Swift2.2-baseline.md`. Add a new section "Pre-existing source-data bugs caught by Phase 37 validators" documenting: (a) the 3 column-name mistakes that SqlIdentifierValidator catches, (b) the 5 orphan page IDs that BaselineLinkSweeper catches (2026-04-21 all cleaned by `tools/swift22-cleanup/`), (c) the 267 orphan-area pages + 238 soft-deleted pages removed by the cleanup scripts, (d) the `acknowledgedOrphanPageIds: [15717]` note for the paragraph-anchor false-positive until B.5 closes.
- **D-38-15 (E.2):** Create `docs/baselines/env-bucket.md`. Content: what is NOT in the baseline and why. Covers (a) `/Files/GlobalSettings.config` including Friendly URL `/en-us/` routing (the key finding from the 2026-04-20 E2E); (b) Azure Key Vault secrets (payment gateway credentials, storage keys); (c) per-env AreaDomain / AreaCdnHost / GoogleTagManagerID; (d) the Swift templates filesystem (git clone, not serialized); (e) pointers to Azure App Service config pattern. Target audience: a new customer adopting the baseline who needs to know "what do I configure per environment".

### After Phase 38 closes

- **D-38-16:** Restore `strictMode` default to ON for API/CLI (per Phase 37-04 D-16 original intent), OFF for admin UI. Remove `"strictMode": false` from `swift2.2-combined.json`. This is the final step of Phase 38 - gates on B.1-B.5 + C.1 all closing first.

### Claude's Discretion

- Test framework + in-memory/testcontainer choice for A.2 integration test (project already uses xUnit + Moq; pick whichever is cleaner).
- SQL cleanup for B.1/B.2 - identify affected ItemType_Swift-v2_* tables/columns by scanning for references to the 3 template names; pattern matches `tools/swift22-cleanup/01-null-orphan-page-refs.sql`.
- Logging verbosity on C.1 diagnosis run; user said "likely empty name" so don't over-engineer.
- Tool language for D.3 smoke (PowerShell, .NET console, bash) - pick whatever integrates smoothest with the existing tooling directory on Windows + SQL Server stack.
- Exact form of A.3 migration warning in ConfigLoader when legacy mode-level `acknowledgedOrphanPageIds` is seen.
- Whether `knownEnvSchemaDrift` (D-38-07 option) is a new config field or just a documented pattern using existing excludes.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase 37 baseline work (every 38 item builds on this)
- `.planning/sessions/2026-04-20-e2e-baseline-roundtrip/REPORT.md` - the session that surfaced the 14 items; contains reproduce recipe + exact curl commands + expected DB counts
- `.planning/phases/37-production-ready-baseline/37-01-SUMMARY.md` - Deploy/Seed split reference
- `.planning/phases/37-production-ready-baseline/37-02-SUMMARY.md` - `TargetSchemaCache` (tolerates missing Area cols - B.3 builds on this)
- `.planning/phases/37-production-ready-baseline/37-03-SUMMARY.md` - `SqlIdentifierValidator` / `SqlWhereClauseValidator` / `RuntimeExcludes`
- `.planning/phases/37-production-ready-baseline/37-04-SUMMARY.md` - `StrictModeEscalator` + `DwCacheServiceRegistry` (D-38-16 depends on this)
- `.planning/phases/37-production-ready-baseline/37-05-SUMMARY.md` - `BaselineLinkSweeper` (B.5 builds on this) + `TemplateAssetManifest` (B.1/B.2 scope reference)
- `.planning/phases/37-production-ready-baseline/37-CONTEXT.md` - the D-01..D-24 decisions Phase 38 must not contradict

### Swift 2.2 baseline
- `docs/baselines/Swift2.2-baseline.md` - three-bucket split (DEPLOYMENT/SEED/ENVIRONMENT); update target for E.1
- `src/DynamicWeb.Serializer/Configuration/swift2.2-combined.json` - canonical combined config; must end up strict-mode default after D-38-16
- `tools/swift22-cleanup/` - applied 2026-04-21; B.1/B.2 will add a new numbered script here

### Swift upstream (for B.1/B.2 investigation)
- https://github.com/dynamicweb/Swift - confirmed NOT to contain the 3 missing templates, so their presence in Swift 2.2 is stale data
- The Swift 2.2 DB local path: `C:\Projects\Solutions\swift.test.forsync\Swift2.2\Dynamicweb.Host.Suite` (port 54035); CleanDB at `:58217`

### Code files the planner must read
- `src/DynamicWeb.Serializer/Models/ProviderPredicateDefinition.cs` (A.3 consolidation target)
- `src/DynamicWeb.Serializer/Configuration/ModeConfig.cs` (A.3 removal target)
- `src/DynamicWeb.Serializer/Configuration/ConfigLoader.cs` (A.3 migration warning, D.1 query-param investigation)
- `src/DynamicWeb.Serializer/Configuration/ConfigWriter.cs` (A.3 removal target)
- `src/DynamicWeb.Serializer/Serialization/ContentSerializer.cs` (A.3 read path, B.5 sweep call site)
- `src/DynamicWeb.Serializer/Serialization/ContentDeserializer.cs` (A.2 test target - CreateAreaFromProperties lines ~443-472)
- `src/DynamicWeb.Serializer/Infrastructure/BaselineLinkSweeper.cs` (B.5 fix site - `InternalLinkPattern` regex + `WalkPage` for paragraph ID collection)
- `src/DynamicWeb.Serializer/Providers/SqlTable/SqlTableWriter.cs` (C.1 write path - check empty-name handling)
- `src/DynamicWeb.Serializer/Providers/SqlTable/SqlTableProvider.cs` (C.1 serialize path - check filter criterion)
- `src/DynamicWeb.Serializer/AdminUI/Commands/SerializerSerializeCommand.cs` (D.1 query-param binding)
- `src/DynamicWeb.Serializer/AdminUI/Commands/SerializerDeserializeCommand.cs` (D.1 query-param binding; D.2 HTTP 400 bug)

</canonical_refs>

<specifics>
## Specific Ideas

### The B.1/B.2 SQL cleanup template pattern

Follow the existing `tools/swift22-cleanup/01-null-orphan-page-refs.sql` shape: dynamic SQL that iterates all `ItemType_Swift-v2_%` tables with string-type columns, scans for references to the 3 template names (`1ColumnEmail`, `2ColumnsEmail`, `Swift-v2_PageNoLayout.cshtml`), and nulls/replaces them. Adjacent to the page-ID refs cleanup so re-running the whole script set produces a fully-clean Swift 2.2.

### D.3 smoke tool expected output

When run against CleanDB after Deploy+Seed deserialize, the tool enumerates every `PageActive=1` page under area 3 via the DB (same connection as the host), hits `https://host/en-us/{page-slug}` (Friendly URL pattern), buckets responses:
- 2xx: counted, no body capture
- 3xx: counted, capture final-URL after redirects
- 4xx: counted, first 500 chars of body
- 5xx: counted, first 2000 chars of body + full response headers

Exits 0 if no 5xx, non-zero if any 5xx. A report-only mode (exit 0 regardless) is a stretch goal.

### The A.3 migration warning wording

When ConfigLoader sees mode-level `acknowledgedOrphanPageIds` during Load:
```
[Serializer] WARNING: deploy.acknowledgedOrphanPageIds (or seed.acknowledgedOrphanPageIds)
is no longer supported. Move the IDs onto the Content predicate(s) that contain the
orphan references. The mode-level list is ignored this load. See Phase 38 D-38-03.
```

</specifics>

<deferred>
## Deferred Ideas

- **TEMPLATE-01 scope expansion** (capture email/grid-row/page-layout template *content* alongside the manifest) - explicitly deferred per D-38-06. If B.1/B.2 investigation reveals templates that should be serialized (customer-created vs Swift-shipped), file a Phase 39 item.
- **Validator upgrade for paragraph anchors in arbitrary other contexts** - B.5 covers `Default.aspx?ID=X#Y`. If `SelectedValue` in ButtonEditor JSON can ALSO carry `#anchor` suffixes, document and handle in a follow-up.
- **SerializerSmoke CI integration** - D-38-13 explicitly local-only. If a customer asks for CI smoke, build it then.
- **Automatic detection of stale Swift upstream drift** - if Swift 2.2 sources reference templates/pages that no longer exist in upstream Swift, could the serializer auto-detect and suggest cleanup? Out of scope for Phase 38.

</deferred>

---

*Phase: 38-production-ready-baseline-hardening*
*Context gathered: 2026-04-21 via /gsd-discuss-phase*
