# Phase 1: Foundation - Research

**Researched:** 2026-03-19
**Domain:** C# DTO design, YamlDotNet serialization configuration, mirror-tree file I/O
**Confidence:** HIGH

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**DTO Field Design:**
- ItemType custom fields represented as `Dictionary<string, object>` — flexible key-value bag, works for any ItemType without schema discovery
- Parent-child relationships expressed via children collections: Page has `List<GridRow>`, GridRow has `List<Paragraph>`, etc. — tree structure in the DTO itself
- Capture ALL fields including system/audit fields (CreatedDate, ModifiedDate, CreatedBy, etc.) — full fidelity for exact replica on deserialize
- DTOs must have NO DynamicWeb dependencies — plain C# records/classes only

**YAML File Structure:**
- One .yml file per content item (page, grid row, paragraph) — granular files for clean git diffs
- Page .yml contains only page metadata and fields, NOT nested children
- Grid rows and paragraphs get their own files in subfolders within the page folder
- Example structure:
  ```
  Customer Center/
    page.yml           # Page metadata + fields only
    grid-row-1/
      grid-row.yml     # Grid row metadata
      paragraph-1.yml  # Paragraph content
      paragraph-2.yml
    Sub Page/
      page.yml
  ```

**Mirror-tree Naming:**
- Folder names use sanitized page names (special chars replaced, spaces preserved)
- Duplicate sibling page names disambiguated with short GUID suffix: "Customer Center [a1b2c3]"
- Output directory is configurable via config file (not a hardcoded path)
- Deterministic sort: items always written in a consistent order to prevent git noise

### Claude's Discretion

- .NET project layout (namespace conventions, folder structure)
- Exact sanitization rules for folder names (which characters to strip/replace)
- YamlDotNet serializer/deserializer configuration details (ScalarStyle, emitter settings)
- Test project structure and framework choice

### Deferred Ideas (OUT OF SCOPE)

None — discussion stayed within phase scope

</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| SER-01 | Serialize full content tree (Area > Page > Grid > Row > Paragraph) to YAML files | DTO types define the data contract for the full hierarchy; FileSystemStore writes each item to its own .yml file in the mirror-tree layout |
| SER-02 | Mirror-tree file layout — folder structure reflects content hierarchy with .yml per item | FileSystemStore path-building logic mirrors content hierarchy directly; folder names derived from sanitized item names; GUID suffix for duplicate siblings |
| SER-04 | Deterministic serialization order to prevent git noise from non-deterministic DB queries | DTOs include SortOrder field; FileSystemStore writes items in consistent sorted order; YAML serializer produces stable key ordering via YamlDotNet's `NamingConventions` |

</phase_requirements>

---

## Summary

Phase 1 establishes the shared data contract for the entire project. It has three concrete deliverables: plain C# DTO types for the full DynamicWeb content hierarchy, a YamlDotNet serializer configuration that provably round-trips known-tricky strings without data loss, and a FileSystemStore that reads and writes the mirror-tree folder layout.

This phase has no DynamicWeb API dependencies — it operates entirely on plain types and the file system. That makes it the only phase with comprehensive unit test coverage possible from day one. The critical investment here is in YAML scalar style configuration: if that is wrong when the first content is serialized, all downstream YAML files are suspect and must be regenerated. Proving round-trip fidelity before writing any other serialization code is the single most important risk mitigation for the project.

The file-per-item mirror-tree layout is well-established prior art (Sitecore Unicorn/Rainbow serialization format). The DTO design with `Dictionary<string, object>` for ItemType custom fields is the correct choice for a CMS where field schemas are defined per-installation — it avoids the need for schema discovery during serialization.

**Primary recommendation:** Design YamlDotNet scalar style configuration first, write the round-trip fidelity test before any other code, then build the DTOs and FileSystemStore. Do not write a single YAML file to disk until the round-trip test passes for all known-tricky strings.

