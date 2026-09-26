using Jint.Browser.Events;
using Jint.HtmlParser;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Browser.Dom;

/// <summary>Input facts that do not require materializing current value or selection state.</summary>
internal static class DomInputMembers
{
    internal static bool Checked(DomRealm realm, Element input)
    {
        realm.Engine.Constraints.Check();
        HtmlCheckableState.GetRadioGroupFacts(input, realm.NativeReadCheckpoint, realm.CancellationToken);
        var value = HtmlCheckableState.Get(input, realm.NativeReadCheckpoint, realm.CancellationToken)!.Checked;
        realm.Engine.Constraints.Check();
        realm.CancellationToken.ThrowIfCancellationRequested();
        return value;
    }

    internal static JsValue SetChecked(DomRealm realm, Element input, bool value)
    {
        realm.Engine.Constraints.Check();
        HtmlCheckableState.Get(input, realm.NativeReadCheckpoint, realm.CancellationToken)!.SetChecked(value, realm.NativeReadCheckpoint, realm.CancellationToken);
        realm.Engine.Constraints.Check();
        realm.CancellationToken.ThrowIfCancellationRequested();
        return JsValue.Undefined;
    }

    internal static JsValue SetIndeterminate(DomRealm realm, Element input, bool value)
    {
        realm.Engine.Constraints.Check();
        HtmlCheckableState.Get(input, realm.NativeReadCheckpoint, realm.CancellationToken)!.SetIndeterminate(value, realm.NativeReadCheckpoint, realm.CancellationToken);
        realm.Engine.Constraints.Check();
        realm.CancellationToken.ThrowIfCancellationRequested();
        return JsValue.Undefined;
    }

    // HTML §4.10.20: offsets and direction are the native UTF-16 selection, with
    // the existing Events queue carrying the selection range algorithm's events.
    internal static JsValue SelectionOffset(DomRealm realm, Element input, bool start)
    {
        var selection = ValueState(realm, input).GetSelection(realm.NativeReadCheckpoint, realm.CancellationToken);
        return selection is { } value ? DomConvert.Number(start ? value.Start : value.End) : JsValue.Null;
    }

    internal static JsValue SelectionDirection(DomRealm realm, Element input)
    {
        var selection = ValueState(realm, input).GetSelection(realm.NativeReadCheckpoint, realm.CancellationToken);
        if (selection is null) return JsValue.Null;
        return DomConvert.Text(selection.Value.Direction switch
        {
            HtmlSelectionDirection.Forward => "forward",
            HtmlSelectionDirection.Backward => "backward",
            _ => "none",
        });
    }

    internal static JsValue SetSelectionDirection(DomRealm realm, Element input, string direction)
    {
        var state = ValueState(realm, input);
        var previous = state.Selection;
        state.SetSelectionDirection(direction, realm.NativeReadCheckpoint, realm.CancellationToken);
        SelectionChanged(realm, input, previous, state.Selection);
        return JsValue.Undefined;
    }

    internal static JsValue SetSelectionOffset(DomRealm realm, Element input, uint offset, bool start)
    {
        var state = ValueState(realm, input);
        var previous = state.Selection;
        if (start) state.SetSelectionStart(offset, realm.NativeReadCheckpoint, realm.CancellationToken);
        else state.SetSelectionEnd(offset, realm.NativeReadCheckpoint, realm.CancellationToken);
        SelectionChanged(realm, input, previous, state.Selection);
        return JsValue.Undefined;
    }

    internal static JsValue SetSelectionRange(DomRealm realm, Element input, JsValue[] arguments)
    {
        const string member = "HTMLInputElement.setSelectionRange";
        // Finish WebIDL conversions before demanding any native current value state.
        var start = DomConvert.RequiredUInt32(arguments, 0, member);
        var end = DomConvert.RequiredUInt32(arguments, 1, member);
        var direction = DomConvert.OptionalText(arguments, 2, null);
        var state = ValueState(realm, input);
        var previous = state.Selection;
        state.SetSelectionRange(start, end, direction, realm.NativeReadCheckpoint, realm.CancellationToken);
        SelectionChanged(realm, input, previous, state.Selection);
        return JsValue.Undefined;
    }

    internal static JsValue Select(DomRealm realm, Element input)
    {
        var state = ValueState(realm, input);
        var previous = state.Selection;
        state.Select(realm.NativeReadCheckpoint, realm.CancellationToken);
        SelectionChanged(realm, input, previous, state.Selection);
        return JsValue.Undefined;
    }

    private static void SelectionChanged(DomRealm realm, Element input, HtmlTextSelection previous, HtmlTextSelection current)
    {
        if (previous == current) return;
        SelectionChange.Schedule(realm, input);
        var wrapper = realm.WrapNode(input);
        realm.Engine.Tasks.Post(() => ActivationBehaviors.Fire(wrapper, "select", bubbles: true, composed: false));
    }

