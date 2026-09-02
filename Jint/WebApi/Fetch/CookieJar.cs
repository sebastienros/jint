#if NET8_0_OR_GREATER
namespace Jint.WebApi.Fetch;

/// <summary>
/// The cookie store <c>fetch</c> reads a <c>Cookie</c> header from and writes a response's <c>Set-Cookie</c>
/// headers back to.
/// </summary>
/// <remarks>
/// <para>
/// <b>Both methods are called from transport threads and must never touch the <see cref="Engine"/>.</b> They
/// run on whichever thread the HTTP stack completed a hop on, while the script that started the fetch is
/// still running; they must be thread-safe and must not block.
/// </para>
/// <para>
/// <b>Consulted once per redirect hop, not once per fetch.</b> The <c>Cookie</c> header is recomputed for
/// each hop's URL and every response's <c>Set-Cookie</c> headers are stored, including a redirect's — which
/// is what makes a login that answers <c>302</c> with a session cookie work.
/// </para>
/// <para>
/// <b>Whether a jar is consulted at all is the request's <c>credentials</c> mode.</b> <c>omit</c> neither
/// sends nor stores, <c>same-origin</c> does both only while the hop is same-origin with
/// <c>Options.WebApi.Fetch.Origin</c>, and <c>include</c> always does.
/// </para>
/// <para>
/// <b>Same-site is not decided here.</b> Jint has no top-level site and no public suffix list, so a
/// <c>SameSite</c> attribute cannot be enforced against anything; a host that does know its own browsing
/// context enforces it inside its own jar.
/// </para>
/// </remarks>
public abstract class CookieJar
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CookieJar"/> class.
    /// </summary>
    protected CookieJar()
    {
    }

    /// <summary>
    /// Returns the <c>Cookie</c> header value to send to <paramref name="url"/>, or <see langword="null"/>
    /// when this jar has none.
    /// </summary>
    /// <param name="url">The absolute URL of the hop about to be requested.</param>
    /// <remarks>
    /// The value is a <c>name=value</c> list joined with <c>"; "</c>, as
    /// https://httpwg.org/http-extensions/draft-ietf-httpbis-rfc6265bis.html#name-the-cookie-header-field
    /// defines it. An empty string is treated as no header, so a jar may answer either.
    /// </remarks>
    public abstract string? GetCookieHeader(Uri url);

    /// <summary>
    /// Stores the <c>Set-Cookie</c> header values a response to <paramref name="url"/> carried.
    /// </summary>
    /// <param name="url">The absolute URL that produced the response.</param>
    /// <param name="setCookieHeaders">One entry per <c>Set-Cookie</c> header, never combined into one.</param>
    /// <remarks>
    /// Called for every response, a redirect's included, and never with an empty list. A value this jar
    /// cannot parse is the jar's own business to ignore: a malformed <c>Set-Cookie</c> is not a failed fetch.
    /// </remarks>
    public abstract void StoreResponseCookies(Uri url, IReadOnlyList<string> setCookieHeaders);
}
#endif
