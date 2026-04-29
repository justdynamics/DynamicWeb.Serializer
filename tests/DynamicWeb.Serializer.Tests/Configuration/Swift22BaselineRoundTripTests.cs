using System.Linq;
using DynamicWeb.Serializer.Configuration;
using Xunit;

namespace DynamicWeb.Serializer.Tests.Configuration;

/// <summary>
/// Phase 40 Plan 04 Task 1: round-trip the canonical Swift 2.2 baseline
/// (<c>src/DynamicWeb.Serializer/Configuration/swift2.2-combined.json</c>) through
/// <see cref="ConfigLoader.Load(string, SqlIdentifierValidator?)"/> with the
/// <c>identifierValidator: null</c> overload, asserting the new flat-shape parse
/// + per-predicate <c>Mode</c> distribution match the baseline contents.
///
/// Scope decision (per Plan 04 Task 1, checker Warning #7 option (b)): this test
/// scopes its assertion to JSON-shape parse + <c>Mode</c> round-trip — it does NOT
/// exercise the <see cref="SqlIdentifierValidator"/> pipeline. Extending the
/// validator allowlist with the 23 baseline SqlTable names would be disproportionate
/// to value; the validator path is exercised end-to-end via the live host smoke test
/// in Plan 04 Task 4 (the human-verify checkpoint), not via unit test.
///
/// Therefore this class does NOT inherit from
/// <c>ConfigLoaderValidatorFixtureBase</c> — there is no fixture state to manage
/// because <c>identifierValidator: null</c> bypasses the static validator override entirely.
/// </summary>
public class Swift22BaselineRoundTripTests
{
    private static string GetBaselinePath()
    {
        // Tests/DynamicWeb.Serializer.Tests/bin/Debug/net8.0 → walk up to repo root → Configuration
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "DynamicWeb.Serializer", "Configuration", "swift2.2-combined.json");
        return Path.GetFullPath(path);
    }

    [Fact]
    public void Load_Swift22Combined_HasExpectedPredicateCounts()
    {
        var path = GetBaselinePath();
        Assert.True(File.Exists(path), $"Baseline file not found at: {path}");

        var config = ConfigLoader.Load(path, identifierValidator: null);

        // 17 Deploy + 9 Seed = 26 total — matches the source file's predicate set.
        Assert.Equal(26, config.Predicates.Count);
        Assert.Equal(17, config.Predicates.Count(p => p.Mode == DeploymentMode.Deploy));
        Assert.Equal(9, config.Predicates.Count(p => p.Mode == DeploymentMode.Seed));
    }

    [Fact]
    public void Load_Swift22Combined_HasTopLevelExcludeXmlElementsByType()
    {
        var path = GetBaselinePath();
        var config = ConfigLoader.Load(path, identifierValidator: null);

        // The dictionary contains 16 keys at the top level (D-04 hoist).
        Assert.True(config.ExcludeXmlElementsByType.Count >= 16,
            $"Expected at least 16 entries in ExcludeXmlElementsByType, got {config.ExcludeXmlElementsByType.Count}");
    }

    [Fact]
    public void Load_Swift22Combined_EcomShops_IsDeploy()
    {
        var path = GetBaselinePath();
        var config = ConfigLoader.Load(path, identifierValidator: null);

        var p = config.Predicates.SingleOrDefault(x => x.Name == "EcomShops");
        Assert.NotNull(p);
        Assert.Equal(DeploymentMode.Deploy, p!.Mode);
    }

    [Fact]
    public void Load_Swift22Combined_EcomGroups_IsSeed()
    {
        var path = GetBaselinePath();
        var config = ConfigLoader.Load(path, identifierValidator: null);

        var p = config.Predicates.SingleOrDefault(x => x.Name == "EcomGroups");
        Assert.NotNull(p);
        Assert.Equal(DeploymentMode.Seed, p!.Mode);
    }

    [Fact]
    public void Load_Swift22Combined_TopLevelOutputSubfolders_HoistedFromSection()
    {
        var path = GetBaselinePath();
        var config = ConfigLoader.Load(path, identifierValidator: null);

        Assert.Equal("deploy", config.DeployOutputSubfolder);
        Assert.Equal("seed", config.SeedOutputSubfolder);
    }
}
