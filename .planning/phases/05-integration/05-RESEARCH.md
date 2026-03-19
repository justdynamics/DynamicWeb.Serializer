# Phase 5: Integration - Research

**Researched:** 2026-03-19
**Domain:** DynamicWeb AppStore packaging, NuGet metadata, scheduled task wiring, serializer logging enhancement, end-to-end integration tests
**Confidence:** HIGH

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

#### NuGet Package Shape (INF-01)
- PackageId: `Dynamicweb.ContentSync` — matches assembly name, consistent with DW AppStore conventions
- Version: `0.1.0-beta` — pre-release tag, first beta before committing to stable API
- Tags: must include `dynamicweb-app-store` and `task` per DW AppStore requirements
- Switch from DLL references to NuGet package references for Dynamicweb.dll and Dynamicweb.Core.dll — cleaner dependency resolution for distributable package
- Standard NuGet metadata to add: Authors, Description, License, ProjectUrl, RepositoryUrl

#### End-to-End Verification
- Integration tests only — no manual DW admin testing required
- Extend existing integration test project to instantiate and call both scheduled tasks programmatically
- Verifies the full pipeline without needing the DW admin UI
- Byte-identical YAML output: serialize task via scheduled task entry point must produce identical output to calling ContentSerializer directly. Deterministic serialization guarantees from Phase 1 apply
- Compare directory trees to assert identical output

#### Serialize Logging Parity (OPS-03)
- No SerializeResult object — current basic logging is sufficient for the serialize side
- Add a count summary line at end of serialization: "Serialization complete: X pages, Y grid rows, Z paragraphs serialized"
- Lightweight enhancement — keep existing `Action<string>` logging pattern, just add aggregate counts
- OPS-03's full per-item structured summary (new/updated/skipped/failed with error details) applies primarily to deserialization, which already has DeserializeResult

### Claude's Discretion
- Exact NuGet package metadata values (Authors, Description text, license type)
- Which Dynamicweb NuGet packages to reference and their versions (research needed — known issue from Phase 3 that NuGet packages may not include all namespaces)
- How to structure the count tracking in ContentSerializer (local variables vs a lightweight struct)
- Integration test assertions and helper methods
- Whether to add a sample ContentSync.config.json to the NuGet package content files

### Deferred Ideas (OUT OF SCOPE)
None — discussion stayed within phase scope
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| OPS-01 | Scheduled task for full serialization (DB to disk) | Task already implemented as `SerializeScheduledTask`; E2E test wires the task entry point |
| OPS-02 | Scheduled task for full deserialization (disk to DB) | Task already implemented as `DeserializeScheduledTask`; E2E test wires the task entry point |
| OPS-03 | Structured logging — log new, updated, skipped items and errors | Serialize side: add count summary line; deserialize side: already complete via `DeserializeResult.Summary` |
| INF-01 | DynamicWeb AppStore app structure (.NET 8.0+, NuGet package) | NuGet metadata in csproj; switch DLL refs to NuGet package refs; required tags confirmed |
</phase_requirements>

---

## Summary

Phase 5 is a packaging and polish phase, not a feature-building phase. Both scheduled tasks (`SerializeScheduledTask`, `DeserializeScheduledTask`) already exist and are functionally complete. The work is: (1) add NuGet metadata to the csproj and switch from DLL references to NuGet package references, (2) add a count summary line to `ContentSerializer.Serialize()`, and (3) write end-to-end integration tests that exercise the full pipeline through the scheduled task entry points.

The critical research question — which NuGet packages provide the DW namespaces — is now fully resolved. `Dynamicweb.dll` ships in the `Dynamicweb` NuGet package (version 10.23.9 matches the lib/ DLLs) and provides `Dynamicweb.Content.*` (PageService, AreaService, GridService, ParagraphService, Services static accessor). `Dynamicweb.Core.dll` ships in the `Dynamicweb.Core` NuGet package and provides both `Dynamicweb.Scheduling.BaseScheduledTaskAddIn` AND `Dynamicweb.Extensibility.AddIns.*` (AddInName, AddInLabel, AddInDescription attributes). The `Dynamicweb` package depends on `Dynamicweb.Core`, so a single `<PackageReference Include="Dynamicweb" Version="10.23.9" />` transitively provides everything.

