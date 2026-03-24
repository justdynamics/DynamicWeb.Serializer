using Dynamicweb.CoreUI;
using Dynamicweb.CoreUI.Actions;
using Dynamicweb.CoreUI.Actions.Implementations;
using Dynamicweb.CoreUI.Icons;
using Dynamicweb.CoreUI.Layout;
using Dynamicweb.CoreUI.Screens;
using Dynamicweb.Files.UI.Screens.Files;
using DynamicWeb.Serializer.AdminUI.Queries;
using DynamicWeb.Serializer.AdminUI.Screens;
using DynamicWeb.Serializer.Configuration;

namespace DynamicWeb.Serializer.AdminUI.Injectors;

/// <summary>
/// Injects "Import to database" action into the FileOverviewScreen for .zip files
/// that are located in the configured output directory.
/// Auto-discovered by DW's AddInManager.
/// </summary>
public sealed class SerializerFileOverviewInjector : ScreenInjector<FileOverviewScreen>
{
    public override void OnAfter(FileOverviewScreen screen, UiComponentBase content)
    {
        var model = Screen?.Model;
        if (model is null)
            return;

        if (!IsZipExtension(model.Extension))
            return;

        if (!IsInOutputDirectory(model.FilePath))
            return;

        content.TryGet<ScreenLayout>(out var screenLayout);
        screenLayout?.ContextActionGroups.Add(new ActionGroup
        {
            Nodes =
            [
                new ActionNode
                {
                    Name = "Import to database",
                    Icon = Icon.Upload,
                    NodeAction = OpenDialogAction.To<DeserializeFromZipScreen>()
                        .With(new DeserializeFromZipQuery { FilePath = model.FilePath })
                }
            ]
        });
    }

    /// <summary>
    /// Checks if the given extension is .zip (case-insensitive).
    /// </summary>
    public static bool IsZipExtension(string? extension)
    {
        if (string.IsNullOrEmpty(extension))
            return false;

        return string.Equals(extension, ".zip", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if the file path is under the configured output directory.
    /// Uses config to resolve the output directory.
    /// </summary>
    internal static bool IsInOutputDirectory(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return false;

        var configPath = ConfigPathResolver.FindConfigFile();
        if (configPath == null)
            return false;

        var config = ConfigLoader.Load(configPath);
        var outputDir = config.OutputDirectory?.Replace('\\', '/').TrimEnd('/') ?? "";

        return IsPathUnderDirectory(filePath, outputDir);
    }

    /// <summary>
    /// Pure path comparison: checks if filePath contains the directory segment.
    /// Exposed for unit testing.
    /// </summary>
    public static bool IsPathUnderDirectory(string? filePath, string? directory)
    {
        if (string.IsNullOrEmpty(filePath) || string.IsNullOrEmpty(directory))
            return false;

        var normalizedPath = filePath.Replace('\\', '/');
        var normalizedDir = directory.Replace('\\', '/').TrimEnd('/');

        return normalizedPath.Contains(normalizedDir, StringComparison.OrdinalIgnoreCase);
    }
}
