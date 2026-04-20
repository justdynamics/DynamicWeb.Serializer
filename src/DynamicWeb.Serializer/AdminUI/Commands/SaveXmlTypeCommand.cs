using DynamicWeb.Serializer.AdminUI.Models;
using DynamicWeb.Serializer.Configuration;
using Dynamicweb.CoreUI.Data;

namespace DynamicWeb.Serializer.AdminUI.Commands;

public sealed class SaveXmlTypeCommand : CommandBase<XmlTypeEditModel>
{
    /// <summary>Optional override for testing -- bypasses ConfigPathResolver.</summary>
    public string? ConfigPath { get; set; }

    /// <summary>
    /// Which <see cref="DeploymentMode"/>'s <see cref="ModeConfig.ExcludeXmlElementsByType"/>
    /// dictionary this save targets (Phase 37-01.1). Defaulted to Deploy for safety; the admin
    /// UI's tree-to-edit-screen routing populates it explicitly from <see cref="XmlTypeEditModel.Mode"/>.
    /// </summary>
    public DeploymentMode Mode { get; set; } = DeploymentMode.Deploy;

    public override CommandResult Handle()
    {
        if (Model is null)
            return new() { Status = CommandResult.ResultType.Invalid, Message = "Model data must be given" };

        if (string.IsNullOrWhiteSpace(Model.TypeName))
            return new() { Status = CommandResult.ResultType.Invalid, Message = "Type name is required" };

        try
        {
            var configPath = ConfigPath ?? ConfigPathResolver.FindOrCreateConfigFile();
            var config = ConfigLoader.Load(configPath);

            // Parse excluded elements from newline-separated string
            var excludedElements = (Model.ExcludedElements ?? string.Empty)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(e => e.Trim())
                .Where(e => e.Length > 0)
                .ToList();

            // Phase 37-01.1: route to the per-mode ModeConfig. Model.Mode takes precedence (set by
            // the tree's edit-screen navigation); the command's own Mode is a safety default.
            var mode = Model.Mode;
            var modeConfig = config.GetMode(mode);

            var updated = new Dictionary<string, List<string>>(modeConfig.ExcludeXmlElementsByType, StringComparer.OrdinalIgnoreCase);
            updated[Model.TypeName] = excludedElements;

            var updatedMode = modeConfig with { ExcludeXmlElementsByType = updated };
            var newConfig = mode == DeploymentMode.Deploy
                ? config with { Deploy = updatedMode }
                : config with { Seed = updatedMode };
            ConfigWriter.Save(newConfig, configPath);

            return new() { Status = CommandResult.ResultType.Ok, Model = Model };
        }
        catch (Exception ex)
        {
            return new() { Status = CommandResult.ResultType.Error, Message = ex.Message };
        }
    }
}
