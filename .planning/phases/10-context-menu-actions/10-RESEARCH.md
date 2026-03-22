# Phase 10: Context Menu Actions - Research

**Researched:** 2026-03-22
**Domain:** DW10 Admin UI context menu injection, file download/upload, zip creation
**Confidence:** HIGH

## Summary

This phase adds Serialize and Deserialize context menu actions to every page node in the DW content tree. The research focused on five critical areas: (1) how to inject custom actions into the page list context menu, (2) how to trigger browser file downloads from a command, (3) how to implement file upload in a modal dialog, (4) how to build prompt/modal screens, and (5) zip file creation in .NET 8.

All five areas are well-supported by DW10's CoreUI framework. The `ListScreenInjector` pattern provides a clean way to add actions to the `PageListScreen`. The `DownloadFileAction` + `FileResult` pattern handles browser downloads. The `PromptScreenBase<TModel>` + `FileUpload` editor handles the deserialize modal. Standard `System.IO.Compression.ZipFile` handles zip creation.

**Primary recommendation:** Use `ListScreenInjector<PageListScreen, PageDataModel>` to inject a "Content Sync" action group with Serialize (via `DownloadFileAction`) and Deserialize (via `OpenDialogAction` to a `PromptScreenBase` modal) actions into every page's context menu.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- **D-01:** Zip contains YAML files in mirror-tree layout plus a log file -- no config or metadata manifests
- **D-02:** Serialize runs synchronously -- user clicks, waits, gets download. No async/progress bar.
- **D-03:** Download filename format: `ContentSync_{PageName}_{date}.zip` (e.g. `ContentSync_CustomerCenter_2026-03-22.zip`)
- **D-04:** Zip is also saved to a configurable export path on disk -- new config field `ExportDirectory` in SyncConfiguration
- **D-05:** Reuse ContentSerializer with a temporary SyncConfiguration scoped to the clicked page's subtree
- **D-06:** Single modal dialog with: file upload field, mode selection (radio/dropdown), and Go button
- **D-07:** Three import modes: Overwrite (replace clicked page + subtree), Add as children (zip becomes children under clicked page), Add as sibling (zip root appears next to clicked page)
- **D-08:** Validate-then-apply strategy: parse and validate entire zip before touching DB. If runtime error during apply, continue with remaining pages and report failures.
- **D-09:** Show summary after deserialize: "X/Y pages imported successfully. Z failed: [reasons]"
- **D-10:** Reuse ContentDeserializer with a temporary SyncConfiguration scoped to the target location
- **D-11:** Actions appear on every page node -- no predicate filtering
- **D-12:** Actions appear in their own "Content Sync" action group at the bottom of the context menu
- **D-13:** Use DW's ScreenInjector or equivalent pattern to inject actions

### Claude's Discretion
- How to inject context menu actions (ListScreenInjector target type)
- How to implement file upload in the deserialize modal
- How to trigger browser download after serialize
- Temporary directory management for serialize
- How to scope ContentSerializer/ContentDeserializer to a single page subtree via temp SyncConfiguration
- ExportDirectory config field placement and default value

### Deferred Ideas (OUT OF SCOPE)
None
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| ACT-01 | Serialize action appears in page context menu | ListScreenInjector pattern with GetListItemActions returning ActionGroup with DownloadFileAction |
| ACT-02 | Serialize creates a zip of the page subtree at a temporary location | ContentSerializer + temp SyncConfiguration + System.IO.Compression.ZipFile |
| ACT-03 | Serialize zip is available for browser download | DownloadFileAction + CommandResult with FileResult model |
| ACT-04 | Serialize zip is also saved to a configurable location on disk | ExportDirectory field on SyncConfiguration, File.Copy after zip creation |
| ACT-05 | Deserialize action appears in page context menu | Same ListScreenInjector, second ActionNode with OpenDialogAction |
| ACT-06 | Deserialize prompts user to upload a zip file | PromptScreenBase with FileUpload editor (Accept = ".zip") |
| ACT-07 | Deserialize lets user choose overwrite-node or import-as-subtree | PromptScreenBase with Select editor (radio/dropdown) for import mode |
| ACT-08 | Context menu actions reuse existing ContentSerializer/ContentDeserializer | Temp SyncConfiguration with single predicate; no duplication |
</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Dynamicweb.Content.UI | 10.23.9 | PageListScreen, PageDataModel, ContentActionsHelper | Already referenced; provides the target screen for injection |
| Dynamicweb.CoreUI | (transitive) | ListScreenInjector, DownloadFileAction, FileResult, PromptScreenBase, FileUpload, OpenDialogAction | Core DW10 admin UI framework |
| System.IO.Compression | (built-in) | ZipFile.CreateFromDirectory, ZipArchive | Built into .NET 8 runtime |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| YamlDotNet | 13.7.1 | Already used by ContentSerializer | No new dependency needed |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| ListScreenInjector | ScreenInjector (OnAfter with layout manipulation) | ListScreenInjector is purpose-built for adding context menu actions to list screens; ScreenInjector is more general but requires manual layout.ContextActionGroups manipulation |

