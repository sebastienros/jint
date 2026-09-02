using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using Jint.WebApi.Fetch;

namespace Jint.Browser.Runtime;

/// <summary>
/// One <see cref="BrowserContext"/>'s network position: the client its pages send through, the filter every
/// hop is re-checked against, and the jar its cookies live in.
/// </summary>
/// <remarks>
/// <para>
/// <b>One instance per context, shared by every page in it</b> — which is what makes two pages of a context
/// share cookies and what makes two contexts share nothing. It is reached from page threads and from
/// transport threads, so everything on it is immutable after construction and the two host objects it holds
/// carry their own thread-safety obligation.
/// </para>
/// <para>
/// <b>The filter is the context's, composed once.</b> A host's own <c>UrlFilter</c> and
/// <see cref="BrowserContextOptions.BlockPrivateNetwork"/> are combined here rather than at each call site,
/// so the same function is what bounds a navigation, a subresource, an <c>XMLHttpRequest</c>, a
/// <c>fetch</c> and a worker's module load — one filter, every kind of load, on the first hop and on every
/// redirect.
/// </para>
/// </remarks>
internal sealed class PageNetwork
{
    private readonly HttpClient? _client;
    private readonly Func<Engine, HttpClient>? _clientFactory;

    internal PageNetwork(BrowserContextOptions options)
    {
        _client = options.HttpClient;
        _clientFactory = options.HttpClientFactory;

        // A jar per context, always: a context is the unit of isolation a browser profile is, and cookies
        // are the state that makes that visible. A host supplying its own is supplying the partition.
        CookieJar = options.CookieJar ?? new CookieContainerCookieJar();
        Storage = options.StoragePartition ?? new InMemoryStoragePartitionProvider();
        BlockPrivateNetwork = options.BlockPrivateNetwork;

        var hostFilter = options.UrlFilter;
        UrlFilter = (hostFilter, options.BlockPrivateNetwork) switch
        {
            (null, false) => static _ => true,
            (null, true) => IsPublic,
            ({ } filter, false) => filter,
            ({ } filter, true) => uri => IsPublic(uri) && filter(uri),
        };
    }

    /// <summary>Where this context's cookies live; never <see langword="null"/>.</summary>
    internal CookieJar CookieJar { get; }

    /// <summary>Where this context's <c>localStorage</c> lives, one store per origin.</summary>
    internal StoragePartitionProvider Storage { get; }

    /// <summary>The last word on whether a hop may be made, host filter and private-network rule combined.</summary>
    internal Func<Uri, bool> UrlFilter { get; }

    /// <summary>Whether the private-network rule is part of <see cref="UrlFilter"/>.</summary>
    internal bool BlockPrivateNetwork { get; }

    /// <summary>The client a host supplied for the whole context, or <see langword="null"/>.</summary>
    internal HttpClient? Client => _client;

    /// <summary>The per-engine client factory a host supplied, or <see langword="null"/>.</summary>
    internal Func<Engine, HttpClient>? ClientFactory => _clientFactory;

    /// <summary>The client one load goes through: the factory, then the context's client, then the shared one.</summary>
    /// <remarks>
    /// Must be called on the thread that owns <paramref name="engine"/>, because a host's factory is
    /// documented to run there and to be allowed to read <c>engine.HostDefined</c>. A caller with no engine
    /// to offer — a worker being built before its own engine exists — passes <see langword="null"/> and gets
    /// the context's client instead.
    /// </remarks>
    internal HttpClient ClientFor(Engine? engine)
    {
        if (_clientFactory is { } factory && engine is not null && factory(engine) is { } fromFactory)
        {
            return fromFactory;
        }

        return _client ?? FetchTransport.SharedClient;
    }

    /// <summary>
    /// Whether a URL names a host outside the private address space — the
    /// <see cref="BrowserContextOptions.BlockPrivateNetwork"/> rule.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It refuses a loopback, link-local, unique-local or RFC 1918 literal, and the names
    /// <c>localhost</c> and anything under <c>.localhost</c>, which the URL Standard defines as resolving to
    /// loopback. It cannot refuse a <i>name</i> that resolves to a private address — that answer only exists
    /// after DNS, which is inside the socket — so this is a coarse rule and says so: a deployment that must
    /// be sure resolves the name itself in a <see cref="BrowserContextOptions.UrlFilter"/> of its own, or
    /// puts the process where the private network is not reachable.
    /// </para>
    /// <para>
    /// The cloud metadata endpoint (<c>169.254.169.254</c>) is link-local, so it is covered.
    /// </para>
    /// </remarks>
    private static bool IsPublic(Uri uri)
    {
        var host = uri.Host;

        if (uri.IsLoopback
            || string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!IPAddress.TryParse(host.Trim('[', ']'), out var address))
        {
            // A name. Nothing here can know where it resolves to; see the remarks.
            return true;
        }

        if (IPAddress.IsLoopback(address))
        {
            return false;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            Span<byte> octets = stackalloc byte[4];
            if (!address.TryWriteBytes(octets, out _))
            {
                return false;
            }

            return octets[0] switch
            {
                10 => false,
                127 => false,
                0 => false,
                172 when octets[1] >= 16 && octets[1] <= 31 => false,
                192 when octets[1] == 168 => false,
                169 when octets[1] == 254 => false,
                100 when octets[1] >= 64 && octets[1] <= 127 => false,
                _ => true,
            };
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6UniqueLocal)
            {
                return false;
            }

            // An IPv4 address written as ::ffff:a.b.c.d is the same host by another spelling.
            if (address.IsIPv4MappedToIPv6)
            {
                return IsPublic(new UriBuilder(uri) { Host = address.MapToIPv4().ToString() }.Uri);
            }
        }

        return true;
    }
}
