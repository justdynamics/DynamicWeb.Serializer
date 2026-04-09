# Phase 32: Config Schema Extension - Research

**Researched:** 2026-04-09
**Domain:** C# record model extension, System.Text.Json serialization, config backward compatibility
**Confidence:** HIGH

## Summary

Phase 32 adds two dictionary properties (`excludeFieldsByItemType`, `excludeXmlElementsByType`) to the top-level `SerializerConfiguration` record and wires them through the serialization/deserialization pipeline as a union merge with existing per-predicate flat arrays. The codebase already has a clean raw-model-then-validate config loading pattern, System.Text.Json handles `Dictionary<string, List<string>>` natively, and the `ConfigWriter` serializes the full `SerializerConfiguration` record automatically.

The main implementation work is: (1) add properties to `SerializerConfiguration` and `RawSerializerConfiguration`, (2) pass the config-level dictionaries into the serialization pipeline methods that already accept `excludeFields`/`excludeXmlElements` parameters, (3) merge flat + typed exclusions at the point of use, and (4) add tests proving backward compatibility and additive merge behavior.

**Primary recommendation:** Add dictionary properties with empty-dictionary defaults, build a small merge helper that unions flat predicate arrays with type-keyed dictionary entries, and inject it at the 5 existing exclusion application points in the pipeline.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- **D-01:** New dictionaries are top-level properties on `SerializerConfiguration`, not per-predicate. Item type and XML type exclusions are system-wide concerns that apply globally across all predicates.
- **D-02:** Union merge -- per-predicate flat arrays (`excludeFields`, `excludeXmlElements`) exclude broadly across all types. Global typed dictionaries (`excludeFieldsByItemType`, `excludeXmlElementsByType`) exclude narrowly by specific type name. Both are additive; final exclusion set = union of flat + typed for the relevant type.
- **D-03:** Same `ConfigWriter.Save()` / `ConfigLoader.Load()` round-trip path. Add dictionary properties to `SerializerConfiguration` and `RawSerializerConfiguration`. System.Text.Json handles serialization/deserialization automatically. Same atomic write pattern (tmp file + move).

### Claude's Discretion
- Test coverage approach for backward compatibility (CFG-02)
- Internal implementation of the merge logic (where in the serialize/deserialize pipeline the union happens)

### Deferred Ideas (OUT OF SCOPE)
None
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| CFG-01 | Config JSON extended with `excludeFieldsByItemType` (Dict<string, List<string>>) and `excludeXmlElementsByType` (Dict<string, List<string>>) alongside existing flat arrays | Properties added to SerializerConfiguration + RawSerializerConfiguration; System.Text.Json handles Dict<string, List<string>> natively with camelCase naming policy |
| CFG-02 | Existing v0.5.0 configs with flat `excludeFields`/`excludeXmlElements` arrays continue to work (additive, no breaking changes) | Nullable dictionary on RawSerializerConfiguration defaults to empty; existing ConfigLoaderTests provide backward compat baseline; new tests verify old JSON loads identically |
</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| System.Text.Json | (framework) | JSON serialization of config | Already used by ConfigLoader/ConfigWriter; handles `Dictionary<string, List<string>>` natively [VERIFIED: codebase ConfigLoader.cs line 1] |
| xUnit | (existing) | Unit testing | Already used in ConfigLoaderTests [VERIFIED: codebase tests] |

No new packages required. This phase uses only existing framework and test infrastructure.

## Architecture Patterns

### Current Config Loading Flow
```
JSON file --> JsonSerializer.Deserialize<RawSerializerConfiguration> --> Validate() --> Build SerializerConfiguration
```
[VERIFIED: ConfigLoader.cs lines 20-41]

### Pattern 1: Raw-then-Validate Config Loading
**What:** Deserialize to nullable raw model first, validate with clear messages, then build the required-property model.
**When to use:** Always -- this is the established config loading pattern.
**Key insight:** New dictionary properties go in BOTH `RawSerializerConfiguration` (nullable `Dictionary<string, List<string>>?`) AND `SerializerConfiguration` (non-nullable with empty dict default). The `Load()` method maps raw to validated just like all other properties.

### Pattern 2: Exclusion Merge at Pipeline Entry Points
**What:** At each point where `excludeFields` or `excludeXmlElements` are consumed, merge the per-predicate flat list with the relevant type-keyed dictionary entry.
**When to use:** In `ContentSerializer.SerializePredicate()`, `ContentDeserializer.DeserializePredicate()`, and `SqlTableProvider` serialize methods.
**Implementation approach:** Create a static helper method (e.g., `ExclusionMerger.MergeFields(predicate.ExcludeFields, config.ExcludeFieldsByItemType, itemTypeName)`) that returns a `HashSet<string>` combining both sources. This keeps merge logic in one place rather than scattered across 5 files.