---

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET 8.0 LTS | 8.0 | Target framework | Required by DynamicWeb 10.2+. LTS release. |
| YamlDotNet | 16.3.0 | YAML serialization/deserialization | Dominant .NET YAML library, 43.5M+ downloads, actively maintained. No meaningful competition. Published 2024-12-23. |
| xunit | 2.9.3 | Unit test framework | Dominant community choice for .NET libraries. Stable v2 series. |
| xunit.runner.visualstudio | 3.1.5 | Test runner (VS/CLI) | Required to run xUnit tests via `dotnet test`. |
| Moq | 4.20.72 | Mocking | Standard .NET mock library. Used to mock file system in FileSystemStore tests. |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Microsoft.Extensions.Configuration.Json | 8.0.x | JSON config file loading | Phase 1: load the configurable output directory path from a JSON config file. Phase 2 will expand this for full predicate config. |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| YamlDotNet 16.3.0 | SharpYaml | Never — SharpYaml is unmaintained since 2018, not compatible with modern .NET targets. |
| xunit 2.9.3 | xunit 3.x | v3 has breaking API changes; v2 is still actively maintained for security fixes. Either works; v2 is safer for a new project. |
| xunit 2.9.3 | NUnit or MSTest | Both are acceptable; xUnit is the dominant community choice for .NET libraries. |

**Installation:**
```xml
<!-- ContentSync.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="YamlDotNet" Version="16.3.0" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="8.0.1" />
  </ItemGroup>
</Project>

<!-- ContentSync.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.5" />
    <PackageReference Include="Moq" Version="4.20.72" />
    <ProjectReference Include="../ContentSync/ContentSync.csproj" />
  </ItemGroup>
</Project>
```

**Version verification:** YamlDotNet 16.3.0 confirmed current on 2026-03-19 (NuGet.org — published 2024-12-23). xunit 2.9.3 confirmed on NuGet.org.

---

## Architecture Patterns

### Recommended Project Structure

This phase delivers only the Models/ and Infrastructure/ layers. Other layers are stubs or absent.

```
Dynamicweb.ContentSync/
├── Models/
│   ├── SerializedArea.cs           # DTO for Website/Area
│   ├── SerializedPage.cs           # DTO for Page + ItemType custom fields
│   ├── SerializedGridRow.cs        # DTO for GridRow
│   ├── SerializedGridColumn.cs     # DTO for GridColumn grouping
│   └── SerializedParagraph.cs      # DTO for Paragraph + ItemType custom fields
│
├── Infrastructure/
│   ├── FileSystemStore.cs          # Mirror-tree read/write; path building; duplicate slug handling
│   └── YamlConfiguration.cs        # Centralised YamlDotNet Serializer/Deserializer builder
│
└── ContentSync.Tests/
    ├── Models/
    │   └── DtoTests.cs             # Equality, null handling, Dictionary round-trip
    ├── Infrastructure/
    │   ├── YamlRoundTripTests.cs   # Known-tricky strings: tilde, CRLF, HTML, quotes, bang
    │   └── FileSystemStoreTests.cs # Mirror-tree path building; write/read; determinism
    └── Fixtures/
        └── ContentTreeBuilder.cs   # Helpers that build test content trees
```

### Pattern 1: Plain C# Record DTOs (no DW dependencies)

**What:** Each DynamicWeb content node type maps to a C# record. Fields are all value types or standard collections. `Dictionary<string, object>` holds ItemType custom fields. No `using Dynamicweb.*` anywhere in the Models/ folder.

**When to use:** Always. The DTO layer is the isolation boundary between the DW API and the rest of the system.

**Example:**
```csharp
// No DW references — plain C# only
public record SerializedPage
{
    public required Guid PageUniqueId { get; init; }
    public required string Name { get; init; }
    public required string MenuText { get; init; }
    public required string UrlName { get; init; }
    public required int SortOrder { get; init; }
    public bool IsActive { get; init; }

    // System/audit fields — full fidelity capture
    public DateTime? CreatedDate { get; init; }
    public DateTime? UpdatedDate { get; init; }
    public string? CreatedBy { get; init; }
    public string? UpdatedBy { get; init; }

    // ItemType custom fields — flexible key-value bag
    public Dictionary<string, object> Fields { get; init; } = new();
}
```

