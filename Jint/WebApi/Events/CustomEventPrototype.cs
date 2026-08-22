#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Events;

/// <summary>
/// <c>CustomEvent.prototype</c> — the interface prototype object.
/// <para>
/// https://dom.spec.whatwg.org/#interface-customevent
/// </para>
/// </summary>
/// <remarks>
/// Its <c>[[Prototype]]</c> is <c>Event.prototype</c>, which is what gives a <c>CustomEvent</c> every
/// <c>Event</c> member and makes <c>new CustomEvent('x') instanceof Event</c> hold.
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class CustomEventPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly CustomEventConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString CustomEventToStringTag = new("CustomEvent");

    internal CustomEventPrototype(
        Engine engine,
        Realm realm,
        CustomEventConstructor constructor,
        ObjectInstance eventPrototype) : base(engine, realm)
    {
        _prototype = eventPrototype;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-customevent-detail
    /// </summary>
    [JsAccessor("detail", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue DetailGet(JsValue thisObject) => Brand(thisObject).Detail;

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-customevent-initcustomevent — <c>initEvent()</c>'s two steps followed
    /// by "set this's detail attribute to <i>detail</i>".
    /// </summary>
    /// <remarks>
    /// The IDL default of <c>detail</c> is <c>null</c>, so calling this with fewer arguments <i>clears</i> a
    /// detail the constructor set rather than leaving it alone. <c>length</c> is 1 for the reason
    /// <c>initEvent</c>'s is: only <c>type</c> is required.
    /// </remarks>
    [JsFunction(Name = "initCustomEvent", Length = 1)]
    private JsValue InitCustomEvent(JsValue thisObject, JsValue type, JsValue bubbles, JsValue cancelable, JsValue detail)
    {
        var ev = Brand(thisObject);
        if (ev.DispatchFlag)
        {
            return Undefined;
        }

        ev.InitializeEvent(
            TypeConverter.ToJsString(type),
            TypeConverter.ToBoolean(bubbles),
            TypeConverter.ToBoolean(cancelable));

        ev.Detail = detail.IsUndefined() ? Null : detail;
        return Undefined;
    }

    /// <summary>
    /// The WebIDL brand check every member performs: a receiver that is not a <c>CustomEvent</c> raises a
    /// <c>TypeError</c>.
    /// </summary>
    private JsCustomEvent Brand(JsValue thisObject)
    {
        if (thisObject is JsCustomEvent customEvent)
        {
            return customEvent;
        }

        Throw.TypeError(_realm, "Illegal invocation: receiver is not a CustomEvent");
        return null!;
    }
}
#endif
