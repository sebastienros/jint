using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using Jint.DevTools.Session;
using Jint.DevTools.Transport;

namespace Jint.Tests.DevTools.Transport;

/// <summary>
/// The send whose completion is the socket write, and the one thing a command may keep alive past its own
/// dispatch: the memory its reply occupies.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why it exists.</b> <c>SendAsync</c> completes at the channel enqueue, which says nothing about how long
/// a large encoded payload keeps occupying memory. A command that reserved an allowance for its own reply
/// therefore had no moment at which to release, and repeated commands could queue unboundedly many encoded
/// copies behind a slow socket. <c>Fetch.getResponseBody</c> is the first command that needs the moment.
/// </para>
/// <para>
/// <b>Every wait is bounded.</b> A transport test that can hang is a continuous-integration leg that can hang.
/// </para>
/// </remarks>
[NonParallelizable]
public class TrackedSendTests
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(30);

    /// <summary>A lease that says whether and how often it was released.</summary>
    private sealed class Lease : IDisposable
    {
        internal int Releases { get; private set; }

        public void Dispose() => Releases++;
    }

    // ---------------------------------------------------------------- the session's half

    [Test]
    public void AContextReleasesWhatItHoldsExactlyOnce()
    {
        var lease = new Lease();
        var context = new CommandContext(session: null!, sessionId: null, CancellationToken.None);

        context.HoldsUntilReplyWritten.Should().BeFalse("a command that reserved nothing takes the ordinary send");

        context.HoldUntilReplyWritten(null);
        context.HoldsUntilReplyWritten.Should().BeFalse("nothing is not something to hold");

        context.HoldUntilReplyWritten(lease);
        context.HoldsUntilReplyWritten.Should().BeTrue();

        context.ReleaseHeld();
        context.ReleaseHeld();

        lease.Releases.Should().Be(1, "the session releases once, whatever happened to the reply");
    }

    // ---------------------------------------------------------------- the socket's half

    /// <summary>
    /// A tracked send completes when the bytes are on the socket, not when they are queued — the whole
    /// distinction the API exists to make.
    /// </summary>
    [Test]
    public async Task ATrackedSendCompletesWhenTheBytesAreOnTheSocket()
    {
        await using var pair = await SocketPair.CreateAsync();

        var connection = new WebSocketConnection(pair.Server, maxMessageBytes: 1 << 20);
        using var stopping = new CancellationTokenSource();
        var running = connection.RunAsync(stopping.Token);

        var tracked = connection.SendTrackedAsync("{\"id\":1}").AsTask();

        // Nothing has read from the client end yet, so the writer may or may not have flushed; what the test
        // pins is that reading the frame is enough to complete it, and within the bound.
        var received = await pair.ReceiveAsync().WaitAsync(Bound);
        received.Should().Be("{\"id\":1}");

        await tracked.WaitAsync(Bound);

        await stopping.CancelAsync();
        await pair.CloseAsync();

        try
        {
            await running.WaitAsync(Bound);
        }
#pragma warning disable CA1031 // the connection ending is what the test just asked for
        catch (Exception)
#pragma warning restore CA1031
        {
        }
    }

    /// <summary>
    /// A tracked send whose message will never be written completes anyway. What a caller waits for is that
    /// the message is no longer its own, and a reservation nobody releases is a leak.
    /// </summary>
    [Test]
    public async Task ATrackedSendOnAConnectionThatIsGoingAwayStillCompletes()
    {
        await using var pair = await SocketPair.CreateAsync();

        var connection = new WebSocketConnection(pair.Server, maxMessageBytes: 1 << 20);

        // Never run, so nothing will ever drain the queue; disposing is what has to answer the caller.
        var tracked = connection.SendTrackedAsync("{\"id\":1}").AsTask();
        tracked.IsCompleted.Should().BeFalse("nothing has written it yet");

        await connection.DisposeAsync();
        await tracked.WaitAsync(Bound);

        // And one made after the queue is closed is answered immediately rather than parked forever.
        await connection.SendTrackedAsync("{\"id\":2}").AsTask().WaitAsync(Bound);
    }

    /// <summary>Two ends of one real socket, already upgraded, which is where a connection starts.</summary>
    private sealed class SocketPair : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly TcpClient _clientSocket;
        private readonly TcpClient _serverSocket;
        private readonly WebSocket _client;

        private SocketPair(TcpListener listener, TcpClient clientSocket, TcpClient serverSocket, WebSocket client, WebSocket server)
        {
            _listener = listener;
            _clientSocket = clientSocket;
            _serverSocket = serverSocket;
            _client = client;
            Server = server;
        }

        internal WebSocket Server { get; }

        internal static async Task<SocketPair> CreateAsync()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();

            var clientSocket = new TcpClient();
            var connecting = clientSocket.ConnectAsync(IPAddress.Loopback, ((IPEndPoint) listener.LocalEndpoint).Port);
            var serverSocket = await listener.AcceptTcpClientAsync().WaitAsync(Bound).ConfigureAwait(false);
            await connecting.WaitAsync(Bound).ConfigureAwait(false);

            // The handshake belongs to the HTTP layer above; this is the socket as a connection receives it.
            var server = WebSocket.CreateFromStream(serverSocket.GetStream(), isServer: true, subProtocol: null, Timeout.InfiniteTimeSpan);
            var client = WebSocket.CreateFromStream(clientSocket.GetStream(), isServer: false, subProtocol: null, Timeout.InfiniteTimeSpan);

            return new SocketPair(listener, clientSocket, serverSocket, client, server);
        }

        internal async Task<string> ReceiveAsync()
        {
            var buffer = new byte[4096];
            var received = await _client.ReceiveAsync(buffer.AsMemory(), CancellationToken.None).ConfigureAwait(false);
            return Encoding.UTF8.GetString(buffer, 0, received.Count);
        }

        internal async Task CloseAsync()
        {
            try
            {
                if (_client.State == WebSocketState.Open)
                {
                    await _client.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, statusDescription: null, CancellationToken.None).ConfigureAwait(false);
                }
            }
#pragma warning disable CA1031 // a socket that will not close politely closes rudely, which is fine here
            catch (Exception)
#pragma warning restore CA1031
            {
            }
        }

        public async ValueTask DisposeAsync()
        {
            await CloseAsync().ConfigureAwait(false);

            _client.Dispose();
            Server.Dispose();
            _clientSocket.Dispose();
            _serverSocket.Dispose();
            _listener.Stop();
        }
    }
}
