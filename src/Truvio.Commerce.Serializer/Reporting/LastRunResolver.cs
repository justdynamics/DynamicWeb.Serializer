using Truvio.Commerce.Serializer.Configuration;
using Truvio.Commerce.Serializer.Infrastructure;
using Truvio.Commerce.Serializer.Models;

namespace Truvio.Commerce.Serializer.Reporting;

/// <summary>
/// Answers "when did this environment last receive a replace/merge?" from the run logs:
/// the newest non-dry-run summary per operation + mode, read via the existing
/// <see cref="LogFileWriter"/> summary headers. Logs written before the summary carried
/// a <c>Mode</c> field never match a mode filter — timestamps appear after the first run
/// on the current version. Everything here is best-effort: an unreadable log file is
/// skipped, a missing log directory yields null.
/// </summary>
public static class LastRunResolver
{
    /// <summary>Operations that land data ON this environment (replace/merge received).</summary>
    private static readonly string[] ReceiveOperations = { "Deserialize", "ZipImport" };

    /// <summary>
    /// Newest non-dry-run summary whose Operation is one of <paramref name="operations"/>
    /// and whose Mode equals <paramref name="mode"/> (lowercase). Null when none exists.
    /// </summary>
    public static LogFileSummary? FindLastRun(string logDir, IReadOnlyList<string> operations, string mode)
    {
        foreach (var file in LogFileWriter.GetLogFiles(logDir))
        {
            try
            {
                var summary = LogFileWriter.ParseSummaryHeader(file.FullName);
                if (summary is null || summary.DryRun)
                    continue;
                if (!operations.Any(o => string.Equals(o, summary.Operation, StringComparison.OrdinalIgnoreCase)))
                    continue;
                if (!string.Equals(mode, summary.Mode, StringComparison.OrdinalIgnoreCase))
                    continue;
                return summary;
            }
            catch
            {
                // Unreadable/foreign log file — skip and keep scanning.
            }
        }
        return null;
    }

    /// <summary>Newest real (non-dry-run) deserialize/zip-import summary for a mode.</summary>
    public static LogFileSummary? FindLastReceived(string logDir, string mode)
        => FindLastRun(logDir, ReceiveOperations, mode);

    /// <summary>
    /// UTC timestamp of the last real replace received by this environment, resolved from
    /// the canonical log directory next to the config file. Null when unknown.
    /// </summary>
    public static DateTime? FindLastReplaceReceivedUtc()
    {
        try
        {
            var configPath = ConfigPathResolver.FindConfigFile();
            if (configPath is null)
                return null;
            return FindLastReceived(GetLogDir(configPath), "replace")?.Timestamp;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Canonical log directory for a resolved config path (no directory creation).</summary>
    public static string GetLogDir(string configPath) =>
        Path.Combine(ConfigPathResolver.GetFilesRoot(configPath),
            "System", SerializerPathResolver.DefaultOutputDirectory, "Log");
}
