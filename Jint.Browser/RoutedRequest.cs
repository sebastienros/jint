using System.Globalization;
using System.Text;
using Jint.Browser.Runtime;

namespace Jint.Browser;

/// <summary>What one request is for, as a route handler filters on it.</summary>
/// <remarks>
/// The vocabulary is Playwright's <c>request.resourceType()</c> and the Chrome DevTools Protocol's
/// <c>Network.ResourceType</c>, which name the same distinctions; a child frame's document is told apart from
/// the page's own, because a handler stubbing an embed rarely means to stub the page that embeds it.
/// </remarks>
public enum RequestResourceType
{
    /// <summary>A navigation's own document.</summary>
    Document,

    /// <summary>A child frame's document.</summary>
    Frame,

    /// <summary>A classic or module script.</summary>
    Script,

    /// <summary>A style sheet, an imported one included.</summary>
    Stylesheet,

    /// <summary>An image an <c>&lt;img&gt;</c> or an <c>&lt;input type=image&gt;</c> named.</summary>
    Image,

    /// <summary>A font a <c>FontFace</c> loaded.</summary>
    Font,

    /// <summary>A <c>fetch()</c> a script made.</summary>
    Fetch,

    /// <summary>An <c>XMLHttpRequest</c> a script made.</summary>
    Xhr,

    /// <summary>An <c>EventSource</c> stream a script opened.</summary>
    EventSource,

    /// <summary>Anything else.</summary>
    Other,
}

/// <summary>What a <see cref="RoutedRequest.ContinueAsync"/> changes about the request before it is sent.</summary>
/// <remarks>
/// Anything left <see langword="null"/> is sent as the page composed it. A rewritten URL is checked against the
/// context's <see cref="BrowserContextOptions.UrlFilter"/> exactly as a redirect target is, so a route decides what
/// a request says and never where the context lets it go.
/// </remarks>
public sealed class RouteContinueOverrides
{
    /// <summary>The absolute URL to send the request to instead, or <see langword="null"/>.</summary>
    public string? Url { get; init; }

    /// <summary>The method to send instead, or <see langword="null"/>.</summary>
    public string? Method { get; init; }

    /// <summary>The complete header list to send instead of the request's own, or <see langword="null"/>.</summary>
    public IEnumerable<KeyValuePair<string, string>>? Headers { get; init; }

    /// <summary>The request body to send instead, or <see langword="null"/> to keep the page's.</summary>
    public ReadOnlyMemory<byte>? PostData { get; init; }
}

/// <summary>
/// One request a route matched, and the three ways a handler can answer it: fulfill, abort or continue.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is plain data, and its handler runs off the page's loop.</b> Nothing here is a <c>JsValue</c>, an
/// engine or a DOM node, and the handler that receives it runs on a thread-pool thread while the page's loop
/// goes on running, so a handler may block. One that waits on its own page (an
/// <see cref="Page.EvaluateAsync(string, object?)"/>, say) can still deadlock a fetch the loop is itself
/// blocked on, such as a <c>&lt;script src&gt;</c> a running script inserted.
/// </para>
/// <para>
/// <b>A request is decided once, before the handler's task completes.</b> A second
/// <see cref="FulfillAsync(int, IEnumerable{KeyValuePair{string, string}}?, string?, string?)"/>,
/// <see cref="AbortAsync"/> or <see cref="ContinueAsync"/> throws <see cref="InvalidOperationException"/>, and
/// so does one made after the task completed. A handler whose task completes without deciding falls back to the
/// next matching route — the page's older routes, then the context's — and a request no route decided is sent
/// unchanged.
/// </para>
/// <para>
/// <b>The body is a preview.</b> <see cref="PostDataBuffer"/> holds at most the first 64 KiB of the request
/// body, which is what the page's request log copies, and nothing for a streamed body;
/// <see cref="HasPostData"/> says whether there was one.
/// </para>
/// </remarks>
public sealed class RoutedRequest
{
    /// <summary>What <see cref="_decision"/> holds once the router has stopped listening to this handler.</summary>
    private static readonly PageNetworkDecision s_sealed = PageNetworkDecision.Fail("net::ERR_FAILED");

    private readonly ReadOnlyMemory<byte> _postData;
    private PageNetworkDecision? _decision;

    internal RoutedRequest(
        Uri url,
        string method,
        IReadOnlyList<PageHeader> headers,
        ReadOnlyMemory<byte> postData,
        bool hasPostData,
        RequestResourceType resourceType,
        RequestInitiator initiator,
        int redirectCount)
    {
        Url = url;
        Method = method;

        // A copy, so a handler cannot reach the array the request log and a protocol client also hold.
        Headers = Array.AsReadOnly([.. headers]);
        _postData = postData;
        HasPostData = hasPostData;
        ResourceType = resourceType;
        Initiator = initiator;
        RedirectCount = redirectCount;
    }

