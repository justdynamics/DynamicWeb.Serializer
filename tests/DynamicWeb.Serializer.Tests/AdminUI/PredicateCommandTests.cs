using System.Reflection;
using DynamicWeb.Serializer.AdminUI.Commands;
using DynamicWeb.Serializer.AdminUI.Models;
using DynamicWeb.Serializer.AdminUI.Queries;
using DynamicWeb.Serializer.AdminUI.Screens;
using DynamicWeb.Serializer.Configuration;
using DynamicWeb.Serializer.Models;
using DynamicWeb.Serializer.Tests.TestHelpers;
using Dynamicweb.CoreUI.Data;
using Dynamicweb.CoreUI.Editors;
using Dynamicweb.CoreUI.Editors.Lists;
using Xunit;

namespace DynamicWeb.Serializer.Tests.AdminUI;

public class PredicateCommandTests : ConfigLoaderValidatorFixtureBase
{
    private readonly string _tempDir;
    private readonly string _configPath;

    public PredicateCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "PredCmdTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _configPath = Path.Combine(_tempDir, "Serializer.config.json");
    }

    public override void Dispose()
    {
        base.Dispose();  // clear AsyncLocal first
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private void CreateSeedConfig(List<ProviderPredicateDefinition>? predicates = null)
    {
        // Phase 40 D-01: flat predicate list with explicit per-predicate Mode.
        var config = new SerializerConfiguration
        {
            OutputDirectory = @"\System\Serializer",
            LogLevel = "info",
            DryRun = false,
            Predicates = predicates ?? new List<ProviderPredicateDefinition>
            {
                new() { Name = "Default", Mode = DeploymentMode.Deploy, ProviderType = "Content", Path = "/", AreaId = 1, PageId = 10 }
            }
        };
        ConfigWriter.Save(config, _configPath);
    }

    // -------------------------------------------------------------------------
    // SavePredicateCommand tests
    // -------------------------------------------------------------------------

    [Fact]
    public void Save_NullModel_ReturnsInvalid()
    {
        var cmd = new SavePredicateCommand { Model = null };

        var result = cmd.Handle();

        Assert.Equal(CommandResult.ResultType.Invalid, result.Status);
        Assert.Contains("Model data must be given", result.Message);
    }

    [Fact]
    public void Save_EmptyName_ReturnsInvalid()
    {
        var cmd = new SavePredicateCommand
        {
            Model = new PredicateEditModel
            {
                Name = "",
                ProviderType = "Content",
                AreaId = 1,
                PageId = 10
            }
        };

        var result = cmd.Handle();

        Assert.Equal(CommandResult.ResultType.Invalid, result.Status);
        Assert.Contains("Name", result.Message);
    }

    [Fact]
    public void Save_DuplicateName_ReturnsInvalid()
    {
        CreateSeedConfig(new List<ProviderPredicateDefinition>
        {
            new() { Name = "Existing", Mode = DeploymentMode.Deploy, ProviderType = "Content", Path = "/existing", AreaId = 1, PageId = 10 }
        });

        var cmd = new SavePredicateCommand
        {
            ConfigPath = _configPath,
            Model = new PredicateEditModel
            {
                Index = -1,
                Name = "Existing",
                ProviderType = "Content",
                AreaId = 1,
                PageId = 10
            }
        };

        var result = cmd.Handle();

        Assert.Equal(CommandResult.ResultType.Invalid, result.Status);
        Assert.Contains("duplicate", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Save_IndexOutOfRange_ReturnsError()
    {
        CreateSeedConfig(new List<ProviderPredicateDefinition>
        {
            new() { Name = "Only", Mode = DeploymentMode.Deploy, ProviderType = "Content", Path = "/only", AreaId = 1, PageId = 10 }
        });

        var cmd = new SavePredicateCommand
        {
            ConfigPath = _configPath,
            Model = new PredicateEditModel
            {
                Index = 5,
                Name = "Updated",
                ProviderType = "Content",
                AreaId = 1,
                PageId = 10
            }
        };

        var result = cmd.Handle();

        Assert.Equal(CommandResult.ResultType.Error, result.Status);
    }

    [Fact]
    public void Save_NewPredicate_AppendsToConfig()
    {
        CreateSeedConfig(new List<ProviderPredicateDefinition>
        {
            new() { Name = "Existing", Mode = DeploymentMode.Deploy, ProviderType = "Content", Path = "/existing", AreaId = 1, PageId = 10 }
        });

        var cmd = new SavePredicateCommand
        {
            ConfigPath = _configPath,
            Model = new PredicateEditModel
            {
                Index = -1,
                Name = "New Predicate",
                ProviderType = "Content",
                AreaId = 2,
                PageId = 20,
                Excludes = "path1\r\npath2\n\npath3"
            }
        };

        var result = cmd.Handle();

        Assert.Equal(CommandResult.ResultType.Ok, result.Status);
        var config = ConfigLoader.Load(_configPath);
        Assert.Equal(2, config.Predicates.Count);
        Assert.Equal("New Predicate", config.Predicates[1].Name);
        Assert.Equal("Content", config.Predicates[1].ProviderType);
        Assert.Equal(3, config.Predicates[1].Excludes.Count);
        Assert.Equal("path1", config.Predicates[1].Excludes[0]);
        Assert.Equal("path2", config.Predicates[1].Excludes[1]);
        Assert.Equal("path3", config.Predicates[1].Excludes[2]);
    }

    [Fact]
    public void Save_UpdateExisting_ReplacesAtIndex()
    {
        CreateSeedConfig(new List<ProviderPredicateDefinition>
        {
            new() { Name = "First", Mode = DeploymentMode.Deploy, ProviderType = "Content", Path = "/first", AreaId = 1, PageId = 10 },
            new() { Name = "Second", Mode = DeploymentMode.Deploy, ProviderType = "Content", Path = "/second", AreaId = 2, PageId = 20 }
        });

        var cmd = new SavePredicateCommand
        {
            ConfigPath = _configPath,
            Model = new PredicateEditModel
            {
                Index = 0,
                Name = "Updated First",
                ProviderType = "Content",
                AreaId = 3,
                PageId = 30,
                Excludes = ""
            }
        };

        var result = cmd.Handle();

        Assert.Equal(CommandResult.ResultType.Ok, result.Status);
        var config = ConfigLoader.Load(_configPath);
        Assert.Equal(2, config.Predicates.Count);
        Assert.Equal("Updated First", config.Predicates[0].Name);
        Assert.Equal(3, config.Predicates[0].AreaId);
        Assert.Equal(30, config.Predicates[0].PageId);
    }

    // -------------------------------------------------------------------------
    // Multi-provider SavePredicateCommand tests
    // -------------------------------------------------------------------------

    [Fact]
    public void Save_SqlTable_NewPredicate_PersistsAllFields()
    {
        CreateSeedConfig(new List<ProviderPredicateDefinition>());

        var cmd = new SavePredicateCommand
        {
            ConfigPath = _configPath,
            Model = new PredicateEditModel
            {
                Index = -1,
                Name = "Order Flows",
                ProviderType = "SqlTable",
                Table = "EcomOrderFlow",
                NameColumn = "OrderFlowName",
                CompareColumns = "",
                ServiceCaches = "Dynamicweb.Ecommerce.Orders.PaymentService\nDynamicweb.Ecommerce.Orders.ShippingService"
            }
        };

        var result = cmd.Handle();

        Assert.Equal(CommandResult.ResultType.Ok, result.Status);
        var config = ConfigLoader.Load(_configPath);
        Assert.Single(config.Predicates);
        var pred = config.Predicates[0];
        Assert.Equal("SqlTable", pred.ProviderType);
        Assert.Equal("EcomOrderFlow", pred.Table);
        Assert.Equal("OrderFlowName", pred.NameColumn);
        Assert.Equal(2, pred.ServiceCaches.Count);
        Assert.Equal("Dynamicweb.Ecommerce.Orders.PaymentService", pred.ServiceCaches[0]);
        Assert.Equal("Dynamicweb.Ecommerce.Orders.ShippingService", pred.ServiceCaches[1]);
    }

    [Fact]
    public void Save_SqlTable_MissingTable_ReturnsInvalid()
    {
        CreateSeedConfig(new List<ProviderPredicateDefinition>());

        var cmd = new SavePredicateCommand
        {
            ConfigPath = _configPath,
            Model = new PredicateEditModel
            {
                Index = -1,
                Name = "Missing Table",
                ProviderType = "SqlTable",
                Table = ""
            }
        };

        var result = cmd.Handle();

        Assert.Equal(CommandResult.ResultType.Invalid, result.Status);
        Assert.Contains("Table", result.Message);
    }

    [Fact]
    public void Save_Content_MissingArea_ReturnsInvalid()
    {
        CreateSeedConfig(new List<ProviderPredicateDefinition>());

        var cmd = new SavePredicateCommand
        {
            ConfigPath = _configPath,
            Model = new PredicateEditModel
            {
                Index = -1,
                Name = "Missing Area",
                ProviderType = "Content",
                AreaId = 0,
                PageId = 10
            }
        };

        var result = cmd.Handle();

        Assert.Equal(CommandResult.ResultType.Invalid, result.Status);
        Assert.Contains("Area", result.Message);
    }

    [Fact]
    public void Save_Content_NewPredicate_PersistsContentFields()
    {
        CreateSeedConfig(new List<ProviderPredicateDefinition>());

        var cmd = new SavePredicateCommand
        {
            ConfigPath = _configPath,
            Model = new PredicateEditModel
            {
                Index = -1,
                Name = "Content Pred",
                ProviderType = "Content",
                AreaId = 1,
                PageId = 10,
                Excludes = "/excluded"
            }
        };

        var result = cmd.Handle();

        Assert.Equal(CommandResult.ResultType.Ok, result.Status);
        var config = ConfigLoader.Load(_configPath);
        Assert.Single(config.Predicates);
        var pred = config.Predicates[0];
        Assert.Equal("Content", pred.ProviderType);
        Assert.Equal(1, pred.AreaId);
        Assert.Equal(10, pred.PageId);
        Assert.Single(pred.Excludes);
        Assert.Null(pred.Table);
        Assert.Null(pred.NameColumn);
        Assert.Null(pred.CompareColumns);
    }

    [Fact]
    public void Save_EmptyProviderType_ReturnsInvalid()
    {
        CreateSeedConfig(new List<ProviderPredicateDefinition>());

        var cmd = new SavePredicateCommand
        {
            ConfigPath = _configPath,
            Model = new PredicateEditModel
            {
                Index = -1,
                Name = "No Provider",
                ProviderType = ""
            }
        };

        var result = cmd.Handle();

        Assert.Equal(CommandResult.ResultType.Invalid, result.Status);
        Assert.Contains("Provider Type", result.Message);
    }

    [Fact]
    public void Save_UnknownProviderType_ReturnsInvalid()
    {
        CreateSeedConfig(new List<ProviderPredicateDefinition>());

        var cmd = new SavePredicateCommand
        {
            ConfigPath = _configPath,
            Model = new PredicateEditModel
            {
                Index = -1,
                Name = "Bad Provider",
                ProviderType = "Unknown"
            }
        };

        var result = cmd.Handle();

        Assert.Equal(CommandResult.ResultType.Invalid, result.Status);
        Assert.Contains("Unknown provider type", result.Message);
    }

    [Fact]
    public void Save_SqlTable_UpdateExisting_PreservesProviderType()
    {
        CreateSeedConfig(new List<ProviderPredicateDefinition>
        {
            new() { Name = "Order Flows", Mode = DeploymentMode.Deploy, ProviderType = "SqlTable", Table = "EcomOrderFlow", NameColumn = "OrderFlowName" }
        });

        // Attempt to tamper ProviderType on update — D-02 should preserve "SqlTable"
        var cmd = new SavePredicateCommand
        {
            ConfigPath = _configPath,
            Model = new PredicateEditModel
            {
                Index = 0,
                Name = "Order Flows Updated",
                ProviderType = "Content", // tampered — should be ignored
                Table = "EcomOrderFlowV2",
                NameColumn = "OrderFlowName"
            }
        };

        var result = cmd.Handle();

        Assert.Equal(CommandResult.ResultType.Ok, result.Status);
        var config = ConfigLoader.Load(_configPath);
        Assert.Single(config.Predicates);
        var pred = config.Predicates[0];
        Assert.Equal("SqlTable", pred.ProviderType); // D-02: locked to original
        Assert.Equal("Order Flows Updated", pred.Name);
        Assert.Equal("EcomOrderFlowV2", pred.Table);
    }

    // -------------------------------------------------------------------------
    // Filtering fields round-trip tests
    // -------------------------------------------------------------------------

    [Fact]
    public void Save_Content_NewPredicate_PersistsExcludeFields()
    {
        CreateSeedConfig(new List<ProviderPredicateDefinition>());

        var cmd = new SavePredicateCommand
        {
            ConfigPath = _configPath,
            Model = new PredicateEditModel
            {
                Index = -1,
                Name = "Content Filtering",
                ProviderType = "Content",
                AreaId = 1,
                PageId = 10,
                ExcludeFields = "PageNavigationTag\r\nAreaDomain",
                ExcludeXmlElements = "sort\npagesize"
            }
        };

        var result = cmd.Handle();

        Assert.Equal(CommandResult.ResultType.Ok, result.Status);
        var config = ConfigLoader.Load(_configPath);
        var pred = config.Predicates[0];
        Assert.Equal(2, pred.ExcludeFields.Count);
        Assert.Equal("PageNavigationTag", pred.ExcludeFields[0]);
        Assert.Equal("AreaDomain", pred.ExcludeFields[1]);
        Assert.Equal(2, pred.ExcludeXmlElements.Count);
        Assert.Equal("sort", pred.ExcludeXmlElements[0]);
        Assert.Equal("pagesize", pred.ExcludeXmlElements[1]);
        Assert.Empty(pred.XmlColumns);
    }

    [Fact]
    public void Save_SqlTable_NewPredicate_PersistsFilteringFields()
    {
        CreateSeedConfig(new List<ProviderPredicateDefinition>());

        var cmd = new SavePredicateCommand
        {
            ConfigPath = _configPath,
            Model = new PredicateEditModel
            {
                Index = -1,
                Name = "SqlTable Filtering",
                ProviderType = "SqlTable",
                Table = "EcomOrderFlow",
                ExcludeFields = "LastModified",
                XmlColumns = "ShippingXml\nSettingsXml",
                ExcludeXmlElements = "cache"
            }
        };

        var result = cmd.Handle();

        Assert.Equal(CommandResult.ResultType.Ok, result.Status);
        var config = ConfigLoader.Load(_configPath);
        var pred = config.Predicates[0];
        Assert.Single(pred.ExcludeFields);
        Assert.Equal("LastModified", pred.ExcludeFields[0]);
        Assert.Equal(2, pred.XmlColumns.Count);
        Assert.Equal("ShippingXml", pred.XmlColumns[0]);
        Assert.Equal("SettingsXml", pred.XmlColumns[1]);
        Assert.Single(pred.ExcludeXmlElements);
        Assert.Equal("cache", pred.ExcludeXmlElements[0]);
    }

    [Fact]
    public void Save_Content_XmlColumnsNotPersisted()
    {
        CreateSeedConfig(new List<ProviderPredicateDefinition>());

        var cmd = new SavePredicateCommand
        {
            ConfigPath = _configPath,
            Model = new PredicateEditModel
            {
                Index = -1,
                Name = "Content No XmlColumns",
                ProviderType = "Content",
                AreaId = 1,
                PageId = 10,
                XmlColumns = "SomeColumn"
            }
        };

        var result = cmd.Handle();

        Assert.Equal(CommandResult.ResultType.Ok, result.Status);
        var config = ConfigLoader.Load(_configPath);
        var pred = config.Predicates[0];
        Assert.Empty(pred.XmlColumns);
    }

    // -------------------------------------------------------------------------
    // CheckboxList-style value round-trip tests (33-01)
    // -------------------------------------------------------------------------

    [Fact]
    public void Save_SqlTable_ExcludeFields_RoundTrips()
    {
        CreateSeedConfig(new List<ProviderPredicateDefinition>());

        var cmd = new SavePredicateCommand
        {
            ConfigPath = _configPath,
            Model = new PredicateEditModel
            {
                Index = -1,
                Name = "ExcludeFields RT",
                ProviderType = "SqlTable",
                Table = "EcomOrderFlow",
                ExcludeFields = "OrderFlowID\nOrderFlowName\nOrderFlowOrderStateID"
            }
        };

        var result = cmd.Handle();

        Assert.Equal(CommandResult.ResultType.Ok, result.Status);
        var config = ConfigLoader.Load(_configPath);
        var pred = config.Predicates[0];
        Assert.Equal(3, pred.ExcludeFields.Count);
        Assert.Equal("OrderFlowID", pred.ExcludeFields[0]);
        Assert.Equal("OrderFlowName", pred.ExcludeFields[1]);
        Assert.Equal("OrderFlowOrderStateID", pred.ExcludeFields[2]);
    }

    [Fact]
    public void Save_SqlTable_XmlColumns_RoundTrips()
    {
        CreateSeedConfig(new List<ProviderPredicateDefinition>());

        var cmd = new SavePredicateCommand
        {
            ConfigPath = _configPath,
            Model = new PredicateEditModel
            {
                Index = -1,
                Name = "XmlColumns RT",
                ProviderType = "SqlTable",
                Table = "EcomOrderFlow",
                XmlColumns = "SettingsXml\nConfigXml"
            }
        };

        var result = cmd.Handle();

        Assert.Equal(CommandResult.ResultType.Ok, result.Status);
        var config = ConfigLoader.Load(_configPath);
        var pred = config.Predicates[0];
        Assert.Equal(2, pred.XmlColumns.Count);
        Assert.Equal("SettingsXml", pred.XmlColumns[0]);
        Assert.Equal("ConfigXml", pred.XmlColumns[1]);
    }

    [Fact]
    public void Save_SqlTable_EmptyFilteringFields_PersistsAsEmptyLists()
    {
        CreateSeedConfig(new List<ProviderPredicateDefinition>());

        var cmd = new SavePredicateCommand
        {
            ConfigPath = _configPath,
            Model = new PredicateEditModel
            {
                Index = -1,
                Name = "Empty Filtering",
                ProviderType = "SqlTable",
                Table = "EcomOrderFlow",
                ExcludeFields = "",
                XmlColumns = ""
            }
        };

        var result = cmd.Handle();

        Assert.Equal(CommandResult.ResultType.Ok, result.Status);
        var config = ConfigLoader.Load(_configPath);
        var pred = config.Predicates[0];
        Assert.Empty(pred.ExcludeFields);
        Assert.Empty(pred.XmlColumns);
    }

    [Fact]
    public void Save_SqlTable_UpdateExisting_PreservesFilteringFields()
    {
        CreateSeedConfig(new List<ProviderPredicateDefinition>
        {
            new()
            {
                Name = "Updatable",
                Mode = DeploymentMode.Deploy,
                ProviderType = "SqlTable",
                Table = "EcomOrderFlow",
                ExcludeFields = new List<string> { "Col1" }
            }
        });

        var cmd = new SavePredicateCommand
        {
            ConfigPath = _configPath,
            Model = new PredicateEditModel
            {
                Index = 0,
                Name = "Updatable",
                ProviderType = "SqlTable",
                Table = "EcomOrderFlow",
                ExcludeFields = "Col1\nCol2\nCol3"
            }
        };

        var result = cmd.Handle();

        Assert.Equal(CommandResult.ResultType.Ok, result.Status);
        var config = ConfigLoader.Load(_configPath);
        var pred = config.Predicates[0];
        Assert.Equal(3, pred.ExcludeFields.Count);
        Assert.Contains("Col1", pred.ExcludeFields);
        Assert.Contains("Col2", pred.ExcludeFields);
        Assert.Contains("Col3", pred.ExcludeFields);
    }

    [Fact]
    public void Save_Content_ExcludeFields_StillWorksWithNewlines()
    {
        CreateSeedConfig(new List<ProviderPredicateDefinition>());

        var cmd = new SavePredicateCommand
        {
            ConfigPath = _configPath,
            Model = new PredicateEditModel
            {
                Index = -1,
                Name = "Content EF",
                ProviderType = "Content",
                AreaId = 1,
                PageId = 10,
                ExcludeFields = "Field1\r\nField2"
            }
        };

        var result = cmd.Handle();

        Assert.Equal(CommandResult.ResultType.Ok, result.Status);
        var config = ConfigLoader.Load(_configPath);
        var pred = config.Predicates[0];
        Assert.Equal(2, pred.ExcludeFields.Count);
        Assert.Equal("Field1", pred.ExcludeFields[0]);
        Assert.Equal("Field2", pred.ExcludeFields[1]);
    }

    // -------------------------------------------------------------------------
    // DeletePredicateCommand tests
    // -------------------------------------------------------------------------

    [Fact]
    public void Delete_ValidIndex_RemovesPredicate()
    {
        CreateSeedConfig(new List<ProviderPredicateDefinition>
        {
            new() { Name = "First", Mode = DeploymentMode.Deploy, ProviderType = "Content", Path = "/first", AreaId = 1, PageId = 10 },
            new() { Name = "Second", Mode = DeploymentMode.Deploy, ProviderType = "Content", Path = "/second", AreaId = 2, PageId = 20 }
        });

        var cmd = new DeletePredicateCommand
        {
            ConfigPath = _configPath,
            Index = 0
        };

        var result = cmd.Handle();

        Assert.Equal(CommandResult.ResultType.Ok, result.Status);
        var config = ConfigLoader.Load(_configPath);
        Assert.Single(config.Predicates);
        Assert.Equal("Second", config.Predicates[0].Name);
    }

    [Fact]
    public void Delete_NegativeIndex_ReturnsError()
    {
        CreateSeedConfig();

        var cmd = new DeletePredicateCommand
        {
            ConfigPath = _configPath,
            Index = -1
        };

        var result = cmd.Handle();

        Assert.Equal(CommandResult.ResultType.Error, result.Status);
        Assert.Contains("Invalid", result.Message);
    }

    [Fact]
    public void Delete_IndexOutOfRange_ReturnsError()
    {
        CreateSeedConfig();

        var cmd = new DeletePredicateCommand
        {
            ConfigPath = _configPath,
            Index = 99
        };

        var result = cmd.Handle();

        Assert.Equal(CommandResult.ResultType.Error, result.Status);
    }

    [Fact]
    public void Delete_LastPredicate_ResultsInEmptyList()
    {
        CreateSeedConfig(new List<ProviderPredicateDefinition>
        {
            new() { Name = "Only", Mode = DeploymentMode.Deploy, ProviderType = "Content", Path = "/only", AreaId = 1, PageId = 10 }
        });

        var cmd = new DeletePredicateCommand
        {
            ConfigPath = _configPath,
            Index = 0
        };

        var result = cmd.Handle();

        Assert.Equal(CommandResult.ResultType.Ok, result.Status);
        var config = ConfigLoader.Load(_configPath);
        Assert.Empty(config.Predicates);
    }

    // -------------------------------------------------------------------------
    // Phase 40 D-01: SavePredicateCommand persists per-predicate Mode in flat list
    // -------------------------------------------------------------------------

    [Fact]
    public void Save_PredicateInDeployMode_AppendsToFlatListWithDeployMode()
    {
        CreateSeedConfig(new List<ProviderPredicateDefinition>());

        var cmd = new SavePredicateCommand
        {
            ConfigPath = _configPath,
            Model = new PredicateEditModel
            {
                Index = -1,
                Mode = nameof(DeploymentMode.Deploy),  // Phase 41 D-13: string-typed model
                Name = "Deploy1",
                ProviderType = "Content",
                AreaId = 1,
                PageId = 10
            }
        };

        var result = cmd.Handle();

        Assert.Equal(CommandResult.ResultType.Ok, result.Status);
        var config = ConfigLoader.Load(_configPath);
        Assert.Single(config.Predicates);
        Assert.Equal("Deploy1", config.Predicates[0].Name);
        Assert.Equal(DeploymentMode.Deploy, config.Predicates[0].Mode);
    }

    [Fact]
    public void Save_PredicateInSeedMode_AppendsToFlatListWithSeedMode()
    {
        // Seed a config with one existing Deploy predicate so we can prove Seed.Save doesn't overwrite Deploy.
        CreateSeedConfig(new List<ProviderPredicateDefinition>
        {
            new() { Name = "DeployExisting", Mode = DeploymentMode.Deploy, ProviderType = "Content", Path = "/d", AreaId = 1, PageId = 10 }
        });

        var cmd = new SavePredicateCommand
        {
            ConfigPath = _configPath,
            Model = new PredicateEditModel
            {
                Index = -1,
                Mode = nameof(DeploymentMode.Seed),  // Phase 41 D-13: string-typed model
                Name = "Seed1",
                ProviderType = "Content",
                AreaId = 1,
                PageId = 10
            }
        };

        var result = cmd.Handle();

        Assert.Equal(CommandResult.ResultType.Ok, result.Status);
        var config = ConfigLoader.Load(_configPath);
        Assert.Equal(2, config.Predicates.Count);
        var deploy = config.Predicates.Where(p => p.Mode == DeploymentMode.Deploy).ToList();
        var seed = config.Predicates.Where(p => p.Mode == DeploymentMode.Seed).ToList();
        Assert.Single(deploy);
        Assert.Equal("DeployExisting", deploy[0].Name);
        Assert.Single(seed);
        Assert.Equal("Seed1", seed[0].Name);
    }

    // -------------------------------------------------------------------------
    // Phase 37-03: WhereClause + IncludeFields round-trip + validation
    // -------------------------------------------------------------------------

    [Fact]
    public void Save_SqlTable_WhereClauseAndIncludeFields_RoundTrip()
    {
        CreateSeedConfig(new List<ProviderPredicateDefinition>());

        // Validator must be bypassed for this unit test (no live DB). SavePredicateCommand
        // exposes IdentifierValidator / WhereValidator hooks that default to production
        // validators; tests inject fixture validators that accept anything.
        var cmd = new SavePredicateCommand
        {
            ConfigPath = _configPath,
            IdentifierValidator = new SqlIdentifierValidator(
                tableLoader: () => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "AccessUser" },
                columnLoader: _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "AccessUserType", "AccessUserUserName", "AccessUserHostingName"
                }),
            WhereValidator = new SqlWhereClauseValidator(),
            Model = new PredicateEditModel
            {
                Index = -1,
                Name = "AU-Roles",
                ProviderType = "SqlTable",
                Table = "AccessUser",
                WhereClause = "AccessUserType = 2 AND AccessUserUserName IN ('Admin','Editors')",
                IncludeFields = "AccessUserHostingName"
            }
        };

        var result = cmd.Handle();

        Assert.Equal(CommandResult.ResultType.Ok, result.Status);
        var config = ConfigLoader.Load(_configPath);
        var pred = config.Predicates[0];
        Assert.Equal("AccessUserType = 2 AND AccessUserUserName IN ('Admin','Editors')", pred.Where);
        Assert.Single(pred.IncludeFields);
        Assert.Equal("AccessUserHostingName", pred.IncludeFields[0]);
    }

    [Fact]
    public void Save_InvalidWhereClause_ReturnsInvalid()
    {
        CreateSeedConfig(new List<ProviderPredicateDefinition>());

        var cmd = new SavePredicateCommand
        {
            ConfigPath = _configPath,
            IdentifierValidator = new SqlIdentifierValidator(
                tableLoader: () => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "AccessUser" },
                columnLoader: _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "AccessUserType" }),
            WhereValidator = new SqlWhereClauseValidator(),
            Model = new PredicateEditModel
            {
                Index = -1,
                Name = "BadWhere",
                ProviderType = "SqlTable",
                Table = "AccessUser",
                WhereClause = "AccessUserType = 2; DROP TABLE X"
            }
        };

        var result = cmd.Handle();

        Assert.Equal(CommandResult.ResultType.Invalid, result.Status);
        Assert.Contains(";", result.Message);
    }

    [Fact]
    public void Save_InvalidTableIdentifier_ReturnsInvalid()
    {
        CreateSeedConfig(new List<ProviderPredicateDefinition>());

        var cmd = new SavePredicateCommand
        {
            ConfigPath = _configPath,
            IdentifierValidator = new SqlIdentifierValidator(
                tableLoader: () => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "AccessUser" },
                columnLoader: _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "AccessUserType" }),
            WhereValidator = new SqlWhereClauseValidator(),
            Model = new PredicateEditModel
            {
                Index = -1,
                Name = "BadTable",
                ProviderType = "SqlTable",
                Table = "NotARealTable"
            }
        };

        var result = cmd.Handle();

        Assert.Equal(CommandResult.ResultType.Invalid, result.Status);
        Assert.Contains("INFORMATION_SCHEMA", result.Message);
    }

    [Fact]
    public void Save_InvalidIncludeFieldIdentifier_ReturnsInvalid()
    {
        CreateSeedConfig(new List<ProviderPredicateDefinition>());

        var cmd = new SavePredicateCommand
        {
            ConfigPath = _configPath,
            IdentifierValidator = new SqlIdentifierValidator(
                tableLoader: () => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "AccessUser" },
                columnLoader: _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "AccessUserType" }),
            WhereValidator = new SqlWhereClauseValidator(),
            Model = new PredicateEditModel
            {
                Index = -1,
                Name = "BadInclude",
                ProviderType = "SqlTable",
                Table = "AccessUser",
                IncludeFields = "NonexistentColumn"
            }
        };

        var result = cmd.Handle();

        Assert.Equal(CommandResult.ResultType.Invalid, result.Status);
        Assert.Contains("INFORMATION_SCHEMA", result.Message);
    }

    [Fact]
    public void Delete_PredicateInSeedMode_RemovesByIndexFromFlatList()
    {
        // Seed a config with one Deploy + one Seed predicate.
        CreateSeedConfig(new List<ProviderPredicateDefinition>
        {
            new() { Name = "DeployExisting", Mode = DeploymentMode.Deploy, ProviderType = "Content", Path = "/d", AreaId = 1, PageId = 10 },
            new() { Name = "SeedOnly", Mode = DeploymentMode.Seed, ProviderType = "Content", Path = "/d", AreaId = 1, PageId = 10 }
        });

        // Phase 40 D-01: delete is index-based on the flat list — caller passes the flat-list index.
        var del = new DeletePredicateCommand
        {
            ConfigPath = _configPath,
            Index = 1  // SeedOnly is at flat-list index 1
        };

        var result = del.Handle();

        Assert.Equal(CommandResult.ResultType.Ok, result.Status);
        var config = ConfigLoader.Load(_configPath);
        Assert.Single(config.Predicates);
        Assert.Equal("DeployExisting", config.Predicates[0].Name);
        Assert.Equal(DeploymentMode.Deploy, config.Predicates[0].Mode);
    }

    // -------------------------------------------------------------------------
    // Phase 41 D-13 + D-12 + D-11 RED tests: Mode property must become string-typed,
    // ConfigurableProperty must carry hint text, PredicateEditScreen Mode-option
    // labels must drop the parenthetical suffixes. Tests use reflection so the file
    // stays compilable while Mode is still enum-typed; assertions encode the GREEN
    // target so Plan 41-03 must turn them GREEN.
    // -------------------------------------------------------------------------

    [Fact]
    public void ModeProperty_IsString_NotEnum_PostPhase41()
    {
        // Phase 41 D-13: Mode must be string-typed (matches LogLevel/ConflictStrategy precedent).
        // Will FAIL until Plan 41-03 lands.
        var modeProp = typeof(PredicateEditModel).GetProperty("Mode");
        Assert.NotNull(modeProp);
        Assert.Equal(typeof(string), modeProp!.PropertyType);
    }

    [Fact]
    public void ModeProperty_HasHint_WithExplanatoryCopy_PostPhase41()
    {
        // Phase 41 D-12: tooltip-style copy moves to `hint:` parameter (not `explanation:`).
        // Assert via stable substrings — the wording is allowed minor evolution.
        var modeProp = typeof(PredicateEditModel).GetProperty("Mode");
        Assert.NotNull(modeProp);
        var attr = modeProp!.GetCustomAttribute<ConfigurablePropertyAttribute>();
        Assert.NotNull(attr);
        var hint = attr!.Hint ?? string.Empty;
        Assert.Contains("Deploy =", hint);
        Assert.Contains("Seed =", hint);
        Assert.Contains("source-wins", hint);
        Assert.Contains("field-level merge", hint);
    }

    [Fact]
    public void Save_ModeAsString_Deploy_RoundTripsViaQuery_PostPhase41()
    {
        // Phase 41 D-13: SavePredicateCommand parses string→enum on save; PredicateByIndexQuery
        // returns string on hydrate. Round-trip must preserve "Deploy" verbatim.
        // RED today: Mode is enum-typed so the round-trip yields DeploymentMode.Deploy, not "Deploy".
        CreateSeedConfig(new List<ProviderPredicateDefinition>());

        var savedOverride = ConfigPathResolver.TestOverridePath;
        ConfigPathResolver.TestOverridePath = _configPath;
        try
        {
            var model = new PredicateEditModel { Index = -1, Name = "Default", ProviderType = "Content", AreaId = 1, PageId = 10 };
            // Mode set via reflection to keep this test compilable while the property is still enum-typed.
            var modeProp = typeof(PredicateEditModel).GetProperty("Mode")!;
            if (modeProp.PropertyType == typeof(string))
                modeProp.SetValue(model, "Deploy");
            else
                modeProp.SetValue(model, Enum.Parse(typeof(DeploymentMode), "Deploy"));

            var saveCmd = new SavePredicateCommand { ConfigPath = _configPath, Model = model };
            var saveResult = saveCmd.Handle();
            Assert.Equal(CommandResult.ResultType.Ok, saveResult.Status);

            var query = new PredicateByIndexQuery();
            typeof(PredicateByIndexQuery).GetProperty("Index")!.SetValue(query, 0);
            var reloaded = query.GetModel();
            Assert.NotNull(reloaded);

            var reloadedMode = modeProp.GetValue(reloaded);
            // GREEN target: reloadedMode is the string "Deploy". RED today: it's DeploymentMode.Deploy.
            Assert.Equal("Deploy", reloadedMode);
        }
        finally
        {
            ConfigPathResolver.TestOverridePath = savedOverride;
        }
    }

    [Fact]
    public void Save_ModeAsString_Seed_RoundTripsViaQuery_PostPhase41()
    {
        CreateSeedConfig(new List<ProviderPredicateDefinition>());

        var savedOverride = ConfigPathResolver.TestOverridePath;
        ConfigPathResolver.TestOverridePath = _configPath;
        try
        {
            var model = new PredicateEditModel { Index = -1, Name = "Default", ProviderType = "Content", AreaId = 1, PageId = 10 };
            var modeProp = typeof(PredicateEditModel).GetProperty("Mode")!;
            if (modeProp.PropertyType == typeof(string))
                modeProp.SetValue(model, "Seed");
            else
                modeProp.SetValue(model, Enum.Parse(typeof(DeploymentMode), "Seed"));

            var saveCmd = new SavePredicateCommand { ConfigPath = _configPath, Model = model };
            Assert.Equal(CommandResult.ResultType.Ok, saveCmd.Handle().Status);

            var query = new PredicateByIndexQuery();
            typeof(PredicateByIndexQuery).GetProperty("Index")!.SetValue(query, 0);
            var reloaded = query.GetModel();
            Assert.NotNull(reloaded);
            Assert.Equal("Seed", modeProp.GetValue(reloaded));
        }
        finally
        {
            ConfigPathResolver.TestOverridePath = savedOverride;
        }
    }

    [Fact]
    public void Save_ModeAsString_BogusValue_ReturnsInvalid_PostPhase41()
    {
        // Phase 41 D-13 + threat model T-41-01: invalid Mode strings rejected by SavePredicateCommand
        // with CommandResult.ResultType.Invalid (mirrors ConfigLoader's case-insensitive Enum.TryParse
        // pre-validation). RED today: when Mode is enum, you can't even ASSIGN "BogusMode" — the test
        // proves the post-fix invariant. Skips early until model is string-typed.
        CreateSeedConfig(new List<ProviderPredicateDefinition>());
        var model = new PredicateEditModel { Index = -1, Name = "Default", ProviderType = "Content", AreaId = 1, PageId = 10 };
        var modeProp = typeof(PredicateEditModel).GetProperty("Mode")!;
        if (modeProp.PropertyType != typeof(string))
        {
            // RED today: this short-circuits while Mode is enum. Becomes a real RED assertion
            // once Plan 41-03 lands the string-typed Mode and the test reaches SavePredicateCommand.
            return;
        }
        modeProp.SetValue(model, "BogusMode");

        var saveCmd = new SavePredicateCommand { ConfigPath = _configPath, Model = model };
        var result = saveCmd.Handle();

        Assert.Equal(CommandResult.ResultType.Invalid, result.Status);
        Assert.Contains("Mode", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(
            result.Message.Contains("Deploy", StringComparison.OrdinalIgnoreCase) ||
            result.Message.Contains("Seed", StringComparison.OrdinalIgnoreCase),
            $"Expected error message to name the valid values; got: {result.Message}");
    }

    [Fact]
    public void PredicateEditScreen_ModeOptions_HaveCleanLabels_PostPhase41()
    {
        // Phase 41 D-11: option labels lose the parenthetical "(source-wins)" /
        // "(field-level merge)" suffixes.
        var screen = new PredicateEditScreen();
        var getEditor = typeof(PredicateEditScreen)
            .GetMethod("GetEditor", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var editor = (EditorBase?)getEditor.Invoke(screen, new object[] { "Mode" });
        Assert.NotNull(editor);
        var select = Assert.IsType<Select>(editor);
        Assert.NotNull(select.Options);
        Assert.Contains(select.Options!, o => (o.Value as string) == "Deploy" && o.Label == "Deploy");
        Assert.Contains(select.Options!, o => (o.Value as string) == "Seed" && o.Label == "Seed");
        // Reject parens-suffix labels explicitly.
        Assert.DoesNotContain(select.Options!, o => o.Label != null && o.Label.Contains("("));
    }
}
