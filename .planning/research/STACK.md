# Technology Stack

**Project:** Dynamicweb.ContentSync v1.2 Admin UI
**Researched:** 2026-03-21 (updated from 2026-03-19 original)
**Scope:** Stack additions for admin UI integration, query configuration, context menu actions, zip packaging

---

## Current Stack (v1.0/v1.1 -- Validated, DO NOT CHANGE)

| Technology | Version | Purpose |
|------------|---------|---------|
| .NET | 8.0 | Runtime target |
| Dynamicweb | 10.23.9 | Core DW APIs (Content, Scheduling, Extensibility) |
| YamlDotNet | 13.7.1 | YAML serialization/deserialization |
| Microsoft.Extensions.Configuration.Json | 8.0.1 | Config file reading |

---

## Required Stack Addition for v1.2

### Single Package Addition: Dynamicweb.Content.UI

| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| Dynamicweb.Content.UI | 10.23.9 | Admin UI screens, content tree injection, page screen types | Provides `PageEditScreen`, `PageListScreen`, content tree `NavigationSection` for context menu injection. Transitively brings in `Dynamicweb.Application.UI` (settings area/sections, `ActionBuilder`), `Dynamicweb.CoreUI` (screen bases, commands, queries, actions), and `Dynamicweb.QueryPublisher`. |

**Confidence:** HIGH -- verified via NuGet dependency chain AND assembly reflection on test instance DLLs at `C:\Projects\Solutions\swift.test.forsync\Swift2.1\Dynamicweb.Host.Suite\bin\Debug\net8.0\`.

### Transitive Dependency Chain

Adding `Dynamicweb.Content.UI` brings the full admin UI dependency chain:

```
Dynamicweb.Content.UI (10.23.9)
  +-- Dynamicweb.Application.UI (10.23.9)     -- AreasSection, SettingsArea, ActionBuilder
  |     +-- Dynamicweb.CoreUI (10.23.9)       -- EditScreenBase, ListScreenBase, ScreenInjector,
  |     |                                        CommandBase, DataQueryModelBase, NavigationNodeProvider,
  |     |                                        DownloadFileAction, RunCommandAction, FileResult
  |     +-- Dynamicweb (10.23.9)              -- Already have this
  |     +-- Dynamicweb.DataIntegration         -- Transitive, not directly used
  |     +-- Dynamicweb.Forms                   -- Transitive, not directly used
  |     +-- Dynamicweb.Marketplace             -- Transitive, not directly used
  +-- Dynamicweb.CoreUI (10.23.9)             -- (same as above, deduplicated)
  +-- Dynamicweb.Files.UI (10.23.9)           -- Transitive, not directly used
  +-- Dynamicweb.QueryPublisher (10.23.9)     -- Query expression infrastructure
