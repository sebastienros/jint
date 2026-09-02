#if NET8_0_OR_GREATER
using Jint.WebApi.Url.Parsing;

namespace Jint.WebApi.Fetch;

/// <summary>
/// Which part of a referrer travels with a request, https://w3c.github.io/webappsec-referrer-policy/.
/// </summary>
/// <remarks>
/// The names are the enumeration's own, spelled the .NET way: <see cref="StrictOriginWhenCrossOrigin"/> is
/// <c>strict-origin-when-cross-origin</c>, which is also the default the standard gives a request that names
/// no policy.
/// </remarks>
public enum ReferrerPolicy
{
    /// <summary>No <c>Referer</c> header is ever sent.</summary>
    NoReferrer = 0,

    /// <summary>The full URL, unless the request downgrades from a secure referrer to an insecure URL.</summary>
    NoReferrerWhenDowngrade = 1,

    /// <summary>Only the referrer's origin, always.</summary>
    Origin = 2,

    /// <summary>The full URL for a same-origin request, the origin alone otherwise.</summary>
    OriginWhenCrossOrigin = 3,

    /// <summary>The full URL for a same-origin request, nothing at all otherwise.</summary>
    SameOrigin = 4,

    /// <summary>Only the origin, and nothing at all when the request downgrades.</summary>
    StrictOrigin = 5,

    /// <summary>The full URL same-origin, the origin cross-origin, and nothing on a downgrade.</summary>
    StrictOriginWhenCrossOrigin = 6,

    /// <summary>The full URL, whatever the request is and wherever it goes.</summary>
    UnsafeUrl = 7,
}

/// <summary>
/// Where a request's referrer comes from, https://fetch.spec.whatwg.org/#concept-request-referrer.
/// </summary>
internal enum FetchReferrerSource
{
    /// <summary>The empty string: the request carries no referrer at all.</summary>
    NoReferrer,

    /// <summary>"client": whatever the environment's referrer is, i.e. <c>Options.WebApi.Fetch.Referrer</c>.</summary>
    Client,

    /// <summary>An explicit URL the <c>referrer</c> init member named.</summary>
    Url,
}

/// <summary>
/// https://fetch.spec.whatwg.org/#determine-requests-referrer and the <c>Origin</c> header beside it.
/// </summary>
/// <remarks>
/// Engine-free by construction, like the rest of the transport's inputs: it takes URL records and answers
/// strings, so a redirect hop can recompute both from a thread pool thread.
/// </remarks>
internal static class FetchReferrer
{
    /// <summary>
    /// The length past which the standard replaces the referrer URL with its origin —
    /// https://fetch.spec.whatwg.org/#determine-requests-referrer step 4.
    /// </summary>
    private const int MaxReferrerLength = 4096;

    internal static string ToWireValue(ReferrerPolicy policy) => policy switch
    {
        ReferrerPolicy.NoReferrer => "no-referrer",
        ReferrerPolicy.NoReferrerWhenDowngrade => "no-referrer-when-downgrade",
        ReferrerPolicy.Origin => "origin",
        ReferrerPolicy.OriginWhenCrossOrigin => "origin-when-cross-origin",
        ReferrerPolicy.SameOrigin => "same-origin",
        ReferrerPolicy.StrictOrigin => "strict-origin",
        ReferrerPolicy.StrictOriginWhenCrossOrigin => "strict-origin-when-cross-origin",
        ReferrerPolicy.UnsafeUrl => "unsafe-url",
        _ => "strict-origin-when-cross-origin",
    };

    internal static bool TryParse(string text, out ReferrerPolicy policy)
    {
        switch (text)
        {
            case "no-referrer": policy = ReferrerPolicy.NoReferrer; return true;
            case "no-referrer-when-downgrade": policy = ReferrerPolicy.NoReferrerWhenDowngrade; return true;
            case "origin": policy = ReferrerPolicy.Origin; return true;
            case "origin-when-cross-origin": policy = ReferrerPolicy.OriginWhenCrossOrigin; return true;
            case "same-origin": policy = ReferrerPolicy.SameOrigin; return true;
            case "strict-origin": policy = ReferrerPolicy.StrictOrigin; return true;
            case "strict-origin-when-cross-origin": policy = ReferrerPolicy.StrictOriginWhenCrossOrigin; return true;
            case "unsafe-url": policy = ReferrerPolicy.UnsafeUrl; return true;
            default: policy = ReferrerPolicy.StrictOriginWhenCrossOrigin; return false;
        }
    }

