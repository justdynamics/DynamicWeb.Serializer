namespace Truvio.Commerce.Serializer.Infrastructure;

/// <summary>
/// Phase 37-04 / STRICT-01 / SEED-001: routes recoverable warnings through a single policy.
/// <list type="bullet">
/// <item><b>Lenient mode</b> — log and continue (v0.4.x behavior).</item>
/// <item><b>Strict mode</b> — log, record, and throw <see cref="CumulativeStrictModeException"/>
/// at end of run via <see cref="AssertNoWarnings"/>.</item>
/// </list>
/// <para>Per D-18, unresolvable links, missing templates, unresolvable cache names,
/// permission-map fallbacks, schema-drift drops, and FK orphans all escalate.</para>
/// </summary>
public class StrictModeEscalator
{
    /// <summary>
    /// T-37-04-03 DoS guard: cap recorded warnings so a pathological input (e.g. every row
    /// on a 1500-page baseline emitting a warning) can't balloon process memory. Beyond
    /// the cap <see cref="Escalate"/> is log-only (the cap is still sufficient for
    /// <see cref="AssertNoWarnings"/> to throw).
    /// </summary>
    public const int MaxRecordedWarnings = 10_000;

    private readonly bool _strict;
    private readonly Action<string>? _log;
    private readonly IReadOnlySet<string> _quarantinedClasses;
    private readonly List<string> _recordedWarnings = new();
    private readonly List<string> _quarantinedWarnings = new();

    public StrictModeEscalator(bool strict, Action<string>? log)
        : this(strict, log, quarantinedClasses: null)
    {
    }

    /// <param name="quarantinedClasses">
    /// Engine issue #5: warning classes (see <see cref="StrictModeWarningClass"/>) that are
    /// reported loudly but do NOT contribute to the end-of-run failure. A single unresolvable
    /// link used to fail the whole pass, turning a cosmetic defect in one row into a total
    /// content outage; quarantining that class degrades it to a per-item escalation while
    /// every other class keeps failing the run. Null / empty = no quarantine (default).
    /// </param>
    public StrictModeEscalator(bool strict, Action<string>? log, IReadOnlySet<string>? quarantinedClasses)
    {
        _strict = strict;
        _log = log;
        _quarantinedClasses = quarantinedClasses ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    public bool IsStrict => _strict;
    public int WarningCount => _recordedWarnings.Count;

    /// <summary>Warnings routed to quarantine instead of the end-of-run failure.</summary>
    public int QuarantinedCount => _quarantinedWarnings.Count;

    /// <summary>
    /// The quarantined warnings verbatim. The caller (orchestrator) reports these at end of
    /// run so a quarantined defect is never silent — it just doesn't abort the pass.
    /// </summary>
    public IReadOnlyList<string> QuarantinedWarnings => _quarantinedWarnings;

    /// <summary>
    /// Log a warning. In strict mode, record it (up to <see cref="MaxRecordedWarnings"/>)
    /// for the end-of-run assertion. The message SHOULD include context (predicate,
    /// page GUID, table name) because the end-of-run exception surfaces it verbatim.
    /// </summary>
    public void Escalate(string warning)
    {
        var output = warning.StartsWith("WARNING", StringComparison.OrdinalIgnoreCase)
            ? warning
            : "WARNING: " + warning;
        _log?.Invoke(output);

        Record(warning);
    }

    /// <summary>
    /// Record-only variant of <see cref="Escalate"/> — used by callers (e.g. the
    /// SerializerOrchestrator log-wrapper) that have already emitted the log line
    /// through a separate sink and only need the warning captured for the end-of-run
    /// assertion. Respects the same cap as <see cref="Escalate"/>.
    /// </summary>
    public void RecordOnly(string warning) => Record(warning);

    /// <summary>
    /// Shared capture step for <see cref="Escalate"/> and <see cref="RecordOnly"/>: no-op in
    /// lenient mode; in strict mode routes the warning to the quarantine buffer when its class
    /// is quarantined, otherwise to the end-of-run failure buffer. Both buffers respect
    /// <see cref="MaxRecordedWarnings"/>.
    /// </summary>
    private void Record(string warning)
    {
        if (!_strict) return;

        if (_quarantinedClasses.Count > 0)
        {
            var warningClass = StrictModeWarningClass.Classify(warning);
            if (warningClass != null && _quarantinedClasses.Contains(warningClass))
            {
                if (_quarantinedWarnings.Count < MaxRecordedWarnings)
                    _quarantinedWarnings.Add(warning);
                return;
            }
        }

        if (_recordedWarnings.Count < MaxRecordedWarnings)
            _recordedWarnings.Add(warning);
    }

    /// <summary>
    /// In strict mode with recorded warnings, throw a single aggregated
    /// <see cref="CumulativeStrictModeException"/>. No-op in lenient mode or when the
    /// buffer is empty. Called once at the end of a run (the orchestrator does this).
    /// </summary>
    public void AssertNoWarnings()
    {
        if (!_strict || _recordedWarnings.Count == 0) return;
        throw new CumulativeStrictModeException(_recordedWarnings);
    }

    /// <summary>
    /// Null instance for call sites that don't care about strict mode. Always lenient,
    /// never records. Used as the default parameter value on <c>DeserializeAll</c> etc.
    /// so legacy callers keep v0.4.x behavior without migration.
    /// </summary>
    public static readonly StrictModeEscalator Null = new(strict: false, log: null);
}

/// <summary>
/// Engine issue #5: names the warning classes that a run may quarantine instead of failing
/// on. Classification is by message text because the orchestrator's log wrapper only ever
/// sees the emitted line — the emitting call sites are spread across the pipeline and are
/// not escalator-aware.
///
/// <para>Quarantine is opt-in and OFF by default: a CI gate that says "strict" keeps failing
/// on every class unless the run explicitly asks for a class to be quarantined.</para>
/// </summary>
public static class StrictModeWarningClass
{
    /// <summary>
    /// An internal link (page or paragraph) that could not be remapped to a target id and was
    /// left at its source id. Cosmetic on one row; the row itself still deserializes.
    /// Emitted by <c>InternalLinkResolver</c>.
    /// </summary>
    public const string UnresolvableLink = "unresolvable-link";

