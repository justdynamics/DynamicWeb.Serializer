using DynamicWeb.Serializer.AdminUI.Infrastructure;
using DynamicWeb.Serializer.Configuration;
using DynamicWeb.Serializer.Providers.SqlTable;
using Dynamicweb.CoreUI.Data;

namespace DynamicWeb.Serializer.AdminUI.Commands;

public sealed class ScanXmlTypesCommand : CommandBase<object>
{
    /// <summary>Optional test overrides</summary>
    public string? ConfigPath { get; set; }
    public XmlTypeDiscovery? Discovery { get; set; }

    public override CommandResult Handle()
    {
        try
        {
            var configPath = ConfigPath ?? ConfigPathResolver.FindOrCreateConfigFile();
            var config = ConfigLoader.Load(configPath);

            var discovery = Discovery ?? new XmlTypeDiscovery(new DwSqlExecutor());
            var discoveredTypes = discovery.DiscoverXmlTypes();

            // Merge: add new types with empty exclusion lists, preserve existing
            var updated = new Dictionary<string, List<string>>(config.ExcludeXmlElementsByType, StringComparer.OrdinalIgnoreCase);
            foreach (var typeName in discoveredTypes)
            {
                if (!updated.ContainsKey(typeName))
                    updated[typeName] = new List<string>();
            }

            var newConfig = config with { ExcludeXmlElementsByType = updated };
            ConfigWriter.Save(newConfig, configPath);

            return new() { Status = CommandResult.ResultType.Ok };
        }
        catch (Exception ex)
        {
            return new() { Status = CommandResult.ResultType.Error, Message = ex.Message };
        }
    }
}
