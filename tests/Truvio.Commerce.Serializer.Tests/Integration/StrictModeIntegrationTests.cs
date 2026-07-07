using Truvio.Commerce.Serializer.Configuration;
using Truvio.Commerce.Serializer.Infrastructure;
using Truvio.Commerce.Serializer.Models;
using Truvio.Commerce.Serializer.Providers;
using Moq;
using Xunit;

namespace Truvio.Commerce.Serializer.Tests.Integration;

/// <summary>
/// Phase 37-04 STRICT-01 / SEED-001: end-to-end tests for the orchestrator strict-mode path.
/// Uses a Mock&lt;ISerializationProvider&gt; to emit a WARNING through the log callback and verifies
/// the orchestrator's wrapper routes it through the escalator, accumulates, and throws at
/// end-of-run in strict mode.
///
/// <para>
/// Phase 44 / CONVERGE-03: ported from predicate-fixture-driven
/// <c>orchestrator.DeserializeAll(predicates, ...)</c> ([Obsolete] overload, deleted in
/// commit 7) to entry-fixture-driven <c>orchestrator.DeserializeEntries(entries, ...)</c>
/// internal test seam (retained per CONTEXT D-06 / must_haves.truths #14 — Layer A tests
/// in <see cref="Providers.SerializerOrchestratorTests"/> use this seam to bypass on-disk
/// manifest setup, and the StrictMode integration tests share that need).
/// </para>
/// </summary>
[Trait("Category", "Phase37-04")]
public class StrictModeIntegrationTests
{
    private static SqlTableEntry SqlEntry(string name) =>
        new()
        {
            EntryId = $"sql/{name}",
            Files = Array.Empty<string>(),
            Table = name
        };

    private static Mock<ISerializationProvider> MakeWarningProvider(string warningLine)
    {
        var provider = new Mock<ISerializationProvider>();
        provider.Setup(p => p.ProviderType).Returns("SqlTable");
        provider.Setup(p => p.Deserialize(
                It.IsAny<ManifestEntry>(),
                It.IsAny<string>(),
                It.IsAny<Action<string>?>(),
                It.IsAny<bool>(),
                It.IsAny<ConflictStrategy>(),
                It.IsAny<Truvio.Commerce.Serializer.Serialization.InternalLinkResolver?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>()))
            .Returns((ManifestEntry e, string _, Action<string>? log, bool _, ConflictStrategy _, Truvio.Commerce.Serializer.Serialization.InternalLinkResolver? _, IReadOnlyDictionary<string, List<string>>? _, IReadOnlyDictionary<string, List<string>>? _) =>
            {
                log?.Invoke(warningLine);
                var tableName = (e as SqlTableEntry)?.Table ?? "Test";
                return new ProviderDeserializeResult { Created = 1, TableName = tableName };
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

        var result = orchestrator.DeserializeEntries(
            new List<ManifestEntry> { SqlEntry("EcomPayments") },
            modeRoot: "/input",
            mode: SerializerMode.Replace,
            strategy: ConflictStrategy.SourceWins,
            log: logs.Add,
            isDryRun: false,
            providerFilter: null,
            escalator: escalator,
            excludeFieldsByItemType: null,
            excludeXmlElementsByType: null);

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

        var result = orchestrator.DeserializeEntries(
            new List<ManifestEntry> { SqlEntry("EcomPayments") },
            modeRoot: "/input",
            mode: SerializerMode.Replace,
            strategy: ConflictStrategy.SourceWins,
            log: logs.Add,
            isDryRun: false,
            providerFilter: null,
            escalator: escalator,
            excludeFieldsByItemType: null,
            excludeXmlElementsByType: null);

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

        var result = orchestrator.DeserializeEntries(
            new List<ManifestEntry> { SqlEntry("EcomPayments") },
            modeRoot: "/input",
            mode: SerializerMode.Replace,
            strategy: ConflictStrategy.SourceWins,
            log: null,
            isDryRun: false,
            providerFilter: null,
            escalator: null,
            excludeFieldsByItemType: null,
            excludeXmlElementsByType: null);

        Assert.False(result.HasErrors);
    }

    [Fact]
    public void Deserialize_StrictMode_TwoWarnings_BothInException()
    {
        // Each provider emits one warning; both are captured in the cumulative exception.
        var providerA = MakeWarningProvider("WARNING: A failure").Object;
        var providerB = new Mock<ISerializationProvider>();
        providerB.Setup(p => p.ProviderType).Returns("Content");
        providerB.Setup(p => p.Deserialize(It.IsAny<ManifestEntry>(),
                It.IsAny<string>(), It.IsAny<Action<string>?>(), It.IsAny<bool>(), It.IsAny<ConflictStrategy>(), It.IsAny<Truvio.Commerce.Serializer.Serialization.InternalLinkResolver?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>()))
            .Returns((ManifestEntry _, string _, Action<string>? log, bool _, ConflictStrategy _, Truvio.Commerce.Serializer.Serialization.InternalLinkResolver? _, IReadOnlyDictionary<string, List<string>>? _, IReadOnlyDictionary<string, List<string>>? _) =>
            {
                log?.Invoke("WARNING: B failure");
                return new ProviderDeserializeResult { Created = 1, TableName = "Content" };
            });

        var registry = new ProviderRegistry();
        registry.Register(providerA);
        registry.Register(providerB.Object);
        var orchestrator = new SerializerOrchestrator(registry);

        var escalator = new StrictModeEscalator(strict: true, log: null);

        var contentEntry = new ContentEntry
        {
            EntryId = "content/area-1",
            Files = Array.Empty<string>(),
            AreaId = 1,
            AreaName = "Area 1",
            Path = "/",
            PageId = 0
        };

        var result = orchestrator.DeserializeEntries(
            new List<ManifestEntry> { SqlEntry("EcomPayments"), contentEntry },
            modeRoot: "/input",
            mode: SerializerMode.Replace,
            strategy: ConflictStrategy.SourceWins,
            log: null,
            isDryRun: false,
            providerFilter: null,
            escalator: escalator,
            excludeFieldsByItemType: null,
            excludeXmlElementsByType: null);

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

        orchestrator.DeserializeEntries(
            new List<ManifestEntry> { SqlEntry("EcomPayments") },
            modeRoot: "/input",
            mode: SerializerMode.Replace,
            strategy: ConflictStrategy.SourceWins,
            log: logs.Add,
            isDryRun: false,
            providerFilter: null,
            escalator: escalator,
            excludeFieldsByItemType: null,
            excludeXmlElementsByType: null);

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
