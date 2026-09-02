using System.Buffers;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;

namespace Jint.DevTools.Transport;

/// <summary>
/// One client's WebSocket: a reader task that hands text to the session, and a single writer task that puts
/// text back on the wire.
/// </summary>
/// <remarks>
/// <para>
/// <b>One writer, always.</b> Responses come from whichever thread answered a command and events come from
/// the engine thread; two of them calling <see cref="WebSocket.SendAsync(ReadOnlyMemory{byte},
/// WebSocketMessageType, bool, CancellationToken)"/> at once would interleave inside a frame and hand the
/// client a broken message. Everything therefore goes through a channel that exactly one task drains.
/// </para>
/// <para>
/// <b>Sending never blocks the caller.</b> The channel is unbounded and the write is a queue insertion, which
/// is what lets the engine thread emit an event without waiting on a socket. The cost of that decision is
/// that a client which stops reading grows the queue; the connection's own message bound does not cover it,
/// and a bound on the queue would mean either dropping events or blocking the engine, both of which are
/// worse than the memory.
/// </para>
/// <para>
/// <b>Text frames only.</b> The protocol is JSON; a binary frame is closed with <c>1003</c> and a message
/// over <see cref="DevToolsServerOptions.MaxMessageBytes"/> with <c>1009</c>, rather than being buffered
/// until the host runs out of memory.
/// </para>
/// </remarks>
internal sealed class WebSocketConnection : IDevToolsConnection, IAsyncDisposable
{
    /// <summary>How long a closing connection waits for what is still queued to reach the wire.</summary>
    private static readonly TimeSpan FlushBound = TimeSpan.FromSeconds(5);

    private readonly WebSocket _socket;
    private readonly int _maxMessageBytes;

