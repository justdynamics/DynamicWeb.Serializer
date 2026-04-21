# Phase 38: Production-Ready Baseline Hardening - Pattern Map

**Mapped:** 2026-04-21
**Files analyzed:** 19 (7 new + 12 modified)
**Analogs found:** 17 / 19 (2 tooling files have no direct analog — fall back to RESEARCH.md excerpts)

---

## File Classification

### New files (created in Phase 38)

| New file | Role | Data flow | Closest analog | Match quality |
|----------|------|-----------|----------------|---------------|
| `tests/DynamicWeb.Serializer.Tests/Infrastructure/BaselineLinkSweeperAckTests.cs` | test (unit) | read-only (in-memory SerializedPage fixtures) | `tests/DynamicWeb.Serializer.Tests/Infrastructure/BaselineLinkSweeperTests.cs` | **exact** (same fixture, same sweeper) |
| `tests/DynamicWeb.Serializer.Tests/Serialization/ContentDeserializerIdentityInsertTests.cs` | test (unit via ISqlExecutor seam) | mock SQL execution, capture CommandBuilder text | `tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/SqlTableWriterTests.cs` | **role + data-flow match** (same ISqlExecutor mocking pattern) |
| `tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/FlatFileStoreDedupTests.cs` | test (unit) | file I/O to temp dir | `tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/FlatFileStoreTests.cs` | **exact** (same `IDisposable` tempdir pattern, same `FlatFileStore`) |
| `tests/DynamicWeb.Serializer.Tests/AdminUI/SerializerCommandQueryBindingTests.cs` | test (unit) | in-process command `Handle()` | `tests/DynamicWeb.Serializer.Tests/AdminUI/SaveSerializerSettingsCommandTests.cs` | **role-match** (CommandBase subclass, temp-dir filesystem fixture) |
| `tools/swift22-cleanup/05-null-orphan-template-refs.sql` | SQL-cleanup script | state-mutating (UPDATE on Swift 2.2 DB) | `tools/swift22-cleanup/01-null-orphan-page-refs.sql` | **exact** (dynamic SQL over INFORMATION_SCHEMA on `ItemType_Swift-v2_%`) |
| `tools/smoke/Test-BaselineFrontend.ps1` | tool-script (PowerShell) | read-only HTTP + read-only SQL | (partial) `tools/purge-cleandb.sql` for sqlcmd/connection shape | **partial** (no existing PowerShell tool in repo — first of its kind) |
| `tools/smoke/README.md` | docs (tool usage) | — | `tools/swift22-cleanup/README.md` | **exact** (heading shape, target-DB callout, run-order pattern) |
| `docs/baselines/env-bucket.md` | docs (markdown) | — | `docs/baselines/Swift2.2-baseline.md` | **exact** (same three-bucket split, same `##` heading hierarchy) |

### Modified source files

| Modified file | Role | Data flow | Change type | Reference pattern |
|---------------|------|-----------|-------------|-------------------|
| `src/DynamicWeb.Serializer/Models/ProviderPredicateDefinition.cs` | source-model (record) | data-bearing | A.3: **keep** `AcknowledgedOrphanPageIds` (already canonical home) | — |
| `src/DynamicWeb.Serializer/Configuration/ModeConfig.cs` | source-model (record) | data-bearing | A.3: **remove** `AcknowledgedOrphanPageIds` field (lines 39-46) | — |
| `src/DynamicWeb.Serializer/Configuration/ConfigLoader.cs` | source-config | state-transforming (JSON → ModeConfig) | A.3: log warning when `deploy.acknowledgedOrphanPageIds` seen, drop from result; remove line 378 | `Console.Error.WriteLine` migration pattern on line 329 |
| `src/DynamicWeb.Serializer/Configuration/ConfigWriter.cs` | source-config | state-transforming (ModeConfig → JSON) | A.3: remove `AcknowledgedOrphanPageIds` from `PersistedModeSection` + `ToPersistedMode` (lines 53, 76) | — |
| `src/DynamicWeb.Serializer/Serialization/ContentSerializer.cs` | source-serializer | orchestration | A.3: change read path from `_configuration.Deploy.AcknowledgedOrphanPageIds` (line 91, 95-96) to `_configuration.Deploy.Predicates.SelectMany(p => p.AcknowledgedOrphanPageIds)` | — |
| `src/DynamicWeb.Serializer/Serialization/ContentDeserializer.cs` | source-serializer | state-mutating SQL write | A.2: refactor `CreateAreaFromProperties` (lines 443-474) to take `ISqlExecutor` seam | `SqlTableWriter.cs:29` ctor pattern |
| `src/DynamicWeb.Serializer/Infrastructure/BaselineLinkSweeper.cs` | source-infra | read-only validator | B.5: add `validParagraphIds` HashSet + validate regex group 4 in `CheckField` | — |
| `src/DynamicWeb.Serializer/Providers/SqlTable/FlatFileStore.cs` | source-provider | state-mutating (file write) | C.1: fix `DeduplicateFileName` at lines 120-134 — monotonic counter instead of single-MD5 | — |
| `src/DynamicWeb.Serializer/Providers/SqlTable/SqlTableWriter.cs` | source-provider | reference only | (unchanged — analog source for ISqlExecutor pattern) | — |
| `src/DynamicWeb.Serializer/Providers/SqlTable/SqlTableProvider.cs` | source-provider | reference only | (unchanged — C.1 root cause is downstream in `FlatFileStore`, not here) | — |
| `src/DynamicWeb.Serializer/AdminUI/Commands/SerializerSerializeCommand.cs` | source-admin-api | public API surface | D.1: query-param binding fallback; D.2: HTTP status mapping on line 116 | `Dynamicweb.Context.Current.Request["mode"]` fallback inside `Handle()` |
| `src/DynamicWeb.Serializer/AdminUI/Commands/SerializerDeserializeCommand.cs` | source-admin-api | public API surface | D.1: query-param binding fallback; D.2: HTTP status mapping on line 140 | same as Serialize command |
| `src/DynamicWeb.Serializer/Configuration/swift2.2-combined.json` | config-data | deployment config | D-38-16: remove `"strictMode": false` (line 5); remove `"acknowledgedOrphanPageIds": [15717]` (line 15) after B.5 closes | — |
| `docs/baselines/Swift2.2-baseline.md` | docs | — | E.1: add new section "Pre-existing source-data bugs caught by Phase 37 validators" | existing file structure |

