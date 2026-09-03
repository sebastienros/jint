using Jint.Browser.Runtime;
using Jint.DevTools;
using Jint.DevTools.Protocol.Network;
using ProtocolNetworkEvents = Jint.DevTools.Domains.NetworkEvents;

namespace Jint.Browser.DevTools;

/// <summary>
/// What the <c>Network</c> domain says without being asked: one request's life, in Chrome's order.
/// </summary>
/// <remarks>
/// <para>
/// <b>The order is the transport's and it is the order Chrome sends.</b> For one request:
/// <c>requestWillBeSent</c> and <c>requestWillBeSentExtraInfo</c> per hop, with the hop after a redirect
/// carrying that redirect as <c>redirectResponse</c> and no response event of its own; then
/// <c>responseReceived</c> and <c>responseReceivedExtraInfo</c> for the answer; then <c>dataReceived</c> per
/// body chunk; then exactly one of <c>loadingFinished</c> and <c>loadingFailed</c>.
/// </para>
/// <para>
/// <b>The document's <c>requestWillBeSent</c> comes before <c>Page.frameNavigated</c></b>, and not by
/// arrangement: the fetch happens off the page loop and the commit is a mailbox request that cannot run until
/// the fetch has answered. Its <c>requestId</c> <i>is</i> the <c>loaderId</c>, which is what Chrome does and
/// what a client reads as "this request is the navigation" — Puppeteer decides exactly that by comparing the
/// two strings, and a <c>goto</c> builds its response object from the <c>responseReceived</c> that follows.
/// </para>
/// <para>
/// <b>Every event here is emitted from a transport thread</b>, through <c>EmitDetached</c>, which queues
/// rather than writes — so a slow client slows no request and no write failure erupts into a fetch.
/// </para>
/// <para>
/// <b>Three fields are honestly empty.</b> There is no connection pool, so <c>connectionId</c> is zero and
/// <c>connectionReused</c> false; there is no cache, so <c>fromDiskCache</c> is never true and
/// <c>requestServedFromCache</c> is never sent; and no phase of a request is timed, so <c>timing</c> is
/// absent rather than a document of zeros that would read as a page which loaded instantly.
/// </para>
/// </remarks>
internal sealed partial class NetworkDomain
{
    /// <summary>One hop is about to go out.</summary>
    internal void RequestWillBeSent(PageNetworkRequest request, string frameId)
    {
        if (!IsEnabled)
        {
            return;
        }

        var timestamp = Timestamp();

        EmitDetached(ProtocolNetworkEvents.RequestWillBeSent(new RequestWillBeSentEvent
        {
            RequestId = request.RequestId,
            LoaderId = request.LoaderId,
            DocumentURL = request.DocumentUrl,
            Request = Describe(request),
            Timestamp = timestamp,
            WallTime = WallTime(),
            Initiator = Initiator(request),
            Type = ResourceType(request.Kind),
            FrameId = frameId,
            RedirectResponse = request.RedirectResponse is { } redirect ? Describe(redirect) : null,

            // No responseReceivedExtraInfo is sent for a redirect, so a client must not wait for one.
            RedirectHasExtraInfo = false,
        }));

        EmitDetached(ProtocolNetworkEvents.RequestWillBeSentExtraInfo(new RequestWillBeSentExtraInfoEvent
        {
            RequestId = request.RequestId,
            Headers = Map(request.Headers),

            // The cookies a request carried are in its Cookie header, which is above; naming each of them
            // again with the reason it was or was not included is Chrome's own cookie-blocking bookkeeping,
            // and this browser blocks none.
            AssociatedCookies = [],
            ConnectTiming = new ConnectTiming { RequestTime = timestamp },
        }));
    }

