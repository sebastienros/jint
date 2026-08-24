#if NET8_0_OR_GREATER
using Jint.WebApi.Url.Parsing;

namespace Jint.WebApi.WebSockets;

/// <summary>
/// Whether a socket may be opened at all: the host's network policy, translated from the schemes
/// <c>fetch</c> speaks to the two a <c>WebSocket</c> speaks.
/// </summary>
/// <remarks>
/// <para>
/// <b>There is no separate WebSocket policy group.</b> A socket is outbound network access from the same
/// process, reaching the same hosts, so it answers to the same settings — <c>Options.WebApi.Fetch</c>. Only
/// the scheme list needs translating, because a host writes it in terms of <c>http</c> and <c>https</c>:
/// <c>ws</c> is admitted by <c>http</c> and <c>wss</c> by <c>https</c>, and naming <c>ws</c> or <c>wss</c>
/// outright works too, for a host that wants sockets and not fetches.
/// </para>
/// <para>
/// <b>The filter sees the <c>ws:</c> URL</b>, not the <c>http:</c> one the handshake is made of. That is the
/// URL the script asked for and the one an error message would name, and it fails safe: a filter written for
/// fetch that tests <c>uri.Scheme == "https"</c> refuses every socket rather than admitting one it was never
/// shown.
/// </para>
/// <para>
/// Unlike <c>fetch</c> there is no per-hop re-check to do, because there are no hops: the WHATWG handshake
/// sets the request's redirect mode to <c>error</c>, so the one URL the filter admitted is the only one the
/// socket can reach.
/// </para>
/// </remarks>
internal static class WebSocketPolicy
{
    /// <summary>
    /// Runs the whole check, and yields the <see cref="Uri"/> the transport should open.
    /// </summary>
    internal static bool Allows(Options.FetchOptions options, UrlRecord url, out Uri uri)
    {
        uri = null!;

        if (!IsSchemeAllowed(options.AllowedSchemes, url.Scheme))
        {
            return false;
        }

        // The WHATWG serialization is always absolute, but Uri has a grammar of its own and a host it cannot
        // represent must be refused rather than guessed at.
        if (!Uri.TryCreate(url.Serialize(), UriKind.Absolute, out var parsed))
        {
            return false;
        }

        uri = parsed;

        var filter = options.UrlFilter;
        return filter is null || filter(uri);
    }

    private static bool IsSchemeAllowed(OptionsList<string> allowed, string scheme)
    {
        // The parser has already lowercased the scheme and the constructor has already refused everything
        // that is not one of these two.
        var httpEquivalent = string.Equals(scheme, "wss", StringComparison.Ordinal) ? "https" : "http";

        foreach (var candidate in allowed)
        {
            if (string.Equals(candidate, scheme, StringComparison.OrdinalIgnoreCase)
                || string.Equals(candidate, httpEquivalent, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
#endif
