#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Jint.WebApi.Fetch;

namespace Jint.WebApi.WebSockets;

/// <summary>
/// Identifies one <c>WebSocket</c> from its construction to its close.
/// </summary>
/// <param name="Value">A number unique within the process for the lifetime of the socket.</param>
/// <remarks>
/// <b>Deliberately not a <see cref="FetchRequestId"/>.</b> A socket is not a request: it has no body, no
/// redirect chain and no end the transport can predict, so counting one as an outstanding request would
/// make a page with an open socket a page whose network never goes quiet.
/// </remarks>
[StructLayout(LayoutKind.Auto)]
public readonly record struct WebSocketId(long Value)
{
    /// <inheritdoc />
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>
/// The opening handshake a socket is about to make, as plain CLR data.
/// </summary>
/// <remarks>
/// <b><see cref="Headers"/> is what this engine set, and not the whole request.</b> The handshake is made by
/// <see cref="System.Net.WebSockets.ClientWebSocket"/>, which adds <c>Sec-WebSocket-Key</c>,
/// <c>Sec-WebSocket-Version</c>, <c>Connection</c> and <c>Upgrade</c> inside itself and exposes none of
/// them — so an observer is told the headers that were chosen here and nothing is invented for the rest. A
/// host mapping this onto a protocol reports the absent ones as absent.
/// </remarks>
[Experimental(JintDiagnosticIds.PreviewDiagnostic)]
public sealed record ObservedWebSocketHandshake
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ObservedWebSocketHandshake"/> record.
    /// </summary>
    public ObservedWebSocketHandshake()
    {
    }

    /// <summary>Gets the socket this handshake belongs to.</summary>
    public required WebSocketId Id { get; init; }

    /// <summary>Gets the <c>ws:</c> or <c>wss:</c> URL the script asked for.</summary>
    public required Uri Url { get; init; }

    /// <summary>Gets the headers this engine set on the handshake; see the type's remarks.</summary>
    public required IReadOnlyList<FetchHeader> Headers { get; init; }

    /// <summary>Gets the subprotocols the script offered, in order.</summary>
    public required IReadOnlyList<string> Protocols { get; init; }
}

/// <summary>
/// What the server answered the opening handshake with.
/// </summary>
/// <remarks>
/// Reported only when the handshake produced an HTTP response at all: a connection refused before one, a
/// name that did not resolve and a TLS failure end at <see cref="WebSocketObserver.OnClosed"/> with nothing
/// to report here.
/// </remarks>
[Experimental(JintDiagnosticIds.PreviewDiagnostic)]
public sealed record ObservedWebSocketResponse
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ObservedWebSocketResponse"/> record.
    /// </summary>
    public ObservedWebSocketResponse()
    {
    }

    /// <summary>Gets the socket this response belongs to.</summary>
    public required WebSocketId Id { get; init; }

    /// <summary>Gets the HTTP status code, which is <c>101</c> for a handshake that succeeded.</summary>
    public required int Status { get; init; }

    /// <summary>Gets the response headers, one entry per value.</summary>
    public required IReadOnlyList<FetchHeader> Headers { get; init; }

    /// <summary>Gets the subprotocol the handshake settled on, or the empty string.</summary>
    public required string SubProtocol { get; init; }
}

/// <summary>
/// Watches the opening and closing handshakes of the <c>WebSocket</c>s one engine opens.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is not <see cref="FetchObserver"/>, and that is the decision rather than an omission.</b> A socket's
/// handshake never reaches the fetch transport — <see cref="System.Net.WebSockets.ClientWebSocket"/> makes
/// its own HTTP request — so there is no hop to intercept, an interception's <c>Fulfill</c> could not be
/// honoured, and the frames afterwards are not a body. What is left that a host can honestly be told is the
/// four moments below, so they are their own seam with their own identifier
/// (<see href="https://github.com/sebastienros/jint/issues/3701">#3701</see> item 2).
/// </para>
/// <para>
/// <b>Nothing here is a request.</b> A socket stays open for as long as the page wants it, so counting one
/// against the fetch lifecycle would leave a request outstanding for that whole time — and a host that waits
/// for its network to go quiet would wait for ever. <see cref="WebSocketId"/> is a separate identifier for
/// exactly that reason.
/// </para>
/// <para>
/// <b>Every callback runs on a transport thread and must never touch the <see cref="Engine"/>.</b> Nothing
/// it is handed is a <c>JsValue</c>, an <see cref="Engine"/> or a realm — the same rule
/// <see cref="FetchObserver"/> carries, and for the same reason. A callback that throws is ignored: there is
/// no engine thread to report it to, and a socket must not depend on an observer.
/// </para>
/// <para>
/// <b>The order is fixed</b>: <see cref="OnCreated"/>, <see cref="OnHandshakeRequest"/>, then
/// <see cref="OnHandshakeResponse"/> when the server answered one at all, then exactly one
/// <see cref="OnClosed"/> — including for a socket whose handshake never succeeded, because a
/// <c>WebSocket</c> that fails to open still reaches <c>CLOSED</c> and still fires <c>close</c> at script.
/// </para>
/// <para>
/// The shape of this class is a preview and is declared to the compiler as <c>JINT0002</c>; see
/// <see cref="Options.FetchOptions.WebSocketObserver"/>.
/// </para>
/// </remarks>
[Experimental(JintDiagnosticIds.PreviewDiagnostic)]
public abstract class WebSocketObserver
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketObserver"/> class.
    /// </summary>
    protected WebSocketObserver()
    {
    }

    /// <summary>
    /// Called on the engine thread as <c>new WebSocket(...)</c> returns, before anything is sent.
    /// </summary>
    /// <param name="id">The socket.</param>
    /// <param name="url">The URL the script asked for.</param>
    /// <remarks>
    /// The one callback that is <i>not</i> on a transport thread, because there is no transport yet. It is
    /// still not allowed to touch the engine: the script that called the constructor is still running.
    /// </remarks>
    public virtual void OnCreated(WebSocketId id, Uri url)
    {
    }

    /// <summary>
    /// Called before the opening handshake goes out.
    /// </summary>
    /// <param name="handshake">The URL, the subprotocols and the headers this engine set.</param>
    public virtual void OnHandshakeRequest(ObservedWebSocketHandshake handshake)
    {
    }

    /// <summary>
    /// Called when the server answered the handshake, whether or not it accepted it.
    /// </summary>
    /// <param name="response">The status, the headers and the settled subprotocol.</param>
    public virtual void OnHandshakeResponse(ObservedWebSocketResponse response)
    {
    }

    /// <summary>
    /// Called once, when the socket has closed — for any reason, a failed handshake included.
    /// </summary>
    /// <param name="id">The socket.</param>
    /// <param name="code">The close code the script will see.</param>
    /// <param name="reason">The close reason, which may be empty.</param>
    /// <param name="wasClean">Whether the closing handshake completed.</param>
    public virtual void OnClosed(WebSocketId id, int code, string reason, bool wasClean)
    {
    }
}
#endif
