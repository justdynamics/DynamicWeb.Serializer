using Truvio.Commerce.Serializer.Infrastructure;
using Truvio.Commerce.Serializer.Models;
using Truvio.Commerce.Serializer.Reporting;
using Xunit;

namespace Truvio.Commerce.Serializer.Tests.Reporting;

public class LastRunResolverTests : IDisposable
{
    private readonly string _logDir;

    public LastRunResolverTests()
    {
        _logDir = Path.Combine(Path.GetTempPath(), "LastRunResolverTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_logDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_logDir, recursive: true); } catch { }
    }

    private string WriteLog(string operation, string mode, bool dryRun, DateTime timestampUtc, int created = 0)
    {
        // Unique suffix per file — two writes within the same second would otherwise
        // collide on the timestamped filename. Resolution filters on the summary header,
        // not the filename, so the suffix content is irrelevant.
        var file = LogFileWriter.CreateLogFile(_logDir, operation, $"{mode}-{Guid.NewGuid():N}");
        LogFileWriter.WriteSummaryHeader(file, new LogFileSummary
        {
            Operation = operation,
            Mode = mode,
            DryRun = dryRun,
            Timestamp = timestampUtc,
            TotalCreated = created
        });
        return file;
    }

    [Fact]
    public void FindLastReceived_ReturnsNewestRealReplaceRun()
    {
        var older = WriteLog("Deserialize", "replace", dryRun: false, new DateTime(2026, 6, 10, 10, 0, 0, DateTimeKind.Utc), created: 5);
        File.SetLastWriteTime(older, DateTime.Now.AddMinutes(-30));
        WriteLog("Deserialize", "replace", dryRun: false, new DateTime(2026, 6, 11, 12, 0, 0, DateTimeKind.Utc), created: 9);

        var result = LastRunResolver.FindLastReceived(_logDir, "replace");

        Assert.NotNull(result);
        Assert.Equal(9, result!.TotalCreated);
    }

    [Fact]
    public void FindLastReceived_SkipsDryRuns()
    {
        var real = WriteLog("Deserialize", "replace", dryRun: false, new DateTime(2026, 6, 10, 10, 0, 0, DateTimeKind.Utc), created: 3);
        File.SetLastWriteTime(real, DateTime.Now.AddMinutes(-30));
        WriteLog("Deserialize", "replace", dryRun: true, new DateTime(2026, 6, 11, 12, 0, 0, DateTimeKind.Utc), created: 99);

        var result = LastRunResolver.FindLastReceived(_logDir, "replace");

        Assert.NotNull(result);
        Assert.Equal(3, result!.TotalCreated);
    }

    [Fact]
    public void FindLastReceived_FiltersByMode_AndIgnoresSerializeRuns()
    {
        WriteLog("Deserialize", "merge", dryRun: false, DateTime.UtcNow, created: 7);
        WriteLog("Serialize", "replace", dryRun: false, DateTime.UtcNow, created: 50);

        Assert.Null(LastRunResolver.FindLastReceived(_logDir, "replace"));
        Assert.Equal(7, LastRunResolver.FindLastReceived(_logDir, "merge")!.TotalCreated);
    }

    [Fact]
    public void FindLastReceived_AcceptsZipImportAsReplaceReceived()
    {
        WriteLog("ZipImport", "replace", dryRun: false, DateTime.UtcNow, created: 4);

        Assert.Equal(4, LastRunResolver.FindLastReceived(_logDir, "replace")!.TotalCreated);
    }

    [Fact]
    public void FindLastReceived_EmptyOrMissingDir_ReturnsNull()
    {
        Assert.Null(LastRunResolver.FindLastReceived(_logDir, "replace"));
        Assert.Null(LastRunResolver.FindLastReceived(Path.Combine(_logDir, "nope"), "replace"));
    }

    [Fact]
    public void LegacyLogsWithoutMode_NeverMatchAModeFilter()
    {
        var file = LogFileWriter.CreateLogFile(_logDir, "Deserialize");
        LogFileWriter.WriteSummaryHeader(file, new LogFileSummary
        {
            Operation = "Deserialize",
            Timestamp = DateTime.UtcNow
        });

        Assert.Null(LastRunResolver.FindLastReceived(_logDir, "replace"));
    }
}
