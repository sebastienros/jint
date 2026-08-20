#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Messaging;

/// <summary>
/// <c>MessageEvent.prototype</c> — the interface prototype object.
/// <para>
/// https://html.spec.whatwg.org/multipage/comms.html#messageevent
/// </para>
/// </summary>
/// <remarks>
/// Its <c>[[Prototype]]</c> is <c>Event.prototype</c>, which is what gives a <c>MessageEvent</c> every
/// <c>Event</c> member and makes <c>ev instanceof Event</c> hold. <c>initMessageEvent()</c> is deliberately
/// absent — the specification marks it legacy and tells new code not to use it.
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class MessageEventPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly MessageEventConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString MessageEventToStringTag = new("MessageEvent");

    internal MessageEventPrototype(
        Engine engine,
        Realm realm,
        MessageEventConstructor constructor,
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
    /// https://html.spec.whatwg.org/multipage/comms.html#dom-messageevent-data
    /// </summary>
    [JsAccessor("data", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue DataGet(JsValue thisObject) => Brand(thisObject).Data;

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/comms.html#dom-messageevent-origin
    /// </summary>
    [JsAccessor("origin", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsString OriginGet(JsValue thisObject) => Brand(thisObject).Origin;

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/comms.html#dom-messageevent-lasteventid
    /// </summary>
    [JsAccessor("lastEventId", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsString LastEventIdGet(JsValue thisObject) => Brand(thisObject).LastEventId;

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/comms.html#dom-messageevent-source
    /// </summary>
    [JsAccessor("source", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue SourceGet(JsValue thisObject) => Brand(thisObject).Source;

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/comms.html#dom-messageevent-ports — a <c>FrozenArray</c>, so the
    /// very same array object is answered every time.
    /// </summary>
    [JsAccessor("ports", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsArray PortsGet(JsValue thisObject) => Brand(thisObject).Ports;

    private JsMessageEvent Brand(JsValue thisObject)
    {
        if (thisObject is JsMessageEvent messageEvent)
        {
            return messageEvent;
        }

        Throw.TypeError(_realm, "Illegal invocation: receiver is not a MessageEvent");
        return null!;
    }
}
#endif
