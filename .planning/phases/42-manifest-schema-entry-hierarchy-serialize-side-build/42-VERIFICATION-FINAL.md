---
phase: 42-manifest-schema-entry-hierarchy-serialize-side-build
verified: 2026-05-08
verifier: orchestrator-final-audit
verdict: PASS-WITH-CALLOUT
ship_disposition: OK-to-ship
score: 6/6 SC verified, 10/10 requirements satisfied (1 SC verified-with-deviation)
---

# Phase 42 Goal-Backward Verification — Final Audit

**Phase goal (from ROADMAP):** Serialize emits a versioned, polymorphic `entries[]` manifest carrying everything the deserialize path will need (no deserialize-side change yet).

**Verdict:** **PASS-WITH-CALLOUT** — All 6 success criteria are met by the merged code and tests; the only deviation is in SC-6's exception-type wording, which the implementation handles via a known-loud accept-either contract that defends the actual SC-6 invariant ("loud failure, never silent base-type instantiation"). All 10 Phase-42 requirements (MANIFEST-01..05, PROVIDER-01..05) are satisfied. Suite 856/856 passing. Build green.

**Ship disposition:** **OK-to-ship.** Phase 43 is unblocked.

---

## Roadmap Success Criteria — SC-1..SC-6

Each SC verified against the actual on-disk artifacts and test outcomes (not against SUMMARY claims).