    /// <summary>The final response's headers are in.</summary>
    internal void ResponseReceived(PageNetworkRequest request, PageNetworkResponse response, string frameId)
    {
        if (!IsEnabled)
        {
            return;
        }

        // Chrome sends the extra info first, and a client that pairs the two reads the pair in that order.
        EmitDetached(ProtocolNetworkEvents.ResponseReceivedExtraInfo(new ResponseReceivedExtraInfoEvent
        {
            RequestId = response.RequestId,
            BlockedCookies = [],
            Headers = Map(response.Headers),
            ResourceIPAddressSpace = AddressSpace(response.Url),
            StatusCode = response.Status,
        }));

        EmitDetached(ProtocolNetworkEvents.ResponseReceived(new ResponseReceivedEvent
        {
            RequestId = response.RequestId,
            LoaderId = request.LoaderId,
            Timestamp = Timestamp(),
            Type = ResourceType(request.Kind),
            Response = Describe(response),
            HasExtraInfo = true,
            FrameId = frameId,
        }));
    }

    /// <summary>Some of the body arrived.</summary>
    internal void DataReceived(string requestId, int length)
    {
        if (!IsEnabled)
        {
            return;
        }

        EmitDetached(ProtocolNetworkEvents.DataReceived(new DataReceivedEvent
        {
            RequestId = requestId,
            Timestamp = Timestamp(),
            DataLength = length,

            // Nothing here decodes a transfer encoding of its own — HttpClient does, above the point the
            // bytes are counted — so the encoded length is the decoded one rather than a number invented for
            // the field.
            EncodedDataLength = length,
        }));
    }

    /// <summary>The body has been read to its end.</summary>
    internal void LoadingFinished(string requestId, long encodedLength)
    {
        if (!IsEnabled)
        {
            return;
        }

        EmitDetached(ProtocolNetworkEvents.LoadingFinished(new LoadingFinishedEvent
        {
            RequestId = requestId,
            Timestamp = Timestamp(),
            EncodedDataLength = encodedLength,
        }));
    }

    /// <summary>The request failed instead of finishing.</summary>
    internal void LoadingFailed(string requestId, PageRequestKind kind, string errorText, bool canceled, string? blockedReason)
    {
        if (!IsEnabled)
        {
            return;
        }

        EmitDetached(ProtocolNetworkEvents.LoadingFailed(new LoadingFailedEvent
        {
            RequestId = requestId,
            Timestamp = Timestamp(),
            Type = ResourceType(kind),
            ErrorText = errorText,
            Canceled = canceled,
            BlockedReason = blockedReason,
        }));
    }

    /// <summary>One hop, as the protocol describes a request.</summary>
    private static Request Describe(PageNetworkRequest request) => new()
    {
        Url = request.Url,
        Method = request.Method,
        Headers = Map(request.Headers),
        PostData = request.PostData,
        HasPostData = request.HasPostData ? true : null,

        // Nothing here schedules by priority: a page fetches what it needs when it needs it, and every
        // request is therefore the same one. Saying High for everything would be a ranking that is not made.
        InitialPriority = ResourcePriorityValues.Medium,
        ReferrerPolicy = RequestReferrerPolicyValues.StrictOriginWhenCrossOrigin,
    };

    /// <summary>One response, as the protocol describes one.</summary>
    private static Response Describe(PageNetworkResponse response) => new()
    {
        Url = response.Url,
        Status = response.Status,
        StatusText = response.StatusText,
        Headers = Map(response.Headers),
        MimeType = response.MimeType,
        Charset = response.Charset,
        ConnectionReused = false,
        ConnectionId = 0,
        EncodedDataLength = 0,
        SecurityState = SecurityState(response.Url),
        FromDiskCache = false,
        FromServiceWorker = false,
        FromPrefetchCache = false,
    };

