using Dynamicweb.ContentSync.Configuration;
using Xunit;

namespace Dynamicweb.ContentSync.Tests.Configuration;

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
    public void Load_ValidConfig_ReturnsSyncConfiguration()
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
    public void Load_MissingPredicates_ThrowsInvalidOperationException_WithFieldName()
    {
        var json = """
            {
              "outputDirectory": "/serialization"
            }
            """;
        var path = WriteConfigFile(json);

        var ex = Assert.Throws<InvalidOperationException>(() => ConfigLoader.Load(path));

        Assert.Contains("predicates", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_EmptyPredicates_ThrowsInvalidOperationException_WithFieldName()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "predicates": []
            }
            """;
        var path = WriteConfigFile(json);

        var ex = Assert.Throws<InvalidOperationException>(() => ConfigLoader.Load(path));

        Assert.Contains("predicates", ex.Message, StringComparison.OrdinalIgnoreCase);
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
}
