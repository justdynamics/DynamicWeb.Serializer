using System.Text.Json;

namespace DynamicWeb.Serializer.Infrastructure;

/// <summary>
/// Phase 43 / DESER-05 final: emits a one-time WARNING when the on-disk Serializer.config.json
/// has <c>strictMode</c> set even though the deserialize path no longer consults it. Routes
/// through the standard log plumbing (caller's <c>Action&lt;string&gt;</c> log) — no admin-UI
/// banner, no process-wide singleton; "once per run" is implicit because each command call
/// invokes <see cref="EmitIfLegacyValueSet"/> exactly once at command start.
///
/// Intentionally does NOT participate in full config-load semantics — that's the banned
/// path per SC-4 grep gate (no ConfigLoader on the deserialize hot path). Reads the JSON
/// directly via <see cref="JsonDocument"/> to peek at the legacy property only.
/// </summary>
public static class StrictModeDeprecationWarning
{
    /// <summary>
    /// Emit a one-time WARNING if <paramref name="configPath"/> contains a top-level
    /// <c>strictMode</c> boolean. Silent on absent file, missing property, or any
    /// JSON-parse failure — peek failure is non-fatal because the deserialize path
    /// doesn't depend on this signal; the warning is purely advisory.
    /// </summary>
    public static void EmitIfLegacyValueSet(string? configPath, Action<string>? log)
    {
        if (string.IsNullOrEmpty(configPath) || !File.Exists(configPath)) return;
        try
        {
            using var stream = File.OpenRead(configPath);
            using var doc = JsonDocument.Parse(stream);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return;
            if (!doc.RootElement.TryGetProperty("strictMode", out var sm)) return;
            if (sm.ValueKind != JsonValueKind.True && sm.ValueKind != JsonValueKind.False) return;

            log?.Invoke(
                $"WARNING: config.StrictMode is set in `{configPath}` but no longer consulted on " +
                "the deserialize path; use the per-call ?strictMode=true query parameter or rely " +
                "on the entry-point default");
        }
        catch
        {
            // Peek failure is non-fatal — the deserialize path doesn't need this; leave silent.
        }
    }
}
