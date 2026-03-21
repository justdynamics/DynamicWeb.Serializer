using Dynamicweb.ContentSync.AdminUI.Models;
using Dynamicweb.ContentSync.Configuration;
using Dynamicweb.CoreUI.Data;

namespace Dynamicweb.ContentSync.AdminUI.Queries;

public sealed class SyncSettingsQuery : DataQueryModelBase<SyncSettingsModel>
{
    public override SyncSettingsModel? GetModel()
    {
        var configPath = ConfigPathResolver.FindConfigFile();
        if (configPath == null)
            return new SyncSettingsModel();

        var config = ConfigLoader.Load(configPath);
        return new SyncSettingsModel
        {
            OutputDirectory = config.OutputDirectory,
            LogLevel = config.LogLevel
        };
    }
}