### Pattern 2: YamlDotNet ScalarStyle Configuration

**What:** YamlDotNet's default scalar style inference has well-known gaps for CMS content. A custom `ChainedEventEmitter` forces `ScalarStyle.Literal` (pipe block `|`) for multiline strings and HTML. `ScalarStyle.DoubleQuoted` is the safe default for all other strings. This is applied at the serializer level — individual DTO properties do not need attributes.

**When to use:** Always, for every `Serializer` and `Deserializer` instance in the project.

**Why this matters:** The tilde character (`~`) is parsed as YAML null without quoting. CRLF (`\r\n`) is normalized to LF by the default emitter. Raw HTML may be truncated or escaped. Bang (`!`) triggers YAML type tags. All of these cause silent deserialization of different values from what was serialized — the content appears to round-trip but the field values differ.

**Example:**
```csharp
// Source: YamlDotNet GitHub Issues #846, #391 — verified patterns
public static class YamlConfiguration
{
    public static ISerializer BuildSerializer() =>
        new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .WithEventEmitter(next => new ForceStringScalarEmitter(next))
            .Build();

    public static IDeserializer BuildDeserializer() =>
        new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
}

// Custom emitter: forces literal block for multiline, double-quoted for everything else
public class ForceStringScalarEmitter : ChainedEventEmitter
{
    public ForceStringScalarEmitter(IEventEmitter next) : base(next) { }

    public override void Emit(ScalarEventInfo eventInfo, IEmitter emitter)
    {
        if (eventInfo.Source.Type == typeof(string) && eventInfo.Source.Value is string value)
        {
            if (value.Contains('\n') || value.Contains('\r'))
                eventInfo.Style = ScalarStyle.Literal;
            else
                eventInfo.Style = ScalarStyle.DoubleQuoted;
        }
        base.Emit(eventInfo, emitter);
    }
}
```

### Pattern 3: Mirror-Tree Path Building

**What:** FileSystemStore computes a deterministic file path for any content item from its position in the content hierarchy. Folder names are derived from the item's `Name` field with sanitization. Duplicate siblings get a short GUID suffix. Files are always named by type (`page.yml`, `grid-row.yml`, `paragraph-{N}.yml`).

**When to use:** All writes and reads through FileSystemStore.

**Example:**
```csharp
// Sanitize: strip characters invalid in Windows paths; preserve spaces per user decision
private static string SanitizeFolderName(string name)
{
    var invalid = Path.GetInvalidFileNameChars();
    return string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c));
}

// Disambiguation: "Customer Center [a1b2c3]" when sibling name would collide
private static string DeduplicateSiblingName(string sanitized, Guid guid)
{
    var suffix = guid.ToString("N")[..6]; // first 6 hex chars
    return $"{sanitized} [{suffix}]";
}
```

### Pattern 4: Deterministic Write Ordering

**What:** When writing a content tree to disk, items are always sorted by `SortOrder` within their parent scope before being written. This ensures that serializing the same content tree twice produces byte-for-byte identical output, regardless of the order the items were retrieved from the database.

**When to use:** All writes in FileSystemStore. The sort is applied per-level (area children sorted, then page children sorted, etc.).

**Example:**
```csharp
// Sort pages by SortOrder before writing — prevents git noise from DB query order variance
var sortedPages = pages.OrderBy(p => p.SortOrder).ThenBy(p => p.Name);
```

### Anti-Patterns to Avoid

