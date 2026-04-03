using Dynamicweb.Content;
using DynamicWeb.Serializer.Models;

namespace DynamicWeb.Serializer.Serialization;

/// <summary>
/// Maps DynamicWeb content objects (Area, Page, GridRow, Paragraph) to the corresponding DTOs.
/// Pure conversion — no traversal, no I/O.
/// </summary>
public class ContentMapper
{
    private readonly ReferenceResolver _resolver;

    public ContentMapper(ReferenceResolver resolver)
    {
        _resolver = resolver;
    }

    /// <summary>
    /// Maps a DW Area to a SerializedArea DTO.
    /// </summary>
    public SerializedArea MapArea(Area area, List<SerializedPage> pages)
    {
        var itemFields = new Dictionary<string, object>();
        if (!string.IsNullOrEmpty(area.ItemType) && !string.IsNullOrEmpty(area.ItemId))
        {
            var itemEntry = Services.Items.GetItem(area.ItemType, area.ItemId);
            if (itemEntry != null)
            {
                var dict = new Dictionary<string, object?>();
                itemEntry.SerializeTo(dict);
                foreach (var kvp in dict)
                {
                    if (kvp.Value != null)
                        itemFields[kvp.Key] = kvp.Value;
                }
            }
        }

        return new SerializedArea
        {
            AreaId = area.UniqueId,
            Name = area.Name ?? string.Empty,
            SortOrder = area.Sort,
            ItemType = area.ItemType,
            ItemFields = itemFields,
            Pages = pages
        };
    }

    /// <summary>
    /// Maps a DW Page to a SerializedPage DTO.
    /// </summary>
    public SerializedPage MapPage(Page page, List<SerializedGridRow> gridRows, List<SerializedPage> children, List<SerializedPermission> permissions)
    {
        var fields = ExtractItemFields(page.Item);
        var propertyFields = ExtractPropertyItemFields(page);

        return new SerializedPage
        {
            PageUniqueId = page.UniqueId,
            SourcePageId = page.ID,
            // DW10 Page does not have a distinct Name property; MenuText is the navigation/display label.
            Name = page.MenuText ?? string.Empty,
            MenuText = page.MenuText ?? string.Empty,
            UrlName = page.UrlName ?? string.Empty,
            SortOrder = page.Sort,
            IsActive = page.Active,
            ItemType = page.ItemType,
            Layout = page.LayoutTemplate,
            LayoutApplyToSubPages = page.LayoutApplyToSubPages,
            IsFolder = page.IsFolder,
            TreeSection = page.TreeSection,
            NavigationTag = page.NavigationTag,
            ShortCut = page.ShortCut,
            Hidden = page.Hidden,
            Allowclick = page.Allowclick,
            Allowsearch = page.Allowsearch,
            ShowInSitemap = page.ShowInSitemap,
            ShowInLegend = page.ShowInLegend,
            SslMode = page.SslMode,
            ColorSchemeId = page.ColorSchemeId,
            ExactUrl = page.ExactUrl,
            ContentType = page.ContentType,
            TopImage = page.TopImage,
            DisplayMode = page.DisplayMode.ToString(),
            ActiveFrom = page.ActiveFrom,
            ActiveTo = page.ActiveTo,
            PermissionType = page.PermissionType,
            Seo = new SerializedSeoSettings
            {
                MetaTitle = page.MetaTitle,
                MetaCanonical = page.MetaCanonical,
                Description = page.Description,
                Keywords = page.Keywords,
                Noindex = page.Noindex,
                Nofollow = page.Nofollow,
                Robots404 = page.Robots404
            },
            UrlSettings = new SerializedUrlSettings
            {
                UrlDataProviderTypeName = page.UrlDataProviderTypeName,
                UrlDataProviderParameters = page.UrlDataProviderParameters,
                UrlIgnoreForChildren = page.UrlIgnoreForChildren,
                UrlUseAsWritten = page.UrlUseAsWritten
            },
            Visibility = new SerializedVisibilitySettings
            {
                HideForPhones = page.HideForPhones,
                HideForTablets = page.HideForTablets,
                HideForDesktops = page.HideForDesktops
            },
            NavigationSettings = MapNavigationSettings(page.NavigationSettings),
            Fields = fields,
            PropertyFields = propertyFields,
            Permissions = permissions,
            GridRows = gridRows,
            Children = children
        };
    }

    /// <summary>
    /// Maps a DW GridRow to a SerializedGridRow DTO.
    /// </summary>
    public SerializedGridRow MapGridRow(GridRow gridRow, List<SerializedGridColumn> columns)
    {
        var fields = new Dictionary<string, object>();
        if (!string.IsNullOrEmpty(gridRow.ItemType) && !string.IsNullOrEmpty(gridRow.ItemId))
        {
            var itemEntry = Services.Items.GetItem(gridRow.ItemType, gridRow.ItemId);
            if (itemEntry != null)
            {
                var dict = new Dictionary<string, object?>();
                itemEntry.SerializeTo(dict);
                foreach (var kvp in dict)
                {
                    if (kvp.Value != null)
                        fields[kvp.Key] = kvp.Value;
                }
            }
        }

        return new SerializedGridRow
        {
            Id = gridRow.UniqueId,
            SortOrder = gridRow.Sort,
            DefinitionId = gridRow.DefinitionId,
            ItemType = gridRow.ItemType,
            Container = gridRow.Container,
            ContainerWidth = gridRow.ContainerWidth,
            BackgroundImage = gridRow.BackgroundImage,
            ColorSchemeId = gridRow.ColorSchemeId,
            TopSpacing = gridRow.TopSpacing,
            BottomSpacing = gridRow.BottomSpacing,
            GapX = gridRow.GapX,
            GapY = gridRow.GapY,
            MobileLayout = gridRow.MobileLayout,
            VerticalAlignment = gridRow.VerticalAlignment.ToString(),
            FlexibleColumns = gridRow.FlexibleColumns,
            Fields = fields,
            Columns = columns
        };
    }

