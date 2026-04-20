using DynamicWeb.Serializer.AdminUI.Models;
using DynamicWeb.Serializer.Configuration;
using Dynamicweb.Content.Items;
using Dynamicweb.Content.Items.Metadata;
using Dynamicweb.CoreUI.Data;
using Dynamicweb.CoreUI.Lists;

namespace DynamicWeb.Serializer.AdminUI.Queries;

public sealed class ItemTypeListQuery : DataQueryModelBase<DataListViewModel<ItemTypeListModel>>
{
    /// <summary>Optional config path override for tests -- bypasses ConfigPathResolver.</summary>
    public string? ConfigPath { get; set; }

    /// <summary>
    /// Which <see cref="DeploymentMode"/>'s excluded-field counts are shown on the list (Phase 37-01.1).
    /// Set by the tree when the user opens "Deploy / Item Types" vs "Seed / Item Types".
    /// </summary>
    public DeploymentMode Mode { get; set; } = DeploymentMode.Deploy;

    public override DataListViewModel<ItemTypeListModel>? GetModel()
    {
        var metadata = ItemManager.Metadata.GetMetadata();
        if (metadata?.Items == null || metadata.Items.Count == 0)
            return new DataListViewModel<ItemTypeListModel>();

        // Load config for excluded field counts (may not exist)
        Dictionary<string, List<string>>? excludeMap = null;
        var configPath = ConfigPath ?? ConfigPathResolver.FindConfigFile();
        if (configPath != null)
        {
            try
            {
                var config = ConfigLoader.Load(configPath);
                // Phase 37-01.1: read from the mode-scoped ModeConfig; rebuild case-insensitive.
                excludeMap = new Dictionary<string, List<string>>(
                    config.GetMode(Mode).ExcludeFieldsByItemType,
                    StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                // Corrupt config -- show list without exclusion counts
            }
        }

        var items = metadata.Items
            .Select(t => new ItemTypeListModel
            {
                SystemName = t.SystemName,
                DisplayName = t.Name,
                Category = t.Category?.FullName ?? "",
                // Fields declared directly on this type only (not inherited).
                // GetItemFields includes inherited fields and is too expensive per row.
                FieldCount = t.Fields?.Count ?? 0,
                ExcludedFieldCount = excludeMap != null
                    && excludeMap.TryGetValue(t.SystemName, out var excluded)
                    ? excluded.Count
                    : 0
            })
            .OrderBy(m => m.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(m => m.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new DataListViewModel<ItemTypeListModel>
        {
            Data = items,
            TotalCount = items.Count
        };
    }
}
