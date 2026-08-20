#if NET8_0_OR_GREATER
using System.Buffers;
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
/// engine-free layers, and the body is a byte window. That is what lets the whole of
/// <see cref="FetchTransport"/> run on a thread pool thread while the engine goes on running script.
/// </remarks>
internal sealed class FetchRequestSnapshot
{
    internal required string Method { get; init; }

    internal required UrlRecord Url { get; init; }

    internal required List<HeaderEntry> Headers { get; init; }

    internal required ReadOnlyMemory<byte>? Body { get; init; }

    /// <summary>One of <c>follow</c>, <c>error</c> or <c>manual</c>.</summary>
    internal required string Redirect { get; init; }
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

    /// <summary>The URL that produced this response, i.e. the last hop of the redirect chain.</summary>
    internal required UrlRecord Url { get; init; }

    internal required bool Redirected { get; init; }

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

    internal required byte[] Body { get; init; }

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
    /// through <c>engine.Advanced.HostDefined</c>. Answers <see langword="null"/> only when a host factory
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
    /// Runs the request, following redirects itself, and reads the body under the size cap.
    /// </summary>
    internal static async Task<FetchResponseSnapshot> SendAsync(
        HttpClient client,
        FetchRequestSnapshot request,
        FetchPolicy policy,
        CancellationToken cancellationToken)
    {
        var exchange = await SendForStreamAsync(client, request, policy, cancellationToken).ConfigureAwait(false);
        try
        {
            return await ReadResponseAsync(exchange.Response, exchange.Url, exchange.Redirected, policy, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            exchange.Dispose();
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
        CancellationToken cancellationToken)
    {
        var url = request.Url;
        var method = request.Method;
        var body = request.Body;
        var headers = new List<HeaderEntry>(request.Headers);
        var redirectCount = 0;

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

            HttpResponseMessage response;
            try
            {
                using var message = BuildRequest(method, uri, headers, body);
                response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException and not FetchFailureException)
            {
                throw new FetchFailureException(FetchFailureKind.Network, $"The request to '{uri}' failed: {ex.Message}", ex);
            }

            // The response is disposed here only while the loop goes on to another hop; the one it ends with
            // belongs to the caller, whose body may not have been read yet.
            var handedOver = false;
            try
            {
                // https://fetch.spec.whatwg.org/#concept-http-fetch step 6, the three redirect modes. "manual"
                // is Node's reading of it: the redirect response itself is handed to the script, Location and
                // all, rather than a browser's opaque-redirect filtered response — which exists to hide a
                // cross-origin redirect from a page, a concern an embedded engine with no origin does not have.
                var status = (int) response.StatusCode;
                if (!FetchValues.IsRedirectStatus(status)
                    || string.Equals(request.Redirect, JsRequest.RedirectManual, StringComparison.Ordinal))
                {
                    handedOver = true;
                    return new FetchExchange { Response = response, Url = url, Redirected = redirectCount > 0 };
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
                    return new FetchExchange { Response = response, Url = url, Redirected = redirectCount > 0 };
                }

                if (++redirectCount > policy.MaxRedirects)
                {
                    throw new FetchFailureException(
                        FetchFailureKind.RedirectLimit,
                        $"The request to '{request.Url.Serialize()}' exceeded the limit of {policy.MaxRedirects} redirects.");
                }

                Rewrite(status, ref method, ref body, headers, url, location);
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
    private static void Rewrite(int status, ref string method, ref ReadOnlyMemory<byte>? body, List<HeaderEntry> headers, UrlRecord from, UrlRecord to)
    {
        var dropsBody = (status is 301 or 302 && string.Equals(method, "POST", StringComparison.Ordinal))
            || (status == 303 && method is not ("GET" or "HEAD"));

        if (dropsBody)
        {
            method = "GET";
            body = null;
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

    private static HttpRequestMessage BuildRequest(string method, Uri uri, List<HeaderEntry> headers, ReadOnlyMemory<byte>? body)
    {
        var message = new HttpRequestMessage(new HttpMethod(method), uri);

        if (body is { } bytes)
        {
            message.Content = new ReadOnlyMemoryContent(bytes);

            // ReadOnlyMemoryContent guesses a Content-Type of its own; the header list is the only authority
            // on what the request carries, and it already holds whatever the body implied.
            message.Content.Headers.ContentType = null;
        }

        foreach (var header in headers)
        {
            // A content header belongs on the content, and the request-header collection refuses it. Both
            // calls are TryAddWithoutValidation: the header list has already been validated against the
            // Fetch Standard's own grammar, which is what a script is entitled to be measured against.
            if (!message.Headers.TryAddWithoutValidation(header.LowerName, header.Value))
            {
                message.Content?.Headers.TryAddWithoutValidation(header.LowerName, header.Value);
            }
        }

        return message;
    }

    /// <summary>
    /// Reads the body under <c>MaxResponseBytes</c> and collects the headers.
    /// </summary>
    /// <remarks>
    /// The bytes counted are the <b>decompressed</b> ones, because the handler decompresses before this sees
    /// the stream — so the cap bounds what the process actually spends rather than what the server sent, and
    /// a compression bomb is refused at the number the host chose. A <c>Content-Length</c> that already
    /// exceeds the cap short-circuits the read, but it is only a shortcut: a server that lies, or that uses
    /// chunked encoding, is caught by the running total.
    /// </remarks>
    private static async Task<FetchResponseSnapshot> ReadResponseAsync(
        HttpResponseMessage response,
        UrlRecord url,
        bool redirected,
        FetchPolicy policy,
        CancellationToken cancellationToken)
    {
        var declaredLength = response.Content.Headers.ContentLength;
        if (declaredLength is { } length && length > policy.MaxResponseBytes)
        {
            throw TooLarge(policy);
        }

        byte[] body;
        try
        {
            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            body = await ReadBoundedAsync(stream, policy, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not FetchFailureException)
        {
            throw new FetchFailureException(FetchFailureKind.Network, $"Reading the response body of '{url.Serialize()}' failed: {ex.Message}", ex);
        }

        var headers = new List<HeaderEntry>();
        Collect(headers, response.Headers);
        Collect(headers, response.Content.Headers);

        return new FetchResponseSnapshot
        {
            Status = (int) response.StatusCode,
            StatusText = response.ReasonPhrase ?? string.Empty,
            Headers = headers,
            Body = body,
            Url = url.Serialize(excludeFragment: true),
            Redirected = redirected,
        };
    }

    private static async Task<byte[]> ReadBoundedAsync(Stream stream, FetchPolicy policy, CancellationToken cancellationToken)
    {
        var writer = new ArrayBufferWriter<byte>();
        var buffer = ArrayPool<byte>.Shared.Rent(16 * 1024);
        try
        {
            while (true)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    return writer.WrittenSpan.ToArray();
                }

                // Checked before the copy, so the cap is never overshot by a whole chunk and the connection
                // is dropped as soon as the limit is known to be broken.
                if (writer.WrittenCount + (long) read > policy.MaxResponseBytes)
                {
                    throw TooLarge(policy);
                }

                writer.Write(buffer.AsSpan(0, read));
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
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