The Phase 3 concern that "Dynamicweb.Core NuGet lacks Dynamicweb.Content namespace" was accurate at the time — `Dynamicweb.Core` alone does NOT contain `Dynamicweb.Content`. The fix is to reference `Dynamicweb` (the main package), not `Dynamicweb.Core`. The `Dynamicweb.Scheduler` package is NOT needed for adding or implementing scheduled task addins — it is the scheduler runtime, which the DW host provides. The addin base class and attributes live in `Dynamicweb.Core`.

**Primary recommendation:** Replace both `<Reference>` DLL entries with `<PackageReference Include="Dynamicweb" Version="10.23.9" />`. This provides all required namespaces transitively and allows the package to be consumed as a NuGet reference.

---

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Dynamicweb | 10.23.9 | `Dynamicweb.Content.*` — PageService, AreaService, GridService, ParagraphService, Services | The main DW NuGet package; version matches lib/ DLLs in this repo; provides Dynamicweb.dll transitively |
| Dynamicweb.Core | 10.23.9 | `Dynamicweb.Scheduling.*` (BaseScheduledTaskAddIn), `Dynamicweb.Extensibility.AddIns.*` (AddInName/Label/Description) | Already a transitive dep via Dynamicweb; listing explicitly ensures version pinning |
| YamlDotNet | 13.7.1 | YAML serialization/deserialization | Already in use; do not change |
| Microsoft.Extensions.Configuration.Json | 8.0.1 | JSON config loading | Already in use; do not change |
| xunit | 2.9.3 | Integration tests | Already in use by test project |

### NuGet Metadata (csproj properties to add)
These properties are required by the DynamicWeb AppStore listing system:

| Property | Required Value | Source |
|----------|---------------|--------|
| `PackageId` | `Dynamicweb.ContentSync` | Locked decision |
| `Version` | `0.1.0-beta` | Locked decision |
| `PackageTags` | `dynamicweb-app-store task dw10 addin` | AppStore docs + research — `dynamicweb-app-store` is mandatory; `dw10`, `addin`, `task` are categorization tags |
| `GeneratePackageOnBuild` | `true` | Required so `dotnet build` produces the .nupkg |
| `TargetFramework` | `net8.0` | Already set |
| `Title` | Claude's discretion | NuGet display name |
| `Description` | Claude's discretion | NuGet search text |
| `Authors` | Claude's discretion | Required for valid NuGet package |
| `PackageProjectUrl` | Claude's discretion | AppStore docs example |
| `RepositoryUrl` | Claude's discretion | NuGet metadata |

**Installation (switch from DLL refs to NuGet):**
```xml
<!-- Remove these: -->
<Reference Include="Dynamicweb">
  <HintPath>..\..\lib\Dynamicweb.dll</HintPath>
</Reference>
<Reference Include="Dynamicweb.Core">
  <HintPath>..\..\lib\Dynamicweb.Core.dll</HintPath>
</Reference>

<!-- Add this: -->
<PackageReference Include="Dynamicweb" Version="10.23.9" />
```

**Version verification:** The lib/ DLLs (`Dynamicweb.dll`, `Dynamicweb.Core.dll`) are both version 10.23.9.0, confirmed via `[FileVersionInfo]::GetVersionInfo()`. The `Dynamicweb` NuGet package version 10.23.9 is the latest stable release (released 2026-03-17 per NuGet.org). These match.

---

## Architecture Patterns

### DW AppStore App csproj Structure (complete)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>Dynamicweb.ContentSync</RootNamespace>
    <AssemblyName>Dynamicweb.ContentSync</AssemblyName>

    <!-- NuGet package metadata (INF-01) -->
    <PackageId>Dynamicweb.ContentSync</PackageId>
    <Version>0.1.0-beta</Version>
    <Title>ContentSync for DynamicWeb</Title>
    <Description>...</Description>
    <Authors>...</Authors>
    <PackageProjectUrl>...</PackageProjectUrl>
    <RepositoryUrl>...</RepositoryUrl>
    <PackageTags>dynamicweb-app-store task dw10 addin</PackageTags>
    <GeneratePackageOnBuild>true</GeneratePackageOnBuild>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Dynamicweb" Version="10.23.9" />
    <PackageReference Include="YamlDotNet" Version="13.7.1" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="8.0.1" />
  </ItemGroup>