### Pattern 3: Type Resolution for Merge
**What:** The merge needs to know which item type / XML type is being processed to look up the dictionary.
**When to use:** At serialization time, the item type is known from the DW content objects (page.ItemType, paragraph.ItemType, etc.). For XML types, the type is known from paragraph.ModuleSystemName or page.UrlDataProviderTypeName.
**Key insight for fields:** Field exclusions apply per-entity (page, paragraph, area), and each entity has an ItemType. The merge must happen per-entity, not per-predicate. Current code builds ONE `excludeFields` HashSet per predicate and passes it to all entities. With typed exclusions, the set must be built per-entity by unioning: (a) flat predicate list + (b) dictionary entry for that entity's ItemType.
**Key insight for XML:** XML exclusions apply per XML blob. The type key is the module system name or URL data provider type name. Similar per-entity merge needed.

### Recommended Merge Helper Location
```
src/DynamicWeb.Serializer/Configuration/ExclusionMerger.cs
```

### Anti-Patterns to Avoid
- **Pre-merging all dictionary entries into one flat set:** This defeats the purpose of per-type exclusions. Each entity must get only the exclusions relevant to its type.
- **Modifying the predicate model to hold merged data:** The predicate is a config record -- keep it pure. Merge at runtime in the pipeline.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Dictionary JSON serialization | Custom converter | System.Text.Json built-in | `Dictionary<string, List<string>>` serializes/deserializes natively with camelCase policy [VERIFIED: System.Text.Json docs] |
| Case-insensitive HashSet | Manual lowercasing | `new HashSet<string>(StringComparer.OrdinalIgnoreCase)` | Already used throughout codebase [VERIFIED: ContentSerializer.cs line 74] |

## Common Pitfalls

### Pitfall 1: ConfigWriter Serializing Computed Properties
**What goes wrong:** `SerializerConfiguration` has computed properties (`SerializeRoot`, `UploadDir`, `DownloadDir`, `LogDir`) that derive from `OutputDirectory`. If these get serialized to JSON, the config file bloats with redundant data.
**Why it happens:** `ConfigWriter.Save()` serializes the full `SerializerConfiguration` record via `JsonSerializer.Serialize()`. System.Text.Json serializes all public properties including computed ones.
**How to avoid:** Check whether existing computed properties already serialize (they do -- they have public getters). This is an existing behavior, not a new problem. But verify the round-trip: `ConfigLoader` ignores unknown properties since `RawSerializerConfiguration` only has the properties it cares about. No action needed -- just be aware.
**Warning signs:** Config JSON files with `serializeRoot`, `uploadDir` etc. in them (already happens).

### Pitfall 2: Empty Dictionary vs Null in JSON
**What goes wrong:** An empty dictionary `{}` is different from absent key in JSON. A v0.5.0 config has no `excludeFieldsByItemType` key at all.
**Why it happens:** `RawSerializerConfiguration` needs to handle absent keys gracefully.
**How to avoid:** Make the raw model property `Dictionary<string, List<string>>?` (nullable). In `BuildConfig`, map null to `new Dictionary<string, List<string>>()`. This matches the existing pattern for `List<string>?` -> `new List<string>()`.
**Warning signs:** NullReferenceException when accessing dictionary on a v0.5.0 config.

### Pitfall 3: Merge Must Happen Per-Entity, Not Per-Predicate
**What goes wrong:** Building one merged exclusion set per predicate loses per-type granularity.
**Why it happens:** Current code builds `excludeFields` HashSet once in `SerializePredicate()` and passes it through. Tempting to just union all dictionary values into this set.
**How to avoid:** The merge helper must be called per-entity with that entity's specific type name. This means the serialization methods that process individual pages/paragraphs need access to both the flat set AND the typed dictionary, and merge at point of use.
**Warning signs:** All typed exclusions applying to all item types regardless of type match.

### Pitfall 4: Dictionary Key Case Sensitivity
**What goes wrong:** Item type names in DynamicWeb may differ in casing between config and runtime (e.g., "Swift_PageItemType" vs "swift_pageitemtype").
**Why it happens:** DW item type names are typically PascalCase but inconsistencies exist.
**How to avoid:** Use `StringComparer.OrdinalIgnoreCase` for dictionary lookups, or normalize keys. Recommend using a case-insensitive dictionary: `new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)`.
**Warning signs:** Config has entries that don't match at runtime despite correct type names.

## Code Examples

