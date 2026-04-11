using DynamicWeb.Serializer.AdminUI.Commands;
using DynamicWeb.Serializer.AdminUI.Models;
using DynamicWeb.Serializer.Configuration;
using DynamicWeb.Serializer.Models;
using Dynamicweb.CoreUI.Data;
using Xunit;

namespace DynamicWeb.Serializer.Tests.AdminUI;

public class PredicateCommandTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _configPath;

    public PredicateCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "PredCmdTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _configPath = Path.Combine(_tempDir, "Serializer.config.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private void CreateSeedConfig(List<ProviderPredicateDefinition>? predicates = null)
    {
        var config = new SerializerConfiguration
        {
            OutputDirectory = @"\System\Serializer",
            LogLevel = "info",
            DryRun = false,
            ConflictStrategy = ConflictStrategy.SourceWins,
            Predicates = predicates ?? new List<ProviderPredicateDefinition>
            {
                new() { Name = "Default", ProviderType = "Content", Path = "/", AreaId = 1, PageId = 10 }
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
            new() { Name = "Existing", ProviderType = "Content", Path = "/existing", AreaId = 1, PageId = 10 }
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
            new() { Name = "Only", ProviderType = "Content", Path = "/only", AreaId = 1, PageId = 10 }
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
            new() { Name = "Existing", ProviderType = "Content", Path = "/existing", AreaId = 1, PageId = 10 }
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
            new() { Name = "First", ProviderType = "Content", Path = "/first", AreaId = 1, PageId = 10 },
            new() { Name = "Second", ProviderType = "Content", Path = "/second", AreaId = 2, PageId = 20 }
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
                ServiceCaches = "Dynamicweb.Ecommerce.Orders.OrderFlowService\nDynamicweb.Ecommerce.Orders.OrderStateService"
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
        Assert.Equal("Dynamicweb.Ecommerce.Orders.OrderFlowService", pred.ServiceCaches[0]);
        Assert.Equal("Dynamicweb.Ecommerce.Orders.OrderStateService", pred.ServiceCaches[1]);
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
            new() { Name = "Order Flows", ProviderType = "SqlTable", Table = "EcomOrderFlow", NameColumn = "OrderFlowName" }
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
            new() { Name = "First", ProviderType = "Content", Path = "/first", AreaId = 1, PageId = 10 },
            new() { Name = "Second", ProviderType = "Content", Path = "/second", AreaId = 2, PageId = 20 }
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
            new() { Name = "Only", ProviderType = "Content", Path = "/only", AreaId = 1, PageId = 10 }
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
}