</Project>
```

### Pattern 1: Scheduled Task Addin Discovery

DW discovers scheduled task addins automatically at runtime by scanning loaded assemblies for classes that inherit from `BaseScheduledTaskAddIn` and have `[AddInName]`, `[AddInLabel]`, `[AddInDescription]` attributes. No explicit registration or service registration is required beyond the attributes.

**What:** Addin scanning is reflection-based, happens at DW startup when the host loads assemblies from the bin folder.
**When to use:** The existing pattern in `SerializeScheduledTask` and `DeserializeScheduledTask` is correct. No additional wiring is needed.

```csharp
// Source: Dynamicweb.Core.xml (Dynamicweb.Extensibility.AddIns namespace) + existing code
[AddInName("ContentSync.Serialize")]
[AddInLabel("ContentSync - Serialize")]
[AddInDescription("...")]
public class SerializeScheduledTask : BaseScheduledTaskAddIn
{
    public override bool Run() { ... }
}
```

Both `BaseScheduledTaskAddIn` (namespace: `Dynamicweb.Scheduling`) and the attributes (namespace: `Dynamicweb.Extensibility.AddIns`) are in `Dynamicweb.Core.dll`.

### Pattern 2: Count Tracking in ContentSerializer (OPS-03)

The `Serialize()` method currently iterates predicates and calls `SerializePredicate()` which calls `SerializePage()` recursively. To add a count summary, use local accumulators at the `Serialize()` level without changing the return type of helper methods.

**Option A — local variables in `Serialize()` and pass-by-ref into helpers:**
Works but changes multiple method signatures.

**Option B — lightweight result struct from `SerializePredicate()`:**
`SerializePredicate` already returns `SerializedArea?`. A simple approach is to count pages after serialization by examining the returned tree rather than tracking during traversal.

**Recommended approach (Claude's discretion):** Count pages, grid rows, and paragraphs from the returned `SerializedArea` object after `SerializePredicate()` returns — zero signature changes, no new types. Add a local accumulator in `Serialize()`:

```csharp
// Source: derived from existing ContentSerializer.cs pattern
public void Serialize()
{
    int totalPages = 0, totalGridRows = 0, totalParagraphs = 0;

    foreach (var predicate in _configuration.Predicates)
    {
        var area = SerializePredicate(predicate);
        _referenceResolver.Clear();

        if (area != null)
        {
            // Count items from serialized tree
            CountItems(area.Pages, ref totalPages, ref totalGridRows, ref totalParagraphs);
        }
    }

    Log($"Serialization complete: {totalPages} pages, {totalGridRows} grid rows, {totalParagraphs} paragraphs serialized.");
}

private static void CountItems(IEnumerable<SerializedPage> pages, ref int pageCount, ref int gridRowCount, ref int paragraphCount)
{
    foreach (var page in pages)
    {
        pageCount++;
        gridRowCount += page.GridRows.Count;
        paragraphCount += page.GridRows.Sum(gr => gr.Columns.Sum(c => c.Paragraphs.Count));
        CountItems(page.Children, ref pageCount, ref gridRowCount, ref paragraphCount);
    }
}
```

### Pattern 3: End-to-End Scheduled Task Test

The E2E tests instantiate the scheduled task's `Run()` method directly. The challenge is that `Run()` uses `FindConfigFile()` (a private method searching file system paths) to locate `ContentSync.config.json`. Tests must write a config file to a path that `FindConfigFile()` will find, specifically `AppDomain.CurrentDomain.BaseDirectory + "ContentSync.config.json"` which is the last candidate in the fallback list.

```csharp
// Source: derived from existing test patterns in CustomerCenterSerializationTests.cs
// and SerializeScheduledTask.cs FindConfigFile() implementation

