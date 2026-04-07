# Pitfalls Research: Granular Serialization Control (v0.5.0)

**Domain:** Adding embedded XML handling, field-level filtering, and area consolidation to an existing YAML serializer for DynamicWeb
**Researched:** 2026-04-07
**Confidence:** HIGH (based on codebase analysis, YAML spec, .NET XML APIs, and existing patterns)

## Critical Pitfalls

### Pitfall 1: YAML Literal Block Scalar + Pretty-Printed XML Indentation Interaction

**What goes wrong:**
When you pretty-print XML and store it in a YAML literal block scalar (`|`), YamlDotNet handles the indentation correctly -- the library adds the required YAML indentation prefix to every line during emit, and strips it during parse. However, the risk is in MANUAL construction or post-processing. If code tries to manually build YAML strings with embedded pretty-printed XML, or applies regex-based transformations to the YAML output, the indentation context breaks and produces invalid YAML.

Additionally, YamlDotNet has a known issue (GitHub #523) where literal block scalars with inconsistent leading whitespace in the first content line throw `SemanticErrorException: "While scanning a literal block scalar, found extra spaces in first line"`. Pretty-printed XML naturally has the root element at column 0 and children indented -- this is fine. But if the pretty-print function accidentally adds leading whitespace to the root element, the scanner fails.

**Why it happens:**
Developers unfamiliar with YAML block scalars try to "help" by manually indenting the XML to match the surrounding YAML structure, or they use string concatenation instead of letting the serializer handle it.

**How to avoid:**
1. Pretty-print XML in the model layer (before YamlDotNet touches it). Set the string property to the pretty-printed XML. Let `ForceStringScalarEmitter` and YamlDotNet handle all YAML indentation.
2. Ensure the pretty-printed XML string starts at column 0 (no leading whitespace on the first line).
3. On deserialize, the YAML parser returns the clean XML string -- no post-processing needed.
4. Write a round-trip test: original XML -> pretty-print -> YAML serialize -> YAML deserialize -> compare with pretty-printed version (not original compact version).

**Warning signs:**
- `SemanticErrorException` mentioning "block scalar" during deserialize
- XML that looks correct in the `.yml` file but has extra/missing indentation after loading
- Tests pass with flat XML (`<a/>`) but fail with nested XML

**Phase to address:**
Phase 1 (XML pretty-print) -- validate with round-trip tests before building anything on top.

---

### Pitfall 2: Field-Level Blacklist Causes Null-Out of Excluded Fields on Deserialize

**What goes wrong:**
The existing `SaveItemFields` method (ContentDeserializer.cs line 842-850) implements source-wins by nulling out any field present in the target's ItemType definition but ABSENT from the serialized YAML:

```csharp
// Source-wins: null out item fields not present in the serialized data.
foreach (var fieldName in itemEntry.Names)
{
    if (!ItemSystemFields.Contains(fieldName) && !contentFields.ContainsKey(fieldName))
    {
        contentFields[fieldName] = null;
    }
}
```

If you add a field blacklist that excludes fields during SERIALIZATION, those excluded fields will be absent from the YAML. On DESERIALIZE, this null-out loop sees them as "missing from source" and actively destroys the target's values.

**Why it happens:**
The source-wins null-out was designed for a world where "absent from YAML" means "should be cleared." Field-level blacklisting introduces a third state: "intentionally not tracked." The deserializer cannot distinguish "missing because excluded" from "missing because should be empty."

**How to avoid:**
1. The blacklist config MUST be available at both serialize AND deserialize time. On deserialize, excluded fields must be SKIPPED entirely (not set, not nulled).
2. Add an `excludeFields` parameter to `SaveItemFields`. In the null-out loop, skip any field in the exclude set.
3. Apply the same fix to `SavePropertyItemFields` (line 775-779) which has identical null-out logic.
4. For SQL tables: `SqlTableWriter.BuildMergeCommand` must exclude blacklisted columns from both the UPDATE SET clause and the INSERT column list. Otherwise MERGE will SET excluded columns to NULL.

**Warning signs:**
- Fields that exist in the target but are absent from YAML get wiped to null after deserialize
- Works correctly when blacklist is empty; breaks as soon as any field is excluded
- Environment-specific settings (domains, API keys, connection strings) vanish after sync

**Phase to address:**
Field blacklist phase -- this MUST be solved simultaneously with the serialize-side exclusion. Never ship serialize-side filtering without the corresponding deserialize-side skip guard.

---

### Pitfall 3: CRLF Line Endings in XML Force DoubleQuoted YAML (Unreadable)

**What goes wrong:**
The existing `ForceStringScalarEmitter` (line 18) checks `value.Contains('\r')` and forces `ScalarStyle.DoubleQuoted` for any string with carriage returns. SQL Server on Windows stores XML with `\r\n` line endings. Pretty-printed XML from `XDocument.ToString()` uses `Environment.NewLine` which is `\r\n` on Windows. Result: the pretty-printed XML gets emitted as a single DoubleQuoted string with escape sequences: `"<settings>\r\n  <module/>\r\n</settings>"`. This defeats the entire purpose of pretty-printing.

**Why it happens:**
The CRLF guard in the emitter was designed to preserve exact line endings for general content. XML content does not need this preservation -- the XML spec (section 2.11) normalizes all line endings to `\n` during parsing.

**How to avoid:**
1. Normalize XML line endings to LF-only BEFORE setting the model property: `xmlString.Replace("\r\n", "\n").Replace("\r", "\n")`.
2. Apply normalization in the XML pretty-print function, NOT in the emitter (keep emitter general-purpose).
3. This is safe per XML spec: all conformant XML parsers normalize `\r\n` to `\n`.
4. On deserialize, the LF-only XML from YAML is functionally identical to the original CRLF XML.

**Warning signs:**
- Pretty-printed XML appearing as a single long DoubleQuoted line in YAML files
- YAML files that are LARGER after pretty-print than before
- `moduleSettings` field showing escaped `\r\n` instead of actual newlines

**Phase to address:**
Phase 1 (XML pretty-print) -- must be part of the core XML normalization step.

---

### Pitfall 4: XML Declaration Lost on Round-Trip

**What goes wrong:**
Some DW XML blobs include `<?xml version="1.0" encoding="utf-8"?>` and some do not. `XDocument.ToString()` OMITS the XML declaration. If the original had a declaration and the round-tripped version does not, DW's XML parser may behave differently (unlikely but possible for encoding-sensitive content).

**Why it happens:**
`XDocument.ToString()` returns only the root element and its children. The `Declaration` property is separate and must be explicitly included. `XDocument.Save(writer)` includes it based on `XmlWriterSettings.OmitXmlDeclaration`, but `ToString()` always omits it.

**How to avoid:**
1. Before pretty-printing, check if the original string starts with `<?xml`.
2. If yes, reconstruct: `xdoc.Declaration?.ToString() + "\n" + xdoc.ToString()`.
3. If no declaration in original, emit without it.
4. Test with actual DW data: `moduleSettings` and `urlDataProviderParameters` from the Swift test instances.

**Warning signs:**
- XML strings that start with `<?xml` in the database lose that prefix after round-trip
- Encoding attribute changes (e.g., `utf-16` to `utf-8` or vice versa)

**Phase to address:**
Phase 1 (XML pretty-print).

---

### Pitfall 5: Area Consolidation Creates Deserialize Ordering Dependency

**What goes wrong:**
Currently, `ContentDeserializer.DeserializePredicate` does `Services.Areas.GetArea(predicate.AreaId)` and SKIPS the predicate if the area is null. It does NOT create areas. If you consolidate the Area SQL table into ContentProvider (removing the SqlTable predicate for Area), and the target database has no matching area, deserialization silently skips everything.

Additionally, the current `SerializedArea` model only has 5 properties: AreaId (GUID), Name, SortOrder, ItemType, ItemFields. The Area SQL table has 60+ columns including Domain, DefaultLanguageId, EcomLanguageId, MasterArea, PrimaryDomain, CdnDomain, Culture, TimeZone, MailServer, etc. Most of these are environment-specific -- exactly the kind of fields that need the blacklist feature.

**Why it happens:**
The original design assumed areas pre-exist in target environments (they are typically created during DW setup). Area consolidation changes this assumption.

**How to avoid:**
1. Expand `SerializedArea` to include the full set of Area SQL columns (60+ fields as nullable properties).
2. Add area creation logic to `ContentDeserializer`: if `GetArea()` returns null, create the area from YAML data before processing pages.
3. Must call `Services.Areas.ClearCache()` after area creation (documented in `project_dw_area_cache.md` memory).
4. Apply field-level blacklist to area columns so environment-specific fields (Domain, MailServer, etc.) can be excluded.
5. The expanded `SerializedArea` is backward-compatible with old YAML because `IgnoreUnmatchedProperties` is already configured in `YamlConfiguration.BuildDeserializer()`.

**Warning signs:**
- Deserialize silently skips with "Area with ID X not found" warning
- Area properties (domain, language) not set because ContentProvider only handled ItemType fields
- Cache staleness: new area created but `GetArea()` still returns null

**Phase to address:**
Area consolidation phase -- must be implemented after field blacklist is working (blacklist is needed for environment-specific area columns).

---

### Pitfall 6: Detecting Which SQL Columns Contain XML -- Heuristics Fail

**What goes wrong:**
To pretty-print XML in SQL table YAML files, you need to identify which columns contain XML. DW stores XML as `nvarchar(max)` or `ntext`, not as the SQL `xml` type. Heuristic detection (checking if value starts with `<`) false-positives on HTML content, partial XML fragments, and any text starting with angle brackets. Attempting `XDocument.Parse()` on non-XML data crashes or produces garbage.

**Why it happens:**
DW's schema makes no distinction between XML columns and other string columns. The ~100 SQL YAML files with XML span 5+ different table patterns (DashboardWidget, EcomFeed, EcomShippings, PersonalSettings, ScheduledTask) with inconsistent column naming.

**How to avoid:**
1. Use a CONFIG-DRIVEN known-columns approach, not heuristic detection.
2. Add `xmlColumns` to the predicate config: `"xmlColumns": ["ShippingXml", "PaymentXml"]`.
3. Content provider has KNOWN XML fields: `moduleSettings` on Paragraph, `urlDataProviderParameters` on Page -- hardcode these in ContentProvider.
4. For SQL tables, require explicit declaration. As a discovery aid, log when a column contains a value that looks like XML but is not in the known list.
5. NEVER try-parse arbitrary columns as XML during production serialization.

**Warning signs:**
- Serialize crashes on non-XML data that starts with `<`
- HTML content in SQL columns getting reformatted (breaking the HTML)
- Inconsistent: some XML columns pretty-printed, others left compact

**Phase to address:**
Phase 1 (XML pretty-print) -- the detection/configuration strategy must be decided before implementation.

---

### Pitfall 7: Config Schema Change Silently Drops New Fields

**What goes wrong:**
`ConfigLoader` uses a three-class pattern: `RawPredicateDefinition` (JSON deserialization) -> `Validate()` -> `BuildPredicate()` -> `ProviderPredicateDefinition` (runtime model). Adding new fields like `excludeFields`, `xmlColumns`, `excludeXmlFields` to `ProviderPredicateDefinition` without ALSO adding them to `RawPredicateDefinition` and the `BuildPredicate()` mapping function causes the fields to be silently null/empty after config load.

**Why it happens:**
The three-class mapping is explicit (no reflection/AutoMapper). Every field added to the model requires changes in 3 places. It is easy to add the field to the record, write code that reads it, test with a manually constructed config object, and miss that the file-loaded config drops the field.

**How to avoid:**
1. When adding ANY field to `ProviderPredicateDefinition`, immediately update `RawPredicateDefinition` and `BuildPredicate()`.
2. Write a config round-trip test: create JSON with all new fields populated -> `ConfigLoader.Load()` -> verify every field is present.
3. Consider consolidating to 2 classes (eliminate Raw or use `JsonSerializer` directly to the model) since the beta product does not need forward/backward compat.

**Warning signs:**
- Config loads without error but field blacklists are empty
- Admin UI shows blank exclude lists despite JSON file having values
- Feature "does not work" but no errors logged (config field silently dropped)

**Phase to address:**
Every phase that adds config fields -- this is a process pitfall. The first phase should establish the pattern with a test.

---

### Pitfall 8: Pretty-Print on Serialize Without Compact on Deserialize Creates Git Noise

**What goes wrong:**
If you pretty-print XML on serialize (for readable YAML) but write the pretty-printed XML directly to the database on deserialize, the database now contains pretty-printed XML. When the SAME environment re-serializes, the XML is already pretty-printed, so it gets pretty-printed AGAIN (double-indented) or matches (no change). But if a DIFFERENT serializer version (or DW itself) writes compact XML to the DB, the next serialize shows the entire XML blob as "changed" because compact != pretty-printed.

**Why it happens:**
The serializer is not the only thing that writes to these columns. DW admin UI, modules, and other tools write compact XML. If the serializer does not normalize on deserialize, the DB format is inconsistent.

**How to avoid:**
1. On SERIALIZE: pretty-print for YAML readability.
2. On DESERIALIZE: compact the XML before writing to DB. Use `xdoc.ToString(SaveOptions.DisableFormatting)` to produce a canonical compact form.
3. This makes the serialize->DB path idempotent: compact XML in DB -> pretty-print in YAML -> compact back to DB = original.
4. Alternative: write pretty-printed XML to DB (DW does not care about XML whitespace). But this changes DB content unnecessarily and may conflict with DW's own writes.

**Warning signs:**
- Serialize -> deserialize -> serialize cycle shows XML diffs (not idempotent)
- DB contains mix of compact and pretty-printed XML depending on how the data was last written
- Checksum drift: `SqlTableReader.CalculateChecksum` produces different hashes for same logical data

**Phase to address:**
Phase 1 (XML pretty-print) -- decide the deserialize strategy upfront. Recommend: compact on deserialize for clean round-trips.

---

## Technical Debt Patterns

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|----------------|-----------------|
| Heuristic XML detection (try-parse) instead of config-driven known-columns | No config changes needed | False positives on HTML, crashes on malformed data, silent corruption | Never for serialize; acceptable only as a discovery logging aid |
| Normalize XML on serialize only, skip deserialize compaction | Simpler code, one-way transform | DB accumulates pretty-printed XML, serialize becomes non-idempotent | Never -- always compact on deserialize |
| Expanding SerializedArea with 60+ nullable properties | Directly maps to SQL schema | Massive model class, most fields unused | Acceptable for 0.x; can refactor to groups later |
| Storing XML column list in code rather than config | No schema change needed | Adding a new XML column requires code change + rebuild | Never -- put in config from the start |
| Skipping null-out guard for SqlTable field blacklist | SQL MERGE just sets all available columns | Excluded columns get SET to NULL/empty in target | Never -- must exclude from MERGE command |

## Integration Gotchas

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|------------------|
| `ForceStringScalarEmitter` + XML with CRLF | Pretty-printed XML emitted as DoubleQuoted (unreadable) | Normalize to LF before setting model property; XML spec says this is safe |
| `XDocument.Parse()` + `ToString()` | XML declaration (`<?xml ...?>`) silently dropped | Check and preserve `xdoc.Declaration` explicitly |
| `SqlTableWriter.BuildMergeCommand` + field exclusion | Excluded columns still in MERGE UPDATE SET, setting them to NULL | Filter `updateColumns` and `insertColumns` to remove blacklisted columns |
| `Services.Areas.GetArea()` after area creation | AreaService cache returns stale null | Call `Services.Areas.ClearCache()` after area insert (per `project_dw_area_cache.md`) |
| `SaveItemFields` null-out + field blacklist | Excluded fields get nulled because absent from YAML | Add `excludeFields` set parameter; skip excluded fields in null-out loop |
| `ConfigLoader.BuildPredicate()` + new fields | New fields on `ProviderPredicateDefinition` but not mapped from `RawPredicateDefinition` | Update all 3 locations: model record, raw class, mapping function |
| `XDocument.Parse()` on non-XML data | Crash or mangled output on HTML/text columns | Config-driven known-columns list; never try-parse arbitrary columns |

## Performance Traps

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|----------------|
| XML parsing every cell in every SQL row during serialize | Serialize time 10x slower for large tables | Only parse columns explicitly listed as XML (known-list) | Tables with 1000+ rows and nvarchar(max) columns |
| Pretty-printing XML during deserialize (unnecessary work) | Deserialize slower with no benefit -- DB stores compact XML | Only pretty-print on SERIALIZE; compact on DESERIALIZE | Always -- deserialize should never format XML |
| Re-parsing XML that is already pretty-printed (double work) | XML parsed, formatted, parsed again on next serialize cycle | Check if XML is already formatted before re-formatting (or accept minor cost) | Only noticeable with very large XML blobs (>100KB) |

## UX Pitfalls

| Pitfall | User Impact | Better Approach |
|---------|-------------|-----------------|
| Massive git diff on first serialize after enabling pretty-print | User thinks something is wrong, reverts, re-runs | Log: "XML format migration: N files reformatted. One-time change." |
| Field blacklist configured but no visual feedback | User cannot verify exclusion is working -- field simply absent | Log: "Excluded 3 fields from [entity]: [field1, field2, field3]" during serialize |
| Area consolidation silently drops area properties | Area domain, language not synced because old area.yml lacks new fields | Validate at config load: warn if Area table has no SqlTable predicate AND ContentProvider area mapping is incomplete |

## "Looks Done But Isn't" Checklist

- [ ] **XML pretty-print:** Round-trip test with CRLF line endings -- verify `\r\n` XML survives serialize-deserialize as LF
- [ ] **XML pretty-print:** XML declaration preservation -- verify `<?xml ...?>` header survives round-trip when present in original
- [ ] **XML pretty-print:** Empty/null XML handling -- verify null, empty string, and whitespace-only values do not crash the parser
- [ ] **XML pretty-print:** Malformed XML fallback -- verify non-parseable XML is passed through as-is (not crashed, not corrupted)
- [ ] **Field blacklist serialize:** Deserialize-side guard present -- verify excluded fields are NOT nulled on deserialize
- [ ] **Field blacklist SQL:** MERGE command excludes blacklisted columns -- verify excluded columns not in UPDATE SET
- [ ] **Field blacklist SQL:** INSERT command excludes blacklisted columns -- verify excluded columns not in INSERT INTO
- [ ] **Area consolidation:** Area CREATION works (not just update) -- verify deserialize into DB with no matching area
- [ ] **Area consolidation:** AreaService cache cleared after creation -- verify new area visible to subsequent ContentProvider calls
- [ ] **Area consolidation:** Environment-specific fields blacklistable -- verify Domain, MailServer, etc. can be excluded
- [ ] **Config schema:** `BuildPredicate()` maps all new fields -- verify config round-trips through load/save/load
- [ ] **Config schema:** Admin UI reads/writes new fields -- verify predicate edit screen shows exclude lists

## Recovery Strategies

| Pitfall | Recovery Cost | Recovery Steps |
|---------|---------------|----------------|
| Null-out of excluded fields | MEDIUM | Re-serialize from source to regenerate complete YAML; re-deserialize. Target DB may need manual restore from backup for nulled values. |
| XML declaration lost | LOW | Re-serialize to regenerate YAML. DW is likely tolerant of missing declarations. |
| Area properties missing after consolidation | MEDIUM | Re-add SqlTable predicate for Area table temporarily. Or manually set area properties in DW admin. |
| Config fields silently dropped | LOW | Fix mapping code, re-load config. No data loss -- feature just was not active. |
| Pretty-printed XML corrupted in DB | HIGH | Restore from backup or re-serialize from clean source. Prevention much cheaper than recovery. |
| CRLF XML in DoubleQuoted YAML (unreadable but functional) | LOW | Add LF normalization, re-serialize. Data is not corrupted, just ugly. |

## Pitfall-to-Phase Mapping

| Pitfall | Prevention Phase | Verification |
|---------|------------------|--------------|
| Literal block indentation (P1) | XML pretty-print | Round-trip test: serialize -> file -> deserialize -> byte-compare XML string |
| Null-out of excluded fields (P2) | Field blacklist | Test: exclude field X, serialize, deserialize, verify X unchanged in target |
| CRLF -> DoubleQuoted (P3) | XML pretty-print | Test: Windows CRLF XML -> literal block in YAML (not DoubleQuoted) |
| XML declaration lost (P4) | XML pretty-print | Test: XML with and without `<?xml` -> both preserved correctly |
| Area ordering dependency (P5) | Area consolidation | Test: deserialize into empty DB -> area created -> pages created successfully |
| XML column detection (P6) | XML pretty-print | Config-driven known-list with discovery logging; test: unknown column logged not crashed |
| Config schema mapping (P7) | Every config-changing phase | Test: write JSON with new fields -> load -> verify fields non-null |
| Serialize/deserialize format mismatch (P8) | XML pretty-print | Test: serialize -> deserialize -> serialize -> no diff in YAML files |

## Sources

- **Codebase analysis (primary):**
  - `ContentDeserializer.cs` lines 842-850: source-wins null-out logic for ItemFields
  - `ContentDeserializer.cs` lines 775-779: source-wins null-out for PropertyItem fields
  - `ForceStringScalarEmitter.cs` line 18: CRLF detection forcing DoubleQuoted style
  - `ConfigLoader.cs` lines 79-92: three-class mapping pattern (Raw -> Build -> Model)
  - `SqlTableWriter.cs` lines 48-52: MERGE UPDATE column list construction
  - `SerializedArea.cs`: current 5-field model needing expansion to 60+ fields
- [YamlDotNet literal block scalar issue #523](https://github.com/aaubry/YamlDotNet/issues/523) -- indentation errors
- [YAML Multiline Strings reference](https://yaml-multiline.info/) -- chomp indicators and block scalar rules
- [.NET XDocument whitespace preservation](https://learn.microsoft.com/en-us/dotnet/standard/linq/preserve-white-space-loading-parsing-xml)
- [.NET XDocument serialization whitespace](https://learn.microsoft.com/en-us/dotnet/standard/linq/preserve-white-space-serializing)
- Memory: `feedback_no_backcompat.md` (0.x beta, format changes OK), `project_dw_area_cache.md` (AreaService cache clearing required)

---
*Pitfalls research for: DynamicWeb.Serializer v0.5.0 -- granular serialization control*
*Researched: 2026-04-07*
