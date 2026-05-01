using System.Reflection;
using DynamicWeb.Serializer.AdminUI.Models;
using DynamicWeb.Serializer.AdminUI.Screens;
using Dynamicweb.CoreUI.Editors.Lists;
using Dynamicweb.CoreUI.Screens;
using Xunit;

namespace DynamicWeb.Serializer.Tests.AdminUI;

/// <summary>
/// Phase 41 D-06 RED tests + D-01 edit-screen rename reflection assertions.
/// CreateFieldSelector tests will FAIL against the current early-return at line 99-101
/// (when ItemManager.Metadata.GetItemType returns null, saved exclusions are dropped).
/// GetScreenName tests will FAIL against current "Item Type:" prefix at line 130-131.
/// Plans 41-02 (D-01) and 41-03 (D-06) make them pass.
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

    private static void SetModel(ItemTypeEditScreen screen, ItemTypeEditModel? model)
    {
        // Model lives on ScreenBase<TModel> (parent of EditScreenBase<TModel>).
        var prop = typeof(EditScreenBase<ItemTypeEditModel>)
            .GetProperty("Model", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
        prop.SetValue(screen, model);
    }

    [Fact]
    public void CreateFieldSelector_MetadataEmpty_SavedNonEmpty_ShowsSavedAsOptions()
    {
        // Arrange: ItemManager.Metadata.GetItemType returns null for this systemName (the "metadata
        // empty" pattern). Saved exclusions exist. Current code drops them via early-return at line 100.
        var screen = new ItemTypeEditScreen();
        SetModel(screen, new ItemTypeEditModel
        {
            SystemName = "SomeNonExistentType",
            ExcludedFields = "f1\nf2\nf3"
        });

        var editor = InvokeCreateFieldSelector(screen);

        // GREEN target: saved exclusions must surface as Options + pre-select Value.
        // RED today: editor.Options is empty/null because the screen short-circuits.
        Assert.NotNull(editor.Options);
        Assert.Equal(3, editor.Options!.Count);
        Assert.Contains(editor.Options, o => o.Value as string == "f1");
        Assert.Contains(editor.Options, o => o.Value as string == "f2");
        Assert.Contains(editor.Options, o => o.Value as string == "f3");
        Assert.NotNull(editor.Value);
        Assert.Equal(3, ((string[])editor.Value!).Length);
    }

    [Fact]
    public void CreateFieldSelector_NoSaved_NoSelectionPreset()
    {
        // Regression baseline: empty ExcludedFields → Value should be null. Should already pass.
        var screen = new ItemTypeEditScreen();
        SetModel(screen, new ItemTypeEditModel
        {
            SystemName = "AnyType",
            ExcludedFields = string.Empty
        });

        var editor = InvokeCreateFieldSelector(screen);
        Assert.Null(editor.Value);
    }

    [Fact]
    public void GetScreenName_WithSystemName_StartsWithItemTypeExcludes()
    {
        // D-01 RED: current returns "Item Type: {SystemName}". After Plan 41-02 it must return
        // "Item Type Excludes - {SystemName}".
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
}
