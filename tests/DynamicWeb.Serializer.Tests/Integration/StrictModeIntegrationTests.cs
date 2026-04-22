using DynamicWeb.Serializer.Configuration;
using DynamicWeb.Serializer.Infrastructure;
using DynamicWeb.Serializer.Models;
using DynamicWeb.Serializer.Providers;
using Moq;
using Xunit;

namespace DynamicWeb.Serializer.Tests.Integration;

/// <summary>
/// Phase 37-04 STRICT-01 / SEED-001: end-to-end tests for the orchestrator strict-mode path.
/// Uses a Mock&lt;ISerializationProvider&gt; to emit a WARNING through the log callback and verifies
/// the orchestrator's wrapper routes it through the escalator, accumulates, and throws at
/// end-of-run in strict mode.
/// </summary>
[Trait("Category", "Phase37-04")]
public class StrictModeIntegrationTests
{
    private static ProviderPredicateDefinition SqlPred(string name) =>
        new()
        {
            Name = name,
            ProviderType = "SqlTable",
            Table = name
        };

    private static Mock<ISerializationProvider> MakeWarningProvider(string warningLine)
    {
        var provider = new Mock<ISerializationProvider>();
        provider.Setup(p => p.ProviderType).Returns("SqlTable");
        provider.Setup(p => p.ValidatePredicate(It.IsAny<ProviderPredicateDefinition>()))
            .Returns(ValidationResult.Success());
        provider.Setup(p => p.Deserialize(
                It.IsAny<ProviderPredicateDefinition>(),
                It.IsAny<string>(),
                It.IsAny<Action<string>?>(),
                It.IsAny<bool>(),
                It.IsAny<ConflictStrategy>(),
                It.IsAny<DynamicWeb.Serializer.Serialization.InternalLinkResolver?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>()))
            .Returns((ProviderPredicateDefinition pred, string _, Action<string>? log, bool _, ConflictStrategy _, DynamicWeb.Serializer.Serialization.InternalLinkResolver? _, IReadOnlyDictionary<string, List<string>>? _, IReadOnlyDictionary<string, List<string>>? _) =>
            {
                log?.Invoke(warningLine);
                return new ProviderDeserializeResult { Created = 1, TableName = pred.Table! };
            });
        return provider;
    }

    // -------------------------------------------------------------------------
    // Strict mode end-to-end
    // -------------------------------------------------------------------------

    [Fact]
    public void Deserialize_StrictMode_OneWarning_ReturnsErrors()
    {
        var registry = new ProviderRegistry();
        registry.Register(MakeWarningProvider("WARNING: template 'eCom_Catalog' missing for page 'X'").Object);
        var orchestrator = new SerializerOrchestrator(registry);

        var logs = new List<string>();
        var escalator = new StrictModeEscalator(strict: true, log: logs.Add);

        var result = orchestrator.DeserializeAll(
            new List<ProviderPredicateDefinition> { SqlPred("EcomPayments") },
            inputRoot: "/input",
            mode: DeploymentMode.Deploy,
            strategy: ConflictStrategy.SourceWins,
            log: logs.Add,
            escalator: escalator);

        // Strict mode: the warning escalated → AssertNoWarnings threw → errors list has the
        // CumulativeStrictModeException message appended.
        Assert.True(result.HasErrors);
        Assert.Contains(result.Errors, e =>
            e.Contains("Strict mode") && e.Contains("template 'eCom_Catalog' missing"));
    }

    [Fact]
    public void Deserialize_Lenient_OneWarning_Succeeds()
    {
        var registry = new ProviderRegistry();
        registry.Register(MakeWarningProvider("WARNING: template missing").Object);
        var orchestrator = new SerializerOrchestrator(registry);

        var logs = new List<string>();
        var escalator = new StrictModeEscalator(strict: false, log: logs.Add);

        var result = orchestrator.DeserializeAll(
            new List<ProviderPredicateDefinition> { SqlPred("EcomPayments") },
            inputRoot: "/input",
            mode: DeploymentMode.Deploy,
            strategy: ConflictStrategy.SourceWins,
            log: logs.Add,
            escalator: escalator);

        // Lenient mode: the warning was logged (real-time) but did not escalate to Errors.
        Assert.False(result.HasErrors);
        Assert.Contains(logs, l => l.Contains("template missing"));
    }

    [Fact]
    public void Deserialize_NullEscalator_UsesLenientDefault()
    {
        // Legacy call path (no escalator provided): keeps v0.4.x behavior exactly —
        // log-and-continue, no error escalation from warnings.
        var registry = new ProviderRegistry();
        registry.Register(MakeWarningProvider("WARNING: something").Object);
        var orchestrator = new SerializerOrchestrator(registry);

        var result = orchestrator.DeserializeAll(
            new List<ProviderPredicateDefinition> { SqlPred("EcomPayments") },
            inputRoot: "/input",
            mode: DeploymentMode.Deploy,
            strategy: ConflictStrategy.SourceWins);

        Assert.False(result.HasErrors);
    }

