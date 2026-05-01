using System.Reflection;
using DynamicWeb.Serializer.AdminUI.Models;
using DynamicWeb.Serializer.AdminUI.Screens;
using Dynamicweb.CoreUI.Editors.Lists;
using Dynamicweb.CoreUI.Screens;
using Xunit;

namespace DynamicWeb.Serializer.Tests.AdminUI;

/// <summary>
/// ItemTypeEditScreen tests. CreateFieldSelector now only builds Options (the union of live
/// metadata + saved exclusions); Value is bound by EditScreenBase.BuildEditor from the
/// List&lt;string&gt; Model.ExcludedFields after GetEditor returns. The framework-binding test
/// simulates that pipeline so the live regression (Value being overwritten by the framework)
/// cannot recur.
/// </summary>
public class ItemTypeEditScreenTests
{
    private static SelectMultiDual InvokeCreateFieldSelector(ItemTypeEditScreen screen)
    {
        var method = typeof(ItemTypeEditScreen)
            .GetMethod("CreateFieldSelector", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (SelectMultiDual)method.Invoke(screen, null)!;
    }

    private static string InvokeGetScreenName(ItemTypeEditScreen screen)
    {
        var method = typeof(ItemTypeEditScreen)
            .GetMethod("GetScreenName", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (string)method.Invoke(screen, null)!;
    }

    private static SelectMultiDual InvokeFrameworkBindingFlow(ItemTypeEditScreen screen, ItemTypeEditModel model)
    {
        var getEditorMethod = typeof(ItemTypeEditScreen)
            .GetMethod("GetEditor", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var editor = (SelectMultiDual)getEditorMethod.Invoke(screen,
            new object[] { nameof(ItemTypeEditModel.ExcludedFields) })!;
        editor.Value = model.ExcludedFields;
        return editor;
    }

    private static void SetModel(ItemTypeEditScreen screen, ItemTypeEditModel? model)
    {
        var prop = typeof(EditScreenBase<ItemTypeEditModel>)
            .GetProperty("Model", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
        prop.SetValue(screen, model);
    }

    [Fact]
    public void CreateFieldSelector_MetadataEmpty_SavedNonEmpty_ShowsSavedAsOptions()
    {
        var screen = new ItemTypeEditScreen();
        SetModel(screen, new ItemTypeEditModel
        {
            SystemName = "SomeNonExistentType",
            ExcludedFields = new List<string> { "f1", "f2", "f3" }
        });

        var editor = InvokeCreateFieldSelector(screen);

        Assert.NotNull(editor.Options);
        Assert.Equal(3, editor.Options!.Count);
        Assert.Contains(editor.Options, o => o.Value as string == "f1");
        Assert.Contains(editor.Options, o => o.Value as string == "f2");
        Assert.Contains(editor.Options, o => o.Value as string == "f3");
    }

    [Fact]
    public void CreateFieldSelector_NoSaved_OptionsEmpty()
    {
        var screen = new ItemTypeEditScreen();
        SetModel(screen, new ItemTypeEditModel
        {
            SystemName = "AnyType",
            ExcludedFields = new List<string>()
        });

        var editor = InvokeCreateFieldSelector(screen);
        Assert.True(editor.Options is null || editor.Options.Count == 0);
    }

    [Fact]
    public void GetScreenName_WithSystemName_StartsWithItemTypeExcludes()
    {
        var screen = new ItemTypeEditScreen();
        SetModel(screen, new ItemTypeEditModel { SystemName = "TestSys" });

        var name = InvokeGetScreenName(screen);

        Assert.StartsWith("Item Type Excludes", name);
        Assert.Contains("TestSys", name);
    }

    [Fact]
    public void GetScreenName_NoModel_IsItemTypeExcludes()
    {
        var screen = new ItemTypeEditScreen();
        SetModel(screen, null);

        var name = InvokeGetScreenName(screen);

        Assert.Equal("Item Type Excludes", name);
    }

    [Fact]
    public void FrameworkBinding_SavedExclusions_RenderAsSelected()
    {
        // Regression-locking: simulates EditScreenBase.BuildEditor's full flow
        // (GetEditor + editor.SetValue(rawValue)) and asserts saved exclusions land on the
        // Selected side as a List<string>. Mirrors the XmlTypeEditScreen test for D-06.
        var saved = new List<string> { "f1", "f2", "f3" };
        var model = new ItemTypeEditModel { SystemName = "SomeNonExistentType", ExcludedFields = saved };
        var screen = new ItemTypeEditScreen();
        SetModel(screen, model);

        var editor = InvokeFrameworkBindingFlow(screen, model);

        Assert.NotNull(editor.Value);
        var bound = Assert.IsAssignableFrom<List<string>>(editor.Value);
        Assert.Equal(saved, bound);

        Assert.NotNull(editor.Options);
        Assert.Equal(3, editor.Options!.Count);
        Assert.Contains(editor.Options, o => o.Value as string == "f1");
        Assert.Contains(editor.Options, o => o.Value as string == "f2");
        Assert.Contains(editor.Options, o => o.Value as string == "f3");
    }
}