    /// <summary>
    /// The <c>Referer</c> value a hop carries, or <see langword="null"/> for no header at all.
    /// </summary>
    /// <remarks>
    /// <paramref name="source"/> is the referrer as it stands for this hop, which for a redirect is the
    /// value the previous hop computed rather than the original — the specification re-runs main fetch per
    /// hop, so a policy that has already narrowed a URL to its origin does not widen it again.
    /// </remarks>
    internal static string? Determine(UrlRecord? source, UrlRecord target, ReferrerPolicy policy)
    {
        if (source is null || policy == ReferrerPolicy.NoReferrer)
        {
            return null;
        }

        // Step 4: strip credentials and the fragment, and fall back to the origin for an absurdly long URL.
        // "Strip url for use as a referrer" with originOnly set empties the path and drops the query, so the
        // origin arm answers a URL with a trailing slash rather than an ASCII-serialized origin — which is
        // what a browser puts on the wire and what the Origin header pointedly does not.
        var referrerUrl = StripForReferrer(source, originOnly: false);
        var referrerOrigin = StripForReferrer(source, originOnly: true);
        if (referrerUrl.Length > MaxReferrerLength)
        {
            referrerUrl = referrerOrigin;
        }

        // An opaque origin has nothing to disclose, so a policy that would answer one answers nothing.
        var hasOrigin = !string.Equals(source.SerializeOrigin(), "null", StringComparison.Ordinal);
        var sameOrigin = hasOrigin && string.Equals(source.SerializeOrigin(), target.SerializeOrigin(), StringComparison.Ordinal);
        var downgrade = IsPotentiallyTrustworthy(source) && !IsPotentiallyTrustworthy(target);

        return policy switch
        {
            ReferrerPolicy.UnsafeUrl => referrerUrl,
            ReferrerPolicy.Origin => hasOrigin ? referrerOrigin : null,
            ReferrerPolicy.SameOrigin => sameOrigin ? referrerUrl : null,
            ReferrerPolicy.OriginWhenCrossOrigin => sameOrigin ? referrerUrl : hasOrigin ? referrerOrigin : null,
            ReferrerPolicy.NoReferrerWhenDowngrade => downgrade ? null : referrerUrl,
            ReferrerPolicy.StrictOrigin => downgrade || !hasOrigin ? null : referrerOrigin,
            _ => StrictOriginWhenCrossOrigin(referrerUrl, referrerOrigin, sameOrigin, hasOrigin, downgrade),
        };
    }

    private static string? StrictOriginWhenCrossOrigin(string referrerUrl, string referrerOrigin, bool sameOrigin, bool hasOrigin, bool downgrade)
    {
        if (sameOrigin)
        {
            return referrerUrl;
        }

        return downgrade || !hasOrigin ? null : referrerOrigin;
    }

    /// <summary>
    /// https://w3c.github.io/webappsec-referrer-policy/#strip-url — the credentials and the fragment go, and
    /// nothing else does.
    /// </summary>
    private static string StripForReferrer(UrlRecord url, bool originOnly)
    {
        if (url.HasOpaquePath || url.Host is null)
        {
            return originOnly ? url.Scheme + ":" : url.Scheme + ":" + url.SerializePath() + Search(url);
        }

        var prefix = url.Scheme + "://" + url.SerializeHostAndPort();
        return originOnly ? prefix + "/" : prefix + url.SerializePath() + Search(url);
    }

    private static string Search(UrlRecord url) => url.Query is null ? string.Empty : "?" + url.Query;

    /// <summary>
    /// https://w3c.github.io/webappsec-secure-contexts/#is-origin-trustworthy, reduced to the two arms that
    /// can decide a network fetch: the scheme, and a loopback host.
    /// </summary>
    /// <remarks>
    /// The <c>file</c> and extension arms are not reachable here — <c>AllowedSchemes</c> admits neither by
    /// default and neither can be fetched — and the "user agent has configured this origin as trusted" arm
    /// has no configuration to read.
    /// </remarks>
    internal static bool IsPotentiallyTrustworthy(UrlRecord url)
    {
        if (url.Scheme is "https" or "wss")
        {
            return true;
        }

        var host = url.SerializeHost();
        if (host.Length == 0)
        {
            return false;
        }

        if (string.Equals(host, "localhost", StringComparison.Ordinal)
            || host.EndsWith(".localhost", StringComparison.Ordinal)
            || string.Equals(host, "[::1]", StringComparison.Ordinal))
        {
            return true;
        }

        // 127.0.0.0/8, which the URL parser has already normalized into dotted-quad form.
        return host.StartsWith("127.", StringComparison.Ordinal) && System.Net.IPAddress.TryParse(host, out var address)
            && address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork;
    }

    /// <summary>
    /// https://fetch.spec.whatwg.org/#append-a-request-origin-header, reduced to the arm an engine with no
    /// CORS model reaches: a request whose method is neither <c>GET</c> nor <c>HEAD</c>.
    /// </summary>
    /// <remarks>
    /// Answers <see langword="null"/> for no header at all — the host named no origin — and the literal
    /// <c>"null"</c> where the specification says to send an opaque origin, which is what the downgrade and
    /// same-origin arms of step 3.1 produce.
    /// </remarks>
    internal static string? DetermineOrigin(UrlRecord? origin, UrlRecord target, string method, ReferrerPolicy policy)
    {
        if (origin is null || method is "GET" or "HEAD")
        {
            return null;
        }

        var serialized = origin.SerializeOrigin();
        if (string.Equals(serialized, "null", StringComparison.Ordinal))
        {
            return "null";
        }

        switch (policy)
        {
            case ReferrerPolicy.NoReferrer:
                return "null";

            case ReferrerPolicy.NoReferrerWhenDowngrade:
            case ReferrerPolicy.StrictOrigin:
            case ReferrerPolicy.StrictOriginWhenCrossOrigin:
                return string.Equals(origin.Scheme, "https", StringComparison.Ordinal)
                    && !string.Equals(target.Scheme, "https", StringComparison.Ordinal)
                        ? "null"
                        : serialized;

            case ReferrerPolicy.SameOrigin:
                return string.Equals(serialized, target.SerializeOrigin(), StringComparison.Ordinal) ? serialized : "null";

            default:
                return serialized;
        }
    }
}
#endif
