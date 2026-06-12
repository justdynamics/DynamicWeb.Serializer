using Dynamicweb.Content;
using Dynamicweb.CoreUI.Data;
using Truvio.Commerce.Serializer.AdminUI.Models;
using Truvio.Commerce.Serializer.AdminUI.Security;
using Truvio.Commerce.Serializer.Configuration;
using Truvio.Commerce.Serializer.Serialization;

namespace Truvio.Commerce.Serializer.AdminUI.Commands;

/// <summary>
/// "Download Package": builds the subtree zip with the scope and asset choices made in
/// <see cref="Screens.DownloadPackageScreen"/> and streams it as a browser download.
/// A copy lands in the configured Download folder.
/// </summary>
public sealed class DownloadPackageCommand : CommandBase<DownloadPackageModel>
{
    public override CommandResult Handle()
    {
        if (Model is null)
            return new() { Status = CommandResult.ResultType.Invalid, Message = "Model data must be given" };
        if (Model.PageId <= 0)
            return new() { Status = CommandResult.ResultType.Invalid, Message = "PageId is required" };
        if (Model.AreaId <= 0)
            return new() { Status = CommandResult.ResultType.Invalid, Message = "AreaId is required" };

        try
        {
            var page = Services.Pages.GetPage(Model.PageId);
            if (page is null)
                return new() { Status = CommandResult.ResultType.Error, Message = $"Page {Model.PageId} not found" };

            if (!PackageAccess.CanDownload(page))
                return new()
                {
                    Status = CommandResult.ResultType.NotAllowed,
                    Message = "You do not have permission to download packages for this page."
                };

            var filesRoot = ResolveFilesRoot();
            var result = PackageBuilder.Build(Model.PageId, Model.AreaId, Model.Scope, Model.IncludeAssets, filesRoot);

            CopyToDownloadDir(result.ZipPath, result.ZipFileName);

            var zipStream = new FileStream(result.ZipPath, FileMode.Open, FileAccess.Read, FileShare.Delete);
            return new CommandResult
            {
                Status = CommandResult.ResultType.Ok,
                Model = new FileResult
                {
                    FileStream = zipStream,
                    ContentType = "application/zip",
                    FileDownloadName = result.ZipFileName
                }
            };
        }
        catch (Exception ex)
        {
            return new() { Status = CommandResult.ResultType.Error, Message = $"Download Package failed: {ex.Message}" };
        }
    }

    internal static string? ResolveFilesRoot()
    {
        try
        {
            var configPath = ConfigPathResolver.FindOrCreateConfigFile();
            return ConfigPathResolver.GetFilesRoot(configPath);
        }
        catch
        {
            return ConfigPathResolver.TryGetDwFilesRoot();
        }
    }

    internal static void CopyToDownloadDir(string zipPath, string zipFileName)
    {
        try
        {
            var configPath = ConfigPathResolver.FindOrCreateConfigFile();
            var config = ConfigLoader.Load(configPath);

            var filesDir = ConfigPathResolver.GetFilesRoot(configPath);
            var systemDir = Path.Combine(filesDir, "System");
            var paths = config.EnsureDirectories(systemDir);

            var destPath = Path.Combine(paths.Download, zipFileName);
            File.Copy(zipPath, destPath, overwrite: true);
        }
        catch
        {
            // Download copy is best-effort — don't fail the browser download
        }
    }
}
