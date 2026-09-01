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
}
