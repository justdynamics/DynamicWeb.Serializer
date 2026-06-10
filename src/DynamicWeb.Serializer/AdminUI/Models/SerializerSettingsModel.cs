using Dynamicweb.CoreUI.Data;
using Dynamicweb.CoreUI.Data.Validation;

namespace DynamicWeb.Serializer.AdminUI.Models;

public sealed class SerializerSettingsModel : DataViewModelBase
{
    [ConfigurableProperty("Output Directory", explanation: "Top-level folder relative to Files/System. Subfolders are managed automatically: SerializeRoot (YAML files), Upload (zip imports), Download (zip exports).")]
    [Required(ErrorMessage = "Output Directory is required")]
    public string OutputDirectory { get; set; } = string.Empty;

    [ConfigurableProperty("Config File", explanation: "Location of the configuration file (relative to wwwroot). Settings and predicates are stored here. You can also edit this file manually.")]
    public string ConfigFilePath { get; set; } = string.Empty;

    [ConfigurableProperty("About Predicates", explanation: "Predicates define which content trees to synchronize. Each predicate targets a root page in a specific area. Pages under that root are included in sync. Use the Predicates sub-node to add, edit, or remove predicates. Only content matching at least one predicate will be serialized or deserialized.")]
    public string PredicatesSummary { get; set; } = string.Empty;
}
