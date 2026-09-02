using Jint.DevTools.Protocol;
using Jint.DevTools.Protocol.Target;
using Jint.DevTools.Session;

namespace Jint.DevTools.Domains;

/// <summary>
/// Answers the <c>Target</c> commands: what targets exist, and which of them a client is attached to.
/// </summary>
/// <remarks>
/// <para>
/// <b>Flattened sessions only.</b> <c>attachToTarget</c> and <c>setAutoAttach</c> both refuse
/// <c>flatten: false</c> with <c>-32000</c>, because the alternative is the wrapped model —
/// <c>Target.sendMessageToTarget</c> carrying a whole protocol message as a string — and no client recorded
/// in <c>tools/devtools-protocol/handshakes/</c> asks for it. Answering both models would mean a second
/// routing path kept alive forever for nobody.
/// </para>
/// <para>
/// <b>Creating targets and contexts is the host's, when there is one.</b> A server with no
/// <see cref="ITargetHost"/> has nothing to open and nothing to partition, so <c>createBrowserContext</c>
/// refuses with a reason rather than minting an identifier that isolates nothing and
/// <c>createTarget</c> falls back to <see cref="DevToolsServerOptions.EngineFactory"/>. A server that
/// <i>has</i> one — <c>Jint.Browser</c>, which is AngleSharp plus Jint — routes all four commands through it,
/// and everything else on this domain behaves identically either way.
/// </para>
/// <para>
/// See <see href="https://chromedevtools.github.io/devtools-protocol/tot/Target/"/>.
/// </para>
/// </remarks>
internal sealed class TargetDomain : TargetDomainBase
{
    private readonly BrowserSession _browser;
    private readonly bool _nested;

    private bool _discover;
    private FilterEntry[]? _discoverFilter;
    private bool _autoAttach;
    private bool _autoAttachWaitsForDebugger;
    private FilterEntry[]? _autoAttachFilter;

    /// <summary>Creates the domain for one session.</summary>
    /// <param name="browser">The conversation whose attachments this domain mints.</param>
    /// <param name="nested">
    /// Whether this is the copy on an attached session rather than on the browser session, which decides
    /// only what <c>setAutoAttach</c> does: a target here has no children to attach to.
    /// </param>
    internal TargetDomain(BrowserSession browser, bool nested)
    {
        _browser = browser;
        _nested = nested;
    }

    /// <inheritdoc/>
    protected override ValueTask<GetTargetsResponse> GetTargetsAsync(GetTargetsRequest parameters, CommandContext context)
    {
        var infos = new List<TargetInfo>();
        foreach (var target in _browser.Server.AllTargets)
        {
            if (Matches(parameters.Filter, target.Type))
            {
                infos.Add(Describe(target));
            }
        }

        return new ValueTask<GetTargetsResponse>(new GetTargetsResponse { TargetInfos = [.. infos] });
    }

    /// <inheritdoc/>
    protected override ValueTask<GetTargetInfoResponse> GetTargetInfoAsync(GetTargetInfoRequest parameters, CommandContext context)
    {
        var target = Find(parameters.TargetId);
        return new ValueTask<GetTargetInfoResponse>(new GetTargetInfoResponse { TargetInfo = Describe(target) });
    }

    /// <inheritdoc/>
    protected override async ValueTask<EmptyResult> SetDiscoverTargetsAsync(SetDiscoverTargetsRequest parameters, CommandContext context)
    {
        var wasDiscovering = _discover;
        _discover = parameters.Discover;
        _discoverFilter = parameters.Filter;

        if (_discover && !wasDiscovering)
        {
            // Chrome replays targetCreated for everything that already exists, which is what makes discovery
            // usable by a client that connected after the targets did.
            foreach (var target in _browser.Server.AllTargets)
            {
                if (Matches(_discoverFilter, target.Type))
                {
                    await EmitAsync(TargetEvents.TargetCreated(new TargetCreatedEvent { TargetInfo = Describe(target) }), context.CancellationToken).ConfigureAwait(false);
                }
            }
        }

        return EmptyResult.Instance;
    }

    /// <inheritdoc/>
    protected override async ValueTask<EmptyResult> SetAutoAttachAsync(SetAutoAttachRequest parameters, CommandContext context)
    {
        if (parameters.Flatten != true)
        {
            RefuseUnflattened();
        }

        if (_nested)
        {
            // A target here has no children — a worker is not a target in this server, and an engine has
            // none at all — so there is nothing to attach to and nothing to remember. Answered as the
            // success it is: a client walks down the tree by sending this on every session it is given, and
            // a refusal there reads to it as a broken target.
            return EmptyResult.Instance;
        }

        _autoAttach = parameters.AutoAttach;
        _autoAttachWaitsForDebugger = parameters.WaitForDebuggerOnStart;
        _autoAttachFilter = parameters.Filter;

        if (!_autoAttach)
        {
            return EmptyResult.Instance;
        }

        foreach (var target in _browser.Server.AllTargets)
        {
            if (Matches(_autoAttachFilter, target.Type))
            {
                await AttachAsync(target, context.CancellationToken).ConfigureAwait(false);
            }
        }

        return EmptyResult.Instance;
    }

