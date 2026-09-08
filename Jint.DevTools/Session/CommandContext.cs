namespace Jint.DevTools.Session;

/// <summary>
/// What one command is answered against: the session that received it, the session identifier it carries,
/// and the token that says the client has gone.
/// </summary>
/// <remarks>
/// <para>
/// A class rather than a record struct, and deliberately: it carries a reference the callee follows, an
/// <c>async</c> override may not take an <c>in</c> parameter, and the flattened-session and engine-dispatcher
/// work adds fields to it rather than a second argument to every command.
/// </para>
/// <para>
/// A command runs on the engine thread. The context is what a domain reaches the session through, and
/// nothing on it may be captured past the command that received it.
/// </para>
/// </remarks>
internal sealed class CommandContext
{
    private List<IDisposable>? _held;

    internal CommandContext(DevToolsSession session, string? sessionId, CancellationToken cancellationToken)
    {
        Session = session;
        SessionId = sessionId;
        CancellationToken = cancellationToken;
    }

    /// <summary>Gets the session the command arrived on.</summary>
    internal DevToolsSession Session { get; }

    /// <summary>
    /// Gets the <c>sessionId</c> the client addressed the command to, which the response echoes back.
    /// </summary>
    internal string? SessionId { get; }

    /// <summary>Gets the token cancelled when the client disconnects.</summary>
    internal CancellationToken CancellationToken { get; }

    /// <summary>Gets whether anything is holding memory until this command's reply has been written.</summary>
    internal bool HoldsUntilReplyWritten => _held is { Count: > 0 };

    /// <summary>
    /// Keeps <paramref name="lease"/> alive until this command's reply has actually left the process.
    /// </summary>
    /// <param name="lease">What to dispose then, or <see langword="null"/> for nothing.</param>
    /// <remarks>
    /// <para>
    /// <b>A result is not free the moment it is returned.</b> A command answering with a large encoded
    /// payload reserved memory for it, and the reservation has to outlive the command by exactly as long as
    /// the string does — until the transport has written it. Registering the lease here is what makes the
    /// session release it at that moment rather than at the end of the dispatch, which is what stops
    /// repeated commands queueing unboundedly many encoded copies behind a slow socket.
    /// </para>
    /// <para>
    /// The session disposes it exactly once, whether the reply was written, the write failed, or the command
    /// ended in an error instead. The context itself must still not be captured past the command.
    /// </para>
    /// </remarks>
    internal void HoldUntilReplyWritten(IDisposable? lease)
    {
        if (lease is not null)
        {
            (_held ??= []).Add(lease);
        }
    }

    /// <summary>Disposes what this command was holding. Idempotent, and called by the session alone.</summary>
    internal void ReleaseHeld()
    {
        if (_held is not { } held)
        {
            return;
        }

        _held = null;
        foreach (var lease in held)
        {
            lease.Dispose();
        }
    }
}
