# Phase 20: Link Resolution Core - Research

**Researched:** 2026-04-02
**Domain:** Internal page link rewriting during content deserialization
**Confidence:** HIGH

## Summary

Phase 20 implements the core link resolution engine: detecting `Default.aspx?ID=NNN` patterns in ItemType field values during deserialization and rewriting source page IDs to target page IDs. Phase 19 (complete) already serializes `SourcePageId` on pages and `SourceParagraphId` on paragraphs into YAML, providing the source-side of the ID mapping bridge. This phase builds the target-side map from `WriteContext.PageGuidCache` and performs the actual string replacement.

The architecture is a two-phase deserialization: Phase 1 (existing code) deserializes all pages/paragraphs and builds `PageGuidCache` (GUID -> targetId). Phase 2 (new) constructs sourceId -> targetId mapping from YAML's SourcePageId + PageGuidCache, then re-scans all saved Item field values for internal links and rewrites them. DynamicWeb's `LinkHelper.GetInternalPageIdsFromText` API handles link detection; we only need boundary-aware string replacement for the actual rewriting.

The critical implementation detail is boundary-aware replacement: naive `string.Replace("ID=1", "ID=999")` corrupts `ID=12` and `ID=100`. The solution uses `Regex.Replace` with a pattern that matches `Default.aspx?ID=NNN` followed by a non-digit boundary (end-of-string, `&`, `#`, `"`, `<`, space, etc.). This phase explicitly excludes paragraph anchor resolution (`#PPP` fragments), which is Phase 21 (LINK-05).

**Primary recommendation:** Create a stateless `InternalLinkResolver` helper class (following PermissionMapper pattern), wire it into `DeserializePredicate` as a second pass after all pages are written, and apply to all string field values universally without field-type filtering.

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| LINK-01 | Detect `Default.aspx?ID=NNN` patterns in ItemType field values and rewrite page IDs during deserialization | LinkHelper.GetInternalPageIdsFromText for detection + boundary-aware Regex.Replace for rewriting; two-phase architecture ensures complete ID map |
| LINK-02 | Source-to-target page ID mapping built using PageUniqueId (GUID) as bridge | YAML SourcePageId (from Phase 19) + WriteContext.PageGuidCache = complete bridge; build `Dictionary<int,int>` after Phase 1 |
| LINK-03 | Link resolution handles all field types: structured link fields, button fields, and rich text HTML | Scan ALL string field values universally; LinkHelper.GetInternalPageIdsFromText works on any text containing Default.aspx?ID=NNN regardless of surrounding context |
| LINK-04 | Unresolvable links preserved as-is with warning logged | InternalLinkResolver.ResolveLinks returns original string unchanged when sourceId not in map; logs warning per unresolved ID |
</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Dynamicweb | 10.23.9 | LinkHelper.GetInternalPageIdsFromText for link detection | Already referenced; handles all DW link format edge cases |
| System.Text.RegularExpressions | built-in | Boundary-aware ID replacement | .NET built-in; needed for non-digit boundary assertion |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| YamlDotNet | 13.7.1 | YAML deserialization (reads SourcePageId) | Already in stack; no changes needed |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Regex.Replace for boundary-aware rewriting | string.Replace with descending-length sort | Descending sort is fragile (misses edge cases like ID=1&foo=bar); regex is cleaner |
| LinkHelper for detection | Custom regex `Default\.aspx\?ID=(\d+)` | LinkHelper handles case variations, URL encoding, edge cases we would miss |
| Universal field scanning | Field-type allowlist | Allowlist is fragile; users paste links into any text field; LinkHelper returns empty fast for non-link text |

**Installation:**
```bash
# No new packages needed
```

## Architecture Patterns

### Recommended Project Structure
```
src/DynamicWeb.Serializer/
  Serialization/
    InternalLinkResolver.cs   # NEW: stateless link resolver helper
    ContentDeserializer.cs    # MODIFIED: add Phase 2 link resolution pass
    ContentMapper.cs          # UNCHANGED (Phase 19 already serializes SourcePageId)
  Models/
    SerializedPage.cs         # UNCHANGED (SourcePageId already present)
    SerializedParagraph.cs    # UNCHANGED (SourceParagraphId already present)
tests/DynamicWeb.Serializer.Tests/
  Serialization/
    InternalLinkResolverTests.cs  # NEW: unit tests for resolver
```

