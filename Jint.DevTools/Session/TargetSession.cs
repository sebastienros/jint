using Jint.DevTools.Domains;

namespace Jint.DevTools.Session;

/// <summary>
/// One client's conversation with one engine: the domains that hold that engine's state, and the mailbox
/// that brings a command to the thread allowed to touch it.
/// </summary>
/// <remarks>
/// <para>
/// Made two ways, and the difference is only which session node it hangs off. A client that attached through
/// <c>Target.attachToTarget</c> gets a child node whose <c>sessionId</c> rides every message; a client that
/// connected straight to <c>/devtools/page/&lt;targetId&gt;</c> gets the root node itself, and its messages
/// carry no <c>sessionId</c> at all. Everything below that is the same.
/// </para>
/// <para>
/// Detaching releases what this session owned and nothing the target owns: the engine and its thread belong
/// to whoever made the target.
/// </para>
/// </remarks>
internal sealed class TargetSession
{
    private readonly BrowserSession? _browser;
    private readonly TargetDomains _domains;

    private int _detached;

    private TargetSession(DevToolsSession session, DevToolsTarget target, BrowserSession? browser)
    {
        Session = session;
        Target = target;
        _browser = browser;

        // Everything registered below holds engine state, so every command addressed here crosses to the
        // engine thread first. That is the whole of the thread rule, in one line -- and the gateway is the
        // target rather than one mailbox, because a page replaces its mailbox with its engine on every
        // navigation and a session that captured one would queue for a document that has gone.
        session.UseGateway(target);
        _domains = target.RegisterDomains(session, browser);
    }

    /// <summary>Gets the session node this attachment answers on.</summary>
    internal DevToolsSession Session { get; }

    /// <summary>Gets the engine this session speaks to.</summary>
    internal DevToolsTarget Target { get; }

    /// <summary>
    /// Gets the identifier a client addresses this session by, or <see langword="null"/> for a direct
    /// connection.
    /// </summary>
    internal string? SessionId => Session.SessionId;

    /// <summary>Attaches to <paramref name="target"/> under a new child of <paramref name="browser"/>.</summary>
    internal static TargetSession Attach(BrowserSession browser, DevToolsTarget target, string sessionId)
    {
        return new TargetSession(browser.Session.CreateChild(sessionId), target, browser);
    }

    /// <summary>Builds the session a direct <c>/devtools/page/</c> connection speaks over.</summary>
    internal static TargetSession Direct(DevToolsSession root, DevToolsTarget target)
    {
        return new TargetSession(root, target, browser: null);
    }

    /// <summary>Releases what this session owns, once.</summary>
    /// <remarks>
    /// Runs on a transport thread, and both halves are chosen so that it can: removing a child session
    /// touches a dictionary, and releasing this attachment's remote-object handles drops references without
    /// running a line of script. Nothing here reaches the engine, so a detach is answered rather than queued
    /// behind whatever that engine is busy with — including a target still waiting for a debugger.
    /// </remarks>
    internal void Detach()
    {
        if (Interlocked.Exchange(ref _detached, 1) != 0)
        {
            return;
        }

        if (SessionId is { } sessionId)
        {
            _browser?.Session.RemoveChild(sessionId);
        }

        _domains.Detach();
    }
}
