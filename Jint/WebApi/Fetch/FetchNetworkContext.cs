#if NET8_0_OR_GREATER
using Jint.WebApi.Url.Parsing;

namespace Jint.WebApi.Fetch;

/// <summary>
/// The document-shaped half of <c>Options.WebApi.Fetch</c>, parsed once per engine: the API base URL, the
/// referrer and the origin as URL records, plus the two host objects a request may reach.
/// </summary>
/// <remarks>
/// <para>
/// The three URLs are parsed here rather than per request so that a <c>new Request()</c> in a loop does not
/// re-parse the base URL every time, and so that a value <see cref="Uri"/> can express but the WHATWG parser
/// cannot is refused once instead of on every call.
/// </para>
/// <para>
/// Built on the engine thread and never mutated, which is what lets the transport read it from a pool
/// thread; it is hung off <c>WebApiEngineState</c> and rebuilt whenever the settings are attached or
/// re-pointed.
/// </para>
/// </remarks>
internal sealed class FetchNetworkContext
{
    /// <summary>What an engine whose host configured none of this reads.</summary>
    internal static readonly FetchNetworkContext Empty = new();

    private FetchNetworkContext()
    {
    }

    /// <summary>https://html.spec.whatwg.org/multipage/webappapis.html#api-base-url, or null for none.</summary>
    internal UrlRecord? BaseUrl { get; private init; }

    /// <summary>The environment's referrer — what a request whose referrer is "client" resolves to.</summary>
    internal UrlRecord? Referrer { get; private init; }

    /// <summary>The URL <c>Options.WebApi.Fetch.Origin</c> named, kept for its origin alone.</summary>
    internal UrlRecord? Origin { get; private init; }

    internal ReferrerPolicy ReferrerPolicy { get; private init; } = ReferrerPolicy.StrictOriginWhenCrossOrigin;

    internal CookieJar? CookieJar { get; private init; }

    internal FetchObserver? Observer { get; private init; }

    /// <summary>
    /// What <c>credentials: "same-origin"</c> compares a hop against: the configured origin, or the base
    /// URL's when no origin was named, or nothing — in which case no same-origin request carries cookies.
    /// </summary>
    internal UrlRecord? SameOriginReference => Origin ?? BaseUrl;

    internal static FetchNetworkContext From(Options.FetchOptions? options)
    {
        if (options is null)
        {
            return Empty;
        }

        return new FetchNetworkContext
        {
            BaseUrl = Parse(options.BaseUrl),
            Referrer = Parse(options.Referrer),
            Origin = options.Origin is { } origin ? UrlParser.Parse(origin) : null,
            ReferrerPolicy = options.ReferrerPolicy,
            CookieJar = options.CookieJar,
            Observer = options.Observer,
        };
    }

    /// <summary>
    /// A <see cref="Uri"/> read through the WHATWG parser, which is the grammar every other URL in this
    /// subtree is measured against; one the parser refuses is treated as absent.
    /// </summary>
    private static UrlRecord? Parse(Uri? uri)
        => uri is { IsAbsoluteUri: true } ? UrlParser.Parse(uri.AbsoluteUri) : null;
}
#endif