[Fact]
public void SerializeScheduledTask_Run_ProducesSameOutputAsContentSerializer()
{
    // 1. Write ContentSync.config.json to BaseDirectory
    var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ContentSync.config.json");
    // write JSON config pointing to a temp output dir

    // 2. Run ContentSerializer directly → first output tree
    var serializer = new ContentSerializer(config);
    serializer.Serialize();
    // snapshot firstTree

    // 3. Run SerializeScheduledTask.Run() → second output tree
    var task = new SerializeScheduledTask();
    var result = task.Run();
    Assert.True(result);
    // snapshot secondTree

    // 4. Assert byte-identical trees
    AssertDirectoryTreesEqual(firstTreeDir, secondTreeDir);
}
```

**Key constraint:** `FindConfigFile()` is private and searches for `ContentSync.config.json` using a 4-path cascade. The config file must be placed at `AppDomain.CurrentDomain.BaseDirectory` (last candidate) to be found. Both scheduled tasks use identical `FindConfigFile()` logic.

**Config file concern:** The config's `OutputDirectory` field is a path. The test must write the config pointing to a temp dir, then run the task. Because `Run()` also resolves `Path.GetFullPath(config.OutputDirectory)`, relative paths work if the process cwd is predictable — safest to write an absolute path.

### Anti-Patterns to Avoid

- **Referencing `Dynamicweb.Core` alone:** Does NOT provide `Dynamicweb.Content.*`. Must reference `Dynamicweb` (which depends on `Dynamicweb.Core`).
- **Referencing `Dynamicweb.Scheduler`:** This is the scheduler runtime host, not the addin API. `BaseScheduledTaskAddIn` is in `Dynamicweb.Core`, not `Dynamicweb.Scheduler`.
- **Including DLL files in the NuGet package:** Forum guidance from DW staff is to declare NuGet dependencies, not bundle DLLs. DW will install what's missing.
- **Making `FindConfigFile()` public just for tests:** Instead, write a config file to the expected path during test setup.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| NuGet package generation | Custom build scripts | `<GeneratePackageOnBuild>true</GeneratePackageOnBuild>` in csproj | SDK auto-generates on build |
| Addin registration | Manual DI registration | DW reflection-based addin scanning | DW discovers addins automatically |
| Directory tree comparison | Custom recursive file compare | xunit assertion with byte array comparison (pattern already in `Serialize_CustomerCenter_Idempotent`) | Existing test already demonstrates the pattern |

**Key insight:** Phase 3 already solved the hard problems (DLL references, serialization pipeline). Phase 5 is assembly-line work: add metadata, add one log line, add two test classes.

---

## Common Pitfalls

### Pitfall 1: Wrong NuGet Package for DW APIs

**What goes wrong:** Referencing `Dynamicweb.Core` (which was already a DLL reference) as the NuGet upgrade path. `Dynamicweb.Core` does NOT include `Dynamicweb.Content.*`. Build succeeds (AddIns and Scheduling are in Core) but `Services.Pages`, `Services.Grids`, etc. are unresolved.

**Why it happens:** The Phase 3 note says "Dynamicweb.Core NuGet lacks Dynamicweb.Content namespace." This is true. The fix is to reference `Dynamicweb` (not `Dynamicweb.Core`), which ships `Dynamicweb.dll` and depends on `Dynamicweb.Core`.

**How to avoid:** Add `<PackageReference Include="Dynamicweb" Version="10.23.9" />`. Remove both DLL `<Reference>` entries. The `Dynamicweb` package transitively provides `Dynamicweb.Core`.

**Warning signs:** Build error: `The type or namespace name 'Content' does not exist in the namespace 'Dynamicweb'`

### Pitfall 2: NuGet Package Build — Missing Tags

**What goes wrong:** Package builds but doesn't appear in DW AppStore listing.

**Why it happens:** `dynamicweb-app-store` tag is mandatory for DW AppStore discovery. Missing it means the package won't be found.

**How to avoid:** Verify `<PackageTags>` contains `dynamicweb-app-store`. Additional required tags per official docs: `dw10`, `addin`. Category tag for this package: `task`.

**Warning signs:** Package publishes successfully to NuGet but doesn't appear under "Apps" in DW admin.

### Pitfall 3: E2E Test Config File Path

**What goes wrong:** `SerializeScheduledTask.Run()` returns `false` in tests with "ContentSync.config.json not found" log message.

**Why it happens:** `FindConfigFile()` searches 4 specific paths. Test temp directories are not in the search path. The config must be at one of: `{BaseDirectory}/../wwwroot/Files/`, `{CWD}/wwwroot/Files/`, `{BaseDirectory}/`, or `{CWD}/`.

**How to avoid:** Write the test config to `Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ContentSync.config.json")`. Use `IDisposable.Dispose()` to delete it after the test.

**Warning signs:** `Run()` returns `false`; log file contains "ERROR: ContentSync.config.json not found."

### Pitfall 4: Scheduled Task E2E — Shared Log File Contention

**What goes wrong:** Parallel test execution causes `File.AppendAllText(_logFile, ...)` in `SerializeScheduledTask` to throw `IOException` (file locked by another test).

**Why it happens:** Both tasks write to `ContentSync.log` in `AppDomain.CurrentDomain.BaseDirectory`. All tests run in the same process, so `BaseDirectory` is shared.

