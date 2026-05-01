using System.Reflection;
using DynamicWeb.Serializer.AdminUI.Models;
using DynamicWeb.Serializer.AdminUI.Screens;
using Dynamicweb.CoreUI.Editors.Lists;
using Dynamicweb.CoreUI.Screens;
using Xunit;

namespace DynamicWeb.Serializer.Tests.AdminUI;

/// <summary>
/// Phase 41 D-05 RED tests: dual-list editor must merge saved exclusions into discovered options
/// instead of short-circuiting when XmlTypeDiscovery returns 0 elements. These tests will FAIL
/// against the current XmlTypeEditScreen.CreateElementSelector early-return at lines 108-112.
/// Plan 41-03 makes them pass.
/// </summary>
public class XmlTypeEditScreenTests
{
    private static SelectMultiDual InvokeCreateElementSelector(XmlTypeEditScreen screen)
    {
        // CreateElementSelector is private — reflect into it. Same approach as
        // SerializerSettingsNodeProviderModeTreeTests uses for direct provider-method calls.
        var method = typeof(XmlTypeEditScreen)
            .GetMethod("CreateElementSelector", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (SelectMultiDual)method.Invoke(screen, null)!;
    }

    private static void SetModel(XmlTypeEditScreen screen, XmlTypeEditModel model)
    {
        // Model lives on ScreenBase<TModel> (parent of EditScreenBase<TModel>).
        // GetProperty walks the inheritance chain by default for inherited public/protected members.
        var prop = typeof(EditScreenBase<XmlTypeEditModel>)
            .GetProperty("Model", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
        prop.SetValue(screen, model);
    }

    [Fact]
    public void CreateElementSelector_DiscoveryEmpty_SavedNonEmpty_ShowsSavedAsOptions()
    {
        // Arrange: live DB has no XML for this type, but config has 3 saved exclusions.
        // This is the eCom_CartV2 repro from 41-RESEARCH.md.
        //
        // NOTE: in the current screen XmlTypeDiscovery is constructed inline; this test cannot
        // inject a FakeSqlExecutor without the Plan 41-03 DI seam. The current XmlTypeEditScreen
        // short-circuits with editor.Options=null + a no-data Explanation when discovery returns 0,
        // dropping the 3 saved exclusions. After Plan 41-03 ships the DI seam + merge logic,
        // Options will surface the saved exclusions and pre-select them.
        //
        // Wave 0 RED: assert the GREEN target so Plan 41-03 must implement the merge.
        var screen = new XmlTypeEditScreen();
        SetModel(screen, new XmlTypeEditModel
        {
            TypeName = "eCom_CartV2",
            ExcludedElements = "elemA\nelemB\nelemC"
        });

        var editor = InvokeCreateElementSelector(screen);

        // GREEN target: Options has 3 items (the saved set), Value pre-selects them.
        // RED today: editor.Options is null because the screen short-circuits.
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
        // Arrange: discovery returns 0 or some elements (no FakeSqlExecutor seam yet);
        // saved has elemA, elemC. After Plan 41-03 ships the merge, Options must include
        // every saved entry even if discovery didn't supply it.
        var screen = new XmlTypeEditScreen();
        SetModel(screen, new XmlTypeEditModel
        {
            TypeName = "TestType",
            ExcludedElements = "elemA\nelemC"
        });

        var editor = InvokeCreateElementSelector(screen);

        // GREEN target: Options must include elemC (the saved-only one) under the post-fix merge.
        // RED today: editor.Options is null (short-circuit) so the assertion fails.
        Assert.NotNull(editor.Options);
        Assert.Contains(editor.Options!, o => string.Equals(o.Value as string, "elemC", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CreateElementSelector_NoSaved_NoSelectionPreset()
    {
        // Regression baseline: with no saved exclusions the editor.Value must not be set.
        // This already passes today; included to lock existing behavior.
        var screen = new XmlTypeEditScreen();
        SetModel(screen, new XmlTypeEditModel
        {
            TypeName = "TestTypeWithNoSaved",
            ExcludedElements = string.Empty
        });

        var editor = InvokeCreateElementSelector(screen);

        // editor.Value should be null when no exclusions are saved (existing behavior).
        Assert.Null(editor.Value);
    }
}
