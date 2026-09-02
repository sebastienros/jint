using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Jint.Tests.DevTools.Transport;

/// <summary>
/// The client half of the socket tests: a <see cref="ClientWebSocket"/> with an identifier counter and a
/// bounded wait for each reply.
/// </summary>
/// <remarks>
/// <para>
/// Every wait is bounded, without exception. A protocol test that can hang is a CI leg that can hang, and
/// the thing most likely to be wrong in this area is exactly the thing that makes a reply never arrive.
/// </para>
/// <para>
/// <b>The bound is generous on purpose.</b> It exists to stop a hang, not to assert a speed: this suite
/// finishes in under a second unloaded, and on a two-core CI runner sharing the machine with four other
/// test processes it has been seen to take four and a half minutes. A bound tight enough to be interesting
/// is a bound that fails on a busy runner and says nothing about the server.
/// </para>
/// </remarks>
internal sealed class DevToolsClient : IAsyncDisposable
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(120);

    private readonly ClientWebSocket _socket = new();
    private readonly List<JsonElement> _received = [];
    private readonly CancellationTokenSource _stopping = new();

    private Task? _reader;
    private int _nextId;

    private DevToolsClient()
    {
    }

    /// <summary>Connects to <paramref name="url"/> and starts reading.</summary>
    internal static async Task<DevToolsClient> ConnectAsync(string url)
    {
        var client = new DevToolsClient();

        using var connecting = new CancellationTokenSource(Bound);
        await client._socket.ConnectAsync(new Uri(url), connecting.Token).ConfigureAwait(false);

        client._reader = Task.Run(() => client.ReadAsync(client._stopping.Token), CancellationToken.None);
        return client;
    }

    /// <summary>Fetches one of the server's discovery documents.</summary>
    internal static async Task<(int Status, string Body)> GetAsync(string url)
    {
        using var http = new HttpClient { Timeout = Bound };
        using var response = await http.GetAsync(new Uri(url)).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        return ((int) response.StatusCode, body);
    }

    /// <summary>Sends one command and waits for its reply.</summary>
    internal async Task<JsonElement> SendAsync(string method, string? parameters = null, string? sessionId = null)
    {
        var identifier = Interlocked.Increment(ref _nextId);
        var payload = parameters is null ? "" : ",\"params\":" + parameters;
        var session = sessionId is null ? "" : ",\"sessionId\":\"" + sessionId + "\"";
        var message = $$"""{"id":{{identifier}},"method":"{{method}}"{{payload}}{{session}}}""";

        using var sending = new CancellationTokenSource(Bound);
        await _socket.SendAsync(Encoding.UTF8.GetBytes(message), WebSocketMessageType.Text, endOfMessage: true, sending.Token).ConfigureAwait(false);

        return await WaitAsync(candidate =>
            candidate.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.Number && id.GetInt32() == identifier).ConfigureAwait(false);
    }

    /// <summary>Sends one command and hands back its <c>result</c>, failing the test when it errored.</summary>
    internal async Task<JsonElement> ResultOfAsync(string method, string? parameters = null, string? sessionId = null)
    {
        var reply = await SendAsync(method, parameters, sessionId).ConfigureAwait(false);
        reply.TryGetProperty("error", out var error).Should().BeFalse("'{0}' was expected to succeed, and it answered {1}", method, error);
        return reply.GetProperty("result");
    }

    /// <summary>Waits for the first event of <paramref name="method"/>, whenever it arrives.</summary>
    internal Task<JsonElement> WaitForEventAsync(string method)
    {
        return WaitAsync(candidate => candidate.TryGetProperty("method", out var name) && name.GetString() == method);
    }

    /// <summary>Sends a raw message, for the cases where the point is that it is not well formed.</summary>
    internal async Task SendRawAsync(string message)
    {
        using var sending = new CancellationTokenSource(Bound);
        await _socket.SendAsync(Encoding.UTF8.GetBytes(message), WebSocketMessageType.Text, endOfMessage: true, sending.Token).ConfigureAwait(false);
    }

    /// <summary>Sends a binary frame, which the protocol has no use for and the server refuses.</summary>
    internal async Task SendBinaryAsync()
    {
        using var sending = new CancellationTokenSource(Bound);
        await _socket.SendAsync(new byte[] { 1, 2, 3 }, WebSocketMessageType.Binary, endOfMessage: true, sending.Token).ConfigureAwait(false);
    }

    /// <summary>Waits until the server has closed the connection.</summary>
    internal async Task<WebSocketCloseStatus?> WaitForCloseAsync()
    {
        var deadline = DateTime.UtcNow + Bound;
        while (DateTime.UtcNow < deadline)
        {
            if (_socket.State is WebSocketState.Closed or WebSocketState.CloseReceived or WebSocketState.Aborted)
            {
                return _socket.CloseStatus;
            }

            await Task.Delay(20).ConfigureAwait(false);
        }

        throw new TimeoutException("the server did not close the connection");
    }

    /// <summary>
    /// Ends the conversation by closing the stream, not by pulling it out from under itself.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The close goes first, and the order is the whole of it.</b> Cancelling a
    /// <see cref="ClientWebSocket.ReceiveAsync(ArraySegment{byte}, CancellationToken)"/> does not cancel a
    /// receive — the class has no way to — it <i>aborts</i> the socket, which resets the connection and lets
    /// the peer's kernel discard whatever it had not read yet. On a fast machine there is nothing left to
    /// discard; on a loaded one the last command this client sent is still in the server's receive buffer,
    /// and it vanishes. That is what made
    /// <c>WebSocketServerTests.AClientClosingMidCommandLeavesTheEngineRunning</c> fail on the ARM leg with
    /// <c>Expected number but got Undefined</c>: the command it had just sent was never read, so the marker
    /// it sets was never set.
    /// </para>
    /// <para>
    /// <see cref="WebSocket.CloseOutputAsync"/> rather than <see cref="WebSocket.CloseAsync"/> because the
    /// reader task is inside a receive: <c>CloseAsync</c> waits for the reply by receiving, and two
    /// concurrent receives on one <see cref="ClientWebSocket"/> are not allowed. Sending the close frame and
    /// letting the reader see the server's answer ends the stream in order.
    /// </para>
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_socket.State == WebSocketState.Open)
            {
                using var closing = new CancellationTokenSource(Bound);
                await _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, statusDescription: null, closing.Token).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is WebSocketException or OperationCanceledException or ObjectDisposedException)
        {
        }

        // Bounded, like every other wait here: a server that never answers the close must end this test
        // rather than hang it, and the cancellation below is what unblocks the receive when it does not.
        await SettledAsync(Bound).ConfigureAwait(false);

        await _stopping.CancelAsync().ConfigureAwait(false);

        // And again, briefly, so that the socket is never disposed under a live receive.
        await SettledAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

        _socket.Dispose();
        _stopping.Dispose();
    }

    /// <summary>Waits for the reader to finish, swallowing every way it can end.</summary>
    private async Task SettledAsync(TimeSpan bound)
    {
        if (_reader is not { } reader)
        {
            return;
        }

        try
        {
            await reader.WaitAsync(bound).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // a disposing client has nothing left to report a reader's failure to
        catch (Exception)
#pragma warning restore CA1031
        {
        }
    }

    private async Task<JsonElement> WaitAsync(Func<JsonElement, bool> matches)
    {
        var deadline = DateTime.UtcNow + Bound;
        var seen = 0;

        while (DateTime.UtcNow < deadline)
        {
            lock (_received)
            {
                for (; seen < _received.Count; seen++)
                {
                    if (matches(_received[seen]))
                    {
                        return _received[seen];
                    }
                }
            }

            await Task.Delay(10).ConfigureAwait(false);
        }

        throw new TimeoutException("no matching message arrived within " + Bound);
    }

    private async Task ReadAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[16 * 1024];
        var message = new List<byte>();

        try
        {
            while (!cancellationToken.IsCancellationRequested && _socket.State == WebSocketState.Open)
            {
                var received = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken).ConfigureAwait(false);
                if (received.MessageType == WebSocketMessageType.Close)
                {
                    return;
                }

                message.AddRange(buffer.AsSpan(0, received.Count).ToArray());
                if (!received.EndOfMessage)
                {
                    continue;
                }

                using var document = JsonDocument.Parse(Encoding.UTF8.GetString(message.ToArray()));
                message.Clear();

                lock (_received)
                {
                    _received.Add(document.RootElement.Clone());
                }
            }
        }
        catch (Exception exception) when (exception is WebSocketException or OperationCanceledException)
        {
        }
    }
}
