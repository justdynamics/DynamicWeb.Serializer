# Phase 39: Seed mode field-level merge — Pattern Map

**Mapped:** 2026-04-22
**Files analyzed:** 11 (4 modified sources + 2 new helpers + 5 new test files + 1 pipeline edit)
**Analogs found:** 11 / 11 — every new file has a proven in-repo analog

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `src/DynamicWeb.Serializer/Serialization/ContentDeserializer.cs` (modify ~684–692, ~1169, ~1193–1268) | deserializer | request-response (DW API writes) | Itself — source-wins UPDATE path at lines 700–733 is the direct template for merge-gated calls | exact (self-analog) |
| `src/DynamicWeb.Serializer/Providers/SqlTable/SqlTableProvider.cs` (modify ~313–322, extend ~292–297) | provider | CRUD (SQL read+write) | Itself — existing `existingChecksums` loop at 292–297 and checksum fast-path at 304–311 are the exact extension point | exact (self-analog) |
| `src/DynamicWeb.Serializer/Providers/SqlTable/SqlTableWriter.cs` (add `UpdateColumnSubset`) | writer | CRUD (parameterized SQL) | `SqlTableWriter.BuildMergeCommand` (lines 35–123) — same class, same `CommandBuilder` + `{0}`-placeholder idiom | exact |
| `src/DynamicWeb.Serializer/Infrastructure/MergePredicate.cs` (NEW) | utility (pure static) | transform | `src/DynamicWeb.Serializer/Configuration/ExclusionMerger.cs` (static class, pure functions, `null`-returning-is-meaningful optimization) | exact |
| `src/DynamicWeb.Serializer/Infrastructure/XmlMergeHelper.cs` (NEW, planner may rename per D-27) | utility (pure static) | transform | `src/DynamicWeb.Serializer/Infrastructure/XmlFormatter.cs` — static class, `XDocument.Parse` + swallow-`XmlException` defensive pattern, already has a sibling element-level merge via `CompactWithMerge` | exact |
| `tools/e2e/full-clean-roundtrip.ps1` (extend steps 15.1/15.2/15.3) | infra (pipeline) | request-response (HTTP + sqlcmd) | Itself — steps 14/15 at lines 527–545 are the exact Deploy/Seed HTTP call shape; `Invoke-Sqlcmd-Scalar` at line 193 is the assertion shape | exact (self-analog) |
| `tests/DynamicWeb.Serializer.Tests/Infrastructure/MergePredicateTests.cs` (NEW) | unit test | in-process | `tests/DynamicWeb.Serializer.Tests/Configuration/ExclusionMergerTests.cs` — pure-static-utility test: plain `[Fact]` / `[Theory]` with `InlineData`, no fixtures, no Moq | exact |
| `tests/DynamicWeb.Serializer.Tests/Serialization/ContentDeserializerSeedMergeTests.cs` (NEW) | integration test | in-process (DW service) | `tests/DynamicWeb.Serializer.Tests/Serialization/ContentDeserializerAreaSchemaTests.cs` + `Fixtures/ContentTreeBuilder.cs` | role-match (no DestinationWins tests exist yet) |
| `tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/SqlTableWriterUpdateSubsetTests.cs` (NEW) | unit test | in-process (Moq ISqlExecutor) | `tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/SqlTableWriterTests.cs` — identical Moq+CommandBuilder shape | exact |
| `tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/SqlTableProviderSeedMergeTests.cs` (NEW) | integration test | in-process (Moq stack) | `tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/SqlTableProviderDeserializeTests.cs` — full provider harness with `CreateProviderWithFiles` helper | exact |
| `tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/XmlMergeHelperTests.cs` + `EcomXmlMergeTests.cs` (NEW) | unit / integration test | in-process | `tests/DynamicWeb.Serializer.Tests/Infrastructure/XmlFormatterTests.cs` — pure static helper tests, no mocks | exact |

---

## Pattern Assignments

### `src/DynamicWeb.Serializer/Infrastructure/MergePredicate.cs` (NEW, utility, transform)

**Analog:** `src/DynamicWeb.Serializer/Configuration/ExclusionMerger.cs`

**File header + namespace pattern** (ExclusionMerger.cs lines 1–8):
```csharp
namespace DynamicWeb.Serializer.Configuration;

/// <summary>
/// Merges per-predicate flat exclusion lists with type-specific dictionary entries.
/// Used at each entity (page/paragraph/area) during serialize/deserialize to build
/// the effective exclusion set for that entity's specific item type or XML type.
/// </summary>
public static class ExclusionMerger
```

**Apply to MergePredicate:** `namespace DynamicWeb.Serializer.Infrastructure;` + `public static class MergePredicate` (per D-08 + research §Location recommendation — co-located with `TargetSchemaCache` in the `Infrastructure/` folder).

**Pure-static method shape** (ExclusionMerger.cs lines 14–35 — mirror this for each `IsUnsetForMerge` overload):
```csharp
public static HashSet<string>? MergeFieldExclusions(
    IReadOnlyList<string> predicateExclusions,
    IReadOnlyDictionary<string, List<string>> typedExclusions,
    string? itemTypeName)
{
    var hasFlat = predicateExclusions.Count > 0;
    List<string>? typeList = null;
    var hasTyped = !string.IsNullOrEmpty(itemTypeName)
        && TryGetValueIgnoreCase(typedExclusions, itemTypeName!, out typeList)
        && typeList!.Count > 0;

    if (!hasFlat && !hasTyped)
        return null;  // <-- "null-means-no-action" optimization — MergePredicate should adopt the inverse: a single `false` short-circuit for "definitely set"
    ...
}
```

