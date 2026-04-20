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

            // Resolve OutputDirectory relative to Files/System and ensure subdirectories exist
            var filesDir = Path.GetDirectoryName(configPath)!; // wwwroot/Files/
            var systemDir = Path.Combine(filesDir, "System");
            var resolvedOutputDir = Path.GetFullPath(
                Path.Combine(systemDir, Model.OutputDirectory.TrimStart('\\', '/')));

            // Create the top-level folder and all subfolders (serializeRoot, upload, download)
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

            // Phase 37-01 D-02: settings screen does not edit predicates directly — the Deploy
            // and Seed ModeConfigs round-trip unchanged. ConflictStrategy on the top-level model
            // maps to Deploy's strategy (legacy alias); Seed keeps its own strategy.
            var conflictStrategy = Model.ConflictStrategy switch
            {
                "source-wins" => Configuration.ConflictStrategy.SourceWins,
                "destination-wins" => Configuration.ConflictStrategy.DestinationWins,
                _ => Configuration.ConflictStrategy.SourceWins
            };

            var updatedDeploy = existingConfig.Deploy with { ConflictStrategy = conflictStrategy };

            var updatedConfig = new SerializerConfiguration
            {
                OutputDirectory = Model.OutputDirectory,
                LogLevel = Model.LogLevel,
                DryRun = Model.DryRun,
                Deploy = updatedDeploy,
                Seed = existingConfig.Seed
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
