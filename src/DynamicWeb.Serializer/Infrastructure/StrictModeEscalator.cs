namespace DynamicWeb.Serializer.Infrastructure;

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
    private readonly List<string> _recordedWarnings = new();

    public StrictModeEscalator(bool strict, Action<string>? log)
    {
        _strict = strict;
        _log = log;
    }

    public bool IsStrict => _strict;
    public int WarningCount => _recordedWarnings.Count;

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

        if (_strict && _recordedWarnings.Count < MaxRecordedWarnings)
            _recordedWarnings.Add(warning);
    }

    /// <summary>
    /// Record-only variant of <see cref="Escalate"/> — used by callers (e.g. the
    /// SerializerOrchestrator log-wrapper) that have already emitted the log line
    /// through a separate sink and only need the warning captured for the end-of-run
    /// assertion. Respects the same cap as <see cref="Escalate"/>.
    /// </summary>
    public void RecordOnly(string warning)
    {
        if (_strict && _recordedWarnings.Count < MaxRecordedWarnings)
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
