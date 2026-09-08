namespace Jint.DevTools.Transport;

/// <summary>
/// One client's connection: a bidirectional stream of protocol messages, each a complete JSON document.
/// </summary>
/// <remarks>
/// <para>
/// A transport moves <see langword="string"/>s and nothing else. That is the whole reason the interface is
/// this narrow: whatever thread a WebSocket receive loop happens to be on, the only thing it may hand the
/// session is text, and the session is what brings it to the engine thread. Nothing here ever sees a
/// <c>JsValue</c>.
/// </para>
/// <para>
/// <see cref="MessageReceived"/> and <see cref="Closed"/> are set by whatever owns the session, once,
/// before the transport starts delivering.
/// </para>
/// </remarks>
internal interface IDevToolsConnection
{
    /// <summary>Sends one complete protocol message to the client.</summary>
    ValueTask SendAsync(string message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends one message and completes when it has actually left this process, rather than when it was
    /// queued.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="cancellationToken">Cancelled when the client goes away.</param>
    /// <remarks>
    /// <para>
    /// <b>The difference from <see cref="SendAsync"/> is the whole point.</b> A queue-and-return send says
    /// nothing about how long the message keeps occupying memory, so a caller holding a reservation for the
    /// bytes it has just encoded has no moment at which to release it — and repeated commands could queue
    /// unboundedly many encoded copies behind a slow socket. Awaiting this is that moment.
    /// </para>
    /// <para>
    /// <b>It completes either way.</b> A write that failed, a connection that closed and a queue that was
    /// abandoned all complete it, because what a caller waits for here is "this message is no longer mine",
    /// not "the client received it". A transport failure is never thrown out of it.
    /// </para>
    /// </remarks>
    ValueTask SendTrackedAsync(string message, CancellationToken cancellationToken = default);

    /// <summary>Gets or sets what one received message is handed to.</summary>
    Func<string, CancellationToken, ValueTask>? MessageReceived { get; set; }

    /// <summary>Gets or sets what is run once when the client goes away.</summary>
    Action? Closed { get; set; }
}
