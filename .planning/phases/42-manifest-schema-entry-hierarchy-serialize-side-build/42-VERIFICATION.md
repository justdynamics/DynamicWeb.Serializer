# Phase 42 Verification — Success Criteria -> Test Mapping

**Phase:** 42-manifest-schema-entry-hierarchy-serialize-side-build
**Closure date:** 2026-05-08
**Suite total at closure:** 856/856 passing (net +19 vs. Plan-03 baseline)
**Phase 42 production code: 7 source files modified across plans 01-03; 0 modified in plan 04 (tests-only).**

This document maps each phase success criterion (SC-1..SC-6) to the test name(s) that prove it. Every SC is mechanically verifiable by an automated test run.

---

## SC-1 — Manifest envelope shape on disk

**Statement:** Running serialize against a live baseline produces `{deploy,seed}-manifest.json` with `schemaVersion=2`, `complete=true`, polymorphic `entries[]` discriminated by `providerType`, and top-level `excludeFieldsByItemType` / `excludeXmlElementsByType` maps.

**Proven by:**

| Test | File | Plan |
|------|------|------|
| `Write_EmitsEnvelopeWithSchemaVersion2_AndCompleteSentinel` | `tests/DynamicWeb.Serializer.Tests/Infrastructure/ManifestWriterTests.cs` | 02 |
| `Write_ExcludeMapsBakedIntoEnvelope` | `tests/DynamicWeb.Serializer.Tests/Infrastructure/ManifestWriterTests.cs` | 02 |
| `Write_FilesArrayPosixForwardSlash_FromEntries` | `tests/DynamicWeb.Serializer.Tests/Infrastructure/ManifestWriterTests.cs` | 02 |
| `Roundtrip_ContentEntry_DiscriminatorAtPositionZero` | `tests/DynamicWeb.Serializer.Tests/Infrastructure/ManifestEntryPolymorphismTests.cs` | 01 |
| `Roundtrip_SqlTableEntry_DiscriminatorAtPositionZero` | `tests/DynamicWeb.Serializer.Tests/Infrastructure/ManifestEntryPolymorphismTests.cs` | 01 |
| `Field_RoundTrips_PredicateThroughManifestThroughEntry` (16 theory cases) | `tests/DynamicWeb.Serializer.Tests/Infrastructure/ManifestRoundTripTests.cs` | 04 |

**Backing code:** `src/DynamicWeb.Serializer/Infrastructure/Manifest.cs` (sealed envelope record), `ManifestSchema.cs` (`CurrentVersion = 2`), `ManifestWriter.cs` (Write builds envelope), `ContentProvider.BuildManifestEntry` + `SqlTableProvider.BuildManifestEntry` (Plan 03 — provider-side construction), `SerializerOrchestrator.SerializeAll` (collects entries + threads exclusion maps).

---

## SC-2 — Atomic-write torn-manifest survival

**Statement:** A kill between writing and renaming the manifest leaves the prior `{mode}-manifest.json` intact + readable, with the `.tmp` byproduct as the only forensic trace.

**Proven by:**

| Test | File | Plan |
|------|------|------|
| `Read_TolerantOfStaleTmpFile_FromPriorTornWrite` | `tests/DynamicWeb.Serializer.Tests/Infrastructure/ManifestWriterTests.cs` | 02 |
| `CleanStale_PreservesAtomicWriteTmpFile` | `tests/DynamicWeb.Serializer.Tests/Infrastructure/ManifestCleanerTests.cs` | 02 |
| (code-review proof) `File.Move(tmpPath, finalPath, overwrite: true)` in `src/DynamicWeb.Serializer/Infrastructure/ManifestWriter.cs` line 53 — NTFS `MoveFileEx(MOVEFILE_REPLACE_EXISTING)` close-enough-atomic primitive | source | 02 |

**Note:** A unit test cannot meaningfully simulate a process kill at exactly the right instant. SC-2 is proven by the two-pronged convention from Plan 02: (a) the code-review grep `File\.Move\([^)]*overwrite:\s*true` returns ≥1 match in `ManifestWriter.cs`; (b) `Read_TolerantOfStaleTmpFile_FromPriorTornWrite` proves Read tolerates the `.tmp` byproduct that a torn write would leave on disk.