---

## Pattern Assignments

### `tests/DynamicWeb.Serializer.Tests/Infrastructure/BaselineLinkSweeperAckTests.cs` (test, unit)

**Analog:** `tests/DynamicWeb.Serializer.Tests/Infrastructure/BaselineLinkSweeperTests.cs`

**Imports pattern** (lines 1-5):

```csharp
using DynamicWeb.Serializer.Infrastructure;
using DynamicWeb.Serializer.Models;
using Xunit;

namespace DynamicWeb.Serializer.Tests.Infrastructure;
```

**Fixture pattern** (`MakePage` helper, lines 9-36):

```csharp
private static SerializedPage MakePage(
    int? sourceId = 100,
    string? shortCut = null,
    string? productPage = null,
    Dictionary<string, object>? fields = null,
    Dictionary<string, object>? propertyFields = null,
    List<SerializedGridRow>? gridRows = null,
    List<SerializedPage>? children = null,
    string menuText = "P")
{
    return new SerializedPage
    {
        PageUniqueId = Guid.NewGuid(),
        SourcePageId = sourceId,
        Name = menuText,
        MenuText = menuText,
        UrlName = menuText,
        SortOrder = 1,
        ShortCut = shortCut,
        Fields = fields ?? new(),
        PropertyFields = propertyFields ?? new(),
        // ...
        GridRows = gridRows ?? new(),
        Children = children ?? new()
    };
}
```

**Core pattern — direct-sweeper test** (lines 61-72):

```csharp
[Fact]
public void Sweep_OneUnresolvableLink_Reported()
{
    var page1 = MakePage(sourceId: 100, shortCut: "Default.aspx?ID=99999");
    var sweeper = new BaselineLinkSweeper();
    var result = sweeper.Sweep(new List<SerializedPage> { page1 });

    Assert.Single(result.Unresolved);
    Assert.Equal(99999, result.Unresolved[0].UnresolvablePageId);
}
```

**Adaptation for A.1 tests** — since ack-list is on `ProviderPredicateDefinition` (per-predicate) and the read path is in `ContentSerializer` (not `BaselineLinkSweeper` itself), A.1 tests must drive `ContentSerializer.Serialize` with a captured log callback. Two viable approaches:

1. **Direct sweep + separate ack-filter test** — unit-test the `acknowledged.Contains` filter logic in isolation (requires making `BaselineLinkSweeper.Sweep` purity-preserving and the filter visible to tests via a helper, OR lifting the filter out of `ContentSerializer.Serialize` lines 93-120 into a named method). Research prefers this approach.
2. **ContentSerializer.Serialize end-to-end** — requires `IContentStore` fake (FakeContentStore exists in `tests/DynamicWeb.Serializer.Tests/TestHelpers/`). Heavier setup; more realistic.

Copy the `log` callback capture pattern from the existing ContentSerializer ctor (line 23 `Action<string>? log = null`) and collect into `var logged = new List<string>();`.

**No throw assertion** (reference from research §Example 1):

```csharp
var logged = new List<string>();
// ... configure predicate.AcknowledgedOrphanPageIds = [15717] and a tree with ref to 15717
// ... invoke serializer.Serialize()
Assert.Contains(logged, l => l.Contains("acknowledged orphan ID 15717"));
// Assert: no InvalidOperationException thrown.
```

---

### `tests/DynamicWeb.Serializer.Tests/Serialization/ContentDeserializerIdentityInsertTests.cs` (test, unit via mock)

**Analog:** `tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/SqlTableWriterTests.cs`

**Imports pattern** (lines 1-6):

```csharp
using System.Data;
using DynamicWeb.Serializer.Models;
using DynamicWeb.Serializer.Providers.SqlTable;
using Dynamicweb.Data;
using Moq;
using Xunit;
```

**ISqlExecutor mock pattern** (lines 62-77):

```csharp
[Fact]
public void BuildMergeCommand_GeneratesValidMerge()
{
    var mockExecutor = new Mock<ISqlExecutor>();
    var writer = new SqlTableWriter(mockExecutor.Object);
    var metadata = CreateEcomOrderFlowMetadata();
    var row = CreateSampleRow();

    var cb = writer.BuildMergeCommand(row, metadata);

    // CommandBuilder produces SQL text; verify via ToString
    var sql = cb.ToString();
    Assert.Contains("MERGE [EcomOrderFlow] AS target", sql);
    Assert.Contains("SET IDENTITY_INSERT [EcomOrderFlow] ON", sql);
    Assert.Contains("SET IDENTITY_INSERT [EcomOrderFlow] OFF", sql);
}
```

**Capture pattern (for A.2)** — use `Callback<CommandBuilder>` to record the executed CommandBuilder's text (research §Example 2):

```csharp
var capturedCommands = new List<string>();
var fakeExecutor = new Mock<ISqlExecutor>();
fakeExecutor.Setup(e => e.ExecuteNonQuery(It.IsAny<CommandBuilder>()))
    .Callback<CommandBuilder>(cb => capturedCommands.Add(cb.ToString()));

var deserializer = new ContentDeserializer(
    /* existing params */,
    sqlExecutor: fakeExecutor.Object);

// ... invoke Area-create path

Assert.Contains(capturedCommands,
    c => c.Contains("SET IDENTITY_INSERT [Area] ON") && c.Contains("SET IDENTITY_INSERT [Area] OFF"));
```

**ISqlExecutor seam pattern** (source: `SqlTableWriter.cs:25-29`):

```csharp
public class SqlTableWriter
{
    private readonly ISqlExecutor _sqlExecutor;
    public SqlTableWriter(ISqlExecutor sqlExecutor) => _sqlExecutor = sqlExecutor;
```

The A.2 plan must introduce the **same seam on `ContentDeserializer`**: add an optional `ISqlExecutor? sqlExecutor = null` ctor parameter (defaulting to a `DwSqlExecutor` wrapper that calls `Database.ExecuteNonQuery`), then replace the bare `Database.ExecuteNonQuery(cb)` call at `ContentDeserializer.cs:473` with `_sqlExecutor.ExecuteNonQuery(cb)`.

**Existing ContentDeserializer ctor** (source: `ContentDeserializer.cs:39-61`) — the new `ISqlExecutor?` parameter is added as an **optional** arg at the end so all existing callers (ContentProvider, SaveSerializerSettingsCommand, live deserialize path) compile unchanged:

```csharp
public ContentDeserializer(
    SerializerConfiguration configuration,
    IContentStore? store = null,
    Action<string>? log = null,
    bool isDryRun = false,
    string? filesRoot = null,
    ConflictStrategy conflictStrategy = ConflictStrategy.SourceWins,
    TargetSchemaCache? schemaCache = null,
    // NEW for A.2:
    ISqlExecutor? sqlExecutor = null)
```

---

### `tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/FlatFileStoreDedupTests.cs` (test, unit)

**Analog:** `tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/FlatFileStoreTests.cs`

**IDisposable temp-directory fixture** (lines 8-24):

```csharp
[Trait("Category", "Phase13")]
public class FlatFileStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FlatFileStore _store;

    public FlatFileStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "FlatFileStoreTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _store = new FlatFileStore();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
```

**Core test pattern — WriteRow with `usedNames` collision** (adapted for C.1):

```csharp
[Fact]
public void WriteRow_MultipleDuplicateIdentities_AllRowsPreserved()
{
    var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var writtenFiles = new List<string>();

    // Write N rows with identical identity "" (simulating empty ProductName)
    for (int i = 0; i < 5; i++)
    {
        _store.WriteRow(
            _tempDir, "EcomProducts", rowIdentity: "",
            rowData: new() { ["ProductId"] = $"P{i}", ["ProductName"] = "" },
            usedNames: usedNames,
            writtenFiles: writtenFiles);
    }

    var files = Directory.GetFiles(Path.Combine(_tempDir, "_sql", "EcomProducts"), "*.yml");
    Assert.Equal(5, files.Length); // FAILS under current bug (would be 1)
    Assert.Equal(5, writtenFiles.Count);
}
```

---

### `tests/DynamicWeb.Serializer.Tests/AdminUI/SerializerCommandQueryBindingTests.cs` (test, unit)

**Analog:** `tests/DynamicWeb.Serializer.Tests/AdminUI/SaveSerializerSettingsCommandTests.cs`

**Temp-filesystem fixture for CommandBase tests** (lines 11-29):

```csharp
public class SaveSerializerSettingsCommandTests : ConfigLoaderValidatorFixtureBase
{
    private readonly string _tempDir;
    private readonly string _filesDir;
    private readonly string _systemDir;
    private readonly string _outputDir;
    private readonly string _configPath;

    public SaveSerializerSettingsCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SaveCmdTests_" + Guid.NewGuid().ToString("N")[..8]);
        _filesDir = Path.Combine(_tempDir, "wwwroot", "Files");
        _systemDir = Path.Combine(_filesDir, "System");
        _outputDir = Path.Combine(_systemDir, "System", "Serializer");
        _configPath = Path.Combine(_filesDir, "Serializer.config.json");

        Directory.CreateDirectory(_filesDir);
        Directory.CreateDirectory(_systemDir);
    }
```

**Imports pattern** (lines 1-7):

```csharp
using DynamicWeb.Serializer.AdminUI.Commands;
using DynamicWeb.Serializer.AdminUI.Models;
using DynamicWeb.Serializer.Configuration;
using DynamicWeb.Serializer.Models;
using DynamicWeb.Serializer.Tests.TestHelpers;
using Dynamicweb.CoreUI.Data;
using Xunit;
```

**Test shape** (adapt for D.1 query-param + D.2 status):

```csharp
[Fact]
public void Handle_ModeProperty_BindsFromJsonBody()
{
    // Current baseline — confirms JSON body path works (serialization is done by framework; direct property set here)
    var cmd = new SerializerSerializeCommand { Mode = "seed" };
    var result = cmd.Handle();
    Assert.NotEqual(CommandResult.ResultType.Invalid, result.Status);
}

[Fact]
public void Handle_QueryParamFallback_BindsWhenModeIsDefault()
{
    // D.1: when Mode is still "deploy" (default) and Dynamicweb.Context.Current.Request["mode"] == "seed",
    //      the fallback inside Handle() should pick up the query-string value.
    // Approach depends on D.1 investigation outcome — if CommandBase binds queryparams natively,
    // this test can drive via the framework; otherwise test the fallback directly.
}

[Fact]
public void Handle_ZeroErrors_ReturnsOkStatus()
{
    // D.2: serialize with no errors must produce ResultType.Ok (maps to HTTP 200).
    // ...
    Assert.Equal(CommandResult.ResultType.Ok, result.Status);
}
```

---

### `tools/swift22-cleanup/05-null-orphan-template-refs.sql` (SQL-cleanup)

**Analog:** `tools/swift22-cleanup/01-null-orphan-page-refs.sql`

**Header pattern** (lines 1-18):

```sql
-- 01-null-orphan-page-refs.sql — Null out 77 paragraph/item-field references
-- to 5 known-broken page IDs. These pages either don't exist or live in a
-- different baseline (Area 26 Digital Assets Portal).
-- ...

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRAN;
```

**Dynamic SQL over INFORMATION_SCHEMA** (lines 59-67 — the canonical pattern for B.1/B.2):

```sql
DECLARE @sql NVARCHAR(MAX) = N'';
SELECT @sql = @sql
    + N'UPDATE [' + c.TABLE_NAME + N'] SET [' + c.COLUMN_NAME + N'] = '
    + N'REPLACE(REPLACE(CAST([' + c.COLUMN_NAME + N'] AS NVARCHAR(MAX)), ''"SelectedValue":"15717"'', ''"SelectedValue":""''), ''Default.aspx?ID=15717'', '''') '
    + N'WHERE CAST([' + c.COLUMN_NAME + N'] AS NVARCHAR(MAX)) LIKE ''%15717%'';' + CHAR(10)
FROM INFORMATION_SCHEMA.COLUMNS c
WHERE c.TABLE_NAME LIKE 'ItemType_Swift-v2_%'
  AND c.DATA_TYPE IN ('nvarchar', 'ntext', 'varchar', 'nchar');
EXEC sp_executesql @sql;
```

**Verify-after-cleanup pattern** (lines 69-83) — required shape: SELECT COUNT(*) per expected-zero location, UNION ALL'd for a single result set. Apply to 05- script with COUNT of each template-name LIKE pattern across the scanned tables.