- **YamlDotNet default scalar styles:** Never use `new SerializerBuilder().Build()` without configuring scalar styles. The defaults cause silent round-trip data loss for tilde, CRLF, HTML content, and bang characters. Configure a `ChainedEventEmitter` before writing any YAML to disk.
- **Serializing children inline in the parent YAML:** The decision is one file per item — page.yml must contain page metadata only, not the list of GridRows. Children go in subfolders.
- **Hardcoded output paths:** The output directory must come from config. No `Path.Combine("C:\\content", ...)` anywhere in the codebase.
- **Relying on dictionary key order:** `Dictionary<string, object>` does not guarantee insertion order for YAML output. Use `SortedDictionary<string, object>` for `Fields` if deterministic key ordering in the YAML file matters, or apply a `ToSortedDictionary()` transform before serializing.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| YAML serialization | Custom YAML writer | YamlDotNet 16.3.0 | YAML spec has 30+ edge cases (anchors, aliases, multi-document, type tags, block vs flow). Hand-rolled output will fail on CMS content within days. |
| YAML scalar quoting logic | Custom string escaper | YamlDotNet `ChainedEventEmitter` | The emitter hooks are the designed extension point. Writing a pre-pass string escaper misses deserialization symmetry. |
| Test file system | Abstract `IFileSystem` and mock it | Use real temp directory in tests + `Path.GetTempPath()` | FileSystemStore tests need real I/O behavior (path casing, separator, long paths). Fake file systems hide real bugs. |

**Key insight:** YamlDotNet's extension points (`ChainedEventEmitter`, `NamingConvention`, `TypeConverter`) are sufficient for all CMS serialization requirements. The pattern is: configure the builder once in `YamlConfiguration`, use that builder everywhere, never configure inline.

---

## Common Pitfalls

### Pitfall 1: YAML Round-Trip Data Loss from Default Scalar Styles

**What goes wrong:** Tilde (`~`) deserializes as `null`. CRLF (`\r\n`) becomes LF or disappears. Raw HTML fields survive serialization visually but fail byte-for-byte comparison. Bang (`!`) triggers YAML type tags, breaking deserialization.

**Why it happens:** YamlDotNet's default emitter chooses scalar style by heuristics that have known gaps for CMS content. This is not a bug being fixed — it is by-design YAML spec behavior.

**How to avoid:** Implement `ForceStringScalarEmitter` (see Pattern 2 above) as the first piece of code in this phase. Write `YamlRoundTripTests` for all five known-tricky strings. Do not proceed to FileSystemStore until all round-trip tests pass.

**Warning signs:** Deserialized string value differs from original when compared with `==`; HTML fields truncated at `<` or `>`; `~` values become null in deserialized DTO.

### Pitfall 2: Non-Deterministic YAML Key Ordering for Dictionary Fields

**What goes wrong:** `Dictionary<string, object>` for ItemType `Fields` enumerates keys in insertion order (in modern .NET), but that order is whatever the DB returned. Two serializations of the same page may emit `Fields` keys in different orders, producing a non-empty `git diff` even when nothing changed.

**Why it happens:** The "deterministic" requirement (SER-04) applies to the full YAML file output, not just the item ordering. Dictionary keys are part of the file content.

**How to avoid:** Before passing `Fields` to YamlDotNet, sort the dictionary by key: `item.Fields.OrderBy(kv => kv.Key)`. Apply this transform in the serialization path, not in the DTO constructor.

**Warning signs:** `git diff` shows reordered keys in `fields:` block after re-serializing with unchanged content.

### Pitfall 3: Windows Path Length Overflow on Deep Trees

**What goes wrong:** The mirror-tree layout maps hierarchy depth directly to directory depth. Pages deeply nested in the content tree, combined with long page names, easily exceed Windows MAX_PATH (260 characters). `File.WriteAllText` throws `PathTooLongException`, or silently truncates the path on older .NET runtimes.

**Why it happens:** .NET 8 on Windows supports long paths when `AppContext.SetSwitch("Switch.System.IO.UseLegacyPathHandling", false)` is set or when running on Windows 10 1607+ with long paths enabled in group policy. The default is still 260 chars unless explicitly configured.