    [Fact]
    public void Deserialize_StrictMode_TwoWarnings_BothInException()
    {
        // Each provider emits one warning; both are captured in the cumulative exception.
        var providerA = MakeWarningProvider("WARNING: A failure").Object;
        var providerB = new Mock<ISerializationProvider>();
        providerB.Setup(p => p.ProviderType).Returns("Content");
        providerB.Setup(p => p.ValidatePredicate(It.IsAny<ProviderPredicateDefinition>()))
            .Returns(ValidationResult.Success());
        providerB.Setup(p => p.Deserialize(It.IsAny<ProviderPredicateDefinition>(),
                It.IsAny<string>(), It.IsAny<Action<string>?>(), It.IsAny<bool>(), It.IsAny<ConflictStrategy>(), It.IsAny<DynamicWeb.Serializer.Serialization.InternalLinkResolver?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>()))
            .Returns((ProviderPredicateDefinition pred, string _, Action<string>? log, bool _, ConflictStrategy _, DynamicWeb.Serializer.Serialization.InternalLinkResolver? _, IReadOnlyDictionary<string, List<string>>? _, IReadOnlyDictionary<string, List<string>>? _) =>
            {
                log?.Invoke("WARNING: B failure");
                return new ProviderDeserializeResult { Created = 1, TableName = "Content" };
            });

        var registry = new ProviderRegistry();
        registry.Register(providerA);
        registry.Register(providerB.Object);
        var orchestrator = new SerializerOrchestrator(registry);

        var escalator = new StrictModeEscalator(strict: true, log: null);

        var contentPred = new ProviderPredicateDefinition
        {
            Name = "Pages",
            ProviderType = "Content",
            Path = "/",
            AreaId = 1
        };

        var result = orchestrator.DeserializeAll(
            new List<ProviderPredicateDefinition> { SqlPred("EcomPayments"), contentPred },
            inputRoot: "/input",
            mode: DeploymentMode.Deploy,
            strategy: ConflictStrategy.SourceWins,
            escalator: escalator);

        Assert.True(result.HasErrors);
        var cumulativeMessage = result.Errors.Single(e => e.StartsWith("Strict mode"));
        Assert.Contains("A failure", cumulativeMessage);
        Assert.Contains("B failure", cumulativeMessage);
        Assert.Contains("2 warning", cumulativeMessage);
    }

    [Fact]
    public void Deserialize_StrictModeHeader_LoggedAtRunStart()
    {
        var registry = new ProviderRegistry();
        registry.Register(MakeWarningProvider("nothing").Object); // no warning prefix → ignored
        var orchestrator = new SerializerOrchestrator(registry);

        var logs = new List<string>();
        var escalator = new StrictModeEscalator(strict: true, log: null);

        orchestrator.DeserializeAll(
            new List<ProviderPredicateDefinition> { SqlPred("EcomPayments") },
            inputRoot: "/input",
            mode: DeploymentMode.Deploy,
            strategy: ConflictStrategy.SourceWins,
            log: logs.Add,
            escalator: escalator);

        // The header includes the strict flag so operators see whether the run is gated.
        Assert.Contains(logs, l => l.Contains("Strict: True"));
    }

    // -------------------------------------------------------------------------
    // Entry-point default resolution end-to-end
    // -------------------------------------------------------------------------

    [Fact]
    public void StrictModeResolver_CliEntryPoint_DefaultsStrictOn()
    {
        // Matches the contract the CLI/API commands use: EntryPoint.Cli with no overrides
        // produces strict=true.
        var strict = StrictModeResolver.Resolve(
            StrictModeResolver.EntryPoint.Cli,
            configValue: null,
            requestValue: null);
        Assert.True(strict);
    }

    [Fact]
    public void StrictModeResolver_AdminUiEntryPoint_DefaultsStrictOff()
    {
        var strict = StrictModeResolver.Resolve(
            StrictModeResolver.EntryPoint.AdminUi,
            configValue: null,
            requestValue: null);
        Assert.False(strict);
    }

    [Fact]
    public void StrictModeResolver_ConfigOverridesEntryPointDefault()
    {
        // Config=false wins against CLI's default ON.
        var strict = StrictModeResolver.Resolve(
            StrictModeResolver.EntryPoint.Cli,
            configValue: false,
            requestValue: null);
        Assert.False(strict);
    }

    [Fact]
    public void StrictModeResolver_RequestOverridesConfig()
    {
        // Request=true wins against config=false and AdminUi's default OFF.
        var strict = StrictModeResolver.Resolve(
            StrictModeResolver.EntryPoint.AdminUi,
            configValue: false,
            requestValue: true);
        Assert.True(strict);
    }
}
