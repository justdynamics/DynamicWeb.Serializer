---
phase: 37-production-ready-baseline
reviewed: 2026-04-20T00:00:00Z
depth: standard
files_reviewed: 93
files_reviewed_list:
  - src/DynamicWeb.Serializer/AdminUI/Commands/DeletePredicateCommand.cs
  - src/DynamicWeb.Serializer/AdminUI/Commands/SaveItemTypeCommand.cs
  - src/DynamicWeb.Serializer/AdminUI/Commands/SavePredicateCommand.cs
  - src/DynamicWeb.Serializer/AdminUI/Commands/SaveSerializerSettingsCommand.cs
  - src/DynamicWeb.Serializer/AdminUI/Commands/SaveXmlTypeCommand.cs
  - src/DynamicWeb.Serializer/AdminUI/Commands/ScanXmlTypesCommand.cs
  - src/DynamicWeb.Serializer/AdminUI/Commands/SerializeSubtreeCommand.cs
  - src/DynamicWeb.Serializer/AdminUI/Commands/SerializerDeserializeCommand.cs
  - src/DynamicWeb.Serializer/AdminUI/Commands/SerializerSerializeCommand.cs
  - src/DynamicWeb.Serializer/AdminUI/Models/ItemTypeEditModel.cs
  - src/DynamicWeb.Serializer/AdminUI/Models/ItemTypeListModel.cs
  - src/DynamicWeb.Serializer/AdminUI/Models/PredicateEditModel.cs
  - src/DynamicWeb.Serializer/AdminUI/Models/PredicateListModel.cs
  - src/DynamicWeb.Serializer/AdminUI/Models/SerializerSettingsModel.cs
  - src/DynamicWeb.Serializer/AdminUI/Models/XmlTypeEditModel.cs
  - src/DynamicWeb.Serializer/AdminUI/Models/XmlTypeListModel.cs
  - src/DynamicWeb.Serializer/AdminUI/Queries/ItemTypeBySystemNameQuery.cs
  - src/DynamicWeb.Serializer/AdminUI/Queries/ItemTypeListQuery.cs
  - src/DynamicWeb.Serializer/AdminUI/Queries/PredicateByIndexQuery.cs
  - src/DynamicWeb.Serializer/AdminUI/Queries/PredicateListQuery.cs
  - src/DynamicWeb.Serializer/AdminUI/Queries/SerializerSettingsQuery.cs
  - src/DynamicWeb.Serializer/AdminUI/Queries/XmlTypeByNameQuery.cs
  - src/DynamicWeb.Serializer/AdminUI/Queries/XmlTypeListQuery.cs
  - src/DynamicWeb.Serializer/AdminUI/Screens/ItemTypeEditScreen.cs
  - src/DynamicWeb.Serializer/AdminUI/Screens/PredicateEditScreen.cs
  - src/DynamicWeb.Serializer/AdminUI/Screens/PredicateListScreen.cs
  - src/DynamicWeb.Serializer/AdminUI/Screens/SerializerSettingsEditScreen.cs
  - src/DynamicWeb.Serializer/AdminUI/Screens/XmlTypeEditScreen.cs
  - src/DynamicWeb.Serializer/AdminUI/Tree/ItemTypeEditNavigationNodePathProvider.cs
  - src/DynamicWeb.Serializer/AdminUI/Tree/ItemTypeNavigationNodePathProvider.cs
  - src/DynamicWeb.Serializer/AdminUI/Tree/PredicateNavigationNodePathProvider.cs
  - src/DynamicWeb.Serializer/AdminUI/Tree/SerializerSettingsNodeProvider.cs
  - src/DynamicWeb.Serializer/Configuration/ConfigLoader.cs
  - src/DynamicWeb.Serializer/Configuration/ConfigPathResolver.cs
  - src/DynamicWeb.Serializer/Configuration/ConfigWriter.cs
  - src/DynamicWeb.Serializer/Configuration/ConflictStrategy.cs
  - src/DynamicWeb.Serializer/Configuration/DeploymentMode.cs
  - src/DynamicWeb.Serializer/Configuration/ModeConfig.cs
  - src/DynamicWeb.Serializer/Configuration/RuntimeExcludes.cs
  - src/DynamicWeb.Serializer/Configuration/SerializerConfiguration.cs
  - src/DynamicWeb.Serializer/Configuration/SqlIdentifierValidator.cs
  - src/DynamicWeb.Serializer/Configuration/SqlWhereClauseValidator.cs
  - src/DynamicWeb.Serializer/Infrastructure/BaselineLinkSweeper.cs
  - src/DynamicWeb.Serializer/Infrastructure/DwCacheServiceRegistry.cs
  - src/DynamicWeb.Serializer/Infrastructure/ManifestCleaner.cs
  - src/DynamicWeb.Serializer/Infrastructure/ManifestWriter.cs
  - src/DynamicWeb.Serializer/Infrastructure/StrictModeEscalator.cs
  - src/DynamicWeb.Serializer/Infrastructure/TargetSchemaCache.cs
  - src/DynamicWeb.Serializer/Infrastructure/TemplateAssetManifest.cs
  - src/DynamicWeb.Serializer/Infrastructure/TemplateReferenceScanner.cs
  - src/DynamicWeb.Serializer/Models/ProviderPredicateDefinition.cs
  - src/DynamicWeb.Serializer/Providers/CacheInvalidator.cs
  - src/DynamicWeb.Serializer/Providers/Content/ContentProvider.cs
  - src/DynamicWeb.Serializer/Providers/ISerializationProvider.cs
  - src/DynamicWeb.Serializer/Providers/ProviderDeserializeResult.cs
  - src/DynamicWeb.Serializer/Providers/ProviderRegistry.cs
  - src/DynamicWeb.Serializer/Providers/SerializationProviderBase.cs
  - src/DynamicWeb.Serializer/Providers/SerializeResult.cs
  - src/DynamicWeb.Serializer/Providers/SerializerOrchestrator.cs
  - src/DynamicWeb.Serializer/Providers/SqlTable/FlatFileStore.cs
  - src/DynamicWeb.Serializer/Providers/SqlTable/SqlTableProvider.cs
  - src/DynamicWeb.Serializer/Providers/SqlTable/SqlTableReader.cs
  - src/DynamicWeb.Serializer/Providers/SqlTable/SqlTableWriter.cs
  - src/DynamicWeb.Serializer/Serialization/ContentDeserializer.cs
  - src/DynamicWeb.Serializer/Serialization/ContentSerializer.cs
  - src/DynamicWeb.Serializer/Serialization/InternalLinkResolver.cs
  - tests/DynamicWeb.Serializer.Tests/AdminUI/ItemTypeCommandTests.cs
  - tests/DynamicWeb.Serializer.Tests/AdminUI/ItemTypePerModeTests.cs
  - tests/DynamicWeb.Serializer.Tests/AdminUI/PredicateCommandTests.cs
  - tests/DynamicWeb.Serializer.Tests/AdminUI/SaveSerializerSettingsCommandTests.cs
  - tests/DynamicWeb.Serializer.Tests/AdminUI/SerializerSettingsNodeProviderModeTreeTests.cs
  - tests/DynamicWeb.Serializer.Tests/AdminUI/XmlTypeCommandTests.cs
  - tests/DynamicWeb.Serializer.Tests/AdminUI/XmlTypePerModeTests.cs
  - tests/DynamicWeb.Serializer.Tests/Configuration/ConfigLoaderTests.cs
  - tests/DynamicWeb.Serializer.Tests/Configuration/DeployModeConfigLoaderTests.cs
  - tests/DynamicWeb.Serializer.Tests/Configuration/RuntimeExcludesTests.cs
  - tests/DynamicWeb.Serializer.Tests/Configuration/SqlIdentifierValidatorTests.cs
  - tests/DynamicWeb.Serializer.Tests/Configuration/SqlWhereClauseValidatorTests.cs
  - tests/DynamicWeb.Serializer.Tests/Infrastructure/BaselineLinkSweeperTests.cs
  - tests/DynamicWeb.Serializer.Tests/Infrastructure/DwCacheServiceRegistryTests.cs
  - tests/DynamicWeb.Serializer.Tests/Infrastructure/ManifestCleanerTests.cs
  - tests/DynamicWeb.Serializer.Tests/Infrastructure/ManifestWriterTests.cs
  - tests/DynamicWeb.Serializer.Tests/Infrastructure/StrictModeEscalatorTests.cs
  - tests/DynamicWeb.Serializer.Tests/Infrastructure/TargetSchemaCacheTests.cs
  - tests/DynamicWeb.Serializer.Tests/Infrastructure/TemplateAssetManifestTests.cs
  - tests/DynamicWeb.Serializer.Tests/Infrastructure/TemplateReferenceScannerTests.cs
  - tests/DynamicWeb.Serializer.Tests/Integration/StrictModeIntegrationTests.cs
  - tests/DynamicWeb.Serializer.Tests/Providers/CacheInvalidatorTests.cs
  - tests/DynamicWeb.Serializer.Tests/Providers/SerializerOrchestratorTests.cs
  - tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/SqlTableLinkResolutionIntegrationTests.cs
  - tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/SqlTableProviderCoercionTests.cs
  - tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/SqlTableProviderDeserializeTests.cs
  - tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/SqlTableProviderSerializeTests.cs
  - tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/SqlTableReaderWhereClauseTests.cs
  - tests/DynamicWeb.Serializer.Tests/Serialization/ContentDeserializerAreaSchemaTests.cs
  - tests/DynamicWeb.Serializer.Tests/Serialization/InternalLinkResolverSqlTableTests.cs
