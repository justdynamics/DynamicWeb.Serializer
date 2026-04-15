using Dynamicweb.Content;
using DynamicWeb.Serializer.AdminUI.Models;
using DynamicWeb.Serializer.Configuration;
using DynamicWeb.Serializer.Models;
using Dynamicweb.CoreUI.Data;

namespace DynamicWeb.Serializer.AdminUI.Commands;

public sealed class SavePredicateCommand : CommandBase<PredicateEditModel>
{
    /// <summary>
    /// Optional override for testing — bypasses ConfigPathResolver.
    /// </summary>
    public string? ConfigPath { get; set; }

    public override CommandResult Handle()
    {
        if (Model is null)
            return new() { Status = CommandResult.ResultType.Invalid, Message = "Model data must be given" };

        if (string.IsNullOrWhiteSpace(Model.Name))
            return new() { Status = CommandResult.ResultType.Invalid, Message = "Name is required" };

        try
        {
            var configPath = ConfigPath ?? ConfigPathResolver.FindOrCreateConfigFile();
            var config = ConfigLoader.Load(configPath);
            var predicates = config.Predicates.ToList();

            // D-02: ProviderType locked after creation — use existing type on update
            string providerType;
            if (Model.Index >= 0 && Model.Index < predicates.Count)
            {
                providerType = predicates[Model.Index].ProviderType;
            }
            else
            {
                providerType = Model.ProviderType;
            }

            if (string.IsNullOrWhiteSpace(providerType))
                return new() { Status = CommandResult.ResultType.Invalid, Message = "Provider Type is required" };

            // Provider-branched validation
            if (providerType == "Content")
            {
                if (Model.AreaId <= 0)
                    return new() { Status = CommandResult.ResultType.Invalid, Message = "Area is required for Content predicates" };
                if (Model.PageId <= 0)
                    return new() { Status = CommandResult.ResultType.Invalid, Message = "Page is required for Content predicates" };
            }
            else if (providerType == "SqlTable")
            {
                if (string.IsNullOrWhiteSpace(Model.Table))
                    return new() { Status = CommandResult.ResultType.Invalid, Message = "Table is required for SqlTable predicates" };
            }
            else
            {
                return new() { Status = CommandResult.ResultType.Invalid, Message = $"Unknown provider type: {providerType}" };
            }

            // Validate unique name (per D-11), excluding current index on edit
            var duplicateIndex = predicates.FindIndex(p =>
                string.Equals(p.Name, Model.Name, StringComparison.OrdinalIgnoreCase));
            if (duplicateIndex >= 0 && duplicateIndex != Model.Index)
                return new() { Status = CommandResult.ResultType.Invalid, Message = $"A predicate with the name '{Model.Name}' already exists (duplicate)" };

            // Build predicate based on provider type
            ProviderPredicateDefinition predicate;

            if (providerType == "Content")
            {
                // Resolve page path from PageId via DW Services when available
                string path;
                try
                {
                    var page = Services.Pages?.GetPage(Model.PageId);
                    path = page?.GetBreadcrumbPath()
                        ?? (Model.Index >= 0 && Model.Index < predicates.Count
                            ? predicates[Model.Index].Path
                            : $"/page-{Model.PageId}");
                }
                catch
                {
                    // DW runtime not available (e.g., unit tests) — use fallback path
                    path = Model.Index >= 0 && Model.Index < predicates.Count
                        ? predicates[Model.Index].Path
                        : $"/page-{Model.PageId}";
                }

                var excludeAreaColumns = (Model.ExcludeAreaColumns ?? string.Empty)
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(e => e.Trim())
                    .Where(e => e.Length > 0)
                    .ToList();

                // Split excludes: handle \r\n and \n, trim, remove empties
                var excludes = (Model.Excludes ?? string.Empty)
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(e => e.Trim())
                    .Where(e => e.Length > 0)
                    .ToList();

                predicate = new ProviderPredicateDefinition
                {
                    Name = Model.Name.Trim(),
                    ProviderType = "Content",
                    Path = path,
                    AreaId = Model.AreaId,
                    PageId = Model.PageId,
                    Excludes = excludes,
                    ExcludeAreaColumns = excludeAreaColumns
                };
            }
            else // SqlTable
            {
                var serviceCaches = (Model.ServiceCaches ?? string.Empty)
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => s.Length > 0)
                    .ToList();

                predicate = new ProviderPredicateDefinition
                {
                    Name = Model.Name.Trim(),
                    ProviderType = "SqlTable",
                    Table = Model.Table?.Trim(),
                    NameColumn = string.IsNullOrWhiteSpace(Model.NameColumn) ? null : Model.NameColumn.Trim(),
                    CompareColumns = string.IsNullOrWhiteSpace(Model.CompareColumns) ? null : Model.CompareColumns.Trim(),
                    ServiceCaches = serviceCaches
                };
            }

            if (Model.Index < 0)
            {
                // New predicate
                predicates.Add(predicate);
            }
            else if (Model.Index < predicates.Count)
            {
                // Update existing
                predicates[Model.Index] = predicate;
            }
            else
            {
                return new() { Status = CommandResult.ResultType.Error, Message = "Invalid predicate index" };
            }

            var updated = config with { Predicates = predicates };
            ConfigWriter.Save(updated, configPath);

            return new() { Status = CommandResult.ResultType.Ok, Model = Model };
        }
        catch (Exception ex)
        {
            return new() { Status = CommandResult.ResultType.Error, Message = ex.Message };
        }
    }
}
