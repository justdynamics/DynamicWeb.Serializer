using Truvio.Commerce.Serializer.Configuration;
using Truvio.Commerce.Serializer.Infrastructure;
using Truvio.Commerce.Serializer.Models;
using Dynamicweb.CoreUI.Data;

namespace Truvio.Commerce.Serializer.AdminUI.Models;

public sealed class LogViewerModel : DataViewModelBase
{
    [ConfigurableProperty("Log File", explanation: "Select a log file to view")]
    public string SelectedFileName { get; set; } = string.Empty;

    [ConfigurableProperty("Has Logs")]
    public bool HasLogs { get; set; }

    public List<string> AvailableLogFiles { get; set; } = new();

    public LogFileSummary? Summary { get; set; }

    // Flattened summary fields for EditorFor binding
    [ConfigurableProperty("Operation", explanation: "The operation that produced this log")]
    public string SummaryOperation { get; set; } = string.Empty;

    [ConfigurableProperty("Timestamp", explanation: "When the operation ran")]
    public string SummaryTimestamp { get; set; } = string.Empty;

    [ConfigurableProperty("Total Created", explanation: "Total items created")]
    public string SummaryTotalCreated { get; set; } = "0";

    [ConfigurableProperty("Total Updated", explanation: "Total items updated")]
    public string SummaryTotalUpdated { get; set; } = "0";

    [ConfigurableProperty("Total Skipped", explanation: "Total items skipped")]
    public string SummaryTotalSkipped { get; set; } = "0";

    [ConfigurableProperty("Total Failed", explanation: "Total items that failed")]
    public string SummaryTotalFailed { get; set; } = "0";

    [ConfigurableProperty("Predicate Breakdown", explanation: "Per-predicate summary")]
    public string PredicateBreakdown { get; set; } = string.Empty;

    [ConfigurableProperty("Advice", explanation: "Guided recommendations based on errors")]
    public string AdviceText { get; set; } = string.Empty;

    [ConfigurableProperty("Log Output", explanation: "Raw log text from the selected file")]
    public string RawLogText { get; set; } = string.Empty;

    // Resolved log directory path, set during Load
    private string? _logDir;

    /// <summary>
    /// Loads the file list and default selection. Called by query.
    /// </summary>
    public static LogViewerModel Load(string? selectedFile)
    {
        var model = new LogViewerModel();

        var configPath = ConfigPathResolver.FindConfigFile();
        if (configPath == null)
            return model;

        var config = ConfigLoader.Load(configPath);
        var filesRoot = ConfigPathResolver.GetFilesRoot(configPath);
        var systemDir = Path.Combine(filesRoot, "System");
        var paths = config.EnsureDirectories(systemDir);

        var logFiles = LogFileWriter.GetLogFiles(paths.Log);
        if (logFiles.Length == 0)
            return model;

        model.HasLogs = true;
        model._logDir = paths.Log;
        model.AvailableLogFiles = logFiles.Select(f => f.Name).ToList();

        // Select the requested file, or default to most recent
        var targetFileName = logFiles[0].Name;
        if (!string.IsNullOrEmpty(selectedFile))
        {
            var match = logFiles.FirstOrDefault(f =>
                f.Name.Equals(selectedFile, StringComparison.OrdinalIgnoreCase));
            if (match != null)
                targetFileName = match.Name;
        }

        model.SelectedFileName = targetFileName;
        model.LoadFileData(paths.Log, targetFileName);

        return model;
    }

    /// <summary>
    /// Reloads file-specific data for the currently selected file.
    /// Called by the screen's BuildEditScreen after ShadowEdit overlays the selection.
    /// </summary>
    public void ReloadSelectedFile()
    {
        if (string.IsNullOrEmpty(SelectedFileName) || string.IsNullOrEmpty(_logDir))
        {
            // Try to resolve log dir fresh if not cached
            var configPath = ConfigPathResolver.FindConfigFile();
            if (configPath == null) return;
            var config = ConfigLoader.Load(configPath);
            var filesRoot = ConfigPathResolver.GetFilesRoot(configPath);
            var systemDir = Path.Combine(filesRoot, "System");
            var paths = config.EnsureDirectories(systemDir);
            _logDir = paths.Log;
        }

        if (!string.IsNullOrEmpty(_logDir) && !string.IsNullOrEmpty(SelectedFileName))
            LoadFileData(_logDir, SelectedFileName);
    }

    private void LoadFileData(string logDir, string fileName)
    {
        var filePath = Path.Combine(logDir, fileName);
        if (!File.Exists(filePath))
            return;

        // Parse summary header
        var summary = LogFileWriter.ParseSummaryHeader(filePath);
        Summary = summary;

        if (summary != null)
        {
            SummaryOperation = summary.Operation;
            SummaryTimestamp = summary.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
            SummaryTotalCreated = summary.TotalCreated.ToString();
            SummaryTotalUpdated = summary.TotalUpdated.ToString();
            SummaryTotalSkipped = summary.TotalSkipped.ToString();
            SummaryTotalFailed = summary.TotalFailed.ToString();

            // Build predicate breakdown text
            if (summary.Predicates.Count > 0)
            {
                PredicateBreakdown = string.Join(Environment.NewLine,
                    summary.Predicates.Select(p =>
                        $"{p.Name} ({p.Table}): Created={p.Created}, Updated={p.Updated}, Skipped={p.Skipped}, Failed={p.Failed}"));
            }

            // Build advice text
            if (summary.Advice.Count > 0)
            {
                AdviceText = string.Join(Environment.NewLine, summary.Advice);
            }
        }

        // Read raw log text (everything after the summary end marker)
        var content = File.ReadAllText(filePath);
        const string endMarker = "=== END SUMMARY ===";
        var endIdx = content.IndexOf(endMarker, StringComparison.Ordinal);
        if (endIdx >= 0)
        {
            RawLogText = content[(endIdx + endMarker.Length)..].TrimStart('\r', '\n');
        }
        else
        {
            RawLogText = content;
        }
    }
}