**Installation:** No new packages needed. All required types are already available through existing NuGet references.

## Architecture Patterns

### Recommended Project Structure
```
src/Dynamicweb.ContentSync/AdminUI/
  Injectors/
    ContentSyncPageListInjector.cs    # ListScreenInjector<PageListScreen, PageDataModel>
  Commands/
    SerializeSubtreeCommand.cs        # Creates zip, returns FileResult for download
    DeserializeSubtreeCommand.cs      # Extracts zip, runs ContentDeserializer
  Screens/
    DeserializePromptScreen.cs        # PromptScreenBase<DeserializeModel> with file upload + mode select
  Models/
    DeserializeModel.cs               # DataViewModelBase with UploadedFilePath + ImportMode
  Queries/
    DeserializePromptQuery.cs         # Carries pageId + areaId to the prompt screen
```

### Pattern 1: ListScreenInjector for Context Menu Actions
**What:** Subclass `ListScreenInjector<PageListScreen, PageDataModel>` to add custom action groups to every page row's context menu.
**When to use:** Whenever you need to add actions to an existing DW list screen without modifying its source.
**Example:**
```csharp
// Source: DW10 source - Dynamicweb.CoreUI/Screens/ListScreenInjector.cs
// and ExpressDelivery sample - OrderOverviewInjector.cs
using Dynamicweb.Content.UI.Models;
using Dynamicweb.Content.UI.Screens;
using Dynamicweb.CoreUI.Actions;
using Dynamicweb.CoreUI.Actions.Implementations;
using Dynamicweb.CoreUI.Icons;
using Dynamicweb.CoreUI.Screens;

namespace Dynamicweb.ContentSync.AdminUI.Injectors;

public sealed class ContentSyncPageListInjector : ListScreenInjector<PageListScreen, PageDataModel>
{
    public override IEnumerable<ActionGroup>? GetListItemActions(PageDataModel model)
    {
        var pageId = model.Id;
        var areaId = model.AreaId;

        return new[]
        {
            new ActionGroup
            {
                Nodes = new List<ActionNode>
                {
                    new()
                    {
                        Name = "Serialize subtree",
                        Icon = Icon.Download,
                        NodeAction = DownloadFileAction.Using(
                            new SerializeSubtreeCommand { PageId = pageId, AreaId = areaId }
                        )
                    },
                    new()
                    {
                        Name = "Deserialize into...",
                        Icon = Icon.Upload,
                        NodeAction = OpenDialogAction
                            .To<DeserializePromptScreen>()
                            .With(new DeserializePromptQuery { PageId = pageId, AreaId = areaId })
                    }
                }
            }
        };
    }
}
```

### Pattern 2: DownloadFileAction + FileResult for Browser Downloads
**What:** A command returns `CommandResult` with `Model` set to a `FileResult` instance. DW's `DownloadFileAction` UI action type causes the browser to download the file.
**When to use:** Triggering a file download from a context menu action.
**Example:**
```csharp
// Source: DW10 source - Dynamicweb.Files.UI/Commands/Directories/DirectoryDownloadCommand.cs
public override CommandResult Handle()
{
    // ... serialize to temp dir, create zip ...
    var zipStream = new FileStream(zipPath, FileMode.Open, FileAccess.Read);
    return new CommandResult
    {
        Status = CommandResult.ResultType.Ok,
        Model = new FileResult
        {
            FileStream = zipStream,
            ContentType = "application/zip",
            FileDownloadName = $"ContentSync_{pageName}_{DateTime.Now:yyyy-MM-dd}.zip"
        }
    };
}
```

