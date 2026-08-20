#if NET8_0_OR_GREATER
using SystemEncoding = System.Text.Encoding;
using Jint.Native;
using Jint.Runtime;
using Jint.WebApi.Events;
using Jint.WebApi.Files;

namespace Jint.WebApi.WebSockets;

/// <summary>
/// A <c>WebSocket</c> instance: the ready state, the attributes the standard exposes, and the four tasks the
/// protocol feeds back into the event loop.
/// <para>
/// https://websockets.spec.whatwg.org/#the-websocket-interface
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>Every member here runs on the engine's thread.</b> The socket itself lives in
/// <see cref="WebSocketOperation"/>, which touches nothing on this object directly — it queues the four
/// <c>Report</c> methods below as event-loop jobs, so the ready state only ever moves while the engine is
/// being pumped or inside a <c>close()</c> the script itself called.
/// </para>
/// <para>
/// Two attributes are permanently what an embedded engine can honestly say. <c>extensions</c> is always the
/// empty string, because the in-box transport offers no <c>permessage-deflate</c> — see
/// <see cref="ClientWebSocketConnection"/>. And <c>binaryType</c> defaults to <c>"arraybuffer"</c> where the
/// standard's default is <c>"blob"</c>: this is the one deliberate divergence in the interface, taken because
/// a <c>Blob</c> is an inert byte holder here whose only consumers are asynchronous, so defaulting to it would
/// make every binary message cost a promise round-trip to look at. Setting <c>binaryType = "blob"</c> works
/// and produces real <c>Blob</c>s, which is why enabling this feature also enables <c>Files</c>.
/// </para>
/// </remarks>
internal sealed class JsWebSocket : JsEventTarget
{
    /// <summary>https://websockets.spec.whatwg.org/#dom-websocket-connecting.</summary>
    internal const int Connecting = 0;

    /// <summary>https://websockets.spec.whatwg.org/#dom-websocket-open.</summary>
    internal const int Open = 1;

    /// <summary>https://websockets.spec.whatwg.org/#dom-websocket-closing.</summary>
    internal const int Closing = 2;

    /// <summary>https://websockets.spec.whatwg.org/#dom-websocket-closed.</summary>
    internal const int Closed = 3;

    /// <summary>The <c>BinaryType</c> enumeration, https://websockets.spec.whatwg.org/#binarytype.</summary>
    internal const string BinaryTypeArrayBuffer = "arraybuffer";

    /// <summary>The <c>BinaryType</c> enumeration, https://websockets.spec.whatwg.org/#binarytype.</summary>
    internal const string BinaryTypeBlob = "blob";

    internal const string OpenEventType = "open";
    internal const string MessageEventType = "message";
    internal const string ErrorEventType = "error";
    internal const string CloseEventType = "close";

    private static readonly JsString _openEvent = new(OpenEventType);
    private static readonly JsString _messageEvent = new(MessageEventType);
    private static readonly JsString _errorEvent = new(ErrorEventType);
    private static readonly JsString _closeEvent = new(CloseEventType);

    internal JsWebSocket(Engine engine, Realm realm, string url, string origin) : base(engine, realm)
    {
        Url = url;
        Origin = origin;
    }

    /// <summary>https://websockets.spec.whatwg.org/#dom-websocket-url — this's url, serialized.</summary>
    internal string Url { get; }

    /// <summary>
    /// The serialization of the url's origin, which is what every <c>message</c> event carries —
    /// https://websockets.spec.whatwg.org/#feedback-from-the-protocol.
    /// </summary>
    internal string Origin { get; }

    /// <summary>https://websockets.spec.whatwg.org/#dom-websocket-readystate.</summary>
    internal int ReadyState { get; private set; } = Connecting;

    /// <summary>
    /// https://websockets.spec.whatwg.org/#dom-websocket-protocol — the subprotocol the handshake settled on,
    /// which is the empty string until it has.
    /// </summary>
    internal string Protocol { get; private set; } = string.Empty;

    /// <summary>https://websockets.spec.whatwg.org/#dom-websocket-binarytype.</summary>
    internal string BinaryType { get; set; } = BinaryTypeArrayBuffer;

    /// <summary>https://websockets.spec.whatwg.org/#dom-websocket-bufferedamount.</summary>
    internal long BufferedAmount { get; private set; }

    /// <summary>
    /// The socket behind this object. Always set once the constructor has returned; it is a property rather
    /// than a constructor argument only because the operation needs the object it reports to.
    /// </summary>
    internal WebSocketOperation? Operation { get; set; }

    /// <summary>
    /// "Increase the bufferedAmount attribute by the number of bytes needed to express the argument" — every
    /// <c>send()</c> that does not throw, whether or not the bytes are ever written.
    /// </summary>
    internal void AddBufferedAmount(long bytes) => BufferedAmount += bytes;

