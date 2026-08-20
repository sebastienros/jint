#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.WebApi.Events;

namespace Jint.WebApi.WebSockets;

/// <summary>
/// A <c>CloseEvent</c> instance: how a <c>WebSocket</c> connection ended.
/// <para>
/// https://websockets.spec.whatwg.org/#the-closeevent-interface
/// </para>
/// </summary>
internal sealed class JsCloseEvent : JsEvent
{
    internal JsCloseEvent(
        Engine engine,
        JsString type,
        EventInit init,
        double timeStamp,
        bool wasClean,
        int code,
        string reason)
        : base(engine, type, init, timeStamp)
    {
        WasClean = wasClean;
        Code = code;
        Reason = reason;
    }

    /// <summary>
    /// https://websockets.spec.whatwg.org/#dom-closeevent-wasclean — whether the connection closed after the
    /// closing handshake completed, rather than being dropped.
    /// </summary>
    internal bool WasClean { get; }

    /// <summary>
    /// https://websockets.spec.whatwg.org/#dom-closeevent-code — the WebSocket connection close code, which
    /// is 1006 for every abnormal closure and 1005 when the peer sent no code at all.
    /// </summary>
    internal int Code { get; }

    /// <summary>
    /// https://websockets.spec.whatwg.org/#dom-closeevent-reason — the close reason, UTF-8 decoded.
    /// </summary>
    internal string Reason { get; }
}
#endif
