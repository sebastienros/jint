#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.Events;

namespace Jint.WebApi.ServerSentEvents;

/// <summary>
/// <c>EventSource.prototype</c> — the interface prototype object.
/// <para>
/// https://html.spec.whatwg.org/multipage/server-sent-events.html#the-eventsource-interface
/// </para>
/// </summary>
/// <remarks>
/// Its <c>[[Prototype]]</c> is <c>EventTarget.prototype</c>, so an event source has <c>addEventListener</c>
/// and the rest. The three readyState constants appear here as well as on the interface object, per
/// https://webidl.spec.whatwg.org/#es-constants, which defines them one after another in the order the IDL
/// declares them. That order is observable, so they are declared below in it and
/// <c>PreserveDeclarationOrder</c> keeps the generator from sorting them by name.
/// </remarks>
[JsObject(UseShape = true, PreserveDeclarationOrder = true)]
internal sealed partial class EventSourcePrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly EventSourceConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString EventSourceToStringTag = new("EventSource");

    [JsProperty(Name = "CONNECTING", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber Connecting = JsNumber.Create(JsEventSource.Connecting);
    [JsProperty(Name = "OPEN", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber Open = JsNumber.Create(JsEventSource.Open);
    [JsProperty(Name = "CLOSED", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber Closed = JsNumber.Create(JsEventSource.Closed);

    internal EventSourcePrototype(
        Engine engine,
        Realm realm,
        EventSourceConstructor constructor,
        ObjectInstance eventTargetPrototype) : base(engine, realm)
    {
        _prototype = eventTargetPrototype;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/server-sent-events.html#dom-eventsource-url — "must return the
    /// serialization of this EventSource object's url".
    /// </summary>
    [JsAccessor("url", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsString UrlGet(JsValue thisObject) => Brand(thisObject).Href;

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/server-sent-events.html#dom-eventsource-withcredentials — the
    /// value the object was initialized with. See <see cref="JsEventSource.WithCredentials"/> for why it
    /// changes nothing.
    /// </summary>
    [JsAccessor("withCredentials", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsBoolean WithCredentialsGet(JsValue thisObject) => JsBoolean.Create(Brand(thisObject).WithCredentials);

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/server-sent-events.html#dom-eventsource-readystate
    /// </summary>
    [JsAccessor("readyState", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsNumber ReadyStateGet(JsValue thisObject) => JsNumber.Create(Brand(thisObject).ReadyState);

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/server-sent-events.html#handler-eventsource-onopen
    /// </summary>
    [JsAccessor("onopen", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnOpenGet(JsValue thisObject) => EventHandlerAttributes.Get(Brand(thisObject), JsEventSource.OpenEventType);

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/server-sent-events.html#handler-eventsource-onopen, setter half.
    /// </summary>
    [JsAccessor("onopen", AccessorKind.Set, Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnOpenSet(JsValue thisObject, JsValue value) => EventHandlerAttributes.Set(Brand(thisObject), JsEventSource.OpenEventType, value);

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/server-sent-events.html#handler-eventsource-onmessage
    /// </summary>
    [JsAccessor("onmessage", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnMessageGet(JsValue thisObject) => EventHandlerAttributes.Get(Brand(thisObject), EventStreamParser.DefaultEventType);

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/server-sent-events.html#handler-eventsource-onmessage, setter
    /// half. It is the handler for the <c>message</c> type only: an event the stream renamed with an
    /// <c>event</c> field reaches <c>addEventListener(thatName, …)</c> and nothing else, which is what makes
    /// custom event types worth sending.
    /// </summary>
    [JsAccessor("onmessage", AccessorKind.Set, Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnMessageSet(JsValue thisObject, JsValue value) => EventHandlerAttributes.Set(Brand(thisObject), EventStreamParser.DefaultEventType, value);

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/server-sent-events.html#handler-eventsource-onerror
    /// </summary>
    [JsAccessor("onerror", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnErrorGet(JsValue thisObject) => EventHandlerAttributes.Get(Brand(thisObject), JsEventSource.ErrorEventType);

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/server-sent-events.html#handler-eventsource-onerror, setter
    /// half. The event it receives is a plain <c>Event</c> carrying no detail at all — the standard gives an
    /// event source no way to say <i>why</i> a connection failed, and <c>readyState</c> is what tells a script
    /// whether this one is retrying (<c>CONNECTING</c>) or over (<c>CLOSED</c>).
    /// </summary>
    [JsAccessor("onerror", AccessorKind.Set, Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnErrorSet(JsValue thisObject, JsValue value) => EventHandlerAttributes.Set(Brand(thisObject), JsEventSource.ErrorEventType, value);

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/server-sent-events.html#dom-eventsource-close — "must abort any
    /// instances of the fetch algorithm started for this EventSource object, and must set the readyState
    /// attribute to CLOSED". Calling it twice, or on a source that already failed, does nothing.
    /// </summary>
    [JsFunction(Name = "close", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue Close(JsValue thisObject)
    {
        Brand(thisObject).Close();
        return Undefined;
    }

    private JsEventSource Brand(JsValue thisObject)
    {
        if (thisObject is JsEventSource source)
        {
            return source;
        }

        Throw.TypeError(_realm, "Illegal invocation: receiver is not an EventSource");
        return null!;
    }
}
#endif
