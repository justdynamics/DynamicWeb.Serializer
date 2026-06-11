using Truvio.Commerce.Serializer.AdminUI.Models;
using Truvio.Commerce.Serializer.Configuration;
using Truvio.Commerce.Serializer.Infrastructure;
using Truvio.Commerce.Serializer.Models;
using Truvio.Commerce.Serializer.Providers;
using Truvio.Commerce.Serializer.Providers.Content;
using Truvio.Commerce.Serializer.Reporting;
using Dynamicweb.CoreUI.Data;
using System.IO.Compression;

namespace Truvio.Commerce.Serializer.AdminUI.Commands;

/// <summary>
/// Command that imports content from an extracted zip directly into the target area.
///
/// <para>
/// Phase 44 / CONVERGE-01 + CONVERGE-02 (D-01..D-03): zip-import now routes through the
/// shared <see cref="SerializerOrchestrator"/> pipeline via the new public
/// <c>DeserializeAll(Manifest, contentRoot, ...)</c> overload (D-01). The shape source
/// is the same <see cref="ContentProvider.BuildContentEntryForArea(int, string, IEnumerable{int}?, IEnumerable{string}?)"/>
/// helper used by the full deserialize path (D-02), so zip-import and full-deserialize
/// share one canonical <see cref="ContentEntry"/> construction site. Strict-mode wiring
/// uses the same <see cref="StrictModeResolver"/> literal as
/// <see cref="SerializerDeserializeCommand"/> (D-03), closing the silent strict-mode bypass
/// that previously existed on zip-import.
/// </para>
///
/// Zip is extracted to Files/System/Serializer/ZipImport/ and cleaned up after.
/// </summary>
public sealed class DeserializeFromZipCommand : CommandBase<DeserializeFromZipModel>
{
    public string FilePath { get; set; } = "";

    public int TargetAreaId { get; set; }

    /// <summary>
    /// Phase 44 / CONVERGE-02 + D-03: per-call strict-mode override mirroring
    /// <see cref="SerializerDeserializeCommand.StrictMode"/>. Null = use the entry-point
    /// default (admin-UI invocation → lenient, API/CLI → strict). Explicit true/false
    /// overrides.
    /// </summary>
    public bool? StrictMode { get; set; }

    /// <summary>
    /// Phase 44 / CONVERGE-02 + D-03: admin-UI invocation flag — flips entry-point default
    /// to AdminUi (lenient) per D-16 / D-38-11 precedent. Not bound from JSON body / query
    /// string (mirrors <see cref="SerializerDeserializeCommand.IsAdminUiInvocation"/>).
    /// </summary>
    public bool IsAdminUiInvocation { get; set; } = false;

    /// <summary>Dry-run preview: runs the import pipeline without writing anything,
    /// mirroring <see cref="SerializerDeserializeCommand.IsDryRun"/>.</summary>
    public bool IsDryRun { get; set; }

    private readonly List<string> _logLines = new();

    private void Log(string message)
    {
        _logLines.Add($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}");
    }

    private void FlushLog(string logFile, LogFileSummary summary)
    {
        LogFileWriter.WriteSummaryHeader(logFile, summary);
        foreach (var line in _logLines)
            File.AppendAllText(logFile, line + "\n");
    }

