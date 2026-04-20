using System.Text.Json.Serialization;
using DynamicWeb.Serializer.Models;

namespace DynamicWeb.Serializer.Configuration;

/// <summary>
/// Top-level serializer configuration. Phase 37-01 (D-01..D-06) introduces the Deploy / Seed
/// structural split: each mode has its own <see cref="ModeConfig"/> with an independent predicate
/// list, exclusion config and conflict strategy. Deploy is the default, source-wins and ships
/// deployment data. Seed is explicit opt-in, destination-wins and ships one-time content.
///
/// Legacy flat-style callers (predicate list + exclusion dictionaries at the top level) are still
/// supported via pass-through properties that read/write the Deploy mode. This keeps the blast
/// radius of the split small while the rest of the codebase migrates. The on-disk JSON format
/// always uses the new Deploy/Seed shape (see <see cref="ConfigWriter"/>).
/// </summary>
public record SerializerConfiguration
{
    /// <summary>
    /// Top-level folder relative to Files/System. Subfolders are managed automatically:
    /// SerializeRoot/ (YAML files), Upload/ (zip imports), Download/ (zip exports).
    /// </summary>
    public required string OutputDirectory { get; init; }
    public string LogLevel { get; init; } = "info";
    public bool DryRun { get; init; } = false;

    // -------------------------------------------------------------------------
    // Phase 37-01: Deploy + Seed ModeConfigs
    // -------------------------------------------------------------------------

    /// <summary>
    /// Deploy mode config — source-wins, default serialize/deserialize path for
    /// deployment data (shop structure, currencies, payment methods, etc.).
    /// </summary>
    public ModeConfig Deploy { get; init; } = new()
    {
        OutputSubfolder = "deploy",
        ConflictStrategy = ConflictStrategy.SourceWins
    };

    /// <summary>
    /// Seed mode config — destination-wins, opt-in path for seed content
    /// (Customer Center pages, FAQ copy, etc.) that must not overwrite
    /// customer-edited target data on re-deploy.
    /// </summary>
    public ModeConfig Seed { get; init; } = new()
    {
        OutputSubfolder = "seed",
        ConflictStrategy = ConflictStrategy.DestinationWins
    };

    /// <summary>Resolve a <see cref="ModeConfig"/> by <see cref="DeploymentMode"/>.</summary>
    public ModeConfig GetMode(DeploymentMode mode) =>
        mode == DeploymentMode.Deploy ? Deploy : Seed;

    // -------------------------------------------------------------------------
    // Legacy flat pass-through properties (kept to minimise diff blast radius
    // across admin UI / tests / ContentSerializer until they migrate). Setters
    // only make sense at construction time because this is a record. They
    // delegate to Deploy on read and are serialised via init-setters only —
    // the on-disk JSON uses the Deploy/Seed shape (see ConfigWriter), so these
    // shims are not emitted twice.
    //
    // All four legacy properties are [JsonIgnore]-d to prevent double-write;
    // ConfigLoader handles migration of any still-legacy JSON input.
    // -------------------------------------------------------------------------

    /// <summary>
    /// Legacy alias for <c>Deploy.Predicates</c>. Init-time setter populates Deploy.Predicates.
    /// Marked <see cref="JsonIgnoreAttribute"/> — on-disk JSON uses Deploy/Seed structure.
    /// </summary>
    [JsonIgnore]
    public List<ProviderPredicateDefinition> Predicates
    {
        get => Deploy.Predicates;
        init
        {
            // Overlay onto the existing Deploy defaults so OutputSubfolder/ConflictStrategy stay "deploy" / SourceWins.
            Deploy = Deploy with { Predicates = value ?? new List<ProviderPredicateDefinition>() };
        }
    }

    /// <summary>Legacy alias for <c>Deploy.ExcludeFieldsByItemType</c>.</summary>
    [JsonIgnore]
    public Dictionary<string, List<string>> ExcludeFieldsByItemType
    {
        get => Deploy.ExcludeFieldsByItemType;
        init
        {
            Deploy = Deploy with { ExcludeFieldsByItemType = value ?? new Dictionary<string, List<string>>() };
        }
    }

    /// <summary>Legacy alias for <c>Deploy.ExcludeXmlElementsByType</c>.</summary>
    [JsonIgnore]
    public Dictionary<string, List<string>> ExcludeXmlElementsByType
    {
        get => Deploy.ExcludeXmlElementsByType;
        init
        {
            Deploy = Deploy with { ExcludeXmlElementsByType = value ?? new Dictionary<string, List<string>>() };
        }
    }

    /// <summary>Legacy alias for <c>Deploy.ConflictStrategy</c>.</summary>
    [JsonIgnore]
    public ConflictStrategy ConflictStrategy
    {
        get => Deploy.ConflictStrategy;
        init
        {
            Deploy = Deploy with { ConflictStrategy = value };
        }
    }

    // -------------------------------------------------------------------------
    // Paths
    // -------------------------------------------------------------------------

    /// <summary>Parent folder for YAML serialization output. Per-mode subfolders sit beneath this.</summary>
    public string SerializeRoot => Path.Combine(OutputDirectory, "SerializeRoot");

    /// <summary>Per-mode YAML serialization subfolder (e.g., SerializeRoot/deploy/).</summary>
    public string GetModeSerializeRoot(DeploymentMode mode) =>
        Path.Combine(SerializeRoot, GetMode(mode).OutputSubfolder);

    /// <summary>Subfolder for zip files uploaded for import.</summary>
    public string UploadDir => Path.Combine(OutputDirectory, "Upload");

    /// <summary>Subfolder for zip files produced by ad-hoc serialize.</summary>
    public string DownloadDir => Path.Combine(OutputDirectory, "Download");

    /// <summary>Subfolder for log files.</summary>
    public string LogDir => Path.Combine(OutputDirectory, "Log");

    /// <summary>
    /// Resolves all subfolder paths relative to Files/System and ensures they exist on disk,
    /// including the per-mode Deploy / Seed serialize roots.
    /// </summary>
    public ResolvedPaths EnsureDirectories(string filesSystemDir)
    {
        var resolved = new ResolvedPaths
        {
            Root = Path.GetFullPath(Path.Combine(filesSystemDir, OutputDirectory.TrimStart('\\', '/'))),
            SerializeRoot = Path.GetFullPath(Path.Combine(filesSystemDir, SerializeRoot.TrimStart('\\', '/'))),
            Upload = Path.GetFullPath(Path.Combine(filesSystemDir, UploadDir.TrimStart('\\', '/'))),
            Download = Path.GetFullPath(Path.Combine(filesSystemDir, DownloadDir.TrimStart('\\', '/'))),
            Log = Path.GetFullPath(Path.Combine(filesSystemDir, LogDir.TrimStart('\\', '/')))
        };

        Directory.CreateDirectory(resolved.Root);
        Directory.CreateDirectory(resolved.SerializeRoot);
        Directory.CreateDirectory(resolved.Upload);
        Directory.CreateDirectory(resolved.Download);
        Directory.CreateDirectory(resolved.Log);

        // Ensure per-mode subfolders exist (D-03).
        Directory.CreateDirectory(Path.Combine(resolved.SerializeRoot, Deploy.OutputSubfolder));
        Directory.CreateDirectory(Path.Combine(resolved.SerializeRoot, Seed.OutputSubfolder));

        return resolved;
    }

    public record ResolvedPaths
    {
        public required string Root { get; init; }
        public required string SerializeRoot { get; init; }
        public required string Upload { get; init; }
        public required string Download { get; init; }
        public required string Log { get; init; }
    }
}
