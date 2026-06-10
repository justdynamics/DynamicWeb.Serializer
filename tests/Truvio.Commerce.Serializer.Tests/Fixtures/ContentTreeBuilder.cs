using Truvio.Commerce.Serializer.Models;

namespace Truvio.Commerce.Serializer.Tests.Fixtures;

public static class ContentTreeBuilder
{
    public static SerializedArea BuildSampleTree()
    {
        var page1 = BuildSinglePage("Customer Center") with
        {
            SortOrder = 1,
            IsActive = true,
            Fields = new Dictionary<string, object>
            {
                ["title"] = "Customer Center",
                ["body"] = "<p>Test &amp; value</p>",
                ["tilde"] = "~",
                ["crlf"] = "Line1\r\nLine2"
            },
            GridRows = new List<SerializedGridRow>
            {
                new SerializedGridRow
                {
                    Id = Guid.NewGuid(),
                    SortOrder = 1,
                    Columns = new List<SerializedGridColumn>
                    {
                        new SerializedGridColumn
                        {
                            Id = 1,
                            Width = 12,
                            Paragraphs = new List<SerializedParagraph>
                            {
                                new SerializedParagraph
                                {
                                    ParagraphUniqueId = Guid.NewGuid(),
                                    SortOrder = 1,
                                    ItemType = "ContentModule",
                                    Header = "Welcome",
                                    Fields = new Dictionary<string, object>
                                    {
                                        ["text"] = "Hello World",
                                        ["html"] = "<h1>Welcome</h1>"
                                    }
                                },
                                new SerializedParagraph
                                {
                                    ParagraphUniqueId = Guid.NewGuid(),
                                    SortOrder = 2,
                                    ItemType = "ImageModule",
                                    Fields = new Dictionary<string, object>
                                    {
                                        ["imageUrl"] = "/images/hero.jpg"
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        var page2 = BuildSinglePage("About Us") with
        {
            SortOrder = 2,
            IsActive = true,
            Fields = new Dictionary<string, object>
            {
                ["title"] = "About Us",
                ["body"] = "Plain text content"
            },
            GridRows = new List<SerializedGridRow>
            {
                new SerializedGridRow
                {
                    Id = Guid.NewGuid(),
                    SortOrder = 1,
                    Columns = new List<SerializedGridColumn>
                    {
                        new SerializedGridColumn
                        {
                            Id = 1,
                            Width = 12,
                            Paragraphs = new List<SerializedParagraph>
                            {
                                new SerializedParagraph
                                {
                                    ParagraphUniqueId = Guid.NewGuid(),
                                    SortOrder = 1,
                                    ItemType = "ContentModule",
                                    Header = "About",
                                    Fields = new Dictionary<string, object>
                                    {
                                        ["text"] = "About Us content"
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        return new SerializedArea
        {
            AreaId = Guid.NewGuid(),
            Name = "Main Website",
            SortOrder = 1,
            Pages = new List<SerializedPage> { page1, page2 }
        };
    }

    public static SerializedPage BuildSinglePage(string name, Guid? guid = null)
    {
        return new SerializedPage
        {
            PageUniqueId = guid ?? Guid.NewGuid(),
            Name = name,
            MenuText = name,
            UrlName = name.ToLowerInvariant().Replace(" ", "-"),
            SortOrder = 1
        };
    }

    public static SerializedPage BuildSinglePageWithSourceId(string name, int sourcePageId, Guid? guid = null)
    {
        return BuildSinglePage(name, guid) with { SourcePageId = sourcePageId };
    }

    /// <summary>
    /// Builds a single page with 2 sample permissions for testing permission serialization.
    /// Anonymous with "none" level and AuthenticatedFrontend with "read" level.
    /// </summary>
    public static SerializedPage BuildSinglePageWithPermissions(string name, Guid? guid = null)
    {
        return BuildSinglePage(name, guid) with
        {
            Permissions = new List<SerializedPermission>
            {
                new SerializedPermission
                {
                    Owner = "Anonymous",
                    OwnerType = "role",
                    OwnerId = null,
                    Level = "none",
                    LevelValue = 1
                },
                new SerializedPermission
                {
                    Owner = "AuthenticatedFrontend",
                    OwnerType = "role",
                    OwnerId = null,
                    Level = "read",
                    LevelValue = 4
                }
            }
        };
    }

    /// <summary>
    /// Builds a multi-column tree with 2 columns for testing column round-trip.
    /// Area "Test Area"
    ///   Page "Multi-Column Page" (1 grid row, 2 columns)
    ///     Column 1 (Width=6): 2 paragraphs (SortOrder 1,2)
    ///     Column 2 (Width=6): 1 paragraph (SortOrder 1)
    /// </summary>
    public static SerializedArea BuildMultiColumnTree()
    {
        var page = BuildSinglePage("Multi-Column Page") with
        {
            SortOrder = 1,
            IsActive = true,
            GridRows = new List<SerializedGridRow>
            {
                new SerializedGridRow
                {
                    Id = Guid.NewGuid(),
                    SortOrder = 1,
                    Columns = new List<SerializedGridColumn>
                    {
                        new SerializedGridColumn
                        {
                            Id = 1,
                            Width = 6,
                            Paragraphs = new List<SerializedParagraph>
                            {
                                new SerializedParagraph
                                {
                                    ParagraphUniqueId = Guid.NewGuid(),
                                    SortOrder = 1,
                                    ColumnId = 1,
                                    ItemType = "ContentModule",
                                    Fields = new Dictionary<string, object> { ["text"] = "Col1 Para1" }
                                },
                                new SerializedParagraph
                                {
                                    ParagraphUniqueId = Guid.NewGuid(),
                                    SortOrder = 2,
                                    ColumnId = 1,
                                    ItemType = "ContentModule",
                                    Fields = new Dictionary<string, object> { ["text"] = "Col1 Para2" }
                                }
                            }
                        },
                        new SerializedGridColumn
                        {
                            Id = 2,
                            Width = 6,
                            Paragraphs = new List<SerializedParagraph>
                            {
                                new SerializedParagraph
                                {
                                    ParagraphUniqueId = Guid.NewGuid(),
                                    SortOrder = 1,
                                    ColumnId = 2,
                                    ItemType = "ImageModule",
                                    Fields = new Dictionary<string, object> { ["imageUrl"] = "/images/col2.jpg" }
                                }
                            }
                        }
                    }
                }
            }
        };

        return new SerializedArea
        {
            AreaId = Guid.NewGuid(),
            Name = "Test Area",
            SortOrder = 1,
            Pages = new List<SerializedPage> { page }
        };
    }

    /// <summary>
    /// Builds a page with every new property populated for round-trip testing.
    /// </summary>
    public static SerializedPage BuildPageWithAllProperties(string name = "Full Properties Page", Guid? guid = null)
    {
        return BuildSinglePage(name, guid) with
        {
            NavigationTag = "main-nav",
            ShortCut = "Default.aspx?ID=42",
            Hidden = true,
            Allowclick = false,
            Allowsearch = false,
            ShowInSitemap = false,
            ShowInLegend = false,
            SslMode = 2,
            ColorSchemeId = "dark-theme",
            ExactUrl = "/custom-url",
            ContentType = "text/html",
            TopImage = "/images/banner.jpg",
            DisplayMode = "List",
            ActiveFrom = new DateTime(2025, 1, 15, 10, 0, 0, DateTimeKind.Utc),
            ActiveTo = new DateTime(2025, 12, 31, 23, 59, 59, DateTimeKind.Utc),
            PermissionType = 3,
            Seo = new SerializedSeoSettings
            {
                MetaTitle = "Test Meta Title",
                MetaCanonical = "https://example.com/page",
                Description = "A test description",
                Keywords = "test, keywords",
                Noindex = true,
                Nofollow = true,
                Robots404 = false
            },
            UrlSettings = new SerializedUrlSettings
            {
                UrlDataProviderTypeName = "CustomProvider",
                UrlDataProviderParameters = "param1=val1",
                UrlIgnoreForChildren = true,
                UrlUseAsWritten = true
            },
            Visibility = new SerializedVisibilitySettings
            {
                HideForPhones = true,
                HideForTablets = false,
                HideForDesktops = false
            },
            NavigationSettings = new SerializedNavigationSettings
            {
                UseEcomGroups = true,
                ParentType = "Groups",
                Groups = "GROUP1,GROUP2",
                ShopID = "SHOP1",
                MaxLevels = 3,
                ProductPage = "Default.aspx?Id=42",
                NavigationProvider = "EcomNavProvider",
                IncludeProducts = true
            }
        };
    }

    /// <summary>
    /// Builds a 3-level nested hierarchy:
    /// Area "Test Website"
    ///   Page "Parent" (SortOrder=1)
    ///     Page "Child A" (SortOrder=1) — 1 grid row, 1 paragraph
    ///     Page "Child B" (SortOrder=2)
    ///       Page "Grandchild" (SortOrder=1)
    ///   Page "Sibling" (SortOrder=2)
    /// </summary>
    public static SerializedArea BuildNestedTree()
    {
        var grandchild = BuildSinglePage("Grandchild") with
        {
            SortOrder = 1,
            Fields = new Dictionary<string, object> { ["title"] = "Grandchild Page" },
            GridRows = new List<SerializedGridRow>
            {
                new SerializedGridRow
                {
                    Id = Guid.NewGuid(),
                    SortOrder = 1,
                    Columns = new List<SerializedGridColumn>
                    {
                        new SerializedGridColumn
                        {
                            Id = 1,
                            Width = 12,
                            Paragraphs = new List<SerializedParagraph>
                            {
                                new SerializedParagraph
                                {
                                    ParagraphUniqueId = Guid.NewGuid(),
                                    SortOrder = 1,
                                    ItemType = "ContentModule",
                                    Header = "Grandchild",
                                    Fields = new Dictionary<string, object> { ["text"] = "Grandchild content" }
                                }
                            }
                        }
                    }
                }
            }
        };

        var childA = BuildSinglePage("Child A") with
        {
            SortOrder = 1,
            Fields = new Dictionary<string, object> { ["title"] = "Child A Page" },
            GridRows = new List<SerializedGridRow>
            {
                new SerializedGridRow
                {
                    Id = Guid.NewGuid(),
                    SortOrder = 1,
                    Columns = new List<SerializedGridColumn>
                    {
                        new SerializedGridColumn
                        {
                            Id = 1,
                            Width = 12,
                            Paragraphs = new List<SerializedParagraph>
                            {
                                new SerializedParagraph
                                {
                                    ParagraphUniqueId = Guid.NewGuid(),
                                    SortOrder = 1,
                                    ItemType = "ContentModule",
                                    Header = "Child A",
                                    Fields = new Dictionary<string, object> { ["text"] = "Child A content" }
                                }
                            }
                        }
                    }
                }
            }
        };

        var childB = BuildSinglePage("Child B") with
        {
            SortOrder = 2,
            Fields = new Dictionary<string, object> { ["title"] = "Child B Page" },
            GridRows = new List<SerializedGridRow>
            {
                new SerializedGridRow
                {
                    Id = Guid.NewGuid(),
                    SortOrder = 1,
                    Columns = new List<SerializedGridColumn>
                    {
                        new SerializedGridColumn
                        {
                            Id = 1,
                            Width = 12,
                            Paragraphs = new List<SerializedParagraph>
                            {
                                new SerializedParagraph
                                {
                                    ParagraphUniqueId = Guid.NewGuid(),
                                    SortOrder = 1,
                                    ItemType = "ContentModule",
                                    Header = "Child B",
                                    Fields = new Dictionary<string, object> { ["text"] = "Child B content" }
                                }
                            }
                        }
                    }
                }
            },
            Children = new List<SerializedPage> { grandchild }
        };

        var parent = BuildSinglePage("Parent") with
        {
            SortOrder = 1,
            Fields = new Dictionary<string, object> { ["title"] = "Parent Page" },
            GridRows = new List<SerializedGridRow>
            {
                new SerializedGridRow
                {
                    Id = Guid.NewGuid(),
                    SortOrder = 1,
                    Columns = new List<SerializedGridColumn>
                    {
                        new SerializedGridColumn
                        {
                            Id = 1,
                            Width = 12,
                            Paragraphs = new List<SerializedParagraph>
                            {
                                new SerializedParagraph
                                {
                                    ParagraphUniqueId = Guid.NewGuid(),
                                    SortOrder = 1,
                                    ItemType = "ContentModule",
                                    Header = "Parent",
                                    Fields = new Dictionary<string, object> { ["text"] = "Parent content" }
                                }
                            }
                        }
                    }
                }
            },
            Children = new List<SerializedPage> { childA, childB }
        };

        var sibling = BuildSinglePage("Sibling") with
        {
            SortOrder = 2,
            Fields = new Dictionary<string, object> { ["title"] = "Sibling Page" },
            GridRows = new List<SerializedGridRow>
            {
                new SerializedGridRow
                {
                    Id = Guid.NewGuid(),
                    SortOrder = 1,
                    Columns = new List<SerializedGridColumn>
                    {
                        new SerializedGridColumn
                        {
                            Id = 1,
                            Width = 12,
                            Paragraphs = new List<SerializedParagraph>
                            {
                                new SerializedParagraph
                                {
                                    ParagraphUniqueId = Guid.NewGuid(),
                                    SortOrder = 1,
                                    ItemType = "ContentModule",
                                    Header = "Sibling",
                                    Fields = new Dictionary<string, object> { ["text"] = "Sibling content" }
                                }
                            }
                        }
                    }
                }
            }
        };

        return new SerializedArea
        {
            AreaId = Guid.NewGuid(),
            Name = "Test Website",
            SortOrder = 1,
            Pages = new List<SerializedPage> { parent, sibling }
        };
    }
}