    /// <summary>
    /// The other half: the bytes have reached the network, so they are no longer queued. Posted by the send
    /// loop as its own event-loop job, one per message.
    /// </summary>
    internal void ReleaseBufferedAmount(long bytes)
    {
        BufferedAmount -= bytes;

        if (BufferedAmount < 0)
        {
            // Unreachable — every release answers one queueing — but a negative bufferedAmount would be an
            // unsigned long long underflow to script, which is a far worse thing to ship than a clamp.
            BufferedAmount = 0;
        }
    }

    /// <summary>
    /// https://websockets.spec.whatwg.org/#dom-websocket-close steps 3.2 and 3.3, which both set the ready
    /// state to CLOSING synchronously, inside the <c>close()</c> call itself.
    /// </summary>
    internal void EnterClosing()
    {
        if (ReadyState is Connecting or Open)
        {
            ReadyState = Closing;
        }
    }

    /// <summary>
    /// https://websockets.spec.whatwg.org/#feedback-from-the-protocol — "when the WebSocket connection is
    /// established".
    /// </summary>
    /// <remarks>
    /// The guard is what makes a <c>close()</c> during the handshake final: it set the ready state to CLOSING
    /// on the spot, so a connection that had already been established by the time the job runs still fires no
    /// <c>open</c> event.
    /// </remarks>
    internal void ReportOpen(string protocol)
    {
        if (ReadyState != Connecting)
        {
            return;
        }

        ReadyState = Open;
        Protocol = protocol;

        // "Fire an event named open at the WebSocket object" — extensions would be updated here too, and are
        // always the empty string; see the class remarks.
        FireEvent(_openEvent);
    }

    /// <summary>
    /// https://websockets.spec.whatwg.org/#feedback-from-the-protocol — "when a WebSocket message has been
    /// received". Step 1 drops anything that arrives once the socket is no longer OPEN.
    /// </summary>
    internal void ReportMessage(bool isText, byte[] data)
    {
        if (ReadyState != Open)
        {
            return;
        }

        var payload = Package(isText, data);
        var message = _realm.Intrinsics.MessageEvent.CreateTrustedMessageEvent(_messageEvent, payload, JsString.Create(Origin), JsString.Empty);
        DispatchEvent(message);
    }

    /// <summary>
    /// https://websockets.spec.whatwg.org/#feedback-from-the-protocol — "when the WebSocket closing handshake
    /// is started", which fires no event and only moves the ready state.
    /// </summary>
    internal void ReportClosingHandshake()
    {
        if (ReadyState == Open)
        {
            ReadyState = Closing;
        }
    }

    /// <summary>
    /// https://websockets.spec.whatwg.org/#feedback-from-the-protocol — "when the WebSocket connection is
    /// closed".
    /// </summary>
    /// <remarks>
    /// The <c>error</c> event is fired for every closure that is not clean, which covers both cases the
    /// standard names — a connection the user agent was required to fail, and one closed after being flagged
    /// as full — and the abrupt drops that amount to the same thing. The socket has already left the engine's
    /// registry and released its transport by the time either listener runs, so a listener that throws — which
    /// erupts from the pump, as every web-API callback in Jint does — leaks nothing.
    /// </remarks>
    internal void ReportClosed(int code, string reason, bool wasClean)
    {
        if (ReadyState == Closed)
        {
            return;
        }

        ReadyState = Closed;
        _engine._webApi?.UnregisterWebSocket(this);
        Operation = null;

        if (!wasClean)
        {
            FireEvent(_errorEvent);
        }

        var close = _realm.Intrinsics.CloseEvent.CreateTrustedClose(_closeEvent, code, reason, wasClean);
        DispatchEvent(close);
    }

    /// <summary>
    /// The <c>data</c> attribute the <c>message</c> event carries: a string for a text message, and for a
    /// binary one whichever of the two <c>binaryType</c> names.
    /// </summary>
    private JsValue Package(bool isText, byte[] data)
    {
        if (isText)
        {
            // A text message is UTF-8 by protocol; decoded leniently, so a peer that lies produces U+FFFD
            // rather than an exception on the engine's pump.
            return JsString.Create(SystemEncoding.UTF8.GetString(data));
        }

        if (string.Equals(BinaryType, BinaryTypeBlob, StringComparison.Ordinal))
        {
            return new JsBlob(_engine, data, string.Empty)
            {
                _prototype = _realm.Intrinsics.Blob.PrototypeObject,
            };
        }

        return new JsArrayBuffer(_engine, data)
        {
            _prototype = _realm.Intrinsics.ArrayBuffer.PrototypeObject,
        };
    }
}
#endif
