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
/// <b>Both stages, and the response stage is opt-in per pattern.</b> A pattern asking for
/// <c>requestStage: "Response"</c> pauses when the response's headers are in and its body has not been read,
/// carrying <c>responseStatusCode</c>, <c>responseStatusText</c> and <c>responseHeaders</c>;
/// <see cref="ContinueResponseAsync"/> lets it through with the status line and the header list rewritable,
/// and <see cref="FulfillRequestAsync"/> and <see cref="FailRequestAsync"/> answer one too. A pattern that
/// names no stage means <c>Request</c>, which is the protocol's own default — pausing both stages for it
/// would double every pause a recorded client expects. The engine seam under it is
/// <c>FetchObserver.OnResponseAsync</c>
/// (<see href="https://github.com/sebastienros/jint/issues/3701">#3701</see> item 1).
/// </para>
/// <para>
/// <b><c>Fetch.getResponseBody</c> and <c>Fetch.takeResponseBodyAsStream</c> (and with them the whole
/// <c>IO</c> domain) need something further</b>: the pause has the response's headers while its body is
/// still on the socket, so there are no bytes to give a paused client without buffering them first — a
/// budget decision rather than a hook. <c>Network.getResponseBody</c> is what answers a body here.
/// </para>
/// <para>
/// <b>Authentication challenges are here.</b> <c>handleAuthRequests</c> turns them on, a <c>401</c> carrying
/// a <c>WWW-Authenticate</c> pauses as <c>Fetch.authRequired</c>, and <c>continueWithAuth</c> answers one
/// over the engine's <c>FetchObserver.OnAuthRequiredAsync</c>. <b>Only <c>Basic</c> can be answered</b> — it
/// is the one scheme whose answer is a function of the credentials alone, while <c>Digest</c> needs a nonce
/// exchange and <c>Negotiate</c> and <c>NTLM</c> a handshake bound to the connection. Every other scheme is
/// still <i>reported</i>, because being asked is how a client tells "unsupported" from "never challenged",
/// and <c>continueWithAuth</c> answering <c>ProvideCredentials</c> for one is refused with an error naming
/// the scheme rather than accepted and dropped: an ask that cannot be honoured must fail visibly. A
/// <c>407</c> is a proxy's and is not reported at all, the proxy belonging to the <c>HttpClient</c> the
/// context was given, so <c>source</c> is always <c>Server</c>
/// (<see href="https://github.com/sebastienros/jint/issues/3828">#3828</see>).
/// </para>
/// <para>
/// See <see href="https://chromedevtools.github.io/devtools-protocol/tot/Fetch/"/>.
/// </para>
/// </remarks>
internal sealed class FetchDomain : FetchDomainBase, IDetachableDomain
{
    private readonly PageTarget _target;
    private readonly ConcurrentDictionary<string, PausedRequest> _paused = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, PausedResponse> _pausedResponses = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, PausedAuth> _pausedAuth = new(StringComparer.Ordinal);

    private volatile RequestPattern[] _patterns = [];
    private volatile bool _handleAuth;
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

        // Unlike the patterns, this is not a filter over requests: a challenge has no resource type and no
        // URL pattern to match, so a client either wants every one of them or none.
        _handleAuth = parameters.HandleAuthRequests ?? false;

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

