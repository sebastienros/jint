using Jint.WebApi.Fetch;

namespace Jint.Browser.Runtime;

/// <summary>What one request is for, in the vocabulary a client asks about it in.</summary>
/// <remarks>
/// <para>
/// It is finer than <see cref="RequestInitiator"/> on purpose. The initiator says <i>who</i> asked — a
/// navigation, a script, the document's own markup — and a client asks <i>what for</i>: a `route` handler
/// filtering on `Script`, a request log grouping style sheets. The two are recorded separately because
/// neither can be derived from the other: a module script and a <c>fetch()</c> are both script-initiated.
/// </para>
/// <para>
/// Every member maps onto one of the Chrome DevTools Protocol's <c>Network.ResourceType</c> values, which is
/// the only reason the distinctions are drawn where they are.
/// </para>
/// </remarks>
internal enum PageRequestKind
{
    /// <summary>A navigation's own document.</summary>
    Document,

    /// <summary>A classic or module script the document referenced.</summary>
    Script,

    /// <summary>A style sheet the document referenced.</summary>
    Stylesheet,

    /// <summary>An <c>XMLHttpRequest</c> a script made.</summary>
    Xhr,

    /// <summary>A <c>fetch()</c> a script made.</summary>
    Fetch,

    /// <summary>An <c>EventSource</c> stream a script opened.</summary>
    /// <remarks>
    /// A page's own scripts cannot open one — the feature is not among the ones a page engine is built with
    /// — so this names the streams of a host that granted it through <c>BrowserOptions.ConfigureEngine</c>.
    /// The engine reports them like any other request, so the log would otherwise call a stream a
    /// <c>fetch</c>.
    /// </remarks>
    EventSource,

    /// <summary>An image the document referenced, which this browser records and does not fetch.</summary>
    Image,

    /// <summary>A nested browsing context's document, which this browser records and does not fetch.</summary>
    Frame,

    /// <summary>Anything else.</summary>
    Other,
}

/// <summary>One hop of one request, as everything above the transport sees it.</summary>
/// <remarks>
/// <b>Plain CLR data, built on a transport thread.</b> Nothing here is a <c>JsValue</c>, an
/// <see cref="Engine"/> or an AngleSharp node, which is what lets it be handed to a listener while the script
/// that started the request goes on running.
/// </remarks>
/// <param name="RequestId">The identifier a client addresses this request by, stable across its redirects.</param>
/// <param name="Kind">What the request is for.</param>
/// <param name="Initiator">Who asked for it.</param>
/// <param name="LoaderId">The document the request belongs to.</param>
/// <param name="DocumentUrl">The URL of that document.</param>
/// <param name="Url">The absolute URL of this hop.</param>
/// <param name="Method">The method of this hop.</param>
/// <param name="Headers">The headers this hop will carry, engine-appended ones included.</param>
/// <param name="PostData">The first bytes of the request body, bounded by the capture limit.</param>
/// <param name="HasPostData">Whether the request carries a body at all.</param>
/// <param name="RedirectCount">How many redirects the request has already followed.</param>
/// <param name="RedirectResponse">The redirect that produced this hop, or <see langword="null"/>.</param>
internal sealed record PageNetworkRequest(
    string RequestId,
    PageRequestKind Kind,
    RequestInitiator Initiator,
    string LoaderId,
    string DocumentUrl,
    string Url,
    string Method,
    IReadOnlyList<PageHeader> Headers,
    string? PostData,
    bool HasPostData,
    int RedirectCount,
    PageNetworkResponse? RedirectResponse);

