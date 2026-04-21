using DynamicWeb.Serializer.AdminUI.Commands;
using DynamicWeb.Serializer.Providers;
using Dynamicweb.CoreUI.Data;
using Xunit;

namespace DynamicWeb.Serializer.Tests.AdminUI;

/// <summary>
/// Phase 38 Plan 01 Task 2 — D.1 (query-param fallback) + D.2 (HTTP status hardening)
/// regression tests for <see cref="SerializerDeserializeCommand"/>. Mirrors the
/// <see cref="SerializerSerializeCommandTests"/> fixture with the unconditional
/// zero-error == Ok invariant applied to the Deserialize command.
/// </summary>
[Trait("Category", "Phase38")]
public class SerializerDeserializeCommandTests
{
    [Fact]
    public void Handle_JsonBodyMode_ParsesSeed()
    {
        // D-38-11 baseline: direct JSON-body path. The mode-string parse must succeed
        // (NotEqual Invalid); downstream resolution may still produce Error (no config,
        // no subfolder), but the mode gate itself cannot reject "seed" as Invalid.
        var cmd = new SerializerDeserializeCommand { Mode = "seed" };
        var result = cmd.Handle();
        Assert.NotEqual(CommandResult.ResultType.Invalid, result.Status);
    }

    [Fact]
    public void Handle_InvalidMode_ReturnsInvalid()
    {
        // T-38-D1-01 threat mitigation applies equally to Deserialize.
        var cmd = new SerializerDeserializeCommand { Mode = "bogus" };
        var result = cmd.Handle();
        Assert.Equal(CommandResult.ResultType.Invalid, result.Status);
        Assert.Contains("Invalid mode", result.Message ?? string.Empty);
    }

    [Fact]
    public void Handle_ZeroErrors_SynthOrchestratorResult_ReturnsOk()
    {
        // D-38-12 (hardened per checker B3): UNCONDITIONAL assertion, same invariant
        // as the Serialize test. Construct a synthetic zero-error OrchestratorResult
        // and drive the Deserialize command's status-mapping branch directly. No
        // environment dependency, no escape hatch. A regression here would reintroduce
        // the HTTP 400-on-zero-errors bug on the deserialize endpoint.
        var synth = SynthOrchestratorResult.WithEmptyErrors();

        var mapped = SerializerDeserializeCommand.InvokeMapStatusForTest(synth);

        Assert.Equal(CommandResult.ResultType.Ok, mapped.Status);
    }

    [Fact]
    public void Handle_ZeroErrors_MessageContainsErrorsLiteral_StatusStillOk()
    {
        // D-38-12 anti-regression on Deserialize: even when the Message would contain
        // "Errors:" literally, Status MUST remain Ok when HasErrors == false.
        var synth = SynthOrchestratorResult.WithEmptyErrors();

        var mapped = SerializerDeserializeCommand.InvokeMapStatusForTest(synth);

        Assert.Equal(CommandResult.ResultType.Ok, mapped.Status);
    }
}
