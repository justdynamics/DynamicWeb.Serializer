using Dynamicweb.ContentSync.AdminUI.Models;
using Dynamicweb.ContentSync.Configuration;
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

            // Per D-05: Validate OutputDirectory exists on disk relative to Files/System
            var filesDir = Path.GetDirectoryName(configPath)!; // wwwroot/Files/
            var systemDir = Path.Combine(filesDir, "System");
            var resolvedOutputDir = Path.GetFullPath(
                Path.Combine(systemDir, Model.OutputDirectory.TrimStart('\\', '/')));

            if (!Directory.Exists(resolvedOutputDir))
                return new()
                {
                    Status = CommandResult.ResultType.Invalid,
                    Message = $"Output Directory does not exist: {Model.OutputDirectory} (resolved to {resolvedOutputDir})"
                };

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
