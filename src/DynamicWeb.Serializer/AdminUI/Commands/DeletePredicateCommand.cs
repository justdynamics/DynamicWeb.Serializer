using DynamicWeb.Serializer.Configuration;
using Dynamicweb.CoreUI.Data;

namespace DynamicWeb.Serializer.AdminUI.Commands;

public sealed class DeletePredicateCommand : CommandBase
{
    public int Index { get; set; }

    /// <summary>
    /// Which <see cref="DeploymentMode"/> the predicate belongs to (Phase 37-01 D-02). Defaults
    /// to Deploy so non-tree invocations keep the legacy Deploy-only behaviour.
    /// </summary>
    public DeploymentMode Mode { get; set; } = DeploymentMode.Deploy;

    /// <summary>
    /// Optional override for testing — bypasses ConfigPathResolver.
    /// </summary>
    public string? ConfigPath { get; set; }

    public override CommandResult Handle()
    {
        var configPath = ConfigPath ?? ConfigPathResolver.FindConfigFile();
        if (configPath == null)
            return new() { Status = CommandResult.ResultType.Error, Message = "Config file not found" };

        var config = ConfigLoader.Load(configPath);
        var modeConfig = config.GetMode(Mode);
        if (Index < 0 || Index >= modeConfig.Predicates.Count)
            return new() { Status = CommandResult.ResultType.Error, Message = "Invalid predicate index" };

        var predicates = modeConfig.Predicates.ToList();
        predicates.RemoveAt(Index);

        var updatedMode = modeConfig with { Predicates = predicates };
        var updated = Mode == DeploymentMode.Deploy
            ? config with { Deploy = updatedMode }
            : config with { Seed = updatedMode };
        ConfigWriter.Save(updated, configPath);

        return new() { Status = CommandResult.ResultType.Ok };
    }
}
