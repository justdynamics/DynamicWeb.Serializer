# Stack Research

**Domain:** DynamicWeb AppStore app — content serialization/sync tooling
**Researched:** 2026-03-19
**Confidence:** MEDIUM-HIGH (DynamicWeb APIs verified via official docs; versions verified via NuGet.org)

---

## Recommended Stack

### Core Technologies

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| .NET 8.0 | 8.0 LTS | Target framework | Required by DynamicWeb 10.2+ per official docs. LTS release with long support window. |
| Dynamicweb.Core | 10.23.9 | DW platform APIs (logging, DB, services) | The root package for all DynamicWeb functionality. Published 2026-03-17. Targets net8.0. Referenced by all other DW packages. |
| YamlDotNet | 16.3.0 | YAML serialization/deserialization | The dominant .NET YAML library with 43.5M+ downloads. Latest stable release (Dec 2024). Supports net8.0, net6.0, netstandard2.0/2.1. No meaningful competition in the .NET ecosystem. |

### DynamicWeb Content APIs

These APIs live in `Dynamicweb.dll` (included transitively through `Dynamicweb.Core`) and are the canonical way to read/write content in DynamicWeb 10.

| Service/Class | Namespace | Purpose | Key Methods |
|---------------|-----------|---------|-------------|
| `AreaService` | `Dynamicweb.Content` | Read website areas | `GetArea(id)`, `GetAreas()`, `GetMasterAreas()` |
| `PageService` | `Dynamicweb.Content` | Read and write pages | `GetPage(id)`, `GetPagesByAreaID(id)`, `GetPagesByParentID(id)` |
| `GridService` | `Dynamicweb.Content` | Read grid rows | `GetGridRowsByPageId(int)`, `GetGridRowsByPageId(int, bool)`, `GetGridRowById(int)` |
| `Paragraphs` (static) | `Dynamicweb.Content.Services` | Read paragraphs | `GetParagraphsByPageId(pageId)` — returns active paragraphs for a page |
| `BaseScheduledTaskAddIn` | `Dynamicweb.Scheduling` | Scheduled task base class | Override `Run()` for task execution logic |
| `LogManager` | `Dynamicweb.Logging` | DW-native logging | `System.AddLog()`, `Instance` singleton |

**Note on `GetParagraphsByPageId`:** Per forum documentation, this method only returns *active* paragraphs by default. For serialization, we need all paragraphs regardless of active state — investigate whether the `bool` overload of GridService's methods exposes this, and verify at implementation time.

### Scheduled Task Addin Pattern

```csharp
[AddInName("ContentSync.SerializeTask")]
[AddInLabel("ContentSync: Serialize Content Trees")]
[AddInDescription("Serializes configured content trees to YAML files on disk")]
public class SerializeScheduledTask : BaseScheduledTaskAddIn
{
    public override void Run()
    {
        // Serialization logic here
    }
}
```

The `Run()` method (not `Execute()`) is the override point — confirmed via DynamicWeb API docs.

### Supporting Libraries

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `Microsoft.Extensions.Configuration.Json` | 8.0.x | Load standalone JSON/config files | Loading the ContentSync predicate config file from disk at task startup |
| `Microsoft.Extensions.Logging.Abstractions` | 8.0.x | `ILogger<T>` abstraction | Bridge between DW's `LogManager` and standard .NET logging patterns; use for testability |
| `xunit` | 2.9.3 (v2) or 3.2.2 (v3) | Unit testing | All unit tests. xUnit is the dominant .NET test framework. v3 is the future but v2 is stable and current. |
| `xunit.runner.visualstudio` | 3.1.5 | Visual Studio/CLI test runner | Required to run xUnit tests in VS/dotnet test |
| `Moq` | 4.x | Mocking for unit tests | Mock DW service interfaces in tests where DW database is unavailable |

### Development Tools

| Tool | Purpose | Notes |
|------|---------|-------|
| Visual Studio 2022 / Rider | IDE | Both work. VS has better NuGet tooling for AppStore package metadata. |
| `dotnet pack` | Build NuGet package | Set `<GeneratePackageOnBuild>true</GeneratePackageOnBuild>` in csproj for CI |
| DynamicWeb Visual Studio Template | Scaffold AddinName/BaseScheduledTaskAddIn boilerplate | Optional but speeds up setup; available from DW developer resources |

