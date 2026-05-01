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
/// XmlTypeEditScreen.CreateElementSelector tests. CreateElementSelector now only builds Options
/// (the union of live + saved); Value is bound by EditScreenBase.BuildEditor from the
/// List&lt;string&gt; Model.ExcludedElements after GetEditor returns. The framework-binding tests
/// simulate that pipeline so the regression that the Phase 41 RED tests missed (Value being
/// overwritten by the framework after CreateElementSelector ran) cannot recur.
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

    /// <summary>
    /// Simulates EditScreenBase.BuildEditor: invoke GetEditor("ExcludedElements") then
    /// editor.SetValue(Model.ExcludedElements). This catches the failure mode the original
    /// reflection-only tests missed -- where editor.Value was being set inside
    /// CreateElementSelector and then overwritten by the framework's later SetValue call.
    /// </summary>
    private static SelectMultiDual InvokeFrameworkBindingFlow(XmlTypeEditScreen screen, XmlTypeEditModel model)
    {
        var getEditorMethod = typeof(XmlTypeEditScreen)
            .GetMethod("GetEditor", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var editor = (SelectMultiDual)getEditorMethod.Invoke(screen,
            new object[] { nameof(XmlTypeEditModel.ExcludedElements) })!;
        editor.Value = model.ExcludedElements;
        return editor;
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
            ExcludedElements = new List<string> { "elemA", "elemB", "elemC" }
        });

        var editor = InvokeCreateElementSelector(screen);

        Assert.NotNull(editor.Options);
        Assert.Equal(3, editor.Options!.Count);
        Assert.Contains(editor.Options, o => o.Value as string == "elemA");
        Assert.Contains(editor.Options, o => o.Value as string == "elemB");
        Assert.Contains(editor.Options, o => o.Value as string == "elemC");
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
            ExcludedElements = new List<string> { "elemA", "elemC" }
        });

        var editor = InvokeCreateElementSelector(screen);

        Assert.NotNull(editor.Options);
        Assert.Contains(editor.Options!, o => string.Equals(o.Value as string, "elemA", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(editor.Options!, o => string.Equals(o.Value as string, "elemB", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(editor.Options!, o => string.Equals(o.Value as string, "elemC", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(3, editor.Options!.Count);
    }

    [Fact]
    public void CreateElementSelector_NoSaved_OptionsFromDiscoveryOnly()
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
            ExcludedElements = new List<string>()
        });

        var editor = InvokeCreateElementSelector(screen);

        // Discovery returns empty AND no saved -> Options stay empty.
        Assert.True(editor.Options is null || editor.Options.Count == 0);
    }

    [Fact]
    public void FrameworkBinding_SavedExclusions_RenderAsSelected()
    {
        // This is the regression-locking test: simulates EditScreenBase.BuildEditor's full flow
        // (GetEditor + editor.SetValue(rawValue)) and asserts the saved exclusions land on the
        // Selected side as a List<string>. The original RED tests bypassed this binding step,
        // which is why they passed while the live UI showed an empty Selected panel.
        var executor = new FakeSqlExecutor();
        executor.AddMapping("PageUrlDataProviderParameters",
            TestTableHelper.CreateSingleColumnTable("PageUrlDataProviderParameters"));
        executor.AddMapping("ParagraphModuleSettings",
            TestTableHelper.CreateSingleColumnTable("ParagraphModuleSettings"));
        var discovery = new XmlTypeDiscovery(executor);

        var saved = new List<string> { "elemA", "elemB", "elemC" };
        var model = new XmlTypeEditModel { TypeName = "eCom_CartV2", ExcludedElements = saved };
        var screen = new XmlTypeEditScreen { Discovery = discovery };
        SetModel(screen, model);

        var editor = InvokeFrameworkBindingFlow(screen, model);

        Assert.NotNull(editor.Value);
        var bound = Assert.IsAssignableFrom<List<string>>(editor.Value);
        Assert.Equal(saved, bound);

        // Options must include the saved values (Available pool unchanged guarantee).
        Assert.NotNull(editor.Options);
        Assert.Equal(3, editor.Options!.Count);
        Assert.Contains(editor.Options, o => o.Value as string == "elemA");
        Assert.Contains(editor.Options, o => o.Value as string == "elemB");
        Assert.Contains(editor.Options, o => o.Value as string == "elemC");
    }
}
