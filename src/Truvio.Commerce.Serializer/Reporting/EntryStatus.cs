namespace Truvio.Commerce.Serializer.Reporting;

/// <summary>
/// Phase 43 / REPORT-01 + Phase 44 / WR-02: per-entry outcome status. Distinct from
/// <see cref="Truvio.Commerce.Serializer.Providers.ProviderDeserializeResult.HasErrors"/>
/// because today's silent-skip class (entry filtered out by providerFilter) has no error
/// but also no work — it needs its own observable. Per CONTEXT D-02 (tight definition):
/// <list type="bullet">
/// <item><b>Succeeded</b> — entry dispatched, completed without error. Includes dry-run
/// (would-be work reported in <c>Counts</c>); includes merge-fill with all fields already
/// on target (per-row skip count in <c>ProviderCounts.Skipped</c>).</item>
/// <item><b>Failed</b> — entry dispatched (or attempted), returned errors OR validation/dispatch
/// failure. Includes "files don't exist on disk" (drift between manifest and disk is a real
/// failure, not a quiet case).</item>
/// <item><b>Skipped</b> — orchestrator NEVER dispatched the entry to a provider. Currently
/// exclusively from <c>providerFilter</c> exclusion (per ROADMAP SC-2). Reserved category —
/// do not extend without updating CONTEXT D-02.</item>
/// </list>
/// Phase 44 / WR-02: the previous <c>Warned</c> enum value was deleted — no production code
/// path produced it; <see cref="EntryOutcome.From"/> mapped <c>warnings.Count &gt; 0</c> to
/// it but that branch was only reachable via a parameter no caller passed. If a future
/// warning emitter wants the value back, re-introduce explicitly with an emitter using it.
/// The enum round-trips through
/// <see cref="Truvio.Commerce.Serializer.Infrastructure.ManifestSchema.ManifestJsonOptions"/>
/// which already has <see cref="System.Text.Json.Serialization.JsonStringEnumConverter"/>.
/// No per-type wiring needed.
/// </summary>
public enum EntryStatus
{
    Succeeded,
    Failed,
    Skipped
}