    /// <summary>
    /// What the single writer task drains. A <see langword="null"/> item is the close request: it travels
    /// the queue like any other message, so everything already queued -- the reply to the very command that
    /// asked for the close -- reaches the client before the socket goes.
    /// </summary>
    private readonly Channel<string?> _outgoing = Channel.CreateUnbounded<string?>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false,
    });

    private int _closed;
    private int _closeRequested;

    internal WebSocketConnection(WebSocket socket, int maxMessageBytes)
    {
        _socket = socket;
        _maxMessageBytes = maxMessageBytes;
    }

    /// <inheritdoc/>
    public Func<string, CancellationToken, ValueTask>? MessageReceived { get; set; }

    /// <inheritdoc/>
    public Action? Closed { get; set; }

    /// <inheritdoc/>
    public ValueTask SendAsync(string message, CancellationToken cancellationToken = default)
    {
        _outgoing.Writer.TryWrite(message);
        return default;
    }

    /// <summary>
    /// Asks for the connection to close once the command that asked for it has been answered.
    /// </summary>
    /// <remarks>
    /// What <c>Browser.close</c> becomes when the host reads it as a disconnect, and the ordering is the
    /// whole of it: a command asks for this from <i>inside</i> its own dispatch, so its reply has not been
    /// written yet. Closing here would drop that reply and the client would see a hang instead of the clean
    /// goodbye it asked for. So this only raises a flag; the reader queues the close behind the reply once
    /// the command has finished, and the writer honours it once everything queued is on the wire.
    /// </remarks>
    internal void RequestClose()
    {
        Volatile.Write(ref _closeRequested, 1);
    }

    /// <summary>
    /// Runs the connection until the client goes away or <paramref name="cancellationToken"/> is cancelled.
    /// </summary>
    internal async Task RunAsync(CancellationToken cancellationToken)
    {
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var writer = Task.Run(() => WriteAsync(stopping.Token), CancellationToken.None);

        try
        {
            await ReadAsync(stopping.Token).ConfigureAwait(false);
        }
        finally
        {
            // Completing rather than cancelling, and waiting before cancelling: the writer drains what is
            // still queued -- including the reply to whatever ended the connection -- and only then is the
            // token pulled out from under it.
            _outgoing.Writer.TryComplete();

            try
            {
                await writer.WaitAsync(FlushBound, CancellationToken.None).ConfigureAwait(false);
            }
#pragma warning disable CA1031 // the writer's failure is the connection ending, which is what we are doing
            catch (Exception)
#pragma warning restore CA1031
            {
            }

            await stopping.CancelAsync().ConfigureAwait(false);

            try
            {
                await writer.ConfigureAwait(false);
            }
#pragma warning disable CA1031 // the writer's failure is the connection ending, which is what we are doing
            catch (Exception)
#pragma warning restore CA1031
            {
            }

            RaiseClosed();
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        _outgoing.Writer.TryComplete();

        try
        {
            if (_socket.State == WebSocketState.Open)
            {
                await _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, statusDescription: null, CancellationToken.None).ConfigureAwait(false);
            }
        }
#pragma warning disable CA1031 // a socket that cannot be closed politely is closed rudely, and that is fine
        catch (Exception)
#pragma warning restore CA1031
        {
        }

        _socket.Dispose();
        RaiseClosed();
    }

    private async Task ReadAsync(CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(8 * 1024);
        var message = new ArrayBufferWriter<byte>(8 * 1024);

        try
        {
            while (!cancellationToken.IsCancellationRequested && _socket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult received;
                try
                {
                    received = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken).ConfigureAwait(false);
                }
                catch (WebSocketException)
                {
                    // A client that vanished rather than closed. There is nothing to say to it.
                    return;
                }

                if (received.MessageType == WebSocketMessageType.Close)
                {
                    await CloseAsync(WebSocketCloseStatus.NormalClosure, statusDescription: null).ConfigureAwait(false);
                    return;
                }

                if (received.MessageType == WebSocketMessageType.Binary)
                {
                    await CloseAsync(WebSocketCloseStatus.InvalidMessageType, "the DevTools protocol is text").ConfigureAwait(false);
                    return;
                }

                if (message.WrittenCount + received.Count > _maxMessageBytes)
                {
                    await CloseAsync(WebSocketCloseStatus.MessageTooBig, "message exceeds MaxMessageBytes").ConfigureAwait(false);
                    return;
                }

                message.Write(buffer.AsSpan(0, received.Count));

                if (!received.EndOfMessage)
                {
                    continue;
                }

                var text = Encoding.UTF8.GetString(message.WrittenSpan);
                message.Clear();

                if (MessageReceived is { } handler)
                {
                    await handler(text, cancellationToken).ConfigureAwait(false);
                }

                if (Volatile.Read(ref _closeRequested) != 0)
                {
                    // The command's reply is already queued, so the close goes in behind it.
                    _outgoing.Writer.TryWrite(null);
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The server is stopping, or the connection is being disposed.
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private async Task WriteAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (await _outgoing.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                while (_outgoing.Reader.TryRead(out var message))
                {
                    if (_socket.State != WebSocketState.Open)
                    {
                        return;
                    }

                    if (message is null)
                    {
                        await CloseAsync(WebSocketCloseStatus.NormalClosure, statusDescription: null).ConfigureAwait(false);
                        return;
                    }

                    var bytes = Encoding.UTF8.GetBytes(message);
                    await _socket.SendAsync(bytes.AsMemory(), WebSocketMessageType.Text, endOfMessage: true, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (WebSocketException)
        {
            // The client went away mid-write. The reader notices too, and the connection ends there.
        }
    }

    private async Task CloseAsync(WebSocketCloseStatus status, string? statusDescription)
    {
        try
        {
            if (_socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                await _socket.CloseAsync(status, statusDescription, CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (WebSocketException)
        {
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void RaiseClosed()
    {
        if (Interlocked.Exchange(ref _closed, 1) != 0)
        {
            return;
        }

        Closed?.Invoke();
    }
}