**Adaptation for B.1/B.2** — three template names (`1ColumnEmail`, `2ColumnsEmail`, `Swift-v2_PageNoLayout.cshtml`). Recommended script structure:

1. Header comment explaining the 3 stale Swift template references (per D-38-06: upstream git repo confirmed does NOT ship these).
2. `BEGIN TRAN;` wrapper.
3. One dynamic-SQL block per template name (or one consolidated block iterating the three values), each emitting `UPDATE [<table>] SET [<column>] = ''` for all string columns under `ItemType_Swift-v2_%` tables where the value matches `%1ColumnEmail%` / `%2ColumnsEmail%` / `%Swift-v2_PageNoLayout.cshtml%`.
4. Verify block with `SELECT COUNT(*) FROM ... WHERE CAST(...) LIKE '%<template>%'` across each affected table — all expected 0.
5. `COMMIT TRAN;` + `PRINT 'Done.';`.

**Research assumption A3** flags that these names may also live in `Paragraph.ItemType` / `Page.Layout` columns (not just `ItemType_Swift-v2_%`). The 05- script's verify step should extend the scan to those columns; if residuals remain, add targeted UPDATEs before the COMMIT.

---

### `tools/smoke/Test-BaselineFrontend.ps1` (tool-script, PowerShell)

**Analog:** No existing PowerShell tool in the repo. Closest directory shape is `tools/swift22-cleanup/` (multi-file tool with README). DB-access pattern reference is `tools/purge-cleandb.sql` (sqlcmd-compatible connection string).

**Use research §Example 5 as the canonical seed** (not reproduced here — research already has the full script). Key structural elements:

- `param()` block: `$Host`, `$ConnectionString`, `$AreaId`, `$LangPrefix` — all with defaults matching REPORT.md's CleanDB (`https://localhost:58217`, `Server=localhost\SQLEXPRESS;Database=Swift-CleanDB;Integrated Security=true;TrustServerCertificate=true`, AreaId 3, `/en-us`).
- `Invoke-Sqlcmd -ConnectionString ... -Query @" ... "@` to enumerate `Page` rows (`WHERE PageAreaID = $AreaId AND PageActive = 1`).
- `$buckets = @{ '2xx' = @(); '3xx' = @(); '4xx' = @(); '5xx' = @() }` accumulator pattern.
- `try { Invoke-WebRequest -Uri $url -SkipCertificateCheck -MaximumRedirection 5 } catch { ... }` for HTTP with redirect + self-signed-cert support.
- Bucket sort via `switch ($code) { {$_ -lt 300} {'2xx'}; {$_ -lt 400} {'3xx'}; {$_ -lt 500} {'4xx'}; default {'5xx'} }`.
- Body excerpt lengths: 4xx = 500 chars, 5xx = 2000 chars + headers (per CONTEXT.md §Specifics).
- Exit 1 on any 5xx; exit 0 otherwise.

**Security constraints** (per RESEARCH.md §Security Domain):

- `$Host` is a **local** parameter (not sourced from external input); do NOT fetch from env var or config file beyond local defaults.
- `-MaximumRedirection 5` caps redirect-chase to prevent runaway.
- Tool is local-dev-only per D-38-13 — README must state "NEVER deploy to customer sites" prominently.

---

### `tools/smoke/README.md` (docs)

**Analog:** `tools/swift22-cleanup/README.md`

**Structure pattern** (lines 1-10):

```markdown
# Swift 2.2 Data Cleanup Scripts

Re-runnable SQL scripts to clean up "obviously wrong" data in a Swift 2.2 DynamicWeb database before serializing it as a deployment baseline.

**Author:** 2026-04-21 autonomous session
**Target DB:** Any Swift 2.2 instance on SQL Server (tested on `localhost\SQLEXPRESS`, DB `Swift-2.2`)
**Source findings:** `.planning/sessions/2026-04-20-e2e-baseline-roundtrip/REPORT.md` — Phase 37 autonomous E2E round-trip surfaced these issues via `BaselineLinkSweeper`, `SqlIdentifierValidator`, and direct DB analysis.

## What these scripts do

| # | Script | Fixes |
```

**Adaptation for D.3** — replace "scripts" with "tool", keep `Target DB` + `Source findings` callouts, replace the numbered-script table with a parameters table + example invocations. Add an explicit "Local-dev only" warning at the top per D-38-13.

---

### `docs/baselines/env-bucket.md` (docs)

**Analog:** `docs/baselines/Swift2.2-baseline.md`

**Structure pattern** (lines 1-8):

```markdown
# Swift 2.2 Baseline — Deployment Configuration

**Config file:** `src/DynamicWeb.Serializer/Configuration/swift2.2-baseline.json`
**Target:** Azure App Service + SQL Azure, dev → test → QA → prod promotion
**Status:** v1 — deployment data only. Seed content and env-specific config are
deliberately out-of-scope (see "Gaps" below).

---

## Purpose
```

**Three-bucket split table** (lines 34-41) — this is the canonical ontology to **reference** (not duplicate) in env-bucket.md:

```markdown
| Bucket | Who owns it | Behavior on deploy | In baseline? |
|--------|-------------|-------------------|--------------|
| **DEPLOYMENT** | Developer / DW template | Overwrite target (source wins) | **YES** |
| **SEED** | Developer initially, end-user thereafter | Apply once if absent; never overwrite | Partial (see gaps) |
| **ENVIRONMENT** | Ops / infrastructure | Never in baseline; per-env config | No |
```

**Env-bucket section shape** (the target doc is the inverse of Swift2.2-baseline.md — it covers the **ENVIRONMENT** bucket specifically). Required sections per D-38-15:

1. `## Purpose` — target audience is "a new customer adopting the baseline who needs to know what do I configure per environment".
2. `## What is NOT in the baseline and why` — mirror the existing heading at line 166 of Swift2.2-baseline.md, but expand each bullet into a subsection.
3. `## GlobalSettings.config` — the `/Files/GlobalSettings.config` file including Friendly URL `/en-us/` routing. Reference the 2026-04-20 E2E REPORT.md where this was the key missed config.
4. `## Azure Key Vault secrets` — payment gateway credentials, storage keys. Link back to Swift2.2-baseline.md's `D2-CREDENTIAL-EXCLUSION` gap.
5. `## Per-env Area fields` — `AreaDomain`, `AreaCdnHost`, `GoogleTagManagerID`; list matches `swift2.2-combined.json` lines 17-33 `excludeFields` / `excludeAreaColumns`.
6. `## Swift templates filesystem` — git clone from `https://github.com/dynamicweb/Swift`, NOT a nupkg; NOT serialized. Reference the 3 stale-template findings from B.1/B.2.
7. `## Azure App Service config pattern` — how the above flow into App Settings + Key Vault references at startup.

