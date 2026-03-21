using Dynamicweb.ContentSync.Configuration;
using Dynamicweb.CoreUI.Data;
using Dynamicweb.CoreUI.Data.Validation;

namespace Dynamicweb.ContentSync.AdminUI.Models;

public sealed class SyncSettingsModel : DataViewModelBase
{
    [ConfigurableProperty("Output Directory", explanation: "Root path for serialized YAML files (relative to Files/System)")]
    [Required(ErrorMessage = "Output Directory is required")]
    public string OutputDirectory { get; set; } = string.Empty;

    [ConfigurableProperty("Log Level", explanation: "Logging verbosity")]
    public string LogLevel { get; set; } = "info";

    [ConfigurableProperty("Dry Run", explanation: "When enabled, sync operations log what would happen without making changes")]
    public bool DryRun { get; set; } = false;

    [ConfigurableProperty("Conflict Strategy", explanation: "How to handle conflicts when source and target differ")]
    public string ConflictStrategy { get; set; } = "source-wins";
}
