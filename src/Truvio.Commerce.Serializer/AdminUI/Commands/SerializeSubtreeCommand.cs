using Dynamicweb.Content;
using Truvio.Commerce.Serializer.AdminUI.Security;
using Truvio.Commerce.Serializer.Serialization;
using Dynamicweb.CoreUI.Data;

namespace Truvio.Commerce.Serializer.AdminUI.Commands;

/// <summary>
/// API-facing flat-parameter variant of the package download (legacy name kept so
/// POST /Admin/Api/SerializeSubtree continues to work). The admin UI goes through
/// <see cref="Screens.DownloadPackageScreen"/> + <see cref="DownloadPackageCommand"/>;
/// both route into <see cref="PackageBuilder"/>.
/// </summary>
public sealed class SerializeSubtreeCommand : CommandBase
{
    public int PageId { get; set; }
    public int AreaId { get; set; }

    /// <summary>Content scope: PageAndSubpages (default), PageOnly, or SubpagesOnly.</summary>
    public string Scope { get; set; } = PackageBuilder.ScopePageAndSubpages;

    /// <summary>Bundle referenced images/files from the Files archive into the package.</summary>
    public bool IncludeAssets { get; set; }

    public override CommandResult Handle()
    {
        if (PageId <= 0)
            return new() { Status = CommandResult.ResultType.Invalid, Message = "PageId is required" };
        if (AreaId <= 0)
            return new() { Status = CommandResult.ResultType.Invalid, Message = "AreaId is required" };

        try
        {
            var page = Services.Pages.GetPage(PageId);
            if (page == null)
                return new() { Status = CommandResult.ResultType.Error, Message = $"Page {PageId} not found" };

            if (!PackageAccess.CanDownload(page))
                return new()
                {
                    Status = CommandResult.ResultType.NotAllowed,
                    Message = "You do not have permission to download packages for this page."
                };

            var filesRoot = DownloadPackageCommand.ResolveFilesRoot();
            var result = PackageBuilder.Build(PageId, AreaId, Scope, IncludeAssets, filesRoot);

            DownloadPackageCommand.CopyToDownloadDir(result.ZipPath, result.ZipFileName);

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
            return new() { Status = CommandResult.ResultType.Error, Message = $"Serialize failed: {ex.Message}" };
        }
    }

    /// <summary>
    /// Builds the content path from root to the given page by walking up the parent chain.
    /// Kept here because tree decoration and predicate path checks call through this name.
    /// </summary>
    internal static string BuildContentPath(Page page) => ContentPathBuilder.BuildContentPath(page);
}
