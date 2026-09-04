using Jint.Browser.Runtime;

namespace Jint.Browser.DevTools;

/// <summary>
/// The network half of a page target: the policy a client set, the attachments that hear about requests, and
/// the one that may answer them.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every member here runs on a transport thread.</b> The page's request log is a
/// <c>Jint.WebApi.Fetch.FetchObserver</c>, so a request reaches this class on whichever thread the HTTP stack
/// is sending it on — never on the page loop. That is deliberate rather than incidental, and
/// <see cref="IPageNetworkListener"/> argues it: the one case a page cannot pump through is the fetch of a
/// <c>&lt;script src&gt;</c> a running script inserted, which blocks the loop, and a pause delivered through
/// the loop would deadlock exactly there.
/// </para>
/// <para>
/// <b>So nothing here may touch the engine or the DOM</b>, and nothing does: the frame identifier is the
/// target's own string, the loader identifier and the document URL are read off the page's volatile fields by
/// the log itself, and everything else is protocol data.
/// </para>
/// <para>
/// <b>The events go out through <c>EmitDetached</c></b>, which queues rather than writes, so a slow client
/// slows no request. The <c>Fetch</c> pause is the one place that waits, and it waits on the fetch's own
/// cancellation token.
/// </para>
/// </remarks>
internal sealed partial class PageTarget : IPageNetworkListener
{
    private readonly object _networkGate = new();

    private NetworkDomain[] _networkDomains = [];
    private volatile FetchDomain? _interceptor;
    private volatile PageNetworkPolicy _networkPolicy = PageNetworkPolicy.None;

    /// <summary>What a client asked this page's network to do.</summary>
    internal PageNetworkPolicy NetworkPolicy => _networkPolicy;

    /// <summary>Rewrites the policy under the gate, so two commands cannot lose each other's change.</summary>
    /// <param name="change">What to make of the policy as it stands.</param>
    internal void UpdateNetworkPolicy(Func<PageNetworkPolicy, PageNetworkPolicy> change)
    {
        lock (_networkGate)
        {
            _networkPolicy = change(_networkPolicy);
        }
    }

    /// <summary>The page's request log, which is the seam every event here comes from.</summary>
    internal PageNetworkRecorder NetworkLog => Page.NetworkLog;

    /// <summary>Registers one attachment's <c>Network</c> domain as a listener.</summary>
    internal void AddNetworkDomain(NetworkDomain domain)
    {
        lock (_networkGate)
        {
            _networkDomains = [.. _networkDomains, domain];
        }
    }

    /// <summary>Stops telling one attachment's <c>Network</c> domain anything, which detaching does.</summary>
    internal void RemoveNetworkDomain(NetworkDomain domain)
    {
        lock (_networkGate)
        {
            _networkDomains = [.. _networkDomains.Where(candidate => !ReferenceEquals(candidate, domain))];
        }

        if (!_networkDomains.Any(candidate => candidate.WantsBodies))
        {
            NetworkLog.CaptureBodies = false;
        }
    }

    /// <summary>Recomputes whether the log should be copying bodies, which is what enabling asks for.</summary>
    internal void RefreshBodyCapture()
    {
        NetworkLog.CaptureBodies = Volatile.Read(ref _networkDomains).Any(domain => domain.WantsBodies);
    }

    /// <summary>
    /// Claims the page's interception for one attachment, refusing a second.
    /// </summary>
    /// <remarks>
    /// <b>One intercepting session at a time</b>, which is the rule <c>Debugger</c> already keeps for the
    /// same reason: Chrome pauses a request on every session that asked and expects each of them to answer,
    /// and a page whose request is waiting for two clients is a page one of them can hang. No recorded client
    /// opens two intercepting sessions on one page.
    /// </remarks>
    internal void ClaimInterception(FetchDomain domain)
    {
        var current = _interceptor;
        if (current is not null && !ReferenceEquals(current, domain))
        {
            Jint.DevTools.Throw.ServerError(
                "Fetch is already enabled on another session of this target",
                "this browser pauses a request for one session at a time; disable Fetch on the other session first");
        }

        _interceptor = domain;
    }

    /// <summary>Gives interception back, continuing whatever was paused.</summary>
    internal void ReleaseInterception(FetchDomain domain)
    {
        if (ReferenceEquals(_interceptor, domain))
        {
            _interceptor = null;
        }

        domain.ContinueEverything();
    }

