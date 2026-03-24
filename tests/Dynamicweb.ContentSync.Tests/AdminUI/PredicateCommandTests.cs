using Dynamicweb.ContentSync.AdminUI.Commands;
using Dynamicweb.ContentSync.AdminUI.Models;
using Dynamicweb.ContentSync.Configuration;
using Dynamicweb.ContentSync.Models;
using Dynamicweb.CoreUI.Data;
using Xunit;

namespace Dynamicweb.ContentSync.Tests.AdminUI;

public class PredicateCommandTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _configPath;

    public PredicateCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "PredCmdTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _configPath = Path.Combine(_tempDir, "ContentSync.config.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private void CreateSeedConfig(List<ProviderPredicateDefinition>? predicates = null)
    {
        var config = new SyncConfiguration
        {
            OutputDirectory = @"\System\ContentSync",
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
