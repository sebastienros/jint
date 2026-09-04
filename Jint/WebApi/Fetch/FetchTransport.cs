#if NET8_0_OR_GREATER
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jint.WebApi.Url.Parsing;

namespace Jint.WebApi.Fetch;

/// <summary>
/// Why a fetch failed, for the failure map <c>FetchOperation</c> turns into a rejection.
/// </summary>
internal enum FetchFailureKind
{
    /// <summary>The scheme list or the host's <c>UrlFilter</c> refused the URL — on the first hop or on a redirect.</summary>
    PolicyDenied,

    /// <summary>More redirects than <c>Options.FetchOptions.MaxRedirects</c> allows.</summary>
    RedirectLimit,

    /// <summary>A redirect arrived and the request's redirect mode is <c>error</c>.</summary>
    RedirectRefused,

    /// <summary>The body exceeded <c>Options.FetchOptions.MaxResponseBytes</c>.</summary>
    ResponseTooLarge,

    /// <summary>DNS, connection, TLS, protocol — anything the transport itself reported.</summary>
    Network,
}

/// <summary>
/// The failure a fetch ended in, as a CLR exception. It never reaches script: <c>FetchOperation</c> turns it
/// into a <c>TypeError</c> whose message says only "Failed to fetch", and attaches this to the error
/// <i>value</i> so that the host — and only the host — can read the detail through
/// <see cref="JintException.TryGetClrException"/>.
/// </summary>
internal sealed class FetchFailureException : Exception
{
    internal FetchFailureException(FetchFailureKind kind, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Kind = kind;
    }

    internal FetchFailureKind Kind { get; }
}

/// <summary>
/// The request as a plain CLR value, taken on the engine thread and handed to the transport.
/// </summary>
/// <remarks>
/// Nothing here is engine state: the URL record and the header list are both from the deliberately
/// engine-free layers, the body is a byte window, and a streaming body is an <see cref="HttpContent"/> whose
/// engine half the transport never reaches. That is what lets the whole of <see cref="FetchTransport"/> run
/// on a thread pool thread while the engine goes on running script.
/// </remarks>
internal sealed class FetchRequestSnapshot
{
    internal required string Method { get; init; }

    internal required UrlRecord Url { get; init; }

    internal required List<HeaderEntry> Headers { get; init; }

    internal required ReadOnlyMemory<byte>? Body { get; init; }

    /// <summary>
    /// The body as a live <see cref="HttpContent"/> rather than as bytes, for the one shape that has no
    /// bytes to give: a <c>ReadableStream</c> body sent with <c>duplex: "half"</c>. Exactly one of this and
    /// <see cref="Body"/> is ever non-null, and this one being non-null is the transport's way of knowing
    /// the body has no <i>source</i> in the standard's sense — which is what decides whether a redirect can
    /// be followed. See <see cref="FetchRequestBodyStream"/>.
    /// </summary>
    internal HttpContent? BodyContent { get; init; }

    /// <summary>One of <c>follow</c>, <c>error</c> or <c>manual</c>.</summary>
    internal required string Redirect { get; init; }

    /// <summary>
    /// One of <c>omit</c>, <c>same-origin</c> or <c>include</c> — how far the host's cookie jar may be
    /// consulted for this request. Defaults to the mode a bare <c>new Request()</c> has.
    /// </summary>
    internal string Credentials { get; init; } = JsRequest.CredentialsSameOrigin;

    /// <summary>
    /// The referrer this request starts from, already resolved through "client" to
    /// <c>Options.WebApi.Fetch.Referrer</c>, or <see langword="null"/> for no referrer at all.
    /// </summary>
    internal UrlRecord? Referrer { get; init; }

    /// <summary>The policy that decides how much of <see cref="Referrer"/> each hop discloses.</summary>
    internal ReferrerPolicy ReferrerPolicy { get; init; } = ReferrerPolicy.StrictOriginWhenCrossOrigin;
}

/// <summary>
/// The host's policy, read once on the engine thread so the transport never touches
/// <see cref="Options"/> from a background thread.
/// </summary>
internal sealed class FetchPolicy
{
    internal required string[] AllowedSchemes { get; init; }

    internal required Func<Uri, bool> UrlFilter { get; init; }

    internal required long MaxResponseBytes { get; init; }

    internal required int MaxRedirects { get; init; }

    /// <summary>
    /// The URL <c>Options.WebApi.Fetch.Origin</c> named, kept for its origin alone, or
    /// <see langword="null"/> when no <c>Origin</c> header is to be sent.
    /// </summary>
    internal UrlRecord? Origin { get; init; }

    /// <summary>
    /// What a <c>same-origin</c> credentials mode compares a hop against — the configured origin, or the
    /// base URL's, or nothing.
    /// </summary>
    internal UrlRecord? SameOriginReference { get; init; }

    /// <summary>The host's cookie store, or <see langword="null"/> when it granted none.</summary>
    internal CookieJar? CookieJar { get; init; }

    /// <summary>
    /// The <c>User-Agent</c> a hop carries when the request named none itself, or <see langword="null"/> to
    /// send no such header — https://fetch.spec.whatwg.org/#default-user-agent-value.
    /// </summary>
    /// <remarks>
    /// It is <c>Options.WebApi.Fetch.UserAgent</c> for every request an <i>engine</i> makes; a host driving
    /// its own pipeline through this transport — a document fetch, a subresource — passes the one its
    /// browsing context reports, so that a page's script and its requests cannot say different things.
    /// </remarks>
    internal string? UserAgent { get; init; }

    /// <summary>
    /// The whole check one hop passes: the scheme list, the <see cref="Uri"/> grammar and the host's filter.
    /// Refusing is what stops a redirect to <c>http://169.254.169.254/</c> from reaching the socket.
    /// </summary>
    internal bool Allows(UrlRecord url, out Uri uri) => TryResolve(url, out uri) && UrlFilter(uri);

    /// <summary>
    /// The half of <see cref="Allows"/> that has no side effect: the scheme list and the <see cref="Uri"/>
    /// grammar. It exists so that the first hop — whose filter the engine thread has already run — is not
    /// asked a second time, because <see cref="UrlFilter"/> is host code and being invoked twice per request
    /// is observable to it.
    /// </summary>
    internal bool TryResolve(UrlRecord url, out Uri uri)
    {
        uri = null!;

        var scheme = url.Scheme;
        var allowed = false;
        foreach (var candidate in AllowedSchemes)
        {
            if (string.Equals(candidate, scheme, StringComparison.OrdinalIgnoreCase))
            {
                allowed = true;
                break;
            }
        }

        if (!allowed)
        {
            return false;
        }

        // The WHATWG serialization is always an absolute URL, but Uri has its own grammar and a host it
        // cannot represent must be refused rather than guessed at.
        if (!Uri.TryCreate(url.Serialize(), UriKind.Absolute, out var parsed))
        {
            return false;
        }

        uri = parsed;
        return true;
    }
}