---

## Installation

```xml
<!-- ContentSync.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Version>1.0.0</Version>
    <Title>Dynamicweb.ContentSync</Title>
    <Description>Serialize and deserialize DynamicWeb content trees to YAML files for source-controlled deployments.</Description>
    <Authors>YourName</Authors>
    <PackageTags>dynamicweb-app-store;dw10;addin;task</PackageTags>
    <GeneratePackageOnBuild>true</GeneratePackageOnBuild>
  </PropertyGroup>

  <ItemGroup>
    <!-- Core DW platform -->
    <PackageReference Include="Dynamicweb.Core" Version="10.23.9" />

    <!-- YAML serialization -->
    <PackageReference Include="YamlDotNet" Version="16.3.0" />

    <!-- Config file loading -->
    <PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="8.0.1" />
  </ItemGroup>
</Project>
```

```xml
<!-- ContentSync.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.5" />
    <PackageReference Include="Moq" Version="4.20.72" />
    <ProjectReference Include="../ContentSync/ContentSync.csproj" />
  </ItemGroup>
</Project>
```

---

## Alternatives Considered

| Recommended | Alternative | When to Use Alternative |
|-------------|-------------|-------------------------|
| YamlDotNet 16.3.0 | SharpYaml | Never — SharpYaml is unmaintained (last release 2018). YamlDotNet has active maintenance and dominates the .NET ecosystem. |
| YamlDotNet 16.3.0 | System.Text.Json (JSON) | If the team later decides JSON is preferable. JSON is more tooling-friendly but produces worse diffs and is more verbose. YAML is the right call for human-readable content. |
| YamlDotNet 16.3.0 | Newtonsoft.Json (JSON) | Same reasoning as System.Text.Json — JSON is an alternative format, not a YAML alternative. |
| `Microsoft.Extensions.Configuration.Json` | Custom config parser | Only if DynamicWeb already injects `IConfiguration` into the addin context — verify at implementation. If DW provides it, don't add the dep. |
| xunit v2 (2.9.3) | xunit v3 (3.2.2) | Use v3 if starting new today and want forward compatibility. v2 is still actively maintained for security fixes. v3 has breaking API changes. Either works. |
| xunit v2 (2.9.3) | MSTest | MSTest is Microsoft-owned and fine, but xUnit is the dominant community choice for .NET libraries. NUnit is also acceptable. |
| `BaseScheduledTaskAddIn` | Custom background service | Never for AppStore apps — DW's scheduled task system is the correct hook point. Background services bypass DW task management UI. |

---

## What NOT to Use

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| SharpYaml | Last release 2018, unmaintained, not compatible with modern .NET targets | YamlDotNet 16.3.0 |
| `Dynamicweb.Ecommerce` | Commerce-specific package, irrelevant to content sync; adds significant unnecessary dependency weight | `Dynamicweb.Core` only — it contains the content APIs |
| `JsonSerializer` / Newtonsoft as primary format | The project decision is YAML — don't drift to JSON. YAML's readability and diff quality are the entire reason for the choice. | YamlDotNet |
| Numeric page IDs as canonical identity | Numeric IDs are environment-specific. They will conflict across DW instances. | PageUniqueId (GUID) as the canonical cross-environment identifier — match on GUID, assign new numeric ID on insert |
| DynamicWeb Notifications API for v1 sync | Adds significant complexity (real-time change detection). Deferred to v2 per project scope. | Scheduled tasks via `BaseScheduledTaskAddIn` |
| Direct SQL queries against DW database | Bypasses DW's caching, lifecycle hooks, and business logic. Also couples to DB schema which can change on DW upgrades. | `AreaService`, `PageService`, `GridService`, `Paragraphs` service APIs |

---

## Stack Patterns by Variant