    /// <summary>The absolute URL of this hop.</summary>
    public Uri Url { get; }

    /// <summary>The method of this hop.</summary>
    public string Method { get; }

    /// <summary>The headers this hop will carry, engine-appended ones included, one entry per value.</summary>
    public IReadOnlyList<PageHeader> Headers { get; }

    /// <summary>Whether the request carries a body at all.</summary>
    public bool HasPostData { get; }

    /// <summary>The first bytes of the request body, at most 64 KiB; empty when there is none.</summary>
    public ReadOnlyMemory<byte> PostDataBuffer => _postData;

    /// <summary>
    /// The request body preview decoded as UTF-8, or <see langword="null"/> when there is none or it was not
    /// captured (a streamed body).
    /// </summary>
    public string? PostData => _postData.IsEmpty ? null : Encoding.UTF8.GetString(_postData.Span);

    /// <summary>What the request is for.</summary>
    public RequestResourceType ResourceType { get; }

    /// <summary>What asked for the request.</summary>
    public RequestInitiator Initiator { get; }

    /// <summary>Whether this request loads a document — the page's own or a child frame's.</summary>
    public bool IsNavigationRequest => ResourceType is RequestResourceType.Document or RequestResourceType.Frame;

    /// <summary>How many redirects the request had followed before this hop; each hop is routed afresh.</summary>
    public int RedirectCount { get; }

    /// <summary>Whether a handler has already fulfilled, aborted or continued this request.</summary>
    public bool IsHandled => Volatile.Read(ref _decision) is { } decision && !ReferenceEquals(decision, s_sealed);

    /// <summary>
    /// Stops accepting a decision, which the router does when the handler's task completes: a decision
    /// arriving later would be one nobody reads, so it throws instead of being silently dropped.
    /// </summary>
    /// <returns>The decision the handler took, or <see langword="null"/> when it took none.</returns>
    internal PageNetworkDecision? Seal()
    {
        var decision = Interlocked.CompareExchange(ref _decision, s_sealed, null);
        return ReferenceEquals(decision, s_sealed) ? null : decision;
    }

    /// <summary>The value of the first header named <paramref name="name"/>, or <see langword="null"/>.</summary>
    /// <param name="name">The header name, compared case-insensitively.</param>
    public string? HeaderValue(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        foreach (var header in Headers)
        {
            if (string.Equals(header.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return header.Value;
            }
        }

        return null;
    }

    /// <summary>Answers the request with a response of the handler's own; no socket is opened.</summary>
    /// <param name="status">The status code.</param>
    /// <param name="headers">The response headers, or <see langword="null"/> for none.</param>
    /// <param name="body">The body, encoded as UTF-8, or <see langword="null"/> for an empty one.</param>
    /// <param name="contentType">A <c>Content-Type</c> that replaces any in <paramref name="headers"/>.</param>
    /// <returns>A completed task; the decision is taken when the call returns.</returns>
    /// <exception cref="InvalidOperationException">The request has already been decided.</exception>
    public Task FulfillAsync(
        int status = 200,
        IEnumerable<KeyValuePair<string, string>>? headers = null,
        string? body = null,
        string? contentType = null)
    {
        Fulfil(status, headers, body is null ? [] : Encoding.UTF8.GetBytes(body), contentType);
        return Task.CompletedTask;
    }

    /// <summary>Answers the request with a binary response of the handler's own; no socket is opened.</summary>
    /// <param name="status">The status code.</param>
    /// <param name="headers">The response headers, or <see langword="null"/> for none.</param>
    /// <param name="body">The body bytes, copied when the call is made.</param>
    /// <param name="contentType">A <c>Content-Type</c> that replaces any in <paramref name="headers"/>.</param>
    /// <returns>A completed task; the decision is taken when the call returns.</returns>
    /// <exception cref="InvalidOperationException">The request has already been decided.</exception>
    public Task FulfillAsync(
        int status,
        IEnumerable<KeyValuePair<string, string>>? headers,
        ReadOnlyMemory<byte> body,
        string? contentType = null)
    {
        Fulfil(status, headers, body.ToArray(), contentType);
        return Task.CompletedTask;
    }

    private void Fulfil(int status, IEnumerable<KeyValuePair<string, string>>? headers, byte[] body, string? contentType)
    {
        if (status is < 100 or > 999)
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "A status code has three digits.");
        }

