using DynamicWeb.Serializer.AdminUI.Models;
using DynamicWeb.Serializer.Configuration;
using Dynamicweb.CoreUI.Data;

namespace DynamicWeb.Serializer.AdminUI.Commands;

public sealed class SaveXmlTypeCommand : CommandBase<XmlTypeEditModel>
{
    /// <summary>Optional override for testing -- bypasses ConfigPathResolver.</summary>
    public string? ConfigPath { get; set; }

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

            var excludedElements = (Model.ExcludedElements ?? new())
                .Select(e => e?.Trim() ?? string.Empty)
                .Where(e => e.Length > 0)
                .ToList();

            // Phase 40 D-04: write to the top-level dict on SerializerConfiguration.
            var updated = new Dictionary<string, List<string>>(config.ExcludeXmlElementsByType, StringComparer.OrdinalIgnoreCase);
            updated[Model.TypeName] = excludedElements;

            var newConfig = config with { ExcludeXmlElementsByType = updated };
            ConfigWriter.Save(newConfig, configPath);

            return new() { Status = CommandResult.ResultType.Ok, Model = Model };
        }
        catch (Exception ex)
        {
            return new() { Status = CommandResult.ResultType.Error, Message = ex.Message };
        }
    }
}
