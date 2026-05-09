using DynamicWeb.Serializer.Infrastructure;
using DynamicWeb.Serializer.Providers;

namespace DynamicWeb.Serializer.Reporting;

/// <summary>
/// Phase 43 / REPORT-02: per-entry outcome record. Replaces aggregate
/// <see cref="OrchestratorResult.DeserializeResults"/> as the canonical reporting surface.
/// One <see cref="EntryOutcome"/> per dispatched manifest entry; <c>Skipped</c> outcomes
/// surface entries the orchestrator never dispatched (today's silent-skip class).
/// </summary>
/// <remarks>
/// Per CONTEXT D-02:
/// <list type="bullet">
/// <item>Files-don't-exist-on-disk ⇒ <see cref="EntryStatus.Failed"/> via <see cref="Failed"/>.</item>
/// <item>Dry-run ⇒ <see cref="EntryStatus.Succeeded"/> with <see cref="Counts"/> populated.</item>
/// <item>Seed-merge no-op ⇒ <see cref="EntryStatus.Succeeded"/> with <see cref="ProviderCounts.Skipped"/> non-zero.</item>
/// <item>providerFilter exclusion ⇒ <see cref="EntryStatus.Skipped"/> via <see cref="Skipped"/>.</item>
/// </list>
/// </remarks>
public sealed record EntryOutcome
{
    public required string EntryId { get; init; }
    public required string ProviderType { get; init; }
    public required EntryStatus Status { get; init; }
    public required string Message { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    public ProviderCounts Counts { get; init; } = ProviderCounts.Zero;
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Build an outcome from a dispatched <see cref="ProviderDeserializeResult"/>. Status
    /// is <see cref="EntryStatus.Failed"/> when <see cref="ProviderDeserializeResult.HasErrors"/>
    /// is true (covers both <c>Failed &gt; 0</c> and non-empty <c>Errors</c> per D-02);
    /// <see cref="EntryStatus.Warned"/> when <paramref name="warnings"/> is non-empty;
    /// otherwise <see cref="EntryStatus.Succeeded"/>.
    /// </summary>
    public static EntryOutcome From(
        ManifestEntry entry,
        ProviderDeserializeResult r,
        TimeSpan duration,
        IReadOnlyList<string>? warnings = null)
    {
        var status = r.HasErrors
            ? EntryStatus.Failed
            : (warnings is { Count: > 0 } ? EntryStatus.Warned : EntryStatus.Succeeded);

        return new EntryOutcome
        {
            EntryId = entry.EntryId,
            ProviderType = entry.ProviderType,
            Status = status,
            Message = r.Summary,
            Errors = r.Errors.ToList(),
            Warnings = warnings?.ToList() ?? (IReadOnlyList<string>)Array.Empty<string>(),
            Counts = ProviderCounts.From(r),
            Duration = duration
        };
    }

    /// <summary>
    /// Build a Skipped outcome — the orchestrator filtered the entry out (providerFilter
    /// exclusion). Reserved per CONTEXT D-02 — do not use for per-row skip counts inside
    /// a successful dispatch (those go in <see cref="ProviderCounts.Skipped"/>).
    /// </summary>
    public static EntryOutcome Skipped(ManifestEntry entry, string reason) =>
        new()
        {
            EntryId = entry.EntryId,
            ProviderType = entry.ProviderType,
            Status = EntryStatus.Skipped,
            Message = reason,
            Errors = Array.Empty<string>(),
            Warnings = Array.Empty<string>(),
            Counts = ProviderCounts.Zero,
            Duration = TimeSpan.Zero
        };

    /// <summary>
    /// Build a Failed outcome for entries that never reached a provider (no provider registered)
    /// or that threw before producing a result (files-don't-exist, exception during dispatch).
    /// </summary>
    public static EntryOutcome Failed(ManifestEntry entry, string error, TimeSpan duration = default) =>
        new()
        {
            EntryId = entry.EntryId,
            ProviderType = entry.ProviderType,
            Status = EntryStatus.Failed,
            Message = error,
            Errors = new[] { error },
            Warnings = Array.Empty<string>(),
            Counts = ProviderCounts.Zero,
            Duration = duration
        };

    /// <summary>
    /// Synthesise a Failed outcome that does not correspond to any single entry — used to
    /// surface run-level errors (e.g. <c>CumulativeStrictModeException</c> from the
    /// strict-mode escalator) into the entry-outcomes list per CONTEXT line 99-100, so
    /// <see cref="OrchestratorResult.HasErrors"/> aggregates them via the same path as
    /// per-entry failures.
    /// </summary>
    public static EntryOutcome RunLevelError(string error) =>
        new()
        {
            EntryId = "<run-level>",
            ProviderType = "<run-level>",
            Status = EntryStatus.Failed,
            Message = error,
            Errors = new[] { error },
            Warnings = Array.Empty<string>(),
            Counts = ProviderCounts.Zero,
            Duration = TimeSpan.Zero
        };
}
