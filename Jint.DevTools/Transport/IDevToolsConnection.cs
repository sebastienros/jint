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

    /// <summary>Gets or sets what one received message is handed to.</summary>
    Func<string, CancellationToken, ValueTask>? MessageReceived { get; set; }

    /// <summary>Gets or sets what is run once when the client goes away.</summary>
    Action? Closed { get; set; }
}
