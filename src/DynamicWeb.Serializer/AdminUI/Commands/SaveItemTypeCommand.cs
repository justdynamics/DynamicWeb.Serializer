using DynamicWeb.Serializer.AdminUI.Models;
using DynamicWeb.Serializer.Configuration;
using Dynamicweb.CoreUI.Data;

namespace DynamicWeb.Serializer.AdminUI.Commands;

public sealed class SaveItemTypeCommand : CommandBase<ItemTypeEditModel>
{
    /// <summary>Optional override for testing -- bypasses ConfigPathResolver.</summary>
    public string? ConfigPath { get; set; }

    /// <summary>
    /// Which <see cref="DeploymentMode"/>'s <see cref="ModeConfig.ExcludeFieldsByItemType"/>
    /// dictionary this save targets (Phase 37-01.1). Defaulted to Deploy for safety; the admin
    /// UI's tree-to-edit-screen routing populates it explicitly from <see cref="ItemTypeEditModel.Mode"/>.
    /// </summary>
    public DeploymentMode Mode { get; set; } = DeploymentMode.Deploy;

    public override CommandResult Handle()
    {
        if (Model is null)
            return new() { Status = CommandResult.ResultType.Invalid, Message = "Model data must be given" };

        if (string.IsNullOrWhiteSpace(Model.SystemName))
            return new() { Status = CommandResult.ResultType.Invalid, Message = "Item type system name is required" };

        try
        {
            var configPath = ConfigPath ?? ConfigPathResolver.FindOrCreateConfigFile();
            var config = ConfigLoader.Load(configPath);

            // Parse excluded fields from newline-separated string
            var excludedFields = (Model.ExcludedFields ?? string.Empty)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(e => e.Trim())
                .Where(e => e.Length > 0)
                .ToList();

            // Phase 37-01.1: route to the per-mode ModeConfig. Model.Mode is the source of truth
            // (the tree node the user drilled into sets it); the command's own Mode is a fallback
            // for pre-model-bound call paths.
            var mode = Model.Mode;
            var modeConfig = config.GetMode(mode);

            // Update the dictionary entry for this type (case-insensitive to prevent duplicate keys).
            var updated = new Dictionary<string, List<string>>(modeConfig.ExcludeFieldsByItemType, StringComparer.OrdinalIgnoreCase);
            updated[Model.SystemName] = excludedFields;

            var updatedMode = modeConfig with { ExcludeFieldsByItemType = updated };
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
