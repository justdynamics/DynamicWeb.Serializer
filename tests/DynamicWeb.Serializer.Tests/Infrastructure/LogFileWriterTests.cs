using DynamicWeb.Serializer.Infrastructure;
using DynamicWeb.Serializer.Models;
using Xunit;

namespace DynamicWeb.Serializer.Tests.Infrastructure;

public class LogFileWriterTests : IDisposable
{
    private readonly string _tempDir;

    public LogFileWriterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"LogFileWriterTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void CreateLogFile_Serialize_ReturnsPathMatchingPattern()
    {
        var path = LogFileWriter.CreateLogFile(_tempDir, "Serialize");

        Assert.StartsWith(Path.Combine(_tempDir, "Serialize_"), path);
        Assert.EndsWith(".log", path);
        // Verify the file was created
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void CreateLogFile_Deserialize_ReturnsPathMatchingPattern()
    {
        var path = LogFileWriter.CreateLogFile(_tempDir, "Deserialize");

        Assert.StartsWith(Path.Combine(_tempDir, "Deserialize_"), path);
        Assert.EndsWith(".log", path);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void CreateLogFile_CreatesDirectory_WhenNotExists()
    {
        var subDir = Path.Combine(_tempDir, "sub", "dir");
        var path = LogFileWriter.CreateLogFile(subDir, "Serialize");

        Assert.True(File.Exists(path));
    }

    [Fact]
    public void WriteSummaryHeader_WritesJsonBetweenMarkers()
    {
        var logFile = LogFileWriter.CreateLogFile(_tempDir, "Serialize");
        var summary = new LogFileSummary
        {
            Operation = "Serialize",
            Timestamp = new DateTime(2026, 3, 24, 12, 0, 0, DateTimeKind.Utc),
            TotalCreated = 10,
            Predicates = new List<PredicateSummary>
            {
                new() { Name = "Test", Table = "TestTable", Created = 10 }
            }
        };

        LogFileWriter.WriteSummaryHeader(logFile, summary);
        var content = File.ReadAllText(logFile);

        Assert.Contains("=== SERIALIZER SUMMARY ===", content);
        Assert.Contains("=== END SUMMARY ===", content);
        Assert.Contains("\"operation\"", content); // camelCase JSON
        Assert.Contains("\"totalCreated\"", content);
    }

    [Fact]
    public void AppendLogLine_PrependsTimestamp()
    {
        var logFile = LogFileWriter.CreateLogFile(_tempDir, "Serialize");
        LogFileWriter.AppendLogLine(logFile, "Test message");

        var content = File.ReadAllText(logFile);
        // Should match [yyyy-MM-dd HH:mm:ss.fff] format
        Assert.Matches(@"\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}\] Test message", content);
    }

    [Fact]
    public void ParseSummaryHeader_ExtractsLogFileSummary()
    {
        var logFile = LogFileWriter.CreateLogFile(_tempDir, "Serialize");
        var original = new LogFileSummary
        {
            Operation = "Serialize",
            Timestamp = new DateTime(2026, 3, 24, 12, 0, 0, DateTimeKind.Utc),
            TotalCreated = 5,
            TotalUpdated = 3,
            Errors = new List<string> { "Some error" }
        };
        LogFileWriter.WriteSummaryHeader(logFile, original);

        var parsed = LogFileWriter.ParseSummaryHeader(logFile);

        Assert.NotNull(parsed);
        Assert.Equal("Serialize", parsed!.Operation);
        Assert.Equal(5, parsed.TotalCreated);
        Assert.Equal(3, parsed.TotalUpdated);
        Assert.Single(parsed.Errors);
    }

    [Fact]
    public void ParseSummaryHeader_ReturnsNull_WhenNoMarkers()
    {
        var logFile = Path.Combine(_tempDir, "empty.log");
        File.WriteAllText(logFile, "Just some random log lines\nNo markers here\n");

        var parsed = LogFileWriter.ParseSummaryHeader(logFile);

        Assert.Null(parsed);
    }

    [Fact]
    public void GetLogFiles_ReturnsSortedByMostRecentFirst()
    {
        var file1 = LogFileWriter.CreateLogFile(_tempDir, "Serialize");
        System.Threading.Thread.Sleep(50); // Ensure different timestamps
        var file2 = LogFileWriter.CreateLogFile(_tempDir, "Deserialize");

        var files = LogFileWriter.GetLogFiles(_tempDir);

        Assert.Equal(2, files.Length);
        Assert.Equal(Path.GetFileName(file2), files[0].Name); // Most recent first
    }
}