    // HTML §4.10.5.3.9: the first matching ID in the ordinary tree must itself be a datalist.
    internal static Element? List(DomRealm realm, Element input)
        => List(input, realm.NativeReadCheckpoint, realm.CancellationToken);

    internal static Element? List(Element input, Action<int>? checkpoint, CancellationToken token)
    {
        var work = new DomReadWork(checkpoint, token);
        work.Check();
        var id = work.Attribute(input, "list");
        if (string.IsNullOrEmpty(id)) { work.Check(); return null; }
        var type = HtmlInputTypes.Parse(work.Attribute(input, "type"));
        if (type is not (HtmlInputType.Text or HtmlInputType.Search or HtmlInputType.Tel or HtmlInputType.Url
            or HtmlInputType.Email or HtmlInputType.Date or HtmlInputType.Month or HtmlInputType.Week
            or HtmlInputType.Time or HtmlInputType.DateTimeLocal or HtmlInputType.Number or HtmlInputType.Range
            or HtmlInputType.Color))
        {
            work.Check();
            return null;
        }
        var root = work.Root(input);
        if (root is Element element && work.Equal(work.Attribute(element, "id"), id))
        {
            work.Check();
            return element is { NamespaceUri: Namespaces.Html, LocalName: "datalist" } ? element : null;
        }
        foreach (var candidate in NodeTraversal.DescendantElements(root, work.Check, token))
        {
            if (!work.Equal(work.Attribute(candidate, "id"), id)) continue;
            work.Check();
            return candidate is { NamespaceUri: Namespaces.Html, LocalName: "datalist" } ? candidate : null;
        }
        work.Check();
        return null;
    }

    private static HtmlInputValueState ValueState(DomRealm realm, Element input)
    {
        realm.Engine.Constraints.Check();
        return input.GetHtmlState()!.GetInputValueState(realm.NativeReadCheckpoint, realm.CancellationToken)!;
    }

    internal static JsValue Number(DomRealm realm, Element input)
        => DomConvert.Number(ValueState(realm, input).GetValueAsNumber(realm.NativeReadCheckpoint, realm.CancellationToken));

    internal static JsValue SetNumber(DomRealm realm, Element input, double value)
    {
        ValueState(realm, input).SetValueAsNumber(value, realm.NativeReadCheckpoint, realm.CancellationToken);
        realm.Engine.Constraints.Check();
        return JsValue.Undefined;
    }

    internal static JsValue Date(DomRealm realm, Element input)
    {
        var value = ValueState(realm, input).GetValueAsDate(realm.NativeReadCheckpoint, realm.CancellationToken);
        realm.Engine.Constraints.Check();
        return value.HasDate
            ? realm.OwningRealm.Intrinsics.Date.Construct([JsNumber.Create(value.UtcMilliseconds)], realm.OwningRealm.Intrinsics.Date)
            : JsValue.Null;
    }

    internal static JsValue SetDate(DomRealm realm, Element input, JsValue value)
    {
        if (!value.IsNullOrUndefined() && value is not ObjectInstance)
            Throw.TypeError(realm.OwningRealm, "valueAsDate must be an object or null.");
        var state = ValueState(realm, input);
        // HTML checks applicability after WebIDL's object? conversion, before checking the Date slot.
        if (state.Type is not (HtmlInputType.Date or HtmlInputType.Month or HtmlInputType.Week or HtmlInputType.Time))
            throw new DomException("InvalidStateError", "This input does not permit the date operation.");
        double? milliseconds = null;
        if (!value.IsNullOrUndefined())
        {
            if (value is not JsDate date) Throw.TypeError(realm.OwningRealm, "valueAsDate must be a Date or null.");
            else milliseconds = date._dateValue.IsNaN ? double.NaN : date._dateValue.Value;
        }
        state.SetValueAsDate(milliseconds, realm.NativeReadCheckpoint, realm.CancellationToken);
        realm.Engine.Constraints.Check();
        return JsValue.Undefined;
    }

    internal static JsValue Step(DomRealm realm, Element input, int count, bool down)
    {
        var state = ValueState(realm, input);
        if (down) state.StepDown(count, realm.NativeReadCheckpoint, realm.CancellationToken);
        else state.StepUp(count, realm.NativeReadCheckpoint, realm.CancellationToken);
        realm.Engine.Constraints.Check();
        return JsValue.Undefined;
    }

    // https://html.spec.whatwg.org/multipage/input.html#dom-input-type
    internal static string Type(DomRealm realm, Element input)
    {
        var work = new DomReadWork(realm.NativeReadCheckpoint, realm.CancellationToken);
        work.Check();
        var type = HtmlInputTypes.Parse(work.Attribute(input, "type"));
        work.Check();
        return HtmlInputTypes.Info(type).Keyword;
    }
}