**For config file format (predicates/settings):**
- Use JSON (`.json`) loaded via `Microsoft.Extensions.Configuration.Json`
- YAML is the serialization *output* format; JSON is better for machine-written/consumed config due to stricter syntax and no indentation ambiguity
- Config file lives in source control alongside the YAML content files

**For YAML serialization of DW content objects:**
- Do NOT serialize DW model objects directly (e.g., `Page`, `Paragraph`) — they contain internal state, lazy-loaded collections, and database references
- Create explicit DTO/record types (`PageRecord`, `ParagraphRecord`, etc.) that contain only serializable fields
- Use YamlDotNet's `SerializerBuilder` / `DeserializerBuilder` with explicit naming conventions

**For the predicate system (which content trees to include):**
- Model after Sitecore Unicorn's `SerializationPresetPredicate`: a list of include roots with optional exclude sub-paths
- Store as JSON in config file; load at task startup
- Match by Area ID or Page path, not numeric page ID (GUIDs only in YAML output)

---

## Version Compatibility

| Package | Compatible With | Notes |
|---------|-----------------|-------|
| Dynamicweb.Core 10.23.9 | net8.0 | Published 2026-03-17. Minimum DW version: 10.2+. Do not use versions below 10.2 — APIs may differ. |
| YamlDotNet 16.3.0 | net8.0, net6.0, netstandard2.0/2.1, net47 | Published Dec 2024. No known compatibility issues with Dynamicweb.Core. |
| Microsoft.Extensions.Configuration.Json 8.0.x | net8.0 | Part of .NET 8 SDK. Version should match the target framework. |
| xunit 2.9.3 | net8.0 | Test project only — never include xunit in the AppStore package output. |

---

## Sources

- [NuGet: Dynamicweb.Core 10.23.9](https://www.nuget.org/packages/Dynamicweb.Core/) — verified version and target framework (HIGH confidence)
- [NuGet: YamlDotNet 16.3.0](https://www.nuget.org/packages/YamlDotNet) — verified version, download count, target frameworks (HIGH confidence)
- [DynamicWeb 10 AppStore App Guide](https://doc.dynamicweb.dev/documentation/extending/guides/newappstoreapp.html) — project structure, required NuGet tags, csproj metadata (HIGH confidence)
- [DynamicWeb: AreaService Class](https://doc.dynamicweb.com/api/html/02c7da84-1d1c-506d-0054-da04eaff373f.htm) — namespace `Dynamicweb.Content`, key methods (HIGH confidence)
- [DynamicWeb: PageService Class](https://doc.dynamicweb.com/api/html/15516fc9-3e1c-ac41-9849-cc6ad67bb84d.htm) — namespace `Dynamicweb.Content`, key methods (HIGH confidence)
- [DynamicWeb: GridService Class](https://doc.dynamicweb.dev/api/Dynamicweb.Content.GridService.html) — namespace `Dynamicweb.Content`, `GetGridRowsByPageId` methods (HIGH confidence)
- [DynamicWeb: BaseScheduledTaskAddIn Fields](https://doc.dynamicweb.com/api/html/75745460-c471-a370-1ddc-e4a3ae983f14.htm) — namespace `Dynamicweb.Scheduling`, `Run()` method (HIGH confidence)
- [DynamicWeb: Paragraphs.GetParagraphsByPageId](https://doc.dynamicweb.com/forum/development/development/) — via forum example, `Dynamicweb.Content.Services.Paragraphs` (MEDIUM confidence — forum source, not official API docs)
- [DynamicWeb: Logging Namespace](http://doc.dynamicweb.com/api/html/e09d5412-29c9-78fa-bfa3-e7ac54caaee2.htm) — `Dynamicweb.Logging`, `LogManager` class (MEDIUM confidence)
- [NuGet: xunit 2.9.3](https://www.nuget.org/packages/xunit) — verified version (HIGH confidence)
- [Sitecore Unicorn GitHub](https://github.com/SitecoreUnicorn/Unicorn) — predicate configuration pattern reference (HIGH confidence — well-established prior art)

---

*Stack research for: Dynamicweb.ContentSync — DynamicWeb AppStore content serialization tool*
*Researched: 2026-03-19*
