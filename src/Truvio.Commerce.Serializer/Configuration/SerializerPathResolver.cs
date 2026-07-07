namespace Truvio.Commerce.Serializer.Configuration;

/// <summary>
/// Phase 43 / DESER-04: config-free path resolution for the deserialize entry points.
/// Replaces <see cref="SerializerConfiguration.EnsureDirectories"/> on call sites that no
/// longer need the full <see cref="ConfigLoader.Load"/> just to derive the canonical
/// Files/System/Serializer/ directory layout. Mirrors the SerializerConfiguration.ResolvedPaths
/// shape so call-site refactors are field-rename-only.
///
/// Layout (rooted at <c>{filesSystemDir}/Serializer/</c>):
/// <list type="bullet">
/// <item><c>SerializeRoot/</c> — YAML files (parent of per-mode subfolders)</item>
/// <item><c>Upload/</c> — zip files uploaded for import</item>
/// <item><c>Download/</c> — zip files produced by ad-hoc serialize</item>
/// <item><c>Log/</c> — log files</item>
/// <item><c>SerializeRoot/replace/</c> + <c>SerializeRoot/merge/</c> — per-mode subfolders</item>
/// </list>
/// </summary>
public static class SerializerPathResolver
{
    /// <summary>
    /// Default OutputDirectory mirroring <see cref="SerializerConfiguration"/>'s convention —
    /// the <c>"Serializer"</c> subfolder under <c>{filesRoot}/System</c>. Phase 43's deserialize
    /// path uses this constant directly so it doesn't need to read <c>OutputDirectory</c> from
    /// disk via <see cref="ConfigLoader.Load"/>.
    /// </summary>
    public const string DefaultOutputDirectory = "Serializer";

    /// <summary>
    /// Resolve canonical layout under <paramref name="filesSystemDir"/> and ensure every
    /// directory exists. Output paths are byte-identical to
    /// <see cref="SerializerConfiguration.EnsureDirectories"/>'s output when its
    /// <c>OutputDirectory</c> is the default <see cref="DefaultOutputDirectory"/>.
    /// </summary>
    public static SerializerPaths EnsureDirectories(string filesSystemDir)
    {
        var root = Path.GetFullPath(Path.Combine(filesSystemDir, DefaultOutputDirectory));
        var serializeRoot = Path.GetFullPath(Path.Combine(root, "SerializeRoot"));
        var upload = Path.GetFullPath(Path.Combine(root, "Upload"));
        var download = Path.GetFullPath(Path.Combine(root, "Download"));
        var log = Path.GetFullPath(Path.Combine(root, "Log"));

        Directory.CreateDirectory(root);
        Directory.CreateDirectory(serializeRoot);
        Directory.CreateDirectory(upload);
        Directory.CreateDirectory(download);
        Directory.CreateDirectory(log);

        // Per-mode subfolders sit beneath SerializeRoot.
        Directory.CreateDirectory(Path.Combine(serializeRoot, "replace"));
        Directory.CreateDirectory(Path.Combine(serializeRoot, "merge"));

        return new SerializerPaths(root, serializeRoot, upload, download, log);
    }
}

/// <summary>
/// Phase 43 / DESER-04: canonical path bundle returned by
/// <see cref="SerializerPathResolver.EnsureDirectories"/>. Property names match
/// <see cref="SerializerConfiguration.ResolvedPaths"/> exactly so call-site refactors
/// are field-rename-only.
/// </summary>
public sealed record SerializerPaths(
    string Root,
    string SerializeRoot,
    string Upload,
    string Download,
    string Log);
