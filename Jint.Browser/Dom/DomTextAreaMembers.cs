using Jint.Browser.Events;
using Jint.HtmlParser;
using Jint.Native;

namespace Jint.Browser.Dom;

/// <summary>HTML §4.10.11 conversions and event scheduling around native textarea state.</summary>
internal static class DomTextAreaMembers
{
    private static HtmlTextAreaState State(Element element) => element.GetHtmlState()!.TextArea!;

    internal static string Value(DomRealm realm, Element element)
    {
        realm.Engine.Constraints.Check();
        var value = State(element).GetValue(realm.CancellationToken);
        realm.Engine.Constraints.Check();
        return value;
    }

    internal static string DefaultValue(DomRealm realm, Element element)
    {
        realm.Engine.Constraints.Check();
        var value = State(element).GetDefaultValue(realm.CancellationToken);
        realm.Engine.Constraints.Check();
        return value;
    }

    internal static JsValue SetValue(DomRealm realm, Element element, string value, bool defaultValue)
    {
        var state = State(element);
        var selection = state.Selection;
        if (defaultValue) state.SetDefaultValue(value, realm.CancellationToken);
        else state.SetValue(value, realm.NativeReadCheckpoint, realm.CancellationToken);
        SelectionChanged(realm, element, selection, state.Selection, fireSelect: false);
        return JsValue.Undefined;
    }

    internal static string Direction(Element element) => State(element).Selection.Direction switch
    {
        HtmlSelectionDirection.Forward => "forward",
        HtmlSelectionDirection.Backward => "backward",
        _ => "none",
    };

    internal static JsValue SetSelectionOffset(DomRealm realm, Element element, uint offset, bool start)
    {
        var state = State(element);
        var previous = state.Selection;
        if (start) state.SetSelectionStart(offset, realm.CancellationToken);
        else state.SetSelectionEnd(offset, realm.CancellationToken);
        SelectionChanged(realm, element, previous, state.Selection, fireSelect: true);
        return JsValue.Undefined;
    }

    internal static JsValue SetSelectionRange(DomRealm realm, Element element, JsValue[] arguments)
    {
        const string member = "HTMLTextAreaElement.setSelectionRange";
        var start = DomConvert.RequiredUInt32(arguments, 0, member);
        var end = DomConvert.RequiredUInt32(arguments, 1, member);
        var direction = DomConvert.OptionalText(arguments, 2, null);
        var state = State(element);
        var previous = state.Selection;
        state.SetSelectionRange(start, end, direction, realm.CancellationToken);
        SelectionChanged(realm, element, previous, state.Selection, fireSelect: true);
        return JsValue.Undefined;
    }

    internal static JsValue Select(DomRealm realm, Element element)
    {
        var state = State(element);
        var previous = state.Selection;
        state.Select(realm.CancellationToken);
        SelectionChanged(realm, element, previous, state.Selection, fireSelect: true);
        return JsValue.Undefined;
    }

    private static void SelectionChanged(DomRealm realm, Element element, HtmlTextSelection previous, HtmlTextSelection current, bool fireSelect)
    {
        if (previous == current) return;
        SelectionChange.Schedule(realm, element);
        if (fireSelect)
        {
            var wrapper = realm.WrapNode(element);
            realm.Engine.Tasks.Post(() => ActivationBehaviors.Fire(wrapper, "select", bubbles: true, composed: false));
        }
    }
}
