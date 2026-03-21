---
phase: 07-config-infrastructure-settings-tree-node
plan: 02
subsystem: admin-ui
tags: [razor-sdk, tree-node, edit-screen, coreui, navigation]
dependency_graph:
  requires: [07-01]
  provides: [admin-ui-infrastructure, sync-tree-node, settings-edit-screen]
  affects: [08-settings-fields, 09-predicates]
tech_stack:
  added: [Dynamicweb.Content.UI 10.23.9, Dynamicweb.CoreUI.Rendering 10.23.9, Microsoft.Extensions.FileProviders.Embedded 8.0.15, Microsoft.NET.Sdk.Razor]
  patterns: [NavigationNodeProvider, EditScreenBase, DataViewModelBase, CommandBase, DataQueryModelBase, NavigationNodePathProvider, IRenderingBundle]
key_files:
  created:
    - src/Dynamicweb.ContentSync/Infrastructure/RenderingBundle.cs
    - src/Dynamicweb.ContentSync/AdminUI/Models/SyncSettingsModel.cs
    - src/Dynamicweb.ContentSync/AdminUI/Queries/SyncSettingsQuery.cs
    - src/Dynamicweb.ContentSync/AdminUI/Commands/SaveSyncSettingsCommand.cs
    - src/Dynamicweb.ContentSync/AdminUI/Screens/SyncSettingsEditScreen.cs
    - src/Dynamicweb.ContentSync/AdminUI/Tree/SyncSettingsNodeProvider.cs
    - src/Dynamicweb.ContentSync/AdminUI/Tree/SyncNavigationNodePathProvider.cs
  modified:
    - src/Dynamicweb.ContentSync/Dynamicweb.ContentSync.csproj
decisions:
  - "Used Microsoft.Extensions.FileProviders.Embedded 8.0.15 (not 8.0.8) to match transitive dependency from Dynamicweb.CoreUI.Rendering"
metrics:
  duration: 3min
  completed: "2026-03-21"
  tasks_completed: 2
  tasks_total: 3
  status: checkpoint-pending
---

# Phase 07 Plan 02: Admin UI Tree Node and Settings Screen Summary

Razor SDK project infrastructure with Sync navigation node under Settings > Content and skeleton edit screen reading config fresh from disk via ConfigPathResolver + ConfigLoader.

## What Was Done

### Task 1: Update csproj to Razor SDK and add NuGet references (3d27430)
- Switched project SDK from `Microsoft.NET.Sdk` to `Microsoft.NET.Sdk.Razor`
- Added `AddRazorSupportForMvc` and `GenerateEmbeddedFilesManifest` properties
- Added `FrameworkReference` for `Microsoft.AspNetCore.App`
- Added `EmbeddedResource` glob for `wwwroot/**/*`
- Added NuGet packages: `Dynamicweb.Content.UI 10.23.9`, `Dynamicweb.CoreUI.Rendering 10.23.9`, `Microsoft.Extensions.FileProviders.Embedded 8.0.15`
- Created `RenderingBundle.cs` marker class implementing `IRenderingBundle` for DW assembly scanning

### Task 2: Create admin UI tree node, screen skeleton, and supporting classes (a9abbe9)
- **SyncSettingsModel**: `DataViewModelBase` with `OutputDirectory` and `LogLevel` configurable properties
- **SyncSettingsQuery**: Reads config fresh from disk each load via `ConfigPathResolver.FindConfigFile()` + `ConfigLoader.Load()` (no caching per D-10)
- **SaveSyncSettingsCommand**: Writes settings back via `ConfigWriter.Save()`, preserving existing `Predicates` array
- **SyncSettingsEditScreen**: `EditScreenBase<SyncSettingsModel>` with "Content Sync" settings tab
- **SyncSettingsNodeProvider**: Injects "Sync" node under `Content_Settings` (Sort=100) with `Predicates` sub-node placeholder
- **SyncNavigationNodePathProvider**: Breadcrumb path through SettingsArea > AreasSection > Content_Settings > ContentSync_Settings

### Task 3: Verify Sync node appears in DW admin tree (CHECKPOINT PENDING)
- Human verification required: deploy DLL, check Sync node visible under Settings > Content

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Fixed NuGet package version for Microsoft.Extensions.FileProviders.Embedded**
- **Found during:** Task 1
- **Issue:** Plan specified version 8.0.8 but Dynamicweb.CoreUI.Rendering 10.23.9 has transitive dependency on 8.0.15, causing NU1605 package downgrade error
- **Fix:** Updated version from 8.0.8 to 8.0.15
- **Files modified:** Dynamicweb.ContentSync.csproj
- **Commit:** 3d27430

## Verification

- Project compiles with Razor SDK and all NuGet references: PASSED
- All 6 AdminUI classes compile without errors: PASSED
- Checkpoint pending: Sync node visual verification in DW admin
