using Jint.DevTools.Protocol.Browser;
using Jint.DevTools.Session;

namespace Jint.DevTools.Domains;

/// <summary>
/// The domains this package answers, named in one place.
/// </summary>
/// <remarks>
/// <para>
/// This file and <c>manifest.json</c>'s <c>implementedMethods</c> are two statements of the same fact, and
/// <c>Jint.Tests.DevTools/Protocol/ProtocolManifestTests.cs</c> holds them to each other: every listed
/// method is overridden here, and nothing else is. Adding a domain without a manifest entry, or the other
/// way round, fails rather than ships.
/// </para>
/// <para>
/// There are two lists rather than one because there are two kinds of session. A browser session answers
/// about the server — which engines exist, what the product is — and touches no engine, so its commands run
/// on the transport thread. A target session answers about one engine and every command of it crosses to
/// that engine's thread first.
/// </para>
/// </remarks>
internal static class BuiltInDomains
{
    /// <summary>Registers what a connection to the browser endpoint answers.</summary>
    /// <param name="session">The root session of that connection.</param>
    /// <param name="version">What <c>Browser.getVersion</c> answers.</param>
    /// <param name="closeRequested">What to run when a client sends <c>Browser.close</c>, if anything.</param>
    /// <param name="targets">The <c>Target</c> domain the conversation keeps its discovery state on.</param>
    internal static DevToolsSession RegisterBrowserDomains(
        DevToolsSession session,
        GetVersionResponse version,
        Action? closeRequested,
        TargetDomain targets)
    {
        if (session is null)
        {
            Throw.ArgumentNull(nameof(session));
        }

        return session
            .Register(new SchemaDomain())
            .Register(new BrowserDomain(version, closeRequested))
            .Register(targets);
    }

    /// <summary>Registers what one attachment to one engine answers.</summary>
    /// <param name="session">The session node the attachment answers on.</param>
    /// <param name="target">The engine it speaks to.</param>
    /// <param name="browser">
    /// The conversation the attachment belongs to, or <see langword="null"/> for a direct
    /// <c>/devtools/page/</c> connection, which has no browser session and therefore no target tree.
    /// </param>
    internal static DevToolsSession RegisterTargetDomains(DevToolsSession session, EngineTarget target, BrowserSession? browser)
    {
        if (session is null)
        {
            Throw.ArgumentNull(nameof(session));
        }

        session.Register(new RuntimeDomain(target));

        if (browser is not null)
        {
            // Clients walk down the target tree by sending setAutoAttach on every session they are given.
            // An engine target has no children, so the nested copy answers it as the success it is.
            session.Register(new TargetDomain(browser, nested: true));
        }

        return session;
    }
}