**How to avoid:** E2E tests should suppress task logging or be marked `[Collection("ScheduledTaskTests")]` (xunit sequential collection) to prevent parallel execution. The existing tasks use `try { ... } catch { /* swallow */ }` in `Log()`, so IOException is silently swallowed — parallel tests will work but may produce incomplete logs.

**Warning signs:** Intermittent test failures on CI (not reproducible locally with single-threaded run).

### Pitfall 5: Version Mismatch Between NuGet Package and Runtime DLLs

**What goes wrong:** Build succeeds with `Dynamicweb` 10.23.9 but the DW instance uses a different version, causing assembly binding failures at runtime.

**Why it happens:** The `Dynamicweb` NuGet version must match the DW instance version. The lib/ DLLs are 10.23.9.0, and `Dynamicweb.Suite` used by Swift test instances is pinned to `10.*` which resolves to 10.23.9.

**How to avoid:** Use `Version="10.23.9"` explicitly. Do NOT use `Version="10.*"` in a distributable library (only appropriate for host applications). If/when the DW instance upgrades, update the package reference.

**Warning signs:** `FileLoadException: Could not load assembly Dynamicweb, Version=10.x.x.x` at test runtime.

---

## Code Examples

### Complete csproj with NuGet Metadata

```xml
<!-- Source: Official DW AppStore docs (doc.dynamicweb.dev/documentation/extending/guides/newappstoreapp.html) -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>Dynamicweb.ContentSync</RootNamespace>
    <AssemblyName>Dynamicweb.ContentSync</AssemblyName>

    <PackageId>Dynamicweb.ContentSync</PackageId>
    <Version>0.1.0-beta</Version>
    <AssemblyVersion>0.1.0.0</AssemblyVersion>
    <Title>ContentSync for DynamicWeb</Title>
    <Description>Serialize and deserialize DynamicWeb content trees to YAML files for version control and environment sync.</Description>
    <Authors>...</Authors>
    <Copyright>...</Copyright>
    <PackageProjectUrl>https://github.com/...</PackageProjectUrl>
    <RepositoryUrl>https://github.com/...</RepositoryUrl>
    <PackageTags>dynamicweb-app-store task dw10 addin</PackageTags>
    <GeneratePackageOnBuild>true</GeneratePackageOnBuild>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Dynamicweb" Version="10.23.9" />
    <PackageReference Include="YamlDotNet" Version="13.7.1" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="8.0.1" />
  </ItemGroup>
</Project>
```

### Count Summary Enhancement to ContentSerializer

```csharp
// Source: ContentSerializer.cs pattern + SerializedPage/SerializedGridRow model
public void Serialize()
{
    int totalPages = 0, totalGridRows = 0, totalParagraphs = 0;

    foreach (var predicate in _configuration.Predicates)
    {
        var area = SerializePredicate(predicate);
        _referenceResolver.Clear();

        if (area != null)
            CountItems(area.Pages, ref totalPages, ref totalGridRows, ref totalParagraphs);
    }

    Log($"Serialization complete: {totalPages} pages, {totalGridRows} grid rows, {totalParagraphs} paragraphs serialized.");
}

private static void CountItems(IEnumerable<SerializedPage> pages, ref int pageCount, ref int gridRowCount, ref int paragraphCount)
{
    foreach (var page in pages)
    {
        pageCount++;
        gridRowCount += page.GridRows.Count;
        paragraphCount += page.GridRows.Sum(gr => gr.Columns.Sum(c => c.Paragraphs.Count));
        CountItems(page.Children, ref pageCount, ref gridRowCount, ref paragraphCount);
    }
}
```

### E2E Scheduled Task Test Setup

```csharp
// Source: derived from CustomerCenterSerializationTests.cs pattern
[Trait("Category", "Integration")]
public class ScheduledTaskEndToEndTests : IDisposable
{
    private readonly string _outputDir;
    private readonly string _configPath;

    public ScheduledTaskEndToEndTests()
    {
        _outputDir = Path.Combine(Path.GetTempPath(), "ContentSyncE2E_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_outputDir);

        // Write config to BaseDirectory so FindConfigFile() discovers it
        _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ContentSync.config.json");
        // Write valid config JSON with OutputDirectory = _outputDir
    }

    public void Dispose()
    {
        if (Directory.Exists(_outputDir)) Directory.Delete(_outputDir, recursive: true);
        if (File.Exists(_configPath)) File.Delete(_configPath);
    }
}
```