```

### No Other NuGet Packages Needed

| Capability | Provided By | Notes |
|------------|-------------|-------|
| Settings edit screen | `Dynamicweb.CoreUI` (transitive) | `EditScreenBase<T>` |
| Predicate list screen | `Dynamicweb.CoreUI` (transitive) | `ListScreenBase<T>` |
| Tree navigation node | `Dynamicweb.CoreUI` (transitive) | `NavigationNodeProvider<T>` |
| Screen injection | `Dynamicweb.CoreUI` (transitive) | `ScreenInjector<T>`, `ListScreenInjector<TScreen,TModel>` |
| Settings area sections | `Dynamicweb.Application.UI` (transitive) | `AreasSection`, `SettingsArea` |
| ActionBuilder helper | `Dynamicweb.Application.UI` (transitive) | `ActionBuilder` for Edit/Delete actions |
| Content tree page screens | `Dynamicweb.Content.UI` (direct) | `PageEditScreen`, `PageListScreen` to inject into |
| Download file action | `Dynamicweb.CoreUI` (transitive) | `DownloadFileAction` for zip download |
| Run command action | `Dynamicweb.CoreUI` (transitive) | `RunCommandAction` for serialize/deserialize |
| Confirm action dialog | `Dynamicweb.CoreUI` (transitive) | `ConfirmAction` for destructive operations |
| Open dialog action | `Dynamicweb.CoreUI` (transitive) | `OpenDialogAction` for upload prompts |
| File upload editor | `Dynamicweb.CoreUI` (transitive) | `FileUpload` input editor |
| File result for downloads | `Dynamicweb.CoreUI` (transitive) | `FileResult` return type from commands |
| Data model base | `Dynamicweb.CoreUI` (transitive) | `DataViewModelBase` |
| Mapping configuration | `Dynamicweb` (existing) | `MappingConfigurationBase` |
| ZIP packaging | .NET 8.0 BCL | `System.IO.Compression.ZipFile` -- built-in |

---

## Key Namespaces and Classes by Feature

### Settings Screen (Settings > Content > Sync)

| Class | Namespace | Purpose |
|-------|-----------|---------|
| `EditScreenBase<T>` | `Dynamicweb.CoreUI.Screens` | Base class for settings edit form |
| `DataViewModelBase` | `Dynamicweb.CoreUI.Data` | Base class for screen data models |
| `CommandBase<T>` | `Dynamicweb.CoreUI.Data` | Base class for save command |
| `DataQueryModelBase<T>` | `Dynamicweb.CoreUI.Data` | Base class for loading settings data |
| `[ConfigurableProperty]` | `Dynamicweb.CoreUI.Data` | Attribute for editable model fields |
| `IIdentifiable` | `Dynamicweb.CoreUI.Data` | Interface for models with identity |

### Tree Navigation (Settings > Content > Sync node)

| Class | Namespace | Purpose |
|-------|-----------|---------|
| `NavigationNodeProvider<T>` | `Dynamicweb.CoreUI.Navigation` | Add "Sync" node to Settings > Content section |
| `NavigationNodePathProvider<T>` | `Dynamicweb.CoreUI.Navigation` | Breadcrumb/highlight tracking for current node |
| `NavigationNode` | `Dynamicweb.CoreUI.Navigation` | Individual tree node definition |
| `NavigationNodePath` | `Dynamicweb.CoreUI.Navigation` | Node path for identification |
| `NavigateScreenAction` | `Dynamicweb.CoreUI.Actions.Implementations` | Navigate to screen on node click |
| `AreasSection` | `Dynamicweb.Application.UI` | Parent section type parameter for `NavigationNodeProvider<AreasSection>` |
| `SettingsArea` | `Dynamicweb.Application.UI` | Settings area reference for node path |

### Content Tree Context Menu Actions (Serialize/Deserialize)

| Class | Namespace | Purpose |
|-------|-----------|---------|
| `ScreenInjector<T>` | `Dynamicweb.CoreUI.Screens` | Inject actions into existing page screens |
| `ListScreenInjector<TScreen,TModel>` | `Dynamicweb.CoreUI.Screens` | Inject into list screen context menus |
| `ActionNode` | `Dynamicweb.CoreUI.Actions` | Context menu item definition |
| `ActionGroup` | `Dynamicweb.CoreUI.Actions` | Group of context menu items |
| `RunCommandAction` | `Dynamicweb.CoreUI.Actions.Implementations` | Execute serialize/deserialize command |
| `ConfirmAction` | `Dynamicweb.CoreUI.Actions.Implementations` | Confirmation before destructive operations |
| `DownloadFileAction` | `Dynamicweb.CoreUI.Actions.Implementations` | Trigger browser file download for zip |
| `OpenDialogAction` | `Dynamicweb.CoreUI.Actions.Implementations` | Open upload dialog for deserialize zip |
| `FileResult` | `Dynamicweb.CoreUI.Data` | Return type for file download commands |
| `FileUpload` | `Dynamicweb.CoreUI.Editors.Inputs` | File upload widget for zip import |
| `Icon` | `Dynamicweb.CoreUI.Icons` | Icon enum for context menu items |
| `PageListScreen` | `Dynamicweb.Content.UI.Screens` | Content tree list screen to inject into |
| `PageEditScreen` | `Dynamicweb.Content.UI.Screens` | Page editor screen to inject into |
| `ActionBuilder` | `Dynamicweb.Application.UI.Helpers` | Helper for building edit/delete actions |

### Data Mapping

| Class | Namespace | Purpose |
|-------|-----------|---------|
| `MappingConfigurationBase` | `Dynamicweb.Extensibility.Mapping` | Auto-mapping between domain/view models |
| `MappingService` | `Dynamicweb.Extensibility.Mapping` | Execute mappings at runtime |

---

## Project File Change

### Exact Diff

```diff
  <ItemGroup>
    <PackageReference Include="Dynamicweb" Version="10.23.9" />