    /// <inheritdoc/>
    protected override async ValueTask<AttachToTargetResponse> AttachToTargetAsync(AttachToTargetRequest parameters, CommandContext context)
    {
        if (parameters.Flatten != true)
        {
            RefuseUnflattened();
        }

        var target = Find(parameters.TargetId);
        var sessionId = await AttachAsync(target, context.CancellationToken).ConfigureAwait(false);
        return new AttachToTargetResponse { SessionId = sessionId };
    }

    /// <inheritdoc/>
    protected override async ValueTask<EmptyResult> DetachFromTargetAsync(DetachFromTargetRequest parameters, CommandContext context)
    {
        var sessionId = parameters.SessionId;
        if (sessionId is null && parameters.TargetId is { } targetId)
        {
            sessionId = _browser.SessionIdOf(Find(targetId));
        }

        if (sessionId is null)
        {
            Throw.InvalidParams("Invalid parameters", "either sessionId or targetId is required");
        }

        var detached = _browser.Detach(sessionId);
        if (detached is null)
        {
            return Throw.SessionNotFound<EmptyResult>();
        }

        await EmitAsync(
            TargetEvents.DetachedFromTarget(new DetachedFromTargetEvent { SessionId = sessionId, TargetId = detached.TargetId }),
            context.CancellationToken).ConfigureAwait(false);

        return EmptyResult.Instance;
    }

    /// <summary>
    /// Creates one target, through the host when there is one and from the engine factory otherwise.
    /// </summary>
    /// <remarks>
    /// <b>A target created while this session asked for <c>waitForDebuggerOnStart</c> is created waiting.</b>
    /// The flag travels with the request rather than being applied afterwards, because a host that navigates
    /// as it creates would otherwise have run the first document before anybody could hold it. What releases
    /// it is <c>Runtime.runIfWaitingForDebugger</c> on the session the client is about to be attached on, and
    /// <c>attachedToTarget.waitingForDebugger</c> is what tells the client it has to send one.
    /// </remarks>
    protected override async ValueTask<CreateTargetResponse> CreateTargetAsync(CreateTargetRequest parameters, CommandContext context)
    {
        var server = _browser.Server;

        if (server.Host is not { } host)
        {
            var engineTarget = server.CreateTarget();
            return new CreateTargetResponse { TargetId = engineTarget.TargetId };
        }

        var request = new TargetCreationRequest(parameters.Url, parameters.BrowserContextId, _autoAttach && _autoAttachWaitsForDebugger);
        var target = await host.CreateTargetAsync(request, context.CancellationToken).ConfigureAwait(false);

        server.AddTarget(target);
        return new CreateTargetResponse { TargetId = target.TargetId };
    }

    /// <inheritdoc/>
    protected override async ValueTask<CloseTargetResponse> CloseTargetAsync(CloseTargetRequest parameters, CommandContext context)
    {
        var target = Find(parameters.TargetId);
        await _browser.Server.CloseTargetAsync(target, context.CancellationToken).ConfigureAwait(false);
        return new CloseTargetResponse { Success = true };
    }

