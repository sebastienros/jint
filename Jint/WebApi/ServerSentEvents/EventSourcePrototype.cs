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
/// https://webidl.spec.whatwg.org/#es-constants.
/// </remarks>
[JsObject(UseShape = true)]
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
    private JsValue OnOpenGet(JsValue thisObject) => HandlerGet(thisObject, JsEventSource.OpenEventType);

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/server-sent-events.html#handler-eventsource-onopen, setter half.
    /// </summary>
    [JsAccessor("onopen", AccessorKind.Set, Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnOpenSet(JsValue thisObject, JsValue value) => HandlerSet(thisObject, JsEventSource.OpenEventType, value);

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/server-sent-events.html#handler-eventsource-onmessage
    /// </summary>
    [JsAccessor("onmessage", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnMessageGet(JsValue thisObject) => HandlerGet(thisObject, EventStreamParser.DefaultEventType);

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/server-sent-events.html#handler-eventsource-onmessage, setter
    /// half. It is the handler for the <c>message</c> type only: an event the stream renamed with an
    /// <c>event</c> field reaches <c>addEventListener(thatName, …)</c> and nothing else, which is what makes
    /// custom event types worth sending.
    /// </summary>
    [JsAccessor("onmessage", AccessorKind.Set, Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnMessageSet(JsValue thisObject, JsValue value) => HandlerSet(thisObject, EventStreamParser.DefaultEventType, value);

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/server-sent-events.html#handler-eventsource-onerror
    /// </summary>
    [JsAccessor("onerror", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnErrorGet(JsValue thisObject) => HandlerGet(thisObject, JsEventSource.ErrorEventType);

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/server-sent-events.html#handler-eventsource-onerror, setter
    /// half. The event it receives is a plain <c>Event</c> carrying no detail at all — the standard gives an
    /// event source no way to say <i>why</i> a connection failed, and <c>readyState</c> is what tells a script
    /// whether this one is retrying (<c>CONNECTING</c>) or over (<c>CLOSED</c>).
    /// </summary>
    [JsAccessor("onerror", AccessorKind.Set, Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnErrorSet(JsValue thisObject, JsValue value) => HandlerSet(thisObject, JsEventSource.ErrorEventType, value);

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/server-sent-events.html#dom-eventsource-close — "must abort any
    /// instances of the fetch algorithm started for this EventSource object, and must set the readyState
    /// attribute to CLOSED". Calling it twice, or on a source that already failed, does nothing.
    /// </summary>
    [JsFunction(Name = "close", Length = 0)]
    private JsValue Close(JsValue thisObject)
    {
        Brand(thisObject).Close();
        return Undefined;
    }

    /// <summary>
    /// The getter half of an event handler IDL attribute,
    /// https://html.spec.whatwg.org/multipage/webappapis.html#event-handler-idl-attributes.
    /// </summary>
    private JsValue HandlerGet(JsValue thisObject, string type)
        => Brand(thisObject).FindEventHandler(type)?.Callback ?? Null;

    /// <summary>
    /// The setter half. <c>EventHandler</c> is a nullable callback function annotated
    /// <c>[LegacyTreatNonObjectAsNull]</c>, so assigning anything that is not an object clears the handler
    /// rather than raising a <c>TypeError</c>; an object that is not callable is stored and read back but
    /// never invoked.
    /// </summary>
    /// <remarks>
    /// The handler is one entry of the object's own event listener list, so it takes its turn in registration
    /// order among the <c>addEventListener</c> listeners rather than running before or after all of them.
    /// Reassigning replaces the value in place — the entry keeps the position it was first given — and
    /// assigning a non-object removes the entry outright.
    /// </remarks>
    private JsValue HandlerSet(JsValue thisObject, string type, JsValue value)
    {
        var source = Brand(thisObject);
        var existing = source.FindEventHandler(type);

        if (value is not ObjectInstance)
        {
            if (existing is not null)
            {
                source.RemoveListener(existing);
            }

            return Undefined;
        }

        if (existing is not null)
        {
            existing.Callback = value;
            return Undefined;
        }

        source.AddListener(new EventListenerRegistration(type, value) { IsEventHandler = true });
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