    public override CommandResult Handle()
    {
        try
        {
            if (TargetAreaId <= 0)
                return new() { Status = CommandResult.ResultType.Invalid, Message = "Target area is required" };

            var configPath = ConfigPathResolver.FindConfigFile();
            if (configPath == null)
                return new() { Status = CommandResult.ResultType.Error, Message = "Serializer.config.json not found" };

            var filesRoot = ConfigPathResolver.GetFilesRoot(configPath);
            var systemDir = Path.Combine(filesRoot, "System");
            var paths = SerializerPathResolver.EnsureDirectories(systemDir);

            // Use the ZipImport directory under System/Serializer/
            var zipImportDir = Path.Combine(filesRoot, "System", "Serializer", "ZipImport");

            // Clean and recreate
            if (Directory.Exists(zipImportDir))
                Directory.Delete(zipImportDir, recursive: true);
            Directory.CreateDirectory(zipImportDir);

            // Extract zip
            var physicalZipPath = Dynamicweb.Core.SystemInformation.MapPath(FilePath);
            if (!File.Exists(physicalZipPath))
                return new() { Status = CommandResult.ResultType.Error, Message = $"Zip file not found: {FilePath}" };

            ZipFile.ExtractToDirectory(physicalZipPath, zipImportDir);

            // Create log file
            var logFile = LogFileWriter.CreateLogFile(paths.Log, "ZipImport",
                IsDryRun ? "deploy-dryrun" : "deploy");
            Log("=== Serializer ZipImport started ===");
            Log($"Source zip: {FilePath}");
            Log($"Target area: {TargetAreaId}");

            // Phase 44 / CONVERGE-01 + D-02: build in-memory manifest via shared helper.
            // acknowledgedOrphanPageIds: zip-import has no orphan-acknowledgement surface today
            // (Claude's Discretion call in CONTEXT line 71 — hardcoded empty list, not exposed
            // as a property on the command).
            var contentEntry = ContentProvider.BuildContentEntryForArea(
                TargetAreaId, zipImportDir, acknowledgedOrphanPageIds: null);

            var manifest = new Manifest
            {
                SchemaVersion = ManifestSchema.CurrentVersion,
                Mode = "deploy",
                WrittenAtUtc = DateTime.UtcNow,
                Complete = true,
                ExcludeFieldsByItemType = new Dictionary<string, List<string>>(),
                ExcludeXmlElementsByType = new Dictionary<string, List<string>>(),
                Entries = new List<ManifestEntry> { contentEntry }
            };

            // Phase 44 / D-03: strict-mode wiring via StrictModeResolver — same grep-friendly
            // literal as SerializerDeserializeCommand. The configValue: null literal is
            // preserved verbatim per Phase 43 D-04 precedent (config.StrictMode no longer
            // consulted on the deserialize hot path).
            var entryPoint = IsAdminUiInvocation
                ? StrictModeResolver.EntryPoint.AdminUi
                : StrictModeResolver.EntryPoint.Api;
            var strict = StrictModeResolver.Resolve(entryPoint, configValue: null, requestValue: StrictMode);
            Log($"=== Strict mode: {strict} (entry-point: {entryPoint}) ===");

            var escalator = new StrictModeEscalator(strict, Log);

            // Phase 44 / CONVERGE-02: orchestrator pipeline — single canonical dispatch site
            // for full-deserialize + zip-import. The new DeserializeAll(Manifest, ...) overload
            // (D-01) accepts an in-memory manifest, eliminating the previous synthetic
            // SerializerConfiguration path + direct ContentDeserializer call.
            var orchestrator = ProviderRegistry.CreateOrchestrator(filesRoot);
            var result = orchestrator.DeserializeAll(
                manifest, zipImportDir, DeploymentMode.Deploy, ConflictStrategy.SourceWins,
                Log, isDryRun: IsDryRun, providerFilter: null, escalator);

            // Build summary from EntryOutcomes (mirrors SerializerDeserializeCommand pattern).
            var summary = new LogFileSummary
            {
                Operation = "ZipImport",
                Mode = "deploy",
                DryRun = IsDryRun,
                Timestamp = DateTime.UtcNow,
                Predicates = result.EntryOutcomes
                    .Where(o => o.EntryId != EntryOutcome.RunLevelEntryId)  // Phase 44 / IN-06: filter run-level synthesis
                    .Select(o => new PredicateSummary
                    {
                        Name = o.EntryId,
                        Table = o.EntryId,
                        Created = o.Counts.Created,
                        Updated = o.Counts.Updated,
                        Skipped = o.Counts.Skipped,
                        Failed = o.Counts.Failed,
                        Errors = o.Errors.ToList()
                    }).ToList(),
                TotalCreated = result.EntryOutcomes.Sum(o => o.Counts.Created),
                TotalUpdated = result.EntryOutcomes.Sum(o => o.Counts.Updated),
                TotalSkipped = result.EntryOutcomes.Sum(o => o.Counts.Skipped),
                TotalFailed = result.EntryOutcomes.Sum(o => o.Counts.Failed),
                Errors = result.Errors.ToList()
            };
            FlushLog(logFile, summary);

            // Clean up ZipImport dir
            try { Directory.Delete(zipImportDir, recursive: true); }
            catch { /* best effort */ }

            var message = result.Summary;
            if (result.HasErrors)
                message += $" Errors: {string.Join("; ", result.Errors)}";

            return new CommandResult
            {
                Status = result.HasErrors ? CommandResult.ResultType.Error : CommandResult.ResultType.Ok,
                Message = message
            };
        }
        catch (Exception ex)
        {
            return new() { Status = CommandResult.ResultType.Error, Message = $"Zip import failed: {ex.Message}" };
        }
    }
}
