using Dynamicweb.ContentSync.Configuration;
using Dynamicweb.CoreUI.Data;
using Dynamicweb.CoreUI.Data.Validation;

namespace Dynamicweb.ContentSync.AdminUI.Models;

public sealed class SyncSettingsModel : DataViewModelBase
{
    [ConfigurableProperty("Output Directory", explanation: "Root path for serialized YAML files (relative to Files/System)")]
    [Required(ErrorMessage = "Output Directory is required")]
    public string OutputDirectory { get; set; } = string.Empty;

    [ConfigurableProperty("Export Directory", explanation: "When set, ad-hoc serialize also saves a copy of the zip to this path (relative to Files/System). Leave empty to skip.")]
    public string ExportDirectory { get; set; } = string.Empty;

    [ConfigurableProperty("Deserialize Source", explanation: "Source for the deserialize scheduled task. Leave empty to use Output Directory (folder mode). Set to a .zip file path for zip mode (e.g. ContentSync/import.zip).")]
    public string DeserializeSource { get; set; } = string.Empty;

    [ConfigurableProperty("Log Level", explanation: "Logging verbosity")]
    public string LogLevel { get; set; } = "info";

    [ConfigurableProperty("Dry Run", explanation: "When enabled, sync operations log what would happen without making changes")]
    public bool DryRun { get; set; } = false;

    [ConfigurableProperty("Conflict Strategy", explanation: "How to handle conflicts when source and target differ")]
    public string ConflictStrategy { get; set; } = "source-wins";

    [ConfigurableProperty("Config File", explanation: "Location of the configuration file (relative to wwwroot). Settings and predicates are stored here. You can also edit this file manually.")]
    public string ConfigFilePath { get; set; } = string.Empty;

    [ConfigurableProperty("About Predicates", explanation: "Predicates define which content trees to synchronize. Each predicate targets a root page in a specific area. Pages under that root are included in sync. Use the Predicates sub-node to add, edit, or remove predicates. Only content matching at least one predicate will be serialized or deserialized.")]
    public string PredicatesSummary { get; set; } = string.Empty;
}