    /// <summary>What asked for the request, in the protocol's three words for it.</summary>
    /// <remarks>
    /// A navigation is <c>other</c>, a resource the markup referenced is <c>parser</c> and names the document
    /// it was found in, and a <c>fetch</c> or an <c>XMLHttpRequest</c> is <c>script</c>. No stack is attached
    /// to the third: capturing one would mean reaching into the engine from a transport thread, which is the
    /// one thing this seam may not do.
    /// </remarks>
    private static Initiator Initiator(PageNetworkRequest request) => request.Initiator switch
    {
        RequestInitiator.Document => new Initiator { Type = InitiatorTypeValues.Other },
        RequestInitiator.Subresource => new Initiator { Type = InitiatorTypeValues.Parser, Url = request.DocumentUrl },
        _ => new Initiator { Type = InitiatorTypeValues.Script },
    };

    /// <summary>The protocol's resource type for one request kind.</summary>
    private static string ResourceType(PageRequestKind kind) => kind switch
    {
        PageRequestKind.Document => ResourceTypeValues.Document,
        PageRequestKind.Script => ResourceTypeValues.Script,
        PageRequestKind.Stylesheet => ResourceTypeValues.Stylesheet,
        PageRequestKind.Xhr => ResourceTypeValues.XHR,
        PageRequestKind.Fetch => ResourceTypeValues.Fetch,
        PageRequestKind.Image => ResourceTypeValues.Image,
        PageRequestKind.Frame => ResourceTypeValues.Document,
        _ => ResourceTypeValues.Other,
    };

    /// <summary>
    /// The headers as the protocol carries them: a map, with a name sent more than once joined by a newline.
    /// </summary>
    /// <remarks>
    /// The Fetch Standard keeps every value of a repeated header apart and the protocol's map cannot, so the
    /// newline is the join Chrome itself uses — which is what makes several <c>Set-Cookie</c> headers legible
    /// to a client rather than one of them silently winning.
    /// </remarks>
    private static Dictionary<string, string> Map(IReadOnlyList<PageHeader> headers)
    {
        var map = new Dictionary<string, string>(headers.Count, StringComparer.OrdinalIgnoreCase);

        foreach (var header in headers)
        {
            map[header.Name] = map.TryGetValue(header.Name, out var existing)
                ? existing + "\n" + header.Value
                : header.Value;
        }

        return map;
    }

    /// <summary>Whether the URL was reached over TLS, in the protocol's own vocabulary.</summary>
    private static string SecurityState(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return "unknown";
        }

        return uri.Scheme == Uri.UriSchemeHttps ? "secure" : "insecure";
    }

    /// <summary>Which address space the response came from, as far as the URL can say.</summary>
    /// <remarks>
    /// A literal that is loopback is <c>Loopback</c> and a private one is <c>Local</c>; a name is
    /// <c>Unknown</c>, because where it resolves to is a fact that only exists inside the socket. The
    /// browser's own private-network rule is <c>BrowserContextOptions.BlockPrivateNetwork</c>, which makes
    /// the same distinction with the same caveat.
    /// </remarks>
    private static string AddressSpace(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return IPAddressSpaceValues.Unknown;
        }

        if (uri.IsLoopback)
        {
            return IPAddressSpaceValues.Loopback;
        }

        if (!System.Net.IPAddress.TryParse(uri.Host.Trim('[', ']'), out var address))
        {
            return IPAddressSpaceValues.Unknown;
        }

        return address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6UniqueLocal || IsPrivateV4(address)
            ? IPAddressSpaceValues.Local
            : IPAddressSpaceValues.Public;
    }

    private static bool IsPrivateV4(System.Net.IPAddress address)
    {
        if (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return false;
        }

        Span<byte> octets = stackalloc byte[4];
        if (!address.TryWriteBytes(octets, out _))
        {
            return false;
        }

        return octets[0] switch
        {
            10 => true,
            172 when octets[1] >= 16 && octets[1] <= 31 => true,
            192 when octets[1] == 168 => true,
            169 when octets[1] == 254 => true,
            _ => false,
        };
    }

    /// <summary>The protocol's monotonic timestamp, in seconds.</summary>
    private static double Timestamp() => DevToolsTarget.UnixMilliseconds() / 1000d;

    /// <summary>The protocol's wall-clock time, in seconds since the epoch.</summary>
    private static double WallTime() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000d;
}
