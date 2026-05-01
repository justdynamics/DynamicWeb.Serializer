using System.Reflection;
using DynamicWeb.Serializer.AdminUI.Infrastructure;
using DynamicWeb.Serializer.AdminUI.Models;
using DynamicWeb.Serializer.AdminUI.Screens;
using DynamicWeb.Serializer.Tests.TestHelpers;
using Dynamicweb.CoreUI.Editors.Lists;
using Dynamicweb.CoreUI.Screens;
using Xunit;

namespace DynamicWeb.Serializer.Tests.AdminUI;

/// <summary>
/// Phase 41 D-05 tests: dual-list editor must merge saved exclusions into discovered options
/// instead of short-circuiting when XmlTypeDiscovery returns 0 elements. Plan 41-03 added the
/// XmlTypeDiscovery DI seam (Discovery property) so tests can inject FakeSqlExecutor-backed
/// instances and exercise both the discovery-empty + saved fallback path and the
/// discovery+saved union path.
/// </summary>
public class XmlTypeEditScreenTests
{
    private static SelectMultiDual InvokeCreateElementSelector(XmlTypeEditScreen screen)
    {
        var method = typeof(XmlTypeEditScreen)
            .GetMethod("CreateElementSelector", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (SelectMultiDual)method.Invoke(screen, null)!;
    }

    private static void SetModel(XmlTypeEditScreen screen, XmlTypeEditModel model)
    {
        var prop = typeof(EditScreenBase<XmlTypeEditModel>)
            .GetProperty("Model", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
        prop.SetValue(screen, model);
    }

    [Fact]
    public void CreateElementSelector_DiscoveryEmpty_SavedNonEmpty_ShowsSavedAsOptions()
    {
        var executor = new FakeSqlExecutor();
        executor.AddMapping("PageUrlDataProviderParameters",
            TestTableHelper.CreateSingleColumnTable("PageUrlDataProviderParameters"));
        executor.AddMapping("ParagraphModuleSettings",
            TestTableHelper.CreateSingleColumnTable("ParagraphModuleSettings"));
        var discovery = new XmlTypeDiscovery(executor);

        var screen = new XmlTypeEditScreen { Discovery = discovery };
        SetModel(screen, new XmlTypeEditModel
        {
            TypeName = "eCom_CartV2",
            ExcludedElements = "elemA\nelemB\nelemC"
        });

        var editor = InvokeCreateElementSelector(screen);

        Assert.NotNull(editor.Options);
        Assert.Equal(3, editor.Options!.Count);
        Assert.Contains(editor.Options, o => o.Value as string == "elemA");
        Assert.Contains(editor.Options, o => o.Value as string == "elemB");
        Assert.Contains(editor.Options, o => o.Value as string == "elemC");
        Assert.NotNull(editor.Value);
        var selected = (string[])editor.Value!;
        Assert.Equal(3, selected.Length);
    }

    [Fact]
    public void CreateElementSelector_DiscoveryAndSavedOverlap_UnionInOptions()
    {
        var executor = new FakeSqlExecutor();
        executor.AddMapping("PageUrlDataProviderParameters",
            TestTableHelper.CreateSingleColumnTable("PageUrlDataProviderParameters",
                "<root><elemA>x</elemA><elemB>y</elemB></root>"));
        executor.AddMapping("ParagraphModuleSettings",
            TestTableHelper.CreateSingleColumnTable("ParagraphModuleSettings"));
        var discovery = new XmlTypeDiscovery(executor);

        var screen = new XmlTypeEditScreen { Discovery = discovery };
        SetModel(screen, new XmlTypeEditModel
        {
            TypeName = "TestType",
            ExcludedElements = "elemA\nelemC"
        });

        var editor = InvokeCreateElementSelector(screen);

        Assert.NotNull(editor.Options);
        Assert.Contains(editor.Options!, o => string.Equals(o.Value as string, "elemA", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(editor.Options!, o => string.Equals(o.Value as string, "elemB", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(editor.Options!, o => string.Equals(o.Value as string, "elemC", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(3, editor.Options!.Count);

        var value = (string[])editor.Value!;
        Assert.Equal(2, value.Length);
        Assert.Contains("elemA", value);
        Assert.Contains("elemC", value);
    }

    [Fact]
    public void CreateElementSelector_NoSaved_NoSelectionPreset()
    {
        var executor = new FakeSqlExecutor();
        executor.AddMapping("PageUrlDataProviderParameters",
            TestTableHelper.CreateSingleColumnTable("PageUrlDataProviderParameters"));
        executor.AddMapping("ParagraphModuleSettings",
            TestTableHelper.CreateSingleColumnTable("ParagraphModuleSettings"));
        var discovery = new XmlTypeDiscovery(executor);

        var screen = new XmlTypeEditScreen { Discovery = discovery };
        SetModel(screen, new XmlTypeEditModel
        {
            TypeName = "TestTypeWithNoSaved",
            ExcludedElements = string.Empty
        });

        var editor = InvokeCreateElementSelector(screen);

        Assert.Null(editor.Value);
    }
}
