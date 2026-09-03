using System.Runtime.InteropServices;

namespace Jint.DevTools;

/// <summary>
/// What mints a target when a client asks for one, and what partitions the targets it mints.
/// </summary>
/// <remarks>
/// <para>
/// <c>Target.createTarget</c> and <c>Target.createBrowserContext</c> are the two commands every automation
/// client sends before it can do anything at all, and an engine-level server has no honest answer to either:
/// there is nothing to open and nothing to partition. A host that <i>does</i> — <c>Jint.Browser</c>, which is
/// AngleSharp plus Jint — registers one of these, and the <c>Target</c> domain routes those commands through
/// it instead of refusing them.
/// </para>
/// <para>
/// <b>Registering a host changes what those commands mean and nothing else.</b> Targets a host added itself
/// keep working exactly as before, discovery and attachment are unchanged, and a server with no host behaves
/// as it always did — which is what keeps this seam from being a second server.
/// </para>
/// <para>
/// Every member runs on a transport thread: a host that has to reach an engine posts to that engine's own
/// mailbox, like everything else in this package.
/// </para>
/// </remarks>
internal interface ITargetHost
{
    /// <summary>Gets the browser contexts that exist, which <c>Target.getBrowserContexts</c> answers.</summary>
    /// <remarks>
    /// The default context is deliberately absent, as it is in Chrome: a client reads this list to find the
    /// contexts <i>it</i> created.
    /// </remarks>
    IReadOnlyList<string> BrowserContextIds { get; }

    /// <summary>Creates one browser context and answers the identifier a client addresses it by.</summary>
    /// <param name="cancellationToken">Cancels the creation.</param>
    ValueTask<string> CreateBrowserContextAsync(CancellationToken cancellationToken);

    /// <summary>Disposes one browser context and everything in it.</summary>
    /// <param name="browserContextId">The context to dispose.</param>
    /// <param name="cancellationToken">Cancels the disposal.</param>
    /// <exception cref="Protocol.ProtocolException">There is no such context.</exception>
    ValueTask DisposeBrowserContextAsync(string browserContextId, CancellationToken cancellationToken);

    /// <summary>Creates one target and answers it, ready to be published.</summary>
    /// <param name="request">What the client asked for.</param>
    /// <param name="cancellationToken">Cancels the creation.</param>
    /// <remarks>
    /// The target must be returned <i>before</i> anything of its own runs when
    /// <see cref="TargetCreationRequest.WaitForDebugger"/> holds, because a client that asked for
    /// <c>waitForDebuggerOnStart</c> asked to be attached before the first line executes.
    /// </remarks>
    ValueTask<DevToolsTarget> CreateTargetAsync(TargetCreationRequest request, CancellationToken cancellationToken);

    /// <summary>Closes one target the host made.</summary>
    /// <param name="target">The target to close.</param>
    /// <param name="cancellationToken">Cancels the close.</param>
    ValueTask CloseTargetAsync(DevToolsTarget target, CancellationToken cancellationToken);

    /// <summary>Registers whatever domains the host answers on the <b>browser</b> session.</summary>
    /// <param name="session">The root session of one conversation.</param>
    /// <remarks>
    /// A target's own domains are registered per attachment; this is the other half, and it exists because
    /// some commands are about the browser rather than about a page. <c>Storage.getCookies</c> is the one
    /// that made it necessary: Puppeteer asks a page's session for its cookies and Playwright asks the
    /// browser's session for a <i>context</i>'s, so a server that registered the domain on page sessions only
    /// answered one of the two clients and <c>-32601</c>'d the other. Called once per conversation, before
    /// anything is read from it.
    /// </remarks>
    void RegisterBrowserDomains(Session.DevToolsSession session);
}

/// <summary>
/// What <c>Target.createTarget</c> asked a host for.
/// </summary>
/// <param name="Url">Where the new target should go, or <see langword="null"/> for a blank one.</param>
/// <param name="BrowserContextId">Which context to create it in, or <see langword="null"/> for the default.</param>
/// <param name="WaitForDebugger">
/// Whether the target runs nothing until a client sends <c>Runtime.runIfWaitingForDebugger</c>. It is the
/// asking session's <c>Target.setAutoAttach(waitForDebuggerOnStart:)</c>, carried in the request rather than
/// read back off the target afterwards: a host that navigates as it creates would otherwise have run the
/// first document before anybody could hold it.
/// </param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct TargetCreationRequest(string? Url, string? BrowserContextId, bool WaitForDebugger);
