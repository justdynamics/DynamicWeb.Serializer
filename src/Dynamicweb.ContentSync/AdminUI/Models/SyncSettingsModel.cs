using Dynamicweb.CoreUI.Data;

namespace Dynamicweb.ContentSync.AdminUI.Models;

public sealed class SyncSettingsModel : DataViewModelBase
{
    [ConfigurableProperty("Output Directory", explanation: "Root path for serialized YAML files")]
    public string OutputDirectory { get; set; } = string.Empty;

    [ConfigurableProperty("Log Level", explanation: "Logging verbosity (info, debug, warn, error)")]
    public string LogLevel { get; set; } = "info";
}
