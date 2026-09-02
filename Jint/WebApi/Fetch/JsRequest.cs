#if NET8_0_OR_GREATER
using Jint.WebApi.Abort;
using Jint.WebApi.Url.Parsing;

namespace Jint.WebApi.Fetch;

/// <summary>
/// A <c>Request</c> instance.
/// <para>
/// https://fetch.spec.whatwg.org/#request-class
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// Every attribute is an accessor on <see cref="RequestPrototype"/> reading this state through a brand check,
/// so an instance has no own property — which is what a browser reports for
/// <c>Object.getOwnPropertyNames(new Request("https://example.org/"))</c>.
/// </para>
/// <para>
/// The rule for which members exist is that they must describe something this engine <i>has</i>.
/// <c>destination</c>, <c>mode</c>, <c>cache</c>, <c>integrity</c>, <c>keepalive</c>,
/// <c>isReloadNavigation</c> and <c>isHistoryNavigation</c> are deliberately absent rather than present and
/// lying: every one of them names a browser concept — a fetch destination, a same-origin policy, an HTTP
/// cache, a navigation — that this engine does not have, and an absent member is what feature detection is
/// written against.
/// </para>
/// <para>
/// <c>duplex</c> is the one that was always the other way round. It describes no browser concept — it says
/// whether the request is sent before the response is read — and it is the member a script must set for a
/// <c>ReadableStream</c> body, so it is both read from <c>RequestInit</c> and exposed as an attribute.
/// </para>
/// <para>
/// <c>referrer</c>, <c>referrerPolicy</c> and <c>credentials</c> joined it once
/// <c>Options.WebApi.Fetch.Referrer</c>, <c>ReferrerPolicy</c>, <c>Origin</c> and <c>CookieJar</c> gave the
/// engine the things they describe. On an engine whose host configured none of those they are still honest:
/// they say what the request asked for, and a request that asks for cookies from an engine with no jar gets
/// none, exactly as a browser with an empty jar would.
/// </para>
/// </remarks>
internal sealed class JsRequest : FetchBodyObject
{
    internal JsRequest(Engine engine, JsHeaders headers) : base(engine, headers)
    {
    }

    /// <summary>
    /// https://fetch.spec.whatwg.org/#concept-request-method — already normalized, so <c>get</c> is
    /// <c>GET</c> while <c>patch</c> stays as written.
    /// </summary>
    internal string Method { get; set; } = "GET";

    /// <summary>
    /// https://fetch.spec.whatwg.org/#concept-request-url. Kept as a parsed record rather than as text
    /// because a redirect hop has to be resolved against it, and because <c>fetch</c>'s scheme policy asks
    /// questions of it that re-parsing a string would only have to answer again.
    /// </summary>
    internal UrlRecord Url { get; set; } = null!;

    /// <summary>
    /// https://fetch.spec.whatwg.org/#concept-request-blob-url-entry — the <c>Blob</c> a <c>blob:</c> URL
    /// named <b>when this request was constructed</b>, or <see langword="null"/> for every other URL.
    /// </summary>
    /// <remarks>
    /// It is resolved once, here, and never again: that is what makes
    /// <c>const r = new Request(url); URL.revokeObjectURL(url); fetch(r)</c> succeed, which is the whole
    /// reason the standard gives a request a field for it rather than looking the URL up when the fetch runs.
    /// A <c>clone()</c> carries it across for the same reason.
    /// </remarks>
    internal Files.JsBlob? BlobUrlEntry { get; set; }

    /// <summary>
    /// https://fetch.spec.whatwg.org/#concept-request-redirect-mode — <c>follow</c>, <c>error</c> or
    /// <c>manual</c>.
    /// </summary>
    internal string Redirect { get; set; } = RedirectFollow;

    /// <summary>
    /// https://fetch.spec.whatwg.org/#dom-request-signal — never null: the constructor always makes one, and
    /// it follows whatever signal the initializer named.
    /// </summary>
    internal JsAbortSignal Signal { get; set; } = null!;

    /// <summary>
    /// https://fetch.spec.whatwg.org/#concept-request-referrer — one of "no-referrer", "client" or a URL, of
    /// which only the last needs <see cref="ReferrerUrl"/> beside it.
    /// </summary>
    internal FetchReferrerSource ReferrerSource { get; set; } = FetchReferrerSource.Client;

    /// <summary>The referrer URL, set only when <see cref="ReferrerSource"/> is a URL.</summary>
    internal UrlRecord? ReferrerUrl { get; set; }

    /// <summary>
    /// https://fetch.spec.whatwg.org/#concept-request-referrer-policy — <see langword="null"/> is the
    /// enumeration's empty-string member, which means "take the one the host configured".
    /// </summary>
    internal ReferrerPolicy? ReferrerPolicy { get; set; }

    /// <summary>
    /// https://fetch.spec.whatwg.org/#concept-request-credentials-mode — <c>omit</c>, <c>same-origin</c> or
    /// <c>include</c>, and what decides whether <c>Options.WebApi.Fetch.CookieJar</c> is consulted for a hop.
    /// </summary>
    internal string Credentials { get; set; } = CredentialsSameOrigin;

    internal const string RedirectFollow = "follow";
    internal const string RedirectError = "error";
    internal const string RedirectManual = "manual";

    internal const string CredentialsOmit = "omit";
    internal const string CredentialsSameOrigin = "same-origin";
    internal const string CredentialsInclude = "include";

    /// <summary>
    /// What the <c>referrer</c> attribute answers for a request that inherits the environment's referrer,
    /// and the one value of the <c>referrer</c> member that means the same thing.
    /// </summary>
    internal const string ReferrerClient = "about:client";

    /// <summary>
    /// https://fetch.spec.whatwg.org/#dom-request-duplex — <c>enum RequestDuplex { "half" };</c> has exactly
    /// one value, and the attribute's getter steps are to return it, so no request carries any state for it.
    /// </summary>
    internal const string DuplexHalf = "half";
}
#endif