+   <PackageReference Include="Dynamicweb.Content.UI" Version="10.23.9" />
    <PackageReference Include="YamlDotNet" Version="13.7.1" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="8.0.1" />
  </ItemGroup>
```

**One line added.** `Dynamicweb.Content.UI` at `10.23.9` to match the existing `Dynamicweb` version pin.

### SDK Type: Keep `Microsoft.NET.Sdk` (NOT Razor SDK)

The ExpressDelivery sample uses `Microsoft.NET.Sdk.Razor` because it ships custom Razor widget views with embedded `wwwroot` resources. ContentSync admin UI uses only standard `EditScreenBase`/`ListScreenBase` screens rendered by DW's built-in CoreUI rendering pipeline. No custom Razor views needed.

---

## What NOT to Add

| Do NOT Add | Why Not | What We Use Instead |
|------------|---------|---------------------|
| `Microsoft.NET.Sdk.Razor` (SDK change) | Only needed for custom widget rendering with embedded Razor views. Our screens are declarative C# | `Microsoft.NET.Sdk` (existing) |
| `IRenderingBundle` marker class | Only needed when shipping custom Razor components with embedded wwwroot | Not needed -- no custom rendering |
| `Microsoft.AspNetCore.Components.Web` | Blazor/Razor component library, not needed for admin screens | Standard CoreUI screen builder |
| `Microsoft.Extensions.FileProviders.Embedded` | Only for embedded static files (CSS, JS, images) | No embedded assets needed |
| `FrameworkReference Microsoft.AspNetCore.App` | Only for apps that host ASP.NET; we're a library loaded into DW host | Already loaded by DW host process |
| `GenerateEmbeddedFilesManifest` / `EmbeddedResource` | Only for Razor SDK embedded content | No embedded content |
| `Dynamicweb.Suite.Ring1` | Meta-package pulling 20+ packages (Ecommerce.UI, Products.UI, Insights.UI, etc.) -- massive overkill for a content-only tool | `Dynamicweb.Content.UI` (single targeted package) |
| `Dynamicweb.CoreUI` (direct reference) | Missing `PageListScreen`/`PageEditScreen` for context menu injection and `AreasSection`/`SettingsArea` for settings tree | `Dynamicweb.Content.UI` brings CoreUI transitively |
| `SharpZipLib` / `DotNetZip` | External ZIP libraries. .NET 8 BCL `System.IO.Compression.ZipFile` handles all our needs | `System.IO.Compression.ZipFile` (built-in) |

---

## Alternatives Considered

| Category | Recommended | Alternative | Why Not |
|----------|-------------|-------------|---------|
| UI package | `Dynamicweb.Content.UI` | `Dynamicweb.Suite.Ring1` | Ring1 transitively pulls 20+ packages including Ecommerce, Products, Insights, Integration etc. ContentSync only touches content -- no reason to depend on the full suite. |
| UI package | `Dynamicweb.Content.UI` | `Dynamicweb.CoreUI` (direct) | CoreUI alone lacks `PageListScreen`/`PageEditScreen` types needed for content tree context menu injection. Also misses `AreasSection`/`SettingsArea` from Application.UI. |
| ZIP library | `System.IO.Compression` (BCL) | SharpZipLib, DotNetZip | Built-in .NET 8 `ZipFile`/`ZipArchive` classes handle create-from-directory and extract. Zero additional dependency. |
| SDK type | `Microsoft.NET.Sdk` | `Microsoft.NET.Sdk.Razor` | No custom Razor views. All screens use DW's declarative C# screen builder pattern. |

---

## Version Pinning Rationale

All DW packages pinned to exact `10.23.9` because:

1. **Consistency** -- matches existing `Dynamicweb` pin; prevents diamond dependency version conflicts
2. **Test environment match** -- Swift 2.1/2.2 test instances run `10.23.9` (verified via `deps.json`)
3. **Reproducible builds** -- floating versions (`*`) cause "works on my machine" issues
4. **DW coupling** -- DW packages at mismatched versions can have breaking internal API changes

The ExpressDelivery sample uses `Version="*"` but that is a sample convention, not a production practice.

---

## Installation

```bash
cd src/Dynamicweb.ContentSync
dotnet add package Dynamicweb.Content.UI --version 10.23.9
```

---

## Sources

- [NuGet: Dynamicweb.Content.UI](https://www.nuget.org/packages/Dynamicweb.Content.UI/) -- dependency chain verified (HIGH)
- [NuGet: Dynamicweb.Application.UI](https://www.nuget.org/packages/Dynamicweb.Application.UI/) -- AreasSection, SettingsArea, ActionBuilder confirmed (HIGH)
- [NuGet: Dynamicweb.CoreUI](https://www.nuget.org/packages/Dynamicweb.CoreUI/) -- screen/command/query base classes (HIGH)
- [NuGet: Dynamicweb.Suite.Ring1](https://www.nuget.org/packages/Dynamicweb.Suite.Ring1/) -- evaluated and rejected as too heavy (HIGH)
- [NuGet: Dynamicweb.Suite](https://www.nuget.org/packages/Dynamicweb.Suite/10.23.9) -- full dependency list inspected (HIGH)
- [DW10 AppStore App Guide](https://doc.dynamicweb.dev/documentation/extending/guides/newappstoreapp.html) -- extension patterns (HIGH)
- [DW10 Screen Types](https://doc.dynamicweb.dev/documentation/extending/administration-ui/screentypes.html) -- UI concepts (MEDIUM)
- Assembly reflection on `Dynamicweb.Content.UI.dll`, `Dynamicweb.CoreUI.dll`, `Dynamicweb.Application.UI.dll` from test instance at `C:\Projects\Solutions\swift.test.forsync\Swift2.1\Dynamicweb.Host.Suite\bin\Debug\net8.0\` (HIGH)
- ExpressDelivery sample at `C:\Projects\temp\dwextensionsample\Samples-main\ExpressDelivery\` -- verified patterns for NavigationNodeProvider, EditScreenBase, ListScreenBase, ScreenInjector, Commands, Queries, MappingConfiguration (HIGH)

---

## Confidence Assessment

| Area | Confidence | Rationale |
|------|------------|-----------|
| Package dependency (Content.UI) | HIGH | Verified NuGet dependency page + assembly inspection of test instance |
| Transitive chain | HIGH | Verified via NuGet dependency pages for each package in chain |
| Class/namespace locations | HIGH | Verified via .NET reflection on actual DLLs from test instance |
| No Razor SDK needed | HIGH | ExpressDelivery only uses Razor for custom widget rendering; our screens are declarative C# |
| ZIP via BCL | HIGH | `System.IO.Compression.ZipFile` is .NET 8 built-in |
| Version pinning at 10.23.9 | HIGH | Matches existing Dynamicweb pin and test instance versions (deps.json) |
| ScreenInjector for context menus | MEDIUM | Pattern confirmed from ExpressDelivery `OrderOverviewInjector`; applying to `PageListScreen` uses same pattern but needs runtime verification |
| Query expression UI reuse | LOW | `Dynamicweb.QueryPublisher` comes transitively; specific query expression editor reuse patterns need investigation during implementation |

---

*Stack research for: Dynamicweb.ContentSync v1.2 Admin UI milestone*
*Updated: 2026-03-21*