    /// <summary>
    /// Answers success and does nothing, which is what activating a target means when there is no window.
    /// </summary>
    /// <remarks>
    /// Refusing would be worse than useless: clients activate a target before driving it and read a failure
    /// as the target being gone.
    /// </remarks>
    protected override ValueTask<EmptyResult> ActivateTargetAsync(ActivateTargetRequest parameters, CommandContext context)
    {
        Find(parameters.TargetId);
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <inheritdoc/>
    protected override ValueTask<GetBrowserContextsResponse> GetBrowserContextsAsync(EmptyParameters parameters, CommandContext context)
    {
        var host = _browser.Server.Host;
        return new ValueTask<GetBrowserContextsResponse>(new GetBrowserContextsResponse
        {
            BrowserContextIds = host is null ? [] : [.. host.BrowserContextIds],
        });
    }

    /// <inheritdoc/>
    /// <remarks>
    /// With no host there is nothing to partition, and a client handed an identifier would believe its next
    /// target was isolated when it is not — so it is refused with the reason rather than answered.
    /// </remarks>
    protected override async ValueTask<CreateBrowserContextResponse> CreateBrowserContextAsync(CreateBrowserContextRequest parameters, CommandContext context)
    {
        if (_browser.Server.Host is not { } host)
        {
            return Throw.ServerError<CreateBrowserContextResponse>(
                "Browser contexts are not supported",
                "an engine target has no cookies, storage or cache to partition, so a context identifier would isolate nothing");
        }

        var browserContextId = await host.CreateBrowserContextAsync(context.CancellationToken).ConfigureAwait(false);
        return new CreateBrowserContextResponse { BrowserContextId = browserContextId };
    }

    /// <inheritdoc cref="CreateBrowserContextAsync"/>
    protected override async ValueTask<EmptyResult> DisposeBrowserContextAsync(DisposeBrowserContextRequest parameters, CommandContext context)
    {
        if (_browser.Server.Host is not { } host)
        {
            return Throw.ServerError<EmptyResult>(
                "Browser contexts are not supported",
                "an engine target has no cookies, storage or cache to partition, so there is no context to dispose");
        }

        await host.DisposeBrowserContextAsync(parameters.BrowserContextId, context.CancellationToken).ConfigureAwait(false);
        return EmptyResult.Instance;
    }

    /// <summary>Tells the client about a target that has just appeared, if it asked to be told.</summary>
    internal async ValueTask TargetAddedAsync(DevToolsTarget target, CancellationToken cancellationToken)
    {
        if (_discover && Matches(_discoverFilter, target.Type))
        {
            await EmitAsync(TargetEvents.TargetCreated(new TargetCreatedEvent { TargetInfo = Describe(target) }), cancellationToken).ConfigureAwait(false);
        }

        if (_autoAttach && Matches(_autoAttachFilter, target.Type))
        {
            await AttachAsync(target, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Tells the client that a target's title or location moved, if it asked to be told.</summary>
    /// <remarks>
    /// A page changes both on every navigation, which is what makes this event worth emitting at all: a
    /// client keeps its own idea of where each target is, and Puppeteer's <c>page.url()</c> reads it.
    /// </remarks>
    internal async ValueTask TargetInfoChangedAsync(DevToolsTarget target, CancellationToken cancellationToken)
    {
        if (!_discover || !Matches(_discoverFilter, target.Type))
        {
            return;
        }

        await EmitAsync(
            TargetEvents.TargetInfoChanged(new TargetInfoChangedEvent { TargetInfo = Describe(target) }),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Tells the client about a target that has gone, detaching first if it was attached.</summary>
    internal async ValueTask TargetRemovedAsync(DevToolsTarget target, CancellationToken cancellationToken)
    {
        if (_browser.SessionIdOf(target) is { } sessionId)
        {
            _browser.Detach(sessionId);
            await EmitAsync(
                TargetEvents.DetachedFromTarget(new DetachedFromTargetEvent { SessionId = sessionId, TargetId = target.TargetId }),
                cancellationToken).ConfigureAwait(false);
        }

        if (_discover && Matches(_discoverFilter, target.Type))
        {
            await EmitAsync(TargetEvents.TargetDestroyed(new TargetDestroyedEvent { TargetId = target.TargetId }), cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask<string> AttachAsync(DevToolsTarget target, CancellationToken cancellationToken)
    {
        var sessionId = _browser.Attach(target, out var created);
        if (!created)
        {
            // Already attached, and announcing it again would have the client believe in two sessions.
            return sessionId;
        }

        await EmitAsync(
            TargetEvents.AttachedToTarget(new AttachedToTargetEvent
            {
                SessionId = sessionId,
                TargetInfo = Describe(target, attached: true),

                // What tells a client it must send Runtime.runIfWaitingForDebugger before anything the host
                // posted will run. A target that is not holding anything says false, and the client's first
                // evaluate goes straight through.
                WaitingForDebugger = _autoAttachWaitsForDebugger && target.IsWaitingForDebugger,
            }),
            cancellationToken).ConfigureAwait(false);

        return sessionId;
    }

    private DevToolsTarget Find(string? targetId)
    {
        if (targetId is not null && _browser.Server.FindTarget(targetId) is { } target)
        {
            return target;
        }

        // Chrome's wording, which clients match on to tell a target that went away from a call that was
        // wrong.
        return Throw.ServerError<DevToolsTarget>("No target with given id found");
    }

    private TargetInfo Describe(DevToolsTarget target, bool attached = false)
        => target.Describe(attached || _browser.SessionIdOf(target) is not null);

    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void RefuseUnflattened()
    {
        Throw.ServerError(
            "Only flatten protocol is supported",
            "the wrapped session model routes every message through Target.sendMessageToTarget, which this server does not answer");
    }

    /// <summary>
    /// Decides whether a target's type passes one of the protocol's filters.
    /// </summary>
    /// <remarks>
    /// The protocol's own rule: the first entry that matches the type — or that names no type, which matches
    /// everything — decides, and <c>exclude</c> inverts it. A filter that matches nothing excludes, and no
    /// filter at all includes, which is what a client sending none means.
    /// </remarks>
    private static bool Matches(FilterEntry[]? filter, string type)
    {
        if (filter is null || filter.Length == 0)
        {
            return true;
        }

        foreach (var entry in filter)
        {
            if (entry.Type is { } wanted && !string.Equals(wanted, type, StringComparison.Ordinal))
            {
                continue;
            }

            return entry.Exclude != true;
        }

        return false;
    }
}
