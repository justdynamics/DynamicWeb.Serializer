using DynamicWeb.Serializer.Configuration;
using Dynamicweb.CoreUI.Data;

namespace DynamicWeb.Serializer.AdminUI.Commands;

public sealed class DeletePredicateCommand : CommandBase
{
    public int Index { get; set; }
    public string? ConfigPath { get; set; }

    public override CommandResult Handle()
    {
        var configPath = ConfigPath ?? ConfigPathResolver.FindConfigFile();
        if (configPath == null)
            return new() { Status = CommandResult.ResultType.Error, Message = "Config file not found" };

        var config = ConfigLoader.Load(configPath);
        if (Index < 0 || Index >= config.Predicates.Count)
            return new() { Status = CommandResult.ResultType.Error, Message = "Invalid predicate index" };

        // Phase 40 D-01: delete from the single flat predicate list.
        var predicates = config.Predicates.ToList();
        predicates.RemoveAt(Index);

        var updated = config with { Predicates = predicates };
        ConfigWriter.Save(updated, configPath);

        return new() { Status = CommandResult.ResultType.Ok };
    }
}