findings:
  critical: 2
  warning: 11
  info: 9
  total: 22
status: issues_found
---

# Phase 37: Code Review Report

**Reviewed:** 2026-04-20
**Depth:** standard
**Files Reviewed:** 93
**Status:** issues_found

## Summary

Phase 37 is a substantial refactor delivering the Deploy/Seed bifurcation, strict mode, manifest-based stale-output cleanup, cross-environment link resolution, and the DW cache service registry. The defensive architecture is strong overall — SEED-002's whitelist validator, CommandBuilder parameterization, the manifest cleaner's symlink guard (T-37-01-01), and StrictModeEscalator's warning cap (T-37-04-03) all show good hygiene.

Two correctness gaps stand out for CI/CD safety:

1. **`SqlTableProvider.Serialize` / `SqlTableProvider.Deserialize` trust the predicate's `Table` name as a SQL identifier without re-validating against `SqlIdentifierValidator` at execution time.** The `table` interpolates into `[{tableName}]` in reader/writer SQL. When `ConfigLoader.Load` is called **without** an `identifierValidator` (the default public overload; every call site that hits production paths other than the CLI takes this branch — e.g., ad-hoc `SerializeSubtreeCommand`, legacy callers, the admin-UI `PredicateByIndexQuery`, and every `ConfigLoader.Load(path)` in the tree and list queries), a hand-edited config file can inject SQL via a crafted table name. The CommandBuilder parameter path only protects the values, not the interpolated identifier.
2. **`ManifestCleaner`'s symlink-safety check is incomplete** on Windows — `Path.GetFullPath` doesn't resolve symlinked directories, and the `StartsWith` prefix check happens against the pre-resolution path, so a `ReparsePoint` directory that survives the `AttributesToSkip` filter (because `EnumerationOptions.AttributesToSkip` does not apply to the root directory itself) can lead the cleaner outside the mode root. The defensive per-file `ReparsePoint` delete is correct, but the containment check is the last line of defense and it isn't tight.

Beyond these, there are several concurrency, state-management, and defensive-coding concerns — most notably the **shared `TargetSchemaCache`** instance in `ProviderRegistry.CreateDefault` is wired correctly for single-run lifetime semantics, but there's no thread-safety on its internal dictionaries (any future parallel predicate execution would race). The StrictModeEscalator wrapper double-records nested WARNING lines in a corner case. And the admin-UI `CreateColumnSelectMultiDual` permits table names like `MyTable` but not schema-qualified names like `dbo.MyTable` or bracketed names — inconsistent with INFORMATION_SCHEMA-driven validation elsewhere.

No test files were flagged (tests are broadly well-constructed — integration tests cover the AreaSchema / SqlTable link resolution scenarios).

## Critical Issues

### CR-01: SQL identifier injection via `Table` name when ConfigLoader is called without an identifier validator

**File:** `src/DynamicWeb.Serializer/Providers/SqlTable/SqlTableReader.cs:29-35` (also `SqlTableWriter.cs:64,67,93,100,119,267,284,300,312,315,322,325,332,347` — every `[{tableName}]` or `[{metadata.TableName}]` interpolation)