### Pattern 3: PromptScreenBase for Modal Dialogs
**What:** Subclass `PromptScreenBase<TModel>` to create a modal dialog with editors (fields). Use `EditorForCommand` to bind editors to command properties.
**When to use:** When you need user input before running a command (file upload + mode selection).
**Example:**
```csharp
// Source: DW10 source - ExpressDelivery/Screens/ExpressDeliverySelectPromptScreen.cs
public sealed class DeserializePromptScreen : PromptScreenBase<DeserializeModel>
{
    protected override void BuildPromptScreen()
    {
        // File upload editor
        var fileUpload = new FileUpload
        {
            Label = "Zip file",
            Path = "/Files/System/ContentSync/uploads",
            Accept = { ".zip" }
        };
        AddComponent(fileUpload, "Upload");

        // Mode selection editor
        var modeEditor = EditorForCommand<DeserializeSubtreeCommand, string>(
            c => c.ImportMode, "Import mode");
        AddComponent(modeEditor, "Options");
    }

    protected override string GetScreenName() => "Deserialize content";
    protected override string GetOkActionName() => "Import";

    protected override ActionBase? GetOkAction() =>
        RunCommandAction
            .For<DeserializeSubtreeCommand>(new() { PageId = Model?.PageId ?? 0, AreaId = Model?.AreaId ?? 0 })
            .WithClosePopupAndReloadOnSuccess(ReloadType.Workspace);
}
```

### Pattern 4: FileUpload Editor for Zip Upload
**What:** The `FileUpload` editor (`Dynamicweb.CoreUI.Editors.Inputs.FileUpload`) provides a file upload widget that uploads to a server-side path. The uploaded file path is then available as the editor's string value.
**When to use:** Accepting file uploads in a prompt screen.
**Key properties:**
- `Path`: Server directory where uploaded files are stored
- `Accept`: List of accepted file types (e.g., `".zip"`)
- `Multiple`: Whether multiple files can be uploaded
- `OnUploadCompleteAction`: Action to run after upload completes

### Anti-Patterns to Avoid
- **Do NOT modify PageListScreen or ContentActionsHelper:** Use the injector pattern to add actions non-invasively
- **Do NOT duplicate serialization logic:** Create a temporary SyncConfiguration and pass it to existing ContentSerializer/ContentDeserializer
- **Do NOT use ScreenInjector<PageListScreen> with OnAfter:** Use ListScreenInjector which has the purpose-built GetListItemActions method
- **Do NOT buffer entire zip in memory for large trees:** Use FileStream pointing to the temp zip file, not MemoryStream

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Browser file download | Custom HTTP endpoint | `DownloadFileAction` + `FileResult` | DW's command infrastructure handles streaming, content type, and download headers |
| Zip creation | Manual entry-by-entry ZipArchive manipulation | `ZipFile.CreateFromDirectory(sourceDir, destZip)` | One-liner, handles subdirectories, proper compression |
| File upload UI | Custom upload endpoint + JS | `FileUpload` editor in PromptScreenBase | DW's built-in upload infrastructure handles chunking and server storage |
| Context menu injection | Reflection/patching PageListScreen | `ListScreenInjector<PageListScreen, PageDataModel>` | DW auto-discovers injectors via AddInManager, appends to context actions |
| Modal dialog with fields | Custom screen layout | `PromptScreenBase<TModel>` with AddComponent | Handles form submission, shadow editing, Cancel/OK buttons |

**Key insight:** DW10's CoreUI framework provides purpose-built abstractions for every interaction pattern needed. The injector pattern ensures our actions are added without modifying DW source, and DownloadFileAction/FileUpload handle the complex browser-server interactions.

## Common Pitfalls

### Pitfall 1: FileUpload Path Must Be a Writable Server Directory
**What goes wrong:** FileUpload requires a server-side path where files are stored after upload. If the path doesn't exist or isn't writable, upload silently fails.
**Why it happens:** The FileUpload editor uploads to a DW Files directory, not a temp location.
**How to avoid:** Use a known writable path like `/Files/System/ContentSync/uploads/` and ensure it exists. Clean up uploaded files after processing.
**Warning signs:** Upload appears to succeed but command can't find the file.

