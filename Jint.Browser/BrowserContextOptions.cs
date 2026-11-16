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
    /// <summary>Gets the context-owned HTTP cache configuration. Caching is disabled by default.</summary>
    public BrowserHttpCacheOptions HttpCache { get; } = new();

    private int? _maxPages;
    private long? _maxTotalStorageBytes;

    /// <summary>Gets or sets this context's aggregate storage quota; null uses BrowserOptions.MaxTotalStorageBytes.</summary>
    /// <remarks>Uses the browser quota's accounting. Under ForUntrustedContent, long.MaxValue uses the finite browser default.</remarks>
    public long? MaxTotalStorageBytes
    {
        get => _maxTotalStorageBytes;
        set
        {
            if (value is < 0) throw new ArgumentOutOfRangeException(nameof(value));
            _maxTotalStorageBytes = value;
        }
    }

    /// <summary>Overrides <see cref="BrowserOptions.MaxPages"/> for this context; null uses the browser default.</summary>
    /// <remarks>
    /// Counts host pages, popups and pending creations together. Zero means unlimited for trusted content;
    /// zero and <see cref="int.MaxValue"/> use the finite browser default under ForUntrustedContent.
    /// </remarks>
    public int? MaxPages
    {
        get => _maxPages;
        set
        {
            if (value is < 0) throw new ArgumentOutOfRangeException(nameof(value));
            _maxPages = value;
        }
    }

    /// <summary>
    /// The viewport every page of this context opens with, or <see langword="null"/> — the default — for
    /// <see cref="BrowserOptions.Viewport"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The device-profile half of a context, which is what lets one browser drive a desktop, a tablet and a
    /// phone at once: each context's pages report their own <c>innerWidth</c>, <c>devicePixelRatio</c> and
    /// dimension media features from their <b>first</b> document. A popup a page of this context opens opens
    /// at this size too.
    /// </para>
    /// <para>
    /// It is where a page starts, not where it has to stay: <see cref="Page.SetViewportAsync(Jint.Browser.Viewport)"/>
    /// and a protocol client's <c>Emulation.setDeviceMetricsOverride</c> resize one page, and
    /// <c>Emulation.clearDeviceMetricsOverride</c> puts it back to this value. Read once, when the context is
    /// created; its width and height must be at least 1 and its device scale factor positive and finite.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a usable viewport.</exception>
    public Viewport? Viewport
    {
        get => _viewport;
        set
        {
            if (value is { } viewport)
            {
                Jint.Browser.Viewport.ThrowIfUnusable(viewport, nameof(value));
            }

            _viewport = value;
        }
    }

    private Viewport? _viewport;

    /// <summary>
    /// The URL a relative address a host passes to a page of this context is resolved against, or
    /// <see langword="null"/> — the default — for none.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A test suite names its server once rather than on every call:
    /// <c>page.NavigateAsync("/admin")</c> and <c>page.WaitForUrlAsync("/admin", ...)</c> resolve against it,
    /// with the URL standard's own rules, so <c>"/admin"</c> keeps its host and <c>"admin"</c> its directory.
    /// It is the host's address bar and nothing else: a page's own links, forms and <c>location</c> writes
    /// still resolve against the document's base URL. An absolute address is left alone.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException">The value is not an absolute URL.</exception>
    public string? BaseUrl
    {
        get => _baseUrl;
        set
        {
            if (value is not null && Runtime.PageUrl.Parse(value, null) is null)
            {
                throw new ArgumentException("A base URL must be absolute.", nameof(value));
            }

            _baseUrl = value;
        }
    }

    private string? _baseUrl;

    /// <summary>
    /// How long a page's actions wait for their target to be actionable; <see cref="TimeSpan.Zero"/> — the
    /// default — looks once.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Page.ClickAsync(string, NavigationOptions)"/>, <see cref="Page.FillAsync(string, string)"/>,
    /// <see cref="Page.TypeAsync(string, string)"/>, <see cref="Page.SelectAsync(string, string)"/>,
    /// <see cref="Page.HoverAsync(string)"/> and <see cref="Page.SetInputFilesAsync(string, IReadOnlyList{string})"/>
    /// answer <see langword="false"/> when their target is missing, not being rendered, not editable or covered
    /// by another element. With a timeout they look again, while the page goes on running, until the target is
    /// ready or the time is up — Playwright's actionability wait, for a suite written against it. An attempt
    /// that dispatched input is never repeated, so a click that started but landed elsewhere still answers
    /// <see langword="false"/> at once.
    /// </para>
    /// <para>
    /// <see cref="Page.FocusAsync"/> and the target form of <see cref="Page.PressAsync(string, string, KeyModifiers, int, NavigationOptions?, TimeSpan?)"/>
    /// have a timeout of their own and do not read this one.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative and not infinite.</exception>
    public TimeSpan ActionTimeout
    {
        get => _actionTimeout;
        set => _actionTimeout = value >= TimeSpan.Zero || value == System.Threading.Timeout.InfiniteTimeSpan
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "ActionTimeout cannot be negative.");
    }

    private TimeSpan _actionTimeout;

    /// <summary>
    /// Whether every page of this context opens as a touch device, or <see langword="null"/> — the default —
    /// for <see cref="BrowserOptions.HasTouch"/>.
    /// </summary>
    /// <remarks>
    /// The other half of a device profile, beside <see cref="Viewport"/>, with the meaning
    /// <see cref="BrowserOptions.HasTouch"/> gives it: what a page <i>detects</i> from its first document, which
    /// <see cref="Page.SetTouchEmulationAsync"/> changes later for one page. Read once, when the context is
    /// created.
    /// </remarks>
    public bool? HasTouch { get; set; }

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

    /// <summary>
    /// The media type and preferences this context's pages open with, over
    /// <see cref="BrowserOptions.MediaEmulation"/>; <see langword="null"/> — the default — adds nothing to the
    /// browser's.
    /// </summary>
    /// <remarks>
    /// Playwright's <c>colorScheme</c>, <c>reducedMotion</c>, <c>forcedColors</c> and <c>contrast</c> context
    /// options, in one value. It is merged property by property: a <see langword="null"/> property keeps the
    /// browser's value and an empty one clears it for this context. Every page of the context — including a
    /// popup one of them opens — parses its first document in it. It is a default beneath the page's
    /// overrides: <see cref="Page.EmulateMediaAsync"/> and <c>Emulation.setEmulatedMedia</c> change one page
    /// from there, and clearing what they set comes back to it.
    /// </remarks>
    public MediaEmulation? MediaEmulation { get; set; }

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