**Case-insensitive lookup helper** (ExclusionMerger.cs lines 69–93) — pattern for `IsUnsetForMergeBySqlType` where SQL `DATA_TYPE` strings like `"nvarchar"`/`"NVARCHAR"` need case-insensitive dispatch. Reuse `StringComparison.OrdinalIgnoreCase` identically.

---

### `src/DynamicWeb.Serializer/Infrastructure/XmlMergeHelper.cs` (NEW, utility, transform — D-21..D-25, D-27)

**Analog:** `src/DynamicWeb.Serializer/Infrastructure/XmlFormatter.cs`

**Static class + defensive-parse pattern** (XmlFormatter.cs lines 12–45):
```csharp
using System.Xml;
using System.Xml.Linq;

namespace DynamicWeb.Serializer.Infrastructure;

public static class XmlFormatter
{
    public static string? PrettyPrint(string? rawXml)
    {
        if (string.IsNullOrWhiteSpace(rawXml))
            return rawXml;
        try
        {
            var xdoc = XDocument.Parse(rawXml);
            ...
            return result.Replace("\r\n", "\n").Replace("\r", "\n");
        }
        catch (XmlException)
        {
            return rawXml;  // malformed XML passes through unchanged
        }
    }
}
```

**Element-level merge reference** — XmlFormatter.cs lines 97–143 (`CompactWithMerge`) already implements a "keep target elements not in source" merge. `XmlMergeHelper` for Phase 39 is the **inverse** rule (D-22/D-24): keep *source* elements only when target element is absent/empty, never strip. The parse/serialize scaffolding is identical — only the per-element predicate flips:

```csharp
// XmlFormatter.cs:116-127 — adopt the identity-key pattern (elementName || name-attribute)
var incomingKeys = new HashSet<string>(
    incomingDoc.Root.Elements().Select(e =>
        e.Attribute("name")?.Value ?? e.Name.LocalName),
    StringComparer.OrdinalIgnoreCase);

foreach (var el in existingDoc.Root.Elements())
{
    var key = el.Attribute("name")?.Value ?? el.Name.LocalName;
    if (!incomingKeys.Contains(key))
        incomingDoc.Root.Add(new XElement(el));  // <-- Phase 39 XmlMergeHelper inverts this: keep source only when target is unset per D-22
}
```

**Key insight for planner:** `CompactWithMerge` already handles the `<Parameter name="X">` DW idiom (EcomPayments uses this). Copy that `name`-attribute-as-identity rule into `XmlMergeHelper` verbatim — it matches `PaymentGatewayParameters` / `ShippingServiceParameters` element shape.