/// <summary>One response, as everything above the transport sees it.</summary>
/// <param name="RequestId">The request this answers.</param>
/// <param name="Url">The URL that produced it.</param>
/// <param name="Status">The status code.</param>
/// <param name="StatusText">The reason phrase, which may be empty.</param>
/// <param name="Headers">The response headers, one entry per value.</param>
/// <param name="MimeType">The essence of the <c>Content-Type</c>, or the empty string.</param>
/// <param name="Charset">The <c>charset</c> parameter of the <c>Content-Type</c>, or the empty string.</param>
/// <param name="FromInterception">Whether a client answered this request instead of the network.</param>
/// <param name="Timing">
/// When the hop that produced this response went out and when its headers came back, or
/// <see langword="null"/> when nothing went on the wire because a client fulfilled the request.
/// </param>
/// <remarks>
/// <see cref="Timing"/> is the engine's own <see cref="FetchTiming"/> rather than a mirror of it, unlike
/// <see cref="PageHeader"/> beside it: the mirrors exist because the shapes they mirror carry the
/// <c>JINT0002</c> preview diagnostic or a vocabulary this package does not share, and this one is neither —
/// it is two readings of a clock with exactly the meaning the <c>Network</c> domain needs.
/// </remarks>
internal sealed record PageNetworkResponse(
    string RequestId,
    string Url,
    int Status,
    string StatusText,
    IReadOnlyList<PageHeader> Headers,
    string MimeType,
    string Charset,
    bool FromInterception,
    FetchTiming? Timing);

/// <summary>What a listener decided about one hop.</summary>
/// <remarks>
/// It is deliberately not <c>Jint.WebApi.Fetch.FetchInterception</c>: that type is the engine's preview
/// surface and carries the <c>JINT0002</c> diagnostic, and the recorder is the one place that translates
/// between the two.
/// </remarks>
internal sealed class PageNetworkDecision
{
    private PageNetworkDecision()
    {
    }

    /// <summary>Send the hop as it stands.</summary>
    internal static PageNetworkDecision Proceed { get; } = new() { Kind = PageNetworkDecisionKind.Proceed };

    internal PageNetworkDecisionKind Kind { get; private init; }

    /// <summary>The complete header list to send instead of the hop's own, or <see langword="null"/>.</summary>
    internal IReadOnlyList<PageHeader>? Headers { get; private init; }

    /// <summary>An absolute URL to send to instead, or <see langword="null"/>.</summary>
    internal string? Url { get; private init; }

    /// <summary>A method to use instead, or <see langword="null"/>.</summary>
    internal string? Method { get; private init; }

    /// <summary>A request body to send instead, or <see langword="null"/>.</summary>
    internal byte[]? Body { get; private init; }

    /// <summary>The status a fulfilled request answers with.</summary>
    internal int Status { get; private init; }

    /// <summary>The reason phrase a fulfilled request answers with, or <see langword="null"/>.</summary>
    internal string? StatusText { get; private init; }

    /// <summary>The <c>net::ERR_*</c> string a failed request is reported with.</summary>
    internal string? ErrorText { get; private init; }

    /// <summary>Why a client blocked the request, in the protocol's own vocabulary, or <see langword="null"/>.</summary>
    internal string? BlockedReason { get; private init; }

    /// <summary>Sends the hop with some of it rewritten; anything left <see langword="null"/> is kept.</summary>
    internal static PageNetworkDecision Continue(
        string? url = null,
        string? method = null,
        IReadOnlyList<PageHeader>? headers = null,
        byte[]? body = null)
        => new()
        {
            Kind = PageNetworkDecisionKind.Continue,
            Url = url,
            Method = method,
            Headers = headers,
            Body = body,
        };

    /// <summary>Answers the request without opening a socket.</summary>
    internal static PageNetworkDecision Fulfill(int status, IReadOnlyList<PageHeader>? headers, byte[]? body, string? statusText)
        => new()
        {
            Kind = PageNetworkDecisionKind.Fulfill,
            Status = status,
            Headers = headers,
            Body = body,
            StatusText = statusText,
        };

    /// <summary>Fails the request, and says what a client should be told it failed with.</summary>
    internal static PageNetworkDecision Fail(string errorText, string? blockedReason = null)
        => new() { Kind = PageNetworkDecisionKind.Fail, ErrorText = errorText, BlockedReason = blockedReason };
}

/// <summary>Which of the four things a listener decided.</summary>
internal enum PageNetworkDecisionKind
{
    /// <summary>Send it unchanged.</summary>
    Proceed,

    /// <summary>Send it with the decision's rewrites applied.</summary>
    Continue,

