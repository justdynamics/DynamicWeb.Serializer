using DynamicWeb.Serializer.AdminUI.Infrastructure;
using DynamicWeb.Serializer.Configuration;
using DynamicWeb.Serializer.Providers.SqlTable;
using Dynamicweb.CoreUI.Data;

namespace DynamicWeb.Serializer.AdminUI.Commands;

public sealed class ScanXmlTypesCommand : CommandBase
{
    /// <summary>Optional test overrides</summary>
    public string? ConfigPath { get; set; }
    public XmlTypeDiscovery? Discovery { get; set; }

    /// <summary>
    /// Which <see cref="DeploymentMode"/>'s <see cref="ModeConfig.ExcludeXmlElementsByType"/>
    /// dictionary to merge discovered types into (Phase 37-01.1). The tree's "Scan for XML types"
    /// action sets this according to the mode subtree it was invoked from.
    /// </summary>
    public DeploymentMode Mode { get; set; } = DeploymentMode.Deploy;

    public override CommandResult Handle()
    {
        try
        {
            var configPath = ConfigPath ?? ConfigPathResolver.FindOrCreateConfigFile();
            var config = ConfigLoader.Load(configPath);

            var discovery = Discovery ?? new XmlTypeDiscovery(new DwSqlExecutor());
            var discoveredTypes = discovery.DiscoverXmlTypes();

            // Phase 37-01.1: merge into the per-mode ModeConfig. Leaves the other mode untouched.
            var modeConfig = config.GetMode(Mode);
            var updated = new Dictionary<string, List<string>>(modeConfig.ExcludeXmlElementsByType, StringComparer.OrdinalIgnoreCase);
            foreach (var typeName in discoveredTypes)
            {
                if (!updated.ContainsKey(typeName))
                    updated[typeName] = new List<string>();
            }

            var updatedMode = modeConfig with { ExcludeXmlElementsByType = updated };
            var newConfig = Mode == DeploymentMode.Deploy
                ? config with { Deploy = updatedMode }
                : config with { Seed = updatedMode };
            ConfigWriter.Save(newConfig, configPath);

            return new() { Status = CommandResult.ResultType.Ok };
        }
        catch (Exception ex)
        {
            return new() { Status = CommandResult.ResultType.Error, Message = ex.Message };
        }
    }
}
