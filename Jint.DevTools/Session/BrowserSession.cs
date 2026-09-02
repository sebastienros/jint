using Jint.DevTools.Domains;
using Jint.DevTools.Transport;

namespace Jint.DevTools.Session;

/// <summary>
/// One client's conversation with the server: the browser-level domains, and every attachment it has made.
/// </summary>
/// <remarks>
/// <para>
/// What a connection to <c>/devtools/browser/&lt;browserId&gt;</c> becomes. Nothing registered here touches
/// an engine — <c>Schema</c>, <c>Browser</c> and <c>Target</c> are all bookkeeping — so its commands are
/// answered on the transport thread with no mailbox in between. Only the attachments it mints have a gateway.
/// </para>
/// <para>
/// A target appearing or disappearing on the server reaches every browser session, which is what makes
/// <c>setDiscoverTargets</c> and <c>setAutoAttach</c> mean anything after the fact rather than only at the
/// moment they were sent.
/// </para>
/// </remarks>
internal sealed class BrowserSession
{
    private readonly Dictionary<string, TargetSession> _attached = new(StringComparer.Ordinal);
    private readonly TargetDomain _targets;

    internal BrowserSession(DevToolsServer server, IDevToolsConnection connection, Action? closeRequested)
    {
        Server = server;
        Session = new DevToolsSession(connection);
        _targets = new TargetDomain(this, nested: false);

        BuiltInDomains.RegisterBrowserDomains(Session, server.Version, closeRequested, _targets, server);
    }

    /// <summary>Gets the server whose targets this session sees.</summary>
    internal DevToolsServer Server { get; }

    /// <summary>Gets the root session node this conversation answers on.</summary>
    internal DevToolsSession Session { get; }

    /// <summary>Attaches to <paramref name="target"/>, minting the session a client then addresses it by.</summary>
    /// <param name="target">The engine to attach to.</param>
    /// <param name="created">
    /// Whether this call is what made the attachment. The check and the mint are one locked step, so two
    /// threads racing to attach the same target -- a client's <c>attachToTarget</c> and a host's
    /// <c>AddTarget</c> reaching auto-attach -- produce one attachment and one announcement rather than two.
    /// </param>
    /// <returns>The session identifier, new or existing.</returns>
    internal string Attach(DevToolsTarget target, out bool created)
    {
        lock (_attached)
        {
            foreach (var existing in _attached)
            {
                if (ReferenceEquals(existing.Value.Target, target))
                {
                    created = false;
                    return existing.Key;
                }
            }

            var sessionId = Identifiers.New();
            _attached.Add(sessionId, TargetSession.Attach(this, target, sessionId));
            created = true;
            return sessionId;
        }
    }

    /// <summary>Detaches one session, answering the target it was attached to.</summary>
    internal DevToolsTarget? Detach(string sessionId)
    {
        TargetSession? attached;
        lock (_attached)
        {
            if (!_attached.Remove(sessionId, out attached))
            {
                return null;
            }
        }

        attached.Detach();
        return attached.Target;
    }

    /// <summary>Answers whether this session is attached to <paramref name="target"/>, and under which identifier.</summary>
    internal string? SessionIdOf(DevToolsTarget target)
    {
        lock (_attached)
        {
            foreach (var existing in _attached)
            {
                if (ReferenceEquals(existing.Value.Target, target))
                {
                    return existing.Key;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Detaches every attachment this session made, which is what a connection going away means.
    /// </summary>
    /// <remarks>
    /// Nothing is announced: the client this would have been announced to is the one that has gone.
    /// </remarks>
    internal void DetachAll()
    {
        TargetSession[] sessions;

        lock (_attached)
        {
            sessions = new TargetSession[_attached.Count];
            _attached.Values.CopyTo(sessions, 0);
            _attached.Clear();
        }

        foreach (var session in sessions)
        {
            session.Detach();
        }
    }

    /// <summary>Tells this session that a target appeared on the server.</summary>
    internal ValueTask TargetAddedAsync(DevToolsTarget target, CancellationToken cancellationToken)
        => _targets.TargetAddedAsync(target, cancellationToken);

    /// <summary>Tells this session that a target's title or location moved.</summary>
    internal ValueTask TargetInfoChangedAsync(DevToolsTarget target, CancellationToken cancellationToken)
        => _targets.TargetInfoChangedAsync(target, cancellationToken);

    /// <summary>Tells this session that a target went away.</summary>
    internal ValueTask TargetRemovedAsync(DevToolsTarget target, CancellationToken cancellationToken)
        => _targets.TargetRemovedAsync(target, cancellationToken);
}
