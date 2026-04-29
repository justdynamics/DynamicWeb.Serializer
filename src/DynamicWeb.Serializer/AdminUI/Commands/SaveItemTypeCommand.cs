using DynamicWeb.Serializer.AdminUI.Models;
using DynamicWeb.Serializer.Configuration;
using Dynamicweb.CoreUI.Data;

namespace DynamicWeb.Serializer.AdminUI.Commands;

public sealed class SaveItemTypeCommand : CommandBase<ItemTypeEditModel>
{
    /// <summary>Optional override for testing -- bypasses ConfigPathResolver.</summary>
    public string? ConfigPath { get; set; }

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

            // Phase 40 D-04: write to the top-level dict on SerializerConfiguration.
            var updated = new Dictionary<string, List<string>>(config.ExcludeFieldsByItemType, StringComparer.OrdinalIgnoreCase);
            updated[Model.SystemName] = excludedFields;

            var newConfig = config with { ExcludeFieldsByItemType = updated };
            ConfigWriter.Save(newConfig, configPath);

            return new() { Status = CommandResult.ResultType.Ok, Model = Model };
        }
        catch (Exception ex)
        {
            return new() { Status = CommandResult.ResultType.Error, Message = ex.Message };
        }
    }
}