**How to avoid:** Set `<AppContextSwitchOverrides value="Switch.System.IO.UseLegacyPathHandling=false" />` or use `\\?\`-prefixed paths for all file operations. Add a path length check in FileSystemStore: if the computed path exceeds 200 characters before the filename, truncate the slug and append a short hash. Log the truncation. Test with 6+ directory depth and 60+ character names.

**Warning signs:** `PathTooLongException` in test output; items missing from output directory with no error logged.

### Pitfall 4: Duplicate Sibling Names Without Disambiguation

**What goes wrong:** Two pages under the same parent with the same name (or names that sanitize to the same string) both try to write to the same folder. The second write either fails or silently overwrites the first.

**Why it happens:** The DynamicWeb content model allows duplicate page names at the same hierarchy level. The file system does not.

**How to avoid:** In FileSystemStore, before creating a folder, check whether the sanitized name already exists among siblings in the output directory. If so, append the `[{guid-prefix}]` suffix as per the locked decision. The check must happen at write time, not be pre-computed, because the output directory state is the ground truth.

**Warning signs:** Fewer output files than input items; no error logged; second page with duplicate name is simply absent from output.

---

## Code Examples

Verified patterns from official sources:

### YamlDotNet Serializer Builder (Full Configuration)

```csharp
// Source: YamlDotNet GitHub README + Issues #846, #391
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization.EventEmitters;

public static ISerializer BuildSerializer() =>
    new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .WithEventEmitter(next => new ForceStringScalarEmitter(next))
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();

public static IDeserializer BuildDeserializer() =>
    new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();
```

### Round-Trip Fidelity Test (Known-Tricky Strings)

```csharp
// Source: PITFALLS.md — must pass before any other serialization code ships
[Theory]
[InlineData("~")]                          // YAML null without quoting
[InlineData("Hello\r\nWorld")]             // CRLF — normalized by default emitter
[InlineData("<p>Hello &amp; World</p>")]   // Raw HTML
[InlineData("\"quoted\"")]                 // Double quotes
[InlineData("!important")]                 // Bang — triggers YAML type tag
public void Yaml_RoundTrips_TrickyString(string original)
{
    var page = new SerializedPage { /* ... Fields["body"] = original */ };
    var serializer = YamlConfiguration.BuildSerializer();
    var deserializer = YamlConfiguration.BuildDeserializer();

    var yaml = serializer.Serialize(page);
    var roundTripped = deserializer.Deserialize<SerializedPage>(yaml);

    Assert.Equal(original, roundTripped.Fields["body"]);
}
```

### FileSystemStore — Compute Mirror Path

```csharp
// Page path: {rootDir}/{sanitized-area}/{sanitized-page}/page.yml
// Grid row path: {rootDir}/{sanitized-area}/{sanitized-page}/grid-row-{n}/grid-row.yml
public string GetPageDirectory(string rootDir, string areaName, string pageName, Guid pageGuid)
{
    var areaSegment = SanitizeFolderName(areaName);
    var pageSegment = SanitizeFolderNameWithDedup(pageName, pageGuid, rootDir, areaSegment);
    return Path.Combine(rootDir, areaSegment, pageSegment);
}
```

### Deterministic Serialization Test

```csharp
// Verifies SER-04: same content tree produces byte-for-byte identical output on repeat runs
[Fact]
public void FileSystemStore_Write_IsIdempotent()
{
    using var tmp = new TempDirectory();
    var tree = ContentTreeBuilder.BuildSampleTree();

    store.WriteTree(tree, tmp.Path);
    var first = ReadAllYamlFiles(tmp.Path);

    store.WriteTree(tree, tmp.Path);
    var second = ReadAllYamlFiles(tmp.Path);

    Assert.Equal(first, second); // byte-for-byte identical
}
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| SharpYaml for .NET YAML | YamlDotNet 16.x | SharpYaml abandoned ~2018 | SharpYaml is not compatible with net8.0; do not use it |
| YamlDotNet `Serialize(object)` with default styles | `SerializerBuilder` with `ChainedEventEmitter` | YamlDotNet 6+ | Extension point model is the correct approach; fluent builder replaces attribute-based configuration |
| C# classes with setters for DTOs | C# `record` with `init` properties | C# 9 / .NET 5 | Records enforce immutability and give structural equality for free; prefer records over classes for DTOs |

