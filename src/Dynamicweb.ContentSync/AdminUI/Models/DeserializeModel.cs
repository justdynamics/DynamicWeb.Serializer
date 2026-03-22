using Dynamicweb.CoreUI.Data;

namespace Dynamicweb.ContentSync.AdminUI.Models;

public sealed class DeserializeModel : DataViewModelBase
{
    public int PageId { get; set; }
    public int AreaId { get; set; }

    [ConfigurableProperty("Zip File", explanation: "Upload a ContentSync zip file containing YAML page definitions")]
    public string UploadedFilePath { get; set; } = string.Empty;

    [ConfigurableProperty("Import Mode", explanation: "How to apply the zip content relative to the selected page")]
    public string ImportMode { get; set; } = "children";
}
