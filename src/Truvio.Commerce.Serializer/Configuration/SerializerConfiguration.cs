using Truvio.Commerce.Serializer.Models;

namespace Truvio.Commerce.Serializer.Configuration;

/// <summary>
/// Top-level serializer configuration. A single flat predicate list where each predicate carries
/// its own <see cref="ProviderPredicateDefinition.Mode"/>. The two modes are semantically distinct —
/// Replace is source-wins (the source overwrites the destination), Merge is destination-wins and
/// fills only empty destination fields. Per-mode subfolder names live as top-level keys;
/// ConflictStrategy is hardcoded per mode (Replace=SourceWins, Merge=DestinationWins) and is
/// resolved at runtime via <see cref="GetConflictStrategyForMode"/>.
///
/// The section-level shape (top-level <c>replace</c> / <c>merge</c> objects) is HARD-REJECTED
/// by <see cref="ConfigLoader"/>; <see cref="ConfigWriter"/> never emits it.
/// </summary>
public record SerializerConfiguration
{
    /// <summary>
    /// Top-level folder relative to Files/System. Subfolders are managed automatically:
    /// SerializeRoot/ (YAML files), Upload/ (zip imports), Download/ (zip exports).
    /// </summary>
    public required string OutputDirectory { get; init; }

    // -------------------------------------------------------------------------
    // Top-level per-mode subfolder names. ConflictStrategy is hardcoded per mode
    // (Replace=SourceWins, Merge=DestinationWins) and not exposed as a config knob — there's
    // no real use case for inverting it. Runtime reads ConflictStrategy through
    // GetConflictStrategyForMode below.
    // -------------------------------------------------------------------------

    /// <summary>Subfolder under <see cref="SerializeRoot"/> for Replace-mode YAML output. Default "replace".</summary>
    public string ReplaceOutputSubfolder { get; init; } = "replace";

    /// <summary>Subfolder under <see cref="SerializeRoot"/> for Merge-mode YAML output. Default "merge".</summary>
    public string MergeOutputSubfolder { get; init; } = "merge";

    // -------------------------------------------------------------------------
    // Top-level (mode-agnostic) field/element exclusions by type.
    // -------------------------------------------------------------------------

    /// <summary>Global per-item-type field exclusions, applied to every predicate regardless of mode.</summary>
    public Dictionary<string, List<string>> ExcludeFieldsByItemType { get; init; } = new();

    /// <summary>Global per-type XML element exclusions, applied to every predicate regardless of mode.</summary>
    public Dictionary<string, List<string>> ExcludeXmlElementsByType { get; init; } = new();

    /// <summary>
    /// Admin UI: show merge indicators — the merge (flower) annotation on content-tree pages
    /// AND the merge info message on content editing screens. Off by default — broad merge
    /// coverage marks nearly every node/page and drowns out the replace warnings, which carry
    /// the actionable signal ("edits here are overwritten by the next replace run").
    /// </summary>
    public bool ShowMergeIndicators { get; init; } = false;

    /// <summary>
    /// Admin UI: show replace indicators — the sync annotation on content-tree pages, the
    /// replace warning on content editing screens, and the replace warning on commerce
    /// settings screens backed by replace-managed SqlTable predicates. On by default — these
    /// carry the actionable "edits here are overwritten by the next replace run" signal — but
    /// can be switched off (e.g. on a source environment where every page is replace-managed
    /// and the warnings are noise).
    /// </summary>
    public bool ShowReplaceIndicators { get; init; } = true;

    // -------------------------------------------------------------------------
    // SINGLE flat predicate list. Each predicate carries its own .Mode.
    // -------------------------------------------------------------------------

    /// <summary>
    /// All predicates, replace and merge mixed. Consumers filter by
    /// <see cref="ProviderPredicateDefinition.Mode"/> when iterating per mode.
    /// </summary>
    public List<ProviderPredicateDefinition> Predicates { get; init; } = new();

    /// <summary>Resolve the per-mode subfolder string by <see cref="SerializerMode"/>.</summary>
    public string GetSubfolderForMode(SerializerMode mode) =>
        mode == SerializerMode.Replace ? ReplaceOutputSubfolder : MergeOutputSubfolder;

    /// <summary>
    /// Resolve the conflict strategy by <see cref="SerializerMode"/>. Hardcoded per mode:
    /// Replace → SourceWins (YAML overwrites target), Merge → DestinationWins (preserve destination edits).
    /// </summary>
    public ConflictStrategy GetConflictStrategyForMode(SerializerMode mode) =>
        mode == SerializerMode.Replace ? ConflictStrategy.SourceWins : ConflictStrategy.DestinationWins;

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
    /// including the per-mode Replace / Merge serialize subfolders.
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

        // Per-mode subfolders sit beneath SerializeRoot.
        Directory.CreateDirectory(Path.Combine(resolved.SerializeRoot, ReplaceOutputSubfolder));
        Directory.CreateDirectory(Path.Combine(resolved.SerializeRoot, MergeOutputSubfolder));

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