### Pitfall 2: ContentSerializer Writes Relative to OutputDirectory
**What goes wrong:** ContentSerializer writes YAML files to `OutputDirectory` from SyncConfiguration. For ad-hoc serialize, you need a *temp* OutputDirectory distinct from the main sync directory.
**Why it happens:** Reusing the main OutputDirectory would overwrite the normal sync tree.
**How to avoid:** Create a temp directory (`Path.GetTempPath() + Guid`), set it as OutputDirectory in a temp SyncConfiguration, serialize there, then zip it. Clean up the temp directory after zipping.
**Warning signs:** Main sync YAML files are overwritten or corrupted.

### Pitfall 3: ContentSerializer Expects a PredicateDefinition with AreaId
**What goes wrong:** ContentSerializer iterates predicates and calls `Services.Areas.GetArea(predicate.AreaId)`. A predicate without a valid AreaId will be skipped.
**Why it happens:** The serializer was designed for config-driven predicates, not ad-hoc page exports.
**How to avoid:** Create a temp PredicateDefinition with the correct AreaId from the clicked page. Set the Path to match the page's position in the tree so the predicate filter includes it. Alternatively, the predicate path filter may need to be adjusted to target the specific page subtree.
**Warning signs:** Serialization completes with 0 pages.

### Pitfall 4: Deserialize Mode Scoping is Non-Trivial
**What goes wrong:** The three import modes (overwrite, add-as-children, add-as-sibling) require different manipulations of the target page tree. The existing ContentDeserializer assumes a predicate-based target area.
**Why it happens:** ContentDeserializer was designed for full-area sync, not targeted subtree import.
**How to avoid:** For each mode, create a specialized adapter that:
  - **Overwrite:** Delete existing subtree under target page, then deserialize with target page's parent as context
  - **Add as children:** Deserialize with target page ID as parent context
  - **Add as sibling:** Deserialize with target page's parent ID as parent context
The adapter may need to manipulate WriteContext or create a custom deserialization flow that reuses the page/gridrow/paragraph writing logic.
**Warning signs:** Pages end up at wrong tree level or orphaned.

### Pitfall 5: Zip Must Be Cleaned Up After Download
**What goes wrong:** Temp directories and zip files accumulate on disk.
**Why it happens:** FileResult.FileStream is read by the framework and the stream is eventually disposed, but the underlying file remains.
**How to avoid:** Use a wrapper stream that deletes the temp file on Dispose, or use a finally block in the command. Alternatively, write to a MemoryStream for small trees and a temp file for large ones.
**Warning signs:** Disk space gradually consumed in temp directory.

### Pitfall 6: PageDataModel.Id vs Page.ID for Context Actions
**What goes wrong:** The injector receives a `PageDataModel` which has `Id` (int) and `AreaId` (int) properties. These are the values to pass to commands, not a string-based page identifier.
**Why it happens:** Confusion between DW's various page identification schemes.
**How to avoid:** Always use `model.Id` and `model.AreaId` from the PageDataModel passed to GetListItemActions.
**Warning signs:** Command receives 0 or wrong page ID.

## Code Examples

### Creating a Temp SyncConfiguration for Ad-Hoc Serialize
```csharp
// Source: Existing ContentSerializer + SyncConfiguration patterns
private static SyncConfiguration CreateTempSerializeConfig(string tempOutputDir, int areaId, string pagePath)
{
    return new SyncConfiguration
    {
        OutputDirectory = tempOutputDir,
        LogLevel = "info",
        DryRun = false,
        ConflictStrategy = ConflictStrategy.SourceWins,
        Predicates = new List<PredicateDefinition>
        {
            new PredicateDefinition
            {
                Name = "ad-hoc-serialize",
                Path = pagePath,    // e.g., "/CustomerCenter"
                AreaId = areaId,
                Excludes = new List<string>()
            }
        }
    };
}
```