Reuse the `Swift2.2-baseline.md:178-194` "Azure deployment assumptions" section as the reference shape for point 7.

---

### `src/DynamicWeb.Serializer/Models/ProviderPredicateDefinition.cs` (source-model)

**Already canonical home** for `AcknowledgedOrphanPageIds` (lines 88-95):

```csharp
/// <summary>
/// Per-predicate Baseline link-sweep bypass (2026-04-20 follow-up to Phase 37-05 LINK-02).
/// Page IDs whose unresolvable references should be logged as warnings rather than raised
/// as fatal errors by the serialize-time <see cref="Infrastructure.BaselineLinkSweeper"/>.
/// Content predicates only. Use for known-broken source data that cannot be cleaned upstream
/// in time; any unresolvable NOT in this list still fails serialize.
/// </summary>
public List<int> AcknowledgedOrphanPageIds { get; init; } = new();
```

**A.3 change:** none (keep as-is). This field is the single source of truth after A.3.

---

### `src/DynamicWeb.Serializer/Configuration/ModeConfig.cs` (source-model)

**A.3 change — remove lines 39-46** (the mode-level duplicate):

```csharp
/// <summary>
/// Phase 37-05 LINK-02 pass-1 bypass (D-22 escape hatch, added 2026-04-20 follow-up):
/// page IDs whose unresolvable references the baseline link sweep should log as warnings
/// rather than raise as fatal errors. Use only for known-broken source data that cannot
/// be cleaned upstream in time for a deploy. Each listed ID is accepted as an acknowledged
/// orphan — any OTHER unresolvable reference still fails serialize. Empty list preserves
/// strict-by-default behavior.
/// </summary>
public List<int> AcknowledgedOrphanPageIds { get; init; } = new();
```

No replacement — the `ProviderPredicateDefinition.AcknowledgedOrphanPageIds` already exists on every predicate.

---

### `src/DynamicWeb.Serializer/Configuration/ConfigLoader.cs` (source-config)

**A.3 change — two edits:**

1. **Remove line 378** in `BuildModeConfig`:

```csharp
AcknowledgedOrphanPageIds = raw.AcknowledgedOrphanPageIds ?? new List<int>()
```

2. **Add migration warning** — when `raw.Deploy.AcknowledgedOrphanPageIds` or `raw.Seed.AcknowledgedOrphanPageIds` is non-empty, log the warning from CONTEXT §Specifics and drop the list:

```csharp
// Inside BuildModeConfigs, after reading raw.Deploy/raw.Seed but before BuildModeConfig calls:
if (raw.Deploy?.AcknowledgedOrphanPageIds?.Count > 0)
{
    Console.Error.WriteLine(
        "[Serializer] WARNING: deploy.acknowledgedOrphanPageIds " +
        "is no longer supported. Move the IDs onto the Content predicate(s) that contain the " +
        "orphan references. The mode-level list is ignored this load. See Phase 38 D-38-03.");
}
// Same for raw.Seed.AcknowledgedOrphanPageIds.
```

3. **Keep line 449** (the `RawModeSection.AcknowledgedOrphanPageIds` JSON DTO field) **temporarily** so old configs parse without JSON errors — it's read-only-for-warning, then discarded.

**Existing warning-emit pattern** (reference: `ConfigLoader.cs:329-331`):

```csharp
Console.Error.WriteLine(
    "[Serializer] Migrating legacy flat Predicates → Deploy.Predicates " +
    "(no backcompat; rewriting on next save)");
```

---

### `src/DynamicWeb.Serializer/Configuration/ConfigWriter.cs` (source-config)

**A.3 change — two edits:**

1. **Remove from `PersistedModeSection`** (line 76):

```csharp
public List<int>? AcknowledgedOrphanPageIds { get; init; }
```

2. **Remove from `ToPersistedMode`** (line 53):

```csharp
AcknowledgedOrphanPageIds = mode.AcknowledgedOrphanPageIds.Count > 0 ? mode.AcknowledgedOrphanPageIds : null
```

No replacement — the per-predicate `AcknowledgedOrphanPageIds` already serializes automatically via `Predicates` (line 73 `public List<ProviderPredicateDefinition> Predicates { get; init; } = new()`; System.Text.Json picks it up via reflection).

---

### `src/DynamicWeb.Serializer/Serialization/ContentSerializer.cs` (source-serializer)

**A.3 change — rewrite sweep-ack filter** (lines 87-121). Current read path:

```csharp
Log($"Link sweep: {sweepResult.ResolvedCount} internal link(s) verified, " +
    $"{sweepResult.Unresolved.Count} unresolvable " +
    $"(ack deploy={_configuration.Deploy.AcknowledgedOrphanPageIds.Count}, seed={_configuration.Seed.AcknowledgedOrphanPageIds.Count})");
if (sweepResult.Unresolved.Count > 0)
{
    var acknowledged = new HashSet<int>(
        _configuration.Deploy.AcknowledgedOrphanPageIds
            .Concat(_configuration.Seed.AcknowledgedOrphanPageIds));
```

**Post-A.3 replacement** — aggregate per-predicate:

```csharp
var deployAck = _configuration.Deploy.Predicates.SelectMany(p => p.AcknowledgedOrphanPageIds).ToList();
var seedAck = _configuration.Seed.Predicates.SelectMany(p => p.AcknowledgedOrphanPageIds).ToList();
Log($"Link sweep: {sweepResult.ResolvedCount} internal link(s) verified, " +
    $"{sweepResult.Unresolved.Count} unresolvable " +
    $"(ack deploy={deployAck.Count}, seed={seedAck.Count})");
if (sweepResult.Unresolved.Count > 0)
{
    var acknowledged = new HashSet<int>(deployAck.Concat(seedAck));
```

Everything downstream of this (lines 97-120 — the GroupBy / Accepted / Fatal split + `WARNING: acknowledged orphan ID ...` log) stays **unchanged**.

---

