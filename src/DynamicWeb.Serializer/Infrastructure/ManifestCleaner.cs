namespace DynamicWeb.Serializer.Infrastructure;

/// <summary>
/// Deletes files under a per-mode SerializeRoot subfolder that were NOT listed in the current
/// run's written-files set. Preserves the manifest JSON itself. Only called on successful runs
/// (exceptions short-circuit the orchestrator before reaching the cleaner). Phase 37-01 Task 2,
/// decisions D-10/D-11/D-12.
///
/// Security: T-37-01-01. Every candidate file path is resolved with <see cref="Path.GetFullPath"/>
/// and checked to sit beneath the resolved <c>modeRoot</c> — a symlink that points outside the
/// subtree is NOT followed for deletion.
/// </summary>
public class ManifestCleaner
{
    /// <summary>
    /// Deletes every file under <paramref name="modeRoot"/> not present in <paramref name="writtenFiles"/>
    /// (and not the manifest file itself). Returns the number of files deleted. Removes any
    /// directories left empty after the sweep.
    /// </summary>
    public int CleanStale(string modeRoot, string mode, IEnumerable<string> writtenFiles, Action<string>? log = null)
    {
        if (!Directory.Exists(modeRoot)) return 0;

        var resolvedModeRoot = Path.GetFullPath(modeRoot);
        // Normalise trailing separator so prefix checks are unambiguous ("/a/b" vs "/a/bb").
        var modeRootPrefix = resolvedModeRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        var manifestFileName = $"{mode}-manifest.json";
        var writtenSet = new HashSet<string>(
            writtenFiles.Select(f => Path.GetFullPath(f)),
            StringComparer.OrdinalIgnoreCase);

        int deleted = 0;
        // EnumerateFiles without following symlinks: get the enumeration options explicit.
        var enumOptions = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.ReparsePoint // T-37-01-01: don't descend into symlinked subtrees.
        };

        foreach (var file in Directory.EnumerateFiles(resolvedModeRoot, "*", enumOptions))
        {
            if (string.Equals(Path.GetFileName(file), manifestFileName, StringComparison.OrdinalIgnoreCase))
                continue;

            var fullFile = Path.GetFullPath(file);

            // T-37-01-01: confine deletion to the resolved modeRoot subtree.
            if (!fullFile.StartsWith(modeRootPrefix, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(Path.GetDirectoryName(fullFile), resolvedModeRoot, StringComparison.OrdinalIgnoreCase))
            {
                log?.Invoke($"Cleanup: skipped candidate outside modeRoot: {fullFile}");
                continue;
            }

            if (writtenSet.Contains(fullFile))
                continue;

            // Defensive: if the entry is itself a reparse point (symlink file), delete the link, not the target.
            var info = new FileInfo(fullFile);
            if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                // Removing the link itself leaves any target file intact.
                File.Delete(fullFile);
                log?.Invoke($"Cleanup: deleted stale symlink {Path.GetRelativePath(resolvedModeRoot, fullFile)}");
                deleted++;
                continue;
            }

            File.Delete(fullFile);
            log?.Invoke($"Cleanup: deleted stale file {Path.GetRelativePath(resolvedModeRoot, fullFile)}");
            deleted++;
        }

        // Remove any now-empty directories (deepest first).
        foreach (var dir in Directory.EnumerateDirectories(resolvedModeRoot, "*", SearchOption.AllDirectories)
            .OrderByDescending(d => d.Length))
        {
            if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
                Directory.Delete(dir);
        }

        return deleted;
    }
}
