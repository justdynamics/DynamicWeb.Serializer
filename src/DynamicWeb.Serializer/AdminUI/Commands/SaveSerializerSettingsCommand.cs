using DynamicWeb.Serializer.AdminUI.Models;
using DynamicWeb.Serializer.Configuration;
using DynamicWeb.Serializer.Models;
using Dynamicweb.CoreUI.Data;

namespace DynamicWeb.Serializer.AdminUI.Commands;

public sealed class SaveSerializerSettingsCommand : CommandBase<SerializerSettingsModel>
{
    public override CommandResult Handle()
    {
        if (Model is null)
            return new() { Status = CommandResult.ResultType.Invalid, Message = "Model data must be given" };
        if (string.IsNullOrWhiteSpace(Model.OutputDirectory))
            return new() { Status = CommandResult.ResultType.Invalid, Message = "Output Directory is required" };

        try
        {
            var configPath = ConfigPathResolver.FindOrCreateConfigFile();
            var filesDir = Path.GetDirectoryName(configPath)!;
            var systemDir = Path.Combine(filesDir, "System");
            var resolvedOutputDir = Path.GetFullPath(
                Path.Combine(systemDir, Model.OutputDirectory.TrimStart('\\', '/')));

            try
            {
                var tempConfig = new SerializerConfiguration
                {
                    OutputDirectory = Model.OutputDirectory,
                    Predicates = new List<ProviderPredicateDefinition>()
                };
                tempConfig.EnsureDirectories(systemDir);
            }
            catch (Exception ex)
            {
                return new()
                {
                    Status = CommandResult.ResultType.Invalid,
                    Message = $"Cannot create Output Directory: {Model.OutputDirectory} (resolved to {resolvedOutputDir}): {ex.Message}"
                };
            }

            var existingConfig = ConfigLoader.Load(configPath);

            // Phase 40 D-02: ConflictStrategy is no longer a config knob — it's hardcoded per mode
            // in SerializerConfiguration.GetConflictStrategyForMode. The settings model still
            // exposes the field for UI compat but the value is dropped on save.
            var updatedConfig = existingConfig with
            {
                OutputDirectory = Model.OutputDirectory,
                LogLevel = Model.LogLevel,
                DryRun = Model.DryRun,
                StrictMode = Model.StrictMode
            };

            ConfigWriter.Save(updatedConfig, configPath);

            return new() { Status = CommandResult.ResultType.Ok, Model = Model };
        }
        catch (InvalidOperationException ex)
        {
            return new() { Status = CommandResult.ResultType.Error, Message = ex.Message };
        }
    }
}
