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

        try
        {
            var configPath = ConfigPathResolver.FindOrCreateConfigFile();
            var existingConfig = ConfigLoader.Load(configPath);

            var updatedConfig = new SyncConfiguration
            {
                OutputDirectory = Model.OutputDirectory,
                LogLevel = Model.LogLevel,
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
