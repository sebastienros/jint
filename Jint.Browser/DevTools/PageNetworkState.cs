using System.Globalization;
using System.Net;
using Jint.Browser.Runtime;
using Jint.WebApi.Fetch;
using Jint.WebApi.Url.Parsing;
using Jint.WebApi.Url.Pattern;
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
/// <para>
/// <b>The user agent is deliberately not here.</b> <c>Network.setUserAgentOverride</c> and
/// <c>Emulation.setUserAgentOverride</c> are one setting in Chrome and one here too, and the page's
/// <c>Runtime/EmulationState</c> is where it lives — because <c>navigator.userAgent</c> has to answer the
/// same string this puts on every request, and a page whose host never attached a client still has one. So
/// <see cref="Apply"/> is handed that state rather than keeping a copy of it.
/// </para>
/// <param name="ExtraHeaders">What <c>Network.setExtraHTTPHeaders</c> asked to add to every request.</param>
/// <param name="Conditions">
/// The network conditions in force, in the order a client listed them; <see cref="IsOffline"/> reads them.
/// <c>Network.emulateNetworkConditions</c> and <c>Network.emulateNetworkConditionsByRule</c> both write the
/// whole list and each replaces what the other set, so the last one sent decides. What <c>navigator.onLine</c>
/// answers is the page's <c>EmulationState.Offline</c> instead.
/// </param>
/// <param name="Blocked">What <c>Network.setBlockedURLs</c> refuses; <see cref="Blocks"/> reads it.</param>
internal sealed record PageNetworkPolicy(
    IReadOnlyList<PageHeader> ExtraHeaders,
    IReadOnlyList<PageNetworkCondition> Conditions,
    PageBlockedUrls Blocked)
{
    /// <summary>A page nobody has configured: every request goes as the page composed it.</summary>
    internal static PageNetworkPolicy None { get; } = new([], [], PageBlockedUrls.None);

    /// <summary>Whether the network conditions in force take <paramref name="url"/> offline.</summary>
    /// <remarks>
    /// <para>
    /// <b>The first entry that matches decides</b>, offline or not, and a URL no entry matches is online. That
    /// is the protocol's rule, and Chromium's network service keeps it exactly: it mints a distinct rule
    /// identifier per entry, so two entries with the same conditions are never merged and their order is never
    /// lost. A global entry matches every URL, so nothing after one is ever read.
    /// </para>
    /// <para>
    /// <b>Called on a transport thread, once per redirect hop</b>, so a redirect into a pattern is refused at the
    /// hop that enters it. Nothing here reaches an engine: a pattern is a <see cref="UrlPatternMatcher"/>, and the
    /// URL is parsed at most once, and only when a pattern has to read it.
    /// </para>
    /// </remarks>
    internal bool IsOffline(string url)
    {
        UrlRecord? parsed = null;
        var unparsed = true;

        foreach (var condition in Conditions)
        {
            if (condition.Pattern is not { } pattern)
            {
                return condition.Offline;
            }

            if (unparsed)
            {
                parsed = UrlParser.Parse(url);
                unparsed = false;
            }

            // A URL that does not parse matches no pattern; only a global entry can reach it.
            if (parsed is not null && pattern.Matches(parsed))
            {
                return condition.Offline;
            }
        }

        return false;
    }

    /// <summary>Whether <paramref name="url"/> is one <c>setBlockedURLs</c> refuses.</summary>
    internal bool Blocks(string url) => Blocked.Blocks(url);

    /// <summary>
    /// The header list one hop should carry, or <see langword="null"/> when the hop's own is already right.
    /// </summary>
    /// <remarks>
    /// A client's header wins over the one the page composed — that is what an override is — and a name the
    /// client did not mention is left exactly as the transport computed it, cookies and referrer included.
    /// </remarks>
    internal IReadOnlyList<PageHeader>? Apply(IReadOnlyList<PageHeader> headers, EmulationState emulation)
    {
        var agent = emulation.UserAgent;
        var language = emulation.AcceptLanguage;

        if (ExtraHeaders.Count == 0 && agent is null && language is null && !emulation.CacheDisabled)
        {
            return null;
        }

        var overrides = new List<PageHeader>(ExtraHeaders.Count + 2);
        overrides.AddRange(ExtraHeaders);

        if (agent is not null)
        {
            overrides.Add(new PageHeader("user-agent", agent));
        }

        if (language is not null)
        {
            overrides.Add(new PageHeader("accept-language", language));
        }

        if (emulation.CacheDisabled)
        {
            overrides.RemoveAll(h => h.Name.Equals("cache-control", StringComparison.OrdinalIgnoreCase));
            overrides.Add(new PageHeader("cache-control", "no-store"));
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

    internal static IReadOnlyList<PageHeader> BypassCache(IReadOnlyList<PageHeader> headers)
    {
        var result = new List<PageHeader>(headers.Count + 1);
        foreach (var header in headers)
            if (!header.Name.Equals("cache-control", StringComparison.OrdinalIgnoreCase)) result.Add(header);
        result.Add(new PageHeader("cache-control", "no-store"));
        return result;
    }
}

/// <summary>One entry of the network conditions in force: the requests it applies to, and whether it takes them offline.</summary>
/// <remarks>
/// Latency, throughput and the WebRTC packet settings are not kept, because nothing honours them — see
/// <c>NetworkDomain.EmulateNetworkConditionsAsync</c>. An entry that only throttles is still kept, as one
/// whose <see cref="Offline"/> is <see langword="false"/>: being the first to match is what lets it keep a
/// request online that a later entry would have taken offline.
/// </remarks>
/// <param name="Pattern">
/// The requests the entry applies to, compiled on the page loop from the entry's <c>urlPattern</c> and matched on a
/// transport thread; <see langword="null"/> for a global entry, which applies to every request.
/// </param>
/// <param name="Offline">Whether a request this entry decides fails with <c>net::ERR_INTERNET_DISCONNECTED</c>.</param>
internal sealed record PageNetworkCondition(UrlPatternMatcher? Pattern, bool Offline)
{
    /// <summary>The single global entry the deprecated <c>Network.emulateNetworkConditions</c> amounts to when offline.</summary>
    internal static PageNetworkCondition GlobalOffline { get; } = new(null, true);
}

/// <summary>
/// What <c>Network.setBlockedURLs</c> asked to refuse: URLPattern entries, read first, and the deprecated globs
/// beside them — https://chromedevtools.github.io/devtools-protocol/tot/Network/#method-setBlockedURLs.
/// </summary>
/// <remarks>
/// <para>
/// <b>Chromium's renderer is the reference</b>, <c>InspectorNetworkAgent::ShouldBlockRequest</c> in
/// <c>third_party/blink/renderer/core/inspector/inspector_network_agent.cc</c>. The first pattern that matches
/// decides, and a <c>block: false</c> one lets the request through whatever comes after it, globs included; only
/// a URL no pattern matches is searched for a glob, and any glob that matches blocks it.
/// </para>
/// <para>
/// <b>Read on a transport thread, once per redirect hop</b>, which is why it holds nothing of an engine: each
/// pattern is a <see cref="UrlPatternMatcher"/> compiled on the page loop, each glob a string, and the URL is
/// parsed at most once, and only when there is a pattern to read it.
/// </para>
/// </remarks>
/// <param name="Patterns">The <c>urlPatterns</c> entries, in the order a client listed them.</param>
/// <param name="Urls">The deprecated <c>urls</c>, each one read as a <see cref="BlockedUrlGlob"/>.</param>
internal sealed record PageBlockedUrls(IReadOnlyList<PageBlockPattern> Patterns, IReadOnlyList<string> Urls)
{
    /// <summary>Nothing blocked, which is also what a command naming neither member sets.</summary>
    internal static PageBlockedUrls None { get; } = new([], []);

    /// <summary>Whether a request for <paramref name="url"/> is refused.</summary>
    internal bool Blocks(string url)
    {
        // A URL that does not parse matches no pattern, and is left to the globs.
        if (Patterns.Count > 0 && UrlParser.Parse(url) is { } parsed)
        {
            foreach (var pattern in Patterns)
            {
                if (pattern.Pattern.Matches(parsed))
                {
                    return pattern.Block;
                }
            }
        }

        foreach (var glob in Urls)
        {
            if (BlockedUrlGlob.Matches(glob, url))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>One <c>Network.BlockPattern</c>: the requests it matches, and whether it blocks or allows them.</summary>
/// <param name="Pattern">The entry's <c>urlPattern</c>, compiled on the page loop and matched on a transport thread.</param>
/// <param name="Block">
/// Whether a request this entry decides is refused; <see langword="false"/> lets it through even when a later entry
/// or a glob would have refused it.
/// </param>
internal sealed record PageBlockPattern(UrlPatternMatcher Pattern, bool Block);

/// <summary>
/// The glob <c>Network.setBlockedURLs</c>' deprecated <c>urls</c> member is read with: literal text, <c>*</c> for
/// any run of characters, and an implicit <c>*</c> at either end.
/// </summary>
/// <remarks>
/// Chromium's own <c>Matches</c> in <c>inspector_network_agent.cc</c>, step for step: the pieces between the stars
/// are found in the URL in order, each after the end of the last, and nothing anchors the first or the last — so
/// <c>tracker.js</c> blocks every URL containing it, and an empty glob or a lone <c>*</c> blocks everything. The
/// search is ordinal and case-sensitive over the URL's serialization. Taking the leftmost occurrence of each piece
/// is what makes one pass enough: it leaves the most of the URL for the pieces after it.
/// </remarks>
internal static class BlockedUrlGlob
{
    /// <summary>Whether <paramref name="url"/> matches <paramref name="glob"/>.</summary>
    internal static bool Matches(string glob, string url)
    {
        var pieces = glob.AsSpan();
        var rest = url.AsSpan();

        while (true)
        {
            var star = pieces.IndexOf('*');
            var piece = star < 0 ? pieces : pieces[..star];

            if (!piece.IsEmpty)
            {
                var at = rest.IndexOf(piece);
                if (at < 0)
                {
                    return false;
                }

                rest = rest[(at + piece.Length)..];
            }

            if (star < 0)
            {
                return true;
            }

            pieces = pieces[(star + 1)..];
        }
    }
}

/// <summary>
/// <c>Fetch.enable</c>'s <c>urlPattern</c>, read as Chromium reads it: <c>*</c> for any run of characters,
/// <c>?</c> for at most one, a backslash making the next character literal, and the whole URL covered except its
/// fragment — https://chromedevtools.github.io/devtools-protocol/tot/Fetch/#type-RequestPattern.
/// </summary>
/// <remarks>
/// <para>
/// <b>Chromium is the reference, step for step.</b> <c>FetchHandler</c>'s <c>ToInterceptionPatterns</c>
/// (<c>content/browser/devtools/protocol/fetch_handler.cc</c>) puts <c>*</c> in place of an <i>omitted</i>
/// <c>urlPattern</c> only, which is the domain's to do; an empty one stays a pattern, and it matches no URL.
/// <c>DevToolsURLLoaderInterceptor::GetInterceptionStages</c>
/// (<c>content/browser/devtools/devtools_url_loader_interceptor.cc</c>) takes the fragment off with
/// <c>NetworkHandler::ExtractFragment</c> and hands the rest to <c>Pattern::Matches</c>, which is
/// <c>base::MatchPattern</c> (<c>base/strings/pattern.cc</c>). That function is what is ported below, one
/// method for each of its own.
/// </para>
/// <para>
/// <b>Where the protocol's description and the browser part, this follows the browser.</b> The description says
/// <c>?</c> is "exactly one" character, and <c>base::MatchPattern</c> takes it as zero or one. And each piece
/// between wildcards is taken at the first place the wildcards before it can reach, never to be reconsidered,
/// so <c>/?a?b</c> does not match <c>/aaab</c> though a backtracking matcher would say it does. A client written
/// against Chrome expects the requests Chrome pauses, no more and no fewer.
/// </para>
/// <para>
/// Matching is ordinal and case-sensitive, a wildcard counts code points rather than UTF-16 units, and the search
/// is a loop with no recursion, so no pattern costs more than the URL's length times its own. It runs on a
/// transport thread once per hop and holds nothing. <c>Network.setBlockedURLs</c> reads neither this nor anything
/// anchored: its <c>urlPatterns</c> are URLPattern syntax, and its deprecated <c>urls</c> a
/// <see cref="BlockedUrlGlob"/>.
/// </para>
/// </remarks>
internal static class FetchUrlGlob
{
    /// <summary>What <c>CBU_SENTINEL</c> stands for: a unit that decodes to no code point, and so equals nothing.</summary>
    private const int Sentinel = -1;

    /// <summary>Whether a request for <paramref name="url"/> matches <paramref name="pattern"/>.</summary>
    internal static bool Matches(string pattern, string url)
    {
        // An absolute URL's serialization has no '#' but the one that starts its fragment.
        var hash = url.IndexOf('#');
        var text = hash < 0 ? url.AsSpan() : url.AsSpan(0, hash);

        return MatchPattern(text, pattern.AsSpan());
    }

    /// <summary><c>MatchPatternT</c>: each run of wildcards, then the piece after it, until the pattern ends.</summary>
    private static bool MatchPattern(ReadOnlySpan<char> text, ReadOnlySpan<char> pattern)
    {
        var p = 0;
        var t = 0;

        do
        {
            var reach = EatWildcards(pattern, ref p);
            if (!SearchForChars(pattern, ref p, text, ref t, reach))
            {
                return false;
            }
        }
        while (p < pattern.Length);

        return true;
    }

    /// <summary>
    /// <c>EatWildcards</c>: consumes a run of <c>?</c> and <c>*</c>, and answers how many code points the piece after
    /// it may start from here — one per <c>?</c>, or <c>-1</c>, unbounded, once the run holds a <c>*</c>.
    /// </summary>
    private static int EatWildcards(ReadOnlySpan<char> pattern, ref int p)
    {
        var questionMarks = 0;
        var asterisk = false;

        for (; p < pattern.Length; p++)
        {
            if (pattern[p] == '?')
            {
                questionMarks++;
            }
            else if (pattern[p] == '*')
            {
                asterisk = true;
            }
            else
            {
                break;
            }
        }

        return asterisk ? -1 : questionMarks;
    }

    /// <summary>
    /// <c>SearchForChars</c>: finds the piece at <paramref name="p"/> — up to the next unescaped wildcard, or the end
    /// of the pattern, which must then meet the end of the URL — starting at most <paramref name="reach"/> code
    /// points past <paramref name="t"/>, and leaves both positions after it.
    /// </summary>
    /// <remarks>
    /// The leftmost start that fits wins, and the caller never comes back to try another; that is Chromium's
    /// search, and what makes one pass enough.
    /// </remarks>
    private static bool SearchForChars(ReadOnlySpan<char> pattern, ref int p, ReadOnlySpan<char> text, ref int t, int reach)
    {
        var pieceStart = p;
        var searchStart = t;

        // Outside the loop, where Chromium declares it: a retry keeps it, which only a piece ending in a lone
        // backslash can ever notice.
        var escape = false;

        while (true)
        {
            if (p == pattern.Length)
            {
                if (t == text.Length)
                {
                    return true;
                }
            }
            else
            {
                if (!escape && pattern[p] is '*' or '?')
                {
                    return true;
                }

                if (!escape && pattern[p] == '\\')
                {
                    escape = true;
                    p++;
                    continue;
                }

                escape = false;

                if (t == text.Length)
                {
                    return false;
                }

                var expected = Next(pattern, p, out var patternLength);
                if (expected != Sentinel && expected == Next(text, t, out var textLength))
                {
                    p += patternLength;
                    t += textLength;
                    continue;
                }
            }

            // A mismatch: start the piece again one code point further along, if the wildcards reach that far.
            if (reach == 0)
            {
                return false;
            }

            if (reach > 0)
            {
                reach--;
            }

            p = pieceStart;
            Next(text, searchStart, out var step);
            searchStart += step;
            t = searchStart;
        }
    }

    /// <summary>The code point at <paramref name="at"/>, or <see cref="Sentinel"/> for a lone surrogate.</summary>
    private static int Next(ReadOnlySpan<char> text, int at, out int length)
    {
        var c = text[at];
        if (!char.IsSurrogate(c))
        {
            length = 1;
            return c;
        }

        return System.Text.Rune.DecodeFromUtf16(text[at..], out var rune, out length) == System.Buffers.OperationStatus.Done
            ? rune.Value
            : Sentinel;
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