**Deprecated/outdated:**
- `YamlDotNet.RepresentationModel` (low-level graph API): Use the object graph serializer (SerializerBuilder) instead. The representation model is for advanced YAML manipulation, not round-trip object serialization.
- `[YamlIgnore]` / `[YamlMember]` attribute-based configuration: Valid but less flexible than the fluent builder. Prefer builder configuration for project-wide settings, attributes only for per-property exceptions.

---

## Open Questions

1. **SortedDictionary vs. post-sort transform for Fields**
   - What we know: `Dictionary<string, object>` does not guarantee stable key ordering; non-deterministic key order produces git noise.
   - What's unclear: Whether `SortedDictionary<string, object>` is better stored in the DTO or whether a pre-serialization sort transform is cleaner.
   - Recommendation: Use a pre-serialization sort transform in `YamlConfiguration.BuildSerializer()` or in the mapping layer. Keep `Dictionary` in the DTO for flexibility; sort at the serialization callsite. This avoids coupling the DTO type to a sorted implementation.

2. **GridColumn as DTO vs. inline in GridRow**
   - What we know: The content hierarchy is Area > Page > GridRow > GridColumn > Paragraph. The locked decision gives GridRow and Paragraph their own files.
   - What's unclear: Whether GridColumn needs its own file or is serialized inline within the GridRow .yml. The CONTEXT.md example shows `grid-row-1/paragraph-1.yml` without a column subfolder.
   - Recommendation: Serialize GridColumn as inline data within the GridRow .yml (not a separate file). Paragraphs belong to both a GridRow and a GridColumn (tracked via a `GridRowColumn` integer field on Paragraph). The column grouping is metadata on the paragraph, not a separate content item.

3. **App pool write permission scope**
   - What we know: The output directory is configurable. The DW app pool identity may not have write access outside the web root.
   - What's unclear: The target deployment environment's write permissions — this is environment-specific.
   - Recommendation: Document the permission requirement. In FileSystemStore, add a startup check that verifies the configured output path is writable before beginning any serialization run. Fail-fast with a clear error message if not writable.

---

## Validation Architecture

> `nyquist_validation` is `true` in `.planning/config.json` — section included.

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xunit 2.9.3 |
| Config file | None — see Wave 0 (ContentSync.Tests.csproj to be created) |
| Quick run command | `dotnet test ContentSync.Tests --filter "Category=Unit" --no-build` |
| Full suite command | `dotnet test ContentSync.Tests` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| SER-01 | All DTO types exist with correct shape (no DW deps, correct field set) | Unit | `dotnet test --filter "FullyQualifiedName~DtoTests"` | Wave 0 |
| SER-01 | Dictionary<string,object> Fields round-trips through YAML without loss | Unit | `dotnet test --filter "FullyQualifiedName~YamlRoundTripTests"` | Wave 0 |
| SER-02 | FileSystemStore creates correct mirror-tree folder layout | Unit | `dotnet test --filter "FullyQualifiedName~FileSystemStoreTests"` | Wave 0 |
| SER-02 | Duplicate sibling names get GUID-suffix disambiguation | Unit | `dotnet test --filter "FullyQualifiedName~FileSystemStoreTests.Dedup"` | Wave 0 |
| SER-02 | Each item has exactly one .yml file in its directory | Unit | `dotnet test --filter "FullyQualifiedName~FileSystemStoreTests.OneFilePerItem"` | Wave 0 |
| SER-04 | Serializing same tree twice produces byte-for-byte identical YAML | Unit | `dotnet test --filter "FullyQualifiedName~FileSystemStoreTests.Idempotent"` | Wave 0 |
| INF-02 (phase 1 portion) | Tilde, CRLF, HTML, double quotes, bang all round-trip without data loss | Unit | `dotnet test --filter "FullyQualifiedName~YamlRoundTripTests"` | Wave 0 |

