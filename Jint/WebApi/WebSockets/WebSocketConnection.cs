#if NET8_0_OR_GREATER
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Jint.WebApi.WebSockets;

/// <summary>
/// What one <see cref="IWebSocketConnection.ReceiveAsync"/> answered with: a whole message, or the peer's
/// half of the closing handshake.
/// </summary>
internal enum WebSocketReceiptKind
{
    /// <summary>A complete text message, still as its UTF-8 bytes.</summary>
    Text,

    /// <summary>A complete binary message.</summary>
    Binary,

    /// <summary>The peer sent a Close frame; <see cref="WebSocketReceipt.CloseCode"/> carries its status code.</summary>
    Close,
}

/// <summary>
/// One answer from the transport. Deliberately message-shaped rather than frame-shaped: the reassembly of a
/// fragmented message, and the ceiling on how large one may grow, belong to the transport, so that everything
/// above it — <see cref="WebSocketOperation"/> and the event dispatch — deals in whole messages.
/// </summary>
/// <param name="Kind">Which of the three shapes this is.</param>
/// <param name="Data">The message bytes, empty for a close.</param>
/// <param name="CloseCode">
/// The status code the peer sent, or 1005 when it sent a Close frame with no body —
/// https://www.rfc-editor.org/rfc/rfc6455#section-7.4.1, where 1005 is "no status code was actually present".
/// </param>
/// <param name="CloseReason">The peer's close reason, UTF-8 decoded, or the empty string.</param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct WebSocketReceipt(WebSocketReceiptKind Kind, byte[] Data, int CloseCode, string CloseReason)
{
    /// <summary>https://www.rfc-editor.org/rfc/rfc6455#section-7.4.1 — "no status code was actually present".</summary>
    internal const int NoStatusReceived = 1005;

    internal static WebSocketReceipt Message(bool isText, byte[] data)
        => new(isText ? WebSocketReceiptKind.Text : WebSocketReceiptKind.Binary, data, 0, string.Empty);

    internal static WebSocketReceipt Closed(int code, string reason)
        => new(WebSocketReceiptKind.Close, [], code, reason);
}

/// <summary>
/// The socket operations <c>WebSocket</c> needs, as an interface rather than a <c>ClientWebSocket</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing here may touch the engine.</b> Every member runs on a thread pool thread while the script that
/// opened the socket goes on running, exactly as <c>FetchTransport</c> does: the engine is not thread-safe,
/// so the only way back to it is the generation-stamped event-loop job
/// <see cref="WebSocketOperation"/> queues.
/// </para>
/// <para>
/// The seam exists so the whole state machine above it — the handshake, the receive loop, the send queue,
/// <c>bufferedAmount</c>, the close handshake and every event — can be driven by a scripted double with no
/// network anywhere. It is deliberately <see langword="internal"/>: a host-facing transport seam is a larger
/// design question (a host supplying its own would want headers, proxies, client certificates and a
/// keep-alive policy with it) and is left for a follow-up.
/// </para>
/// <para>
/// The concurrency contract is the one <c>ClientWebSocket</c> itself has: at most one
/// <see cref="SendAsync"/> and at most one <see cref="ReceiveAsync"/> may be outstanding at a time, and those
/// two may overlap each other. <see cref="Abort"/> may be called from any thread at any time, including from
/// the engine's.
/// </para>
/// </remarks>
internal interface IWebSocketConnection : IDisposable
{
    /// <summary>
    /// The subprotocol the handshake settled on, or the empty string. Read after <see cref="ConnectAsync"/>
    /// has completed.
    /// </summary>
    string SubProtocol { get; }

    /// <summary>Opens the connection — https://websockets.spec.whatwg.org/#concept-websocket-establish.</summary>
    Task ConnectAsync(CancellationToken cancellationToken);

    /// <summary>Sends one whole message, as a text frame or a binary one.</summary>
    Task SendAsync(ReadOnlyMemory<byte> payload, bool isText, CancellationToken cancellationToken);

    /// <summary>
    /// Waits for the next whole message, or for the peer's Close frame.
    /// </summary>
    /// <exception cref="WebSocketMessageTooLargeException">
    /// The message exceeded the ceiling the connection was created with.
    /// </exception>
    Task<WebSocketReceipt> ReceiveAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Sends this endpoint's Close frame, which is the whole of "start the WebSocket closing handshake".
    /// </summary>
    /// <param name="code">
    /// The status code, or <see langword="null"/> for a Close frame with no body at all — which is what
    /// <c>close()</c> with neither argument is specified to send.
    /// </param>
    /// <param name="reason">The reason, which is only ever sent alongside a code.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task CloseOutputAsync(int? code, string reason, CancellationToken cancellationToken);

    /// <summary>
    /// Drops the connection without a handshake — "fail the WebSocket connection",
    /// https://www.rfc-editor.org/rfc/rfc6455#section-7.1.7. Safe to call from any thread and more than once.
    /// </summary>
    void Abort();
}

/// <summary>
/// Where a <see cref="IWebSocketConnection"/> comes from. One instance serves every engine.
/// </summary>
internal interface IWebSocketConnectionFactory
{
    /// <summary>
    /// Builds a connection for one <c>WebSocket</c>. Called on the engine's thread, before anything is sent.
    /// </summary>
    /// <param name="url">The absolute <c>ws:</c> or <c>wss:</c> URL, already past the host's policy.</param>
    /// <param name="protocols">The subprotocols to offer, already validated as tokens.</param>
    /// <param name="maxMessageBytes">
    /// The largest message the connection may reassemble before it raises
    /// <see cref="WebSocketMessageTooLargeException"/>, from <c>Options.WebApi.Fetch.MaxResponseBytes</c>.
    /// </param>
    /// <param name="userAgent">
    /// The <c>User-Agent</c> the opening handshake carries, from <c>Options.WebApi.Fetch.UserAgent</c>, or
    /// <see langword="null"/> for none. The handshake is an HTTP request like any other, so it says what the
    /// rest of the engine says.
    /// </param>
    IWebSocketConnection Create(Uri url, IReadOnlyList<string> protocols, long maxMessageBytes, string? userAgent);
}

/// <summary>
/// A peer sent more in one message than <c>Options.WebApi.Fetch.MaxResponseBytes</c> allows.
/// </summary>
/// <remarks>
/// It never reaches script: <see cref="WebSocketOperation"/> turns it into a close with status code 1009,
/// which is RFC 6455's "message is too big", plus the <c>error</c> event every abnormal closure fires.
/// </remarks>
internal sealed class WebSocketMessageTooLargeException : Exception
{
    internal WebSocketMessageTooLargeException(string message) : base(message)
    {
    }
}
#endif