    /// <summary>Every quarantinable class — the set a caller may choose from.</summary>
    public static readonly IReadOnlyList<string> All = new[] { UnresolvableLink };

    /// <summary>
    /// Returns the class of <paramref name="warning"/>, or <c>null</c> when it belongs to no
    /// quarantinable class (those always fail the run under strict mode).
    /// </summary>
    public static string? Classify(string? warning)
    {
        if (string.IsNullOrEmpty(warning)) return null;

        if (warning.Contains("Unresolvable page ID", StringComparison.OrdinalIgnoreCase) ||
            warning.Contains("Unresolvable paragraph ID", StringComparison.OrdinalIgnoreCase))
        {
            return UnresolvableLink;
        }

        return null;
    }
}

/// <summary>
/// Phase 37-04: single aggregated exception thrown at end-of-run when strict mode
/// recorded one or more warnings. The message lists each warning verbatim.
/// </summary>
public class CumulativeStrictModeException : Exception
{
    public IReadOnlyList<string> Warnings { get; }

    public CumulativeStrictModeException(IReadOnlyList<string> warnings)
        : base($"Strict mode: {warnings.Count} warning(s) escalated to failure:\n  - " +
               string.Join("\n  - ", warnings))
    {
        Warnings = warnings;
    }
}

/// <summary>
/// D-16: entry-point-aware default resolver. API / CLI entry points default strict ON
/// (CI/CD target); admin UI defaults strict OFF (interactive experimentation).
/// Both overridable by config or by an explicit request parameter.
///
/// <para>Precedence: request-parameter &gt; config-value &gt; entry-point-default.</para>
/// </summary>
public static class StrictModeResolver
{
    public enum EntryPoint { Cli, Api, AdminUi }

    public static bool Resolve(EntryPoint entryPoint, bool? configValue, bool? requestValue)
    {
        if (requestValue.HasValue) return requestValue.Value;
        if (configValue.HasValue) return configValue.Value;
        return entryPoint switch
        {
            EntryPoint.Cli => true,       // D-16: CI/CD target, default ON
            EntryPoint.Api => true,       // D-16: CI/CD target, default ON
            EntryPoint.AdminUi => false,  // D-16: interactive, default OFF
            _ => false
        };
    }
}
