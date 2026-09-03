#if NET8_0_OR_GREATER
using System.Buffers;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Jint.WebApi.WebSockets;

/// <summary>
/// The in-box transport: one <see cref="ClientWebSocket"/> per <c>WebSocket</c>.
/// </summary>
/// <remarks>
/// <para>
/// Two things the WHATWG handshake asks for are deliberately not offered here, and both show up in the
/// interface's own attributes. <b>No <c>permessage-deflate</c></b> — the standard has the user agent offer a
/// compression extension, and .NET only enables one when a host configures it; not offering it means
/// <c>extensions</c> is always the empty string, which is the honest answer rather than a guess, and a
/// decompression bomb cannot be built out of a message the size cap measures after inflation. And <b>no
/// cookies and no credentials</b>: there is no cookie jar in an embedded engine, and the standard's
/// <c>credentials: "include"</c> would mean sending some other component's.
/// </para>
/// <para>
/// Redirects are not followed, which is what the standard asks for too — "redirect mode is error" — so unlike
/// <c>fetch</c> there is no per-hop policy re-check to perform: the one URL the host's filter admitted is the
/// only one a socket ever reaches.
/// </para>
/// </remarks>
internal sealed class ClientWebSocketConnection : IWebSocketConnection
{
    /// <summary>
    /// The read buffer one receive rents. A whole message is reassembled from as many of these as it takes;
    /// the number only decides how many awaits a large message costs.
    /// </summary>
    private const int ReceiveChunkSize = 16 * 1024;

    private readonly ClientWebSocket _socket = new();
    private readonly Uri _url;
    private readonly long _maxMessageBytes;

    internal ClientWebSocketConnection(Uri url, IReadOnlyList<string> protocols, long maxMessageBytes, string? userAgent)
    {
        _url = url;
        _maxMessageBytes = maxMessageBytes;

        for (var i = 0; i < protocols.Count; i++)
        {
            // Already validated against the token grammar by the constructor, which is what keeps this from
            // being the place a bad subprotocol is discovered.
            _socket.Options.AddSubProtocol(protocols[i]);
        }

        _socket.Options.UseDefaultCredentials = false;
        _socket.Options.Cookies = null;

        // The opening handshake is an HTTP request, so it carries the engine's own default `User-Agent`
        // value (https://fetch.spec.whatwg.org/#default-user-agent-value) exactly as a fetch does. .NET
        // sends none of its own, which is what made a socket the one lane that said nothing.
        if (userAgent is { Length: > 0 })
        {
            _socket.Options.SetRequestHeader("User-Agent", userAgent);
        }
    }

    public string SubProtocol => _socket.SubProtocol ?? string.Empty;

    public Task ConnectAsync(CancellationToken cancellationToken) => _socket.ConnectAsync(_url, cancellationToken);

    public Task SendAsync(ReadOnlyMemory<byte> payload, bool isText, CancellationToken cancellationToken)
    {
        var type = isText ? WebSocketMessageType.Text : WebSocketMessageType.Binary;
        return _socket.SendAsync(payload, type, endOfMessage: true, cancellationToken).AsTask();
    }

    public async Task<WebSocketReceipt> ReceiveAsync(CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(ReceiveChunkSize);
        ArrayBufferWriter<byte>? pending = null;
        var isText = false;

        try
        {
            while (true)
            {
                var result = await _socket.ReceiveAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    // A Close frame with no body leaves CloseStatus at Empty, which is 1005 — the code the
                    // standard has the close event report for "no status code was actually present".
                    var code = (int?) _socket.CloseStatus ?? WebSocketReceipt.NoStatusReceived;
                    return WebSocketReceipt.Closed(code, _socket.CloseStatusDescription ?? string.Empty);
                }

                if (pending is null)
                {
                    // The first frame of the message decides whether the whole of it is text.
                    isText = result.MessageType == WebSocketMessageType.Text;

                    if (result.EndOfMessage)
                    {
                        // The common case: one frame, one message, one copy and no writer at all.
                        Guard(result.Count);
                        return WebSocketReceipt.Message(isText, buffer.AsSpan(0, result.Count).ToArray());
                    }

                    pending = new ArrayBufferWriter<byte>(result.Count == 0 ? ReceiveChunkSize : result.Count * 2);
                }

                Guard(pending.WrittenCount + (long) result.Count);
                pending.Write(buffer.AsSpan(0, result.Count));

                if (result.EndOfMessage)
                {
                    return WebSocketReceipt.Message(isText, pending.WrittenSpan.ToArray());
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public Task CloseOutputAsync(int? code, string reason, CancellationToken cancellationToken)
    {
        // WebSocketCloseStatus.Empty is 1005, which the framework writes as a Close frame with no body at
        // all; it refuses to carry a reason, which is exactly the protocol's own rule — a reason has nowhere
        // to live without a status code in front of it.
        if (code is not { } status)
        {
            return _socket.CloseOutputAsync(WebSocketCloseStatus.Empty, statusDescription: null, cancellationToken);
        }

        return _socket.CloseOutputAsync((WebSocketCloseStatus) status, reason, cancellationToken);
    }

    public void Abort()
    {
        try
        {
            _socket.Abort();
        }
        catch (ObjectDisposedException)
        {
            // Raced with the run's own disposal; the connection is gone either way.
        }
    }

    public void Dispose() => _socket.Dispose();

    private void Guard(long total)
    {
        if (total > _maxMessageBytes)
        {
            throw new WebSocketMessageTooLargeException(
                $"The message exceeded the {_maxMessageBytes} byte limit set by Options.WebApi.Fetch.MaxResponseBytes.");
        }
    }
}

/// <summary>
/// The factory behind every socket a host did not redirect elsewhere.
/// </summary>
internal sealed class ClientWebSocketConnectionFactory : IWebSocketConnectionFactory
{
    internal static readonly ClientWebSocketConnectionFactory Instance = new();

    private ClientWebSocketConnectionFactory()
    {
    }

    public IWebSocketConnection Create(Uri url, IReadOnlyList<string> protocols, long maxMessageBytes, string? userAgent)
        => new ClientWebSocketConnection(url, protocols, maxMessageBytes, userAgent);
}
#endif