### FileResult Streaming Pattern (from DirectoryDownloadCommand)
```csharp
// Source: DW10 source - Dynamicweb.Files.UI/Commands/Directories/DirectoryDownloadCommand.cs
return new CommandResult
{
    Status = CommandResult.ResultType.Ok,
    Model = new FileResult
    {
        FileStream = new FileStream(zipFilePath, FileMode.Open, FileAccess.Read),
        ContentType = "application/zip",
        FileDownloadName = $"ContentSync_{pageName}_{DateTime.Now:yyyy-MM-dd}.zip"
    }
};
```

### ListScreenInjector Discovery (Automatic)
```csharp
// No registration needed. DW's AddInManager automatically discovers classes
// inheriting from ListScreenInjector<TScreen, TRowModel> at startup.
// The injector is instantiated and its GetListItemActions() is called
// for each row in the PageListScreen.
// See: ListScreenBase.GetListItemContextActionsInternal() lines 277-283
```

### Select Editor for Import Mode
```csharp
// Source: DW10 CoreUI pattern from Phase 08/09 learnings
using static Dynamicweb.CoreUI.Editors.Inputs.ListBase;

private static Select GetImportModeSelect()
{
    return new Select
    {
        Options = new List<ListOption>
        {
            new() { Label = "Overwrite (replace page and subtree)", Value = "overwrite" },
            new() { Label = "Add as children", Value = "children" },
            new() { Label = "Add as sibling", Value = "sibling" }
        }
    };
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `ScreenInjector<T>.OnAfter` with layout manipulation | `ListScreenInjector<TScreen, TRowModel>.GetListItemActions` | CoreUI current | Purpose-built method for injecting context menu actions into list screens |
| `new DownloadFileAction(command)` | `DownloadFileAction.Using(command)` | CoreUI current | Old constructor is [Obsolete], use static factory |
| `new OpenDialogAction(typeof(T))` | `OpenDialogAction.To<T>()` | CoreUI current | Old constructor is [Obsolete], use static factory |
| `new ConfirmAction(action, label)` | `ConfirmAction.For(action, label)` | CoreUI current | Old constructor is [Obsolete], use static factory |

**Deprecated/outdated:**
- Generic versions (`DownloadFileAction<T>`, `OpenDialogAction<T>`) are all marked `[Obsolete]` -- use the non-generic class with static factory methods

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | Manual testing in DW10 admin UI |
| Config file | N/A -- UI phase requires runtime DW instance |
| Quick run command | `dotnet build src/Dynamicweb.ContentSync/Dynamicweb.ContentSync.csproj` |
| Full suite command | Build + deploy to DW test instance + manual verification |

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| ACT-01 | Serialize action in page context menu | manual-only | Deploy + right-click page in content tree | N/A |
| ACT-02 | Serialize creates zip of subtree | manual-only | Click Serialize, verify zip contents | N/A |
| ACT-03 | Zip available for browser download | manual-only | Click Serialize, browser downloads file | N/A |
| ACT-04 | Zip saved to disk at ExportDirectory | manual-only | Check ExportDirectory after Serialize | N/A |
| ACT-05 | Deserialize action in page context menu | manual-only | Deploy + right-click page in content tree | N/A |
| ACT-06 | Deserialize prompts for zip upload | manual-only | Click Deserialize, verify modal appears | N/A |
| ACT-07 | Deserialize mode selection works | manual-only | Test all three modes with zip upload | N/A |
| ACT-08 | Reuses existing serializer/deserializer | unit | `dotnet build` (compilation check -- no duplicate logic) | N/A |

### Sampling Rate
- **Per task commit:** `dotnet build src/Dynamicweb.ContentSync/Dynamicweb.ContentSync.csproj`
- **Per wave merge:** Build + deploy to test instance
- **Phase gate:** Full manual test of all 8 requirements on DW test instance

### Wave 0 Gaps
None -- this is a UI phase; validation is compilation + manual testing. No test infrastructure changes needed.

## Open Questions

1. **FileUpload path behavior in PromptScreenBase**
   - What we know: FileUpload editor has a `Path` property for server-side storage and the value becomes a string path. The editor has `Accept` for file type filtering.
   - What's unclear: Exactly how the uploaded file path is propagated to the command -- is it through the model's string property, or through a separate mechanism? Does FileUpload work correctly inside a PromptScreenBase form submission flow?
   - Recommendation: During implementation, test with a simple PromptScreenBase that has a FileUpload editor. If FileUpload path binding doesn't work with `EditorForCommand`, use a model property with `EditorFor` instead and read the path in the command.

2. **ContentSerializer predicate scoping for single page subtree**
   - What we know: ContentSerializer iterates predicates and filters pages by path. The predicate Path field is matched against page content paths.
   - What's unclear: Can a predicate Path be set to target a specific page deep in the tree (e.g., `/CustomerCenter/SubPage`)? The serializer builds content paths as `"/" + rootPage.MenuText` for root pages and recursively appends. If the clicked page is not a root page, the predicate filter needs to match a deeper path.
   - Recommendation: Investigate whether creating a predicate with Path = `/ClickedPageMenuText` would only match if the clicked page is a root page. For non-root pages, may need to build the full content path up to the clicked page and use that as the predicate path. Alternatively, consider a simpler approach: traverse the page subtree manually using `Services.Pages`, serialize to temp dir using FileSystemStore directly, bypassing the predicate filtering.

3. **Temp file cleanup after FileResult streaming**
   - What we know: DirectoryDownloadCommand creates a MemoryStream via ZipHelper.ZipFilesToMemory. For potentially large content trees, a FileStream-based approach is safer.
   - What's unclear: When does DW dispose the FileStream? Does the framework guarantee cleanup after response completes?
   - Recommendation: Use a subclassed FileStream that deletes the file on Dispose. Or follow DW's own pattern and use MemoryStream (the content trees for a single page subtree are unlikely to be truly massive).

## Sources

### Primary (HIGH confidence)
- `C:\Projects\temp\dw10source\Dynamicweb.CoreUI\Screens\ListScreenInjector.cs` -- ListScreenInjector class hierarchy and interface
- `C:\Projects\temp\dw10source\Dynamicweb.CoreUI\Screens\ListScreenBase.cs` -- How injectors are integrated (lines 277-283)
- `C:\Projects\temp\dw10source\Dynamicweb.CoreUI\Actions\Implementations\DownloadFileAction.cs` -- File download action pattern
- `C:\Projects\temp\dw10source\Dynamicweb.CoreUI\Data\FileResult.cs` -- FileResult with ContentType, FileDownloadName, FileStream
- `C:\Projects\temp\dw10source\Dynamicweb.CoreUI\Data\CommandResult.cs` -- CommandResult.Model carries FileResult
- `C:\Projects\temp\dw10source\Dynamicweb.Files.UI\Commands\Directories\DirectoryDownloadCommand.cs` -- Real-world download command example
- `C:\Projects\temp\dw10source\Dynamicweb.CoreUI\Screens\PromptScreenBase.cs` -- PromptScreen base class
- `C:\Projects\temp\dw10source\Dynamicweb.CoreUI\Editors\Inputs\FileUpload.cs` -- FileUpload editor
- `C:\Projects\temp\dw10source\Dynamicweb.Content.UI\Screens\PageListScreen.cs` -- Target screen (ListScreenBase<PageDataModel>)
- `C:\Projects\temp\dw10source\Dynamicweb.Content.UI\Models\PageDataModel.cs` -- Row model with Id and AreaId
- `C:\Projects\temp\dw10source\Dynamicweb.Content.UI\Tree\ContentActionsHelper.cs` -- How DW builds page context actions (shows pattern)
- `C:\Projects\temp\dwextensionsample\Samples-main\ExpressDelivery\ExpressDelivery\Injectors\OrderOverviewInjector.cs` -- ScreenInjector sample
- `C:\Projects\temp\dwextensionsample\Samples-main\ExpressDelivery\ExpressDelivery\Screens\ExpressDeliverySelectPromptScreen.cs` -- PromptScreenBase sample

### Secondary (MEDIUM confidence)
- Existing project code: ContentSerializer.cs, ContentDeserializer.cs, SyncConfiguration.cs, PredicateDefinition.cs -- Confirmed API signatures for reuse

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- all types verified in DW10 source code
- Architecture: HIGH -- ListScreenInjector pattern verified with real injector examples and ListScreenBase integration code
- Pitfalls: MEDIUM -- pitfalls 3-4 (predicate scoping, deserialize mode) have some uncertainty around exact implementation approach

**Research date:** 2026-03-22
**Valid until:** 2026-04-22 (stable DW10 APIs, version pinned at 10.23.9)