### Pattern 1: InternalLinkResolver (Stateless Helper)
**What:** A focused helper class that takes a sourceId-to-targetId dictionary, scans strings for internal page links, and rewrites them. Follows the established PermissionMapper pattern.
**When to use:** Called once per deserialization run with the complete ID map, then applied to every field value.
**Example:**
```csharp
// Source: PermissionMapper pattern in this codebase
public class InternalLinkResolver
{
    private readonly Dictionary<int, int> _sourceToTargetPageIds;
    private readonly Action<string>? _log;
    private int _resolvedCount;
    private int _unresolvedCount;

    // Boundary-aware regex: matches Default.aspx?ID=NNN where NNN is
    // followed by a non-digit (& # " ' < > space) or end of string.
    // Case-insensitive to handle default.aspx?id=123 variants.
    private static readonly Regex InternalLinkPattern = new(
        @"(Default\.aspx\?ID=)(\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public InternalLinkResolver(
        Dictionary<int, int> sourceToTargetPageIds,
        Action<string>? log = null)
    {
        _sourceToTargetPageIds = sourceToTargetPageIds;
        _log = log;
    }

    public string ResolveLinks(string fieldValue)
    {
        if (string.IsNullOrEmpty(fieldValue))
            return fieldValue;

        return InternalLinkPattern.Replace(fieldValue, match =>
        {
            var sourceId = int.Parse(match.Groups[2].Value);
            if (_sourceToTargetPageIds.TryGetValue(sourceId, out var targetId))
            {
                _resolvedCount++;
                return match.Groups[1].Value + targetId.ToString();
            }
            else
            {
                _log?.Invoke($"  WARNING: Unresolvable page ID {sourceId} in link");
                _unresolvedCount++;
                return match.Value; // preserve original
            }
        });
    }

    public (int resolved, int unresolved) GetStats() =>
        (_resolvedCount, _unresolvedCount);
}
```

### Pattern 2: Two-Phase Deserialization in DeserializePredicate
**What:** After the existing page tree walk (Phase 1), add Phase 2 that builds the ID map and rewrites all field values.
**When to use:** At the end of `DeserializePredicate`, after all `DeserializePageSafe` calls.
**Example:**
```csharp
// In DeserializePredicate, after the foreach (page) loop:

// Phase 2: Link resolution
var sourceToTarget = BuildSourceToTargetMap(area, ctx);
if (sourceToTarget.Count > 0)
{
    var resolver = new InternalLinkResolver(sourceToTarget, _log);
    ResolveLinksInAllItems(predicate.AreaId, ctx, resolver);
    var (resolved, unresolved) = resolver.GetStats();
    Log($"Link resolution: {resolved} resolved, {unresolved} unresolvable");
}
```

### Pattern 3: Source-to-Target Map Construction
**What:** Build `Dictionary<int,int>` from YAML SourcePageIds + PageGuidCache.
**When to use:** After Phase 1 completes (all pages deserialized, PageGuidCache complete).
**Example:**
```csharp
private Dictionary<int, int> BuildSourceToTargetMap(
    SerializedArea area, WriteContext ctx)
{
    var map = new Dictionary<int, int>();
    CollectSourcePageIds(area.Pages, ctx, map);
    return map;
}

private void CollectSourcePageIds(
    List<SerializedPage> pages, WriteContext ctx,
    Dictionary<int, int> map)
{
    foreach (var page in pages)
    {
        if (page.SourcePageId.HasValue &&
            ctx.PageGuidCache.TryGetValue(page.PageUniqueId, out var targetId))
        {
            map[page.SourcePageId.Value] = targetId;
        }
        CollectSourcePageIds(page.Children, ctx, map);
    }
}
```

### Pattern 4: Re-read and Rewrite Item Fields
**What:** For Phase 2, re-load each item by type+id, scan string fields, and re-save if any changed.
**When to use:** During the link resolution pass.
**Example:**
```csharp
private void ResolveLinksInItemFields(
    string? itemType, string itemId,
    InternalLinkResolver resolver)
{
    if (string.IsNullOrEmpty(itemType)) return;

    var item = Services.Items.GetItem(itemType, itemId);
    if (item == null) return;

    var fields = new Dictionary<string, object?>();
    item.SerializeTo(fields);

    bool anyChanged = false;
    foreach (var kvp in fields)
    {
        if (kvp.Value is string strValue && strValue.Length > 0)
        {
            var resolved = resolver.ResolveLinks(strValue);
            if (resolved != strValue)
            {
                fields[kvp.Key] = resolved;
                anyChanged = true;
            }
        }
    }

    if (anyChanged)
    {
        item.DeserializeFrom(fields);
        item.Save();
    }
}
```