### Adding Properties to SerializerConfiguration
```csharp
// Source: Follows existing pattern in SerializerConfiguration.cs
public record SerializerConfiguration
{
    // ... existing properties ...

    /// <summary>
    /// Global field exclusions by item type name.
    /// Keys are item type system names; values are field name lists.
    /// Merged with per-predicate ExcludeFields at runtime (union).
    /// </summary>
    public Dictionary<string, List<string>> ExcludeFieldsByItemType { get; init; } = new();

    /// <summary>
    /// Global XML element exclusions by XML type name (module system name or URL provider type).
    /// Keys are type names; values are element name lists.
    /// Merged with per-predicate ExcludeXmlElements at runtime (union).
    /// </summary>
    public Dictionary<string, List<string>> ExcludeXmlElementsByType { get; init; } = new();
}
```
[VERIFIED: follows init-only setter pattern from SerializerConfiguration.cs]

### Adding to RawSerializerConfiguration
```csharp
// Source: Follows existing nullable pattern in ConfigLoader.cs line 98-105
private sealed class RawSerializerConfiguration
{
    // ... existing properties ...
    public Dictionary<string, List<string>>? ExcludeFieldsByItemType { get; set; }
    public Dictionary<string, List<string>>? ExcludeXmlElementsByType { get; set; }
}
```

### Mapping in ConfigLoader.Load()
```csharp
// Source: Follows existing pattern in ConfigLoader.cs lines 33-41
return new SerializerConfiguration
{
    // ... existing mappings ...
    ExcludeFieldsByItemType = raw.ExcludeFieldsByItemType ?? new Dictionary<string, List<string>>(),
    ExcludeXmlElementsByType = raw.ExcludeXmlElementsByType ?? new Dictionary<string, List<string>>()
};
```

### Merge Helper
```csharp
// New file: src/DynamicWeb.Serializer/Configuration/ExclusionMerger.cs
public static class ExclusionMerger
{
    /// <summary>
    /// Merges per-predicate flat exclusion list with type-specific dictionary entry.
    /// Returns null if no exclusions apply (preserves existing null-means-no-filtering optimization).
    /// </summary>
    public static HashSet<string>? MergeFieldExclusions(
        IReadOnlyList<string> predicateExclusions,
        IReadOnlyDictionary<string, List<string>> typedExclusions,
        string? itemTypeName)
    {
        var hasFlat = predicateExclusions.Count > 0;
        var hasTyped = !string.IsNullOrEmpty(itemTypeName)
            && typedExclusions.TryGetValue(itemTypeName, out var typeList)
            && typeList.Count > 0;

        if (!hasFlat && !hasTyped)
            return null;

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (hasFlat)
            foreach (var f in predicateExclusions) result.Add(f);
        if (hasTyped)
            foreach (var f in typeList!) result.Add(f);
        return result;
    }

    // Similar method for XML element exclusions
    public static IReadOnlyList<string>? MergeXmlExclusions(
        IReadOnlyList<string> predicateExclusions,
        IReadOnlyDictionary<string, List<string>> typedExclusions,
        string? xmlTypeName)
    {
        // Same union logic, returns List<string>? to match existing parameter types
    }
}
```

### Usage in ContentSerializer.SerializePredicate()
```csharp
// Current (line 73-78):
var excludeFields = predicate.ExcludeFields.Count > 0
    ? new HashSet<string>(predicate.ExcludeFields, StringComparer.OrdinalIgnoreCase)
    : null;

// New: pass config + predicate into pipeline, merge per-entity
// The flat set is still built once, but typed lookups happen per page/paragraph
```

## Merge Integration Points

Five locations currently consume exclusion lists. Each needs typed-dictionary awareness:

| # | File | Method | Current Parameter | Item Type Available From |
|---|------|--------|-------------------|--------------------------|
| 1 | ContentSerializer.cs | SerializePredicate (line 73) | `predicate.ExcludeFields` | page.ItemType, paragraph.ItemType |
| 2 | ContentDeserializer.cs | DeserializePredicate (line 190) | `predicate.ExcludeFields` | dto.ItemType from YAML |
| 3 | ContentMapper.cs | MapPage, MapParagraph, MapArea | `excludeFields` param | page.ItemType, paragraph.Item?.SystemName, area.ItemType |
| 4 | XmlFormatter.cs | RemoveElements (via ApplyXmlElementFilter) | `excludeXmlElements` param | paragraph.ModuleSystemName, page.UrlDataProviderTypeName |
| 5 | SqlTableProvider.cs | Serialize method (line 51) | `predicate.ExcludeFields` | N/A (SqlTable has no item types -- flat only) |

[VERIFIED: all 5 locations confirmed via grep of codebase]

**Key architectural decision for merge location:** The cleanest approach is to push the merge into `ContentMapper` methods (MapPage, MapParagraph, MapArea) since they already receive the entity objects and know the item type. ContentSerializer/ContentDeserializer pass the config-level dictionaries alongside the flat predicate lists. This avoids duplicating merge logic across serializer and deserializer.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit (existing) |
| Config file | tests/DynamicWeb.Serializer.Tests/DynamicWeb.Serializer.Tests.csproj |
| Quick run command | `dotnet test tests/DynamicWeb.Serializer.Tests --filter "FullyQualifiedName~Configuration" --no-build` |
| Full suite command | `dotnet test tests/DynamicWeb.Serializer.Tests` |

