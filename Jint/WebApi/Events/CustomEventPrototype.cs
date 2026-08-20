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
/// <c>initCustomEvent()</c> is deliberately absent — the specification marks it legacy and tells new
/// interfaces not to introduce one.
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
    private JsValue DetailGet(JsValue thisObject)
    {
        if (thisObject is JsCustomEvent customEvent)
        {
            return customEvent.Detail;
        }

        Throw.TypeError(_realm, "Illegal invocation: receiver is not a CustomEvent");
        return null!;
    }
}
#endif