/// <summary>
/// A response whose body has <b>not</b> been read: the message itself, plus the two facts about the request
/// that produced it which only the redirect loop knows.
/// </summary>
/// <remarks>
/// What <see cref="FetchTransport.SendForStreamAsync"/> answers with, for the one consumer that must not have
/// its body buffered — <c>EventSource</c>, whose response is a stream that stays open. The caller owns the
/// <see cref="HttpResponseMessage"/> and must dispose it; the buffered path does that for itself.
/// </remarks>
internal sealed class FetchExchange : IDisposable
{
    internal required HttpResponseMessage Response { get; init; }

    /// <summary>
    /// The method the last hop was made with, which is not always the one the request started with: a
    /// redirect rewrites <c>POST</c> to <c>GET</c>. What answers whether the response has a body at all.
    /// </summary>
    internal required string Method { get; init; }

    /// <summary>The URL that produced this response, i.e. the last hop of the redirect chain.</summary>
    internal required UrlRecord Url { get; init; }

    /// <summary>
    /// The same URL as a <see cref="Uri"/>, which is what the host-facing cookie jar and observer are given
    /// and what the policy check already had to produce.
    /// </summary>
    internal required Uri RequestUri { get; init; }

    internal required bool Redirected { get; init; }

    /// <summary>Whether a <see cref="FetchObserver"/> answered this hop instead of the network.</summary>
    internal bool FromInterception { get; init; }

    /// <summary>
    /// When the hop that produced this response went out and when its headers came back, or
    /// <see langword="null"/> when an observer fulfilled it and nothing went on the wire.
    /// </summary>
    /// <remarks>
    /// The redirect loop is the only place either instant exists, and every caller of
    /// <see cref="FetchTransport.SendForStreamAsync"/> reports the final response itself — so this is what
    /// carries the reading from the one to the others.
    /// </remarks>
    internal FetchTiming? Timing { get; init; }

    public void Dispose() => Response.Dispose();
}

/// <summary>
/// A response as plain CLR data, classified off the engine thread. <c>FetchOperation</c> turns it into a
/// <c>Response</c> object on the engine thread, in the realm the fetch started in.
/// </summary>
internal sealed class FetchResponseSnapshot
{
    internal required int Status { get; init; }

    internal required string StatusText { get; init; }

    internal required List<HeaderEntry> Headers { get; init; }

    /// <summary>
    /// The body still on the wire, or <see langword="null"/> for the two answers the standard gives no body:
    /// a null body status (https://fetch.spec.whatwg.org/#null-body-status) and a response to
    /// <c>HEAD</c>. The connection is <b>still open</b> when this
    /// reaches the engine thread: <see cref="FetchBodyStream.Attach"/> is what starts reading it, and
    /// <see cref="FetchBodyStream.Dispose"/> is what lets it go if nothing ever will.
    /// </summary>
    internal required FetchBodyStream? Body { get; init; }

    /// <summary>The last URL the request reached, serialized without its fragment.</summary>
    internal required string Url { get; init; }

    internal required bool Redirected { get; init; }
}

/// <summary>
/// The HTTP half of <c>fetch</c>: the redirect loop, the per-hop policy re-check and the bounded body read.
/// <para>
/// https://fetch.spec.whatwg.org/#http-fetch
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing here touches the engine.</b> It takes a <see cref="FetchRequestSnapshot"/> and a
/// <see cref="FetchPolicy"/> and answers a <see cref="FetchResponseSnapshot"/> or throws a
/// <see cref="FetchFailureException"/>; every type it mentions is a plain CLR value. The engine is not
/// thread-safe and the script that started the fetch goes on running while this does its work.
/// </para>
/// <para>
/// <b>Redirects are followed here rather than by <see cref="HttpClient"/>.</b> That is the whole reason for
/// the loop: an automatic redirect would be followed underneath the scheme and <c>UrlFilter</c> checks, so a
/// server answering <c>302 Location: http://169.254.169.254/latest/meta-data/</c> would reach the cloud
/// metadata endpoint however carefully the host had written its filter.
/// </para>
/// <para>
/// <b>The final response's body is not read here.</b> Only its headers are: the snapshot carries a
/// <see cref="FetchBodyStream"/> that still owns the open <see cref="HttpResponseMessage"/>, and the engine
/// thread turns it into a <c>ReadableStream</c> whose <c>pull</c> drives the reading. Every response the loop
/// walks <i>past</i> — a redirect — is disposed here as before.
/// </para>
/// </remarks>
internal static class FetchTransport
{
    /// <summary>
    /// The headers a redirect that rewrites the method to GET must drop with the body it drops —
    /// https://fetch.spec.whatwg.org/#request-body-header-name.
    /// </summary>
    private static readonly string[] _bodyHeaderNames = ["content-encoding", "content-language", "content-location", "content-type"];

    /// <summary>
    /// The headers stripped when a redirect crosses to another origin. The Fetch Standard removes
    /// <c>Authorization</c> (https://fetch.spec.whatwg.org/#http-redirect-fetch step 13); <c>Cookie</c> and
    /// <c>Proxy-Authorization</c> are stripped for the same reason, and are what every server-side client
    /// does — a credential a script attached for one host must not travel to whichever host that one names.
    /// </summary>
    private static readonly string[] _crossOriginHeaderNames = ["authorization", "cookie", "proxy-authorization"];

    /// <summary>
    /// The client used when the host supplied none. Created once per process and <b>never disposed</b>: it
    /// holds a connection pool that is worth sharing, and there is no moment at which Jint knows no engine
    /// will fetch again. A host that needs to control the lifetime — or to interpose a
    /// <c>DelegatingHandler</c> — supplies its own through <c>Options.FetchOptions.HttpClient</c>.
    /// </summary>
    /// <remarks>
    /// <c>AllowAutoRedirect</c> is off because the loop below owns redirects; <c>UseCookies</c> is off
    /// because a cookie jar shared by every engine in the process would be a cross-tenant channel;
    /// <c>PooledConnectionLifetime</c> is two minutes so that DNS changes are picked up; and the client's own
    /// <c>Timeout</c> is infinite because the deadline rides the cancellation token, where the operation can
    /// tell a timeout apart from an abort.
    /// </remarks>
    private static readonly Lazy<HttpClient> _sharedClient = new(CreateSharedClient, LazyThreadSafetyMode.ExecutionAndPublication);

    internal static HttpClient SharedClient => _sharedClient.Value;

