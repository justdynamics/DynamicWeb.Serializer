using Truvio.Commerce.Serializer.AdminUI.Models;
using Truvio.Commerce.Serializer.Configuration;
using Truvio.Commerce.Serializer.Models;
using Dynamicweb.CoreUI.Data;

namespace Truvio.Commerce.Serializer.AdminUI.Commands;

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

            var updatedConfig = existingConfig with
            {
                OutputDirectory = Model.OutputDirectory
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