### `src/DynamicWeb.Serializer/Serialization/ContentDeserializer.cs` (source-serializer)

**A.2 change — add `ISqlExecutor` seam for testability. Reference analog: `SqlTableWriter.cs:25-29`.**

**Current write site** (lines 461-473):

```csharp
// 2026-04-20: wrap in SET IDENTITY_INSERT so explicit AreaID writes succeed against
// a fresh target where Area.AreaId is an identity column. Keeping the areaId stable
// across env is required for predicate.areaId references to work.
var cb = new CommandBuilder();
cb.Add("SET IDENTITY_INSERT [Area] ON; ");
cb.Add($"INSERT INTO [Area] ({string.Join(", ", columns)}) VALUES (");
for (int i = 0; i < values.Count; i++)
{
    if (i > 0) cb.Add(", ");
    cb.Add("{0}", values[i]);
}
cb.Add("); SET IDENTITY_INSERT [Area] OFF;");
Database.ExecuteNonQuery(cb);
```

**A.2 refactor:**

1. Add private field + optional ctor param (mirror `SqlTableWriter.cs:27-29`):

```csharp
private readonly ISqlExecutor _sqlExecutor;
// In ctor:
_sqlExecutor = sqlExecutor ?? new DwSqlExecutor(); // or the existing default wrapper
```

2. Replace line 436 (`Database.ExecuteNonQuery(cb);` in `UpdateAreaFromProperties`) and line 473 (in `CreateAreaFromProperties`) with `_sqlExecutor.ExecuteNonQuery(cb);`.

3. Keep the `SET IDENTITY_INSERT [Area] ON/OFF` wrapping string literal untouched — the test asserts its presence in captured CommandBuilder.ToString().

**Default executor pattern** — `DwSqlExecutor` (or equivalent production wrapper around `Database.ExecuteNonQuery`) should already exist; check `src/DynamicWeb.Serializer/Providers/SqlTable/` for the default that `SqlTableWriter` uses in production. If not public, mirror its implementation in a new `DwSqlExecutor` class next to `ISqlExecutor.cs`.

---

### `src/DynamicWeb.Serializer/Infrastructure/BaselineLinkSweeper.cs` (source-infra)

**B.5 change — paragraph-anchor validation.**

**Current regex** (lines 33-35) — already captures group 4:

```csharp
private static readonly Regex InternalLinkPattern = new(
    @"(Default\.aspx\?ID=)(\d+)(#(\d+))?",
    RegexOptions.IgnoreCase | RegexOptions.Compiled);
```

**Current `CheckField`** (lines 108-123) — ignores group 4:

```csharp
foreach (Match m in InternalLinkPattern.Matches(value))
{
    if (!int.TryParse(m.Groups[2].Value, out var id)) continue;
    if (validIds.Contains(id)) { resolved++; continue; }
    unresolved.Add(new UnresolvedLink(sourceIdent, fieldName, id, m.Value));
}
```

**Post-B.5 replacement — collect paragraph IDs + validate both parts. Full shape:**

1. Add `CollectSourceParagraphIds` helper (mirrors `CollectSourceIds` at lines 58-65):

```csharp
private static void CollectSourceParagraphIds(IEnumerable<SerializedPage> pages, HashSet<int> acc)
{
    foreach (var p in pages)
    {
        foreach (var row in p.GridRows)
            foreach (var col in row.Columns)
                foreach (var para in col.Paragraphs)
                    if (para.SourceParagraphId.HasValue) acc.Add(para.SourceParagraphId.Value);
        CollectSourceParagraphIds(p.Children, acc);
    }
}
```

2. Modify `Sweep` (lines 41-53) to collect + thread paragraph IDs through recursion — add a second `HashSet<int> validParagraphIds` alongside `validSourceIds`.

3. Modify `CheckField` to read group 4 when present:

```csharp
foreach (Match m in InternalLinkPattern.Matches(value))
{
    if (!int.TryParse(m.Groups[2].Value, out var pageId)) continue;
    if (!validIds.Contains(pageId))
    {
        unresolved.Add(new UnresolvedLink(sourceIdent, fieldName, pageId, m.Value));
        continue;
    }
    // Page resolved — validate optional #paragraph anchor.
    if (m.Groups[4].Success && int.TryParse(m.Groups[4].Value, out var paraId)
        && !validParagraphIds.Contains(paraId))
    {
        unresolved.Add(new UnresolvedLink(sourceIdent, fieldName, paraId, m.Value));
        continue;
    }
    resolved++;
}
```

**Existing test** `BaselineLinkSweeperTests.Sweep_AnchorFragment_StripsFragment_AndResolvesPage` (lines 193-203) must stay green — the fix **only adds** a new failure path when the paragraph ID is absent from the tree; existing tests where page resolves but no `#Y` suffix exists still pass.

