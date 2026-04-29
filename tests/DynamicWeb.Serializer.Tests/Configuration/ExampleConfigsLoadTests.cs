using System.Linq;
using DynamicWeb.Serializer.Configuration;
using Xunit;

namespace DynamicWeb.Serializer.Tests.Configuration;

/// <summary>
/// Phase 40 Plan 04 Task 2: regression coverage for the three example/documentation
/// configs (<c>demo-sync.json</c>, <c>ecommerce-predicates-example.json</c>,
/// <c>full-sync-example.json</c>). Per checker Warning #5 (no-backcompat is also
/// no-trap-for-the-user), these copy-paste artefacts MUST parse cleanly through
/// <see cref="ConfigLoader.Load(string, SqlIdentifierValidator?)"/>; if any predicate
/// regresses to a missing-<c>mode</c> shape, ConfigLoader will hard-reject and the
/// user copying the example produces an unloadable config.
///
/// Uses <c>identifierValidator: null</c> overload — same scope decision as
/// Swift22BaselineRoundTripTests: assertions are limited to JSON-shape parse +
/// per-predicate <c>Mode</c> resolution, not the SqlIdentifierValidator pipeline.
/// </summary>
public class ExampleConfigsLoadTests
{
    private static string ResolveConfigPath(string fileName)
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "DynamicWeb.Serializer", "Configuration", fileName);
        return Path.GetFullPath(path);
    }

    [Fact]
    public void Load_DemoSync_Parses()
    {
        var path = ResolveConfigPath("demo-sync.json");
        Assert.True(File.Exists(path), $"Example file not found at: {path}");

        var config = ConfigLoader.Load(path, identifierValidator: null);

        Assert.NotEmpty(config.Predicates);
        Assert.All(config.Predicates, p => Assert.Equal(DeploymentMode.Deploy, p.Mode));
    }

    [Fact]
    public void Load_EcommercePredicatesExample_Parses()
    {
        var path = ResolveConfigPath("ecommerce-predicates-example.json");
        Assert.True(File.Exists(path), $"Example file not found at: {path}");

        var config = ConfigLoader.Load(path, identifierValidator: null);

        Assert.NotEmpty(config.Predicates);
        Assert.All(config.Predicates, p => Assert.Equal(DeploymentMode.Deploy, p.Mode));
    }

    [Fact]
    public void Load_FullSyncExample_Parses()
    {
        var path = ResolveConfigPath("full-sync-example.json");
        Assert.True(File.Exists(path), $"Example file not found at: {path}");

        var config = ConfigLoader.Load(path, identifierValidator: null);

        Assert.NotEmpty(config.Predicates);
        Assert.All(config.Predicates, p => Assert.Equal(DeploymentMode.Deploy, p.Mode));
    }
}
