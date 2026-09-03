using System.Globalization;
using System.Net;
using Jint.Browser.Runtime;
using Jint.WebApi.Fetch;
using ProtocolNetwork = Jint.DevTools.Protocol.Network;

namespace Jint.Browser.DevTools;

/// <summary>
/// What a client asked this page's network to do, held as one immutable value so a transport thread reads a
/// coherent policy rather than half of two.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the target's, not an attachment's.</b> Two attachments both setting extra headers is last writer
/// wins, exactly as it is for <c>Emulation</c>: the page has one network, and a per-attachment policy would
/// mean a request being sent two different ways at once.
/// </para>
/// <para>
/// Every field is read from a transport thread while a request is being composed, which is why the whole
/// thing is swapped as a unit instead of being mutated in place.
/// </para>
/// </remarks>
/// <param name="ExtraHeaders">What <c>Network.setExtraHTTPHeaders</c> asked to add to every request.</param>
/// <param name="UserAgent">What <c>Network.setUserAgentOverride</c> or <c>Emulation.setUserAgentOverride</c> set, or <see langword="null"/>.</param>
/// <param name="AcceptLanguage">The <c>Accept-Language</c> that came with the user agent, or <see langword="null"/>.</param>
/// <param name="Platform">What <c>navigator.platform</c> should answer, or <see langword="null"/>.</param>
/// <param name="Offline">Whether <c>Network.emulateNetworkConditions</c> pulled the plug.</param>
/// <param name="BlockedUrls">The patterns <c>Network.setBlockedURLs</c> refuses.</param>
internal sealed record PageNetworkPolicy(
    IReadOnlyList<PageHeader> ExtraHeaders,
    string? UserAgent,
    string? AcceptLanguage,
    string? Platform,
    bool Offline,
    IReadOnlyList<string> BlockedUrls)
{
    /// <summary>A page nobody has configured: every request goes as the page composed it.</summary>
    internal static PageNetworkPolicy None { get; } = new([], null, null, null, false, []);

    /// <summary>Whether anything here would change a request that is about to be sent.</summary>
    internal bool RewritesRequests => ExtraHeaders.Count != 0 || UserAgent is not null || AcceptLanguage is not null;

    /// <summary>Whether <paramref name="url"/> is one <c>setBlockedURLs</c> refuses.</summary>
    internal bool Blocks(string url)
    {
        foreach (var pattern in BlockedUrls)
        {
            if (UrlPattern.Matches(pattern, url))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The header list one hop should carry, or <see langword="null"/> when the hop's own is already right.
    /// </summary>
    /// <remarks>
    /// A client's header wins over the one the page composed — that is what an override is — and a name the
    /// client did not mention is left exactly as the transport computed it, cookies and referrer included.
    /// </remarks>
    internal IReadOnlyList<PageHeader>? Apply(IReadOnlyList<PageHeader> headers)
    {
        if (!RewritesRequests)
        {
            return null;
        }

        var overrides = new List<PageHeader>(ExtraHeaders.Count + 2);
        overrides.AddRange(ExtraHeaders);

        if (UserAgent is { } agent)
        {
            overrides.Add(new PageHeader("user-agent", agent));
        }

        if (AcceptLanguage is { } language)
        {
            overrides.Add(new PageHeader("accept-language", language));
        }

        var merged = new List<PageHeader>(headers.Count + overrides.Count);
        foreach (var header in headers)
        {
            var replaced = false;
            foreach (var over in overrides)
            {
                if (string.Equals(header.Name, over.Name, StringComparison.OrdinalIgnoreCase))
                {
                    replaced = true;
                    break;
                }
            }

            if (!replaced)
            {
                merged.Add(header);
            }
        }

        merged.AddRange(overrides);
        return merged;
    }
}

/// <summary>
/// The protocol's own URL pattern: literal text with <c>*</c> standing for any run of characters.
/// </summary>
/// <remarks>
/// <b>Not a regular expression and not a glob.</b> Chrome's <c>Network.setBlockedURLs</c> and
/// <c>Fetch.enable</c> both take this shape and nothing else — <c>*</c> crosses <c>/</c>, <c>?</c> is a
/// literal question mark, and a pattern with no <c>*</c> must equal the URL. Matching is ordinal and
/// case-sensitive, which is what Chrome does; a host that wants more writes a <c>UrlFilter</c>.
/// </remarks>
internal static class UrlPattern
{
    /// <summary>Whether <paramref name="url"/> matches <paramref name="pattern"/>.</summary>
    internal static bool Matches(string pattern, string url)
    {
        if (pattern.Length == 0 || string.Equals(pattern, "*", StringComparison.Ordinal))
        {
            return true;
        }

        return Matches(pattern.AsSpan(), url.AsSpan());
    }

    private static bool Matches(ReadOnlySpan<char> pattern, ReadOnlySpan<char> text)
    {
        while (true)
        {
            var star = pattern.IndexOf('*');
            if (star < 0)
            {
                return pattern.SequenceEqual(text);
            }

            var prefix = pattern[..star];
            if (!text.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }

            pattern = pattern[(star + 1)..];
            text = text[prefix.Length..];

            if (pattern.IsEmpty)
            {
                return true;
            }

            // The rest of the pattern has to match somewhere further along; try every position, shortest
            // first. Patterns here are a handful of segments, so the quadratic worst case is not one.
            for (var i = 0; i <= text.Length; i++)
            {
                if (Matches(pattern, text[i..]))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

/// <summary>
/// The cookie commands, over the jar the browser context owns.
/// </summary>
/// <remarks>
/// <para>
/// <b>Setting and deleting work through any jar; reading needs the default one.</b>
/// <see cref="CookieJar"/> publishes two methods — read a <c>Cookie</c> header, store a <c>Set-Cookie</c>
/// one — so a client's <c>setCookie</c> is a <c>Set-Cookie</c> the jar stores and a <c>deleteCookies</c> is
/// the same value with an expiry in the past. Enumerating is what has no method, so
/// <c>getCookies</c>, <c>getAllCookies</c> and <c>clearBrowserCookies</c> reach into
/// <see cref="CookieContainerCookieJar.Container"/> and refuse, by name, a jar a host supplied itself. That
/// is the same honest degradation <c>document.cookie</c> already makes for <c>HttpOnly</c>.
/// </para>
/// <para>
/// <b>Every attribute the protocol carries and the container does not is dropped rather than guessed.</b>
/// <c>SameSite</c> is decided nowhere in Jint (there is no top-level site and no public suffix list),
/// priority and source scheme are Chrome's own bookkeeping, and a partition key names a partitioning model
/// this browser does not have.
/// </para>
/// </remarks>
internal static class PageCookies
{
    /// <summary>Everything the context's jar holds, or a refusal naming the jar that cannot say.</summary>
    internal static ProtocolNetwork.Cookie[] All(PageNetwork network)
    {
        var container = Container(network);
        var cookies = new List<ProtocolNetwork.Cookie>();

        lock (container)
        {
            foreach (Cookie cookie in container.GetAllCookies())
            {
                // An expired cookie is not a cookie. Expiring is also how Clear and Delete below ask a
                // container with no removal to forget one, so the filter is what makes those two visible.
                if (!cookie.Expired)
                {
                    cookies.Add(Describe(cookie));
                }
            }
        }

        return [.. cookies];
    }

    /// <summary>What the jar would send to each of <paramref name="urls"/>, with no duplicates.</summary>
    internal static ProtocolNetwork.Cookie[] For(PageNetwork network, IReadOnlyList<string> urls)
    {
        var container = Container(network);
        var cookies = new List<ProtocolNetwork.Cookie>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var url in urls)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                continue;
            }

            lock (container)
            {
                foreach (Cookie cookie in container.GetCookies(uri))
                {
                    if (!cookie.Expired && seen.Add(cookie.Domain + " " + cookie.Path + " " + cookie.Name))
                    {
                        cookies.Add(Describe(cookie));
                    }
                }
            }
        }

        return [.. cookies];
    }

    /// <summary>Stores one cookie, answering whether the jar took it.</summary>
    /// <remarks>
    /// A cookie needs a URL to be stored against — the jar's interface is "what a response to this URL
    /// said" — so a parameter set carrying neither <c>url</c> nor <c>domain</c> is refused rather than
    /// guessed at.
    /// </remarks>
    internal static bool Set(PageNetwork network, ProtocolNetwork.CookieParam cookie, string documentUrl)
    {
        var target = Target(cookie.Url, cookie.Domain, cookie.Secure == true, documentUrl);
        if (target is null)
        {
            return false;
        }

        network.CookieJar.StoreResponseCookies(target, [Header(cookie)]);
        return true;
    }

    /// <summary>Expires one cookie, which is how a jar with no removal is asked to forget.</summary>
    internal static void Delete(PageNetwork network, string name, string? url, string? domain, string? path, string documentUrl)
    {
        var target = Target(url, domain, secure: false, documentUrl);
        if (target is null)
        {
            return;
        }

        var header = name + "=; Expires=Thu, 01 Jan 1970 00:00:00 GMT; Path=" + (path is { Length: > 0 } ? path : "/");
        if (domain is { Length: > 0 })
        {
            header += "; Domain=" + domain;
        }

        network.CookieJar.StoreResponseCookies(target, [header]);
    }

    /// <summary>Empties the context's jar.</summary>
    internal static void Clear(PageNetwork network)
    {
        var container = Container(network);

        lock (container)
        {
            foreach (Cookie cookie in container.GetAllCookies())
            {
                cookie.Expired = true;
            }
        }
    }

    /// <summary>The <c>Set-Cookie</c> value one parameter set amounts to.</summary>
    private static string Header(ProtocolNetwork.CookieParam cookie)
    {
        var header = new System.Text.StringBuilder();
        header.Append(cookie.Name).Append('=').Append(cookie.Value);

        if (cookie.Domain is { Length: > 0 } domain)
        {
            header.Append("; Domain=").Append(domain);
        }

        if (cookie.Path is { Length: > 0 } path)
        {
            header.Append("; Path=").Append(path);
        }

        if (cookie.Expires is { } expires && expires > 0)
        {
            header.Append("; Expires=").Append(
                DateTimeOffset.FromUnixTimeMilliseconds((long) (expires * 1000)).UtcDateTime.ToString("R", CultureInfo.InvariantCulture));
        }

        if (cookie.Secure == true)
        {
            header.Append("; Secure");
        }

        if (cookie.HttpOnly == true)
        {
            header.Append("; HttpOnly");
        }

        return header.ToString();
    }

    /// <summary>The URL a cookie is stored against: the client's, the domain it named, or the document's.</summary>
    private static Uri? Target(string? url, string? domain, bool secure, string documentUrl)
    {
        if (url is { Length: > 0 } && Uri.TryCreate(url, UriKind.Absolute, out var fromClient))
        {
            return fromClient;
        }

        if (domain is { Length: > 0 })
        {
            var scheme = secure ? "https" : "http";
            return Uri.TryCreate(scheme + "://" + domain.TrimStart('.') + "/", UriKind.Absolute, out var fromDomain) ? fromDomain : null;
        }

        return Uri.TryCreate(documentUrl, UriKind.Absolute, out var fromDocument)
            && (fromDocument.Scheme == Uri.UriSchemeHttp || fromDocument.Scheme == Uri.UriSchemeHttps)
            ? fromDocument
            : null;
    }

    private static ProtocolNetwork.Cookie Describe(Cookie cookie) => new()
    {
        Name = cookie.Name,
        Value = cookie.Value,
        Domain = cookie.Domain,
        Path = cookie.Path,
        Expires = cookie.Expires == DateTime.MinValue ? -1 : new DateTimeOffset(cookie.Expires.ToUniversalTime()).ToUnixTimeMilliseconds() / 1000d,
        Size = cookie.Name.Length + cookie.Value.Length,
        HttpOnly = cookie.HttpOnly,
        Secure = cookie.Secure,
        Session = cookie.Expires == DateTime.MinValue,
        Priority = ProtocolNetwork.CookiePriorityValues.Medium,
        SourceScheme = cookie.Secure ? ProtocolNetwork.CookieSourceSchemeValues.Secure : ProtocolNetwork.CookieSourceSchemeValues.NonSecure,

        // -1 is the protocol's own "unspecified": the jar records no port, and a cookie is port-insensitive
        // anyway, so answering 80 or 443 would be inventing a fact.
        SourcePort = -1,
    };

    /// <summary>
    /// The container behind the context's jar, or a refusal naming the jar that has no enumeration.
    /// </summary>
    private static CookieContainer Container(PageNetwork network)
    {
        if (network.CookieJar is CookieContainerCookieJar jar)
        {
            return jar.Container;
        }

        return Jint.DevTools.Throw.ServerError<CookieContainer>(
            "Cookies cannot be enumerated",
            "the browser context was given a CookieJar of its own, and that surface publishes reading a Cookie header and storing a Set-Cookie one but not listing what it holds; use the default CookieContainerCookieJar to make this command answerable");
    }
}
