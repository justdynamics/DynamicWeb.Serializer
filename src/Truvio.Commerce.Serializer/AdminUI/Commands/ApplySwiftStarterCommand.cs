using System.Reflection;
using System.Text.Json.Nodes;
using Truvio.Commerce.Serializer.AdminUI.Models;
using Truvio.Commerce.Serializer.Configuration;
using Dynamicweb.CoreUI.Data;

namespace Truvio.Commerce.Serializer.AdminUI.Commands;

/// <summary>
/// "Get started" path: writes the embedded Swift starter configuration, rebinding its
/// Content predicates to the chosen website. Refuses to touch a configuration that
/// already has predicates — getting started never overwrites a real setup.
/// </summary>
public sealed class ApplySwiftStarterCommand : CommandBase<StarterConfigModel>
{
    /// <summary>Optional test override; production resolves via ConfigPathResolver.</summary>
    public string? ConfigPath { get; set; }

    public override CommandResult Handle()
    {
        if (Model is null || Model.AreaId <= 0)
            return new() { Status = CommandResult.ResultType.Invalid, Message = "Select the website the starter's content predicates should target." };

        try
        {
            var targetPath = ConfigPath ?? ConfigPathResolver.FindConfigFile() ?? ConfigPathResolver.DefaultPath;

            if (File.Exists(targetPath))
            {
                var existing = ConfigLoader.Load(targetPath);
                if (existing.Predicates.Count > 0)
                    return new()
                    {
                        Status = CommandResult.ResultType.Invalid,
                        Message = $"The configuration already has {existing.Predicates.Count} predicate(s) — the starter is only applied to an empty configuration."
                    };
            }

            var starterJson = ReadEmbeddedStarter();
            var root = JsonNode.Parse(starterJson)
                ?? throw new InvalidOperationException("Embedded starter configuration could not be parsed.");

            if (root["predicates"] is JsonArray predicates)
            {
                foreach (var predicate in predicates.OfType<JsonObject>())
                {
                    var providerType = predicate["providerType"]?.GetValue<string>();
                    var isContent = string.IsNullOrEmpty(providerType)
                        || string.Equals(providerType, "Content", StringComparison.OrdinalIgnoreCase);
                    if (isContent)
                        predicate["areaId"] = Model.AreaId;
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            File.WriteAllText(targetPath, root.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

            // Fail fast if the produced config doesn't load — better now than on first serialize.
            ConfigLoader.Load(targetPath);

            return new()
            {
                Status = CommandResult.ResultType.Ok,
                Message = $"Swift starter applied for website {Model.AreaId}. Review the predicates, then run 'Preview deserialize (dry run)' before any real sync.",
                Model = Model
            };
        }
        catch (Exception ex)
        {
            return new() { Status = CommandResult.ResultType.Error, Message = $"Could not apply the starter configuration: {ex.Message}" };
        }
    }

    internal static string ReadEmbeddedStarter()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("swift-starter.json", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Embedded resource swift-starter.json not found in the assembly.");
        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

/// <summary>"Get started" fallback: creates an empty configuration (no predicates) at the
/// canonical location so the sub-nodes (Predicates, excludes) become editable.</summary>
public sealed class CreateEmptyConfigCommand : CommandBase
{
    public override CommandResult Handle()
    {
        try
        {
            var path = ConfigPathResolver.FindOrCreateConfigFile();
            return new()
            {
                Status = CommandResult.ResultType.Ok,
                Message = $"Configuration created at {path}. Add predicates via the Predicates sub-node — nothing syncs until you do."
            };
        }
        catch (Exception ex)
        {
            return new() { Status = CommandResult.ResultType.Error, Message = $"Could not create the configuration: {ex.Message}" };
        }
    }
}
