using System.IO.Compression;
using Truvio.Commerce.Serializer.AdminUI.Models;
using Truvio.Commerce.Serializer.AdminUI.Security;
using Truvio.Commerce.Serializer.Configuration;
using Dynamicweb.CoreUI.Data;

namespace Truvio.Commerce.Serializer.AdminUI.Commands;

/// <summary>
/// Bulk transport for a full SerializeRoot payload (LRN-hosted-publish-02). Accepts one zip of
/// a whole mode tree and expands it host-side into the engine-owned
/// <c>Files/System/Serializer/SerializeRoot/&lt;mode&gt;/</c> directory, REPLACING the existing
/// tree, then returns the number of files extracted. The follow-up call
/// <c>SerializerDeserialize {Mode}</c> is left unchanged — it reads the materialised tree from
/// disk and runs every predicate.
///
/// <para>
/// Getting a serialized payload onto a hosted target used to be the slowest leg of a publish:
/// <c>POST /Admin/Api/Upload</c> is per-directory multipart and the target directory must
/// already exist, so a mid-size demo (~1943 files across 588 dirs) meant ~1200 chatty round
/// trips for a payload that zips to ~1 MB. This endpoint collapses that to one upload + one
/// unzip. The unzip target is a fixed engine-owned path — no arbitrary write primitive — and
/// zip entries that resolve outside it are rejected (zip-slip defence).
/// </para>
///
/// Use via DW Management API: <c>POST /Admin/Api/SerializerUploadRoot</c> (multipart: one zip),
/// or supply <c>FilePath</c> pointing at a zip already uploaded via <c>/Admin/Api/Upload</c>.
/// </summary>
public sealed class SerializerUploadRootCommand : CommandBase
{
    /// <summary>Serializer mode subfolder to replace: "replace" (default) or "merge". Case-insensitive.</summary>
    public string Mode { get; set; } = "replace";

    /// <summary>
    /// /Files-rooted path to a zip already uploaded (e.g. via <c>/Admin/Api/Upload</c> into
    /// <c>/Files/System/Serializer/Upload/</c>). Optional when a multipart file is posted
    /// directly (see <see cref="Files"/>).
    /// </summary>
    public string FilePath { get; set; } = "";

    /// <summary>
    /// Uploaded file path(s) bound from a multipart POST (mirrors
    /// <see cref="DeserializeUploadedZipCommand.Files"/>). The first <c>.zip</c> is used.
    /// </summary>
    public IEnumerable<string> Files { get; set; } = [];

    public override CommandResult Handle()
    {
        // Parse mode strictly before any path interpolation.
        if (!Enum.TryParse<SerializerMode>(Mode?.Trim(), ignoreCase: true, out var serializerMode))
        {
            return new()
            {
                Status = CommandResult.ResultType.Invalid,
                Message = $"Invalid mode '{Mode}'. Expected 'replace' or 'merge' (case-insensitive)."
            };
        }

        if (!PackageAccess.CanUploadRoot())
        {
            return new()
            {
                Status = CommandResult.ResultType.NotAllowed,
                Message = "You do not have permission to upload a serialize root."
            };
        }

        try
        {
            // Resolve the zip: an explicit FilePath wins, else the first .zip in the multipart set.
            var virtualZipPath = ResolveZipVirtualPath();
            if (virtualZipPath is null)
                return new() { Status = CommandResult.ResultType.Invalid, Message = "Upload a .zip of a SerializeRoot mode tree (FilePath or a multipart file)." };

            var configPath = ConfigPathResolver.FindConfigFile();
            if (configPath == null)
                return new() { Status = CommandResult.ResultType.Error, Message = "Serializer.config.json not found" };

            var filesRoot = ConfigPathResolver.GetFilesRoot(configPath);
            var systemDir = Path.Combine(filesRoot, "System");
            var paths = SerializerPathResolver.EnsureDirectories(systemDir);

            var physicalZipPath = Dynamicweb.Core.SystemInformation.MapPath(virtualZipPath);
            if (!File.Exists(physicalZipPath))
                return new() { Status = CommandResult.ResultType.Error, Message = $"Zip file not found: {virtualZipPath}" };

            var modeName = serializerMode.ToString().ToLowerInvariant();
            var targetDir = Path.Combine(paths.SerializeRoot, modeName);

            int fileCount = ExtractReplacingTree(physicalZipPath, targetDir);

            return new CommandResult
            {
                Status = CommandResult.ResultType.Ok,
                Message = $"[{serializerMode}] Extracted {fileCount} file(s) into SerializeRoot/{modeName}. " +
                          $"Run SerializerDeserialize (Mode={modeName}) to apply.",
                Model = new SerializerUploadRootResultModel
                {
                    Mode = modeName,
                    FileCount = fileCount,
                    TargetPath = targetDir
                }
            };
        }
        catch (InvalidOperationException ex)
        {
            // Zip-slip rejection surfaces as Invalid (bad input), not a server Error.
            return new() { Status = CommandResult.ResultType.Invalid, Message = ex.Message };
        }
        catch (Exception ex)
        {
            return new() { Status = CommandResult.ResultType.Error, Message = $"SerializeRoot upload failed: {ex.Message}" };
        }
    }

    /// <summary>
    /// Pick the zip to expand: an explicit <see cref="FilePath"/> first, else the first
    /// <c>.zip</c> among the multipart <see cref="Files"/>. Bare file names (no leading slash)
    /// are normalised to the standard Upload directory, mirroring
    /// <see cref="DeserializeUploadedZipCommand"/>.
    /// </summary>
    private string? ResolveZipVirtualPath()
    {
        var candidate = !string.IsNullOrWhiteSpace(FilePath)
            ? FilePath
            : Files.FirstOrDefault(f => f.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(candidate))
            return null;

        return candidate.StartsWith('/')
            ? candidate
            : $"/Files/System/Serializer/Upload/{Path.GetFileName(candidate)}";
    }

    /// <summary>
    /// Replace <paramref name="targetDir"/> with the contents of the zip at
    /// <paramref name="zipPhysicalPath"/>. The existing tree is deleted first so the result is
    /// exactly the zip's contents (no stale files survive). Every entry is validated to resolve
    /// inside <paramref name="targetDir"/> (zip-slip defence); an escaping entry throws
    /// <see cref="InvalidOperationException"/> before any file is written outside the target.
    /// Directory entries are skipped. Returns the number of files extracted.
    /// </summary>
    internal static int ExtractReplacingTree(string zipPhysicalPath, string targetDir)
    {
        if (Directory.Exists(targetDir))
            Directory.Delete(targetDir, recursive: true);
        Directory.CreateDirectory(targetDir);

        var targetFull = Path.GetFullPath(targetDir)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var prefix = targetFull + Path.DirectorySeparatorChar;

        int count = 0;
        using var archive = ZipFile.OpenRead(zipPhysicalPath);
        foreach (var entry in archive.Entries)
        {
            // Directory entries carry an empty Name (path ends with '/').
            if (string.IsNullOrEmpty(entry.Name))
                continue;

            var destPath = Path.GetFullPath(Path.Combine(targetFull, entry.FullName));
            if (!destPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"Zip entry '{entry.FullName}' resolves outside the target directory (zip-slip) — rejected.");

            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            entry.ExtractToFile(destPath, overwrite: true);
            count++;
        }

        return count;
    }
}
