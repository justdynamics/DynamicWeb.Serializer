using DynamicWeb.Serializer.Infrastructure;
using Xunit;

namespace DynamicWeb.Serializer.Tests.Infrastructure;

/// <summary>
/// Phase 37-04 / STRICT-01 / SEED-001: tests for the strict-mode escalator and
/// the entry-point-aware default resolver. Cover the three strict-mode contracts:
/// 1) lenient = log + continue (v0.4.x parity)
/// 2) strict  = log + record + AssertNoWarnings throws CumulativeStrictModeException
/// 3) defaults precedence: request > config > entry-point-default
/// </summary>
[Trait("Category", "Phase37-04")]
public class StrictModeEscalatorTests
{
    // -------------------------------------------------------------------------
    // Escalate behavior
    // -------------------------------------------------------------------------

    [Fact]
    public void Escalate_Lenient_LogsAndContinues()
    {
        var logs = new List<string>();
        var escalator = new StrictModeEscalator(strict: false, log: logs.Add);

        escalator.Escalate("WARNING: template missing");

        Assert.Contains(logs, l => l.Contains("template missing"));
        Assert.Equal(0, escalator.WarningCount);
    }

    [Fact]
    public void Escalate_Strict_LogsAndRecords()
    {
        var logs = new List<string>();
        var escalator = new StrictModeEscalator(strict: true, log: logs.Add);

        escalator.Escalate("WARNING: template missing");

        Assert.Contains(logs, l => l.Contains("template missing"));
        Assert.Equal(1, escalator.WarningCount);
    }

    [Fact]
    public void Escalate_PrefixesWarningWhenMissing()
    {
        var logs = new List<string>();
        var escalator = new StrictModeEscalator(strict: false, log: logs.Add);

        escalator.Escalate("template missing");

        Assert.Contains(logs, l => l.StartsWith("WARNING:"));
    }

    [Fact]
    public void Escalate_DoesNotDoublePrefixWarning()
    {
        var logs = new List<string>();
        var escalator = new StrictModeEscalator(strict: false, log: logs.Add);

        escalator.Escalate("WARNING: already has prefix");

        Assert.Single(logs);
        Assert.Equal("WARNING: already has prefix", logs[0]);
    }

    [Fact]
    public void Escalate_NullLog_NoThrow()
    {
        var escalator = new StrictModeEscalator(strict: true, log: null);
        escalator.Escalate("no log sink");  // no throw
        Assert.Equal(1, escalator.WarningCount);
    }

    // -------------------------------------------------------------------------
    // AssertNoWarnings
    // -------------------------------------------------------------------------

    [Fact]
    public void AssertNoWarnings_Lenient_NeverThrows()
    {
        var escalator = new StrictModeEscalator(strict: false, log: null);
        escalator.Escalate("w1");
        escalator.Escalate("w2");
        escalator.AssertNoWarnings(); // no throw
    }

    [Fact]
    public void AssertNoWarnings_StrictEmpty_DoesNotThrow()
    {
        var escalator = new StrictModeEscalator(strict: true, log: null);
        escalator.AssertNoWarnings(); // no throw
    }

    [Fact]
    public void AssertNoWarnings_StrictWithWarnings_Throws()
    {
        var escalator = new StrictModeEscalator(strict: true, log: null);
        escalator.Escalate("template missing on page 'X'");
        escalator.Escalate("owner role not found on target");

        var ex = Assert.Throws<CumulativeStrictModeException>(() => escalator.AssertNoWarnings());
        Assert.Equal(2, ex.Warnings.Count);
    }

    [Fact]
    public void CumulativeStrictModeException_MessageListsAllWarnings()
    {
        var ex = new CumulativeStrictModeException(new[] { "w1", "w2", "w3" });

        Assert.Contains("3 warning", ex.Message);
        Assert.Contains("w1", ex.Message);
        Assert.Contains("w2", ex.Message);
        Assert.Contains("w3", ex.Message);
    }

    [Fact]
    public void Null_Escalator_IsLenient()
    {
        Assert.False(StrictModeEscalator.Null.IsStrict);
        StrictModeEscalator.Null.Escalate("noise"); // no throw
        StrictModeEscalator.Null.AssertNoWarnings(); // no throw
    }

    [Fact]
    public void Escalate_Strict_CapsRecordedWarningsAt10000()
    {
        // T-37-04-03 DoS guard: pathological input capped at 10k recorded warnings;
        // beyond that, escalate is log-only (still throws in AssertNoWarnings).
        var escalator = new StrictModeEscalator(strict: true, log: null);
        for (var i = 0; i < 10_050; i++) escalator.Escalate($"w{i}");
        Assert.Equal(10_000, escalator.WarningCount);
    }

    // -------------------------------------------------------------------------
    // StrictModeResolver — entry-point-aware defaults
    // -------------------------------------------------------------------------

    [Fact]
    public void Resolve_RequestTrue_OverridesAll()
    {
        Assert.True(StrictModeResolver.Resolve(
            StrictModeResolver.EntryPoint.AdminUi, configValue: false, requestValue: true));
    }

    [Fact]
    public void Resolve_RequestFalse_OverridesAll()
    {
        Assert.False(StrictModeResolver.Resolve(
            StrictModeResolver.EntryPoint.Cli, configValue: true, requestValue: false));
    }

    [Fact]
    public void Resolve_ConfigTrue_UsedWhenRequestNull()
    {
        Assert.True(StrictModeResolver.Resolve(
            StrictModeResolver.EntryPoint.AdminUi, configValue: true, requestValue: null));
    }

    [Fact]
    public void Resolve_ConfigFalse_UsedWhenRequestNull()
    {
        Assert.False(StrictModeResolver.Resolve(
            StrictModeResolver.EntryPoint.Cli, configValue: false, requestValue: null));
    }

    [Fact]
    public void Resolve_Cli_DefaultsTrue_WhenBothNull()
    {
        Assert.True(StrictModeResolver.Resolve(
            StrictModeResolver.EntryPoint.Cli, configValue: null, requestValue: null));
    }

    [Fact]
    public void Resolve_Api_DefaultsTrue_WhenBothNull()
    {
        Assert.True(StrictModeResolver.Resolve(
            StrictModeResolver.EntryPoint.Api, configValue: null, requestValue: null));
    }

    [Fact]
    public void Resolve_AdminUi_DefaultsFalse_WhenBothNull()
    {
        Assert.False(StrictModeResolver.Resolve(
            StrictModeResolver.EntryPoint.AdminUi, configValue: null, requestValue: null));
    }
}
