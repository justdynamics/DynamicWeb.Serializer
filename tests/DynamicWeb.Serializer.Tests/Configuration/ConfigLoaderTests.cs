using DynamicWeb.Serializer.Configuration;
using DynamicWeb.Serializer.Models;
using Xunit;

namespace DynamicWeb.Serializer.Tests.Configuration;

public class ConfigLoaderTests : IDisposable
{
    private readonly string _tempDir;
    private readonly List<string> _tempFiles = new();

    public ConfigLoaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ConfigLoaderTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string WriteConfigFile(string json)
    {
        var path = Path.Combine(_tempDir, Guid.NewGuid().ToString("N")[..8] + ".json");
        File.WriteAllText(path, json);
        return path;
    }

    // -------------------------------------------------------------------------
    // Valid config loading
    // -------------------------------------------------------------------------

    [Fact]
    public void Load_ValidConfig_ReturnsSerializerConfiguration()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "logLevel": "info",
              "predicates": [
                {
                  "name": "Customer Center",
                  "path": "/Customer Center",
                  "areaId": 1,
                  "excludes": ["/Customer Center/Archive"]
                }
              ]
            }
            """;
        var path = WriteConfigFile(json);

        var config = ConfigLoader.Load(path);

        Assert.Equal("/serialization", config.OutputDirectory);
        Assert.Equal("info", config.LogLevel);
        Assert.Single(config.Predicates);
        Assert.Equal("Customer Center", config.Predicates[0].Name);
        Assert.Equal("/Customer Center", config.Predicates[0].Path);
        Assert.Equal(1, config.Predicates[0].AreaId);
        Assert.Single(config.Predicates[0].Excludes);
        Assert.Equal("/Customer Center/Archive", config.Predicates[0].Excludes[0]);
    }

    [Fact]
    public void Load_NullExcludes_DefaultsToEmptyList()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "predicates": [
                {
                  "name": "Customer Center",
                  "path": "/Customer Center",
                  "areaId": 1
                }
              ]
            }
            """;
        var path = WriteConfigFile(json);

        var config = ConfigLoader.Load(path);

        Assert.NotNull(config.Predicates[0].Excludes);
        Assert.Empty(config.Predicates[0].Excludes);
    }

    [Fact]
    public void Load_NoLogLevel_DefaultsToInfo()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "predicates": [
                {
                  "name": "Customer Center",
                  "path": "/Customer Center",
                  "areaId": 1
                }
              ]
            }
            """;
        var path = WriteConfigFile(json);

        var config = ConfigLoader.Load(path);

        Assert.Equal("info", config.LogLevel);
    }

    // -------------------------------------------------------------------------
    // Missing file
    // -------------------------------------------------------------------------

    [Fact]
    public void Load_MissingFile_ThrowsFileNotFoundException_WithPath()
    {
        var nonExistentPath = Path.Combine(_tempDir, "nonexistent.json");

        var ex = Assert.Throws<FileNotFoundException>(() => ConfigLoader.Load(nonExistentPath));

        Assert.Contains(nonExistentPath, ex.Message);
    }

    // -------------------------------------------------------------------------
    // Missing required fields
    // -------------------------------------------------------------------------

    [Fact]
    public void Load_MissingOutputDirectory_ThrowsInvalidOperationException_WithFieldName()
    {
        var json = """
            {
              "predicates": [
                {
                  "name": "Customer Center",
                  "path": "/Customer Center",
                  "areaId": 1
                }
              ]
            }
            """;
        var path = WriteConfigFile(json);

        var ex = Assert.Throws<InvalidOperationException>(() => ConfigLoader.Load(path));

        Assert.Contains("outputDirectory", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_MissingPredicatesKey_ReturnsEmptyList()
    {
        var json = """
            {
              "outputDirectory": "/serialization"
            }
            """;
        var path = WriteConfigFile(json);

        var config = ConfigLoader.Load(path);

        Assert.NotNull(config.Predicates);
        Assert.Empty(config.Predicates);
    }

    [Fact]
    public void Load_EmptyPredicates_ReturnsEmptyList()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "predicates": []
            }
            """;
        var path = WriteConfigFile(json);

        var config = ConfigLoader.Load(path);

        Assert.NotNull(config.Predicates);
        Assert.Empty(config.Predicates);
    }

    [Fact]
    public void Load_PredicateMissingPath_ThrowsInvalidOperationException_WithFieldName()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "predicates": [
                {
                  "name": "Customer Center",
                  "areaId": 1
                }
              ]
            }
            """;
        var path = WriteConfigFile(json);

        var ex = Assert.Throws<InvalidOperationException>(() => ConfigLoader.Load(path));

        Assert.Contains("path", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_PredicateMissingAreaId_ThrowsInvalidOperationException_WithFieldName()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "predicates": [
                {
                  "name": "Customer Center",
                  "path": "/Customer Center"
                }
              ]
            }
            """;
        var path = WriteConfigFile(json);

        var ex = Assert.Throws<InvalidOperationException>(() => ConfigLoader.Load(path));

        Assert.Contains("areaId", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_PredicateMissingName_ThrowsInvalidOperationException_WithFieldName()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "predicates": [
                {
                  "path": "/Customer Center",
                  "areaId": 1
                }
              ]
            }
            """;
        var path = WriteConfigFile(json);

        var ex = Assert.Throws<InvalidOperationException>(() => ConfigLoader.Load(path));

        Assert.Contains("name", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // -------------------------------------------------------------------------
    // OutputDirectory existence validation
    // -------------------------------------------------------------------------

    [Fact]
    public void Load_NonExistentOutputDirectory_EmitsWarning()
    {
        var nonExistentDir = Path.Combine(_tempDir, "nonexistent_" + Guid.NewGuid().ToString("N")[..8]);
        var json = $$"""
            {
              "outputDirectory": "{{nonExistentDir.Replace("\\", "\\\\")}}",
              "predicates": [
                {
                  "name": "Test",
                  "path": "/Test",
                  "areaId": 1
                }
              ]
            }
            """;
        var path = WriteConfigFile(json);

        var originalError = Console.Error;
        var errorCapture = new StringWriter();
        Console.SetError(errorCapture);
        try
        {
            var config = ConfigLoader.Load(path);
            Assert.Equal(nonExistentDir, config.OutputDirectory);
        }
        finally
        {
            Console.SetError(originalError);
        }

        var errorOutput = errorCapture.ToString();
        Assert.Contains("does not exist", errorOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(nonExistentDir, errorOutput);
    }

    [Fact]
    public void Load_ExistingOutputDirectory_NoWarning()
    {
        var existingDir = Path.Combine(_tempDir, "existing_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(existingDir);
        var json = $$"""
            {
              "outputDirectory": "{{existingDir.Replace("\\", "\\\\")}}",
              "predicates": [
                {
                  "name": "Test",
                  "path": "/Test",
                  "areaId": 1
                }
              ]
            }
            """;
        var path = WriteConfigFile(json);

        var originalError = Console.Error;
        var errorCapture = new StringWriter();
        Console.SetError(errorCapture);
        try
        {
            var config = ConfigLoader.Load(path);
            Assert.Equal(existingDir, config.OutputDirectory);
        }
        finally
        {
            Console.SetError(originalError);
        }

        var errorOutput = errorCapture.ToString();
        Assert.DoesNotContain(existingDir, errorOutput);
    }

    // -------------------------------------------------------------------------
    // DryRun and ConflictStrategy fields
    // -------------------------------------------------------------------------

    [Fact]
    public void Load_ConfigWithoutNewFields_DefaultsToDryRunFalseAndSourceWins()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "logLevel": "info",
              "predicates": [
                {
                  "name": "Test",
                  "path": "/Test",
                  "areaId": 1
                }
              ]
            }
            """;
        var path = WriteConfigFile(json);

        var config = ConfigLoader.Load(path);

        Assert.False(config.DryRun);
        Assert.Equal(ConflictStrategy.SourceWins, config.ConflictStrategy);
    }

    [Fact]
    public void Load_ConfigWithDryRunTrue_ReturnsDryRunTrue()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "dryRun": true,
              "predicates": [
                {
                  "name": "Test",
                  "path": "/Test",
                  "areaId": 1
                }
              ]
            }
            """;
        var path = WriteConfigFile(json);

        var config = ConfigLoader.Load(path);

        Assert.True(config.DryRun);
    }

    [Fact]
    public void Load_ConfigWithConflictStrategy_ReturnsSourceWins()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "conflictStrategy": "source-wins",
              "predicates": [
                {
                  "name": "Test",
                  "path": "/Test",
                  "areaId": 1
                }
              ]
            }
            """;
        var path = WriteConfigFile(json);

        var config = ConfigLoader.Load(path);

        Assert.Equal(ConflictStrategy.SourceWins, config.ConflictStrategy);
    }

    // -------------------------------------------------------------------------
    // ProviderPredicateDefinition migration tests
    // -------------------------------------------------------------------------

    [Fact]
    public void Load_ContentPredicate_WithoutProviderType_DefaultsToContent()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "predicates": [
                {
                  "name": "Customer Center",
                  "path": "/Customer Center",
                  "areaId": 1
                }
              ]
            }
            """;
        var path = WriteConfigFile(json);

        var config = ConfigLoader.Load(path);

        Assert.Single(config.Predicates);
        Assert.IsType<ProviderPredicateDefinition>(config.Predicates[0]);
        Assert.Equal("Content", config.Predicates[0].ProviderType);
        Assert.Equal("/Customer Center", config.Predicates[0].Path);
        Assert.Equal(1, config.Predicates[0].AreaId);
    }

    [Fact]
    public void Load_SqlTablePredicate_ReturnsProviderPredicateDefinitionWithTableFields()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "predicates": [
                {
                  "name": "Order Flows",
                  "providerType": "SqlTable",
                  "table": "EcomOrderFlow",
                  "nameColumn": "OrderFlowName",
                  "compareColumns": "OrderFlowName,OrderFlowDescription"
                }
              ]
            }
            """;
        var path = WriteConfigFile(json);

        var config = ConfigLoader.Load(path);

        Assert.Single(config.Predicates);
        Assert.Equal("SqlTable", config.Predicates[0].ProviderType);
        Assert.Equal("EcomOrderFlow", config.Predicates[0].Table);
        Assert.Equal("OrderFlowName", config.Predicates[0].NameColumn);
        Assert.Equal("OrderFlowName,OrderFlowDescription", config.Predicates[0].CompareColumns);
    }

    [Fact]
    public void Load_MixedPredicates_ReturnsBothWithCorrectProviderTypes()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "predicates": [
                {
                  "name": "Customer Center",
                  "path": "/Customer Center",
                  "areaId": 1
                },
                {
                  "name": "Order Flows",
                  "providerType": "SqlTable",
                  "table": "EcomOrderFlow",
                  "nameColumn": "OrderFlowName"
                }
              ]
            }
            """;
        var path = WriteConfigFile(json);

        var config = ConfigLoader.Load(path);

        Assert.Equal(2, config.Predicates.Count);
        Assert.Equal("Content", config.Predicates[0].ProviderType);
        Assert.Equal("SqlTable", config.Predicates[1].ProviderType);
    }

    [Fact]
    public void Load_SqlTablePredicate_WithServiceCaches_DeserializesServiceCaches()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "predicates": [
                {
                  "name": "Order Flows",
                  "providerType": "SqlTable",
                  "table": "EcomOrderFlow",
                  "nameColumn": "OrderFlowName",
                  "serviceCaches": ["Dynamicweb.Ecommerce.Orders.PaymentService", "Dynamicweb.Ecommerce.Orders.ShippingService"]
                }
              ]
            }
            """;
        var path = WriteConfigFile(json);

        var config = ConfigLoader.Load(path);

        Assert.Equal(2, config.Predicates[0].ServiceCaches.Count);
        Assert.Equal("Dynamicweb.Ecommerce.Orders.PaymentService", config.Predicates[0].ServiceCaches[0]);
        Assert.Equal("Dynamicweb.Ecommerce.Orders.ShippingService", config.Predicates[0].ServiceCaches[1]);
    }

    [Fact]
    public void Load_SqlTablePredicate_WithoutServiceCaches_DefaultsToEmptyList()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "predicates": [
                {
                  "name": "Order Flows",
                  "providerType": "SqlTable",
                  "table": "EcomOrderFlow",
                  "nameColumn": "OrderFlowName"
                }
              ]
            }
            """;
        var path = WriteConfigFile(json);

        var config = ConfigLoader.Load(path);

        Assert.NotNull(config.Predicates[0].ServiceCaches);
        Assert.Empty(config.Predicates[0].ServiceCaches);
    }

    [Fact]
    public void Load_SqlTablePredicate_MissingPath_DoesNotThrow()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "predicates": [
                {
                  "name": "Order Flows",
                  "providerType": "SqlTable",
                  "table": "EcomOrderFlow",
                  "nameColumn": "OrderFlowName"
                }
              ]
            }
            """;
        var path = WriteConfigFile(json);

        // Should NOT throw — SqlTable predicates don't require path/areaId
        var config = ConfigLoader.Load(path);
        Assert.Single(config.Predicates);
    }

    // -------------------------------------------------------------------------
    // XmlColumns config mapping (Phase 27 — Pitfall P7 three-class mapping)
    // -------------------------------------------------------------------------

    [Fact]
    public void Load_SqlTablePredicate_WithXmlColumns_DeserializesXmlColumns()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "predicates": [
                {
                  "name": "Shipping Methods",
                  "providerType": "SqlTable",
                  "table": "EcomShippings",
                  "nameColumn": "ShippingName",
                  "xmlColumns": ["ShippingXml", "SettingsXml"]
                }
              ]
            }
            """;
        var path = WriteConfigFile(json);

        var config = ConfigLoader.Load(path);

        Assert.Equal(2, config.Predicates[0].XmlColumns.Count);
        Assert.Equal("ShippingXml", config.Predicates[0].XmlColumns[0]);
        Assert.Equal("SettingsXml", config.Predicates[0].XmlColumns[1]);
    }

    [Fact]
    public void Load_SqlTablePredicate_WithoutXmlColumns_DefaultsToEmptyList()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "predicates": [
                {
                  "name": "Order Flows",
                  "providerType": "SqlTable",
                  "table": "EcomOrderFlow",
                  "nameColumn": "OrderFlowName"
                }
              ]
            }
            """;
        var path = WriteConfigFile(json);

        var config = ConfigLoader.Load(path);

        Assert.NotNull(config.Predicates[0].XmlColumns);
        Assert.Empty(config.Predicates[0].XmlColumns);
    }

    // -------------------------------------------------------------------------
    // ExcludeFields / ExcludeXmlElements config mapping (Phase 28 — Pitfall P7)
    // -------------------------------------------------------------------------

    [Fact]
    public void Load_ContentPredicate_WithExcludeFields_DeserializesExcludeFields()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "predicates": [
                {
                  "name": "Customer Center",
                  "path": "/Customer Center",
                  "areaId": 1,
                  "excludeFields": ["NavigationTag", "AreaDomain"]
                }
              ]
            }
            """;
        var path = WriteConfigFile(json);

        var config = ConfigLoader.Load(path);

        Assert.Equal(2, config.Predicates[0].ExcludeFields.Count);
        Assert.Equal("NavigationTag", config.Predicates[0].ExcludeFields[0]);
        Assert.Equal("AreaDomain", config.Predicates[0].ExcludeFields[1]);
    }

    [Fact]
    public void Load_ContentPredicate_WithExcludeXmlElements_DeserializesExcludeXmlElements()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "predicates": [
                {
                  "name": "Customer Center",
                  "path": "/Customer Center",
                  "areaId": 1,
                  "excludeXmlElements": ["sort", "pagesize"]
                }
              ]
            }
            """;
        var path = WriteConfigFile(json);

        var config = ConfigLoader.Load(path);

        Assert.Equal(2, config.Predicates[0].ExcludeXmlElements.Count);
        Assert.Equal("sort", config.Predicates[0].ExcludeXmlElements[0]);
        Assert.Equal("pagesize", config.Predicates[0].ExcludeXmlElements[1]);
    }

    [Fact]
    public void Load_ContentPredicate_WithoutExcludeFields_DefaultsToEmptyLists()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "predicates": [
                {
                  "name": "Customer Center",
                  "path": "/Customer Center",
                  "areaId": 1
                }
              ]
            }
            """;
        var path = WriteConfigFile(json);

        var config = ConfigLoader.Load(path);

        Assert.NotNull(config.Predicates[0].ExcludeFields);
        Assert.Empty(config.Predicates[0].ExcludeFields);
        Assert.NotNull(config.Predicates[0].ExcludeXmlElements);
        Assert.Empty(config.Predicates[0].ExcludeXmlElements);
    }

    // -------------------------------------------------------------------------
    // Typed exclusion dictionaries (Phase 32 — CFG-01, CFG-02)
    // -------------------------------------------------------------------------

    [Fact]
    public void Load_ConfigWithExcludeFieldsByItemType_DeserializesDictionary()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "excludeFieldsByItemType": {
                "Swift_PageItemType": ["NavigationTag", "AreaDomain"],
                "Swift_ParagraphItemType": ["ModuleSettings"]
              },
              "predicates": []
            }
            """;
        var path = WriteConfigFile(json);

        var config = ConfigLoader.Load(path);

        // Phase 37-01.1: legacy flat ExcludeFieldsByItemType alias removed — ConfigLoader migrates
        // the top-level dict into Deploy.ExcludeFieldsByItemType, so assertions read through Deploy.
        Assert.Equal(2, config.Deploy.ExcludeFieldsByItemType.Count);
        Assert.Equal(new List<string> { "NavigationTag", "AreaDomain" }, config.Deploy.ExcludeFieldsByItemType["Swift_PageItemType"]);
        Assert.Equal(new List<string> { "ModuleSettings" }, config.Deploy.ExcludeFieldsByItemType["Swift_ParagraphItemType"]);
    }

    [Fact]
    public void Load_ConfigWithExcludeXmlElementsByType_DeserializesDictionary()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "excludeXmlElementsByType": {
                "Dynamicweb.Frontend.ContentPage": ["sort", "pagesize"]
              },
              "predicates": []
            }
            """;
        var path = WriteConfigFile(json);

        var config = ConfigLoader.Load(path);

        // Phase 37-01.1: assert via Deploy (see note above).
        Assert.Single(config.Deploy.ExcludeXmlElementsByType);
        Assert.Equal(new List<string> { "sort", "pagesize" }, config.Deploy.ExcludeXmlElementsByType["Dynamicweb.Frontend.ContentPage"]);
    }

    [Fact]
    public void Load_ConfigWithoutTypedDictionaries_DefaultsToEmptyDictionaries()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "predicates": []
            }
            """;
        var path = WriteConfigFile(json);

        var config = ConfigLoader.Load(path);

        // Phase 37-01.1: assert via Deploy (see note above).
        Assert.NotNull(config.Deploy.ExcludeFieldsByItemType);
        Assert.Empty(config.Deploy.ExcludeFieldsByItemType);
        Assert.NotNull(config.Deploy.ExcludeXmlElementsByType);
        Assert.Empty(config.Deploy.ExcludeXmlElementsByType);
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsTypedDictionaries()
    {
        // Phase 37-01.1: legacy flat ExcludeFieldsByItemType / ExcludeXmlElementsByType aliases
        // removed. Typed exclusions now live under Deploy (and optionally Seed) ModeConfigs.
        var config = new SerializerConfiguration
        {
            OutputDirectory = _tempDir,
            Deploy = new ModeConfig
            {
                OutputSubfolder = "deploy",
                ConflictStrategy = ConflictStrategy.SourceWins,
                Predicates = new List<ProviderPredicateDefinition>(),
                ExcludeFieldsByItemType = new Dictionary<string, List<string>>
                {
                    ["Swift_PageItemType"] = new List<string> { "NavigationTag" }
                },
                ExcludeXmlElementsByType = new Dictionary<string, List<string>>
                {
                    ["Dynamicweb.Frontend.ContentPage"] = new List<string> { "sort" }
                }
            }
        };
        var path = Path.Combine(_tempDir, "roundtrip.json");
        ConfigWriter.Save(config, path);

        var reloaded = ConfigLoader.Load(path);

        Assert.Single(reloaded.Deploy.ExcludeFieldsByItemType);
        Assert.Equal(new List<string> { "NavigationTag" }, reloaded.Deploy.ExcludeFieldsByItemType["Swift_PageItemType"]);
        Assert.Single(reloaded.Deploy.ExcludeXmlElementsByType);
        Assert.Equal(new List<string> { "sort" }, reloaded.Deploy.ExcludeXmlElementsByType["Dynamicweb.Frontend.ContentPage"]);
    }

    // -------------------------------------------------------------------------
    // Phase 37-03: Where + IncludeFields on SqlTable predicates
    // -------------------------------------------------------------------------

    [Fact]
    public void Load_WithWhereClause_And_IncludeFields_RoundTrips()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "deploy": {
                "outputSubfolder": "deploy",
                "conflictStrategy": "source-wins",
                "predicates": [
                  {
                    "name": "AccessUser-Roles",
                    "providerType": "SqlTable",
                    "table": "AccessUser",
                    "where": "AccessUserType = 2 AND AccessUserUserName IN ('Admin','Editors')",
                    "includeFields": ["AccessUserHostingId", "AccessUserHostingName"]
                  }
                ]
              }
            }
            """;
        var path = WriteConfigFile(json);

        var config = ConfigLoader.Load(path);

        Assert.Single(config.Deploy.Predicates);
        var pred = config.Deploy.Predicates[0];
        Assert.Equal("AccessUser-Roles", pred.Name);
        Assert.Equal("AccessUser", pred.Table);
        Assert.Equal("AccessUserType = 2 AND AccessUserUserName IN ('Admin','Editors')", pred.Where);
        Assert.Equal(2, pred.IncludeFields.Count);
        Assert.Equal("AccessUserHostingId", pred.IncludeFields[0]);
        Assert.Equal("AccessUserHostingName", pred.IncludeFields[1]);
    }

    [Fact]
    public void Load_WithFixtureValidator_BadTableIdentifier_Throws()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "deploy": {
                "outputSubfolder": "deploy",
                "conflictStrategy": "source-wins",
                "predicates": [
                  { "name": "Bad", "providerType": "SqlTable", "table": "NotARealTable" }
                ]
              }
            }
            """;
        var path = WriteConfigFile(json);

        var validator = new SqlIdentifierValidator(
            tableLoader: () => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "AccessUser" },
            columnLoader: _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        var ex = Assert.Throws<InvalidOperationException>(
            () => ConfigLoader.Load(path, validator));
        Assert.Contains("INFORMATION_SCHEMA", ex.Message);
        Assert.Contains("NotARealTable", ex.Message);
    }

    [Fact]
    public void Load_WithFixtureValidator_BadExcludeFieldIdentifier_Throws()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "deploy": {
                "outputSubfolder": "deploy",
                "conflictStrategy": "source-wins",
                "predicates": [
                  {
                    "name": "X", "providerType": "SqlTable", "table": "AccessUser",
                    "excludeFields": ["NonExistentColumn"]
                  }
                ]
              }
            }
            """;
        var path = WriteConfigFile(json);

        var validator = new SqlIdentifierValidator(
            tableLoader: () => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "AccessUser" },
            columnLoader: _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "AccessUserType" });

        var ex = Assert.Throws<InvalidOperationException>(
            () => ConfigLoader.Load(path, validator));
        Assert.Contains("NonExistentColumn", ex.Message);
    }

    [Fact]
    public void Load_WithFixtureValidator_BadWhereIdentifier_Throws()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "deploy": {
                "outputSubfolder": "deploy",
                "conflictStrategy": "source-wins",
                "predicates": [
                  {
                    "name": "X", "providerType": "SqlTable", "table": "AccessUser",
                    "where": "NonExistentColumn = 1"
                  }
                ]
              }
            }
            """;
        var path = WriteConfigFile(json);

        var validator = new SqlIdentifierValidator(
            tableLoader: () => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "AccessUser" },
            columnLoader: _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "AccessUserType" });

        var ex = Assert.Throws<InvalidOperationException>(
            () => ConfigLoader.Load(path, validator));
        Assert.Contains("NonExistentColumn", ex.Message);
    }

    [Fact]
    public void Load_WithFixtureValidator_AggregatesMultipleErrors()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "deploy": {
                "outputSubfolder": "deploy",
                "conflictStrategy": "source-wins",
                "predicates": [
                  { "name": "A", "providerType": "SqlTable", "table": "UnknownTableA" },
                  { "name": "B", "providerType": "SqlTable", "table": "UnknownTableB" },
                  { "name": "C", "providerType": "SqlTable", "table": "UnknownTableC" }
                ]
              }
            }
            """;
        var path = WriteConfigFile(json);

        var validator = new SqlIdentifierValidator(
            tableLoader: () => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "AccessUser" },
            columnLoader: _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        var ex = Assert.Throws<InvalidOperationException>(
            () => ConfigLoader.Load(path, validator));
        Assert.Contains("UnknownTableA", ex.Message);
        Assert.Contains("UnknownTableB", ex.Message);
        Assert.Contains("UnknownTableC", ex.Message);
    }

    [Fact]
    public void Load_WithFixtureValidator_ValidSqlTableConfig_LoadsSuccessfully()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "deploy": {
                "outputSubfolder": "deploy",
                "conflictStrategy": "source-wins",
                "predicates": [
                  {
                    "name": "AU", "providerType": "SqlTable", "table": "AccessUser",
                    "where": "AccessUserType = 2 AND AccessUserUserName IN ('Admin','Editors')",
                    "excludeFields": ["AccessUserPassword"],
                    "includeFields": ["AccessUserHostingName"]
                  }
                ]
              }
            }
            """;
        var path = WriteConfigFile(json);

        var validator = new SqlIdentifierValidator(
            tableLoader: () => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "AccessUser" },
            columnLoader: _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "AccessUserType", "AccessUserUserName", "AccessUserPassword", "AccessUserHostingName"
            });

        var config = ConfigLoader.Load(path, validator);

        Assert.Single(config.Deploy.Predicates);
    }

    [Fact]
    public void Load_WithoutWhereOrIncludeFields_DefaultsNullAndEmpty()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "deploy": {
                "outputSubfolder": "deploy",
                "conflictStrategy": "source-wins",
                "predicates": [
                  {
                    "name": "Plain",
                    "providerType": "SqlTable",
                    "table": "AccessUser"
                  }
                ]
              }
            }
            """;
        var path = WriteConfigFile(json);

        var config = ConfigLoader.Load(path);

        var pred = config.Deploy.Predicates[0];
        Assert.Null(pred.Where);
        Assert.NotNull(pred.IncludeFields);
        Assert.Empty(pred.IncludeFields);
    }

    // -------------------------------------------------------------------------
    // Phase 37-04 CACHE-01: ServiceCaches validation against DwCacheServiceRegistry
    // -------------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Phase37-04")]
    public void Load_UnknownServiceCache_ThrowsWithSupportedList()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "deploy": {
                "predicates": [
                  {
                    "name": "Payments",
                    "providerType": "SqlTable",
                    "table": "EcomPayments",
                    "serviceCaches": ["Dynamicweb.Ecommerce.Orders.NotARealService"]
                  }
                ]
              }
            }
            """;
        var path = WriteConfigFile(json);

        var ex = Assert.Throws<InvalidOperationException>(() => ConfigLoader.Load(path));
        Assert.Contains("ServiceCaches validation failed", ex.Message);
        Assert.Contains("Dynamicweb.Ecommerce.Orders.NotARealService", ex.Message);
        Assert.Contains("'Payments'", ex.Message);
        Assert.Contains("Supported", ex.Message);
    }

    [Fact]
    [Trait("Category", "Phase37-04")]
    public void Load_KnownServiceCaches_ShortAndFullNames_Loads()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "deploy": {
                "predicates": [
                  {
                    "name": "Payments",
                    "providerType": "SqlTable",
                    "table": "EcomPayments",
                    "serviceCaches": [
                      "PaymentService",
                      "Dynamicweb.Ecommerce.Orders.ShippingService"
                    ]
                  }
                ]
              }
            }
            """;
        var path = WriteConfigFile(json);

        var config = ConfigLoader.Load(path);

        Assert.Equal(2, config.Deploy.Predicates[0].ServiceCaches.Count);
    }

    [Fact]
    [Trait("Category", "Phase37-04")]
    public void Load_UnknownServiceCacheInBothDeployAndSeed_AggregatesErrors()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "deploy": {
                "predicates": [
                  {
                    "name": "BadDeploy",
                    "providerType": "SqlTable",
                    "table": "EcomPayments",
                    "serviceCaches": ["Nonexistent.DeployCache"]
                  }
                ]
              },
              "seed": {
                "predicates": [
                  {
                    "name": "BadSeed",
                    "providerType": "SqlTable",
                    "table": "EcomShippings",
                    "serviceCaches": ["Nonexistent.SeedCache"]
                  }
                ]
              }
            }
            """;
        var path = WriteConfigFile(json);

        var ex = Assert.Throws<InvalidOperationException>(() => ConfigLoader.Load(path));

        // Both predicate scopes reported in the same exception.
        Assert.Contains("deploy.predicates 'BadDeploy'", ex.Message);
        Assert.Contains("seed.predicates 'BadSeed'", ex.Message);
        Assert.Contains("Nonexistent.DeployCache", ex.Message);
        Assert.Contains("Nonexistent.SeedCache", ex.Message);
    }

    [Fact]
    [Trait("Category", "Phase37-04")]
    public void Load_EmptyServiceCaches_Passes()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "deploy": {
                "predicates": [
                  {
                    "name": "Plain",
                    "providerType": "SqlTable",
                    "table": "EcomOrderFlow",
                    "serviceCaches": []
                  }
                ]
              }
            }
            """;
        var path = WriteConfigFile(json);

        var config = ConfigLoader.Load(path); // no throw
        Assert.Empty(config.Deploy.Predicates[0].ServiceCaches);
    }
}