**Issue:** `SqlTableReader.ReadAllRows` composes `SELECT * FROM [{tableName}]` and `SqlTableWriter`'s `MERGE`, `INSERT`, `DELETE`, and `ALTER TABLE` statements all interpolate `metadata.TableName` directly into SQL text. The CommandBuilder `{0}` placeholders parameterize the *values* but not the *identifier*. These call sites trust that `TableMetadata.TableName` came from a validated source.

The validation is actually performed — but only in `ConfigLoader.Load(filePath, SqlIdentifierValidator)` (the two-argument overload at `ConfigLoader.cs:29`). The **one-argument public overload** `ConfigLoader.Load(filePath)` at `ConfigLoader.cs:18` forwards `identifierValidator: null`, which means `ValidateIdentifiers` is never called and the raw JSON table string flows straight through to SQL.

Call sites currently using the unvalidated overload include:
- `src/DynamicWeb.Serializer/AdminUI/Commands/SaveSerializerSettingsCommand.cs:47` (`ConfigLoader.Load(configPath)`)
- `src/DynamicWeb.Serializer/AdminUI/Commands/DeletePredicateCommand.cs:27`
- `src/DynamicWeb.Serializer/AdminUI/Commands/SaveItemTypeCommand.cs:30`
- `src/DynamicWeb.Serializer/AdminUI/Commands/SaveXmlTypeCommand.cs:30`
- `src/DynamicWeb.Serializer/AdminUI/Commands/ScanXmlTypesCommand.cs:26`
- `src/DynamicWeb.Serializer/AdminUI/Commands/SerializerSerializeCommand.cs:56`
- `src/DynamicWeb.Serializer/AdminUI/Commands/SerializerDeserializeCommand.cs:70` — **this is the deserialize entry point**, which then runs every SqlTable predicate's SQL
- `src/DynamicWeb.Serializer/AdminUI/Commands/SerializeSubtreeCommand.cs:146`
- `src/DynamicWeb.Serializer/AdminUI/Queries/*.cs` (all five query files)
- `src/DynamicWeb.Serializer/AdminUI/Tree/SerializerSettingsNodeProvider.cs:204,237`

`SavePredicateCommand.cs:195-201` does run validation when `IdentifierValidator` is explicitly set, but no production code sets it — only tests do. Once a malicious/mistyped config is persisted to disk, every subsequent `ConfigLoader.Load(path)` loads it unvalidated.

Realistic attack: an admin edits `Serializer.config.json` manually (this is an explicitly supported workflow — see `SerializerSettingsModel.ConfigFilePath`), sets `Deploy.Predicates[0].Table = "EcomOrders] WHERE 1=1; DROP TABLE Users; --"`, and on next deserialize the composed SQL becomes `SELECT * FROM [EcomOrders] WHERE 1=1; DROP TABLE Users; --]`.

**Fix:** Make `ConfigLoader.Load(string)` always run identifier+where validation with a default `SqlIdentifierValidator()` instance at the live DB. The current one-argument overload is essentially a "skip validation" bypass that every production path hits. Alternatively, move validation into `SqlTableProvider.Serialize` / `Deserialize` as a precondition so it re-runs per predicate regardless of how the config was loaded:

```csharp
// In SqlTableProvider.cs, at the top of Serialize() and Deserialize():
private readonly SqlIdentifierValidator _identifierValidator;

public SqlTableProvider(..., SqlIdentifierValidator? identifierValidator = null)
{
    _identifierValidator = identifierValidator ?? new SqlIdentifierValidator();
    // ...
}

public override SerializeResult Serialize(...)
{
    var validation = ValidatePredicate(predicate);
    if (!validation.IsValid) return new SerializeResult { Errors = validation.Errors };

    // NEW: always whitelist the table identifier before any SQL composition.
    try { _identifierValidator.ValidateTable(predicate.Table!); }
    catch (InvalidOperationException ex)
    {
        return new SerializeResult { Errors = [ex.Message] };
    }

    // ... rest unchanged
}
```

Wire the validator through `ProviderRegistry.CreateDefault` so every production path gets it. Also consider tightening `SqlIdentifierValidator.ValidateTable` to reject any input containing characters outside `[A-Za-z0-9_]` before the INFORMATION_SCHEMA lookup — that's a belt-and-braces guard against `]` injection inside an otherwise-legal-sounding table name.

### CR-02: `ManifestCleaner` containment check does not follow symlinks on the resolved paths

**File:** `src/DynamicWeb.Serializer/Infrastructure/ManifestCleaner.cs:24-54`

**Issue:** The cleaner takes `modeRoot` as input, resolves it with `Path.GetFullPath` (which normalizes `..` and relative segments but does **not** resolve symlinks), and uses the normalized path as the prefix for containment. It then enumerates files with `AttributesToSkip = FileAttributes.ReparsePoint`, which — per .NET docs — **only filters entries during enumeration, not the enumeration root itself**. If `modeRoot` is itself a symlink (e.g., because an admin symlinked `deploy/` to another location to share output), every file discovered under it will have a `fullFile` path whose real-filesystem location is outside the `modeRootPrefix`, but the string prefix check (`fullFile.StartsWith(modeRootPrefix)`) will still pass because the comparison is over the un-resolved string.

