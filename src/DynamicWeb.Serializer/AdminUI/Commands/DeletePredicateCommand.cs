using DynamicWeb.Serializer.Configuration;
using Dynamicweb.CoreUI.Data;

namespace DynamicWeb.Serializer.AdminUI.Commands;

public sealed class DeletePredicateCommand : CommandBase
{
    public int Index { get; set; }

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
        if (Index < 0 || Index >= config.Predicates.Count)
            return new() { Status = CommandResult.ResultType.Error, Message = "Invalid predicate index" };

        var predicates = config.Predicates.ToList();
        predicates.RemoveAt(Index);

        var updated = config with { Predicates = predicates };
        ConfigWriter.Save(updated, configPath);

        return new() { Status = CommandResult.ResultType.Ok };
    }
}