    /// <inheritdoc/>
    async ValueTask<PageNetworkDecision> IPageNetworkListener.RequestWillBeSentAsync(
        PageNetworkRequest request,
        CancellationToken cancellationToken)
    {
        var policy = _networkPolicy;

        // The rewrite happens before the announcement, so a client is told what the request will actually
        // carry rather than what the page composed before its own overrides were applied.
        var headers = policy.Apply(request.Headers, Emulation);
        var effective = headers is null ? request : request with { Headers = headers };

        foreach (var domain in NetworkDomains())
        {
            domain.RequestWillBeSent(effective, FrameId);
        }

        if (policy.Offline)
        {
            // Chrome's own code for the offline switch, and the reason it is a refusal rather than a filter:
            // a page that believes it is offline has to see every request fail, navigations included.
            return PageNetworkDecision.Fail("net::ERR_INTERNET_DISCONNECTED");
        }

        if (policy.Blocks(effective.Url))
        {
            return PageNetworkDecision.Fail("net::ERR_BLOCKED_BY_CLIENT", ProtocolBlockedReason.Inspector);
        }

        if (_interceptor is { } interceptor && interceptor.Wants(effective))
        {
            var answered = await interceptor.PauseAsync(effective, FrameId, cancellationToken).ConfigureAwait(false);

            // A client that answered "continue" without naming headers still gets the page's own overrides,
            // because those are the policy rather than part of the request it was shown.
            if (answered.Kind == PageNetworkDecisionKind.Proceed && headers is not null)
            {
                return PageNetworkDecision.Continue(headers: headers);
            }

            return answered;
        }

        return headers is null ? PageNetworkDecision.Proceed : PageNetworkDecision.Continue(headers: headers);
    }

    /// <inheritdoc/>
    async ValueTask<PageNetworkResponseDecision> IPageNetworkListener.ResponseWillBeDeliveredAsync(
        PageNetworkRequest request,
        PageNetworkResponse response,
        CancellationToken cancellationToken)
    {
        if (_interceptor is not { } interceptor || !interceptor.WantsResponse(request))
        {
            return PageNetworkResponseDecision.Proceed;
        }

        return await interceptor.PauseResponseAsync(request, response, FrameId, cancellationToken).ConfigureAwait(false);
    }

    void IPageNetworkListener.WebSocketCreated(string socketId, string url)
    {
        foreach (var domain in NetworkDomains())
        {
            domain.WebSocketCreated(socketId, url);
        }
    }

    void IPageNetworkListener.WebSocketHandshakeRequest(string socketId, IReadOnlyList<PageHeader> headers)
    {
        foreach (var domain in NetworkDomains())
        {
            domain.WebSocketHandshakeRequest(socketId, headers);
        }
    }

    void IPageNetworkListener.WebSocketHandshakeResponse(string socketId, int status, string statusText, IReadOnlyList<PageHeader> headers)
    {
        foreach (var domain in NetworkDomains())
        {
            domain.WebSocketHandshakeResponse(socketId, status, statusText, headers);
        }
    }

    void IPageNetworkListener.WebSocketClosed(string socketId)
    {
        foreach (var domain in NetworkDomains())
        {
            domain.WebSocketClosed(socketId);
        }
    }

    void IPageNetworkListener.ResponseReceived(PageNetworkRequest request, PageNetworkResponse response)
    {
        foreach (var domain in NetworkDomains())
        {
            domain.ResponseReceived(request, response, FrameId);
        }
    }

    /// <inheritdoc/>
    void IPageNetworkListener.DataReceived(string requestId, int length)
    {
        foreach (var domain in NetworkDomains())
        {
            domain.DataReceived(requestId, length);
        }
    }

    /// <inheritdoc/>
    void IPageNetworkListener.LoadingFinished(string requestId, long encodedLength)
    {
        foreach (var domain in NetworkDomains())
        {
            domain.LoadingFinished(requestId, encodedLength);
        }
    }

    /// <inheritdoc/>
    void IPageNetworkListener.LoadingFailed(string requestId, PageRequestKind kind, string errorText, bool canceled, string? blockedReason)
    {
        foreach (var domain in NetworkDomains())
        {
            domain.LoadingFailed(requestId, kind, errorText, canceled, blockedReason);
        }
    }

    /// <inheritdoc/>
    void IPageNetworkListener.NotFetched(PageNetworkRequest request, string reason)
    {
        foreach (var domain in NetworkDomains())
        {
            domain.RequestWillBeSent(request, FrameId);
            domain.LoadingFailed(request.RequestId, request.Kind, "net::ERR_BLOCKED_BY_CLIENT", canceled: false, ProtocolBlockedReason.Other);
        }
    }

    private NetworkDomain[] NetworkDomains() => Volatile.Read(ref _networkDomains);
}

/// <summary>The protocol's blocked-reason strings this browser ever sends.</summary>
/// <remarks>
/// Two of the fourteen. <c>inspector</c> is what a client's own <c>setBlockedURLs</c> produces, which is
/// exactly what Chrome sends for it, and <c>other</c> is the resource this browser declines to fetch because
/// it renders nothing.
/// </remarks>
internal static class ProtocolBlockedReason
{
    /// <inheritdoc cref="ProtocolBlockedReason"/>
    internal const string Inspector = "inspector";

    /// <inheritdoc cref="ProtocolBlockedReason"/>
    internal const string Other = "other";
}
