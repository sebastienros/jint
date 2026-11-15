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
    private int _awaitingScript;
    private Action<string>? _stopped;

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

    /// <summary>
    /// Gets whether the command's answer waits on a promise reaction of the engine that ran it.
    /// </summary>
    /// <remarks>Read by the transport thread waiting on the reply, so it is a volatile read.</remarks>
    internal bool AwaitsScript => Volatile.Read(ref _awaitingScript) != 0;

    /// <summary>
    /// Says that the command's answer now waits on a promise reaction of the engine that ran it, such as
    /// <c>Runtime.evaluate</c> with <c>awaitPromise</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Only a wait on script.</b> Such a command is answered by a later job of a loop that is demonstrably
    /// being pumped, so <see cref="DevToolsServerOptions.CommandTimeout"/> — which diagnoses a loop nobody
    /// pumps — no longer bounds the client's wait, as Chrome sets no bound on it; and it is answered
    /// <c>Execution context was destroyed.</c> when that engine is replaced, because a promise of a realm
    /// that is gone never settles.
    /// </para>
    /// <para>
    /// A command that awaits host work — <c>Page.navigate</c> awaiting the commit that replaces the engine —
    /// must not say this: the swap is how it completes, not what destroys it.
    /// </para>
    /// <para>
    /// <paramref name="stopped"/> is how the command is answered when an execution limit ends the turn it
    /// ran in before the promise settled: the reaction it waits on was queued in that turn's checkpoint and is
    /// gone with it. It is handed the sentence naming the limit, and it is called on the engine thread.
    /// </para>
    /// </remarks>
    internal void MarkAwaitingScript(Action<string>? stopped = null)
    {
        _stopped = stopped;
        Volatile.Write(ref _awaitingScript, 1);
    }

    /// <summary>Gets what answers this command when an execution limit stops the turn it awaited in.</summary>
    /// <remarks><see langword="null"/> unless <see cref="MarkAwaitingScript"/> was handed one.</remarks>
    internal Action<string>? Stopped => _stopped;

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
