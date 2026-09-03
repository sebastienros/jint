using System.Net.Http;
using Jint.WebApi.Fetch;

namespace Jint.Browser;

/// <summary>
/// What one <see cref="BrowserContext"/> keeps to itself: its cookies, its storage, and the network position
/// its pages load from.
/// </summary>
/// <remarks>
/// <para>
/// A context is the unit of isolation a browser profile is. Two contexts of the same browser share the
/// <see cref="BrowserOptions"/> and share nothing else: not a cookie, not a <c>localStorage</c> entry, not a
/// connection pool if each names its own client. Everything here is read once, when the context is created.
/// </para>
/// <para>
/// <b>Enabling a context is enabling the host's network position.</b> Anything the host process can reach —
/// an internal service, a cloud metadata endpoint, a database admin port — a page's script can reach too
/// unless <see cref="UrlFilter"/> or <see cref="BlockPrivateNetwork"/> says otherwise. One filter covers
/// every kind of load: the document, a subresource, <c>fetch</c>, <c>XMLHttpRequest</c> and a worker's
/// modules, on the first hop and on every redirect.
/// </para>
/// </remarks>
public sealed class BrowserContextOptions
{
    /// <summary>Where this context's cookies live.</summary>
    /// <remarks>
    /// <para>
    /// <see langword="null"/> — the default — gives the context a private <see cref="CookieContainerCookieJar"/>,
    /// which is what makes its pages share cookies with each other and with nothing else. The same jar answers
    /// a request's <c>Cookie</c> header, stores a response's <c>Set-Cookie</c> and backs
    /// <c>document.cookie</c>.
    /// </para>
    /// <para>
    /// <b>An <c>HttpOnly</c> cookie is hidden from script only in the default jar.</b> The
    /// <see cref="CookieJar"/> interface has no such flag — a request never needs one — so the rule is
    /// enforced against <see cref="CookieContainerCookieJar"/>'s container, which does carry it per cookie. A
    /// jar of another type is a jar whose every cookie <c>document.cookie</c> can read and overwrite; a host
    /// that needs the rule keeps the default, or keeps its <c>HttpOnly</c> cookies out of the jar it supplies.
    /// </para>
    /// </remarks>
    public CookieJar? CookieJar { get; set; }

    /// <summary>Where this context's <c>localStorage</c> lives, one store per origin.</summary>
    /// <remarks>
    /// <see langword="null"/> — the default — gives the context an
    /// <see cref="InMemoryStoragePartitionProvider"/>, so two pages of one origin share a store, two origins
    /// do not, and nothing is written anywhere. <c>sessionStorage</c> is never partitioned by this: it is per
    /// page, which is the lifetime its name promises.
    /// </remarks>
    public StoragePartitionProvider? StoragePartition { get; set; }

    /// <summary>The <see cref="System.Net.Http.HttpClient"/> every request of this context goes through.</summary>
    /// <remarks>
    /// <para>
    /// <see langword="null"/> — the default — uses the client Jint shares process-wide, which is never
    /// disposed and whose handler already has automatic redirects off. Supplying one is how a host takes
    /// control of the transport: a <c>DelegatingHandler</c> is the seam for authentication, per-tenant
    /// headers, logging and test doubles.
    /// </para>
    /// <para>
    /// Redirects are driven by Jint so that every hop is re-checked against <see cref="UrlFilter"/>, so set
    /// <c>AllowAutoRedirect</c> to <see langword="false"/> on a handler you supply — one that follows them
    /// itself would follow them underneath the check.
    /// </para>
    /// </remarks>
    public HttpClient? HttpClient { get; set; }

    /// <summary>A per-engine source of clients, which wins over <see cref="HttpClient"/> when both are set.</summary>
    /// <remarks>
    /// Called on the page's own thread, once per page engine — that is, once per navigation — so it may read
    /// per-page host state through <c>engine.HostDefined</c>. A document fetch has no engine of its own to
    /// hand over, because the engine that will show the document does not exist yet, so the factory is asked
    /// with the page's outgoing engine and falls back to <see cref="HttpClient"/> when there is none.
    /// </remarks>
    public Func<Engine, HttpClient>? HttpClientFactory { get; set; }

    /// <summary>The last word on whether any load of this context may be made.</summary>
    /// <remarks>
    /// <para>
    /// <see langword="null"/> — the default — allows everything the scheme list already admitted, which for a
    /// page is <c>http</c> and <c>https</c>. It is combined with <see cref="BlockPrivateNetwork"/> rather than
    /// replaced by it: with both set, a URL has to satisfy both.
    /// </para>
    /// <para>
    /// <b>Re-run on every redirect hop</b>, which is the point: a filter that only saw the first URL would be
    /// defeated by a server answering <c>302 Location: http://169.254.169.254/</c>.
    /// </para>
    /// <para>
    /// <b>Must be thread-safe and must not block.</b> The first call is on the page's own thread; the
    /// redirect calls are on whichever thread the HTTP stack completed the previous hop on. It must not touch
    /// an <see cref="Engine"/>.
    /// </para>
    /// </remarks>
    public Func<Uri, bool>? UrlFilter { get; set; }

    /// <summary>
    /// Whether a load to a private, loopback or link-local address is refused; <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Off by default</b>, deliberately: the first thing most hosts point a page at is a test server on
    /// <c>localhost</c>, and a default that refused it would be turned off wholesale rather than narrowed. A
    /// deployment that loads pages a stranger named wants it on, and wants a <see cref="UrlFilter"/> as well.
    /// </para>
    /// <para>
    /// <b><see cref="BrowserOptions.BlockPrivateNetwork"/> is the browser-wide default</b>, which
    /// <see cref="BrowserOptions.ForUntrustedContent"/> turns on — unless the context's own options assigned
    /// this property, in which case the context keeps its choice and <see langword="false"/> really means
    /// "and I mean it". Assigning <see langword="true"/> is what a context that must block regardless of the
    /// browser's posture does.
    /// </para>
    /// <para>
    /// <b>It is a coarse rule and cannot be otherwise.</b> It refuses an address literal in the private,
    /// loopback, link-local, carrier-grade-NAT or unique-local ranges — the cloud metadata endpoint among
    /// them — and the names <c>localhost</c> and anything under <c>.localhost</c>. It cannot refuse a
    /// <i>name</i> that resolves to a private address, because that answer only exists inside the socket. A
    /// deployment that must be sure resolves the name in a <see cref="UrlFilter"/> of its own, or runs where
    /// the private network is not reachable.
    /// </para>
    /// </remarks>
    public bool BlockPrivateNetwork
    {
        get => _blockPrivateNetwork ?? false;
        set => _blockPrivateNetwork = value;
    }

    /// <summary>
    /// What the host assigned to <see cref="BlockPrivateNetwork"/>, or <see langword="null"/> when it left
    /// the decision to the browser.
    /// </summary>
    internal bool? BlockPrivateNetworkAssignment => _blockPrivateNetwork;

    private bool? _blockPrivateNetwork;
}