    /// <summary>
    /// Maps a DW Paragraph to a SerializedParagraph DTO.
    /// Registers the paragraph with the ReferenceResolver and resolves known reference fields to GUIDs.
    /// </summary>
    public SerializedParagraph MapParagraph(Paragraph paragraph)
    {
        // Register this paragraph so later cross-references to it can be resolved
        _resolver.RegisterParagraph(paragraph.ID, paragraph.UniqueId);

        var fields = ExtractItemFields(paragraph.Item);

        // Include paragraph body text if present
        if (!string.IsNullOrEmpty(paragraph.Text))
            fields["Text"] = paragraph.Text;

        // Resolve known numeric reference fields to GUIDs — do NOT serialize raw numeric IDs
        if (paragraph.MasterParagraphID > 0)
        {
            var guid = _resolver.ResolveParagraphGuid(paragraph.MasterParagraphID);
            if (guid.HasValue)
                fields["MasterParagraphGuid"] = guid.Value.ToString();
        }

        if (paragraph.GlobalRecordPageID > 0)
        {
            var guid = _resolver.ResolvePageGuid(paragraph.GlobalRecordPageID);
            if (guid.HasValue)
                fields["GlobalRecordPageGuid"] = guid.Value.ToString();
        }

        return new SerializedParagraph
        {
            ParagraphUniqueId = paragraph.UniqueId,
            SourceParagraphId = paragraph.ID,
            SortOrder = paragraph.Sort,
            ItemType = paragraph.ItemType,
            Header = paragraph.Header,
            Template = paragraph.Template,
            ColorSchemeId = paragraph.ColorSchemeId,
            ModuleSystemName = paragraph.ModuleSystemName,
            ModuleSettings = paragraph.ModuleSettings,
            Fields = fields
        };
    }

    /// <summary>
    /// Groups paragraphs by GridRowColumn to reconstruct column structure.
    /// Returns a single empty column if no paragraphs are provided.
    /// </summary>
    public List<SerializedGridColumn> BuildColumns(IEnumerable<Paragraph> paragraphs)
    {
        var paragraphList = paragraphs.ToList();

        if (paragraphList.Count == 0)
        {
            return new List<SerializedGridColumn>
            {
                new SerializedGridColumn { Id = 1, Width = 0 }
            };
        }

        var columns = paragraphList
            .GroupBy(p => p.GridRowColumn)
            .OrderBy(g => g.Key)
            .Select(g => new SerializedGridColumn
            {
                Id = g.Key,
                Width = 0, // Column width not available from Paragraph; GridRow definition has this
                Paragraphs = g.OrderBy(p => p.Sort)
                              .Select(p => MapParagraph(p) with { ColumnId = g.Key })
                              .ToList()
            })
            .ToList();

        return columns;
    }

    /// <summary>
    /// Maps DW PageNavigationSettings to DTO. Returns null when UseEcomGroups is false
    /// (DW only populates NavigationSettings when ecommerce navigation is enabled).
    /// </summary>
    private static SerializedNavigationSettings? MapNavigationSettings(
        Dynamicweb.Content.PageNavigationSettings? navSettings)
    {
        if (navSettings == null || !navSettings.UseEcomGroups)
            return null;

        return new SerializedNavigationSettings
        {
            UseEcomGroups = true,
            ParentType = navSettings.ParentType.ToString(),
            Groups = navSettings.Groups,
            ShopID = navSettings.ShopID,
            MaxLevels = navSettings.MaxLevels,
            ProductPage = navSettings.ProductPage,
            NavigationProvider = navSettings.NavigationProvider,
            IncludeProducts = navSettings.IncludeProducts
        };
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static Dictionary<string, object> ExtractItemFields(Dynamicweb.Content.Items.Item? item)
    {
        var fields = new Dictionary<string, object>();

        if (item == null)
            return fields;

        foreach (var fieldName in item.Names)
        {
            var value = item[fieldName];
            if (value != null)
                fields[fieldName] = value;
        }

        return fields;
    }

    /// <summary>
    /// Extracts PropertyItem fields (e.g. Icon, SubmenuType) from a page's PropertyItem.
    /// These are separate from the page's own Item fields.
    /// </summary>
    private static Dictionary<string, object> ExtractPropertyItemFields(Page page)
    {
        var fields = new Dictionary<string, object>();

        if (string.IsNullOrEmpty(page.PropertyItemId))
            return fields;

        var propItem = page.PropertyItem;
        if (propItem == null)
            return fields;

        var dict = new Dictionary<string, object?>();
        propItem.SerializeTo(dict);
        foreach (var kvp in dict)
        {
            if (kvp.Value != null)
                fields[kvp.Key] = kvp.Value;
        }

        return fields;
    }
}
