using DynamicWeb.Serializer.AdminUI.Models;
using DynamicWeb.Serializer.Configuration;
using Dynamicweb.Content.Items;
using Dynamicweb.Content.Items.Metadata;
using Dynamicweb.CoreUI.Data;

namespace DynamicWeb.Serializer.AdminUI.Queries;

public sealed class ItemTypeBySystemNameQuery : DataQueryIdentifiableModelBase<ItemTypeEditModel, string>
{
    public string SystemName { get; set; } = string.Empty;

    protected override void SetKey(string key)
    {
        SystemName = key;
    }

    public override ItemTypeEditModel? GetModel()
    {
        if (string.IsNullOrWhiteSpace(SystemName))
            return null;

        ItemType itemType;
        FieldMetadataCollection allFields;
        try
        {
            itemType = ItemManager.Metadata.GetItemType(SystemName);
            if (itemType == null)
                return null;

            // Use GetItemFields to include inherited fields (not itemType.Fields directly)
            allFields = ItemManager.Metadata.GetItemFields(itemType);
        }
        catch
        {
            return null;
        }

        // Load config for existing exclusions
        var excludedFieldsList = new List<string>();
        try
        {
            var configPath = ConfigPathResolver.FindConfigFile();
            if (configPath != null)
            {
                var config = ConfigLoader.Load(configPath);
                // Case-insensitive lookup for existing exclusions
                var match = config.ExcludeFieldsByItemType
                    .FirstOrDefault(kvp => string.Equals(kvp.Key, SystemName, StringComparison.OrdinalIgnoreCase));
                if (match.Value != null)
                    excludedFieldsList = match.Value;
            }
        }
        catch
        {
            // Config not available -- proceed with empty exclusions
        }

        return new ItemTypeEditModel
        {
            SystemName = itemType.SystemName,
            DisplayName = itemType.Name,
            Category = itemType.Category?.FullName ?? "",
            FieldCount = allFields.Count,
            ExcludedFields = string.Join("\n", excludedFieldsList)
        };
    }
}