**Location:** `Infrastructure/` (D-27 planner discretion — this matches XmlFormatter's neighborhood and TargetSchemaCache convention).

---

### `src/DynamicWeb.Serializer/Providers/SqlTable/SqlTableWriter.cs` — new `UpdateColumnSubset` method

**Analog:** `SqlTableWriter.BuildMergeCommand` (same file, lines 35–123) + `ApplyLinkResolution` signature shape

**Parameterized `CommandBuilder` + `{0}` placeholder pattern** (SqlTableWriter.cs lines 60–95):
```csharp
var cb = new CommandBuilder();

cb.Add($"MERGE [{metadata.TableName}] AS target");
cb.Add("USING (SELECT ");

var count = 0;
foreach (var column in itemColumns)
{
    if (count > 0) cb.Add(",");
    var value = row.TryGetValue(column, out var v) ? v ?? DBNull.Value : DBNull.Value;
    cb.Add("{0}", value);          // <-- SQL-injection-safe parameterization
    count++;
}

cb.Add(") AS source (");
cb.Add(string.Join(",", itemColumns.Select(col => $"[{col}]")));
cb.Add(")");

cb.Add("ON (");
cb.Add(string.Join(" AND ", keyColumns.Select(col => $"target.[{col}] = source.[{col}]")));
cb.Add(")");
```

**Apply to `UpdateColumnSubset`:** Same `CommandBuilder` idiom. `[col]`-bracketed identifiers. `cb.Add("{0}", value)` for every user-supplied value. Identity columns excluded via `!metadata.IdentityColumns.Contains(col, StringComparer.OrdinalIgnoreCase)` — the exact filter pattern at line 51–53.

**`virtual` + `WriteOutcome` return** (lines 129–164, `WriteRow`):
```csharp
public virtual WriteOutcome WriteRow(Dictionary<string, object?> row, TableMetadata metadata,
    bool isDryRun, Action<string>? log = null, HashSet<string>? notNullColumns = null)
{
    try
    {
        ...
        if (isDryRun) return exists ? WriteOutcome.Updated : WriteOutcome.Created;
        var cb = BuildMergeCommand(row, metadata, notNullColumns);
        _sqlExecutor.ExecuteNonQuery(cb);
        return exists ? WriteOutcome.Updated : WriteOutcome.Created;
    }
    catch (Exception ex)
    {
        log?.Invoke($"    ERROR [{metadata.TableName}]: {ex.Message}");
        return WriteOutcome.Failed;
    }
}
```

**Apply to `UpdateColumnSubset`:** Declare `public virtual WriteOutcome UpdateColumnSubset(...)` — `virtual` is required for the `Mock<SqlTableWriter>` pattern already in tests (see SqlTableProviderDeserializeTests.cs:318 `new Mock<SqlTableWriter>(...) { CallBase = false }`). Return `WriteOutcome.Updated` on success, `Failed` on exception, propagate `log` through the same `log?.Invoke` lambda.

**Dry-run short-circuit** (lines 136–140): log, return the would-be outcome, never call `_sqlExecutor.ExecuteNonQuery`. D-19 extension is a per-column `"  would fill [col=X]: target=<unset> → seed='<v>'"` inside this branch.

---

### `src/DynamicWeb.Serializer/Providers/SqlTable/SqlTableProvider.cs` — replace lines 313–322 + extend 292–297

**Analog:** Same file, lines 289–322 (itself)

**Existing row-enumeration pattern (PRESERVE + EXTEND at ~292–297):**
```csharp
var existingChecksums = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
foreach (var existingRow in _tableReader.ReadAllRows(metadata.TableName))
{
    var identity = _tableReader.GenerateRowIdentity(existingRow, metadata);
    var checksum = _tableReader.CalculateChecksum(existingRow, metadata);
    existingChecksums[identity] = checksum;
}
```

**Extend pattern (per research §Reuse Opportunity, zero extra round-trips):**
```csharp
var existingChecksums = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
var existingRowsByIdentity = new Dictionary<string, Dictionary<string, object?>>(StringComparer.OrdinalIgnoreCase);
foreach (var existingRow in _tableReader.ReadAllRows(metadata.TableName))
{
    var identity = _tableReader.GenerateRowIdentity(existingRow, metadata);
    var checksum = _tableReader.CalculateChecksum(existingRow, metadata);
    existingChecksums[identity] = checksum;
    existingRowsByIdentity[identity] = existingRow;  // <-- NEW: full row dict cached for merge
}
```

**Existing checksum fast-path (PRESERVE per D-18 at ~304–311):**
```csharp
if (existingChecksums.TryGetValue(identity, out var existingChecksum)
    && string.Equals(incomingChecksum, existingChecksum, StringComparison.OrdinalIgnoreCase))
{
    skipped++;
    Log($"  Skipped {identity} (unchanged)", log);
    continue;
}
```

**Seed-skip block (REPLACE at ~313–322) — current code to delete:**
```csharp
// DELETE THIS — superseded by field-level merge branch
if (strategy == ConflictStrategy.DestinationWins
    && existingChecksums.ContainsKey(identity))
{
    skipped++;
    Log($"  Seed-skip: [{metadata.TableName}].{identity} (already present)", log);
    continue;
}
```

**Log/counter outcome dispatch pattern to copy** (SqlTableProvider.cs lines 324–340):
```csharp
var outcome = _writer.WriteRow(yamlRow, metadata, isDryRun, log, notNullColumns);
switch (outcome)
{
    case WriteOutcome.Created: created++; break;
    case WriteOutcome.Updated: updated++; break;
    case WriteOutcome.Failed:
        failed++;
        errors.Add($"Failed to write row: {identity}");
        break;
}
Log($"  {outcome} {identity}", log);
```

The merge branch substitutes `_writer.UpdateColumnSubset(...)` for `_writer.WriteRow(...)` and emits the D-11 `"Seed-merge: [identity] — N filled, M left"` line.

---

### `src/DynamicWeb.Serializer/Serialization/ContentDeserializer.cs` — replace lines 684–692 + extend 1169 + 1193–1268

**Analog:** Same file (source-wins UPDATE path at 700–733 + `ApplyPageProperties` at 1193–1268 + `SaveItemFields` at 1153–1187)

**Seed-skip block (REPLACE at 684–692) — current code to delete:**
```csharp
// DELETE THIS — superseded by field-level merge branch
if (_conflictStrategy == ConflictStrategy.DestinationWins)
{
    Log($"Seed-skip: page {dto.PageUniqueId} (already present, ID={existingId})");
    ctx.Skipped++;
    return existingId;
}
```

**Source-wins UPDATE path (lines 700–733) — the scalar assignment template the merge branch wraps with `IsUnsetForMerge` gates:**
```csharp
existingPage.UniqueId = dto.PageUniqueId;            // identity — always source-wins (D-05)
existingPage.AreaId = ctx.TargetAreaId;              // identity — always source-wins (D-05)
existingPage.ParentPageId = ctx.ParentPageId;        // identity — always source-wins (D-05)
existingPage.MenuText = dto.MenuText;                // <-- wrap: if (IsUnsetForMerge(existingPage.MenuText)) existingPage.MenuText = dto.MenuText;
existingPage.UrlName = dto.UrlName;                  // <-- wrap
existingPage.Active = dto.IsActive;                  // <-- wrap (D-10 tradeoff: false == unset)
existingPage.Sort = dto.SortOrder;                   // <-- wrap (D-10: 0 == unset)
existingPage.ItemType = dto.ItemType ?? string.Empty;// <-- wrap
existingPage.LayoutTemplate = dto.Layout ?? string.Empty;
existingPage.LayoutApplyToSubPages = dto.LayoutApplyToSubPages;
existingPage.IsFolder = dto.IsFolder;
existingPage.TreeSection = dto.TreeSection ?? string.Empty;
ApplyPageProperties(existingPage, dto);              // <-- replace with ApplyPagePropertiesWithMerge per Pitfall 2

Services.Pages.SavePage(existingPage);

var updatePageExclude = ctx.ExcludeFieldsByItemType != null
    ? ExclusionMerger.MergeFieldExclusions(
        ctx.ExcludeFields?.ToList() ?? new List<string>(),
        ctx.ExcludeFieldsByItemType,
        dto.ItemType)
    : ctx.ExcludeFields;
SaveItemFields(existingPage.ItemType, existingPage.ItemId, dto.Fields, updatePageExclude);  // <-- replace with MergeItemFields per Pitfall 3
SavePropertyItemFields(existingPage, dto.PropertyFields, updatePageExclude);                // <-- replace with MergePropertyItemFields

ctx.Updated++;
Log($"UPDATED page {dto.PageUniqueId} (ID={existingId})");
_permissionMapper.ApplyPermissions(existingId, dto.Permissions);  // <-- D-06: OMIT in merge branch
return existingId;
```

**Source-wins `SaveItemFields` (lines 1153–1187) — SIBLING `MergeItemFields` to add (D-02 / Pitfall 3):**
```csharp
private void SaveItemFields(string? itemType, string itemId, Dictionary<string, object> fields, IReadOnlySet<string>? excludeFields = null)
{
    if (string.IsNullOrEmpty(itemType)) return;

    var itemEntry = Services.Items.GetItem(itemType, itemId);
    if (itemEntry == null)
    {
        Log($"WARNING: Could not load ItemEntry for type={itemType}, id={itemId}");
        return;
    }

    var contentFields = fields
        .Where(kvp => !ItemSystemFields.Contains(kvp.Key))
        .ToDictionary(kvp => kvp.Key, kvp => (object?)kvp.Value);

    // Source-wins: null out item fields not present in the serialized data.
    foreach (var fieldName in itemEntry.Names)
    {
        if (!ItemSystemFields.Contains(fieldName) && !contentFields.ContainsKey(fieldName))
        {
            if (excludeFields != null && excludeFields.Contains(fieldName))
                continue;
            contentFields[fieldName] = null;
        }
    }

    if (contentFields.Count == 0) return;
    itemEntry.DeserializeFrom(contentFields);
    itemEntry.Save();
}
```

**Apply to new `MergeItemFields`:** Same load+guard shell (`Services.Items.GetItem` + null guard + `ItemSystemFields` filter), but **do NOT** null out absent fields. Instead:
1. Read current target state via `itemEntry.SerializeTo(currentDict)` (pattern from research §Live-Read Mechanism, per ContentMapper.cs:45).
2. Build `filledFields` by testing `MergePredicate.IsUnsetForMerge(currentDict.GetValueOrDefault(k)?.ToString())` per D-02 string rule.
3. **Overlay** `filledFields` onto `currentDict` (Pitfall 7 defense) before `DeserializeFrom` — avoids sibling clearing.

**`ApplyPageProperties` (lines 1193–1268) — template for `ApplyPagePropertiesWithMerge` (D-04 / Pitfall 2):**
```csharp
private static void ApplyPageProperties(Page page, SerializedPage dto)
{
    page.NavigationTag = dto.NavigationTag;        // <-- gate each with IsUnsetForMerge(page.NavigationTag)
    page.ShortCut = dto.ShortCut;                  // <-- gate
    page.Hidden = dto.Hidden;                      // <-- gate (D-10)
    ...

    // SEO sub-object — D-04: per-property merge inside
    if (dto.Seo != null)
    {
        page.MetaTitle = dto.Seo.MetaTitle;        // <-- if (IsUnsetForMerge(page.MetaTitle)) page.MetaTitle = dto.Seo.MetaTitle;
        page.MetaCanonical = dto.Seo.MetaCanonical;// <-- gate
        ...
    }

    // NavigationSettings — Pitfall 5: if target null + YAML non-null → construct whole; else per-property
    if (dto.NavigationSettings != null && dto.NavigationSettings.UseEcomGroups)
    {
        page.NavigationSettings = new PageNavigationSettings { ... };
    }
}
```

**Apply to merge variant:** Mirror the exact method structure. Every `page.X = dto.X` becomes `if (MergePredicate.IsUnsetForMerge(page.X)) { page.X = dto.X; filled++; } else left++;`. Keep Pitfall 5's null-target special case: if `page.NavigationSettings == null && dto.NavigationSettings != null`, assign the whole sub-object as today.

**Dry-run diff template (lines 1428–1496, `LogDryRunPageUpdate`) — D-19 extension pattern:**
```csharp
var diffs = new List<string>();

if (dto.MenuText != existing.MenuText)
    diffs.Add($"MenuText: '{existing.MenuText}' -> '{dto.MenuText}'");
...
foreach (var kvp in dto.Fields)
{
    var currentVal = existing.Item?[kvp.Key]?.ToString();
    var newVal = kvp.Value?.ToString();
    if (currentVal != newVal)
        diffs.Add($"Fields[{kvp.Key}]: '{currentVal}' -> '{newVal}'");
}
```

**Apply to D-19 `LogSeedMergeDryRun`:** Same `diffs` list pattern, but gate each line with `MergePredicate.IsUnsetForMerge(existing.X)` — only emit a `"would fill [col=X]: target=<unset> → seed='<v>'"` when the merge would actually fire. Existing-no-change case emits nothing.

---

### `tools/e2e/full-clean-roundtrip.ps1` — D-15 extension (Deploy → tweak → Seed sub-pipeline)

**Analog:** Same file — existing Deploy/Seed HTTP calls at lines 527–545 + `Invoke-Sqlcmd-Scalar` at lines 193–205

**HTTP-call + log-grep assertion shape** (lines 527–545):
```powershell
$desDeploy = Invoke-DwApi -HostUrl $CleanDbHostUrl -Endpoint '/Admin/Api/SerializerDeserialize?mode=deploy' -LogPath (Join-Path $script:runDir 'deserialize-deploy.log')
if ($desDeploy.Code -ne 200) {
    throw "Deserialize Deploy: expected HTTP 200, got $($desDeploy.Code). See deserialize-deploy.log"
}
if (Select-String -Path (Join-Path $script:runDir 'deserialize-deploy.log') -Pattern 'escalated|CumulativeStrictModeException' -Quiet) {
    throw "Deserialize Deploy emitted strict-mode escalations. See deserialize-deploy.log"
}
Write-Host '  Deserialize Deploy HTTP 200 OK'

$desSeed = Invoke-DwApi -HostUrl $CleanDbHostUrl -Endpoint '/Admin/Api/SerializerDeserialize?mode=seed' -LogPath (Join-Path $script:runDir 'deserialize-seed.log')
if ($desSeed.Code -ne 200) {
    throw "Deserialize Seed: expected HTTP 200, got $($desSeed.Code). See deserialize-seed.log"
}
```

**Apply to D-15 steps 15.1/15.2/15.3:** Clone this exact `Invoke-DwApi` + HTTP-200-check + `Select-String` escalation-guard triplet for the second Seed pass. Assertions use the existing `Invoke-Sqlcmd-Scalar` helper.

**Scalar-assertion shape** (lines 193–205 + 565–574):
```powershell
function Invoke-Sqlcmd-Scalar {
    param([string]$Server, [string]$Database, [string]$Query)
    $raw = & sqlcmd -S $Server -E -d $Database -h -1 -W -Q "SET NOCOUNT ON; $Query" 2>&1
    $line = ($raw | Where-Object { $_ -match '^\s*-?\d+\s*$' } | Select-Object -First 1)
    if (-not $line) { throw "sqlcmd query returned no numeric scalar..." }
    return [int]$line.Trim()
}

# Usage:
$srcCount = Invoke-Sqlcmd-Scalar -Server $SqlServer -Database $SwiftDb -Query 'SELECT COUNT(*) FROM EcomProducts'
if ($srcCount -ne 2051) { throw "..." }
```

**Apply to D-15:** Add a sibling `Invoke-Sqlcmd-StringScalar` (per research §D-15) with the same `-h -1 -W -Q` flags but returning `$line.Trim()` without `[int]` cast. Assert `Mail1SenderEmail` presence via:
```powershell
$mailSender = Invoke-Sqlcmd-StringScalar -Server $SqlServer -Database $CleanDb -Query "SELECT PaymentGatewayParameters FROM EcomPayments WHERE PaymentID = N'<id>'"
if (-not ($mailSender -match 'Mail1SenderEmail')) { throw "Mail1SenderEmail not filled by Seed XML-merge" }
```

**Tweak-preservation step (15.3):** Between steps 14 and 15, inject a `& sqlcmd ... -Q "UPDATE Page SET MenuText='tweaked' WHERE ID=..."` then after 15 assert the value survived. Follow the existing `Invoke-Sqlcmd-File` shape at lines 177–191 if a full script is cleaner than inline `-Q`.

---

### `tests/DynamicWeb.Serializer.Tests/Infrastructure/MergePredicateTests.cs` (NEW)

**Analog:** `tests/DynamicWeb.Serializer.Tests/Configuration/ExclusionMergerTests.cs` + `tests/DynamicWeb.Serializer.Tests/Infrastructure/XmlFormatterTests.cs`

**Imports + namespace** (ExclusionMergerTests.cs lines 1–6):
```csharp
using DynamicWeb.Serializer.Configuration;
using Xunit;

namespace DynamicWeb.Serializer.Tests.Configuration;

public class ExclusionMergerTests
{
```

**Apply:** `using DynamicWeb.Serializer.Infrastructure;` + `namespace DynamicWeb.Serializer.Tests.Infrastructure;` + `public class MergePredicateTests`. No fixtures, no Moq — the helper is pure.

**`[Fact]` single-case shape** (ExclusionMergerTests.cs lines 12–21 / XmlFormatterTests.cs lines 12–15):
```csharp
[Fact]
public void MergeFieldExclusions_EmptyFlatAndEmptyDict_ReturnsNull()
{
    var flat = new List<string>();
    var dict = new Dictionary<string, List<string>>();

    var result = ExclusionMerger.MergeFieldExclusions(flat, dict, "Swift_PageItemType");

    Assert.Null(result);
}
```

**`[Theory] + [InlineData]` shape for type-matrix coverage** (PermissionDeserializationTests.cs lines 13–23):
```csharp
[Theory]
[InlineData("none", PermissionLevel.None)]
[InlineData("read", PermissionLevel.Read)]
...
public void ParseLevelName_ReturnsExpectedLevel(string name, PermissionLevel expected)
{
    Assert.Equal(expected, PermissionMapper.ParseLevelName(name));
}
```

**Apply to MergePredicateTests:** Use `[Theory] + [InlineData]` for the D-13 matrix (22 rows in research §Unit test surface). One `[Theory]` per method signature: `IsUnsetForMerge(object?, Type)`, `IsUnsetForMerge(string?)`, `IsUnsetForMerge(int)`, etc. `[Fact]` for singular `Guid.Empty` / `DateTime.MinValue` / `DBNull.Value` edge cases.

---

### `tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/SqlTableWriterUpdateSubsetTests.cs` (NEW)

**Analog:** `tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/SqlTableWriterTests.cs`

**Imports + Moq `ISqlExecutor` fixture** (SqlTableWriterTests.cs lines 1–10, 59–78):
```csharp
using System.Data;
using DynamicWeb.Serializer.Models;
using DynamicWeb.Serializer.Providers.SqlTable;
using Dynamicweb.Data;
using Moq;
using Xunit;

namespace DynamicWeb.Serializer.Tests.Providers.SqlTable;

[Trait("Category", "Phase13")]
public class SqlTableWriterTests
{
    private static TableMetadata CreateEcomOrderFlowMetadata() => new()
    {
        TableName = "EcomOrderFlow",
        NameColumn = "OrderFlowName",
        KeyColumns = new List<string> { "OrderFlowId" },
        IdentityColumns = new List<string> { "OrderFlowId" },
        AllColumns = new List<string> { "OrderFlowId", "OrderFlowName", "OrderFlowDescription" }
    };

    [Fact]
    public void BuildMergeCommand_GeneratesValidMerge()
    {
        var mockExecutor = new Mock<ISqlExecutor>();
        var writer = new SqlTableWriter(mockExecutor.Object);
        var metadata = CreateEcomOrderFlowMetadata();
        var row = CreateSampleRow();

        var cb = writer.BuildMergeCommand(row, metadata);
        var sql = cb.ToString();
        Assert.Contains("MERGE [EcomOrderFlow] AS target", sql);
        Assert.Contains("WHEN MATCHED THEN UPDATE SET", sql);
    }
}
```

**Apply to `SqlTableWriterUpdateSubsetTests`:** `[Trait("Category", "Phase39")]`. Reuse the exact `CreateEcomOrderFlowMetadata` + `CreateSampleRow` fixtures. For each test: instantiate `new Mock<ISqlExecutor>()` → `new SqlTableWriter(mockExecutor.Object)` → call `UpdateColumnSubset(...)` → assert via `cb.ToString()` text-matching (`Assert.Contains("UPDATE [...]", sql)`, `Assert.DoesNotContain("SET IDENTITY_INSERT", sql)` per research §Narrowed-UPDATE SQL Shape).

**Dry-run `ExecuteNonQuery` absence check** (SqlTableWriterTests.cs lines 99–115):
```csharp
[Fact]
public void WriteRow_DryRun_DoesNotCallExecuteNonQuery()
{
    var mockExecutor = new Mock<ISqlExecutor>();
    mockExecutor.Setup(x => x.ExecuteReader(It.IsAny<CommandBuilder>()))
        .Returns(CreateEmptyReader().Object);
    var writer = new SqlTableWriter(mockExecutor.Object);
    var outcome = writer.WriteRow(row, metadata, isDryRun: true);
    Assert.Equal(WriteOutcome.Created, outcome);
    mockExecutor.Verify(x => x.ExecuteNonQuery(It.IsAny<CommandBuilder>()), Times.Never);
}
```

**Apply:** Same `Times.Never` verification for `UpdateColumnSubset` dry-run — critical for D-19 proof.

---

### `tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/SqlTableProviderSeedMergeTests.cs` (NEW)

**Analog:** `tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/SqlTableProviderDeserializeTests.cs`

**Full provider harness `CreateProviderWithFiles` helper (lines 263–331) — REUSE AS-IS or via shared helper extraction:**
```csharp
private static (SqlTableProvider provider, Mock<ISqlExecutor> executor, Mock<SqlTableWriter> writer, string inputRoot)
    CreateProviderWithFiles(
        IEnumerable<Dictionary<string, object?>> yamlRows,
        IEnumerable<Dictionary<string, object?>> existingDbRows)
{
    var mockExecutor = new Mock<ISqlExecutor>();
    var mockMetadataReader = new Mock<DataGroupMetadataReader>(mockExecutor.Object) { CallBase = false };
    mockMetadataReader.Setup(x => x.GetTableMetadata(...)).Returns(TestMetadata);
    mockMetadataReader.Setup(x => x.TableExists(It.IsAny<string>())).Returns(true);
    ...
    var dbReaderMock = CreateMockDataReader(...);
    mockExecutor.Setup(x => x.ExecuteReader(It.IsAny<CommandBuilder>())).Returns(dbReaderMock.Object);

    var tableReader = new SqlTableReader(mockExecutor.Object);
    var fileStore = new FlatFileStore();
    ...
    var writerMock = new Mock<SqlTableWriter>(mockExecutor.Object) { CallBase = false };
    var schemaCache = new TargetSchemaCache(_ => (...));
    var provider = new SqlTableProvider(
        mockMetadataReader.Object, tableReader, fileStore, writerMock.Object, schemaCache);

    return (provider, mockExecutor, writerMock, tempDir);
}
```

**Apply to SeedMergeTests:** Copy this helper verbatim or extract to a shared `SqlTableProviderTestFixture`. Every Seed-merge test case calls `CreateProviderWithFiles`, then invokes `provider.Deserialize(TestPredicate, inputRoot, strategy: ConflictStrategy.DestinationWins)`.

**`writerMock.Verify(...)` assertion pattern** (SqlTableProviderDeserializeTests.cs lines 131–133):
```csharp
writer.Verify(w => w.WriteRow(It.IsAny<Dictionary<string, object?>>(), It.IsAny<TableMetadata>(), true, It.IsAny<Action<string>?>(), It.IsAny<HashSet<string>?>()), Times.Once);
executor.Verify(x => x.ExecuteNonQuery(It.IsAny<CommandBuilder>()), Times.Never);
```

**Apply to Seed-merge assertions (research §Test Strategy SqlTable scenarios 1–9):** Each scenario verifies either:
- `writer.Verify(w => w.UpdateColumnSubset(...), Times.Once)` for "fill fired"
- `writer.Verify(w => w.UpdateColumnSubset(...), Times.Never)` for "all-set skip" or "checksum fast-path"
- `writer.Verify(w => w.WriteRow(...), Times.Once)` for "identity-unmatched fallthrough"

**Mock `IDataReader` fixture for existing-row simulation** (SqlTableProviderDeserializeTests.cs lines 333–363):
```csharp
private static Mock<IDataReader> CreateMockDataReader(string[] columns, object[][] rows)
{
    var mock = new Mock<IDataReader>();
    var rowIndex = -1;
    mock.Setup(r => r.Read()).Returns(() => { rowIndex++; return rowIndex < rows.Length; });
    mock.Setup(r => r.FieldCount).Returns(columns.Length);
    for (int i = 0; i < columns.Length; i++) { ... }
    return mock;
}
```

**Apply:** Copy verbatim — the Seed-merge "target row partially set" scenarios feed mixed-value rows into this reader to simulate the D-01 unset predicate firing per column.

---

### `tests/DynamicWeb.Serializer.Tests/Serialization/ContentDeserializerSeedMergeTests.cs` (NEW)

**Analog:** `tests/DynamicWeb.Serializer.Tests/Serialization/ContentDeserializerAreaSchemaTests.cs` (closest sibling) + `tests/DynamicWeb.Serializer.Tests/Fixtures/ContentTreeBuilder.cs`

**Note:** Per research A7 + [VERIFIED] grep, no existing test asserts `DestinationWins` Content semantics. The closest analog is the area-schema test file that exercises `ContentDeserializer` construction and Phase-37 `TargetSchemaCache` injection. Planner should open that file when writing this new test class to copy the DW-service-mocking and `WriteContext` setup shape.

**Content tree fixture** (`Fixtures/ContentTreeBuilder.cs` is the helper — import and use its builders for `SerializedPage` + `WriteContext` construction to match the repo pattern).

**Log-assertion shape** (StrictModeIntegrationTests.cs lines 84–97):
```csharp
var logs = new List<string>();
var escalator = new StrictModeEscalator(strict: false, log: logs.Add);
...
Assert.Contains(logs, l => l.Contains("template missing"));
```

**Apply to Seed-merge log assertions (D-11, D-19):**
```csharp
var logs = new List<string>();
// ... invoke ContentDeserializer with log=logs.Add
Assert.Contains(logs, l => l.Contains("Seed-merge:") && l.Contains("filled"));
Assert.DoesNotContain(logs, l => l.Contains("Seed-skip:"));  // D-11 regression guard
```

---

### `tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/XmlMergeHelperTests.cs` + `EcomXmlMergeTests.cs` (NEW, D-25)

**Analog:** `tests/DynamicWeb.Serializer.Tests/Infrastructure/XmlFormatterTests.cs`

**Pure-static helper test shape** (XmlFormatterTests.cs lines 1–45):
```csharp
using DynamicWeb.Serializer.Infrastructure;
using Xunit;

namespace DynamicWeb.Serializer.Tests.Infrastructure;

public class XmlFormatterTests
{
    [Fact]
    public void PrettyPrint_Null_ReturnsNull()
    {
        Assert.Null(XmlFormatter.PrettyPrint(null));
    }

    [Fact]
    public void PrettyPrint_MalformedXml_ReturnsUnchanged()
    {
        const string malformed = "<broken>";
        Assert.Equal(malformed, XmlFormatter.PrettyPrint(malformed));
    }

    [Fact]
    public void PrettyPrint_ModuleSettings_ProducesIndentedXmlWithDeclaration()
    {
        const string input = "<?xml version=\"1.0\" encoding=\"utf-8\"?><Settings>...</Settings>";
        var result = XmlFormatter.PrettyPrint(input);
        Assert.Contains("<?xml", result);
    }
}
```

**Apply to XmlMergeHelperTests (D-25 cases):** Same pure-static test style. One `[Fact]` per merge rule:
- `Merge_ElementMissingOnTarget_FillsFromSource`
- `Merge_ElementEmptyOnTarget_FillsFromSource` (D-22 rule)
- `Merge_ElementSetOnTarget_PreservesTarget`
- `Merge_WhitespaceOnlyElement_TreatedAsUnset` (or `AsSet` per planner decision — Open Question 1)
- `Merge_TargetOnlyElement_PreservedUntouched` (D-24)
- `Merge_NestedElements_MergedPerLeaf`
- `Merge_NameAttributeIdentity_WorksLikeCompactWithMerge` (EcomPayments `<Parameter name="Mail1SenderEmail">` shape)

**For `EcomXmlMergeTests.cs` (integration)** — round-trip scenario for `EcomPayments.PaymentGatewayParameters` + `EcomShippings.ShippingServiceParameters`. Use `SqlTableProviderSeedMergeTests`'s harness (shared `CreateProviderWithFiles` with XML column seeded) to verify end-to-end that Deploy-excluded `Mail1SenderEmail` element reappears after Seed.

---

## Shared Patterns

### Pattern: CommandBuilder `{0}`-placeholder parameterization
**Source:** `src/DynamicWeb.Serializer/Providers/SqlTable/SqlTableWriter.cs:60–95`, `src/DynamicWeb.Serializer/Infrastructure/TargetSchemaCache.cs:37–40`
**Apply to:** `SqlTableWriter.UpdateColumnSubset` (every user-supplied value goes through `cb.Add("{0}", v)`; every identifier is bracketed `[col]`)
```csharp
var cb = new CommandBuilder();
cb.Add($"UPDATE [{tableName}] SET [{col}]=");
cb.Add("{0}", value);  // SQL-injection-safe bind
```
**Security note:** Phase 37-03 `SqlIdentifierValidator` already governs table/column names at config-load; `UpdateColumnSubset` inherits this guarantee — no new validation surface.

### Pattern: Static utility class in `Infrastructure/`
**Source:** `src/DynamicWeb.Serializer/Infrastructure/TargetSchemaCache.cs`, `src/DynamicWeb.Serializer/Infrastructure/XmlFormatter.cs`, `src/DynamicWeb.Serializer/Configuration/ExclusionMerger.cs`
**Apply to:** `MergePredicate` (D-08, co-located with `TargetSchemaCache`) and `XmlMergeHelper` (D-27, co-located with `XmlFormatter`).
- `public static class Foo` — no DI, no state.
- Methods accept primitives or DTOs, return primitives or `null` / `HashSet<T>`.
- Null/empty input → null/passthrough, never throw.

### Pattern: `virtual` methods on writer for mockability
**Source:** `src/DynamicWeb.Serializer/Providers/SqlTable/SqlTableWriter.cs:129` (`public virtual WriteOutcome WriteRow(...)`)
**Apply to:** `UpdateColumnSubset` — must be `virtual` so `Mock<SqlTableWriter>(mockExecutor.Object) { CallBase = false }` (SqlTableProviderDeserializeTests.cs:318) can stub it in provider-level tests.

### Pattern: `WriteContext` counter repurpose (D-11)
**Source:** `src/DynamicWeb.Serializer/Serialization/ContentDeserializer.cs` WriteContext + `src/DynamicWeb.Serializer/Providers/ProviderDeserializeResult.cs` (`Skipped` int field)
**Apply to:** Both merge branches. `ctx.Skipped++` now means "all fields already set" (not "row not touched"). `ctx.Updated++` means "≥1 field filled". No schema change — pure semantic repurpose.

### Pattern: `Log($"... {identity} ...", log)` structured line with optional callback
**Source:** `src/DynamicWeb.Serializer/Providers/SqlTable/SqlTableProvider.cs:309, 320, 339`
**Apply to:** New `"Seed-merge: [identity] — N filled, M left"` format (D-11) and `"  would fill [col=X]: target=<unset> → seed='<v>'"` format (D-19). Keep the two-space indent convention for per-row details; zero-indent for the per-table summary.

### Pattern: ItemEntry live-read via `SerializeTo(dict)`
**Source:** `src/DynamicWeb.Serializer/Serialization/ContentMapper.cs:45` + ContentDeserializer.cs:1461 (inside `LogDryRunPageUpdate`)
**Apply to:** `MergeItemFields` / `MergePropertyItemFields` — call `itemEntry.SerializeTo(currentDict)` to dump current target state as strings (D-02), overlay filled subset onto the dict (Pitfall 7 defense), then `itemEntry.DeserializeFrom(merged)` + `itemEntry.Save()`.

### Pattern: Moq + FlatFileStore test harness for SqlTable provider
**Source:** `tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/SqlTableProviderDeserializeTests.cs:263–331` (`CreateProviderWithFiles`) + `333–363` (`CreateMockDataReader`)
**Apply to:** `SqlTableProviderSeedMergeTests.cs` — copy verbatim (or extract to shared base class). Every new Seed-merge scenario reuses this full wiring.

### Pattern: xUnit `[Trait("Category", "PhaseN")]` test classification
**Source:** `tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/SqlTableWriterTests.cs:10`, `tests/DynamicWeb.Serializer.Tests/Integration/StrictModeIntegrationTests.cs:16`
**Apply to:** All new Phase 39 test classes — add `[Trait("Category", "Phase39")]` for category-filtered test runs.

---

## No Analog Found

None. Every file in the new/modified list has an explicit in-repo analog at the exact granularity needed:
- New helpers analog to `ExclusionMerger` + `XmlFormatter` (static pure utilities).
- New tests analog to `ExclusionMergerTests` + `XmlFormatterTests` + `SqlTableWriterTests` + `SqlTableProviderDeserializeTests` + `PermissionDeserializationTests`.
- Pipeline extension is purely additive — steps 15.1/15.2/15.3 slot into the existing step 14/15 idiom.
- Modifications to `ContentDeserializer` / `SqlTableProvider` / `SqlTableWriter` are self-analogs (the source-wins path in each file is the shape the merge branch mirrors with a predicate wrap).

**Research §Assumption A7 confirms** there is no existing `DestinationWins`-behavior test for the Content path; this means `ContentDeserializerSeedMergeTests.cs` is the first of its kind for merge semantics, but the wiring (WriteContext construction, ContentDeserializer ctor, ContentTreeBuilder fixtures) is fully precedented.

---

## Metadata

**Analog search scope:** `src/DynamicWeb.Serializer/`, `tests/DynamicWeb.Serializer.Tests/`, `tools/e2e/`, `.planning/phases/39-.../` (CONTEXT + RESEARCH)
**Files scanned (directly read or grepped):** 18
- ContentDeserializer.cs (lines 670–735, 1135–1270, 1420–1500)
- SqlTableProvider.cs (lines 145–360)
- SqlTableWriter.cs (lines 1–200)
- TargetSchemaCache.cs (lines 1–100)
- XmlFormatter.cs (full)
- ExclusionMerger.cs (full)
- PermissionMapper.cs (partial)
- SqlTableProviderDeserializeTests.cs (full)
- SqlTableWriterTests.cs (partial)
- XmlFormatterTests.cs (partial)
- TargetSchemaCacheTests.cs (partial)
- ExclusionMergerTests.cs (partial)
- PermissionDeserializationTests.cs (partial)
- StrictModeIntegrationTests.cs (partial)
- full-clean-roundtrip.ps1 (partial + full step 10–20 span)
- 39-CONTEXT.md (full)
- 39-RESEARCH.md (lines 1–1232)

**Pattern extraction date:** 2026-04-22
