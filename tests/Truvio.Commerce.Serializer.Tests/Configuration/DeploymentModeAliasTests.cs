using Truvio.Commerce.Serializer.Configuration;
using Xunit;

namespace Truvio.Commerce.Serializer.Tests.Configuration;

/// <summary>
/// DIST-04 (v3.0 Distribution): replace/merge accepted as aliases for deploy/seed, alias-first.
/// deploy ≡ replace (source-wins), seed ≡ merge (destination-wins). Old names keep working;
/// the resolver also yields the normalized on-disk label the caller requested.
/// </summary>
public class DeploymentModeAliasTests
{
    [Theory]
    [InlineData("deploy", DeploymentMode.Deploy, "deploy")]
    [InlineData("replace", DeploymentMode.Deploy, "replace")]
    [InlineData("seed", DeploymentMode.Seed, "seed")]
    [InlineData("merge", DeploymentMode.Seed, "merge")]
    public void TryResolve_KnownModes_MapsToEnumAndLabel(string input, DeploymentMode expectedMode, string expectedLabel)
    {
        var ok = DeploymentModeAlias.TryResolve(input, out var mode, out var label);

        Assert.True(ok);
        Assert.Equal(expectedMode, mode);
        Assert.Equal(expectedLabel, label);
    }

    [Theory]
    [InlineData("DEPLOY", "deploy")]
    [InlineData("Replace", "replace")]
    [InlineData("  merge  ", "merge")]
    [InlineData("SEED", "seed")]
    public void TryResolve_IsCaseInsensitiveAndTrimmed(string input, string expectedLabel)
    {
        var ok = DeploymentModeAlias.TryResolve(input, out _, out var label);

        Assert.True(ok);
        Assert.Equal(expectedLabel, label);
    }

    [Theory]
    [InlineData("bogus")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void TryResolve_UnknownMode_ReturnsFalse(string? input)
    {
        var ok = DeploymentModeAlias.TryResolve(input, out _, out _);

        Assert.False(ok);
    }

    [Fact]
    public void Candidates_Deploy_LegacyFirstThenAlias()
    {
        Assert.Equal(new[] { "deploy", "replace" }, DeploymentModeAlias.Candidates(DeploymentMode.Deploy));
    }

    [Fact]
    public void Candidates_Seed_LegacyFirstThenAlias()
    {
        Assert.Equal(new[] { "seed", "merge" }, DeploymentModeAlias.Candidates(DeploymentMode.Seed));
    }
}