### Directory Tree Comparison Helper

```csharp
// Source: derived from Serialize_CustomerCenter_Idempotent test pattern
private static void AssertDirectoryTreesEqual(string dirA, string dirB)
{
    var filesA = Directory.EnumerateFiles(dirA, "*.yml", SearchOption.AllDirectories)
        .Select(f => Path.GetRelativePath(dirA, f)).OrderBy(f => f).ToList();
    var filesB = Directory.EnumerateFiles(dirB, "*.yml", SearchOption.AllDirectories)
        .Select(f => Path.GetRelativePath(dirB, f)).OrderBy(f => f).ToList();

    Assert.Equal(filesA, filesB);

    foreach (var rel in filesA)
    {
        var bytesA = File.ReadAllBytes(Path.Combine(dirA, rel));
        var bytesB = File.ReadAllBytes(Path.Combine(dirB, rel));
        Assert.True(bytesA.SequenceEqual(bytesB), $"File differs: {rel}");
    }
}
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| DLL references (`<Reference>` with HintPath) | NuGet package references (`<PackageReference>`) | Phase 5 (this phase) | Package is distributable; no lib/ folder needed by consumers |
| `Dynamicweb.Core` only | `Dynamicweb` (provides both Content and Core) | Phase 5 (this phase) | `Dynamicweb.Content.*` namespace available via NuGet |

**Deprecated/outdated:**
- DLL references in `Dynamicweb.ContentSync.csproj`: The Phase 3 workaround (copy DLLs from Swift2.1 bin) is replaced by NuGet package references in Phase 5.
- DLL references in `Dynamicweb.ContentSync.IntegrationTests.csproj`: Must also be updated to match — the test project references `Dynamicweb.dll` and `Dynamicweb.Core.dll` via DLL HintPath, which must also switch to `<PackageReference Include="Dynamicweb" Version="10.23.9" />`.

---

## Open Questions

1. **Integration test project NuGet upgrade**
   - What we know: Both `Dynamicweb.ContentSync.csproj` and `Dynamicweb.ContentSync.IntegrationTests.csproj` have DLL references to Dynamicweb.dll and Dynamicweb.Core.dll
   - What's unclear: The integration tests run against a live DW instance — the NuGet package reference will bring `Dynamicweb.dll` into the test output, but the DW host also provides `Dynamicweb.dll`. This may cause DLL version conflicts if the test runner loads its own copy vs. the host's copy.
   - Recommendation: Switch the test project to NuGet reference same as main project. Mark the reference as `<PrivateAssets>all</PrivateAssets>` and `<ExcludeAssets>runtime</ExcludeAssets>` so it's compile-only and the DW host's DLLs win at runtime. This is the standard pattern for DW addin development.

2. **`lib/` folder post-migration**
   - What we know: The `lib/` folder contains `Dynamicweb.dll` and `Dynamicweb.Core.dll` copied from Swift2.1 bin
   - What's unclear: Whether to delete the `lib/` folder after switching to NuGet
   - Recommendation: Keep the `lib/` folder for now (useful as reference for DLL version verification) but `.gitignore` it. After NuGet migration is confirmed working, it can be cleaned up in a follow-on commit.

3. **`Microsoft.Extensions.Configuration.Json` NuGet source**
   - What we know: This package is already referenced and works fine with DLL references
   - What's unclear: Whether `Dynamicweb` NuGet transitively includes `Microsoft.Extensions.Configuration.Json`, making the explicit reference redundant
   - Recommendation: Keep the explicit reference for clarity — if it's transitively available, NuGet will resolve to the same version; if not, the explicit reference ensures it's available.

---

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit 2.9.3 |
| Config file | none in repo — tests run via `dotnet test` with `--filter "Category=Integration"` |
| Quick run command | `dotnet test tests/Dynamicweb.ContentSync.IntegrationTests/ --filter "Category=Integration&FullyQualifiedName~ScheduledTask"` |
| Full suite command | `dotnet test tests/Dynamicweb.ContentSync.IntegrationTests/ --filter "Category=Integration"` |

**Important prerequisite:** Integration tests require a running DW instance (Swift2.1 for serialize, Swift2.2 for deserialize). They cannot run on a developer workstation without a live DW host. This is unchanged from Phases 3-4.

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| OPS-01 | `SerializeScheduledTask.Run()` produces YAML tree | integration | `dotnet test ... --filter "Category=Integration&FullyQualifiedName~SerializeScheduledTask"` | ❌ Wave 0 |
| OPS-01 | Scheduled task output is byte-identical to `ContentSerializer` direct call | integration | same | ❌ Wave 0 |
| OPS-02 | `DeserializeScheduledTask.Run()` completes without errors | integration | `dotnet test ... --filter "Category=Integration&FullyQualifiedName~DeserializeScheduledTask"` | ❌ Wave 0 |
| OPS-03 | Count summary log line present after serialization | integration | included in OPS-01 tests | ❌ Wave 0 |
| INF-01 | NuGet package builds (`.nupkg` generated) | smoke | `dotnet build src/Dynamicweb.ContentSync/ -c Release` | n/a — build check |

### Sampling Rate
- **Per task commit:** `dotnet build src/Dynamicweb.ContentSync/ -c Release` (verify package generates)
- **Per wave merge:** Full integration test suite against live DW instances
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `tests/Dynamicweb.ContentSync.IntegrationTests/ScheduledTasks/SerializeScheduledTaskTests.cs` — OPS-01 E2E tests
- [ ] `tests/Dynamicweb.ContentSync.IntegrationTests/ScheduledTasks/DeserializeScheduledTaskTests.cs` — OPS-02 E2E tests

*(Framework and project infrastructure already exist — only new test files needed)*

---

## Sources

### Primary (HIGH confidence)
- `C:\VibeCode\Dynamicweb.ContentSync\lib\Dynamicweb.dll` — file version 10.23.9.0, confirmed via `FileVersionInfo`
- `C:\VibeCode\Dynamicweb.ContentSync\lib\Dynamicweb.Core.dll` — file version 10.23.9.0
- `C:\Projects\Solutions\swift.test.forsync\Swift2.1\Dynamicweb.Host.Suite\bin\Debug\net8.0\Dynamicweb.Core.xml` — namespace proof: `Dynamicweb.Scheduling.BaseScheduledTaskAddIn`, `Dynamicweb.Extensibility.AddIns.AddInNameAttribute` etc. are in `Dynamicweb.Core.dll`
- `C:\Projects\Solutions\swift.test.forsync\Swift2.1\Dynamicweb.Host.Suite\bin\Debug\net8.0\Dynamicweb.xml` — namespace proof: `Dynamicweb.Content.*` (PageService, AreaService) is in `Dynamicweb.dll`
- `C:\Projects\Solutions\swift.test.forsync\Swift2.1\Dynamicweb.Host.Suite\bin\Debug\net8.0\Dynamicweb.Host.Suite.deps.json` — package list: `Dynamicweb/10.23.9`, `Dynamicweb.Core/10.23.9`, `Dynamicweb.Scheduler/10.23.9` are separate packages all at 10.23.9
- [https://www.nuget.org/packages/Dynamicweb/](https://www.nuget.org/packages/Dynamicweb/) — latest stable: 10.23.9 (released 2026-03-17); depends on `Dynamicweb.Core >= 10.23.9`
- [https://doc.dynamicweb.dev/documentation/extending/guides/newappstoreapp.html](https://doc.dynamicweb.dev/documentation/extending/guides/newappstoreapp.html) — required NuGet tags, required csproj properties, `GeneratePackageOnBuild`, `dynamicweb-app-store` tag mandatory

### Secondary (MEDIUM confidence)
- [https://doc.dynamicweb.dev/documentation/extending/extensibilitypoints/scheduled-task-addins.html](https://doc.dynamicweb.dev/documentation/extending/extensibilitypoints/scheduled-task-addins.html) — confirmed: no additional wiring beyond attributes + base class inheritance; addins discovered automatically
- WebSearch results for DW AppStore tags — confirm `dw10`, `addin`, `task` as standard categorization tags used in DW ecosystem

### Tertiary (LOW confidence)
- None — all critical claims verified against source DLLs and official docs

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — verified directly against source DLLs (XML docs) and NuGet.org version data
- Architecture: HIGH — existing scheduled task code is the reference; patterns from official DW docs
- Pitfalls: HIGH — DLL/namespace confusion is verified empirically; config path issue derived from reading actual `FindConfigFile()` code

**Research date:** 2026-03-19
**Valid until:** 2026-06-19 (90 days — DW 10.x versioning is stable; NuGet tags are static)
