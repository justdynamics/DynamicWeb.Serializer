using System.Text.Json;
using Truvio.Commerce.Serializer.Models;

namespace Truvio.Commerce.Serializer.Infrastructure;

/// <summary>
/// Creates per-run log files with JSON summary headers for the Serializer.
/// Each operation run produces a separate timestamped log file.
/// </summary>
public static class LogFileWriter
{
    private const string SummaryStartMarker = "=== SERIALIZER SUMMARY ===";
    private const string SummaryEndMarker = "=== END SUMMARY ===";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Creates a new log file for the given operation with a timestamped filename.
    /// Creates the directory if it doesn't exist. The optional suffix carries the mode
    /// (and dry-run marker) so runs are distinguishable from the filename alone, e.g.
    /// <c>Deserialize_deploy_2026-06-11_142800.log</c>.
    /// </summary>
    public static string CreateLogFile(string logDir, string operation, string? suffix = null)
    {
        if (!Directory.Exists(logDir))
            Directory.CreateDirectory(logDir);

        var stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
        var fileName = string.IsNullOrEmpty(suffix)
            ? $"{operation}_{stamp}.log"
            : $"{operation}_{suffix}_{stamp}.log";
        var path = Path.Combine(logDir, fileName);
        File.WriteAllText(path, "");
        return path;
    }

    /// <summary>
    /// Writes a JSON summary block between marker lines at the start of the log file.
    /// </summary>
    public static void WriteSummaryHeader(string logFile, LogFileSummary summary)
    {
        var json = JsonSerializer.Serialize(summary, JsonOptions);
        var header = $"{SummaryStartMarker}\n{json}\n{SummaryEndMarker}\n";

        var existing = File.Exists(logFile) ? File.ReadAllText(logFile) : "";
        File.WriteAllText(logFile, header + existing);
    }

    /// <summary>
    /// Appends a timestamped log line to the file.
    /// </summary>
    public static void AppendLogLine(string logFile, string message)
    {
        File.AppendAllText(logFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}\n");
    }

    /// <summary>
    /// Parses the JSON summary header from a log file.
    /// Returns null if the summary markers are not found.
    /// </summary>
    public static LogFileSummary? ParseSummaryHeader(string logFile)
    {
        if (!File.Exists(logFile))
            return null;

        var content = File.ReadAllText(logFile);
        var startIdx = content.IndexOf(SummaryStartMarker, StringComparison.Ordinal);
        var endIdx = content.IndexOf(SummaryEndMarker, StringComparison.Ordinal);

        if (startIdx < 0 || endIdx < 0 || endIdx <= startIdx)
            return null;

        var jsonStart = startIdx + SummaryStartMarker.Length;
        var json = content[jsonStart..endIdx].Trim();

        return JsonSerializer.Deserialize<LogFileSummary>(json, JsonOptions);
    }

    /// <summary>
    /// Returns all .log files in the directory, sorted by last write time descending (most recent first).
    /// </summary>
    public static FileInfo[] GetLogFiles(string logDir)
    {
        if (!Directory.Exists(logDir))
            return Array.Empty<FileInfo>();

        return new DirectoryInfo(logDir)
            .GetFiles("*.log")
            .OrderByDescending(f => f.LastWriteTime)
            .ToArray();
    }
}