Additionally, the `AttributesToSkip` mechanism only skips files and directories at *enumeration time*, not symlinks that `Directory.EnumerateFiles` follows transparently on some file systems (Windows junction points in particular don't set `ReparsePoint` in the way the caller expects — directory junctions behave as if transparent to `EnumerateFiles`). A chained junction-then-symlink can lead the enumerator into a subtree whose absolute path still string-prefix-matches, but whose real target is elsewhere. The per-file `info.Attributes & FileAttributes.ReparsePoint` check at line 62 only guards files themselves being symlinks, not directories along the path.

The cleaner deletes files with **no Recycle Bin** (`File.Delete`). A mis-scoped run on production is unrecoverable.

**Fix:** Use real-path resolution (via `FileSystemInfo.ResolveLinkTarget` or `DirectoryInfo.LinkTarget` recursively) when computing the prefix, and reject `modeRoot` entirely when it's itself a reparse point:

```csharp
public int CleanStale(string modeRoot, string mode, IEnumerable<string> writtenFiles, Action<string>? log = null)
{
    if (!Directory.Exists(modeRoot)) return 0;

    // Reject symlinked modeRoot outright — the sanitised path would diverge from the real target.
    var modeRootInfo = new DirectoryInfo(modeRoot);
    if ((modeRootInfo.Attributes & FileAttributes.ReparsePoint) != 0)
    {
        log?.Invoke($"Cleanup aborted: modeRoot '{modeRoot}' is a symlink/junction — refusing to delete.");
        return 0;
    }

    var resolvedModeRoot = Path.GetFullPath(modeRoot);
    var modeRootPrefix = resolvedModeRoot.TrimEnd(...) + Path.DirectorySeparatorChar;

    foreach (var file in Directory.EnumerateFiles(resolvedModeRoot, "*", enumOptions))
    {
        // Resolve each file's real link target before the prefix check.
        var info = new FileInfo(file);
        var realPath = info.LinkTarget != null
            ? Path.GetFullPath(info.LinkTarget, Path.GetDirectoryName(file)!)
            : Path.GetFullPath(file);

        if (!realPath.StartsWith(modeRootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            log?.Invoke($"Cleanup: skipped candidate outside modeRoot: {file} -> {realPath}");
            continue;
        }
        // ... rest unchanged
    }
}
```

Also add an explicit integration test exercising a symlinked mode root (skip on platforms without symlink support).

## Warnings

### WR-01: `TargetSchemaCache` is shared across providers with no thread-safety

**File:** `src/DynamicWeb.Serializer/Infrastructure/TargetSchemaCache.cs:15-19,176-182`
**File:** `src/DynamicWeb.Serializer/Providers/ProviderRegistry.cs:37-38`

**Issue:** `ProviderRegistry.CreateDefault` creates a single `TargetSchemaCache` instance and passes it to `SqlTableProvider`. The cache is then shared across every SqlTable predicate in a run (design intent — coalesces INFORMATION_SCHEMA queries). The internal `_columns`, `_types`, and `_loggedMissing` dictionaries are plain `Dictionary<,>` / `HashSet<>` without any synchronization. `Ensure(tableName)` does a non-atomic check-then-add.

Currently `SerializerOrchestrator.DeserializeAll` runs predicates sequentially, so this is safe today. But the shared-cache comment at `SqlTableProvider.cs:29-30` and the per-run semantics at `ProviderRegistry.cs:30-31` strongly suggest a future parallelize-by-predicate evolution. The first time someone adds `Parallel.ForEach` over predicates, this cache will start corrupting entries silently — two threads loading the same table could double-add, log missing columns twice, or (worst case) one thread reads a half-constructed dictionary during rehash.

**Fix:** Either lock the dictionaries with a `SemaphoreSlim` per-table (preferred — avoids contention on the common hot path of cache-hit reads), or switch to `ConcurrentDictionary<string, ...>` with `GetOrAdd`:

```csharp
private readonly ConcurrentDictionary<string, HashSet<string>> _columns = new(StringComparer.OrdinalIgnoreCase);
private readonly ConcurrentDictionary<string, Dictionary<string, string>> _types = new(StringComparer.OrdinalIgnoreCase);
private readonly ConcurrentDictionary<(string, string), byte> _loggedMissing = new();

private void Ensure(string tableName)
{
    if (_columns.ContainsKey(tableName)) return;
    _columns.GetOrAdd(tableName, _ =>
    {
        var (cols, types) = _schemaLoader(tableName);
        _types[tableName] = types;
        return cols;
    });
}

public bool LogMissingColumnOnce(string table, string column, Action<string>? log)
{
    if (!_loggedMissing.TryAdd((table, column), 0)) return false;
    log?.Invoke(...);
    return true;
}
```

Even in the current single-threaded world, the `LogMissingColumnOnce` non-thread-safety deserves documenting — its whole purpose is "log exactly once."

### WR-02: StrictModeEscalator WARNING-lines routed via wrapper are recorded twice when a provider also calls `Escalate` directly

**File:** `src/DynamicWeb.Serializer/Providers/SerializerOrchestrator.cs:325-343`
**File:** `src/DynamicWeb.Serializer/Infrastructure/StrictModeEscalator.cs:41-50`

**Issue:** `WrapLogWithEscalator` routes every log line that starts with `WARNING` through `escalator.RecordOnly(msg)`. This correctly captures WARNING lines emitted by providers that only call `log?.Invoke("WARNING: ...")`. But it double-records if any call site *also* uses `escalator.Escalate(...)` directly — `Escalate` writes `"WARNING: ..."` into the caller's log sink (the wrapped log), which in turn calls `RecordOnly` on the same escalator instance. The end-of-run `AssertNoWarnings` then sees the warning twice in `_recordedWarnings`.

Currently, `Escalate` is only called from `TemplateAssetManifest.Validate` (`TemplateAssetManifest.cs:101-137`). That path uses `_templateEscalator` in `ContentDeserializer.cs:60` which is a **separate, always-lenient escalator** (`strict: false`), so the double-record doesn't currently surface. But this is fragile: if `ContentDeserializer` ever plumbs the orchestrator's strict escalator into `_templateEscalator` (which is the natural refactor to centralize strict-mode accounting), you'd start seeing every missing template counted twice in `CumulativeStrictModeException.Warnings`.

**Fix:** Either:
(a) have `Escalate` emit to the log sink with a recognizable marker it then skips in the wrapper (e.g., the wrapper skips lines that already passed through `Escalate`), or
(b) make the wrapper aware that it should skip re-routing when the same escalator already recorded the warning (requires de-dup by reference or a hash), or
(c) document the invariant explicitly: "callers must pick one of `Escalate` or `log("WARNING: ...")`, never both," and add an assertion in `Escalate` that `_recordedWarnings.Last() != warning` immediately after add.

Option (c) is the minimum safe change today. Recommended in comments at the top of `StrictModeEscalator`:

```csharp
/// <remarks>
/// IMPORTANT: <see cref="Escalate"/> already writes to the caller's log. If the log is also
/// wrapped by <see cref="SerializerOrchestrator.WrapLogWithEscalator"/>, the warning will be
/// recorded twice. Do NOT call Escalate through a log that is already wrapped by the same
/// escalator — use <see cref="RecordOnly"/> instead, or pass a raw log sink.
/// </remarks>
```

### WR-03: Admin-UI `CreateColumnSelectMultiDual` table-name regex is narrower than `SqlIdentifierValidator`

**File:** `src/DynamicWeb.Serializer/AdminUI/Screens/PredicateEditScreen.cs:169`

**Issue:** The regex `^[A-Za-z_][A-Za-z0-9_]*$` rejects legal SQL identifiers that `SqlIdentifierValidator` would accept (bracketed names, schema-qualified names like `dbo.Area`, or names with spaces that are bracketed in INFORMATION_SCHEMA). More importantly, it's a *different* whitelist than `SqlIdentifierValidator` uses, so the two validators can diverge: the admin UI accepts `Area` but the live validator rejects it because `Area` isn't a base table in this installation. Inconsistent gating makes the save-vs-edit experience confusing.

Also the `DataGroupMetadataReader.GetColumnTypes(tableName)` call on the next line is itself a raw SQL lookup that depends on this regex as its only defence. If someone bypasses the regex by editing the model via developer tools, the unvalidated name flows into the metadata reader's query.

**Fix:** Drop the regex gate here and delegate to `SqlIdentifierValidator.ValidateTable` (if available) or at least match the admin regex to the one effective check. Or, even better, have `DataGroupMetadataReader.GetColumnTypes` itself use a `CommandBuilder {0}` parameter for the table name in its `INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = {0}` query (matching what `TargetSchemaCache.DefaultLoader` already does correctly) so the UI layer doesn't need its own regex.

### WR-04: `ContentProvider.BuildSerializerConfiguration` clobbers Deploy.Predicates via legacy setter, bypassing per-mode exclusions

**File:** `src/DynamicWeb.Serializer/Providers/Content/ContentProvider.cs:222-229`

**Issue:** The helper builds a `SerializerConfiguration` using the legacy `Predicates` property:

```csharp
return new SerializerConfiguration
{
    OutputDirectory = outputDirectory,
    Predicates = new List<ProviderPredicateDefinition> { predicate }
};
```

Per `SerializerConfiguration.cs:83-89`, assigning `Predicates` is a legacy alias that calls `Deploy = Deploy with { Predicates = value }`. That means the resulting configuration has `Deploy.ExcludeFieldsByItemType` and `Deploy.ExcludeXmlElementsByType` left empty, and the `ContentDeserializer` invocation at line 134 reads `_configuration.Deploy.ExcludeFieldsByItemType.Count` which is always 0 here. When a Seed predicate is routed through ContentProvider, its Seed-scoped exclusions never make it to the deserializer.

The `ContentSerializer.SerializePredicate` at `ContentSerializer.cs:154-155` has the same shape: it always reads from `_configuration.Deploy.*` — so Seed predicates serialize without their Seed-scoped excludes applying either.

This is documented as a follow-up in `ContentDeserializer.cs:261-264` ("Follow-up plan threads per-mode exclusions through here"), but it's worth surfacing as a correctness gap — a Seed predicate today effectively ignores `excludeFieldsByItemType` and `excludeXmlElementsByType`.

**Fix:** Pass the owning mode through ContentProvider to the temp-SerializerConfiguration. Either:

```csharp
private static SerializerConfiguration BuildSerializerConfiguration(
    ProviderPredicateDefinition predicate,
    string outputDirectory,
    ModeConfig sourceMode)  // new param
{
    return new SerializerConfiguration
    {
        OutputDirectory = outputDirectory,
        Deploy = new ModeConfig
        {
            OutputSubfolder = "deploy",
            Predicates = new List<ProviderPredicateDefinition> { predicate },
            ExcludeFieldsByItemType = sourceMode.ExcludeFieldsByItemType,
            ExcludeXmlElementsByType = sourceMode.ExcludeXmlElementsByType,
            ConflictStrategy = sourceMode.ConflictStrategy
        }
    };
}
```

Or thread the `DeploymentMode` and the owning `SerializerConfiguration` through, and stop synthesizing a temp config at all — ContentDeserializer is entry-point aware enough to read `config.GetMode(mode).*` if given the mode.

### WR-05: `SqlTableWriter.DisableForeignKeys` failure is silently swallowed in a broad `try { } catch { }`

**File:** `src/DynamicWeb.Serializer/Providers/SqlTable/SqlTableProvider.cs:241-243`

**Issue:**

```csharp
if (!isDryRun)
{
    try { _writer.DisableForeignKeys(metadata.TableName); }
    catch { /* Table may not have FK constraints */ }
}
```

The empty catch swallows every exception type — including transient DB connection errors, permission denials, and deadlocks. If the `ALTER TABLE ... NOCHECK CONSTRAINT ALL` fails for any reason other than "no FKs," the subsequent MERGE runs with enabled FKs but the caller gets no warning, and the end-of-run `EnableForeignKeys` at 328-330 also silently succeeds-as-noop because the disable never happened.

Additionally, `NOCHECK CONSTRAINT ALL` against a table that has no FKs never actually throws on SQL Server — it's a no-op. So the justification in the comment ("Table may not have FK constraints") is misleading: the `catch` was probably written to tolerate insufficient `ALTER TABLE` permissions. Either way, silent failure in the FK-disable path is a data-integrity risk (MERGE into a child table can break FK constraints if FKs are enforced, cascading into failed inserts that are reported as generic row failures later).

**Fix:** Log the exception at WARNING level (so StrictModeEscalator can escalate it in strict mode) and consider propagating as a genuine error:

```csharp
if (!isDryRun)
{
    try { _writer.DisableForeignKeys(metadata.TableName); }
    catch (Exception ex)
    {
        Log($"WARNING: Could not disable FK constraints for [{metadata.TableName}]: {ex.Message} — MERGE may fail on child-table ordering.", log);
    }
}
```

### WR-06: `SqlTableWriter.RowExistsInTarget` builds the CommandBuilder twice, dead code + subtle bug risk

**File:** `src/DynamicWeb.Serializer/Providers/SqlTable/SqlTableWriter.cs:329-360`

**Issue:** The method constructs and then discards a `CommandBuilder` plus a `conditions` `List<string>`, then re-creates a fresh `CommandBuilder` for the actual query. The first block (lines 331-343) is dead code — `cb` is reassigned at 346 before any use. The `condCb` variable at 339-341 is never used. The dead code is harmless but obscures the real logic and creates maintenance risk (future reviewer may think the first pass still matters and modify both halves inconsistently).

Second concern: the actual query at 346-357 reads `WHERE ` with a trailing space, then `cb.Add($"[{keyCol}] = ")` — interpolating the key column name. If `keyCol` is ever set from untrusted input (it currently comes from `metadata.KeyColumns` which comes from `_meta.yml` deserialization — so an attacker with write access to the YAML baseline could craft `keyCol = "X] = '1' OR 1=1--"`), the identifier injection vector re-opens. This is a corollary of CR-01 for the `KeyColumns` path.

**Fix:** Delete the dead code (lines 331-343) and add an identifier-whitelist check for each `keyCol` (ideally against the already-loaded `TargetSchemaCache` columns) before interpolation:

```csharp
public bool RowExistsInTarget(TableMetadata metadata, Dictionary<string, object?> row)
{
    var cb = new CommandBuilder();
    cb.Add($"SELECT 1 FROM [{metadata.TableName}] WHERE ");

    for (int i = 0; i < metadata.KeyColumns.Count; i++)
    {
        if (i > 0) cb.Add(" AND ");
        var keyCol = metadata.KeyColumns[i];
        // ... assume caller validated keyCol (document in xmldoc) ...
        var value = row.TryGetValue(keyCol, out var v) ? v ?? DBNull.Value : DBNull.Value;
        cb.Add($"[{keyCol}] = ");
        cb.Add("{0}", value);
    }

    using var reader = _sqlExecutor.ExecuteReader(cb);
    return reader.Read();
}
```

### WR-07: `InternalLinkResolver.ResolveLinks` uses `_resolvedCount` for both raw-numeric AND regex hits without distinction; stats underreport

**File:** `src/DynamicWeb.Serializer/Serialization/InternalLinkResolver.cs:71-94`

**Issue:** The resolver increments `_resolvedCount` when it detects a raw-numeric page-ID string (line 74) and when the regex matches a `Default.aspx?ID=N` reference (line 94). But it does not increment `_resolvedCount` when the `SelectedValuePattern` regex matches and rewrites successfully (line 82-83 — resolve-and-rewrite returns the new string but the counter is only bumped for `InternalLinkPattern`). So `_resolvedCount` misses every `"SelectedValue": "N"` button-editor rewrite, leading `GetStats()` to under-report.

Similarly, `_unresolvedCount` is only bumped in the `InternalLinkPattern.Replace` callback — `SelectedValuePattern.Replace` does not track unresolved IDs, so a broken button-editor reference is silently preserved with no stat and no warning log.

**Fix:** Apply the same resolved/unresolved accounting inside the `SelectedValuePattern.Replace` lambda:

```csharp
fieldValue = SelectedValuePattern.Replace(fieldValue, match =>
{
    var sourceId = int.Parse(match.Groups[2].Value);
    if (_sourceToTargetPageIds.TryGetValue(sourceId, out var targetId))
    {
        _resolvedCount++;
        return match.Groups[1].Value + targetId.ToString() + match.Groups[3].Value;
    }
    _log?.Invoke($"  WARNING: Unresolvable page ID {sourceId} in SelectedValue");
    _unresolvedCount++;
    return match.Value;
});
```

### WR-08: `ContentDeserializer` iterates `Directory.GetDirectories(contentRoot)` without validating the path is inside the expected Files/ tree

**File:** `src/DynamicWeb.Serializer/Serialization/ContentDeserializer.cs:148-161`
**File:** `src/DynamicWeb.Serializer/Providers/Content/ContentProvider.cs:189-206`

**Issue:** Both paths walk every subdirectory of `contentRoot` (from YAML on disk) with no containment check. A malicious `OutputDirectory` setting (e.g., someone crafts a config with `OutputDirectory: "../../../Windows/System32"`) would cause `Directory.GetDirectories` to enumerate outside the intended Files/ tree — not dangerous for a *read* pass (nothing is deleted), but the YAML deserializer is then run over whatever `area.yml` it finds there, which could trigger DoS via a very large file or expose information via error messages.

The `OutputDirectory` is routed through `SerializerConfiguration.EnsureDirectories` which does `Path.GetFullPath(Path.Combine(filesSystemDir, OutputDirectory.TrimStart('\\', '/')))` but does not verify the result stays under `filesSystemDir`. A path like `"../../sensitive"` escapes.

**Fix:** In `SerializerConfiguration.EnsureDirectories`, add a containment check on `resolved.Root` vs `filesSystemDir`:

```csharp
var filesSystemRealRoot = Path.GetFullPath(filesSystemDir);
if (!resolved.Root.StartsWith(filesSystemRealRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
    && !string.Equals(resolved.Root, filesSystemRealRoot, StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException(
        $"OutputDirectory '{OutputDirectory}' resolves to '{resolved.Root}' which is outside the Files/ tree '{filesSystemRealRoot}'. Refusing to continue.");
}
```

### WR-09: `ConfigPathResolver` static `CandidatePaths` array captures `AppDomain.CurrentDomain.BaseDirectory` and `Directory.GetCurrentDirectory()` at type-init time

**File:** `src/DynamicWeb.Serializer/Configuration/ConfigPathResolver.cs:7-18`

**Issue:** The `CandidatePaths` static array evaluates `AppDomain.CurrentDomain.BaseDirectory` and `Directory.GetCurrentDirectory()` in a field initializer — meaning the paths are captured when the type is first loaded, never refreshed. If the host changes working directory after startup (e.g., a test harness that cds during xUnit parallel runs — this codebase already has xUnit parallelism), later calls to `FindConfigFile` use stale paths.

The AsyncLocal-based `TestOverridePath` escape hatch (line 26-31) explicitly exists to work around this in tests, which is a hint that the underlying design is fragile. The comment at 22-25 acknowledges the test-parallel issue.

**Fix:** Make `CandidatePaths` a method, not a static array:

```csharp
private static IEnumerable<string> GetCandidatePaths() => new[]
{
    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "wwwroot", "Files", "Serializer.config.json"),
    Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Files", "Serializer.config.json"),
    // ...
};

public static string DefaultPath => Path.GetFullPath(GetCandidatePaths().First());

public static string? FindConfigFile()
{
    var overridePath = TestOverridePath;
    if (overridePath != null)
        return File.Exists(overridePath) ? Path.GetFullPath(overridePath) : null;

    foreach (var path in GetCandidatePaths())
    {
        if (File.Exists(path)) return Path.GetFullPath(path);
    }
    return null;
}
```

### WR-10: `SerializerOrchestrator.DeserializeAll` reorders predicates list in-place via reassignment — mutates caller's list reference

**File:** `src/DynamicWeb.Serializer/Providers/SerializerOrchestrator.cs:183,208`

**Issue:** At 183, `predicates = sortedSqlPredicates.Concat(nonSqlPredicates).ToList()` — this rebinds the local parameter but doesn't mutate the caller's list. OK. At 208, same pattern. OK.

**But:** the method takes `predicates` as `List<ProviderPredicateDefinition>` by value (by reference to the list, though). Nothing in the contract promises the original list ordering is preserved. Downstream callers that iterate `modeConfig.Predicates` after calling `DeserializeAll` will see the original order (because the local `predicates` was rebound), but this is by accident — any refactor that moves the reorder to mutate in place would break silently.

**Fix:** Make the reordering intent explicit by copying into a local once at the top and working only with the local:

```csharp
var orderedPredicates = predicates.ToList();  // defensive copy
// ... all reorder logic mutates orderedPredicates ...
foreach (var predicate in orderedPredicates) { ... }
```

This also makes `anySqlNeedsLinks`-based reorder race-free if someone ever makes `DeserializeAll` thread-safe.

### WR-11: `SqlTableProvider.Deserialize` catches all exceptions during `DisableForeignKeys` but lets `EnableForeignKeys` throw silently via broad catch

**File:** `src/DynamicWeb.Serializer/Providers/SqlTable/SqlTableProvider.cs:327-331`

**Issue:** Mirror of WR-05 on the enable side. Re-enable failure logs a warning (good!) but the warning message doesn't route through an escalator — it just calls `Log(...)` with "WARNING:" prefix. Under strict mode, the orchestrator's `WrapLogWithEscalator` should pick this up via the `WARNING:` prefix matcher… **except the log lambda here has 2 leading spaces (`"  WARNING:"`)**, and `WrapLogWithEscalator` at `SerializerOrchestrator.cs:340` does `msg.TrimStart().StartsWith("WARNING", ...)`. That actually does handle leading whitespace. So this is fine in current code, but the implicit dependency between "log message prefix must be WARNING" and "strict mode escalation catches it" is brittle.

**Fix:** Use the escalator pattern explicitly (passed through from `DeserializeAll` via a new ctor param), rather than relying on log-line prefix matching:

```csharp
public override ProviderDeserializeResult Deserialize(
    ProviderPredicateDefinition predicate,
    string inputRoot,
    Action<string>? log = null,
    bool isDryRun = false,
    ConflictStrategy strategy = ConflictStrategy.SourceWins,
    InternalLinkResolver? linkResolver = null,
    StrictModeEscalator? escalator = null)  // NEW
{
    escalator ??= StrictModeEscalator.Null;
    // ...
    catch (Exception ex) { escalator.Escalate($"Could not re-enable FK constraints for [{metadata.TableName}]: {ex.Message}"); }
}
```

This also requires threading escalator through `ISerializationProvider.Deserialize`, which is a breaking change — justified given Phase 37's scope.

## Info

### IN-01: `SqlWhereClauseValidator.BannedTokens` substring-matches across literals — correctly conservative but comment contradicts code

**File:** `src/DynamicWeb.Serializer/Configuration/SqlWhereClauseValidator.cs:43-50,175-196`

**Issue:** The comment at 43-44 says "catches `;`, `--`, `/*`, `xp_`, `sp_executesql` even when hidden inside string literals — conservative, safer than silent allow." The test `Validate_StringLiteralWithBannedKeyword_DoesNotLeakOut` at test line 177 asserts that `'Admin Select Group'` is accepted. But `SELECT` is in `BannedKeywords`, not `BannedTokens`, so the keyword check only runs against stripped (literal-elided) tokens — that's why the test passes. The BannedTokens check at lines 45-50 DOES scan the raw string including literals, which is what rejects `'not;ok'` in the next test.

So the behavior is: literal content can contain banned *keywords* (harmless — they're still inside quotes) but not banned *tokens* (`;`, `--`, etc.). That's a reasonable split. But the code comment could read as if BannedTokens are always searched including literals in ALL tokens — which is misleading about the role of the keyword list.

**Fix:** Tighten the comment at 19-24 to say "Keyword-bans: whole-word match on the *literal-stripped* clause. Token-bans below still scan the raw clause."

### IN-02: `BaselineLinkSweeper` regex patterns missing ReDoS hardening — cap input length

**File:** `src/DynamicWeb.Serializer/Infrastructure/BaselineLinkSweeper.cs:33-39`

**Issue:** The two compiled regex patterns (`InternalLinkPattern` and `SelectedValuePattern`) use `\d+` which is bounded by the input length, not a catastrophic-backtracking vector. However `CheckField` calls `.Matches(value)` on every string field it encounters, with no length cap. A malicious baseline YAML with a 500MB string field would force the regex engine to walk the full input once per `Matches` call. T-37-05-05 addresses the manifest reference cap, but individual field values aren't capped.

**Fix:** Cap per-field input length before running regex:

```csharp
private const int MaxFieldScanLength = 1_000_000; // 1 MB per field

private static void CheckField(string? value, ...)
{
    if (string.IsNullOrEmpty(value)) return;
    if (value.Length > MaxFieldScanLength)
    {
        unresolved.Add(new UnresolvedLink(sourceIdent, fieldName, -1,
            $"Field exceeds {MaxFieldScanLength} chars — refusing to scan."));
        return;
    }
    // ...
}
```

### IN-03: `TargetSchemaCache.DefaultLoader` uses `reader.GetString(0)` / `GetString(1)` without null-safety

**File:** `src/DynamicWeb.Serializer/Infrastructure/TargetSchemaCache.cs:43-47`

**Issue:** `INFORMATION_SCHEMA.COLUMNS.COLUMN_NAME` and `DATA_TYPE` are NOT NULL by spec, so `reader.GetString(0)` and `.GetString(1)` are always safe. But `SqlIdentifierValidator.DefaultTableLoader` at `SqlIdentifierValidator.cs:91` has the same pattern for `TABLE_NAME` — also NOT NULL. Defensively, these could use `reader.IsDBNull(0)` guards to avoid tight coupling to the spec.

**Fix:** Minor — add null guards or a comment justifying the absence.

### IN-04: `FlatFileStore.ReadAllRows` yields a new case-insensitive dictionary per row, rebuilding from the YAML-deserialized dict — O(n) memory duplication

**File:** `src/DynamicWeb.Serializer/Providers/SqlTable/FlatFileStore.cs:84-93`

**Issue:** Each row is deserialized into a default-comparer dict, then copied into a case-insensitive one. For large tables this doubles memory per row read. `YamlDotNet` supports passing a pre-constructed dictionary type, but the cleaner fix is to deserialize straight into the case-insensitive form. Not a correctness issue, just unnecessary allocation.

**Fix:** Use a custom `IDeserializer` convention or a post-step. (Left as info — doesn't affect v0.5.0 correctness.)

### IN-05: `DeletePredicateCommand.Handle` does not catch `InvalidOperationException` from `ConfigLoader.Load` — unhandled exception surfaces to caller

**File:** `src/DynamicWeb.Serializer/AdminUI/Commands/DeletePredicateCommand.cs:21-43`

**Issue:** All sibling commands wrap `ConfigLoader.Load` in a `try/catch(Exception)` and return a `CommandResult` with `Error` status. `DeletePredicateCommand` omits that — if the config is malformed (schema validation failure, etc.), the admin UI gets a raw exception bubble-up.

**Fix:**

```csharp
public override CommandResult Handle()
{
    try
    {
        var configPath = ConfigPath ?? ConfigPathResolver.FindConfigFile();
        if (configPath == null)
            return new() { Status = CommandResult.ResultType.Error, Message = "Config file not found" };

        var config = ConfigLoader.Load(configPath);
        // ... rest unchanged
    }
    catch (Exception ex)
    {
        return new() { Status = CommandResult.ResultType.Error, Message = ex.Message };
    }
}
```

### IN-06: `SerializerSettingsEditScreen.CreateConflictStrategySelect` only offers one option — UI shows a single-option dropdown

**File:** `src/DynamicWeb.Serializer/AdminUI/Screens/SerializerSettingsEditScreen.cs:111-121`

**Issue:** The Select only has `{ Value = "source-wins", Label = "Source Wins" }` — no `destination-wins` option. The model still accepts it via the free-form string setter, and `SaveSerializerSettingsCommand.cs:52-57` handles both. But the UI makes it impossible to pick `destination-wins` from the settings screen. Given that Phase 37 explicitly puts Deploy on source-wins and Seed on destination-wins (D-05/D-06), this may be intentional — Deploy's conflict strategy is locked to source-wins in the settings UI because Seed has its own implicit strategy.

If intentional, please add a comment explaining why — the split currently looks like a forgotten option.

**Fix:** Either add `destination-wins` as a valid option (with explanatory label), or add a code comment at line 111 clarifying that Deploy's strategy is intentionally locked to source-wins.

### IN-07: `SerializeSubtreeCommand` leaves the zip file in `Path.GetTempPath()/Serializer/` after download — accumulates on disk

**File:** `src/DynamicWeb.Serializer/AdminUI/Commands/SerializeSubtreeCommand.cs:71-93`

**Issue:** The command writes `zipPath` to temp (line 72-87), copies to Download dir (line 90), and returns a `FileStream` pointing at the temp file (line 96). The temp zip is never cleaned up — every subtree export leaves a `~/tmp/Serializer/Serializer_*.zip` file behind. Admins triggering this repeatedly during development will fill temp.

**Fix:** Use a `FileShare.Delete` stream (which this already does) combined with a scheduled cleanup sweep at the start of each invocation:

```csharp
// Near the top of Handle():
try
{
    var serializerTempRoot = Path.Combine(Path.GetTempPath(), "Serializer");
    if (Directory.Exists(serializerTempRoot))
    {
        var cutoff = DateTime.UtcNow.AddHours(-1);
        foreach (var oldFile in Directory.EnumerateFiles(serializerTempRoot, "*.zip"))
        {
            if (File.GetLastWriteTimeUtc(oldFile) < cutoff)
                try { File.Delete(oldFile); } catch { }
        }
    }
}
catch { /* best effort */ }
```

### IN-08: `ConfigLoader.BuildModeConfigs` prints to `Console.Error` — leaks into CLI stderr regardless of LogLevel setting

**File:** `src/DynamicWeb.Serializer/Configuration/ConfigLoader.cs:43-45,264-267`

**Issue:** Two `Console.Error.WriteLine` calls that bypass the configured logging pipeline. For CI/CD use (Azure pipelines), stderr output still gets captured but the configurable `LogLevel` setting has no effect on these lines.

**Fix:** Thread an optional `Action<string>?` log into `ConfigLoader.Load` (or add a `Trace.WriteLine` equivalent) so operators can silence/route these messages via the same pipeline as everything else.

### IN-09: `StrictModeEscalator` public constant `MaxRecordedWarnings = 10_000` — no exposed way to override for large baselines

**File:** `src/DynamicWeb.Serializer/Infrastructure/StrictModeEscalator.cs:21`

**Issue:** The 10k cap is a sensible DoS guard for Swift 2.2's ~1500 pages. But for a hypothetical 100k-page multi-tenant install, strict mode would silently drop warnings past 10k and `AssertNoWarnings` would throw only with the first 10k captured. The operator has no insight into "how many warnings were silently dropped."

**Fix:** Track the drop count separately and expose it in `CumulativeStrictModeException`:

```csharp
public const int MaxRecordedWarnings = 10_000;
private int _droppedWarningCount;

public void Escalate(string warning)
{
    // ... existing log logic ...
    if (_strict)
    {
        if (_recordedWarnings.Count < MaxRecordedWarnings)
            _recordedWarnings.Add(warning);
        else
            _droppedWarningCount++;
    }
}

public void AssertNoWarnings()
{
    if (!_strict || _recordedWarnings.Count == 0) return;
    var suffix = _droppedWarningCount > 0
        ? $" (+ {_droppedWarningCount} additional warnings not recorded — cap reached)"
        : "";
    throw new CumulativeStrictModeException(_recordedWarnings, suffix);
}
```

---

_Reviewed: 2026-04-20_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