### Anti-Patterns to Avoid
- **Naive string.Replace without boundaries:** `"Default.aspx?ID=1".Replace("ID=1", "ID=999")` corrupts `ID=12`. Use regex with captured groups so the integer is matched as a complete number.
- **Single-pass inline resolution:** Resolving during SaveItemFields fails on forward references (Page A links to Page B not yet deserialized). Two-phase is mandatory.
- **Field-type allowlist:** Only scanning "Link" or "Button" fields misses links pasted into rich text or arbitrary text fields. Scan everything.
- **Modifying YAML files:** YAML represents the source environment. Resolution happens only during deserialization; YAML stays pristine.
- **Custom regex for link detection:** Use `LinkHelper.GetInternalPageIdsFromText` -- it handles URL encoding, case sensitivity, and query parameter variations.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Internal link detection | Custom regex to find `Default.aspx?ID=NNN` | `LinkHelper.GetInternalPageIdsFromText(string)` | DW API handles case-insensitive matching, URL encoding, query parameters |
| Single URL page ID extraction | Custom URL parser | `LinkHelper.GetInternalPageId(string)` | Handles edge cases like `?ID=123&GroupID=G1` |
| Product/group link detection | Heuristic string matching | `LinkHelper.IsLinkInternalProductOrGroup(string)` | Official API distinguishes page links from product/group links |
| GUID-to-target-ID resolution | Custom DB query | `WriteContext.PageGuidCache` | Already built during Phase 1 deserialization |

**Key insight:** LinkHelper provides all detection capabilities. We only need to build the ID map and do the string replacement. The detection problem is already solved.

## Common Pitfalls

### Pitfall 1: ID Collision in String Replacement (CRITICAL)
**What goes wrong:** `string.Replace("Default.aspx?ID=1", "Default.aspx?ID=999")` also corrupts `Default.aspx?ID=12`, `Default.aspx?ID=100`, etc.
**Why it happens:** Page ID `1` is a substring of `12`, `100`, `1234`.
**How to avoid:** Use `Regex.Replace` with the pattern `(Default\.aspx\?ID=)(\d+)` and a `MatchEvaluator` callback that parses the captured digit group as an integer. The regex engine's greedy `\d+` naturally captures the full number, so `ID=12` matches as `12`, not as `1` followed by `2`.
**Warning signs:** Unit test with IDs 1, 12, and 123 all in the same field value.

### Pitfall 2: Forward References (CRITICAL)
**What goes wrong:** Page A at sort=1 links to Page C at sort=3, but C hasn't been deserialized when A's fields are first saved.
**Why it happens:** Depth-first tree walk. Cross-branch references are common.
**How to avoid:** Two-phase architecture. Phase 1 deserializes everything (existing code). Phase 2 resolves all links (new code). Never resolve inline.
**Warning signs:** Links logged as "unresolvable" that exist in the same area.

### Pitfall 3: Missing SourcePageId in Pre-v0.3.1 YAML
**What goes wrong:** YAML files serialized before Phase 19 lack `SourcePageId`. No map entries can be built.
**Why it happens:** Backward compatibility -- old YAML uses `int? SourcePageId` which defaults to null.
**How to avoid:** Gracefully skip link resolution when map is empty. Log that re-serialization is needed. SourcePageId is `int?` (nullable) so absence is natural.
**Warning signs:** Zero links resolved despite known link content.

### Pitfall 4: Case Sensitivity in Replacement
**What goes wrong:** DW may store links as `default.aspx?id=123` (lowercase) or `Default.aspx?ID=123`.
**Why it happens:** URL case is not standardized across all content editors.
**How to avoid:** Use `RegexOptions.IgnoreCase` on the replacement pattern.
**Warning signs:** Links with non-standard casing left unrewritten.

### Pitfall 5: Double-Save Performance
**What goes wrong:** Phase 2 re-reads and re-saves every item that was already saved in Phase 1.
**Why it happens:** Two-phase architecture requires re-reading to scan for links.
**How to avoid:** Only re-save items where links were actually found and changed. Check `anyChanged` flag before calling `item.Save()`. For MVP, accept the O(n) re-read -- correctness over performance.
**Warning signs:** Slow deserialization on large sites (thousands of pages).

