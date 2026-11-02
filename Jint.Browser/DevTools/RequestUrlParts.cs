namespace Jint.Browser.DevTools;

/// <summary>
/// A request URL the way <c>Network.Request</c> carries it: the URL without its fragment, and the fragment — its
/// leading <c>#</c> included — on its own.
/// </summary>
/// <remarks>
/// Chromium's <c>NetworkHandler::ExtractFragment</c> (<c>content/browser/devtools/protocol/network_handler.cc</c>)
/// takes the fragment off with <c>GURL::ReplaceComponents</c> and sends it as <c>urlFragment</c>, so neither
/// <c>Network.requestWillBeSent</c> nor <c>Fetch.requestPaused</c> shows a <c>#</c> in <c>request.url</c>. An
/// absolute URL's serialization has no <c>#</c> but the one that starts its fragment, and a URL with an empty
/// fragment (<c>https://h/p#</c>) reports <c>#</c> rather than none.
/// </remarks>
internal static class RequestUrlParts
{
    /// <summary>Splits <paramref name="url"/> at its fragment; <paramref name="fragment"/> is <see langword="null"/> when there is none.</summary>
    internal static string Split(string url, out string? fragment)
    {
        var hash = url.IndexOf('#');
        if (hash < 0)
        {
            fragment = null;
            return url;
        }

        fragment = url[hash..];
        return url[..hash];
    }
}
