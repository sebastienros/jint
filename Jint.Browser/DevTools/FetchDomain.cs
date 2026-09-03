using System.Collections.Concurrent;
using System.Globalization;
using Jint.Browser.Runtime;
using Jint.DevTools;
using Jint.DevTools.Domains;
using Jint.DevTools.Protocol;
using Jint.DevTools.Protocol.Fetch;
using Jint.DevTools.Session;
using ProtocolFetchEvents = Jint.DevTools.Domains.FetchEvents;
using ProtocolNetwork = Jint.DevTools.Protocol.Network;

namespace Jint.Browser.DevTools;

/// <summary>
/// The <c>Fetch</c> domain: a client sees a request before it is sent, and answers it itself if it wants to.
/// </summary>
/// <remarks>
/// <para>
/// <b>The pause is the engine's own interception point.</b> <c>FetchObserver.OnRequestAsync</c> is asked
/// about every hop before it goes on the wire and its answer decides the hop, so a paused request is that
/// call not having returned yet. It holds the one transport thread the request is being sent on — never the
/// page loop, which goes on pumping timers, answering commands and committing navigations while a client
/// thinks about a request.
/// </para>
/// <para>
/// <b>That sentence has no exception left, and the reason is that the answer no longer comes from the
/// loop.</b> A <c>&lt;script src&gt;</c> that a <i>running script</i> inserted is still fetched with the
/// loop blocked rather than pumping — <c>Runtime/Parsing/AGENTS.md</c> says why: pumping from inside a
/// running script would run the page's jobs in the middle of one. So <c>PageTarget.RunsOffThread</c> names
/// <see cref="ContinueRequestAsync"/>, <see cref="FailRequestAsync"/> and <see cref="FulfillRequestAsync"/>,
/// which look one entry up and complete a promise and touch no engine state at all: they are answered on the
/// thread that read them, and what they release is the fetch the loop is blocked on.
/// </para>
/// <para>
/// <b>The <c>Request</c> stage only, and that is an engine seam rather than an omission.</b> A pattern
/// asking for <c>requestStage: "Response"</c> is accepted and matches nothing:
/// <c>FetchObserver.OnResponse</c> is a notification an observer cannot answer, so a response-stage pause
/// could only ever continue unchanged — a client that called <c>fulfillRequest</c> from one would be
/// silently ignored, which is worse than not pausing. <c>Fetch.continueResponse</c>,
/// <c>Fetch.getResponseBody</c> and <c>Fetch.takeResponseBodyAsStream</c> (and with them the whole <c>IO</c>
/// domain) are absent for the same reason; <c>Network.getResponseBody</c> is what answers a body here.
/// </para>
/// <para>
/// <b>No authentication challenge exists here.</b> <c>handleAuthRequests</c> is accepted and
/// <c>Fetch.authRequired</c> is never sent: nothing in this browser answers a <c>401</c> with credentials, so
/// there is no challenge for a client to be asked about, and <c>continueWithAuth</c> is absent.
/// </para>
/// <para>
/// See <see href="https://chromedevtools.github.io/devtools-protocol/tot/Fetch/"/>.
/// </para>
/// </remarks>
internal sealed class FetchDomain : FetchDomainBase, IDetachableDomain
{
    private readonly PageTarget _target;
    private readonly ConcurrentDictionary<string, PausedRequest> _paused = new(StringComparer.Ordinal);

    private volatile RequestPattern[] _patterns = [];
    private long _lastInterception;

