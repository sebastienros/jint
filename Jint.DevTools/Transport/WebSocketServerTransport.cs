using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;

namespace Jint.DevTools.Transport;

/// <summary>
/// The listener: a TCP socket, an HTTP/1.1 upgrade, and one WebSocket per client.
/// </summary>
/// <remarks>
/// <para>
/// <b>A <see cref="TcpListener"/> rather than an <c>HttpListener</c>.</b> On Windows the latter registers a
/// URL prefix with <c>http.sys</c>, which needs an administrative reservation for anything but the default
/// prefix — something a library embedded in somebody else's process cannot ask for. Everything this endpoint
/// serves is a request line, a few headers and either a document or an upgrade, so a socket plus
/// <see cref="WebSocket.CreateFromStream(Stream, bool, string, TimeSpan)"/> needs nothing from the operating
/// system that a socket does not. There is deliberately no ASP.NET dependency either: a host that already
/// has one may keep it, and one that does not is not made to take it.
/// </para>
/// <para>
/// Two paths open a conversation. <c>/devtools/browser/&lt;browserId&gt;</c> is the browser endpoint, where a
/// client discovers targets and attaches to them; <c>/devtools/page/&lt;targetId&gt;</c> is one engine
/// directly, and messages on it carry no <c>sessionId</c> at all. Anything else is 404 <i>before</i> the
/// upgrade, so a client that guessed a path is told so rather than left holding an open socket.
/// </para>
/// </remarks>
internal sealed class WebSocketServerTransport : IAsyncDisposable
{
    /// <summary>The GUID RFC 6455 §1.3 says to append to the client's key before hashing it.</summary>
    private const string WebSocketGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

    /// <summary>How much request head is read before a client is assumed to be sending something else.</summary>
    private const int MaxRequestHeadBytes = 16 * 1024;

    private readonly DevToolsServer _server;
    private readonly CancellationTokenSource _stopping = new();
    private readonly List<Task> _connections = [];

    private TcpListener? _listener;
    private Task? _accepting;
    private int _disposed;

    internal WebSocketServerTransport(DevToolsServer server)
    {
        _server = server;
    }

    /// <summary>Gets the port the listener bound to.</summary>
    internal int BoundPort { get; private set; }

    /// <summary>Binds the listener and starts accepting, which is what makes the port readable.</summary>
    internal void Start()
    {
        var address = ResolveAddress(_server.Options.Host);
        var listener = new TcpListener(address, _server.Options.Port);
        listener.Start();

        BoundPort = ((IPEndPoint) listener.LocalEndpoint).Port;
        _listener = listener;
        _accepting = Task.Run(() => AcceptAsync(_stopping.Token), CancellationToken.None);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await _stopping.CancelAsync().ConfigureAwait(false);
        _listener?.Stop();

        Task[] pending;
        lock (_connections)
        {
            pending = _connections.ToArray();
        }

        if (_accepting is { } accepting)
        {
            await Quietly(accepting).ConfigureAwait(false);
        }

        foreach (var connection in pending)
        {
            await Quietly(connection).ConfigureAwait(false);
        }

        _stopping.Dispose();
    }

    private static IPAddress ResolveAddress(string host)
    {
        if (IPAddress.TryParse(host, out var parsed))
        {
            return parsed;
        }

        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return IPAddress.Loopback;
        }