    /// <summary>Answer it from the decision.</summary>
    Fulfill,

    /// <summary>Fail it.</summary>
    Fail,
}

/// <summary>
/// What one watcher of a page's network is told, in the order the transport does it, and the one call that
/// may answer back.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every member runs on a transport thread and must never touch the engine or the DOM.</b> That is the
/// <c>FetchObserver</c> contract this seam sits directly on top of, and it is deliberate rather than
/// incidental: <see cref="RequestWillBeSentAsync"/> is where a protocol client's interception blocks, and it
/// blocks the one fetch it belongs to. Delivering it on the page loop instead would deadlock the one case a
/// page cannot pump through — the fetch of a <c>&lt;script src&gt;</c> a running script inserted, which
/// blocks the loop rather than pumping it, because pumping from inside a running script would run the page's
/// jobs in the middle of one.
/// </para>
/// <para>
/// <b>The order is the observer's</b>: <see cref="RequestWillBeSentAsync"/> per hop, then
/// <see cref="ResponseReceived"/> for the final response, then <see cref="DataReceived"/> per body chunk,
/// then exactly one of <see cref="LoadingFinished"/> and <see cref="LoadingFailed"/>. A redirect is reported
/// as the next hop's <see cref="PageNetworkRequest.RedirectResponse"/> and never as a response of its own,
/// which is what Chrome does too.
/// </para>
/// <para>
/// <b>A listener that throws is ignored</b>, for the reason the observer's own notifications are: there is no
/// engine thread to report it to and a transfer must not depend on a watcher.
/// </para>
/// </remarks>
internal interface IPageNetworkListener
{
    /// <summary>One hop is about to be sent; answer what should happen to it.</summary>
    /// <param name="request">The hop.</param>
    /// <param name="cancellationToken">Cancelled when the fetch is abandoned or times out.</param>
    /// <remarks>
    /// The whole fetch is still bounded by the page's own timeouts, so a client that pauses a request and
    /// never answers costs that request its deadline and nothing else.
    /// </remarks>
    ValueTask<PageNetworkDecision> RequestWillBeSentAsync(PageNetworkRequest request, CancellationToken cancellationToken);

    /// <summary>The final response's headers are in and its body has not been read.</summary>
    /// <param name="request">The request, as its last hop went out.</param>
    /// <param name="response">The response.</param>
    void ResponseReceived(PageNetworkRequest request, PageNetworkResponse response);

    /// <summary>Some of the body arrived.</summary>
    /// <param name="requestId">The request.</param>
    /// <param name="length">How many bytes this chunk was.</param>
    void DataReceived(string requestId, int length);

    /// <summary>The body has been read to its end.</summary>
    /// <param name="requestId">The request.</param>
    /// <param name="encodedLength">How many bytes the body turned out to be.</param>
    void LoadingFinished(string requestId, long encodedLength);

    /// <summary>The request failed instead of finishing.</summary>
    /// <param name="requestId">The request.</param>
    /// <param name="kind">What the request was for, because a failure carries its resource type.</param>
    /// <param name="errorText">The <c>net::ERR_*</c> string a client parses.</param>
    /// <param name="canceled">Whether it was abandoned rather than refused.</param>
    /// <param name="blockedReason">Why a client blocked it, or <see langword="null"/>.</param>
    void LoadingFailed(string requestId, PageRequestKind kind, string errorText, bool canceled, string? blockedReason);

    /// <summary>A reference the page saw and deliberately did not follow.</summary>
    /// <param name="request">The reference, as a request that was never sent.</param>
    /// <param name="reason">Why the page did not follow it.</param>
    /// <remarks>
    /// <b>A divergence from Chrome, and a deliberate one.</b> Chrome fetches an image; this browser has
    /// nothing to render one with, so it records the reference and opens no socket. Reporting it as a request
    /// that was blocked is what lets a client see everything the document asked for rather than the subset
    /// something chose to fetch — which is exactly what <see cref="Page.Requests"/> already promises.
    /// </remarks>
    void NotFetched(PageNetworkRequest request, string reason);
}
