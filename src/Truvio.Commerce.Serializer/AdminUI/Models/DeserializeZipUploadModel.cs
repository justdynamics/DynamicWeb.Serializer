using Truvio.Commerce.Serializer.Configuration;
using Dynamicweb.Content;
using Dynamicweb.CoreUI.Data;

namespace Truvio.Commerce.Serializer.AdminUI.Models;

/// <summary>
/// Model for the DeserializeZipUploadScreen (tree right-click "Deserialize from zip").
/// Resolves the upload folder under Files/System/Serializer/Upload and the target area name.
/// </summary>
public sealed class DeserializeZipUploadModel : DataViewModelBase
{
    public int TargetAreaId { get; set; }

    public string AreaName { get; set; } = "";

    /// <summary>Virtual /Files path the FileUpload editor uploads into.</summary>
    public string UploadFolder { get; set; } = "";

    public string? ValidationError { get; set; }

    public static DeserializeZipUploadModel Load(int targetAreaId)
    {
        var model = new DeserializeZipUploadModel { TargetAreaId = targetAreaId };

        try
        {
            var area = targetAreaId > 0 ? Services.Areas.GetArea(targetAreaId) : null;
            if (area is null)
            {
                model.ValidationError = $"Area {targetAreaId} not found.";
                return model;
            }
            model.AreaName = area.Name;

            var configPath = ConfigPathResolver.FindConfigFile();
            if (configPath == null)
            {
                model.ValidationError = "Serializer.config.json not found.";
                return model;
            }

            // Physical Files root is the config file's directory; mirror it as the
            // virtual /Files root for the FileUpload editor.
            var filesRoot = Path.GetDirectoryName(configPath)!;
            var uploadPhysical = Path.Combine(filesRoot, "System", "Serializer", "Upload");
            Directory.CreateDirectory(uploadPhysical);

            model.UploadFolder = "/Files/System/Serializer/Upload";
        }
        catch (Exception ex)
        {
            model.ValidationError = $"Failed to prepare upload folder: {ex.Message}";
        }

        return model;
    }
}