        // A name rather than an address. Resolving it here rather than at bind time gives the host a
        // failure naming what it configured.
        var addresses = Dns.GetHostAddresses(host);
        return addresses.Length > 0
            ? addresses[0]
            : throw new InvalidOperationException($"'{host}' does not resolve to an address to listen on.");
    }

    private async Task AcceptAsync(CancellationToken cancellationToken)
    {
        var listener = _listener!;

        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (SocketException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            var connection = Task.Run(() => ServeAsync(client, cancellationToken), CancellationToken.None);

            lock (_connections)
            {
                _connections.RemoveAll(static task => task.IsCompleted);
                _connections.Add(connection);
            }
        }
    }

    private async Task ServeAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using (client)
        {
            client.NoDelay = true;

            var stream = client.GetStream();

            HttpRequestHead? head;
            try
            {
                head = await HttpRequestHead.ReadAsync(stream, MaxRequestHeadBytes, cancellationToken).ConfigureAwait(false);
            }
            catch (IOException)
            {
                return;
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (head is not { } request)
            {
                return;
            }

            try
            {
                if (request.IsWebSocketUpgrade)
                {
                    await UpgradeAsync(stream, request, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await ServeDocumentAsync(stream, request, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (IOException)
            {
                // The client hung up in the middle. Every session it opened is torn down by the connection's
                // own closed callback; there is nothing else owed to it.
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private async Task ServeDocumentAsync(NetworkStream stream, HttpRequestHead request, CancellationToken cancellationToken)
    {
        HttpDiscoveryResponse response;
        try
        {
            response = await HttpDiscovery.AnswerAsync(_server, request).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // a discovery document must never take the listener down with it
        catch (Exception exception)
#pragma warning restore CA1031
        {
            response = new HttpDiscoveryResponse(500, "Internal Server Error", "text/plain; charset=UTF-8", exception.Message);
        }

        var body = Encoding.UTF8.GetBytes(response.Body);
        var head = string.Create(
            CultureInfo.InvariantCulture,
            $"HTTP/1.1 {response.Status} {response.Reason}\r\nContent-Type: {response.ContentType}\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");

        await stream.WriteAsync(Encoding.ASCII.GetBytes(head), cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(body, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

        // Half-close, and this is not politeness. The caller disposes the TcpClient the moment this returns;
        // a close with anything still unread -- a keep-alive client's pipelined request, a byte that arrived
        // while the document was being built -- is answered by an RST, and an RST discards whatever is still
        // in the send buffer. The client then sees a connection reset rather than the body, which is a
        // failure that only shows up when the machine is loaded enough for the timing to matter. A FIN says
        // "that is the whole response" and leaves the delivered bytes delivered.
        Shutdown(stream);
    }

    /// <summary>
    /// Ends the sending half of a connection, ignoring a socket that has already gone.
    /// </summary>
    private static void Shutdown(NetworkStream stream)
    {
        try
        {
            stream.Socket.Shutdown(SocketShutdown.Send);
        }
        catch (SocketException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task UpgradeAsync(NetworkStream stream, HttpRequestHead request, CancellationToken cancellationToken)
    {
        var (kind, routed) = Route(request.Path);
        if (kind == RouteKind.None)
        {
            // Answered as an ordinary 404 rather than upgraded and then closed: a client that guessed a
            // path reads the status, and one that was upgraded first would only see the socket go.
            await ServeDocumentAsync(stream, request with { Path = "/none" }, cancellationToken).ConfigureAwait(false);
            return;
        }

        // SHA-1, and not a choice: RFC 6455 section 4.2.2 defines the handshake as SHA-1 of the client's key
        // and a fixed GUID. It authenticates nothing and protects nothing -- it exists so that a cache or a
        // proxy cannot be tricked into completing an upgrade -- so the weakness the analyzer names is not one
        // here, and substituting a stronger hash would simply fail every client.
#pragma warning disable CA5350
        var accept = Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(request.UpgradeKey + WebSocketGuid)));
#pragma warning restore CA5350
        var head = "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: " + accept + "\r\n\r\n";

        await stream.WriteAsync(Encoding.ASCII.GetBytes(head), cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

        var socket = WebSocket.CreateFromStream(stream, isServer: true, subProtocol: null, TimeSpan.FromSeconds(30));
        var connection = new WebSocketConnection(socket, _server.Options.MaxMessageBytes);

        await using (connection.ConfigureAwait(false))
        {
            var closed = routed is { } target ? OpenTarget(connection, target) : OpenBrowser(connection);

            try
            {
                await connection.RunAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                closed?.Invoke();
            }
        }
    }

    private Action? OpenBrowser(WebSocketConnection connection)
    {
        var session = _server.OpenBrowserSession(connection, closeRequested: () =>
        {
            if (_server.Options.CloseIsDisconnect)
            {
                // Every client sends Browser.close on the way out and a browser would exit. Jint is in
                // somebody else's process, so the client is what ends: its socket closes behind the reply to
                // this very command, and the host runs on.
                connection.RequestClose();
            }
        });

        return () => _server.CloseBrowserSession(session);
    }

    private static Action? OpenTarget(WebSocketConnection connection, DevToolsTarget target)
    {
        // There is no browser conversation to forget, but the session itself holds engine state — the
        // handles it minted, the bindings it installed, the debugger it may have paused — and a connection
        // that goes away is a client that will never release any of it. A pause makes that load-bearing
        // rather than tidy: nothing else would let the engine thread out of it.
        var session = DevToolsServer.OpenTargetSession(connection, target);
        return session.Detach;
    }

    /// <summary>
    /// Decides which conversation a path opens.
    /// </summary>
    /// <returns>
    /// <see cref="RouteKind.Browser"/> with no target for the browser endpoint,
    /// <see cref="RouteKind.Target"/> with the engine for a direct one, and <see cref="RouteKind.None"/>
    /// for anything else — including a stale identifier, so a client holding one is told the browser or the
    /// target is gone rather than silently attached to a different one.
    /// </returns>
    private (RouteKind Kind, DevToolsTarget? Target) Route(string path)
    {
        var query = path.IndexOf('?', StringComparison.Ordinal);
        if (query >= 0)
        {
            path = path.Substring(0, query);
        }

        if (path.StartsWith("/devtools/browser/", StringComparison.Ordinal))
        {
            var id = path.Substring("/devtools/browser/".Length);
            return string.Equals(id, _server.BrowserId, StringComparison.Ordinal)
                ? (RouteKind.Browser, null)
                : (RouteKind.None, null);
        }

        if (path.StartsWith("/devtools/page/", StringComparison.Ordinal))
        {
            var id = path.Substring("/devtools/page/".Length);
            return _server.FindTarget(id) is { } target ? (RouteKind.Target, target) : (RouteKind.None, null);
        }

        return (RouteKind.None, null);
    }

    /// <summary>What a WebSocket path names.</summary>
    private enum RouteKind
    {
        /// <summary>Nothing this server serves.</summary>
        None,

        /// <summary>The browser endpoint, where a client discovers targets and attaches to them.</summary>
        Browser,

        /// <summary>One engine directly, whose messages carry no session identifier.</summary>
        Target,
    }

    private static async Task Quietly(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
#pragma warning disable CA1031 // shutting down is not the moment to raise what a finished task held
        catch (Exception)
#pragma warning restore CA1031
        {
        }
    }
}
