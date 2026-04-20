using DynamicWeb.Serializer.AdminUI.Models;
using DynamicWeb.Serializer.Configuration;
using Dynamicweb.Content.Items;
using Dynamicweb.Content.Items.Metadata;
using Dynamicweb.CoreUI.Data;

namespace DynamicWeb.Serializer.AdminUI.Queries;

public sealed class ItemTypeBySystemNameQuery : DataQueryIdentifiableModelBase<ItemTypeEditModel, string>
{
    public string SystemName { get; set; } = string.Empty;

    /// <summary>Optional config path override for tests -- bypasses ConfigPathResolver.</summary>
    public string? ConfigPath { get; set; }

    /// <summary>
    /// Which <see cref="DeploymentMode"/> subtree the edit screen was opened under (Phase 37-01.1).
    /// Determines which <see cref="ModeConfig.ExcludeFieldsByItemType"/> dictionary is read to
    /// pre-fill the ExcludedFields editor. Flows into <see cref="ItemTypeEditModel.Mode"/>.
    /// </summary>
    public DeploymentMode Mode { get; set; } = DeploymentMode.Deploy;

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

        // Phase 37-01.1: read exclusions from the mode's ModeConfig dictionary.
        var excludedFieldsList = new List<string>();
        try
        {
            var configPath = ConfigPath ?? ConfigPathResolver.FindConfigFile();
            if (configPath != null)
            {
                var config = ConfigLoader.Load(configPath);
                var modeConfig = config.GetMode(Mode);
                // Case-insensitive lookup for existing exclusions
                var match = modeConfig.ExcludeFieldsByItemType
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
            Mode = Mode,
            DisplayName = itemType.Name,
            Category = itemType.Category?.FullName ?? "",
            FieldCount = allFields.Count,
            ExcludedFields = string.Join("\n", excludedFieldsList)
        };
    }
}
