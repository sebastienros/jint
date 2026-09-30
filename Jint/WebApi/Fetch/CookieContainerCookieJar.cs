#if NET8_0_OR_GREATER
using System.Net;

namespace Jint.WebApi.Fetch;

/// <summary>
/// A <see cref="CookieJar"/> over <see cref="System.Net.CookieContainer"/>, with the <c>Set-Cookie</c>
/// parsing done by Jint rather than by the container.
/// </summary>
/// <remarks>
/// <para>
/// <b>One jar is one cookie partition.</b> Give each tenant, session or page its own — a jar shared between
/// two engines is a channel between them, which is why nothing creates one by default and why the
/// <c>HttpClientHandler</c> Jint uses still has <c>UseCookies</c> off.
/// </para>
/// <para>
/// <b>Every call takes the jar's own lock</b>, because a redirect chain reaches it from whichever thread the
/// HTTP stack completed the previous hop on and <see cref="System.Net.CookieContainer"/> is not documented as
/// thread-safe.
/// </para>
/// <para>
/// <b>The container's caps still apply</b> — 300 cookies, 20 per domain, 4096 bytes each by default, with
/// the oldest evicted past that. Raise them on a container of your own and pass it in.
/// </para>
/// <para>
/// Where this diverges from RFC 6265bis, and why, is in
/// <see href="https://github.com/sebastienros/jint/blob/main/Jint/WebApi/AGENTS.md">the web-API instructions</see>.
/// </para>
/// </remarks>
public sealed class CookieContainerCookieJar : CookieJar
{
    private readonly CookieContainer _container;
    private readonly System.Threading.Lock _lock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="CookieContainerCookieJar"/> class over a fresh, empty
    /// container.
    /// </summary>
    public CookieContainerCookieJar() : this(new CookieContainer())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CookieContainerCookieJar"/> class over a container the
    /// caller owns.
    /// </summary>
    /// <param name="container">The store, which may already hold cookies and whose caps the caller has set.</param>
    public CookieContainerCookieJar(CookieContainer container)
    {
        _container = container ?? throw new ArgumentNullException(nameof(container));
    }

    /// <summary>
    /// Gets the underlying container, so a host can seed it, read it back, or persist it between engines.
    /// </summary>
    /// <remarks>
    /// Reached without this jar's lock, so touch it while no fetch is in flight — or take
    /// <c>lock (jar.Container)</c> in your own code and accept that Jint does not.
    /// </remarks>
    public CookieContainer Container => _container;

    /// <inheritdoc />
    public override string? GetCookieHeader(Uri url)
    {
        if (url is null)
        {
            return null;
        }

        lock (_lock)
        {
            return _container.GetCookieHeader(RetrievalUri(url));
        }
    }

    /// <inheritdoc />
    public override void StoreResponseCookies(Uri url, IReadOnlyList<string> setCookieHeaders)
    {
        if (url is null || setCookieHeaders is null)
        {
            return;
        }

        for (var i = 0; i < setCookieHeaders.Count; i++)
        {
            if (!SetCookieParser.TryParse(setCookieHeaders[i], out var parsed) || parsed is null)
            {
                continue;
            }

            lock (_lock)
            {
                StoreParsed(url, parsed);
            }
        }
    }

    /// <summary>Copies script-visible cookie state while holding the same lock as response storage.</summary>
    internal List<SetCookie> GetScriptCookies(Uri url)
    {
        lock (_lock)
        {
            var result = new List<SetCookie>();
            foreach (Cookie cookie in _container.GetCookies(RetrievalUri(url)))
            {
                // The container hands out its live cookies, which a host may expire without this lock
                // (Cookie.Expired = true, as CDP's clearBrowserCookies does): read the expiry once and treat a
                // past one as the deletion it is, never as a change to a cookie that still exists.
                var expires = cookie.Expires;
                if (!cookie.HttpOnly && (expires == DateTime.MinValue || expires.ToUniversalTime() > DateTime.UtcNow))
                {
                    result.Add(new SetCookie
                    {
                        Name = cookie.Name,
                        Value = cookie.Value,
                        Domain = cookie.Domain,
                        Path = cookie.Path,
                        Secure = cookie.Secure,
                        Expires = expires == DateTime.MinValue ? null : new DateTimeOffset(expires.ToUniversalTime()),
                    });
                }
            }
            return result;
        }
    }

    /// <summary>
    /// https://cookiestore.spec.whatwg.org/#set-a-cookie - stores through the non-HTTP door atomically.
    /// </summary>
    internal void StoreScriptCookie(Uri url, SetCookie parsed)
    {
        lock (_lock)
        {
            if (parsed.HttpOnly)
            {
                return;
            }

            // Include paths not visible at the document URL: a script can write a different Path.
            var domain = parsed.Domain ?? url.Host;
            var path = parsed.Path ?? DefaultPath(url);
            foreach (Cookie cookie in _container.GetAllCookies())
            {
                if (cookie.HttpOnly && string.Equals(cookie.Name, parsed.Name, StringComparison.Ordinal)
                    && string.Equals(cookie.Domain.TrimStart('.'), domain.TrimStart('.'), StringComparison.OrdinalIgnoreCase)
                    && string.Equals(cookie.Path, path, StringComparison.Ordinal))
                {
                    return;
                }
            }

            StoreParsed(url, parsed);
        }
    }

    /// <summary>RFC 6265bis default-path, shared by the two script cookie APIs.</summary>
    internal static string DefaultPath(Uri url)
    {
        var path = url.AbsolutePath;
        var slash = path.LastIndexOf('/');
        return slash <= 0 ? "/" : path[..slash];
    }

    /// <summary>
    /// RFC 6265bis allows trusted localhost connections to carry Secure cookies, as browsers do.
    /// Only the container's retrieval uses HTTPS; the request's URL and transport are unchanged.
    /// </summary>
    private static Uri RetrievalUri(Uri url)
        => string.Equals(url.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal) && url.IsLoopback
            ? new UriBuilder(url) { Scheme = Uri.UriSchemeHttps }.Uri : url;

    private void StoreParsed(Uri url, SetCookie parsed)
    {
        try
        {
            var cookie = new Cookie(parsed.Name, parsed.Value)
            {
                Secure = parsed.Secure,
                HttpOnly = parsed.HttpOnly,
            };

            // Assigned only when the header carried them, because assigning either at all — even the empty
            // string — clears the container's "implicit" flag for it. A host-only cookie is one whose Domain
            // was never set, and a cookie with no Path attribute is one the container gives the URL's
            // default-path; writing string.Empty instead makes both an explicitly empty value, which the
            // container then refuses outright.
            if (parsed.Domain is { } domain)
            {
                cookie.Domain = domain;
            }

            if (parsed.Path is { } path)
            {
                cookie.Path = path;
            }

            if (parsed.Expires is { } expires)
            {
                // A past date is how a server deletes a cookie, and adding one is how the container removes
                // the entry it replaces.
                cookie.Expires = expires.UtcDateTime;
            }

            _container.Add(url, cookie);
        }
        catch (CookieException)
        {
            // RFC 6265bis storage refusals are silent, including the container's stricter grammar.
        }
        catch (ArgumentException)
        {
            // The container reports some storage refusals as argument failures instead.
        }
    }
}
#endif
