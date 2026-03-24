using Dynamicweb.ContentSync.AdminUI.Models;
using Dynamicweb.ContentSync.Configuration;
using Dynamicweb.ContentSync.Models;
using Dynamicweb.CoreUI.Data;

namespace Dynamicweb.ContentSync.AdminUI.Commands;

public sealed class SaveSyncSettingsCommand : CommandBase<SyncSettingsModel>
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
                var tempConfig = new SyncConfiguration
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

            var conflictStrategy = Model.ConflictStrategy switch
            {
                "source-wins" => Configuration.ConflictStrategy.SourceWins,
                _ => Configuration.ConflictStrategy.SourceWins
            };

            var updatedConfig = new SyncConfiguration
            {
                OutputDirectory = Model.OutputDirectory,
                LogLevel = Model.LogLevel,
                DryRun = Model.DryRun,
                ConflictStrategy = conflictStrategy,
                Predicates = existingConfig.Predicates
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