### Pitfall 6: Dry-Run Mode Bypass
**What goes wrong:** Phase 2 writes resolved links even when `_isDryRun` is true.
**Why it happens:** New code path not gated by dry-run check.
**How to avoid:** In Phase 2, respect `_isDryRun`: log what would be resolved but do not call `item.Save()`.
**Warning signs:** Dry-run mode modifying data.

## Code Examples

Verified patterns from the codebase:

### Building Source-to-Target Map from YAML + PageGuidCache
```csharp
// WriteContext already has: PageGuidCache { GUID -> targetId }
// SerializedPage already has: SourcePageId (nullable int from YAML)
// SerializedPage already has: PageUniqueId (GUID)

// Combine: SourcePageId -> GUID -> targetId
var sourceToTarget = new Dictionary<int, int>();
foreach (var page in allPagesFlattened)
{
    if (page.SourcePageId.HasValue &&
        ctx.PageGuidCache.TryGetValue(page.PageUniqueId, out var targetId))
    {
        sourceToTarget[page.SourcePageId.Value] = targetId;
    }
}
// sourceToTarget now maps: sourceEnvId -> targetEnvId
```

### Boundary-Aware Regex Replacement
```csharp
// Pattern: captures "Default.aspx?ID=" prefix and the full integer
// \d+ is greedy, so ID=12 matches as "12" not "1" then "2"
// IgnoreCase handles default.aspx?id= variants
private static readonly Regex InternalLinkPattern = new(
    @"(Default\.aspx\?ID=)(\d+)",
    RegexOptions.IgnoreCase | RegexOptions.Compiled);

public string ResolveLinks(string fieldValue)
{
    return InternalLinkPattern.Replace(fieldValue, match =>
    {
        var sourceId = int.Parse(match.Groups[2].Value);
        if (_map.TryGetValue(sourceId, out var targetId))
            return match.Groups[1].Value + targetId;
        return match.Value; // preserve unresolvable
    });
}
```

### Existing SaveItemFields Pattern (line 727-758 of ContentDeserializer.cs)
```csharp
// Current code loads item, builds field dict, calls DeserializeFrom + Save
var itemEntry = Services.Items.GetItem(itemType, itemId);
var contentFields = fields
    .Where(kvp => !ItemSystemFields.Contains(kvp.Key))
    .ToDictionary(kvp => kvp.Key, kvp => (object?)kvp.Value);
itemEntry.DeserializeFrom(contentFields);
itemEntry.Save();
```

### Re-read Pattern for Phase 2 (follows SerializeTo pattern from ContentMapper)
```csharp
// ContentMapper.ExtractItemFields uses item.Names + item[fieldName]
// Phase 2 uses the reverse: SerializeTo gets all fields as dictionary
var fields = new Dictionary<string, object?>();
item.SerializeTo(fields);
// Now scan string values for links
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| No link resolution (IDs break cross-env) | Two-phase resolution with GUID bridge | Phase 20 (this phase) | Links survive environment sync |
| Custom regex for link detection | DW LinkHelper API | Always available in DW 10.x | Handles edge cases automatically |
| string.Replace for ID rewriting | Regex.Replace with boundary-aware pattern | Best practice | Prevents ID collision corruption |

**Deprecated/outdated:**
- Nothing deprecated; this is greenfield functionality

## Open Questions

1. **LinkHelper availability at deserialization time**
   - What we know: LinkHelper is a static utility class in `Dynamicweb.Environment.Helpers`. It should be available in any DW context.
   - What's unclear: Whether `GetInternalPageIdsFromText` requires DW's application context to be initialized (unlikely for a pure string parser, but unverified).
   - Recommendation: Use it; if it fails at runtime, fall back to the same regex pattern (`Default\.aspx\?ID=(\d+)`) which is the pattern LinkHelper itself uses internally.

2. **ButtonEditor serialized format**
   - What we know: ButtonEditor stores its value as a string. The exact internal format may wrap URLs.
   - What's unclear: Whether `GetInternalPageIdsFromText` can parse through ButtonEditor's serialized format, or if we need to unwrap it first.
   - Recommendation: Test with actual ButtonEditor field values at integration test time. The universal scan approach means even if the format wraps the URL, `GetInternalPageIdsFromText` or regex will find `Default.aspx?ID=NNN` if it appears as a literal substring.

3. **PropertyItem fields (Icon, SubmenuType)**
   - What we know: PropertyItem fields are saved separately via `SavePropertyItemFields`. They could theoretically contain link values.
   - What's unclear: Whether any standard PropertyItem field types store page links.
   - Recommendation: Apply link resolution to PropertyItem fields too. The overhead is minimal and prevents missed links.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit 2.9.3 + Moq 4.20.72 |
| Config file | `tests/DynamicWeb.Serializer.Tests/DynamicWeb.Serializer.Tests.csproj` |
| Quick run command | `dotnet test tests/DynamicWeb.Serializer.Tests --filter "FullyQualifiedName~InternalLinkResolver" --no-build` |
| Full suite command | `dotnet test tests/DynamicWeb.Serializer.Tests` |

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| LINK-01 | Default.aspx?ID=NNN rewritten to target ID | unit | `dotnet test tests/DynamicWeb.Serializer.Tests --filter "FullyQualifiedName~InternalLinkResolver" -x` | Wave 0 |
| LINK-02 | Source-to-target map built from SourcePageId + PageGuidCache | unit | `dotnet test tests/DynamicWeb.Serializer.Tests --filter "FullyQualifiedName~InternalLinkResolver" -x` | Wave 0 |
| LINK-03 | All field types handled (link, button, rich text) | unit | `dotnet test tests/DynamicWeb.Serializer.Tests --filter "FullyQualifiedName~InternalLinkResolver" -x` | Wave 0 |
| LINK-04 | Unresolvable links preserved + warning logged | unit | `dotnet test tests/DynamicWeb.Serializer.Tests --filter "FullyQualifiedName~InternalLinkResolver" -x` | Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test tests/DynamicWeb.Serializer.Tests --filter "FullyQualifiedName~InternalLinkResolver" --no-build`
- **Per wave merge:** `dotnet test tests/DynamicWeb.Serializer.Tests`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `tests/DynamicWeb.Serializer.Tests/Serialization/InternalLinkResolverTests.cs` -- covers LINK-01 through LINK-04
- Framework install: Already present (xunit 2.9.3)

