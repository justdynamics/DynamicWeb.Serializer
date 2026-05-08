using System.Text.Json;

namespace DynamicWeb.Serializer.Infrastructure;

/// <summary>
/// Emits the v0.6.0 manifest envelope (<see cref="Manifest"/>) to <c>{mode}-manifest.json</c>
/// at the end of every serialize run. Atomic write via temp-file + File.Move(overwrite: true)
/// + Complete=true sentinel defends pitfall #2: a kill between temp-write and rename leaves
/// the prior manifest intact + readable, with the half-written .tmp as the only byproduct
/// (cleaned up by the next successful run, OR left for forensic inspection if the rename
/// completed on a stale .tmp by mistake — it cannot, because Move is the rename, not Write).
///
/// Phase 42-02 rewrite. Plan 01 owns the type definitions; this class owns the I/O.
/// </summary>
public class ManifestWriter
{
    /// <summary>
    /// Write the manifest envelope atomically. Steps: (1) build envelope with Complete=true,
    /// (2) write JSON to {mode}-manifest.json.tmp, (3) File.Move(tmp, final, overwrite: true).
    /// On Windows NTFS this falls through to MoveFileEx(MOVEFILE_REPLACE_EXISTING |
    /// MOVEFILE_WRITE_THROUGH) — close enough to atomic for our recovery model. On a kill
    /// between (2) and (3) the prior manifest stays intact at {mode}-manifest.json and the
    /// .tmp is the only byproduct on disk.
    /// </summary>
    public void Write(
        string modeRoot,
        string mode,
        IEnumerable<ManifestEntry> entries,
        IReadOnlyDictionary<string, List<string>>? excludeFieldsByItemType = null,
        IReadOnlyDictionary<string, List<string>>? excludeXmlElementsByType = null)
    {
        Directory.CreateDirectory(modeRoot);

        var envelope = new Manifest
        {
            SchemaVersion = ManifestSchema.CurrentVersion,
            Mode = mode,
            WrittenAtUtc = DateTime.UtcNow,
            Complete = true,
            ExcludeFieldsByItemType = excludeFieldsByItemType ?? new Dictionary<string, List<string>>(),
            ExcludeXmlElementsByType = excludeXmlElementsByType ?? new Dictionary<string, List<string>>(),
            Entries = entries.ToList()
        };

        var finalPath = Path.Combine(modeRoot, $"{mode}-manifest.json");
        var tmpPath = finalPath + ".tmp";

        // (2) Write JSON to .tmp
        var json = JsonSerializer.Serialize(envelope, ManifestSchema.ManifestJsonOptions);
        File.WriteAllText(tmpPath, json);

        // (3) Atomic rename. overwrite: true → MoveFileEx(MOVEFILE_REPLACE_EXISTING) on NTFS.
        File.Move(tmpPath, finalPath, overwrite: true);
    }

    /// <summary>
    /// Read a v0.6.0 manifest envelope. Two-stage read:
    /// (a) JsonDocument precheck on the schemaVersion field — throws InvalidOperationException
    ///     naming the version mismatch BEFORE typed deserialize. Ensures we never see "couldn't
    ///     bind ContentEntry" downstream noise on a v1 manifest.
    /// (b) Typed deserialize via ManifestSchema.ManifestJsonOptions (UnmappedMemberHandling.Disallow
    ///     + JsonStringEnumConverter + camelCase). Strict reads catch unknown properties and
    ///     missing required fields with a typed JsonException naming the offender.
    /// (c) Post-deserialize, asserts Manifest.Complete == true. A false/missing complete sentinel
    ///     indicates a torn write; throws JsonException so the caller treats the manifest as unreadable.
    ///
    /// Returns null when the manifest file doesn't exist (no run has happened yet).
    /// </summary>
    public Manifest? Read(string modeRoot, string mode)
    {
        var path = Path.Combine(modeRoot, $"{mode}-manifest.json");
        if (!File.Exists(path)) return null;

        var json = File.ReadAllText(path);

        // (a) schemaVersion gate — runs BEFORE typed deserialize.
        using (var doc = JsonDocument.Parse(json))
        {
            if (!doc.RootElement.TryGetProperty("schemaVersion", out var v) || v.ValueKind != JsonValueKind.Number)
                throw new InvalidOperationException(
                    $"Manifest '{path}' is missing a numeric 'schemaVersion' field. " +
                    $"v0.6.0 manifests require schemaVersion={ManifestSchema.CurrentVersion}.");

            var version = v.GetInt32();
            if (version != ManifestSchema.CurrentVersion)
                throw new InvalidOperationException(
                    $"Manifest '{path}' has schemaVersion={version}, expected {ManifestSchema.CurrentVersion}. " +
                    "Re-run serialize against the current Serializer build to regenerate the manifest.");
        }

        // (b) Typed deserialize — strict-mode catches unknown properties / missing required.
        var manifest = JsonSerializer.Deserialize<Manifest>(json, ManifestSchema.ManifestJsonOptions)
            ?? throw new InvalidOperationException($"Manifest '{path}' deserialized to null.");

        // (c) Torn-write sentinel.
        if (!manifest.Complete)
            throw new JsonException(
                $"Manifest '{path}' has Complete=false — torn write detected. Re-run serialize.");

        return manifest;
    }

    /// <summary>
    /// [Obsolete] Wave-2 compile bridge. The legacy flat-files Write signature kept temporarily
    /// so <c>SerializerOrchestrator.SerializeAll</c> and <c>SerializerSerializeCommand</c> keep
    /// compiling during Wave 2 of Phase 42. Plan 03 removes this overload and switches the
    /// orchestrator to the entries-aware Write above. Behavior under the shim: writes a complete
    /// manifest with zero entries and empty exclusion dicts — the orchestrator-side test suite
    /// is not expected to consume manifest contents during Wave 2; Plan 03 lights up real entries.
    /// </summary>
    [Obsolete("Wave-2 compile shim — use Write(modeRoot, mode, IEnumerable<ManifestEntry>, ...) instead. Removed in Plan 42-03 Task 1.")]
    public void Write(string modeRoot, string mode, IEnumerable<string> writtenFiles)
    {
        // Flat-files arg is ignored — Plan 03 will replace the call site with the entries-aware overload.
        _ = writtenFiles;
        Write(modeRoot, mode, Array.Empty<ManifestEntry>(),
            excludeFieldsByItemType: null,
            excludeXmlElementsByType: null);
    }
}