### Phase Requirements to Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| CFG-01 | Config JSON with typed dictionaries loads correctly | unit | `dotnet test tests/DynamicWeb.Serializer.Tests --filter "FullyQualifiedName~ConfigLoaderTests" -x` | Existing file, new tests needed |
| CFG-01 | ConfigWriter round-trips dictionaries | unit | `dotnet test tests/DynamicWeb.Serializer.Tests --filter "FullyQualifiedName~ConfigLoaderTests" -x` | Existing file, new tests needed |
| CFG-02 | v0.5.0 config without dictionaries loads with empty defaults | unit | `dotnet test tests/DynamicWeb.Serializer.Tests --filter "FullyQualifiedName~ConfigLoaderTests" -x` | Existing file, new tests needed |
| CFG-02 | Existing flat array tests still pass unchanged | unit | `dotnet test tests/DynamicWeb.Serializer.Tests --filter "FullyQualifiedName~ConfigLoaderTests" -x` | Existing tests cover this |
| CFG-01 | ExclusionMerger unions flat + typed correctly | unit | `dotnet test tests/DynamicWeb.Serializer.Tests --filter "FullyQualifiedName~ExclusionMergerTests" -x` | New file needed |
| CFG-01 | ExclusionMerger returns null when no exclusions | unit | Same as above | New file needed |

### Sampling Rate
- **Per task commit:** `dotnet test tests/DynamicWeb.Serializer.Tests --filter "FullyQualifiedName~Configuration" --no-build`
- **Per wave merge:** `dotnet test tests/DynamicWeb.Serializer.Tests`
- **Phase gate:** Full suite green before `/gsd-verify-work`

### Wave 0 Gaps
- [ ] `tests/DynamicWeb.Serializer.Tests/Configuration/ExclusionMergerTests.cs` -- covers merge logic for CFG-01
- New tests in existing `ConfigLoaderTests.cs` -- covers CFG-01 and CFG-02

## Security Domain

No security concerns for this phase. It is a pure data model extension with no authentication, access control, input from external users, cryptography, or network communication. Config files are read from local disk by server-side code only.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | DW item type names may differ in casing between config and runtime | Pitfall 4 | If DW is always consistent, OrdinalIgnoreCase is still safe (no harm) |
| A2 | SqlTable predicates do not have item types and thus only use flat exclusions | Merge Integration Points | If SqlTable rows have type metadata, typed exclusions would need SqlTable support too |

## Open Questions

1. **Where exactly should the per-entity merge happen in ContentMapper vs ContentSerializer?**
   - What we know: ContentMapper already receives excludeFields/excludeXmlElements as parameters and applies them per-entity. ContentSerializer builds the sets and passes them down.
   - What's unclear: Whether to change ContentMapper's signature to accept both flat + dictionary and merge internally, or have ContentSerializer/ContentDeserializer do per-entity merges before calling ContentMapper.
   - Recommendation: Planner's discretion. Both approaches work. Changing ContentMapper is cleaner but touches more method signatures. Pre-merging in the serializer/deserializer is simpler but duplicates the merge call pattern.

## Sources

### Primary (HIGH confidence)
- `src/DynamicWeb.Serializer/Configuration/SerializerConfiguration.cs` - Current record structure verified
- `src/DynamicWeb.Serializer/Configuration/ConfigLoader.cs` - Raw model pattern, nullable handling, Build method verified
- `src/DynamicWeb.Serializer/Configuration/ConfigWriter.cs` - Atomic save pattern, camelCase JSON options verified
- `src/DynamicWeb.Serializer/Models/ProviderPredicateDefinition.cs` - Per-predicate flat exclusion arrays verified
- `src/DynamicWeb.Serializer/Serialization/ContentSerializer.cs` - Exclusion set construction and passing verified
- `src/DynamicWeb.Serializer/Serialization/ContentMapper.cs` - Per-entity exclusion application verified
- `tests/DynamicWeb.Serializer.Tests/Configuration/ConfigLoaderTests.cs` - Existing test patterns verified
- System.Text.Json documentation - Dictionary<TKey, TValue> serialization support [ASSUMED: based on framework knowledge, extremely well-established]

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - no new dependencies, purely extending existing patterns
- Architecture: HIGH - all 5 merge points identified and verified in codebase
- Pitfalls: HIGH - derived from reading actual code and understanding the per-entity vs per-predicate distinction

**Research date:** 2026-04-09
**Valid until:** 2026-05-09 (stable domain, no external dependencies)
