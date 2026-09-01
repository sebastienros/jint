namespace Jint.DevTools.Transport;

/// <summary>
/// A connection with no socket under it: messages go in through <see cref="PostAsync"/> and come out into
/// <see cref="Sent"/>.
/// </summary>
/// <remarks>
/// <para>
/// It is what the protocol tests drive, and it is also the shape a host embeds when it wants to speak the
/// protocol without opening a port — a test harness, an in-process inspector, a transport of the host's own.
/// The WebSocket transport arrives separately and implements the same interface.
/// </para>
/// <para>
/// The sent list is guarded by a lock because a domain may emit an event from whichever thread the host
/// pumped the engine on, while the test thread is reading. Nothing else here is thread-safe, and nothing
/// else needs to be.
/// </para>
/// </remarks>
internal sealed class InProcessConnection : IDevToolsConnection
{
    private readonly List<string> _sent = [];

    /// <inheritdoc/>
    public Func<string, CancellationToken, ValueTask>? MessageReceived { get; set; }

    /// <inheritdoc/>
    public Action? Closed { get; set; }

    /// <summary>Gets every message the session has sent, oldest first.</summary>
    internal IReadOnlyList<string> Sent
    {
        get
        {
            lock (_sent)
            {
                return _sent.ToArray();
            }
        }
    }

    /// <inheritdoc/>
    public ValueTask SendAsync(string message, CancellationToken cancellationToken = default)
    {
        lock (_sent)
        {
            _sent.Add(message);
        }

        return default;
    }

    /// <summary>Delivers one message as if a client had sent it.</summary>
    internal ValueTask PostAsync(string message, CancellationToken cancellationToken = default)
    {
        return MessageReceived is { } handler ? handler(message, cancellationToken) : default;
    }

    /// <summary>Reports the client as gone, once.</summary>
    internal void Close()
    {
        var closed = Closed;
        Closed = null;
        closed?.Invoke();
    }
}