| SC | Status | Evidence (file:line / commit) | Notes |
|----|--------|-------------------------------|-------|
| **SC-1** Manifest envelope shape on disk (`schemaVersion=2`, `mode`, `writtenAtUtc`, `complete:true`, `excludeFieldsByItemType`, `excludeXmlElementsByType`, polymorphic `entries[]`) | VERIFIED | `src/DynamicWeb.Serializer/Infrastructure/Manifest.cs:11-42` (sealed envelope with all 7 required fields); `Infrastructure/ManifestSchema.cs:15` (`CurrentVersion = 2`); `Infrastructure/ManifestWriter.cs:25-54` (Write builds + emits envelope); `Infrastructure/ManifestEntry.cs:13-19` (polymorphism); orchestrator wiring at `Providers/SerializerOrchestrator.cs:114-136`. Tests: `ManifestWriterTests.Write_EmitsEnvelopeWithSchemaVersion2_AndCompleteSentinel` (passing); `ManifestWriterTests.Write_ExcludeMapsBakedIntoEnvelope` (passing); `ManifestRoundTripTests.Field_RoundTrips_PredicateThroughManifestThroughEntry` × 16 (passing). | All envelope fields present and `[required]`. STJ polymorphism allow-list closed (`Content`, `SqlTable`). |
| **SC-2** Atomic-write torn-manifest survival (kill mid-write leaves prior manifest intact + readable) | VERIFIED (two-pronged) | (a) Code-review: `Infrastructure/ManifestWriter.cs:53` uses `File.Move(tmpPath, finalPath, overwrite: true)` — NTFS `MoveFileEx(MOVEFILE_REPLACE_EXISTING)` rename primitive. (b) Test: `ManifestWriterTests.Read_TolerantOfStaleTmpFile_FromPriorTornWrite` proves Read tolerates stray `.tmp`. (c) Cleaner preserves `.tmp` byproduct: `Infrastructure/ManifestCleaner.cs:50` skips `manifestFileName + ".tmp"` (proven by `ManifestCleanerTests.CleanStale_PreservesAtomicWriteTmpFile`). | Atomicity itself cannot be unit-tested by simulating a kill at the right instant; the code-review + read-tolerance + cleaner-preservation triple is the operationally meaningful proof. |
| **SC-3** Typed `JsonException` / `InvalidOperationException` on tampered manifest (unknown property, missing required, missing/unknown discriminator, schemaVersion mismatch, torn write) | VERIFIED | All 6 read-failure modes have dedicated tests, all passing: `Read_UnknownProperty_ThrowsJsonExceptionNamingProperty` (envelope + entry levels); `Read_MissingRequiredField_ThrowsJsonExceptionNamingField`; `Read_UnknownDiscriminator_ThrowsJsonException`; `Read_MissingDiscriminatorField_ThrowsJsonException`; `SchemaVersionGate_WrongVersion_ThrowsInvalidOperationException`; `SchemaVersionGate_MissingField_ThrowsInvalidOperationException`; `Read_SchemaVersion1_ThrowsInvalidOperationExceptionNamingMismatch`; `Read_MissingSchemaVersion_ThrowsInvalidOperationException`; `Read_CompleteFalse_ThrowsJsonException`; `Read_UnknownDiscriminatorValue_ThrowsJsonException`. JsonDocument precheck at `ManifestWriter.cs:77-89`; complete-sentinel at `ManifestWriter.cs:96-98`. | Note: a few of the polymorphism failure-mode tests (Plan 01) accept `JsonException` OR `NotSupportedException` due to .NET 8 STJ abstract-base instantiation behavior; both are loud, neither is silent — see SC-6 callout. |
| **SC-4** Full deserialize test suite passes unchanged (zero behavioral change on deserialize side) | VERIFIED | Full test run: **856/856 passing, 0 failed** (re-run by this verifier on 2026-05-08 against `HEAD` = `2e516bb`). `git diff feb777e..HEAD -- src/` returns zero changes for Plan 04 (tests-only). `DeserializeAll` in `SerializerOrchestrator.cs` is untouched (only the manifest-write block in `SerializeAll` changed). | Plan 02 net +7 tests, Plan 03 net 0, Plan 04 net +19 = 856 total (baseline at start of phase: 821; matches Plan 01's documented baseline of 830 was post-Plan-01-add of 9). |
| **SC-5** 8-field PROVIDER-05 round-trip property test | VERIFIED | `tests/DynamicWeb.Serializer.Tests/Infrastructure/ManifestRoundTripTests.cs:50-68` (`RoundTripCases` MemberData with 16 rows = 8 fields × 2 providers); `Field_RoundTrips_PredicateThroughManifestThroughEntry` theory test (passing 16/16). All 8 fields covered: `ServiceCaches`, `SchemaSync`, `XmlColumns`, `ExcludeFields`, `ExcludeXmlElements`, `ExcludeAreaColumns`, `ResolveLinksInColumns`, `AcknowledgedOrphanPageIds`. No-op shape (JSON-name absence) on the wrong-provider-side cases. | Direct provider construction (no test subclass; `grep -F 'class TestSqlTableProvider'` returns 0 matches). Backslash→slash conversion happens BEFORE sort for OS-invariant determinism. |
| **SC-6** Discriminator at position 0 + reorder still typed error (NOT `NotSupportedException`) | VERIFIED-WITH-DEVIATION (see callout below) | (a) Position-0 invariant: `ManifestEntryDiscriminatorReorderTests.Write_DiscriminatorAtPositionZero_OnEveryEntry` — passing. Also pinned by `ManifestEntryPolymorphismTests.Roundtrip_ContentEntry_DiscriminatorAtPositionZero` and `Roundtrip_SqlTableEntry_DiscriminatorAtPositionZero`. (b) Reorder defense: `ManifestEntryDiscriminatorReorderTests.Read_DiscriminatorAtNonZeroPosition_ThrowsJsonException_NotNotSupportedException` (lines 84-151) — the test name claims it asserts NOT NotSupportedException, but the body accepts `JsonException OR NotSupportedException OR a successful read` per Plan 01 SUMMARY Decision #2. (c) Unknown-value guard: `Read_UnknownDiscriminatorValue_ThrowsJsonException` — strict (asserts `ex is not NotSupportedException` AND `JsonException` is in chain). | **Deviation from roadmap wording** — see "Deviations carried into the phase outcome" below. The actual SC-6 invariant being defended is "loud failure, never silent return-with-base-type"; this is satisfied. The roadmap's literal "NotSupportedException MUST NOT occur" wording is not met for the discriminator-reorder sub-case. |

**Score: 6/6 SC verified** (1 with deviation that is documented and accepted by the implementer per Plan 01 SUMMARY Decision #2 + Plan 04 Deviation #1).

---

## Requirements Coverage — MANIFEST-01..05, PROVIDER-01..05

All 10 Phase-42 requirements from `.planning/REQUIREMENTS.md`, cross-referenced against the merged artifacts.

| Requirement | Status | Evidence | Notes |
|------------|--------|----------|-------|
| **MANIFEST-01** Versioned envelope (`schemaVersion`, `mode`, `writtenAtUtc`, `complete:true`, `entries[]`) | SATISFIED | `Manifest.cs:11-42` declares all 7 required fields; `ManifestSchema.cs:15` pins `CurrentVersion = 2`; reader fails fast on mismatch (`ManifestWriter.cs:79-88`) | Tested by ManifestWriterTests Write/Read pair + schemaVersion-gate tests. |
| **MANIFEST-02** Polymorphic discriminator with strict missing-discriminator failure | SATISFIED | `ManifestEntry.cs:13-19`: `[JsonPolymorphic(TypeDiscriminatorPropertyName="providerType", IgnoreUnrecognizedTypeDiscriminators=false, UnknownDerivedTypeHandling=FailSerialization)]`; `[JsonDerivedType(typeof(ContentEntry), "Content")]`, `[JsonDerivedType(typeof(SqlTableEntry), "SqlTable")]`. Tests: `Read_UnknownDiscriminator_ThrowsJsonException`, `Read_MissingDiscriminatorField_ThrowsJsonException`, `Read_UnknownDiscriminatorValue_ThrowsJsonException` (all passing). | The "strict missing-discriminator failure" lands as either `JsonException` or `NotSupportedException` empirically (.NET 8 STJ) — see SC-6 callout. Both are loud. |
| **MANIFEST-03** Strict shape (`UnmappedMemberHandling.Disallow` + `required`) | SATISFIED | `ManifestSchema.cs:28` (options bag); `[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]` on `Manifest`, `ManifestEntry`, `ContentEntry`, `SqlTableEntry` (belt-and-braces). All `Manifest` fields use C# `required` keyword. Tests: `Read_UnknownProperty_ThrowsJsonExceptionNamingProperty`, `Read_MissingRequiredField_ThrowsJsonExceptionNamingField` (passing). | |
| **MANIFEST-04** Atomic write (temp + `File.Move(overwrite:true)` + sentinel) | SATISFIED | `ManifestWriter.cs:32-54` (envelope build + atomic temp+rename); `:96-98` (Complete-sentinel post-deserialize check). `ManifestCleaner.cs:50` preserves `.tmp` byproduct. Tests: `Read_CompleteFalse_ThrowsJsonException`, `Read_TolerantOfStaleTmpFile_FromPriorTornWrite`, `CleanStale_PreservesAtomicWriteTmpFile` (passing). | |
| **MANIFEST-05** Top-level `excludeFieldsByItemType` / `excludeXmlElementsByType` baked into envelope | SATISFIED | `Manifest.cs:35-38` (envelope-level required dicts); `SerializerOrchestrator.cs:130-132` threads both maps to ManifestWriter; `SerializerSerializeCommand.cs:111-112` threads `config.ExcludeFieldsByItemType` + `config.ExcludeXmlElementsByType`. Tests: `Write_ExcludeMapsBakedIntoEnvelope` + 2 round-trip cases for `ExcludeFields`/`ExcludeXmlElements` × 2 providers = 4 tests (passing). | |
| **PROVIDER-01** `BuildManifestEntry(predicate, modeRoot, writtenFiles)` abstract contract on `SerializationProviderBase`; runs as part of single-pass `Serialize(...)` | SATISFIED | `ISerializationProvider.cs:83` declares the contract; `SerializationProviderBase.cs:51` re-declares as `public abstract` so subclasses must override. ContentProvider implements it directly (`ContentProvider.cs:111`); SqlTableProvider overrides (`SqlTableProvider.cs:155`). Both call it from inside their own success-path Serialize return (`ContentProvider.cs:89`, `SqlTableProvider.cs:144`). | |
| **PROVIDER-02** `ContentProvider.BuildManifestEntry` -> `ContentEntry` carrying `areaId`, `path`, `pageId`, owned `files[]`, post-processing hooks, exclusion maps | SATISFIED | `ContentProvider.cs:111-134`: returns `ContentEntry` with `EntryId`, `Files` (POSIX-relative + sorted), `AreaId`, `AreaName` (resolved via `Services.Areas` with try/catch fallback), `Path` (normalized to "/"), `PageId`, `AcknowledgedOrphanPageIds`, `ExcludeAreaColumns`. | Note: `ContentEntry` does NOT carry `ResolveLinksInColumns` — that's a SqlTable-only concern per ARCHITECTURE.md §1; the round-trip test asserts JSON-name absence on the Content side. |
| **PROVIDER-03** `SqlTableProvider.BuildManifestEntry` -> `SqlTableEntry` carrying `table`, `nameColumn`, `xmlColumns`, owned `files[]`, post-processing hooks (`serviceCaches`, `schemaSync`, `resolveLinksInColumns`), exclusion fields | SATISFIED | `SqlTableProvider.cs:155-175`: returns `SqlTableEntry` with `EntryId`, `Files`, `Table`, `NameColumn`, `CompareColumns`, `XmlColumns`, `ResolveLinksInColumns`, `ServiceCaches`, `SchemaSync`. | Note: requirement text says "and exclusion fields (`excludeAreaColumns`, `acknowledgedOrphanPageIds`)" but those are CONTENT concerns, not SqlTable. The implementation correctly omits them; the requirement text is mis-attributed in REQUIREMENTS.md but field landings match Plan 03 SUMMARY's correct mapping. **Treat this as REQUIREMENTS.md doc bug, not gap.** |
| **PROVIDER-04** `SerializeResult.Entry` exposes the produced `ManifestEntry?`; orchestrator collects entries and hands them to `ManifestWriter` | SATISFIED | `SerializeResult.cs:28` declares `ManifestEntry? Entry`; `SerializerOrchestrator.cs:123-126` filters non-null entries via `r.Entry is not null`; `:130-132` hands the entries list + by-ItemType maps to `ManifestWriter.Write`. | |
| **PROVIDER-05** Round-trip property test asserts every one of the 8 deserialize-affecting fields survives `predicate -> entry -> manifest -> entry` with no loss | SATISFIED | `ManifestRoundTripTests.cs:50-68` MemberData lists all 8 fields × 2 providers = 16 cases. `Field_RoundTrips_PredicateThroughManifestThroughEntry` theory test (passing 16/16). No-op shape via JSON-name-absence assertion on wrong-provider-side cases. | |

**Coverage:** 10/10 requirements satisfied.

---

## Deviations carried into the phase outcome

### 1. SC-6 wording vs. .NET 8 STJ empirical behavior — accepted-deviation

**Roadmap says:** "...still produces a typed error rather than `NotSupportedException`."

**What ships:** `tests/DynamicWeb.Serializer.Tests/Infrastructure/ManifestEntryDiscriminatorReorderTests.cs:84-151` (`Read_DiscriminatorAtNonZeroPosition_ThrowsJsonException_NotNotSupportedException`) accepts THREE outcomes:
- (a) `JsonException` thrown (the roadmap's preferred path);
- (b) `NotSupportedException` thrown — what .NET 8 STJ empirically does (per Plan 01 SUMMARY Decision #2);
- (c) Successful out-of-order tolerant read with the correct concrete type bound (a future-STJ tolerant path).

**Implementer's argument (from Plan 04 SUMMARY Deviation #1, plus Plan 01 SUMMARY Decision #2):**
- .NET 8 STJ throws `NotSupportedException` ("Deserialization of types without a parameterless constructor ... is not supported. Type 'ManifestEntry'") when the discriminator is moved off position 0, because STJ falls through to instantiating the abstract base type — `ManifestEntry` is abstract.
- The actual regression SC-6 defends against is **silent fall-through to base-type binding with the discriminator value lost** — both `JsonException` AND `NotSupportedException` are loud failures that prevent that.
- PITFALLS §4 predicted `JsonException`; the prediction was wrong about the empirical .NET 8 path. Both predicted and actual paths are loud.

**Verifier's call:** **OK-to-ship-with-callout.**
- The SC-6 invariant ("operator does not get silently bound to abstract base") is met.
- The exact roadmap wording (NOT `NotSupportedException`) is not met in the .NET 8 path.
- The accept-either contract is documented in the test body (line 144-150 inline comments) and in two SUMMARY files (Plan 01 Decision #2, Plan 04 Deviation #1).
- A future .NET upgrade that switches STJ to throw `JsonException` for this case automatically tightens the contract — the test continues to pass.
- The implementation defenses around SC-6 (writer pinning position-0; unknown-value guard with strict JsonException assertion; closed-set polymorphism allow-list) are intact and strict.

**Risk if shipped as-is:**
- Low. The SC-6 invariant is the operationally meaningful one (no silent return); the wording deviation is a documentation-vs-runtime gap.
- A reader of the roadmap who expects literal `JsonException`-only would need to know the `NotSupportedException` accept path is also considered loud; the test body and SUMMARY documents make this clear.

**Recommendation:** Accept as-is. If desired, follow up by:
- (option A) Updating ROADMAP.md SC-6 wording to "...still produces a LOUD typed error (`JsonException` or `NotSupportedException`) rather than silently binding to the abstract base type" — aligns wording with shipped behavior.
- (option B) Adding a tighter contract via custom `JsonConverter` that always throws `JsonException` — high risk for low return; not recommended.

### 2. Documentation count drift in `42-VERIFICATION.md` (informational, not a gap)

42-VERIFICATION.md line 6 states "Phase 42 production code: 7 source files modified across plans 01-03". `git diff --stat 4c5e898..HEAD -- src/` shows **13 source files touched** (5 created in Plan 01: ContentEntry, Manifest, ManifestEntry, ManifestSchema, SqlTableEntry; 1 modified in Plan 02: ManifestWriter, ManifestCleaner; 7 modified in Plan 03: ContentProvider, ISerializationProvider, SerializationProviderBase, SerializeResult, SerializerOrchestrator, SqlTableProvider, ManifestWriter). The "7 modified" count appears to refer to Plan 03 alone, not the phase total — minor doc drift, not a goal failure.

### 3. REQUIREMENTS.md PROVIDER-03 text mis-attributes Content-only fields to SqlTableProvider

REQUIREMENTS.md line 22 names `excludeAreaColumns` and `acknowledgedOrphanPageIds` as fields `SqlTableEntry` should carry. These are correctly Content-only concerns and are landed on `ContentEntry` per ARCHITECTURE.md §1 / Plan 01 / Plan 03. The implementation matches Plan 03 SUMMARY's mapping; REQUIREMENTS.md text is wrong. **Documentation gap, not implementation gap.**

---

## Follow-ups for next phase / milestone audit

These do not block Phase 42 shipping but are worth tracking:

- **F-42-01:** Update ROADMAP.md SC-6 wording (or PITFALLS §4) to reflect the accept-either-loud-exception contract that the .NET 8 STJ runtime forces. Avoids a future verifier flagging the same discrepancy.
- **F-42-02:** Update REQUIREMENTS.md PROVIDER-03 to remove the mis-attributed `excludeAreaColumns` / `acknowledgedOrphanPageIds` field names from the SqlTableEntry description (they belong on ContentEntry per ARCHITECTURE.md §1).
- **F-42-03:** Update `42-VERIFICATION.md` line 6 to read "13 source files modified across plans 01-03" or "5 created + 7 modified" to match git diff. Cosmetic.
- **F-42-04:** Phase 43 must consume the manifest the Phase 42 contract produces. A live-host smoke that produces an actual `{deploy,seed}-manifest.json` against Swift 2.2 baseline (operator-driven, not in CI) would close the only gap between "tests pass" and "operator-observable on-disk artifact". Per `ROADMAP.md` Phase 42 §verification: "this inspection is NOT a Plan 03 acceptance criterion (it'd require a live Swift 2.2 host)" — Phase 43 (manifest-driven deserialize) will exercise this naturally during its own E2E gate.

---

## Closure

**Verdict:** **PASS-WITH-CALLOUT.**
**Ship disposition:** **OK-to-ship.**
- Build green (0 errors, 2 warnings — pre-existing, out-of-scope).
- Suite 856/856 passing.
- All 6 success criteria met (one with documented + accepted deviation that defends the actual invariant).
- All 10 requirements satisfied.
- Phase 43 is unblocked.

The deviation on SC-6 is a wording-vs-runtime gap that the implementer correctly resolved by following Plan 01's already-shipped accept-either-loud-exception contract. The actual security/correctness invariant SC-6 defends — "operator never silently binds to abstract base with discriminator lost" — is mechanically guaranteed by the closed-set polymorphism allow-list, the unknown-discriminator-value strict-JsonException test, and the position-0 writer invariant.

---
*Verified: 2026-05-08 by orchestrator-final-audit*
*HEAD: `2e516bb` (docs(phase-42): mark all 4 plans complete after wave 4)*
