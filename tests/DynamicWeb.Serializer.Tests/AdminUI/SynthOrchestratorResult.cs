using System.Collections.Generic;
using DynamicWeb.Serializer.Providers;

namespace DynamicWeb.Serializer.Tests.AdminUI;

/// <summary>
/// Phase 38 D-38-12 (B3 hardening): synthetic <see cref="OrchestratorResult"/> factory for
/// driving the zero-error status-mapping branch of SerializerSerializeCommand.Handle()
/// (and SerializerDeserializeCommand.Handle()) without touching the DW DB, filesystem,
/// or HTTP stack. Produces a result where <see cref="OrchestratorResult.HasErrors"/>
/// evaluates to <c>false</c> — any implementation that maps that state to anything
/// other than <see cref="Dynamicweb.CoreUI.Data.CommandResult.ResultType.Ok"/> has
/// regressed D-38-12.
/// </summary>
internal static class SynthOrchestratorResult
{
    /// <summary>
    /// Construct an <see cref="OrchestratorResult"/> whose <c>Errors</c> list and
    /// <c>SerializeResults</c> collection are both empty. The computed
    /// <c>HasErrors</c> expression is therefore guaranteed to be <c>false</c>.
    /// </summary>
    public static OrchestratorResult WithEmptyErrors()
    {
        return new OrchestratorResult
        {
            Errors = new List<string>(),
            SerializeResults = new List<SerializeResult>(),
            DeserializeResults = new List<ProviderDeserializeResult>()
        };
    }
}