    internal FetchDomain(PageTarget target)
    {
        _target = target;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// An empty or absent <c>patterns</c> means every request, which is the protocol's own default and what
    /// every recorded client sends.
    /// </remarks>
    protected override async ValueTask<EmptyResult> EnableAsync(EnableRequest parameters, CommandContext context)
    {
        _target.ClaimInterception(this);
        _patterns = parameters.Patterns ?? [];

        await MarkEnabledAsync(context).ConfigureAwait(false);
        return EmptyResult.Instance;
    }

    /// <inheritdoc/>
    protected override async ValueTask<EmptyResult> DisableAsync(EmptyParameters parameters, CommandContext context)
    {
        await MarkDisabledAsync(context).ConfigureAwait(false);
        _target.ReleaseInterception(this);
        return EmptyResult.Instance;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <b>Detaching continues every paused request rather than failing it.</b> A client that walked away has
    /// no opinion about the page's requests, and a page whose document fetch was left paused by a
    /// disconnected client would never load.
    /// </remarks>
    void IDetachableDomain.Detach() => _target.ReleaseInterception(this);

    /// <summary>Whether this domain wants to be asked about <paramref name="request"/>.</summary>
    internal bool Wants(PageNetworkRequest request)
    {
        if (!IsEnabled)
        {
            return false;
        }

        var patterns = _patterns;
        if (patterns.Length == 0)
        {
            return true;
        }

        foreach (var pattern in patterns)
        {
            if (Matches(pattern, request))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Holds one request until the client answers it, and turns that answer into a decision.
    /// </summary>
    /// <param name="request">The hop about to be sent.</param>
    /// <param name="frameId">The frame the request belongs to.</param>
    /// <param name="cancellationToken">Cancelled when the fetch is abandoned or times out.</param>
    /// <remarks>
    /// The wait is bounded by nothing of this domain's own, deliberately: the fetch already carries the
    /// page's timeout and the document's cancellation token, so a client that never answers costs that one
    /// request its own deadline and the page nothing. A pause the token ends continues the request, because a
    /// request whose fetch has been abandoned is about to fail anyway and failing it twice tells a client
    /// nothing new.
    /// </remarks>
    internal async ValueTask<PageNetworkDecision> PauseAsync(PageNetworkRequest request, string frameId, CancellationToken cancellationToken)
    {
        var id = "interception-job-" + Interlocked.Increment(ref _lastInterception).ToString(CultureInfo.InvariantCulture);
        var paused = new PausedRequest();

        _paused[id] = paused;

        EmitDetached(ProtocolFetchEvents.RequestPaused(new RequestPausedEvent
        {
            RequestId = id,
            Request = Describe(request),
            FrameId = frameId,
            ResourceType = ResourceTypeOf(request.Kind),
            NetworkId = request.RequestId,
        }));

        try
        {
            using var registration = cancellationToken.Register(
                static state => ((PausedRequest) state!).Answer(PageNetworkDecision.Proceed),
                paused);

            return await paused.Completion.Task.ConfigureAwait(false);
        }
        finally
        {
            _paused.TryRemove(id, out _);
        }
    }

    /// <summary>Lets every paused request go, which disabling and detaching both do.</summary>
    internal void ContinueEverything()
    {
        foreach (var id in _paused.Keys)
        {
            if (_paused.TryRemove(id, out var paused))
            {
                paused.Answer(PageNetworkDecision.Proceed);
            }
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <c>interceptResponse</c> is accepted and never honoured, for the reason the class remarks give: this
    /// browser has no response stage to pause at, so asking to be paused again after the response is asking
    /// for something that would never arrive.
    /// </remarks>
    protected override ValueTask<EmptyResult> ContinueRequestAsync(ContinueRequestRequest parameters, CommandContext context)
    {
        var paused = Take(parameters.RequestId);

        var headers = parameters.Headers is { } declared ? Headers(declared) : null;
        var body = parameters.PostData is { } data ? Convert.FromBase64String(data) : null;

        paused.Answer(headers is null && body is null && parameters.Url is null && parameters.Method is null
            ? PageNetworkDecision.Proceed
            : PageNetworkDecision.Continue(parameters.Url, parameters.Method, headers, body));

        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <c>binaryResponseHeaders</c> is the same list in one <c>\0</c>-separated blob, and is read when a
    /// client sent it instead of <c>responseHeaders</c>; Playwright sends the blob.
    /// </remarks>
    protected override ValueTask<EmptyResult> FulfillRequestAsync(FulfillRequestRequest parameters, CommandContext context)
    {
        var paused = Take(parameters.RequestId);

        IReadOnlyList<PageHeader>? headers = parameters.ResponseHeaders is { } declared
            ? Headers(declared)
            : Headers(parameters.BinaryResponseHeaders);

        var body = parameters.Body is { } encoded ? Convert.FromBase64String(encoded) : [];

        paused.Answer(PageNetworkDecision.Fulfill(parameters.ResponseCode, headers, body, parameters.ResponsePhrase));
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <inheritdoc/>
    protected override ValueTask<EmptyResult> FailRequestAsync(FailRequestRequest parameters, CommandContext context)
    {
        var paused = Take(parameters.RequestId);
        paused.Answer(PageNetworkDecision.Fail(NetworkError(parameters.ErrorReason)));
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <summary>The paused request one identifier names, or Chrome's own refusal.</summary>
    private PausedRequest Take(string requestId)
    {
        if (!_paused.TryRemove(requestId, out var paused))
        {
            Throw.ServerError("Invalid InterceptionId.");
        }

        return paused!;
    }

    /// <summary>Whether one pattern asks about one request.</summary>
    /// <remarks>
    /// <c>requestStage: "Response"</c> matches nothing here; see the class remarks. A pattern with no
    /// <c>urlPattern</c> means every URL, which is the protocol's default.
    /// </remarks>
    private static bool Matches(RequestPattern pattern, PageNetworkRequest request)
    {
        if (pattern.RequestStage is { } stage && string.Equals(stage, RequestStageValues.Response, StringComparison.Ordinal))
        {
            return false;
        }

        if (pattern.ResourceType is { } type && !string.Equals(type, ResourceTypeOf(request.Kind), StringComparison.Ordinal))
        {
            return false;
        }

        return pattern.UrlPattern is not { } url || UrlPattern.Matches(url, request.Url);
    }

    /// <summary>One hop, as the protocol describes a request.</summary>
    private static ProtocolNetwork.Request Describe(PageNetworkRequest request)
    {
        var headers = new Dictionary<string, string>(request.Headers.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var header in request.Headers)
        {
            headers[header.Name] = headers.TryGetValue(header.Name, out var existing)
                ? existing + "\n" + header.Value
                : header.Value;
        }

        return new ProtocolNetwork.Request
        {
            Url = request.Url,
            Method = request.Method,
            Headers = headers,
            PostData = request.PostData,
            HasPostData = request.HasPostData ? true : null,
            InitialPriority = ProtocolNetwork.ResourcePriorityValues.Medium,
            ReferrerPolicy = ProtocolNetwork.RequestReferrerPolicyValues.StrictOriginWhenCrossOrigin,
        };
    }

    private static PageHeader[] Headers(HeaderEntry[] entries)
    {
        var headers = new PageHeader[entries.Length];
        for (var i = 0; i < entries.Length; i++)
        {
            headers[i] = new PageHeader(entries[i].Name.ToLowerInvariant(), entries[i].Value);
        }

        return headers;
    }

    /// <summary>
    /// The header list a client sent as one <c>\0</c>-separated blob, or <see langword="null"/> for none.
    /// </summary>
    /// <remarks>
    /// The protocol's <c>binaryResponseHeaders</c> is base64 of <c>name: value</c> lines separated by NUL,
    /// which is the shape Playwright sends because it lets a header carry bytes a JSON string cannot.
    /// </remarks>
    private static List<PageHeader>? Headers(string? binary)
    {
        if (binary is not { Length: > 0 })
        {
            return null;
        }

        var text = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(binary));
        var headers = new List<PageHeader>();

        foreach (var line in text.Split('\0'))
        {
            var separator = line.IndexOf(':', StringComparison.Ordinal);
            if (separator <= 0)
            {
                continue;
            }

            headers.Add(new PageHeader(line[..separator].Trim().ToLowerInvariant(), line[(separator + 1)..].Trim()));
        }

        return headers;
    }

    /// <summary>The <c>net::ERR_*</c> string one of the protocol's error reasons stands for.</summary>
    private static string NetworkError(string reason) => reason switch
    {
        ProtocolNetwork.ErrorReasonValues.Aborted => "net::ERR_ABORTED",
        ProtocolNetwork.ErrorReasonValues.TimedOut => "net::ERR_TIMED_OUT",
        ProtocolNetwork.ErrorReasonValues.AccessDenied => "net::ERR_ACCESS_DENIED",
        ProtocolNetwork.ErrorReasonValues.ConnectionClosed => "net::ERR_CONNECTION_CLOSED",
        ProtocolNetwork.ErrorReasonValues.ConnectionReset => "net::ERR_CONNECTION_RESET",
        ProtocolNetwork.ErrorReasonValues.ConnectionRefused => "net::ERR_CONNECTION_REFUSED",
        ProtocolNetwork.ErrorReasonValues.ConnectionAborted => "net::ERR_CONNECTION_ABORTED",
        ProtocolNetwork.ErrorReasonValues.ConnectionFailed => "net::ERR_CONNECTION_FAILED",
        ProtocolNetwork.ErrorReasonValues.NameNotResolved => "net::ERR_NAME_NOT_RESOLVED",
        ProtocolNetwork.ErrorReasonValues.InternetDisconnected => "net::ERR_INTERNET_DISCONNECTED",
        ProtocolNetwork.ErrorReasonValues.AddressUnreachable => "net::ERR_ADDRESS_UNREACHABLE",
        ProtocolNetwork.ErrorReasonValues.BlockedByClient => "net::ERR_BLOCKED_BY_CLIENT",
        ProtocolNetwork.ErrorReasonValues.BlockedByResponse => "net::ERR_BLOCKED_BY_RESPONSE",
        _ => "net::ERR_FAILED",
    };

    private static string ResourceTypeOf(PageRequestKind kind) => kind switch
    {
        PageRequestKind.Document => ProtocolNetwork.ResourceTypeValues.Document,
        PageRequestKind.Script => ProtocolNetwork.ResourceTypeValues.Script,
        PageRequestKind.Stylesheet => ProtocolNetwork.ResourceTypeValues.Stylesheet,
        PageRequestKind.Xhr => ProtocolNetwork.ResourceTypeValues.XHR,
        PageRequestKind.Fetch => ProtocolNetwork.ResourceTypeValues.Fetch,
        PageRequestKind.Image => ProtocolNetwork.ResourceTypeValues.Image,
        PageRequestKind.Frame => ProtocolNetwork.ResourceTypeValues.Document,
        _ => ProtocolNetwork.ResourceTypeValues.Other,
    };

    /// <summary>The promise the transport thread of one paused request is waiting on.</summary>
    /// <remarks>
    /// The completion runs its continuations asynchronously, deliberately: completing it inline would resume
    /// the transport thread's <c>await</c> on the page loop, which is the thread a command is answered on.
    /// </remarks>
    private sealed class PausedRequest
    {
        internal TaskCompletionSource<PageNetworkDecision> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Answers the pause, ignoring a second answer for the same request.</summary>
        internal void Answer(PageNetworkDecision decision) => Completion.TrySetResult(decision);
    }
}
