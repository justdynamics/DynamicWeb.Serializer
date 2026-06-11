using Dynamicweb.CoreUI.Data;
using Dynamicweb.CoreUI.Data.Validation;

namespace Truvio.Commerce.Serializer.AdminUI.Models;

/// <summary>
/// Settings screen model. Every top-level configuration value is represented here — editable
/// where a screen edit makes sense, as a read-only summary where the value is managed on its
/// own sub-node (predicates, exclusion dicts). Nothing in Serializer.config.json is invisible
/// from the admin UI.
/// </summary>
public sealed class SerializerSettingsModel : DataViewModelBase
{
    [ConfigurableProperty("Output Directory", explanation: "Top-level folder relative to Files/System. Subfolders are managed automatically: SerializeRoot (YAML files), Upload (zip imports), Download (zip exports), Log (run logs).")]
    [Required(ErrorMessage = "Output Directory is required")]
    public string OutputDirectory { get; set; } = string.Empty;

    [ConfigurableProperty("Deploy subfolder", explanation: "Subfolder under SerializeRoot for Deploy-mode YAML. Letters, digits, '-' and '_' only.")]
    public string DeployOutputSubfolder { get; set; } = "deploy";

    [ConfigurableProperty("Seed subfolder", explanation: "Subfolder under SerializeRoot for Seed-mode YAML. Letters, digits, '-' and '_' only.")]
    public string SeedOutputSubfolder { get; set; } = "seed";

    [ConfigurableProperty("Show seed indicators", explanation: "Show seed cues in the admin UI: the flower icon on content-tree pages covered by a seed predicate, and the seed message on content editing screens. Off by default — with broad seed coverage these appear nearly everywhere and drown out the deploy warnings, which carry the actionable signal.")]
    public bool ShowSeedIndicators { get; set; }

    [ConfigurableProperty("Show deploy indicators", explanation: "Show deploy cues in the admin UI: the sync icon on content-tree pages covered by a deploy predicate, the deploy warning on content editing screens, and the deploy warning on commerce settings screens (payment methods, currencies, …) managed by a deploy predicate. On by default — they warn editors that changes are overwritten by the next deploy. Switch off on environments where the warnings are noise, e.g. the source environment itself.")]
    public bool ShowDeployIndicators { get; set; } = true;

    [ConfigurableProperty("Config File", explanation: "Location of the configuration file (relative to wwwroot). It lives inside the serializer folder so the folder travels as one unit — upload an example configuration (e.g. a Swift starter) into that folder via the file manager to start from it. You can also edit the file manually.")]
    public string ConfigFilePath { get; set; } = string.Empty;

    [ConfigurableProperty("Item type field excludes", explanation: "Per-item-type fields excluded from sync — they stay local to each environment. Manage via the Item Type Excludes sub-node. Pages carrying these item types show as partially managed in the content tree.")]
    public string ItemTypeExcludesSummary { get; set; } = string.Empty;

    [ConfigurableProperty("Embedded XML excludes", explanation: "Per-type XML elements (module settings, provider parameters) excluded from sync — they stay local to each environment. Manage via the Embedded XML Excludes sub-node. Content pages carrying these types show as partially managed in the content tree.")]
    public string XmlExcludesSummary { get; set; } = string.Empty;

    [ConfigurableProperty("About Predicates", explanation: "Predicates define which content trees and SQL tables to synchronize, each in Deploy or Seed mode. Use the Predicates sub-node to add, edit, or remove predicates. Only content matching at least one predicate is serialized or deserialized. Terminology: see docs/glossary.md in the project repository.")]
    public string PredicatesSummary { get; set; } = string.Empty;

    [ConfigurableProperty("Sync history", explanation: "Most recent deploy and seed received by this environment, read from the run logs. Dry-run previews are not counted.")]
    public string LastRunsSummary { get; set; } = string.Empty;

    [ConfigurableProperty("Coverage", explanation: "How much of this environment the current predicates manage. Pages are counted per content area; tables per SqlTable predicate.")]
    public string CoverageSummary { get; set; } = string.Empty;

    /// <summary>True when no config exists or no predicates are configured — the screen
    /// swaps its actions for the Get-started group.</summary>
    public bool NeedsSetup { get; set; }
}
