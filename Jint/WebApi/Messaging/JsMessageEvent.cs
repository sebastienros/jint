#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.WebApi.Events;

namespace Jint.WebApi.Messaging;

/// <summary>
/// A <c>MessageEvent</c> instance — the event a <c>MessagePort</c> delivers a message with.
/// <para>
/// https://html.spec.whatwg.org/multipage/comms.html#messageevent
/// </para>
/// </summary>
/// <remarks>
/// Every IDL attribute is read-only, so the state lives in CLR fields here and
/// <see cref="MessageEventPrototype"/> reads it through a brand check, exactly as <c>Event</c> and
/// <c>CustomEvent</c> do. <c>initMessageEvent()</c> is deliberately absent: the specification marks it legacy.
/// </remarks>
internal sealed class JsMessageEvent : JsEvent
{
    internal JsMessageEvent(
        Engine engine,
        JsString type,
        EventInit init,
        double timeStamp,
        JsValue data,
        JsString origin,
        JsString lastEventId,
        JsValue source,
        JsArray ports)
        : base(engine, type, init, timeStamp)
    {
        Data = data;
        Origin = origin;
        LastEventId = lastEventId;
        Source = source;
        Ports = ports;
    }

    /// <summary>https://html.spec.whatwg.org/multipage/comms.html#dom-messageevent-data.</summary>
    internal JsValue Data { get; }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/comms.html#dom-messageevent-origin. Always the empty string for
    /// an event a port fires: the port message steps do not set an origin, and Jint has no origins to name.
    /// </summary>
    internal JsString Origin { get; }

    /// <summary>https://html.spec.whatwg.org/multipage/comms.html#dom-messageevent-lasteventid.</summary>
    internal JsString LastEventId { get; }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/comms.html#dom-messageevent-source — a <c>MessagePort</c> or
    /// <see cref="JsValue.Null"/>. A port delivery never sets one; only the constructor can.
    /// </summary>
    internal JsValue Source { get; }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/comms.html#dom-messageevent-ports — a frozen array, so the same
    /// object is returned on every read. Always empty for an event a port fires, because transferring a port
    /// is not supported; see <see cref="JsMessagePort"/>.
    /// </summary>
    internal JsArray Ports { get; }
}
#endif
