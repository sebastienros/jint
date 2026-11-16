namespace Jint.Browser.Runtime;

/// <summary>
/// The network error reasons a request can be failed with, and the <c>net::ERR_*</c> string each stands for.
/// </summary>
/// <remarks>
/// One table for the two clients that fail a request on purpose: the protocol's <c>Fetch.failRequest</c>,
/// whose <c>Network.ErrorReason</c> values are spelled <c>ConnectionRefused</c>, and a host route's
/// <c>RoutedRequest.AbortAsync</c>, which takes Playwright's <c>connectionrefused</c> — the same words, so the
/// lookup ignores case.
/// </remarks>
internal static class PageNetworkErrors
{
    private static readonly Dictionary<string, string> s_reasons = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Failed"] = "net::ERR_FAILED",
        ["Aborted"] = "net::ERR_ABORTED",
        ["TimedOut"] = "net::ERR_TIMED_OUT",
        ["AccessDenied"] = "net::ERR_ACCESS_DENIED",
        ["ConnectionClosed"] = "net::ERR_CONNECTION_CLOSED",
        ["ConnectionReset"] = "net::ERR_CONNECTION_RESET",
        ["ConnectionRefused"] = "net::ERR_CONNECTION_REFUSED",
        ["ConnectionAborted"] = "net::ERR_CONNECTION_ABORTED",
        ["ConnectionFailed"] = "net::ERR_CONNECTION_FAILED",
        ["NameNotResolved"] = "net::ERR_NAME_NOT_RESOLVED",
        ["InternetDisconnected"] = "net::ERR_INTERNET_DISCONNECTED",
        ["AddressUnreachable"] = "net::ERR_ADDRESS_UNREACHABLE",
        ["BlockedByClient"] = "net::ERR_BLOCKED_BY_CLIENT",
        ["BlockedByResponse"] = "net::ERR_BLOCKED_BY_RESPONSE",
    };

    /// <summary>The <c>net::ERR_*</c> string <paramref name="reason"/> stands for, when it is a known reason.</summary>
    internal static bool TryGet(string reason, out string error)
    {
        if (s_reasons.TryGetValue(reason, out var found))
        {
            error = found;
            return true;
        }

        error = "net::ERR_FAILED";
        return false;
    }
}
