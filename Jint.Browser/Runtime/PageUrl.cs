using Jint.WebApi.Url.Parsing;

namespace Jint.Browser.Runtime;

/// <summary>
/// The URL questions a navigation asks: resolve a target against the document, name its origin, and decide
/// whether two URLs are the same document.
/// </summary>
/// <remarks>
/// <para>
/// Every answer goes through the engine's own WHATWG parser rather than <see cref="Uri"/>, because that is
/// the grammar <c>fetch</c>, <c>XMLHttpRequest</c> and the <c>URL</c> constructor already measure a page's
/// URLs against — and because <see cref="Uri"/> disagrees with it on exactly the inputs a page produces
/// (a bare fragment, a scheme-relative reference, an empty path).
/// </para>
/// <para>
/// The one place <see cref="Uri"/> is still authoritative is the socket: the transport re-parses the
/// serialized URL as one, because a host the WHATWG grammar admits and <see cref="Uri"/> cannot express must
/// be refused rather than guessed at. That check lives in the engine's own fetch policy.
/// </para>
/// </remarks>
internal static class PageUrl
{
    /// <summary>The serialized origin of an opaque origin — a document that has none.</summary>
    internal const string OpaqueOrigin = "null";

    /// <summary>The document a page shows before it has loaded anything.</summary>
    internal const string Blank = "about:blank";

    /// <summary>Resolves <paramref name="target"/> against <paramref name="baseUrl"/>, or answers null.</summary>
    internal static string? Resolve(string target, string? baseUrl)
    {
        var record = Parse(target, baseUrl);
        return record?.Serialize();
    }

    /// <summary>Parses <paramref name="target"/> against <paramref name="baseUrl"/>, or answers null.</summary>
    internal static UrlRecord? Parse(string target, string? baseUrl)
    {
        var relativeTo = baseUrl is null ? null : UrlParser.Parse(baseUrl);
        return UrlParser.Parse(target, relativeTo);
    }

    /// <summary>Whether a page can load this scheme over the network.</summary>
    internal static bool IsNetworkScheme(UrlRecord url)
        => string.Equals(url.Scheme, "http", StringComparison.Ordinal)
        || string.Equals(url.Scheme, "https", StringComparison.Ordinal);

    /// <summary>
    /// The serialized origin of a URL, or <see cref="OpaqueOrigin"/> for a document that has none.
    /// </summary>
    /// <remarks>
    /// <c>about:blank</c>, a <c>data:</c> URL and a document built from markup all have an opaque origin,
    /// which is why <c>localStorage</c> is unreachable from them and why nothing they store could be shared
    /// with anything: HTML gives each of them a fresh opaque origin, and two opaque origins are never equal.
    /// </remarks>
    internal static string OriginOf(string? url)
    {
        if (url is null)
        {
            return OpaqueOrigin;
        }

        var record = UrlParser.Parse(url);
        return record is null || !IsNetworkScheme(record) ? OpaqueOrigin : record.SerializeOrigin();
    }

    /// <summary>Whether the URL has a real origin — the condition for storage and for an <c>Origin</c> header.</summary>
    internal static bool HasOrigin(string? url) => !string.Equals(OriginOf(url), OpaqueOrigin, StringComparison.Ordinal);

    /// <summary>
    /// Whether navigating from <paramref name="from"/> to <paramref name="to"/> stays in the same document:
    /// the two differ in nothing but their fragment.
    /// </summary>
    /// <remarks>
    /// https://html.spec.whatwg.org/multipage/browsing-the-web.html#navigate step 3's "fragment navigation".
    /// A fragment navigation keeps the document, the engine and everything on it, and fires
    /// <c>hashchange</c> instead of unloading; a page's router is built out of exactly this.
    /// </remarks>
    internal static bool IsSameDocument(string? from, string to)
    {
        if (from is null)
        {
            return false;
        }

        var current = UrlParser.Parse(from);
        var target = UrlParser.Parse(to);

        if (current is null || target is null)
        {
            return false;
        }

        return string.Equals(
            current.Serialize(excludeFragment: true),
            target.Serialize(excludeFragment: true),
            StringComparison.Ordinal);
    }

    /// <summary>The fragment of a URL, without its <c>#</c>; the empty string when it has none.</summary>
    internal static string FragmentOf(string url)
    {
        var record = UrlParser.Parse(url);
        return record?.Fragment ?? "";
    }

    /// <summary>
    /// The URL as a <see cref="Uri"/> a transport can open, or <see langword="null"/> when the WHATWG
    /// grammar admits something <see cref="Uri"/> cannot express.
    /// </summary>
    internal static Uri? ToUri(UrlRecord url)
        => Uri.TryCreate(url.Serialize(), UriKind.Absolute, out var parsed) ? parsed : null;
}