        var list = new List<PageHeader>();
        if (headers is not null)
        {
            foreach (var header in headers)
            {
                if (contentType is null || !string.Equals(header.Key, "content-type", StringComparison.OrdinalIgnoreCase))
                {
                    list.Add(Header(header.Key, header.Value, nameof(headers)));
                }
            }
        }

        if (contentType is not null)
        {
            list.Add(Header("content-type", contentType, nameof(contentType)));
        }

        Decide(PageNetworkDecision.Fulfill(status, list, body, statusText: null));
    }

    /// <summary>Fails the request, as the network would have.</summary>
    /// <param name="errorCode">
    /// One of Playwright's codes — <c>failed</c> (the default), <c>aborted</c>, <c>accessdenied</c>,
    /// <c>addressunreachable</c>, <c>blockedbyclient</c>, <c>blockedbyresponse</c>, <c>connectionaborted</c>,
    /// <c>connectionclosed</c>, <c>connectionfailed</c>, <c>connectionrefused</c>, <c>connectionreset</c>,
    /// <c>internetdisconnected</c>, <c>namenotresolved</c> or <c>timedout</c>.
    /// </param>
    /// <returns>A completed task; the decision is taken when the call returns.</returns>
    /// <exception cref="ArgumentException"><paramref name="errorCode"/> is not one of the codes above.</exception>
    /// <exception cref="InvalidOperationException">The request has already been decided.</exception>
    public Task AbortAsync(string errorCode = "failed")
    {
        ArgumentNullException.ThrowIfNull(errorCode);
        if (!PageNetworkErrors.TryGet(errorCode, out var error))
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"'{errorCode}' is not a known network error code."),
                nameof(errorCode));
        }

        Decide(PageNetworkDecision.Fail(error));
        return Task.CompletedTask;
    }

    /// <summary>Sends the request on to the network, with some of it rewritten.</summary>
    /// <param name="overrides">What to change, or <see langword="null"/> to send it as it stands.</param>
    /// <returns>A completed task; the decision is taken when the call returns.</returns>
    /// <exception cref="ArgumentException">The rewritten URL is not an absolute <c>http</c> or <c>https</c> URL.</exception>
    /// <exception cref="InvalidOperationException">The request has already been decided.</exception>
    public Task ContinueAsync(RouteContinueOverrides? overrides = null)
    {
        if (overrides is null)
        {
            Decide(PageNetworkDecision.Continue());
            return Task.CompletedTask;
        }

        if (overrides.Url is { } url
            && (!Uri.TryCreate(url, UriKind.Absolute, out var parsed)
                || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)))
        {
            throw new ArgumentException("A rewritten URL must be an absolute http or https URL.", nameof(overrides));
        }

        if (overrides.Method is { } method && !IsToken(method))
        {
            throw new ArgumentException("'" + method + "' is not an HTTP method.", nameof(overrides));
        }

        List<PageHeader>? headers = null;
        if (overrides.Headers is { } replaced)
        {
            headers = [];
            foreach (var header in replaced)
            {
                headers.Add(Header(header.Key, header.Value, nameof(overrides)));
            }
        }

        Decide(PageNetworkDecision.Continue(
            overrides.Url,
            overrides.Method,
            headers,
            overrides.PostData is { } body ? body.ToArray() : null));
        return Task.CompletedTask;
    }

    private void Decide(PageNetworkDecision decision)
    {
        var previous = Interlocked.CompareExchange(ref _decision, decision, null);
        if (ReferenceEquals(previous, s_sealed))
        {
            throw new InvalidOperationException(
                "The handler's task completed before it decided this request, so the request was routed on without it.");
        }

        if (previous is not null)
        {
            throw new InvalidOperationException("This request has already been fulfilled, aborted or continued.");
        }
    }

    /// <summary>One header, refused here rather than by the transport when its name or value cannot be sent.</summary>
    private static PageHeader Header(string name, string value, string parameter)
    {
        if (!IsToken(name))
        {
            throw new ArgumentException("'" + name + "' is not an HTTP header name.", parameter);
        }

        // https://fetch.spec.whatwg.org/#header-value: no NUL, CR or LF, and no leading or trailing whitespace
        // to speak of; the transport trims the latter, so only the three that would split the message matter.
        if (value is null || value.AsSpan().IndexOfAny('\0', '\r', '\n') >= 0)
        {
            throw new ArgumentException("The value of the header '" + name + "' cannot be sent.", parameter);
        }

        return new PageHeader(name, value);
    }

    /// <summary>https://fetch.spec.whatwg.org/#header-name — RFC 9110's <c>token</c>.</summary>
    private static bool IsToken(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        foreach (var c in value)
        {
            if (c > 0x7E || c <= 0x20 || "\"(),/:;<=>?@[\\]{}".Contains(c))
            {
                return false;
            }
        }

        return true;
    }
}