    private static HttpClient CreateSharedClient()
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            AutomaticDecompression = DecompressionMethods.All,
            AllowAutoRedirect = false,
            UseCookies = false,
        };

        return new HttpClient(handler, disposeHandler: true)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
    }

    /// <summary>
    /// The <see cref="HttpClient"/> a request goes through: the host's per-request factory, the host's own
    /// client, or the shared default — in that order of precedence.
    /// </summary>
    /// <remarks>
    /// Called on the engine thread, once per request, which is what lets a host factory read per-request state
    /// through <c>engine.HostDefined</c>. Answers <see langword="null"/> only when a host factory
    /// did; each caller decides what that failure looks like to script.
    /// </remarks>
    internal static HttpClient? ResolveClient(Engine engine, Options.FetchOptions options)
    {
        if (options.HttpClientFactory is { } factory)
        {
            return factory(engine);
        }

        return options.HttpClient ?? SharedClient;
    }

    /// <summary>
    /// Runs the request, following redirects itself, and begins the response: headers in hand, the body left
    /// on the wire behind a <see cref="FetchBodyStream"/> that owns the connection from here on.
    /// </summary>
    internal static async Task<FetchResponseSnapshot> SendAsync(
        HttpClient client,
        FetchRequestSnapshot request,
        FetchPolicy policy,
        CancellationToken cancellationToken,
        FetchObservation? observation = null)
    {
        var exchange = await SendForStreamAsync(client, request, policy, cancellationToken, observation).ConfigureAwait(false);
        var handedOver = false;
        try
        {
            var snapshot = await BeginResponseAsync(exchange, policy, observation, cancellationToken).ConfigureAwait(false);
            handedOver = snapshot.Body is not null;
            return snapshot;
        }
        finally
        {
            // Cleared only once the body stream has taken ownership of the connection; a bodyless response,
            // and every path that throws, still disposes here exactly as the buffered path always did.
            if (!handedOver)
            {
                exchange.Dispose();
            }
        }
    }

    /// <summary>
    /// The half of <see cref="SendAsync"/> that reaches the response: the redirect loop and the per-hop policy
    /// re-check, stopping with the headers in hand and the body untouched.
    /// </summary>
    /// <remarks>
    /// <b>The caller owns the returned <see cref="FetchExchange"/></b> and must dispose it — which is exactly
    /// what makes it usable for a response that is a stream rather than a document. <c>EventSource</c> reads
    /// that stream itself; <see cref="SendAsync"/> hands it straight to the bounded read below.
    /// </remarks>
    internal static async Task<FetchExchange> SendForStreamAsync(
        HttpClient client,
        FetchRequestSnapshot request,
        FetchPolicy policy,
        CancellationToken cancellationToken,
        FetchObservation? observation = null)
    {
        var exchange = await SendForStreamCoreAsync(client, request, policy, cancellationToken, observation).ConfigureAwait(false);

        if (observation is null)
        {
            return exchange;
        }

        try
        {
            return await AnswerResponseAsync(exchange, observation, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            exchange.Dispose();
            throw;
        }
    }

    /// <summary>
    /// The response stage: the one place every lane passes through with the headers in hand and the body
    /// still on the socket, so the observer's answer reaches a document fetch, a subresource, an
    /// <c>XMLHttpRequest</c> and an <c>EventSource</c> and not only <c>fetch()</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It asks, it does not report.</b> Who tells the observer about the final response is unchanged and
    /// deliberately per lane: <see cref="BeginResponseAsync"/> does it for <see cref="SendAsync"/>, because
    /// the list it reports is the merged one the <c>Response</c> object will carry, and every caller of this
    /// method owes <c>FetchObservation.FinalResponse</c> for the same reason. Both therefore report what the
    /// answer here produced rather than what the socket said, which is what a client that rewrote a status
    /// expects to see afterwards.
    /// </para>
    /// <para>
    /// <b>A substitution disposes the exchange it replaces</b>, which closes the connection with the body
    /// unread — the same thing a caller that drops a <c>Response</c> does.
    /// </para>
    /// </remarks>
    private static async Task<FetchExchange> AnswerResponseAsync(
        FetchExchange exchange,
        FetchObservation observation,
        CancellationToken cancellationToken)
    {
        var response = exchange.Response;

        var snapshot = new ObservedFetchResponse
        {
            Id = observation.Id,
            Url = exchange.RequestUri,
            Status = (int) response.StatusCode,
            StatusText = response.ReasonPhrase ?? string.Empty,
            Headers = ObservedHeaders(response),
            FromInterception = exchange.FromInterception,
            IsRedirect = false,
            Timing = exchange.Timing,
        };

        var interception = await observation.ResponseAsync(snapshot, cancellationToken).ConfigureAwait(false);
        if (interception is null)
        {
            return exchange;
        }

        if (interception.Kind == FetchInterceptionKind.Fail)
        {
            throw new FetchFailureException(FetchFailureKind.PolicyDenied, interception.Reason ?? "A fetch observer failed the response.");
        }

        if (interception.Kind == FetchInterceptionKind.Fulfill)
        {
            var substitute = new FetchExchange
            {
                Response = BuildResponse(interception.Status, interception.StatusText, interception.Headers, interception.Body),
                Method = exchange.Method,
                Url = exchange.Url,
                RequestUri = exchange.RequestUri,
                Redirected = exchange.Redirected,
                FromInterception = true,

                // The hop that produced the response being replaced really was sent, so its timing is kept:
                // what the observer substituted is the answer, not the round trip.
                Timing = exchange.Timing,
            };

            exchange.Dispose();
            return substitute;
        }

        // Continue: the status line and the header list may be rewritten, the body may not — it has not been
        // read yet. Rewriting in place keeps the content, and with it the connection the body is coming on.
        if (interception.Status != 0)
        {
            response.StatusCode = (System.Net.HttpStatusCode) interception.Status;
        }

        if (interception.StatusText is { } statusText)
        {
            response.ReasonPhrase = statusText;
        }

        if (interception.Headers is { } headers)
        {
            response.Headers.Clear();
            response.Content.Headers.Clear();

            foreach (var header in headers)
            {
                if (!response.Headers.TryAddWithoutValidation(header.Name, header.Value))
                {
                    response.Content.Headers.TryAddWithoutValidation(header.Name, header.Value);
                }
            }
        }

        return exchange;
    }

    /// <summary>Every value of every header of one response, as the observer surface spells them.</summary>
    private static List<FetchHeader> ObservedHeaders(HttpResponseMessage response)
    {
        var headers = new List<FetchHeader>();
        Collect(response.Headers);
        Collect(response.Content.Headers);
        return headers;

        void Collect(System.Net.Http.Headers.HttpHeaders source)
        {
            foreach (var header in source.NonValidated)
            {
                foreach (var value in header.Value)
                {
                    headers.Add(new FetchHeader(HeaderList.Lowercase(header.Key), value));
                }
            }
        }
    }

    /// <summary>The <see cref="HttpResponseMessage"/> an interception's status, headers and body make.</summary>
    private static HttpResponseMessage BuildResponse(
        int status,
        string? statusText,
        IReadOnlyList<FetchHeader>? headers,
        ReadOnlyMemory<byte> body)
    {
        var response = new HttpResponseMessage((System.Net.HttpStatusCode) status)
        {
            Content = new ReadOnlyMemoryContent(body),
        };

        if (statusText is not null)
        {
            response.ReasonPhrase = statusText;
        }

        // ReadOnlyMemoryContent supplies one of its own, and an interception that named none must not be
        // given a type it did not choose.
        response.Content.Headers.ContentType = null;

        if (headers is not null)
        {
            foreach (var header in headers)
            {
                if (!response.Headers.TryAddWithoutValidation(header.Name, header.Value))
                {
                    response.Content.Headers.TryAddWithoutValidation(header.Name, header.Value);
                }
            }
        }

        return response;
    }

    private static async Task<FetchExchange> SendForStreamCoreAsync(
        HttpClient client,
        FetchRequestSnapshot request,
        FetchPolicy policy,
        CancellationToken cancellationToken,
        FetchObservation? observation = null)
    {
        var url = request.Url;
        var method = request.Method;
        var body = request.Body;
        var content = request.BodyContent;
        var headers = new List<HeaderEntry>(request.Headers);
        AppendDefaultAccept(headers);
        var redirectCount = 0;

        // https://fetch.spec.whatwg.org/#concept-main-fetch step 6 is re-run per hop, because a redirect
        // re-enters main fetch: what a hop discloses is computed from what the previous hop settled on, so a
        // policy that has already narrowed the referrer to an origin never widens it again.
        var referrer = request.Referrer;
        ObservedFetchResponse? redirectResponse = null;

        // One retry per request, not per hop: an observer has one credential to offer, so a second ask has
        // nothing new to answer with and a loop is worse than delivering the 401.
        var authRetried = false;

        while (true)
        {
            // The first hop's filter has already been run on the engine thread, where the fetch was started;
            // running it again here would call host code twice for one request, which the host can see.
            Uri uri;
            var allowed = redirectCount == 0 ? policy.TryResolve(url, out uri) : policy.Allows(url, out uri);
            if (!allowed)
            {
                throw new FetchFailureException(FetchFailureKind.PolicyDenied, $"The fetch policy refused '{url.Serialize()}'.");
            }

            // The headers the engine appends for this hop and this hop alone. They are recomputed against the
            // hop's own URL rather than carried forward, which is what makes a cross-origin redirect narrow
            // the Referer, re-decide the Origin, and ask the jar for the new host's cookies instead.
            var effective = new List<HeaderEntry>(headers);
            var computedReferrer = Append(effective, request, policy, referrer, url, uri, method);

            if (observation is not null)
            {
                var interception = await Observe(observation, request, effective, method, uri, body, content, redirectCount, redirectResponse, cancellationToken).ConfigureAwait(false);
                if (interception is not null)
                {
                    if (interception.Kind == FetchInterceptionKind.Fail)
                    {
                        throw new FetchFailureException(FetchFailureKind.PolicyDenied, interception.Reason ?? "A fetch observer failed the request.");
                    }

                    if (interception.Kind == FetchInterceptionKind.Fulfill)
                    {
                        return Fulfil(interception, method, url, uri, redirectCount);
                    }

                    // Continue: the rewrites apply to the hop that was answered, and the next hop is computed
                    // from the request as it was — a redirect is offered to the observer again.
                    if (interception.Method is { } rewrittenMethod)
                    {
                        method = rewrittenMethod;
                    }

                    if (interception.Headers is { } rewrittenHeaders)
                    {
                        effective.Clear();
                        foreach (var header in rewrittenHeaders)
                        {
                            effective.Add(new HeaderEntry(HeaderList.Lowercase(header.Name), header.Value));
                        }
                    }

                    if (interception.HasBody)
                    {
                        body = interception.Body;
                        content = null;
                    }

                    if (interception.Url is { } rewrittenUrl)
                    {
                        // Re-checked exactly as a redirect target is: an observer decides what a request
                        // says, never where the host's own policy lets it go.
                        var parsed = UrlParser.Parse(rewrittenUrl.AbsoluteUri);
                        if (parsed is null || !policy.Allows(parsed, out uri))
                        {
                            throw new FetchFailureException(FetchFailureKind.PolicyDenied, $"The fetch policy refused '{rewrittenUrl}'.");
                        }

                        url = parsed;
                    }
                }
            }

            HttpResponseMessage response;

            // The two readings that make a FetchTiming, taken either side of the one call in this process
            // that knows when the hop left and when its headers came back. The wall-clock instant is the
            // origin a host can compare with a timestamp of its own; the monotonic pair is what the elapsed
            // time is measured on, so a clock adjusted mid-request cannot produce a negative duration.
            var sentAt = DateTimeOffset.UtcNow;
            var sentTicks = Stopwatch.GetTimestamp();

            try
            {
                using var message = BuildRequest(method, uri, effective, body, content);
                response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException and not FetchFailureException)
            {
                throw new FetchFailureException(FetchFailureKind.Network, $"The request to '{uri}' failed: {ex.Message}", ex);
            }

            var timing = new FetchTiming(sentAt, Stopwatch.GetElapsedTime(sentTicks));

            // The response is disposed here only while the loop goes on to another hop; the one it ends with
            // belongs to the caller, whose body may not have been read yet.
            var handedOver = false;
            try
            {
                // Every response stores its cookies, a redirect's included: a login that answers 302 with a
                // Set-Cookie is the shape this exists for.
                StoreCookies(request, policy, url, uri, response);

                // https://fetch.spec.whatwg.org/#concept-http-fetch step 6, the three redirect modes. "manual"
                // is Node's reading of it: the redirect response itself is handed to the script, Location and
                // all, rather than a browser's opaque-redirect filtered response — which exists to hide a
                // cross-origin redirect from a page, a concern an embedded engine with no origin does not have.
                var status = (int) response.StatusCode;

                // The challenge is offered before the response is handed anywhere, because answering it
                // re-sends this hop rather than producing one. It is asked here, in the core, beside the
                // per-hop request ask and not beside the final-response one: a 401 on a document fetch, on a
                // subresource and on an XMLHttpRequest all pass through here, and only fetch() takes the lane
                // AnswerResponseAsync is asked in.
                if (status == UnauthorizedStatus
                    && !authRetried
                    && observation is not null
                    && ReadChallenge(response) is { } challenge
                    && await AuthorizeAsync(observation, challenge, uri, headers, cancellationToken).ConfigureAwait(false))
                {
                    // Not a redirect and not counted as one: the same URL is asked again, once, with the
                    // Authorization header now in `headers` so every later hop carries it - until one crosses
                    // to another origin, where _crossOriginHeaderNames strips it as the standard asks.
                    authRetried = true;
                    continue;
                }

                if (!FetchValues.IsRedirectStatus(status)
                    || string.Equals(request.Redirect, JsRequest.RedirectManual, StringComparison.Ordinal))
                {
                    handedOver = true;
                    return new FetchExchange { Response = response, Method = method, Url = url, RequestUri = uri, Redirected = redirectCount > 0, Timing = timing };
                }

                if (string.Equals(request.Redirect, JsRequest.RedirectError, StringComparison.Ordinal))
                {
                    throw new FetchFailureException(FetchFailureKind.RedirectRefused, $"'{uri}' answered a redirect and the request's redirect mode is 'error'.");
                }

                // "If locationURL is null, then return actualResponse" — a 301 with no Location header is an
                // ordinary response, not a failure.
                var location = TryGetRedirectTarget(response, url);
                if (location is null)
                {
                    handedOver = true;
                    return new FetchExchange { Response = response, Method = method, Url = url, RequestUri = uri, Redirected = redirectCount > 0, Timing = timing };
                }

                // The redirect reaches the observer before the hop it causes does, and again on that hop's
                // own snapshot, so a protocol layer can pair the two without holding state of its own.
                if (observation is not null)
                {
                    redirectResponse = Observed(observation, response, uri, isRedirect: true, fromInterception: false, timing: timing);
                    observation.Response(redirectResponse);
                }

                // https://fetch.spec.whatwg.org/#http-redirect-fetch step 12: "If internalResponse's status
                // is not 303, request's body is non-null, and request's body's source is null, then return a
                // network error." A streamed body has already gone down the wire and cannot go again; a 303
                // is exempt because it drops the body along with the method.
                if (content is not null && status != 303)
                {
                    throw new FetchFailureException(
                        FetchFailureKind.Network,
                        $"'{uri}' answered a {status} redirect, which would have to re-send a request body that is a ReadableStream.");
                }

                if (++redirectCount > policy.MaxRedirects)
                {
                    throw new FetchFailureException(
                        FetchFailureKind.RedirectLimit,
                        $"The request to '{request.Url.Serialize()}' exceeded the limit of {policy.MaxRedirects} redirects.");
                }

                Rewrite(status, ref method, ref body, ref content, headers, url, location);

                // "Set request's referrer to the result of invoking determine request's referrer" — the value
                // this hop computed becomes the next hop's source, which is what makes the narrowing stick.
                referrer = computedReferrer is null ? null : UrlParser.Parse(computedReferrer);
                url = location;
            }
            finally
            {
                if (!handedOver)
                {
                    response.Dispose();
                }
            }
        }
    }

    /// <summary>
    /// The one authentication scheme this transport can answer from a username and a password alone.
    /// </summary>
    /// <remarks>
    /// <c>Digest</c> needs a nonce exchange and <c>Negotiate</c> and <c>NTLM</c> a handshake bound to the
    /// connection; a transport that hands the socket back after every response holds neither. Both are still
    /// <i>reported</i> — see <see cref="ObservedFetchAuthChallenge.CanProvideCredentials"/>.
    /// </remarks>
    private const string BasicScheme = "Basic";

    /// <summary>The status a server challenges with. A <c>407</c> is a proxy's and is not this engine's.</summary>
    private const int UnauthorizedStatus = 401;

    /// <summary>
    /// The first <c>WWW-Authenticate</c> challenge on a response, or <see langword="null"/> when it carried
    /// none.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The first, not the strongest.</b> A server may offer several and a browser picks the one it likes
    /// best; there is only one this engine can answer, so choosing between them would be a choice with one
    /// outcome. What a client is told is what the server said first, and if that is a scheme this engine
    /// cannot answer, that is the honest report.
    /// </para>
    /// <para>
    /// The realm comes from the challenge's <c>Parameter</c>, which is the whole
    /// <c>auth-param</c> list; only <c>realm</c> is read out of it, unquoted, because that is the one
    /// parameter the protocol's own challenge type carries.
    /// </para>
    /// </remarks>
    private static (string Scheme, string Realm)? ReadChallenge(HttpResponseMessage response)
    {
        foreach (var header in response.Headers.WwwAuthenticate)
        {
            if (string.IsNullOrEmpty(header.Scheme))
            {
                continue;
            }

            return (header.Scheme, ReadRealm(header.Parameter));
        }

        return null;
    }

    /// <summary>Pulls <c>realm="…"</c> out of a challenge's parameter list.</summary>
    private static string ReadRealm(string? parameter)
    {
        if (string.IsNullOrEmpty(parameter))
        {
            return string.Empty;
        }

        foreach (var part in parameter!.Split(','))
        {
            var trimmed = part.Trim();
            if (!trimmed.StartsWith("realm=", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = trimmed.Substring("realm=".Length).Trim();
            if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
            {
                value = value.Substring(1, value.Length - 2);
            }

            return value;
        }

        return string.Empty;
    }

    /// <summary>
    /// Asks the observer about a challenge and, when it answers with credentials this engine can use, puts
    /// them on <paramref name="headers"/> for the retry.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when the hop should be sent again; <see langword="false"/> to deliver the
    /// <c>401</c> as it is — which covers a declined challenge, an uninterested observer, and credentials
    /// offered for a scheme this engine cannot answer.
    /// </returns>
    private static async Task<bool> AuthorizeAsync(
        FetchObservation observation,
        (string Scheme, string Realm) challenge,
        Uri uri,
        List<HeaderEntry> headers,
        CancellationToken cancellationToken)
    {
        var basic = string.Equals(challenge.Scheme, BasicScheme, StringComparison.OrdinalIgnoreCase);

        var decision = await observation.AuthRequiredAsync(
            new ObservedFetchAuthChallenge
            {
                Id = observation.Id,
                Url = uri,
                Status = UnauthorizedStatus,
                Scheme = challenge.Scheme,
                Realm = challenge.Realm,
                CanProvideCredentials = basic,
            },
            cancellationToken).ConfigureAwait(false);

        if (decision is not { HasCredentials: true } || !basic)
        {
            return false;
        }

        // https://www.rfc-editor.org/rfc/rfc7617 - the user-pass is joined by a colon and encoded as UTF-8,
        // which is what the "charset" parameter asks for and what every server that omits it expects anyway.
        var credentials = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes(decision.Username + ":" + decision.Password));

        // The script's own Authorization header, if it wrote one, has already failed against this server -
        // the challenge is the proof - so the answer replaces it rather than joining it.
        headers.RemoveAll(static entry => string.Equals(entry.LowerName, "authorization", StringComparison.OrdinalIgnoreCase));
        headers.Add(new HeaderEntry("authorization", BasicScheme + " " + credentials));
        return true;
    }

    /// <summary>
    /// The three headers the engine adds for one hop — <c>Referer</c>, <c>Origin</c> and <c>Cookie</c> —
    /// answering the referrer it computed so the next hop can start from it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Each is appended only when the script did not set one itself.</b> The Fetch Standard appends all
    /// three unconditionally, because its forbidden request-header list has already stopped a script setting
    /// them; this implementation deliberately does not enforce that list (see <see cref="HeadersGuard"/>), so
    /// appending unconditionally would send two of the same header instead of honouring the script's.
    /// </para>
    /// <para>
    /// A cookie jar that throws is not caught here: a host store that fails is reported as the fetch's
    /// failure, with the exception on the error value, rather than silently sending no cookies.
    /// </para>
    /// </remarks>
    private static string? Append(
        List<HeaderEntry> headers,
        FetchRequestSnapshot request,
        FetchPolicy policy,
        UrlRecord? referrer,
        UrlRecord url,
        Uri uri,
        string method)
    {
        var computed = FetchReferrer.Determine(referrer, url, request.ReferrerPolicy);
        if (computed is not null && !Contains(headers, "referer"))
        {
            headers.Add(new HeaderEntry("referer", computed));
        }

        var origin = FetchReferrer.DetermineOrigin(policy.Origin, url, method, request.ReferrerPolicy);
        if (origin is not null && !Contains(headers, "origin"))
        {
            headers.Add(new HeaderEntry("origin", origin));
        }

        // https://fetch.spec.whatwg.org/#concept-http-network-or-cache-fetch: "If httpRequest's header list
        // does not contain `User-Agent`, then user agents should append (`User-Agent`, default `User-Agent`
        // value) to httpRequest's header list." The condition is the standard's own, so unlike the three
        // headers around it this one needs no divergence to honour what the script set.
        if (policy.UserAgent is { Length: > 0 } userAgent && !Contains(headers, "user-agent"))
        {
            headers.Add(new HeaderEntry("user-agent", userAgent));
        }

        if (policy.CookieJar is { } jar && CredentialsAllow(request.Credentials, policy, url) && !Contains(headers, "cookie"))
        {
            var cookies = jar.GetCookieHeader(uri);
            if (!string.IsNullOrEmpty(cookies))
            {
                headers.Add(new HeaderEntry("cookie", cookies!));
            }
        }

        return computed;
    }

    private static bool Contains(List<HeaderEntry> headers, string lowerName)
    {
        foreach (var header in headers)
        {
            if (string.Equals(header.LowerName, lowerName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Hands every <c>Set-Cookie</c> a hop answered to the host's jar, under the same credentials rule that
    /// decides whether one is sent.
    /// </summary>
    private static void StoreCookies(FetchRequestSnapshot request, FetchPolicy policy, UrlRecord url, Uri uri, HttpResponseMessage response)
    {
        if (policy.CookieJar is not { } jar || !CredentialsAllow(request.Credentials, policy, url))
        {
            return;
        }

        if (!response.Headers.TryGetValues(HeaderList.SetCookieName, out var values))
        {
            return;
        }

        var list = new List<string>();
        foreach (var value in values)
        {
            list.Add(value);
        }

        if (list.Count != 0)
        {
            jar.StoreResponseCookies(uri, list);
        }
    }

    /// <summary>
    /// https://fetch.spec.whatwg.org/#concept-request-credentials-mode, as far as it can be answered without
    /// an origin: <c>same-origin</c> needs one, and an engine whose host named none sends no cookies for it.
    /// </summary>
    private static bool CredentialsAllow(string credentials, FetchPolicy policy, UrlRecord url)
    {
        if (string.Equals(credentials, JsRequest.CredentialsOmit, StringComparison.Ordinal))
        {
            return false;
        }

        if (string.Equals(credentials, JsRequest.CredentialsInclude, StringComparison.Ordinal))
        {
            return true;
        }

        if (policy.SameOriginReference is not { } reference)
        {
            return false;
        }

        var origin = url.SerializeOrigin();
        return !string.Equals(origin, "null", StringComparison.Ordinal)
            && string.Equals(origin, reference.SerializeOrigin(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Asks the observer about one hop. A throw is turned into a network failure rather than swallowed: this
    /// is the one callback that was asked to decide, and a decision that failed cannot mean "continue".
    /// </summary>
    private static async Task<FetchInterception?> Observe(
        FetchObservation observation,
        FetchRequestSnapshot request,
        List<HeaderEntry> headers,
        string method,
        Uri uri,
        ReadOnlyMemory<byte>? body,
        HttpContent? content,
        int redirectCount,
        ObservedFetchResponse? redirectResponse,
        CancellationToken cancellationToken)
    {
        var preview = ReadOnlyMemory<byte>.Empty;
        if (body is { } bytes && observation.RequestBodyPreviewBytes > 0)
        {
            preview = bytes.Length <= observation.RequestBodyPreviewBytes
                ? bytes.ToArray()
                : bytes.Slice(0, observation.RequestBodyPreviewBytes).ToArray();
        }

        var snapshot = new ObservedFetchRequest
        {
            Id = observation.Id,
            Initiator = observation.Initiator,
            Url = uri,
            Method = method,
            Headers = ToFetchHeaders(headers),
            HasBody = body is not null || content is not null,
            BodyPreview = preview,
            RedirectCount = redirectCount,
            RedirectResponse = redirectResponse,
        };

        try
        {
            return await observation.RequestAsync(snapshot, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not FetchFailureException)
        {
            throw new FetchFailureException(FetchFailureKind.Network, "A fetch observer failed to answer the request: " + ex.Message, ex);
        }
    }

    private static List<FetchHeader> ToFetchHeaders(List<HeaderEntry> headers)
    {
        var result = new List<FetchHeader>(headers.Count);
        foreach (var header in headers)
        {
            result.Add(new FetchHeader(header.LowerName, header.Value));
        }

        return result;
    }

    private static ObservedFetchResponse Observed(
        FetchObservation observation,
        HttpResponseMessage response,
        Uri uri,
        bool isRedirect,
        bool fromInterception,
        FetchTiming? timing)
    {
        var headers = new List<HeaderEntry>();
        Collect(headers, response.Headers);
        Collect(headers, response.Content.Headers);

        return new ObservedFetchResponse
        {
            Id = observation.Id,
            Url = uri,
            Status = (int) response.StatusCode,
            StatusText = response.ReasonPhrase ?? string.Empty,
            Headers = ToFetchHeaders(headers),
            FromInterception = fromInterception,
            IsRedirect = isRedirect,
            Timing = timing,
        };
    }

    /// <summary>
    /// Builds the response an observer answered a hop with, so that everything downstream — the body stream,
    /// the size cap, the <c>Response</c> object — is the same code a network answer goes through.
    /// </summary>
    /// <remarks>
    /// A fulfilled response ends the chain whatever its status: the redirect loop follows what the network
    /// said, and an observer that wants a redirect followed answers the hop after it too.
    /// </remarks>
    private static FetchExchange Fulfil(FetchInterception interception, string method, UrlRecord url, Uri uri, int redirectCount)
    {
        return new FetchExchange
        {
            Response = BuildResponse(interception.Status, interception.StatusText, interception.Headers, interception.Body),
            Method = method,
            Url = url,
            RequestUri = uri,
            Redirected = redirectCount > 0,
            FromInterception = true,

            // Timing stays null on purpose: nothing was sent, so there is no send to time, and a
            // zero-length one would report a socket that was never opened as an instant round trip.
        };
    }

    /// <summary>
    /// https://fetch.spec.whatwg.org/#concept-fetch step 12: a request whose header list does not contain
    /// <c>Accept</c> is given one, and <c>*/*</c> is the value for everything but the few destinations a
    /// browser knows — a document, an image, a stylesheet. A request made here has no destination, so the
    /// general value is the only one that applies.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The list this appends to is the transport's copy, which is what makes it invisible to the script: a
    /// browser sends <c>Accept: */*</c> while the <c>Request</c> the script holds still answers null for it,
    /// and so does this. Every hop of a redirect carries it for the same reason the script's own headers do.
    /// </para>
    /// <para>
    /// <b>Step 13, the <c>Accept-Language</c> beside it, is deliberately not implemented.</b> It applies only
    /// "if request's client is non-null" and its value is the user's language preferences; an embedded engine
    /// has neither a client nor a user, so there is nothing to report and a made-up value would be worse than
    /// the absence.
    /// </para>
    /// </remarks>
    private static void AppendDefaultAccept(List<HeaderEntry> headers)
    {
        foreach (var header in headers)
        {
            if (string.Equals(header.LowerName, "accept", StringComparison.Ordinal))
            {
                return;
            }
        }

        headers.Add(new HeaderEntry("accept", "*/*"));
    }

    /// <summary>
    /// The location a redirect points at, resolved against the URL that produced it, or <see langword="null"/>
    /// when this response is not a redirect to follow.
    /// </summary>
    /// <remarks>
    /// A <c>Location</c> that does not parse is a network error, which is what the standard's "if locationURL
    /// is failure, then return a network error" says.
    /// </remarks>
    private static UrlRecord? TryGetRedirectTarget(HttpResponseMessage response, UrlRecord current)
    {
        // Read the raw header rather than response.Headers.Location, whose Uri parsing is not the WHATWG
        // parser and rejects some relative forms the standard accepts.
        if (!response.Headers.TryGetValues("Location", out var values))
        {
            return null;
        }

        string? location = null;
        foreach (var value in values)
        {
            location = value;
            break;
        }

        if (location is null)
        {
            return null;
        }

        var target = UrlParser.Parse(location, current);
        if (target is null)
        {
            throw new FetchFailureException(FetchFailureKind.Network, $"'{current.Serialize()}' answered a redirect to an unparsable location.");
        }

        return target;
    }

    /// <summary>
    /// The method and header rewrites a redirect performs —
    /// https://fetch.spec.whatwg.org/#http-redirect-fetch steps 11 to 13.
    /// </summary>
    private static void Rewrite(
        int status,
        ref string method,
        ref ReadOnlyMemory<byte>? body,
        ref HttpContent? content,
        List<HeaderEntry> headers,
        UrlRecord from,
        UrlRecord to)
    {
        var dropsBody = (status is 301 or 302 && string.Equals(method, "POST", StringComparison.Ordinal))
            || (status == 303 && method is not ("GET" or "HEAD"));

        if (dropsBody)
        {
            method = "GET";
            body = null;

            // The hop's request message has already been disposed, and with it this content — the check
            // above is what guarantees only a 303 ever reaches here with one.
            content = null;
            Remove(headers, _bodyHeaderNames);
        }

        if (!IsSameOrigin(from, to))
        {
            Remove(headers, _crossOriginHeaderNames);
        }
    }

    private static void Remove(List<HeaderEntry> headers, string[] names)
    {
        headers.RemoveAll(entry =>
        {
            foreach (var name in names)
            {
                if (string.Equals(entry.LowerName, name, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        });
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/browsers.html#same-origin — scheme, host and port, with the
    /// default port already normalized away by the URL parser.
    /// </summary>
    private static bool IsSameOrigin(UrlRecord a, UrlRecord b)
        => string.Equals(a.Scheme, b.Scheme, StringComparison.Ordinal)
        && string.Equals(a.SerializeHost(), b.SerializeHost(), StringComparison.Ordinal)
        && a.Port == b.Port;

    private static HttpRequestMessage BuildRequest(
        string method,
        Uri uri,
        List<HeaderEntry> headers,
        ReadOnlyMemory<byte>? body,
        HttpContent? content)
    {
        var message = new HttpRequestMessage(new HttpMethod(method), uri);

        if (content is not null)
        {
            // A streaming body: the content pulls from the engine as the socket drains it, and computes no
            // length, so this goes out chunked.
            message.Content = content;
        }
        else if (body is { } bytes)
        {
            message.Content = new ReadOnlyMemoryContent(bytes);

            // ReadOnlyMemoryContent guesses a Content-Type of its own; the header list is the only authority
            // on what the request carries, and it already holds whatever the body implied.
            message.Content.Headers.ContentType = null;
        }

        var bodiless = message.Content is null;

        foreach (var header in headers)
        {
            // A content header belongs on the content, and the request-header collection refuses it. Both
            // calls are TryAddWithoutValidation: the header list has already been validated against the
            // Fetch Standard's own grammar, which is what a script is entitled to be measured against.
            if (message.Headers.TryAddWithoutValidation(header.LowerName, header.Value))
            {
                continue;
            }

            // Content-Length is the one content header a carrier does not take: there is no body, and a
            // length is what the transport frames one with, so a script that set one would have this request
            // announce bytes it is never going to send.
            if (bodiless && string.Equals(header.LowerName, "content-length", StringComparison.Ordinal))
            {
                continue;
            }

            message.Content ??= CreateHeaderCarrier(method);
            message.Content.Headers.TryAddWithoutValidation(header.LowerName, header.Value);
        }

        return message;
    }

    /// <summary>
    /// The empty <see cref="HttpContent"/> a request with no body is given so that a content header the
    /// script set can leave at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// https://fetch.spec.whatwg.org/#concept-header-list is <i>one</i> list, and the only names it treats
    /// specially on a request are the forbidden request-headers, which these are not. The BCL splits that one
    /// list in two: <c>Content-Encoding</c>, <c>Content-Language</c>, <c>Content-Location</c>,
    /// <c>Content-Type</c> and the rest belong to <see cref="HttpContent.Headers"/> and are refused by
    /// <see cref="HttpRequestMessage.Headers"/> — so a <c>GET</c> or a <c>HEAD</c> carrying one had nowhere to
    /// put it and dropped it on the floor.
    /// </para>
    /// <para>
    /// <b>The framing it costs.</b> A message with content is framed, and the BCL offers exactly two shapes:
    /// a known length writes <c>Content-Length</c>, an unknown one writes <c>Transfer-Encoding: chunked</c>
    /// and an empty chunked body. For <c>POST</c> and <c>PUT</c> the first is what the standard appends
    /// itself — https://fetch.spec.whatwg.org/#concept-http-network-or-cache-fetch step 8, "If httpRequest's
    /// body is null and httpRequest's method is `POST` or `PUT`, then set contentLengthHeaderValue to `0`" —
    /// and what a bodiless request of those methods already sends, so the carrier keeps its length;
    /// <c>PATCH</c> joins them because a body is expected of it too and <see cref="HttpClient"/> already sends
    /// <c>Content-Length: 0</c> for a bodiless one. For every other method the standard appends no
    /// <c>Content-Length</c> at all, so the
    /// length is suppressed and the chunked framing is taken instead: a transfer encoding is an HTTP/1.1
    /// artefact that no header list contains and that HTTP/2 does not have, where an invented
    /// <c>Content-Length: 0</c> would be a header the server reads out of the very list the standard decides.
    /// </para>
    /// </remarks>
    private static ByteArrayContent CreateHeaderCarrier(string method)
    {
        var carrier = new ByteArrayContent([]);
        if (!MustHaveRequestBody(method))
        {
            carrier.Headers.ContentLength = null;
        }

        return carrier;
    }

    /// <summary>
    /// The methods a request body is expected of, and so the ones a bodiless request already announces a
    /// length for: the two the standard names, plus the one <see cref="HttpClient"/> adds.
    /// </summary>
    /// <remarks>
    /// Case-insensitively, unlike the ordinal comparisons elsewhere here, because
    /// https://fetch.spec.whatwg.org/#concept-method-normalize uppercases six methods and leaves
    /// <c>patch</c> alone — and which of the two spellings a script wrote is no reason to frame its request
    /// differently.
    /// </remarks>
    private static bool MustHaveRequestBody(string method)
        => method.Equals("POST", StringComparison.OrdinalIgnoreCase)
        || method.Equals("PUT", StringComparison.OrdinalIgnoreCase)
        || method.Equals("PATCH", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Collects the headers and hands the still-open body over to a <see cref="FetchBodyStream"/>, which the
    /// engine thread turns into the response's <c>ReadableStream</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The only body check made here is the declared length: a <c>Content-Length</c> that already exceeds
    /// <c>MaxResponseBytes</c> is refused before the <c>fetch</c> promise settles, which is worth keeping
    /// because it costs nothing and it is the one case the cap can still report as a failed fetch. Everything
    /// else is the running total inside <see cref="FetchBodyStream"/>, which is what catches a server that
    /// lies about the length or answers with chunked encoding.
    /// </para>
    /// <para>
    /// The bytes counted either way are the <b>decompressed</b> ones, because the handler decompresses before
    /// anything here sees the stream — so the cap bounds what the process actually spends rather than what
    /// the server sent, and a compression bomb is refused at the number the host chose.
    /// </para>
    /// </remarks>
    private static async Task<FetchResponseSnapshot> BeginResponseAsync(
        FetchExchange exchange,
        FetchPolicy policy,
        FetchObservation? observation,
        CancellationToken cancellationToken)
    {
        var response = exchange.Response;
        var method = exchange.Method;
        var url = exchange.Url;

        // Read before the headers are collected, and not only for the cap below: a response a host's own
        // handler built in memory computes its length on first access and only then carries the header, so
        // this read is also what puts Content-Length in the list.
        var declaredLength = response.Content.Headers.ContentLength;

        var headers = new List<HeaderEntry>();
        Collect(headers, response.Headers);
        Collect(headers, response.Content.Headers);

        var status = (int) response.StatusCode;

        // The final response reaches the observer here rather than in the loop, so that what it is told is
        // the header list the Response object will carry — the two collections already merged into one.
        observation?.Response(new ObservedFetchResponse
        {
            Id = observation.Id,
            Url = exchange.RequestUri,
            Status = status,
            StatusText = response.ReasonPhrase ?? string.Empty,
            Headers = ToFetchHeaders(headers),
            FromInterception = exchange.FromInterception,
            IsRedirect = false,
            Timing = exchange.Timing,
        });

        // https://fetch.spec.whatwg.org/#concept-main-fetch step 22: "If response is not a network error and
        // either request's method is `HEAD` or `CONNECT`, or internalResponse's status is a null body status,
        // set internalResponse's body to null and disregard any enqueuing toward it (if any)." The method half
        // is the one a server cannot be trusted on: a HEAD response carries the headers the GET would have
        // had — Content-Length among them — and none of the bytes they describe. CONNECT never reaches here,
        // being a forbidden method (see FetchValues.IsForbiddenMethod).
        var hasBody = !FetchValues.IsNullBodyStatus(status) && !string.Equals(method, "HEAD", StringComparison.Ordinal);

        // Only a body that will be read is measured against the cap. A HEAD answers the length of the
        // representation it is describing rather than of anything it is about to send, so refusing it as too
        // large would fail a request that transfers nothing — which is the whole point of asking with HEAD.
        if (hasBody && declaredLength is { } length && length > policy.MaxResponseBytes)
        {
            throw TooLarge(policy);
        }

        FetchBodyStream? body = null;
        if (hasBody)
        {
            Stream content;
            try
            {
                content = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException and not FetchFailureException)
            {
                throw new FetchFailureException(FetchFailureKind.Network, $"Reading the response body of '{url.Serialize()}' failed: {ex.Message}", ex);
            }

            // From here the connection belongs to the body stream: the caller sees a non-null body and stops
            // disposing the message underneath it.
            body = new FetchBodyStream(response, content, policy.MaxResponseBytes, observation);
        }
        else
        {
            // Nothing will ever be read, so the request is over here: a 204 and a HEAD complete with a body
            // length of zero rather than waiting for bytes that are not coming.
            observation?.Completed(0);
        }

        return new FetchResponseSnapshot
        {
            Status = status,
            StatusText = response.ReasonPhrase ?? string.Empty,
            Headers = headers,
            Body = body,
            Url = url.Serialize(excludeFragment: true),
            Redirected = exchange.Redirected,
        };
    }

    private static FetchFailureException TooLarge(FetchPolicy policy)
        => new(FetchFailureKind.ResponseTooLarge, $"The response body exceeded the {policy.MaxResponseBytes} byte limit set by Options.WebApi.Fetch.MaxResponseBytes.");

    /// <summary>
    /// Every value of every header becomes its own entry, so that a response carrying several
    /// <c>Set-Cookie</c> headers is not silently folded into one.
    /// </summary>
    private static void Collect(List<HeaderEntry> headers, System.Net.Http.Headers.HttpHeaders source)
    {
        foreach (var header in source.NonValidated)
        {
            foreach (var value in header.Value)
            {
                headers.Add(new HeaderEntry(HeaderList.Lowercase(header.Key), value));
            }
        }
    }
}
#endif
