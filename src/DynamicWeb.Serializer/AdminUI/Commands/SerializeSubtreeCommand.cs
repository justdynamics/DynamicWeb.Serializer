using System.IO.Compression;
using Dynamicweb.Content;
using DynamicWeb.Serializer.Configuration;
using DynamicWeb.Serializer.Models;
using DynamicWeb.Serializer.Serialization;
using Dynamicweb.CoreUI.Data;

namespace DynamicWeb.Serializer.AdminUI.Commands;

public sealed class SerializeSubtreeCommand : CommandBase
{
    public int PageId { get; set; }
    public int AreaId { get; set; }

    public override CommandResult Handle()
    {
        if (PageId <= 0)
            return new() { Status = CommandResult.ResultType.Invalid, Message = "PageId is required" };
        if (AreaId <= 0)
            return new() { Status = CommandResult.ResultType.Invalid, Message = "AreaId is required" };

        try
        {
            // 1. Load the clicked page to get its name and build content path
            var page = Services.Pages.GetPage(PageId);
            if (page == null)
                return new() { Status = CommandResult.ResultType.Error, Message = $"Page {PageId} not found" };

            var pageName = page.MenuText ?? $"Page{PageId}";
            var contentPath = BuildContentPath(page);

            // 2. Create temp directory for serialization output
            var tempDir = Path.Combine(Path.GetTempPath(), "Serializer", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                // 3. Create temp SerializerConfiguration with single predicate targeting the page subtree (per D-05)
                var tempConfig = new SerializerConfiguration
                {
                    OutputDirectory = tempDir,
                    LogLevel = "info",
                    DryRun = false,
                    ConflictStrategy = ConflictStrategy.SourceWins,
                    Predicates = new List<ProviderPredicateDefinition>
                    {
                        new ProviderPredicateDefinition
                        {
                            Name = "ad-hoc-serialize",
                            ProviderType = "Content",
                            Path = contentPath,
                            AreaId = AreaId,
                            Excludes = new List<string>()
                        }
                    }
                };

                // 4. Run serialization (reuses existing ContentSerializer -- ACT-08)
                var serializer = new ContentSerializer(tempConfig);
                serializer.Serialize();

                // 5. Create zip from the serialized output (per D-01: YAML files in mirror-tree layout)
                var zipFileName = $"Serializer_{SanitizeFileName(pageName)}_{DateTime.Now:yyyy-MM-dd}.zip";
                var zipPath = Path.Combine(Path.GetTempPath(), "Serializer", zipFileName);
                Directory.CreateDirectory(Path.GetDirectoryName(zipPath)!);

                if (File.Exists(zipPath))
                    File.Delete(zipPath);

                // Write a log file into the temp dir before zipping (per D-01)
                var logContent = $"Serializer Export\n" +
                                $"Page: {pageName} (ID={PageId})\n" +
                                $"Area: {AreaId}\n" +
                                $"Content Path: {contentPath}\n" +
                                $"Exported: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                                $"Files: {Directory.GetFiles(tempDir, "*.yml", SearchOption.AllDirectories).Length} YAML files\n";
                File.WriteAllText(Path.Combine(tempDir, "export.log"), logContent);

                ZipFile.CreateFromDirectory(tempDir, zipPath);

                // 6. Copy to download subfolder
                CopyToDownloadDir(zipPath, zipFileName);

                // 7. Clean up temp serialization directory (zip is separate)
                try { Directory.Delete(tempDir, recursive: true); } catch { /* best effort */ }

                // 8. Return FileResult for browser download (per D-03)
                var zipStream = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Delete);
                return new CommandResult
                {
                    Status = CommandResult.ResultType.Ok,
                    Model = new FileResult
                    {
                        FileStream = zipStream,
                        ContentType = "application/zip",
                        FileDownloadName = zipFileName
                    }
                };
            }
            catch
            {
                // Clean up temp dir on error
                try { Directory.Delete(tempDir, recursive: true); } catch { }
                throw;
            }
        }
        catch (Exception ex)
        {
            return new() { Status = CommandResult.ResultType.Error, Message = $"Serialize failed: {ex.Message}" };
        }
    }

    /// <summary>
    /// Builds the content path from root to the given page by walking up the parent chain.
    /// ContentSerializer uses "/" + rootPage.MenuText for roots and appends "/" + child.MenuText.
    /// So for a page at /CustomerCenter/SubPage, the path is "/CustomerCenter/SubPage".
    /// A predicate with this path will include this page and all its children.
    /// </summary>
    private static string BuildContentPath(Page page)
    {
        var segments = new List<string>();
        var current = page;
        while (current != null)
        {
            segments.Insert(0, current.MenuText ?? string.Empty);
            current = current.ParentPageId > 0
                ? Services.Pages.GetPage(current.ParentPageId)
                : null;
        }
        return "/" + string.Join("/", segments);
    }

    private static void CopyToDownloadDir(string zipPath, string zipFileName)
    {
        try
        {
            var configPath = ConfigPathResolver.FindOrCreateConfigFile();
            var config = ConfigLoader.Load(configPath);

            var filesDir = Path.GetDirectoryName(configPath)!;
            var systemDir = Path.Combine(filesDir, "System");
            var paths = config.EnsureDirectories(systemDir);

            var destPath = Path.Combine(paths.Download, zipFileName);
            File.Copy(zipPath, destPath, overwrite: true);
        }
        catch
        {
            // Download copy is best-effort -- don't fail the browser download
        }
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    }
}