    /// <summary>Whether this domain wants to be asked about <paramref name="request"/>'s response.</summary>
    /// <remarks>
    /// The response stage is opt-in per pattern, unlike the request stage: a client that named no
    /// <c>requestStage</c> asked for the protocol's default, which is <c>Request</c>, and pausing its
    /// responses as well would double every pause it expects.
    /// </remarks>
    internal bool WantsResponse(PageNetworkRequest request)
    {
        if (!IsEnabled)
        {
            return false;
        }

        foreach (var pattern in _patterns)
        {
            if (IsResponseStage(pattern) && Matches(pattern, request, RequestStageValues.Response))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Holds one response until the client answers it, and turns that answer into a decision.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The same wait <see cref="PauseAsync"/> makes, at the other stage, and bounded by the same nothing of
    /// this domain's own — the fetch carries the page's timeout and the document's token. A pause the token
    /// ends delivers the response, because a fetch that has been abandoned is about to fail anyway.
    /// </para>
    /// <para>
    /// <b>This is also the only place a body can be read</b>, which is why the pause carries the reader: the
    /// bytes are on the socket exactly while this call has not returned, and
    /// <see cref="GetResponseBodyAsync"/> is the one thing that ever asks for them.
    /// </para>
    /// </remarks>
    internal async ValueTask<PageNetworkResponseDecision> PauseResponseAsync(
        PageNetworkRequest request,
        PageNetworkResponse response,
        PageResponseBodyReader body,
        string frameId,
        CancellationToken cancellationToken)
    {
        var id = "interception-job-" + Interlocked.Increment(ref _lastInterception).ToString(CultureInfo.InvariantCulture);
        var paused = new PausedResponse { Body = body, Abandoned = cancellationToken };

        _pausedResponses[id] = paused;

        EmitDetached(ProtocolFetchEvents.RequestPaused(new RequestPausedEvent
        {
            RequestId = id,
            Request = Describe(request),
            FrameId = frameId,
            ResourceType = ResourceTypeOf(request.Kind),
            NetworkId = request.RequestId,

            // What makes this a response-stage pause to every client: the protocol says the status code and
            // the headers are present exactly then.
            ResponseStatusCode = response.Status,
            ResponseStatusText = response.StatusText,
            ResponseHeaders = [.. response.Headers.Select(header => new HeaderEntry { Name = header.Name, Value = header.Value })],
        }));

        try
        {
            using var registration = cancellationToken.Register(
                static state => ((PausedResponse) state!).Terminate(PageNetworkResponseDecision.Proceed),
                paused);

            return await paused.Completion.Task.ConfigureAwait(false);
        }
        finally
        {
            _pausedResponses.TryRemove(id, out _);
        }
    }

    /// <summary>Whether this domain asked to be told about authentication challenges.</summary>
    /// <remarks>
    /// <c>handleAuthRequests</c> alone, with no pattern consulted: a challenge is a property of a response
    /// rather than of a request, so there is nothing for a URL pattern or a resource type to match on, and
    /// the protocol accordingly makes it one flag rather than a stage.
    /// </remarks>
    internal bool WantsAuth => IsEnabled && _handleAuth;

    /// <summary>
    /// Holds one challenge until the client answers it — https://chromedevtools.github.io/devtools-protocol/tot/Fetch/#event-authRequired.
    /// </summary>
    /// <remarks>
    /// The identifier comes from the same counter the two stages use, because the protocol gives a client one
    /// identifier space and <c>continueWithAuth</c> names an id out of it exactly as <c>continueRequest</c>
    /// does. A pause the request's own token ends declines the challenge, which delivers the <c>401</c> — a
    /// fetch that has been abandoned is about to fail anyway.
    /// </remarks>
    internal async ValueTask<PageNetworkAuthDecision> PauseAuthAsync(
        PageNetworkRequest request,
        PageNetworkAuthChallenge challenge,
        string frameId,
        CancellationToken cancellationToken)
    {
        var id = "interception-job-" + Interlocked.Increment(ref _lastInterception).ToString(CultureInfo.InvariantCulture);
        var paused = new PausedAuth { Challenge = challenge };

        _pausedAuth[id] = paused;

        EmitDetached(ProtocolFetchEvents.AuthRequired(new AuthRequiredEvent
        {
            RequestId = id,
            Request = Describe(request),
            FrameId = frameId,
            ResourceType = ResourceTypeOf(request.Kind),
            AuthChallenge = new AuthChallenge
            {
                Source = challenge.Source,
                Origin = OriginOf(challenge.Url),
                Scheme = challenge.Scheme,
                Realm = challenge.Realm,
            },
        }));

        try
        {
            using var registration = cancellationToken.Register(
                static state => ((PausedAuth) state!).Answer(PageNetworkAuthDecision.Proceed),
                paused);

            return await paused.Completion.Task.ConfigureAwait(false);
        }
        finally
        {
            _pausedAuth.TryRemove(id, out _);
        }
    }

    /// <summary>The scheme and authority of a URL, which is what the protocol calls a challenge's origin.</summary>
    private static string OriginOf(string url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.GetLeftPart(UriPartial.Authority) : url;

    /// <inheritdoc/>
    /// <remarks>
    /// <para>
    /// https://chromedevtools.github.io/devtools-protocol/tot/Fetch/#method-continueWithAuth — the three
    /// answers the protocol admits. <c>Default</c> and <c>CancelAuth</c> both deliver the <c>401</c>: a
    /// browser tells them apart by whether it puts its own dialog up, and there is no dialog here.
    /// <c>ProvideCredentials</c> sends the challenged hop again with an <c>Authorization</c> header, once.
    /// </para>
    /// <para>
    /// <b><c>ProvideCredentials</c> for a scheme this browser cannot answer is an error, not a no-op.</b>
    /// Only <c>Basic</c> can be answered from a username and a password alone; a client that offered
    /// credentials for a <c>Digest</c> or a <c>Negotiate</c> challenge is told so by name, and the
    /// <c>401</c> is then delivered. Accepting the command and quietly changing nothing would be the same
    /// silent discard this whole lane exists to remove.
    /// </para>
    /// </remarks>
    protected override ValueTask<EmptyResult> ContinueWithAuthAsync(ContinueWithAuthRequest parameters, CommandContext context)
    {
        if (!_pausedAuth.TryRemove(parameters.RequestId, out var paused))
        {
            return Throw.ServerError<ValueTask<EmptyResult>>(
                "Invalid InterceptionId.",
                "no authentication challenge is paused under that identifier");
        }

        var answer = parameters.AuthChallengeResponse;
        if (!string.Equals(answer.Response, AuthChallengeResponseResponseValues.ProvideCredentials, StringComparison.Ordinal))
        {
            paused.Answer(PageNetworkAuthDecision.Proceed);
            return new ValueTask<EmptyResult>(EmptyResult.Instance);
        }

        if (!paused.Challenge.CanProvideCredentials)
        {
            // Declined first, so the request is released whatever the client does about the error.
            paused.Answer(PageNetworkAuthDecision.Proceed);

            return Throw.ServerError<ValueTask<EmptyResult>>(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Credentials cannot be provided for a '{paused.Challenge.Scheme}' challenge."),
                "only Basic can be answered from a username and a password alone; the response was delivered unchanged");
        }

        paused.Answer(PageNetworkAuthDecision.Credentials(answer.Username ?? "", answer.Password ?? ""));
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// https://chromedevtools.github.io/devtools-protocol/tot/Fetch/#method-continueResponse — the response
    /// stage's own "let it through", with the status line and the header list rewritable and the body not:
    /// the bytes have not been read and are still coming, so what this changes is what the response
    /// <i>says</i>. <c>binaryResponseHeaders</c> is the same list in one <c>\0</c>-separated blob and is
    /// read when a client sent it instead of <c>responseHeaders</c>.
    /// </remarks>
    protected override ValueTask<EmptyResult> ContinueResponseAsync(ContinueResponseRequest parameters, CommandContext context)
    {
        var paused = TakeResponse(parameters.RequestId);

        IReadOnlyList<PageHeader>? headers = parameters.ResponseHeaders is { } declared
            ? Headers(declared)
            : Headers(parameters.BinaryResponseHeaders);

        Answer(
            parameters.RequestId,
            paused,
            headers is null && parameters.ResponseCode is null && parameters.ResponsePhrase is null
                ? PageNetworkResponseDecision.Proceed
                : PageNetworkResponseDecision.Continue(parameters.ResponseCode, parameters.ResponsePhrase, headers),
            "Fetch.continueResponse");

        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <para>
    /// https://chromedevtools.github.io/devtools-protocol/tot/Fetch/#method-getResponseBody — the whole body
    /// of a response the client is holding at the response stage, base64 always: the bytes are the ones the
    /// page is about to consume, and encoding them as text would need a second charset policy and would lose
    /// a binary body.
    /// </para>
    /// <para>
    /// <b>Reading changes nothing the page sees.</b> The bytes taken off the socket are replayed ahead of
    /// the remainder when the response is released, so the document receives every original byte exactly
    /// once whether the read happened, was refused, or was never asked for. Repeated reads of the same pause
    /// answer the same bytes without touching the socket again.
    /// </para>
    /// <para>
    /// <b>It is the only thing that ever buffers a paused body</b>, which is what makes the cost a client's
    /// own: a pattern that pauses responses copies nothing until this is called. The bytes are charged to
    /// the page's one allowance — <c>BrowserOptions.MaxCapturedResponseBytes</c>, shared with the
    /// <c>Network</c> domain's captured bodies — before they are retained, and so is the base64 reply, whose
    /// reservation is held until that reply has actually been written to the transport. A body the allowance
    /// refuses is a <c>-32000</c> error and <b>not</b> a resolved pause: the client can still continue,
    /// fulfil or fail the response afterwards.
    /// </para>
    /// <para>
    /// <b>A read and a terminal decision are serialised.</b> A <c>continueResponse</c>, <c>fulfillRequest</c>
    /// or <c>failRequest</c> arriving while a read is in flight is refused with an explicit error rather
    /// than racing the stream; detaching, disabling and the fetch's own cancellation still always end the
    /// pause, behind the read.
    /// </para>
    /// </remarks>
    protected override async ValueTask<GetResponseBodyResponse> GetResponseBodyAsync(GetResponseBodyRequest parameters, CommandContext context)
    {
        if (!_pausedResponses.TryGetValue(parameters.RequestId, out var paused))
        {
            // A request-stage or authentication pause is a real identifier naming a moment at which there is
            // no response yet, which is a different thing from an identifier naming nothing.
            if (_paused.ContainsKey(parameters.RequestId) || _pausedAuth.ContainsKey(parameters.RequestId))
            {
                Throw.ServerError(
                    "Can only get response body on requests captured after headers received.",
                    "that identifier names a request-stage pause, where the response does not exist yet");
            }

            Throw.ServerError("Invalid InterceptionId.");
        }

        if (!paused!.TryBeginRead())
        {
            Throw.ServerError(
                "Invalid state for Fetch.getResponseBody",
                "the response has already been answered, or another read of it is still in flight");
        }

        try
        {
            using var abandoned = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, paused.Abandoned);

            var body = await paused.Body.ReadAsync(abandoned.Token).ConfigureAwait(false);
            if (body is null)
            {
                return Throw.ServerError<GetResponseBodyResponse>(
                    "Response body exceeds the page's captured-response allowance.",
                    "see BrowserOptions.MaxCapturedResponseBytes; the response is still paused and can be continued, fulfilled or failed");
            }

            return Encode(body.Value, paused.Body, context);
        }
        finally
        {
            paused.EndRead();
        }
    }

    /// <summary>Base64-encodes one body, having reserved what the encoded reply will occupy.</summary>
    /// <remarks>
    /// <para>
    /// A retained-payload cap alone is not a memory bound: the reply is a second copy of the body, four
    /// characters per three bytes and two bytes per character, and the serialized message is a third. Both
    /// are charged to the same allowance before either exists, and the lease is held until the reply has
    /// actually left the process rather than until this command returns — which is what stops repeated
    /// commands queueing unboundedly many encoded copies behind a slow transport.
    /// </para>
    /// <para>
    /// The reservation is released by the session, once, whether the reply was written or the write failed.
    /// </para>
    /// </remarks>
    private static GetResponseBodyResponse Encode(ReadOnlyMemory<byte> body, PageResponseBodyReader reader, CommandContext context)
    {
        // 4 characters per 3 bytes, 2 bytes per character, and the same again for the message the reply is
        // serialized into.
        var encoded = 4L * ((body.Length + 2L) / 3L);
        var cost = 4L * encoded;

        IDisposable? lease = null;
        if (cost > int.MaxValue || !reader.TryReserve((int) cost, out lease))
        {
            return Throw.ServerError<GetResponseBodyResponse>(
                "Response body exceeds the page's captured-response allowance.",
                "the base64 reply would not fit; see BrowserOptions.MaxCapturedResponseBytes");
        }

        try
        {
            var text = Convert.ToBase64String(body.Span);

            context.HoldUntilReplyWritten(lease);
            lease = null;

            return new GetResponseBodyResponse { Body = text, Base64Encoded = true };
        }
        finally
        {
            lease?.Dispose();
        }
    }

    /// <summary>Lets every paused request and response go, which disabling and detaching both do.</summary>
    internal void ContinueEverything()
    {
        foreach (var id in _paused.Keys)
        {
            if (_paused.TryRemove(id, out var paused))
            {
                paused.Answer(PageNetworkDecision.Proceed);
            }
        }

        foreach (var id in _pausedResponses.Keys)
        {
            if (_pausedResponses.TryRemove(id, out var paused))
            {
                // Terminate rather than answer: disabling and detaching end a pause whatever else is
                // happening to it, and a read in flight only defers that by as long as the read lasts.
                paused.Terminate(PageNetworkResponseDecision.Proceed);
            }
        }

        foreach (var id in _pausedAuth.Keys)
        {
            if (_pausedAuth.TryRemove(id, out var paused))
            {
                paused.Answer(PageNetworkAuthDecision.Proceed);
            }
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <c>interceptResponse</c> is accepted and never honoured: a client asking to be paused again after the
    /// response is asking for a second pause on a request it has already released, and the way to get a
    /// response-stage pause here is a pattern that asks for one.
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
        IReadOnlyList<PageHeader>? headers = parameters.ResponseHeaders is { } declared
            ? Headers(declared)
            : Headers(parameters.BinaryResponseHeaders);

        var body = parameters.Body is { } encoded ? Convert.FromBase64String(encoded) : [];

        // Either stage: Chrome answers a response-stage pause with this command too, and there the bytes the
        // server sent are discarded unread rather than never asked for — including a prefix a body read
        // took, which is released rather than replayed because nobody will receive this response.
        if (_pausedResponses.TryGetValue(parameters.RequestId, out var pausedResponse))
        {
            Answer(
                parameters.RequestId,
                pausedResponse,
                PageNetworkResponseDecision.Fulfill(parameters.ResponseCode, headers, body, parameters.ResponsePhrase),
                "Fetch.fulfillRequest");

            return new ValueTask<EmptyResult>(EmptyResult.Instance);
        }

        var paused = Take(parameters.RequestId);
        paused.Answer(PageNetworkDecision.Fulfill(parameters.ResponseCode, headers, body, parameters.ResponsePhrase));
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <inheritdoc/>
    protected override ValueTask<EmptyResult> FailRequestAsync(FailRequestRequest parameters, CommandContext context)
    {
        // Either stage, like fulfillRequest above.
        if (_pausedResponses.TryGetValue(parameters.RequestId, out var pausedResponse))
        {
            Answer(
                parameters.RequestId,
                pausedResponse,
                PageNetworkResponseDecision.Fail(NetworkError(parameters.ErrorReason)),
                "Fetch.failRequest");

            return new ValueTask<EmptyResult>(EmptyResult.Instance);
        }

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

    /// <summary>The paused response one identifier names, or Chrome's own refusal.</summary>
    /// <remarks>
    /// It looks up rather than removes, because the claim is the state machine's: a response with a body
    /// read in flight must be refused rather than quietly taken out of the map and left unanswerable.
    /// </remarks>
    private PausedResponse TakeResponse(string requestId)
    {
        if (!_pausedResponses.TryGetValue(requestId, out var paused))
        {
            Throw.ServerError("Invalid InterceptionId.");
        }

        return paused!;
    }

    /// <summary>Answers a paused response, or refuses because a body read of it is in flight.</summary>
    private void Answer(string requestId, PausedResponse paused, PageNetworkResponseDecision decision, string command)
    {
        switch (paused.TryAnswer(decision))
        {
            case PauseClaim.Taken:
                _pausedResponses.TryRemove(requestId, out _);
                return;

            case PauseClaim.Reading:
                Throw.ServerError(
                    "Invalid state for " + command,
                    "a getResponseBody of that response is still in flight; answer it once the read has returned");
                return;

            default:
                Throw.ServerError("Invalid InterceptionId.");
                return;
        }
    }

    /// <summary>Whether one pattern asks about the response stage.</summary>
    private static bool IsResponseStage(RequestPattern pattern)
        => pattern.RequestStage is { } stage && string.Equals(stage, RequestStageValues.Response, StringComparison.Ordinal);

    /// <summary>Whether one pattern asks about one request.</summary>
    /// <remarks>
    /// A pattern is asked about one stage at a time; a pattern with no
    /// <c>urlPattern</c> means every URL, which is the protocol's default.
    /// </remarks>
    private static bool Matches(RequestPattern pattern, PageNetworkRequest request, string stage = RequestStageValues.Request)
    {
        // A pattern with no requestStage means the protocol's default, which is Request.
        var wanted = pattern.RequestStage ?? RequestStageValues.Request;
        if (!string.Equals(wanted, stage, StringComparison.Ordinal))
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
        PageRequestKind.EventSource => ProtocolNetwork.ResourceTypeValues.EventSource,
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

    /// <summary>
    /// The response stage's own pause. A separate type from <see cref="PausedRequest"/> because the two
    /// stages are answered with different decisions, and a separate map for the same reason — one identifier
    /// space, two things it can name, and a command that looked in the wrong one would answer a client's
    /// <c>continueResponse</c> with "Invalid InterceptionId" for an identifier it had just been given.
    /// </summary>
    private sealed class PausedResponse
    {
        private readonly object _gate = new();
        private bool _reading;
        private bool _answered;
        private PageNetworkResponseDecision? _deferred;

        /// <summary>The bounded read of this response's body, valid for as long as the pause is.</summary>
        internal required PageResponseBodyReader Body { get; init; }

        /// <summary>Cancelled when the fetch this pause belongs to is abandoned or times out.</summary>
        internal required CancellationToken Abandoned { get; init; }

        internal TaskCompletionSource<PageNetworkResponseDecision> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Claims the pause for a body read, refusing while one is in flight or after a decision.</summary>
        internal bool TryBeginRead()
        {
            lock (_gate)
            {
                if (_reading || _answered)
                {
                    return false;
                }

                _reading = true;
                return true;
            }
        }

        /// <summary>Ends a read, applying whatever decision was deferred behind it.</summary>
        internal void EndRead()
        {
            PageNetworkResponseDecision? apply = null;

            lock (_gate)
            {
                _reading = false;

                if (_deferred is { } pending)
                {
                    _deferred = null;
                    _answered = true;
                    apply = pending;
                }
            }

            if (apply is { } decision)
            {
                Completion.TrySetResult(decision);
            }
        }

        /// <summary>
        /// A client's terminal command, which is refused rather than raced while a read is in flight.
        /// </summary>
        internal PauseClaim TryAnswer(PageNetworkResponseDecision decision)
        {
            lock (_gate)
            {
                if (_answered)
                {
                    return PauseClaim.Gone;
                }

                if (_reading)
                {
                    return PauseClaim.Reading;
                }

                _answered = true;
            }

            Completion.TrySetResult(decision);
            return PauseClaim.Taken;
        }

        /// <summary>
        /// Detach, disable and the fetch's own cancellation, which always end the pause.
        /// </summary>
        /// <remarks>
        /// Deferred behind a read rather than racing it: swapping the response's content out from under a
        /// read in flight is the one thing the replay cannot survive, and the read is bounded by the very
        /// token that fires here, so the deferral is over as soon as that read gives up.
        /// </remarks>
        internal void Terminate(PageNetworkResponseDecision decision)
        {
            lock (_gate)
            {
                if (_answered)
                {
                    return;
                }

                if (_reading)
                {
                    _deferred ??= decision;
                    return;
                }

                _answered = true;
            }

            Completion.TrySetResult(decision);
        }
    }

    /// <summary>What claiming a paused response for one decision answered.</summary>
    private enum PauseClaim
    {
        /// <summary>The pause is this caller's, and has been answered.</summary>
        Taken,

        /// <summary>A body read of the same response is in flight; the pause is untouched.</summary>
        Reading,

        /// <summary>Something else has already answered it.</summary>
        Gone,
    }

    /// <summary>
    /// A challenge's own pause, and a third map for the third thing one identifier space can name.
    /// </summary>
    /// <remarks>
    /// It carries the challenge as well as the answer, because <c>continueWithAuth</c> has to refuse
    /// credentials offered for a scheme this browser cannot answer, and the scheme is only known here.
    /// </remarks>
    private sealed class PausedAuth
    {
        internal required PageNetworkAuthChallenge Challenge { get; init; }

        internal TaskCompletionSource<PageNetworkAuthDecision> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Answers the pause, ignoring a second answer for the same challenge.</summary>
        internal void Answer(PageNetworkAuthDecision decision) => Completion.TrySetResult(decision);
    }
}