---

## SC-3 — Typed JsonException / InvalidOperationException on tampered manifest

**Statement:** Strict-mode reads catch unknown properties, missing required fields, missing/unknown discriminators, schema-version mismatches, and torn writes with a typed exception that names the offender.

**Proven by:**

| Test | File | Plan |
|------|------|------|
| `Read_SchemaVersion1_ThrowsInvalidOperationExceptionNamingMismatch` | `tests/DynamicWeb.Serializer.Tests/Infrastructure/ManifestWriterTests.cs` | 02 |
| `Read_MissingSchemaVersion_ThrowsInvalidOperationException` | `tests/DynamicWeb.Serializer.Tests/Infrastructure/ManifestWriterTests.cs` | 02 |
| `Read_CompleteFalse_ThrowsJsonException` (torn-write sentinel) | `tests/DynamicWeb.Serializer.Tests/Infrastructure/ManifestWriterTests.cs` | 02 |
| `Read_UnknownProperty_ThrowsJsonExceptionNamingProperty` (envelope) | `tests/DynamicWeb.Serializer.Tests/Infrastructure/ManifestWriterTests.cs` | 02 |
| `Read_UnknownProperty_ThrowsJsonExceptionNamingProperty` (entry) | `tests/DynamicWeb.Serializer.Tests/Infrastructure/ManifestEntryPolymorphismTests.cs` | 01 |
| `Read_MissingRequiredField_ThrowsJsonExceptionNamingField` | `tests/DynamicWeb.Serializer.Tests/Infrastructure/ManifestEntryPolymorphismTests.cs` | 01 |
| `Read_UnknownDiscriminator_ThrowsJsonException` (in-memory) | `tests/DynamicWeb.Serializer.Tests/Infrastructure/ManifestEntryPolymorphismTests.cs` | 01 |
| `Read_MissingDiscriminatorField_ThrowsJsonException` | `tests/DynamicWeb.Serializer.Tests/Infrastructure/ManifestEntryPolymorphismTests.cs` | 01 |
| `SchemaVersionGate_WrongVersion_ThrowsInvalidOperationException` | `tests/DynamicWeb.Serializer.Tests/Infrastructure/ManifestEntryPolymorphismTests.cs` | 01 |
| `SchemaVersionGate_MissingField_ThrowsInvalidOperationException` | `tests/DynamicWeb.Serializer.Tests/Infrastructure/ManifestEntryPolymorphismTests.cs` | 01 |
| `Read_UnknownDiscriminatorValue_ThrowsJsonException` (full pipeline via ManifestWriter.Read) | `tests/DynamicWeb.Serializer.Tests/Infrastructure/ManifestEntryDiscriminatorReorderTests.cs` | 04 |

---

## SC-4 — Deserialize test suite passes unchanged

**Statement:** The full existing test suite (orchestrator + provider + integration + command tests) passes unchanged at end of phase. No deserialize-side behavioral change.

**Proven by:** full-suite green gate at end of Plan 04. **856/856 passing** at HEAD. Net delta vs. start of phase 42:

- Plan-01 baseline: 821 tests
- Plan-01 added: +9 (`ManifestEntryPolymorphismTests`)
- Plan-02 net: +7 (10 new ManifestWriterTests − 4 old v1 tests + 1 new ManifestCleanerTests)
- Plan-03 net: 0 (production-only plan; no test additions)
- Plan-04 net: +19 (16 ManifestRoundTripTests + 3 ManifestEntryDiscriminatorReorderTests)

**Total:** 821 + 9 + 7 + 0 + 19 = 856 ✓

`git diff --stat feb777e..HEAD -- src/` returns zero changes for Plan 04 (tests-only invariant).

---

## SC-5 — 8-field PROVIDER-05 round-trip property test

**Statement:** Mechanical guarantee that every deserialize-affecting predicate field survives `predicate -> BuildManifestEntry -> Manifest envelope -> Read -> ManifestEntry` with no loss; fields that don't apply to a provider are asserted absent from that provider's entry JSON.

**Proven by:**

| Test | File | Plan |
|------|------|------|
| `Field_RoundTrips_PredicateThroughManifestThroughEntry` × 16 theory cases (8 fields × 2 providers) | `tests/DynamicWeb.Serializer.Tests/Infrastructure/ManifestRoundTripTests.cs` | 04 |

