using DynamicWeb.Serializer.Models;

namespace DynamicWeb.Serializer.Configuration;

/// <summary>
/// Top-level serializer configuration. Phase 40 (D-01..D-04) replaces the section-level Deploy/Seed
/// structural split with a single flat predicate list where each predicate carries its own
/// <see cref="ProviderPredicateDefinition.Mode"/>. The two modes are still semantically distinct —
/// Deploy is source-wins and ships deployment data, Seed is destination-wins and ships one-time
/// content — but the configuration shape is now flat. Per-mode subfolder names live as top-level
/// keys; ConflictStrategy is hardcoded per mode (Deploy=SourceWins, Seed=DestinationWins) and is
/// resolved at runtime via <see cref="GetConflictStrategyForMode"/>.
///
/// The legacy section-level shape (top-level <c>deploy</c> / <c>seed</c> objects) is HARD-REJECTED
/// by <see cref="ConfigLoader"/>; <see cref="ConfigWriter"/> never emits it. No backcompat per
/// project policy.
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

    /// <summary>
    /// Phase 37-04 STRICT-01 / SEED-001: when non-null, explicitly opts in / out of strict mode.
    /// <c>null</c> means "use entry-point default" (per D-16: CLI/API default ON, admin UI default OFF).
    /// In strict mode every recoverable WARNING during deserialize (unresolvable link, missing
    /// template, permission-fallback, schema-drift drop, FK-orphan) accumulates and throws a
    /// single <see cref="Infrastructure.CumulativeStrictModeException"/> at end-of-run.
    /// </summary>
    public bool? StrictMode { get; init; }

    // -------------------------------------------------------------------------
    // Phase 40 D-02: top-level per-mode subfolder names. ConflictStrategy is hardcoded per mode
    // (Deploy=SourceWins, Seed=DestinationWins) and not exposed as a config knob anymore — there's
    // no real use case for inverting it. Phase 39 runtime reads ConflictStrategy through
    // GetConflictStrategyForMode below.
    // -------------------------------------------------------------------------

    /// <summary>Subfolder under <see cref="SerializeRoot"/> for Deploy-mode YAML output. Default "deploy".</summary>
    public string DeployOutputSubfolder { get; init; } = "deploy";

    /// <summary>Subfolder under <see cref="SerializeRoot"/> for Seed-mode YAML output. Default "seed".</summary>
    public string SeedOutputSubfolder { get; init; } = "seed";

    // -------------------------------------------------------------------------
    // Phase 40 D-04: top-level (mode-agnostic) field/element exclusions by type.
    // -------------------------------------------------------------------------

    /// <summary>Global per-item-type field exclusions, applied to every predicate regardless of mode.</summary>
    public Dictionary<string, List<string>> ExcludeFieldsByItemType { get; init; } = new();

    /// <summary>Global per-type XML element exclusions, applied to every predicate regardless of mode.</summary>
    public Dictionary<string, List<string>> ExcludeXmlElementsByType { get; init; } = new();

    // -------------------------------------------------------------------------
    // Phase 40 D-02: SINGLE flat predicate list. Each predicate carries its own .Mode.
    // -------------------------------------------------------------------------

    /// <summary>
    /// All predicates, deploy and seed mixed. Consumers filter by
    /// <see cref="ProviderPredicateDefinition.Mode"/> when iterating per mode.
    /// </summary>
    public List<ProviderPredicateDefinition> Predicates { get; init; } = new();

    /// <summary>Resolve the per-mode subfolder string by <see cref="DeploymentMode"/>.</summary>
    public string GetSubfolderForMode(DeploymentMode mode) =>
        mode == DeploymentMode.Deploy ? DeployOutputSubfolder : SeedOutputSubfolder;

    /// <summary>
    /// Resolve the conflict strategy by <see cref="DeploymentMode"/>. Hardcoded per mode:
    /// Deploy → SourceWins (YAML overwrites target), Seed → DestinationWins (preserve customer edits).
    /// </summary>
    public ConflictStrategy GetConflictStrategyForMode(DeploymentMode mode) =>
        mode == DeploymentMode.Deploy ? ConflictStrategy.SourceWins : ConflictStrategy.DestinationWins;

    // -------------------------------------------------------------------------
    // Paths
    // -------------------------------------------------------------------------

    /// <summary>Parent folder for YAML serialization output. Per-mode subfolders sit beneath this.</summary>
    public string SerializeRoot => Path.Combine(OutputDirectory, "SerializeRoot");

    /// <summary>Subfolder for zip files uploaded for import.</summary>
    public string UploadDir => Path.Combine(OutputDirectory, "Upload");

    /// <summary>Subfolder for zip files produced by ad-hoc serialize.</summary>
    public string DownloadDir => Path.Combine(OutputDirectory, "Download");

    /// <summary>Subfolder for log files.</summary>
    public string LogDir => Path.Combine(OutputDirectory, "Log");

    /// <summary>
    /// Resolves all subfolder paths relative to Files/System and ensures they exist on disk,
    /// including the per-mode Deploy / Seed serialize subfolders.
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

        // Phase 40 D-02: per-mode subfolders sit beneath SerializeRoot.
        Directory.CreateDirectory(Path.Combine(resolved.SerializeRoot, DeployOutputSubfolder));
        Directory.CreateDirectory(Path.Combine(resolved.SerializeRoot, SeedOutputSubfolder));

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
