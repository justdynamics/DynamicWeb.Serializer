using Truvio.Commerce.Serializer.AdminUI.Models;
using Truvio.Commerce.Serializer.Configuration;
using Truvio.Commerce.Serializer.Models;
using Truvio.Commerce.Serializer.Tests.TestHelpers;
using Xunit;

namespace Truvio.Commerce.Serializer.Tests.AdminUI;

public class CarveOutDetailModelTests : ConfigLoaderValidatorFixtureBase
{
    private readonly string _tempDir;

    public CarveOutDetailModelTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "CarveOutDetailTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public override void Dispose()
    {
        ConfigPathResolver.TestOverridePath = null;
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
        base.Dispose();
    }

    private string WriteConfig()
    {
        var config = new SerializerConfiguration
        {
            OutputDirectory = "Serializer",
            ExcludeXmlElementsByType = new Dictionary<string, List<string>>
            {
                ["eCom_CartV2"] = new() { "Mail1Recipient", "DefaultPaymentId" }
            },
            ExcludeFieldsByItemType = new Dictionary<string, List<string>>
            {
                ["Swift-v2_Master"] = new() { "Google_APIKey" }
            },
            Predicates = new List<ProviderPredicateDefinition>
            {
                new()
                {
                    Name = "EcomPayments",
                    Mode = DeploymentMode.Deploy,
                    ProviderType = "SqlTable",
                    Table = "EcomPayments",
                    // Column must be on the fixture's validator allowlist.
                    ExcludeFields = new List<string> { "LastModified" },
                    ExcludeXmlElements = new List<string> { "apiKey", "sharedSecret" }
                }
            }
        };
        var path = Path.Combine(_tempDir, "Serializer.config.json");
        ConfigWriter.Save(config, path);
        ConfigPathResolver.TestOverridePath = path;
        return path;
    }

    [Fact]
    public void Load_XmlElementsKind_ListsExcludedElements()
    {
        WriteConfig();

        var model = CarveOutDetailModel.Load("eCom_CartV2", CarveOutDetailModel.KindXmlElements);

        Assert.Equal("eCom_CartV2", model.TypeName);
        Assert.Contains("Mail1Recipient", model.ExcludedFields);
        Assert.Contains("DefaultPaymentId", model.ExcludedFields);
        Assert.Contains("stay local", model.Summary);
    }

    [Fact]
    public void Load_ItemTypeKind_ListsExcludedFields_CaseInsensitive()
    {
        WriteConfig();

        var model = CarveOutDetailModel.Load("SWIFT-V2_MASTER", CarveOutDetailModel.KindItemTypeFields);

        Assert.Contains("Google_APIKey", model.ExcludedFields);
    }

    [Fact]
    public void Load_PredicateKind_ListsColumnsAndXmlElements_AndIndex()
    {
        WriteConfig();

        var model = CarveOutDetailModel.Load("EcomPayments", CarveOutDetailModel.KindPredicate);

        Assert.Equal(1, model.PredicateIndex);
        Assert.Contains("LastModified", model.ExcludedFields);
        Assert.Contains("sharedSecret", model.ExcludedFields);
        Assert.Contains("EcomPayments", model.Summary);
    }

    [Fact]
    public void Load_UnknownPredicate_ReportsNotFound()
    {
        WriteConfig();

        var model = CarveOutDetailModel.Load("Nope", CarveOutDetailModel.KindPredicate);

        Assert.Contains("not found", model.Summary);
        Assert.Equal(0, model.PredicateIndex);
    }

    [Fact]
    public void Load_NoConfig_ReportsMissingConfiguration()
    {
        ConfigPathResolver.TestOverridePath = Path.Combine(_tempDir, "does-not-exist.json");

        var model = CarveOutDetailModel.Load("eCom_CartV2", CarveOutDetailModel.KindXmlElements);

        Assert.Contains("No serializer configuration", model.Summary);
    }
}