### Key Test Cases for InternalLinkResolverTests
1. **Simple link rewrite:** `Default.aspx?ID=123` -> `Default.aspx?ID=456`
2. **Multiple links in one field:** Two different IDs in same string
3. **Rich text HTML:** `<a href="Default.aspx?ID=123">text</a>` rewritten correctly
4. **Boundary safety:** Field contains both `ID=1` and `ID=12`; only correct ones rewrite
5. **Unresolvable link:** Source ID not in map; returns original, logs warning
6. **No links in value:** Plain text field returns unchanged (fast path)
7. **Case insensitivity:** `default.aspx?id=123` is also detected and rewritten
8. **Query parameter preservation:** `Default.aspx?ID=123&GroupID=G1` -- only page ID rewritten
9. **Empty/null input:** Returns input unchanged without error
10. **Paragraph anchor preservation (no rewrite):** `Default.aspx?ID=123#456` -- page ID rewritten, fragment preserved as-is (fragment resolution is Phase 21)

## Sources

### Primary (HIGH confidence)
- [LinkHelper API](https://doc.dynamicweb.dev/api/Dynamicweb.Environment.Helpers.LinkHelper.html) - GetInternalPageIdsFromText signature, GetInternalPageId, IsLinkInternal
- Direct codebase analysis of ContentDeserializer.cs (940 lines), ContentMapper.cs, PermissionMapper.cs, ReferenceResolver.cs
- SerializedPage.cs and SerializedParagraph.cs -- confirmed SourcePageId/SourceParagraphId present from Phase 19

### Secondary (MEDIUM confidence)
- .planning/research/ARCHITECTURE.md - Two-phase architecture design
- .planning/research/PITFALLS.md - ID collision and forward reference pitfalls
- .planning/research/FEATURES.md - Link format catalog (5 formats documented)
- .planning/research/STACK.md - LinkHelper API coverage

### Tertiary (LOW confidence)
- ButtonEditor serialized format -- needs runtime validation with actual DB values

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - No new deps; LinkHelper API confirmed via official docs
- Architecture: HIGH - Two-phase pattern validated by ARCHITECTURE.md research and established codebase patterns (PermissionMapper)
- Pitfalls: HIGH - ID collision and forward reference well-documented; boundary-aware regex is standard technique
- Testing: HIGH - InternalLinkResolver is pure logic, fully unit-testable without DW runtime

**Research date:** 2026-04-02
**Valid until:** 2026-05-02 (stable domain -- DW link format is not changing)
