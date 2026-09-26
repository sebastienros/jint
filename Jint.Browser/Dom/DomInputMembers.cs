using Jint.HtmlParser;
using Jint.Native;
using Jint.Runtime;

namespace Jint.Browser.Dom;

/// <summary>Input facts that do not require materializing current value or selection state.</summary>
internal static class DomInputMembers
{
    private static HtmlInputValueState ValueState(DomRealm realm, Element input)
    {
        realm.Engine.Constraints.Check();
        return input.GetHtmlState()!.GetInputValueState(realm.CancellationToken)!;
    }

    internal static JsValue Number(DomRealm realm, Element input)
        => DomConvert.Number(ValueState(realm, input).GetValueAsNumber(realm.CancellationToken));

    internal static JsValue SetNumber(DomRealm realm, Element input, double value)
    {
        ValueState(realm, input).SetValueAsNumber(value, realm.CancellationToken);
        realm.Engine.Constraints.Check();
        return JsValue.Undefined;
    }

    internal static JsValue Date(DomRealm realm, Element input)
    {
        var value = ValueState(realm, input).GetValueAsDate(realm.CancellationToken);
        realm.Engine.Constraints.Check();
        return value.HasDate
            ? realm.OwningRealm.Intrinsics.Date.Construct([JsNumber.Create(value.UtcMilliseconds)], realm.OwningRealm.Intrinsics.Date)
            : JsValue.Null;
    }

    internal static JsValue SetDate(DomRealm realm, Element input, JsValue value)
    {
        double? milliseconds = null;
        if (!value.IsNullOrUndefined())
        {
            if (value is not JsDate date) Throw.TypeError(realm.OwningRealm, "valueAsDate must be a Date or null.");
            else milliseconds = date._dateValue.IsNaN ? double.NaN : date._dateValue.Value;
        }
        ValueState(realm, input).SetValueAsDate(milliseconds, realm.CancellationToken);
        realm.Engine.Constraints.Check();
        return JsValue.Undefined;
    }

    internal static JsValue Step(DomRealm realm, Element input, int count, bool down)
    {
        ValueState(realm, input).Step(count, down, _ => realm.Engine.Constraints.Check(), realm.CancellationToken);
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
