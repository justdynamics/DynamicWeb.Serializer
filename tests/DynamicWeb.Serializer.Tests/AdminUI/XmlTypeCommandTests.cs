using System.Data;
using DynamicWeb.Serializer.AdminUI.Commands;
using DynamicWeb.Serializer.AdminUI.Infrastructure;
using DynamicWeb.Serializer.AdminUI.Models;
using DynamicWeb.Serializer.Configuration;
using DynamicWeb.Serializer.Models;
using DynamicWeb.Serializer.Providers.SqlTable;
using Dynamicweb.CoreUI.Data;
using Dynamicweb.Data;
using Xunit;

namespace DynamicWeb.Serializer.Tests.AdminUI;

public class XmlTypeCommandTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _configPath;

    public XmlTypeCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "XmlTypeCmdTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _configPath = Path.Combine(_tempDir, "Serializer.config.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private void CreateSeedConfig(Dictionary<string, List<string>>? excludeXmlElementsByType = null)
    {
        var config = new SerializerConfiguration
        {
            OutputDirectory = @"\System\Serializer",
            LogLevel = "info",
            DryRun = false,
            ConflictStrategy = ConflictStrategy.SourceWins,
            Predicates = new List<ProviderPredicateDefinition>
            {
                new() { Name = "Default", ProviderType = "Content", Path = "/", AreaId = 1, PageId = 10 }
            },
            ExcludeXmlElementsByType = excludeXmlElementsByType ?? new()
        };
        ConfigWriter.Save(config, _configPath);
    }

    private SerializerConfiguration LoadConfig() => ConfigLoader.Load(_configPath);

    /// <summary>
    /// Minimal ISqlExecutor that maps SQL query substrings to canned DataTable results.
    /// </summary>
    private sealed class FakeSqlExecutor : ISqlExecutor
    {
        private readonly List<(string Substring, DataTable Result)> _mappings = new();

        public void AddMapping(string querySubstring, DataTable result)
        {
            _mappings.Add((querySubstring, result));
        }

        public IDataReader ExecuteReader(CommandBuilder command)
        {
            var sql = command.ToString() ?? string.Empty;
            foreach (var (substring, result) in _mappings)
            {
                if (sql.Contains(substring, StringComparison.OrdinalIgnoreCase))
                    return result.CreateDataReader();
            }
            return new DataTable().CreateDataReader();
        }

        public int ExecuteNonQuery(CommandBuilder command) => 0;
    }

    private static DataTable CreateSingleColumnTable(string columnName, params string[] values)
    {
        var dt = new DataTable();
        dt.Columns.Add(columnName, typeof(string));
        foreach (var v in values)
            dt.Rows.Add(v);
        return dt;
    }

    // -------------------------------------------------------------------------
    // ScanXmlTypesCommand tests
    // -------------------------------------------------------------------------

    [Fact]
    public void Scan_AddsNewTypes_PreservesExisting()
    {
        // Seed config with TypeA having existing exclusions
        CreateSeedConfig(new Dictionary<string, List<string>>
        {
            ["TypeA"] = new List<string> { "el1" }
        });

        var executor = new FakeSqlExecutor();
        executor.AddMapping("PageUrlDataProviderType",
            CreateSingleColumnTable("PageUrlDataProviderType", "TypeA", "TypeB"));
        executor.AddMapping("ParagraphModuleSystemName",
            CreateSingleColumnTable("ParagraphModuleSystemName"));

        var discovery = new XmlTypeDiscovery(executor);
        var cmd = new ScanXmlTypesCommand
        {
            ConfigPath = _configPath,
            Discovery = discovery
        };

        var result = cmd.Handle();

        Assert.Equal(CommandResult.ResultType.Ok, result.Status);
        var config = LoadConfig();
        Assert.True(config.ExcludeXmlElementsByType.ContainsKey("TypeA"));
        Assert.True(config.ExcludeXmlElementsByType.ContainsKey("TypeB"));
        // Existing exclusions preserved
        Assert.Contains("el1", config.ExcludeXmlElementsByType["TypeA"]);
        // New type has empty list
        Assert.Empty(config.ExcludeXmlElementsByType["TypeB"]);
    }

    [Fact]
    public void Scan_EmptyConfig_AddsAllDiscoveredTypes()
    {
        CreateSeedConfig();

        var executor = new FakeSqlExecutor();
        executor.AddMapping("PageUrlDataProviderType",
            CreateSingleColumnTable("PageUrlDataProviderType", "TypeA"));
        executor.AddMapping("ParagraphModuleSystemName",
            CreateSingleColumnTable("ParagraphModuleSystemName", "TypeB"));

        var discovery = new XmlTypeDiscovery(executor);
        var cmd = new ScanXmlTypesCommand
        {
            ConfigPath = _configPath,
            Discovery = discovery
        };

        var result = cmd.Handle();

        Assert.Equal(CommandResult.ResultType.Ok, result.Status);
        var config = LoadConfig();
        Assert.Equal(2, config.ExcludeXmlElementsByType.Count);
        Assert.Empty(config.ExcludeXmlElementsByType["TypeA"]);
        Assert.Empty(config.ExcludeXmlElementsByType["TypeB"]);
    }

    [Fact]
    public void Scan_NoNewTypes_ConfigUnchanged()
    {
        CreateSeedConfig(new Dictionary<string, List<string>>
        {
            ["TypeA"] = new List<string> { "el1", "el2" }
        });

        var executor = new FakeSqlExecutor();
        executor.AddMapping("PageUrlDataProviderType",
            CreateSingleColumnTable("PageUrlDataProviderType", "TypeA"));
        executor.AddMapping("ParagraphModuleSystemName",
            CreateSingleColumnTable("ParagraphModuleSystemName"));

        var discovery = new XmlTypeDiscovery(executor);
        var cmd = new ScanXmlTypesCommand
        {
            ConfigPath = _configPath,
            Discovery = discovery
        };

        var result = cmd.Handle();

        Assert.Equal(CommandResult.ResultType.Ok, result.Status);
        var config = LoadConfig();
        Assert.Single(config.ExcludeXmlElementsByType);
        Assert.Equal(new List<string> { "el1", "el2" }, config.ExcludeXmlElementsByType["TypeA"]);
    }

    // -------------------------------------------------------------------------
    // SaveXmlTypeCommand tests
    // -------------------------------------------------------------------------

    [Fact]
    public void Save_NullModel_ReturnsInvalid()
    {
        var cmd = new SaveXmlTypeCommand { Model = null };

        var result = cmd.Handle();

        Assert.Equal(CommandResult.ResultType.Invalid, result.Status);
        Assert.Contains("Model data must be given", result.Message);
    }

    [Fact]
    public void Save_EmptyTypeName_ReturnsInvalid()
    {
        var cmd = new SaveXmlTypeCommand
        {
            Model = new XmlTypeEditModel { TypeName = "", ExcludedElements = "el1" }
        };

        var result = cmd.Handle();

        Assert.Equal(CommandResult.ResultType.Invalid, result.Status);
        Assert.Contains("Type name is required", result.Message);
    }

    [Fact]
    public void Save_ValidModel_PersistsExclusions()
    {
        CreateSeedConfig(new Dictionary<string, List<string>>
        {
            ["TypeA"] = new List<string>()
        });

        var cmd = new SaveXmlTypeCommand
        {
            ConfigPath = _configPath,
            Model = new XmlTypeEditModel
            {
                TypeName = "TypeA",
                ExcludedElements = "el1\nel2"
            }
        };

        var result = cmd.Handle();

        Assert.Equal(CommandResult.ResultType.Ok, result.Status);
        var config = LoadConfig();
        Assert.Equal(new List<string> { "el1", "el2" }, config.ExcludeXmlElementsByType["TypeA"]);
    }

    [Fact]
    public void Save_UpdateExisting_ReplacesExclusions()
    {
        CreateSeedConfig(new Dictionary<string, List<string>>
        {
            ["TypeA"] = new List<string> { "el1" }
        });

        var cmd = new SaveXmlTypeCommand
        {
            ConfigPath = _configPath,
            Model = new XmlTypeEditModel
            {
                TypeName = "TypeA",
                ExcludedElements = "el2\nel3"
            }
        };

        var result = cmd.Handle();

        Assert.Equal(CommandResult.ResultType.Ok, result.Status);
        var config = LoadConfig();
        Assert.Equal(new List<string> { "el2", "el3" }, config.ExcludeXmlElementsByType["TypeA"]);
    }

    [Fact]
    public void Save_EmptyExclusions_PersistsEmptyList()
    {
        CreateSeedConfig(new Dictionary<string, List<string>>
        {
            ["TypeA"] = new List<string> { "el1" }
        });

        var cmd = new SaveXmlTypeCommand
        {
            ConfigPath = _configPath,
            Model = new XmlTypeEditModel
            {
                TypeName = "TypeA",
                ExcludedElements = ""
            }
        };

        var result = cmd.Handle();

        Assert.Equal(CommandResult.ResultType.Ok, result.Status);
        var config = LoadConfig();
        Assert.Empty(config.ExcludeXmlElementsByType["TypeA"]);
    }
}
