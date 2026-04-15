using DynamicWeb.Serializer.AdminUI.Models;
using DynamicWeb.Serializer.Configuration;
using Dynamicweb.Content.Items;
using Dynamicweb.CoreUI.Data;
using Dynamicweb.CoreUI.Lists;

namespace DynamicWeb.Serializer.AdminUI.Queries;

public sealed class ItemTypeListQuery : DataQueryModelBase<DataListViewModel<ItemTypeListModel>>
{
    public override DataListViewModel<ItemTypeListModel>? GetModel()
    {
        var metadata = ItemManager.Metadata.GetMetadata();
        if (metadata?.Items == null || metadata.Items.Count == 0)
            return new DataListViewModel<ItemTypeListModel>();

        // Load config for excluded field counts (may not exist)
        Dictionary<string, List<string>>? excludeMap = null;
        var configPath = ConfigPathResolver.FindConfigFile();
        if (configPath != null)
        {
            var config = ConfigLoader.Load(configPath);
            // Rebuild as case-insensitive for reliable lookups
            excludeMap = new Dictionary<string, List<string>>(
                config.ExcludeFieldsByItemType,
                StringComparer.OrdinalIgnoreCase);
        }

        var items = metadata.Items
            .Select(t => new ItemTypeListModel
            {
                SystemName = t.SystemName,
                DisplayName = t.Name,
                Category = t.Category?.FullName ?? "",
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
