using System.Text.Json;
using System.Text.Json.Serialization;

namespace DynamicWeb.Serializer.Infrastructure;

/// <summary>
/// Emits <c>{mode}-manifest.json</c> listing every YAML file written during a serialize run.
/// Consumed by <see cref="ManifestCleaner"/> to delete stale files from prior runs. Phase 37-01
/// Task 2, decisions D-10/D-11: per-mode manifest, no global cleanup.
/// </summary>
public class ManifestWriter
{
    public record Manifest
    {
        public required string Mode { get; init; }
        public required DateTime WrittenAtUtc { get; init; }
        public required List<string> Files { get; init; }
    }

    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    /// <summary>
    /// Emit the manifest. Paths in <paramref name="writtenFiles"/> are recorded as POSIX-style
    /// relative paths beneath <paramref name="modeRoot"/> so the on-disk file is stable across
    /// Windows/Linux build hosts.
    /// </summary>
    public void Write(string modeRoot, string mode, IEnumerable<string> writtenFiles)
    {
        Directory.CreateDirectory(modeRoot);

        var manifest = new Manifest
        {
            Mode = mode,
            WrittenAtUtc = DateTime.UtcNow,
            Files = writtenFiles
                .Select(f => Path.GetRelativePath(modeRoot, f).Replace('\\', '/'))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
        var path = Path.Combine(modeRoot, $"{mode}-manifest.json");
        File.WriteAllText(path, JsonSerializer.Serialize(manifest, ManifestJsonOptions));
    }

    /// <summary>Read a previously-written manifest. Returns null if absent.</summary>
    public Manifest? Read(string modeRoot, string mode)
    {
        var path = Path.Combine(modeRoot, $"{mode}-manifest.json");
        if (!File.Exists(path)) return null;
        return JsonSerializer.Deserialize<Manifest>(File.ReadAllText(path), ManifestJsonOptions);
    }
}