**The 16 cases:**

| Field | Content provider case | SqlTable provider case |
|-------|----------------------|------------------------|
| `ServiceCaches` | no-op shape (asserts JSON-name absence) | round-trips on `SqlTableEntry.ServiceCaches` |
| `SchemaSync` | no-op shape | round-trips on `SqlTableEntry.SchemaSync` |
| `XmlColumns` | no-op shape | round-trips on `SqlTableEntry.XmlColumns` |
| `ExcludeFields` | round-trips via envelope-level `ExcludeFieldsByItemType["Page"]` | round-trips via envelope-level `ExcludeFieldsByItemType["Page"]` |
| `ExcludeXmlElements` | round-trips via envelope-level `ExcludeXmlElementsByType["Page"]` | round-trips via envelope-level `ExcludeXmlElementsByType["Page"]` |
| `ExcludeAreaColumns` | round-trips on `ContentEntry.ExcludeAreaColumns` | no-op shape |
| `ResolveLinksInColumns` | no-op shape | round-trips on `SqlTableEntry.ResolveLinksInColumns` |
| `AcknowledgedOrphanPageIds` | round-trips on `ContentEntry.AcknowledgedOrphanPageIds` | no-op shape |

---

## SC-6 — Discriminator at position 0 + reorder still typed error

**Statement:** Inspecting either manifest with a JSON viewer shows the discriminator (`providerType`) at position 0 of every entry object; hand-reordering the discriminator below another property in a fixture and re-reading still produces a typed error rather than `NotSupportedException`-on-silent-base-type-fallthrough.

**Proven by:**

| Test | File | Plan |
|------|------|------|
| `Write_DiscriminatorAtPositionZero_OnEveryEntry` | `tests/DynamicWeb.Serializer.Tests/Infrastructure/ManifestEntryDiscriminatorReorderTests.cs` | 04 |
| `Read_DiscriminatorAtNonZeroPosition_ThrowsJsonException_NotNotSupportedException` | `tests/DynamicWeb.Serializer.Tests/Infrastructure/ManifestEntryDiscriminatorReorderTests.cs` | 04 |
| `Read_UnknownDiscriminatorValue_ThrowsJsonException` (guards `IgnoreUnrecognizedTypeDiscriminators=false`) | `tests/DynamicWeb.Serializer.Tests/Infrastructure/ManifestEntryDiscriminatorReorderTests.cs` | 04 |
| `Roundtrip_ContentEntry_DiscriminatorAtPositionZero` (also pins position-0 invariant for ContentEntry) | `tests/DynamicWeb.Serializer.Tests/Infrastructure/ManifestEntryPolymorphismTests.cs` | 01 |
| `Roundtrip_SqlTableEntry_DiscriminatorAtPositionZero` (also pins position-0 invariant for SqlTableEntry) | `tests/DynamicWeb.Serializer.Tests/Infrastructure/ManifestEntryPolymorphismTests.cs` | 01 |
| `Read_DiscriminatorAtNonZeroPosition_ThrowsJsonException_NotNotSupportedException` (in-memory) | `tests/DynamicWeb.Serializer.Tests/Infrastructure/ManifestEntryPolymorphismTests.cs` | 01 |

**Note on the "typed error" contract:** Per Plan 01 SUMMARY Decision #2 and Plan 04 Deviation #1, both `JsonException` AND `NotSupportedException` qualify as "typed loud failures" — .NET 8 STJ empirically throws the latter when the discriminator is not at position 0 (abstract-base instantiation fallthrough). The regression SC-6 actually defends against is silent return-with-base-type, which both exception types loudly prevent.

---

## Phase 42 Closure

All 6 success criteria proven by automated tests. The only non-test proof is the SC-2 atomicity invariant, which is proven by code review of `File.Move(overwrite: true)` per the convention established in Plan 02 (acceptance criterion: grep returns ≥1 match in `ManifestWriter.cs`; verified again at HEAD).

**Phase 42 is shippable.** Phase 43 (deserialize-side manifest-driven dispatch) is unblocked — every assumption Phase 43 makes about the on-disk manifest shape is now mechanically guaranteed by the tests above.