**Pattern source** for the walker shape (per research §Don't Hand-Roll): `InternalLinkResolver.cs:161-187` (`CollectSourceParagraphIds` already exists in that file — consider reusing it directly rather than duplicating).

---

### `src/DynamicWeb.Serializer/Providers/SqlTable/FlatFileStore.cs` (source-provider)

**C.1 change — monotonic counter dedup.**

**Current buggy code** (lines 120-134):

```csharp
private static string DeduplicateFileName(string sanitized, string originalIdentity, HashSet<string>? usedNames)
{
    if (usedNames == null)
        return sanitized;

    if (usedNames.Add(sanitized))
        return sanitized;

    // Collision: append first 6 chars of MD5 of original identity
    var hash = Convert.ToHexString(
        MD5.HashData(Encoding.UTF8.GetBytes(originalIdentity))).ToLowerInvariant();
    var deduped = $"{sanitized} [{hash[..6]}]";
    usedNames.Add(deduped);  // BUG: silently ignores HashSet.Add returning false
    return deduped;          // BUG: returns same deduped name N times for N duplicate identities
}
```

**Post-C.1 replacement (per research §Pattern 3):**

```csharp
private static string DeduplicateFileName(string sanitized, string originalIdentity, HashSet<string>? usedNames)
{
    if (usedNames == null) return sanitized;
    if (usedNames.Add(sanitized)) return sanitized;

    // Same identity seen before — append numeric suffix until unique. Preserves
    // sort order of multiple empty-name / duplicate-name rows without relying on
    // MD5-only (which collapses to one file for N identical identities).
    var hashPrefix = Convert.ToHexString(
        MD5.HashData(Encoding.UTF8.GetBytes(originalIdentity))).ToLowerInvariant()[..6];
    for (int n = 1; n < 100_000; n++)
    {
        var candidate = $"{sanitized} [{hashPrefix}-{n}]";
        if (usedNames.Add(candidate)) return candidate;
    }
    throw new InvalidOperationException(
        $"Exhausted 100000 filename variants for identity '{originalIdentity}' — refuse to silently drop rows.");
}
```

**Sanitize method** (lines 110-118) — **do not touch**. `SanitizeFileName` correctly maps `""` to `"_unnamed"`; that is not the bug.

---

### `src/DynamicWeb.Serializer/AdminUI/Commands/SerializerSerializeCommand.cs` (source-admin-api)

**D.1 change — query-param fallback.** Insert after line 41 (after `Enum.TryParse` but before any logic that reads `Mode`):

```csharp
// D-38-11: if Mode stayed at the default "deploy" but the request query string carried
// ?mode=seed, honor the query value. DW CommandBase does not bind query params by
// default for POST; this fallback closes the gap without changing the DW-managed body
// binding convention.
if (string.Equals(Mode, "deploy", StringComparison.OrdinalIgnoreCase))
{
    var fromQuery = Dynamicweb.Context.Current?.Request?["mode"];
    if (!string.IsNullOrEmpty(fromQuery))
    {
        Mode = fromQuery;
        // Re-parse — fall through to existing Enum.TryParse below.
        if (!Enum.TryParse<DeploymentMode>(Mode, ignoreCase: true, out deploymentMode))
        {
            return new() { Status = CommandResult.ResultType.Invalid, Message = $"Invalid mode '{Mode}'." };
        }
    }
}
```

**Caveat** (per research §Pitfall 5): **investigate first.** If DW `CommandBase` has a native query-binding convention (e.g. a `[FromQuery]` attribute or a naming convention), prefer that over the `Dynamicweb.Context.Current` fallback. The failing-test-first approach (a curl test with `?mode=seed` before JSON body) lets the fix shape itself.

**D.2 change — HTTP status mapping** (line 116 today):

```csharp
return new CommandResult
{
    Status = result.HasErrors ? CommandResult.ResultType.Error : CommandResult.ResultType.Ok,
    Message = message
};
```

**This line is already correct** — status flips to `Ok` iff `result.HasErrors` is false. The D.2 bug (per research §Open Question 5) may be elsewhere (a non-fatal-error path that silently sets `HasErrors=true`, or a framework-level middleware that scans the response body). **Plan's first D.2 task must reproduce the HTTP 400 against clean CleanDB + empty Serializer state** to determine the actual trigger. Do NOT pre-commit to a fix shape.

**Relevant field** (`SerializerOrchestrator.cs:361-364` per RESEARCH.md):

```csharp
public bool HasErrors => Errors.Count > 0 || SerializeResults.Any(r => r.HasErrors);
```

The literal `"Errors: "` string in the Summary only appears when `Errors.Count > 0` (per line 112 of SerializerSerializeCommand.cs: `if (result.HasErrors) message += $" Errors: {string.Join(\"; \", result.Errors)}";`). If the observed HTTP 400 is triggered by this path, the `result.Errors` list is non-empty — the fix is to find which orchestrator path is populating it (likely strict-mode escalation) and either suppress the escalation or preserve the HTTP 200.

---

### `src/DynamicWeb.Serializer/AdminUI/Commands/SerializerDeserializeCommand.cs` (source-admin-api)

**Same shape as Serialize command**: D.1 query-param fallback inserted after line 55 `Enum.TryParse` block; D.2 status mapping already on line 140 (same `result.HasErrors ? Error : Ok` shape). Apply identical pattern from Serialize command above.

**Additional consideration** (source: lines 28, 91-93) — Deserialize also has `bool? StrictMode` parameter and `IsAdminUiInvocation` flag that flow into `StrictModeResolver.Resolve`. The D.1 query-param fallback should match these parameters too if they're observable in the query string:

```csharp
// Optional extension — if strictMode is a query param:
var strictFromQuery = Dynamicweb.Context.Current?.Request?["strictMode"];
if (!string.IsNullOrEmpty(strictFromQuery) && bool.TryParse(strictFromQuery, out var strictQ))
{
    StrictMode = strictQ;
}
```

---

### `src/DynamicWeb.Serializer/Configuration/swift2.2-combined.json` (config-data)

**D-38-16 change — final step of Phase 38:**

1. Remove line 5 (`"strictMode": false,`) — API/CLI default reverts to strict-on via `StrictModeResolver.Resolve` entry-point precedence (Phase 37-04 D-16).
2. Remove line 15 (`"acknowledgedOrphanPageIds": [ 15717 ],`) — only after B.5 closes and the paragraph-anchor false-positive is a correctness fix rather than a toggle.

Gates (per D-38-16): **all of B.1/B.2, B.3, B.4, B.5, C.1 must close first**; without them, a strict-mode serialize/deserialize would escalate the known warnings and fail.

---

### `docs/baselines/Swift2.2-baseline.md` (docs)

**E.1 change — add new section after line 176** (the existing "What's NOT in the baseline" table). Insert a new H2 heading **"Pre-existing source-data bugs caught by Phase 37 validators"**:

Required sub-content (per D-38-14):

- (a) The 3 column-name mistakes that `SqlIdentifierValidator` catches (specific names surfaced in REPORT.md).
- (b) The 5 orphan page IDs that `BaselineLinkSweeper` catches (8308, 149, 15717, 295, 140) — all cleaned 2026-04-21 by `tools/swift22-cleanup/01-null-orphan-page-refs.sql`.
- (c) The 267 orphan-area pages + 238 soft-deleted pages removed by scripts 03- and 04-.
- (d) The `acknowledgedOrphanPageIds: [15717]` note on Content predicate — explain it's a paragraph-anchor false-positive until B.5 closes, then will be removed per D-38-16.

**Section shape pattern** (reference the existing "The Swift 2.2 contamination problem" section at lines 57-71) — use the same "here's what DW ships / here's what we do about it" narrative, followed by a table of findings with `tools/swift22-cleanup/` script references.

---

## Shared Patterns

### Shared: ISqlExecutor seam (applies to A.2)

**Source:** `src/DynamicWeb.Serializer/Providers/SqlTable/ISqlExecutor.cs:10-17` + `SqlTableWriter.cs:25-29`
**Apply to:** `ContentDeserializer` (A.2 refactor)

```csharp
// The interface (full file — 17 lines):
public interface ISqlExecutor
{
    IDataReader ExecuteReader(CommandBuilder command);
    int ExecuteNonQuery(CommandBuilder command);
}

// Consumer shape (SqlTableWriter.cs:25-29):
public class SqlTableWriter
{
    private readonly ISqlExecutor _sqlExecutor;
    public SqlTableWriter(ISqlExecutor sqlExecutor) => _sqlExecutor = sqlExecutor;
```

**Why this over Testcontainers** (per research §Don't Hand-Roll): zero new NuGet deps, CI-fast, matches existing Phase 37 test patterns, future Area-write tests benefit from the same seam.

### Shared: Temp-directory `IDisposable` test fixture (applies to all new tests that touch files)

**Source:** `tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/FlatFileStoreTests.cs:8-24`
**Apply to:** C.1 tests; AdminUI command tests (reuse SaveSerializerSettingsCommandTests pattern).

```csharp
public class FlatFileStoreTests : IDisposable
{
    private readonly string _tempDir;
    public FlatFileStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "FlatFileStoreTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }
    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}
```

### Shared: xUnit + Moq imports + namespace shape (applies to all new test files)

**Source:** `tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/SqlTableWriterTests.cs:1-10`
**Apply to:** All 4 new test files.

```csharp
using System.Data;
using DynamicWeb.Serializer.Models;
using DynamicWeb.Serializer.Providers.SqlTable;  // adapt namespace per tested component
using Dynamicweb.Data;
using Moq;
using Xunit;

namespace DynamicWeb.Serializer.Tests.<SubArea>;

[Trait("Category", "Phase38")]  // NEW — add this trait to all Phase 38 tests for filtering
public class <Component>Tests { ... }
```

**Convention reminder** (per research §Project Constraints):

- No `FluentAssertions` (xUnit assertions only).
- No new NuGet packages in Phase 38.
- Tests colocated under `tests/DynamicWeb.Serializer.Tests/<SubArea>/` matching the source subdirectory layout.

### Shared: Log-callback pattern (applies to A.1, A.2, D.2 tests)

**Source:** existing `Action<string>? log = null` parameter on `ContentSerializer` (line 23), `ContentDeserializer` (line 42), and commands via `private void Log(...)` helpers.
**Apply to:** Any test that asserts on warning/info log content.

```csharp
var logged = new List<string>();
// Pass `logged.Add` as the log callback to ContentSerializer/ContentDeserializer/orchestrator.
Assert.Contains(logged, l => l.Contains("acknowledged orphan ID 15717"));
```

### Shared: Dynamic SQL over INFORMATION_SCHEMA (applies to B.1/B.2)

**Source:** `tools/swift22-cleanup/01-null-orphan-page-refs.sql:59-67`
**Apply to:** B.1/B.2 cleanup script + any future "scan all ItemType_Swift-v2_* tables" cleanup.

Pattern already extracted above in the 05- script section. Key safety points (per research §Security Domain):

- Hardcoded template names (no user input).
- Bracket-escape `[<col>]` identifiers.
- Wrap in `BEGIN TRAN / COMMIT TRAN` with `SET XACT_ABORT ON`.
- Always emit a "Verify after cleanup" block at the end with `SELECT COUNT(*) ... UNION ALL ...`.

### Shared: Warning-emit convention (applies to A.3 migration, ConfigLoader)

**Source:** `src/DynamicWeb.Serializer/Configuration/ConfigLoader.cs:107-109` + `:329-331`

```csharp
Console.Error.WriteLine(
    "[Serializer] WARNING: ..."); // prefix "[Serializer]" for grep'ability
```

**A.3 warning wording** (verbatim from CONTEXT.md §Specifics):

```
[Serializer] WARNING: deploy.acknowledgedOrphanPageIds (or seed.acknowledgedOrphanPageIds)
is no longer supported. Move the IDs onto the Content predicate(s) that contain the
orphan references. The mode-level list is ignored this load. See Phase 38 D-38-03.
```

---

## No Analog Found

| File | Role | Data flow | Reason |
|------|------|-----------|--------|
| `tools/smoke/Test-BaselineFrontend.ps1` | PowerShell tool | HTTP + SQL | No existing PowerShell scripts in the repo. Fall back to RESEARCH.md §Example 5 for canonical shape. |
| `tools/smoke/README.md` | tool docs | — | `tools/swift22-cleanup/README.md` is a distant analog (different tool type, same directory-structure convention). |

For both, the planner should reference **RESEARCH.md** directly rather than an existing file:

- Script shape: RESEARCH.md §Code Examples → Example 5 (PowerShell) lines 598-636.
- Bucket structure + body excerpt lengths: CONTEXT.md §Specifics → "D.3 smoke tool expected output".
- Security constraints: RESEARCH.md §Security Domain → D.3 row ("tool is local-dev-only").

---

## Metadata

**Analog search scope:**

- `tests/DynamicWeb.Serializer.Tests/` (all subdirs) — for test-file analogs.
- `src/DynamicWeb.Serializer/Providers/SqlTable/` — for ISqlExecutor seam.
- `src/DynamicWeb.Serializer/Configuration/` — for ConfigLoader / ConfigWriter / ModeConfig.
- `src/DynamicWeb.Serializer/Infrastructure/` — for BaselineLinkSweeper.
- `src/DynamicWeb.Serializer/Serialization/` — for ContentSerializer + ContentDeserializer.
- `src/DynamicWeb.Serializer/AdminUI/Commands/` — for CommandBase subclasses.
- `tools/swift22-cleanup/` — for SQL-cleanup pattern.
- `tools/` (top level) — for PowerShell/CLI tool patterns.
- `docs/baselines/` — for markdown docs shape.

**Files scanned:** 19 (direct reads) + ~30 (glob/grep hits).

**Pattern extraction date:** 2026-04-21

**Phase 38 context:** Every new file has a strong analog inside the codebase except the PowerShell smoke tool + its README (no prior PowerShell in repo). Every modified source file has line-specific interventions identified in RESEARCH.md; this PATTERNS.md supplements with concrete excerpt copies the planner can lift directly into PLAN.md action blocks.