### Sampling Rate

- **Per task commit:** `dotnet test ContentSync.Tests`
- **Per wave merge:** `dotnet test ContentSync.Tests`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps

- [ ] `ContentSync/ContentSync.csproj` — main project file (net8.0, YamlDotNet ref)
- [ ] `ContentSync.Tests/ContentSync.Tests.csproj` — test project file (xunit, Moq, ProjectRef)
- [ ] `ContentSync.Tests/Infrastructure/YamlRoundTripTests.cs` — covers INF-02 / SER-01 YAML fidelity
- [ ] `ContentSync.Tests/Infrastructure/FileSystemStoreTests.cs` — covers SER-02, SER-04
- [ ] `ContentSync.Tests/Models/DtoTests.cs` — covers SER-01 DTO shape
- [ ] `ContentSync.Tests/Fixtures/ContentTreeBuilder.cs` — shared test data builder

---

## Sources

### Primary (HIGH confidence)

- [NuGet: YamlDotNet 16.3.0](https://www.nuget.org/packages/YamlDotNet) — verified version 16.3.0, published 2024-12-23, 43.5M+ downloads, net8.0 supported
- [YamlDotNet GitHub Issue #846](https://github.com/aaubry/YamlDotNet/issues/846) — special character serialization issues: tilde, bang, quotes
- [YamlDotNet GitHub Issue #391](https://github.com/aaubry/YamlDotNet/issues/391) — multiline ScalarStyle.Literal pattern; `ChainedEventEmitter` extension point
- [YamlDotNet GitHub Issue #361](https://github.com/aaubry/YamlDotNet/issues/361) — newlines/CRLF normalization behavior
- [YamlDotNet GitHub Issue #934](https://github.com/aaubry/YamlDotNet/issues/934) — numeric string encoding pitfall
- [Windows MAX_PATH documentation](https://learn.microsoft.com/en-us/windows/win32/fileio/maximum-file-path-limitation) — 260-char default, long path opt-in
- .planning/research/STACK.md — YamlDotNet versions, xunit versions, .NET 8 requirement; NuGet-verified 2026-03-19
- .planning/research/PITFALLS.md — YAML round-trip pitfalls section; verified against YamlDotNet issue tracker
- .planning/research/ARCHITECTURE.md — DTO design patterns, Models/ structure, FileSystemStore role; verified against Unicorn prior art

### Secondary (MEDIUM confidence)

- [Sitecore Unicorn/Rainbow GitHub](https://github.com/SitecoreUnicorn/Unicorn) — mirror-tree file layout prior art; file-per-item pattern; duplicate-name disambiguation strategy
- [Bloomreach YAML format docs](https://xmdocumentation.bloomreach.com/library/concepts/configuration-management/yaml-format.html) — same-name sibling index cross-environment issues

### Tertiary (LOW confidence)

None — all claims in this document are backed by primary or secondary sources.

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — YamlDotNet 16.3.0 and xunit 2.9.3 verified on NuGet.org 2026-03-19
- Architecture: HIGH — DTO design and FileSystemStore pattern derived from Unicorn prior art + ARCHITECTURE.md; no DW API dependencies in this phase
- Pitfalls: HIGH — YAML scalar style pitfalls verified against YamlDotNet issue tracker; path length from official Microsoft docs; duplicate-name disambiguation from Unicorn issue tracker

**Research date:** 2026-03-19
**Valid until:** 2026-04-19 (stable domain — YamlDotNet releases are infrequent; .NET 8 is LTS)
