using System.Globalization;
using System.Text;
using Jint.Browser.Dom;
using Jint.HtmlParser;
using Jint.WebApi.Fetch;

namespace Jint.Browser.Runtime;

/// <summary>
/// The shared non-HTTP cookie door: https://cookiestore.spec.whatwg.org/#query-cookies and
/// https://html.spec.whatwg.org/multipage/dom.html#dom-document-cookie.
/// </summary>
internal static class ScriptCookies
{
    internal static Uri? DocumentUri(Document document)
    {
        var state = DomDocumentState.Of(document);
        return !state.Origin.IsOpaque && HttpUri(state.Url) is { } uri ? uri : null;
    }

    internal static Uri? HttpUri(string url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" ? uri : null;

    internal static List<SetCookie> Read(CookieJar jar, Uri uri)
    {
        if (jar is CookieContainerCookieJar container)
        {
            return container.GetScriptCookies(uri);
        }

        // A custom jar exposes only the wire header, not HttpOnly or cookie identity/attributes.
        var result = new List<SetCookie>();
        foreach (var pair in (jar.GetCookieHeader(uri) ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var equals = pair.IndexOf('=');
            result.Add(new SetCookie
            {
                Name = equals < 0 ? "" : pair[..equals].Trim(' ', '\t'),
                Value = (equals < 0 ? pair : pair[(equals + 1)..]).Trim(' ', '\t'),
            });
        }
        return result;
    }

    internal static void Write(CookieJar jar, Uri uri, SetCookie cookie, string header)
    {
        if (jar is CookieContainerCookieJar container)
        {
            container.StoreScriptCookie(uri, cookie);
        }
        else
        {
            jar.StoreResponseCookies(uri, [header]);
        }
    }

    /// <summary>Serializes only for custom jars; the default jar receives the structured attributes directly.</summary>
    internal static string Serialize(SetCookie cookie, string sameSite, bool partitioned)
    {
        var builder = new StringBuilder().Append(cookie.Name).Append('=').Append(cookie.Value);
        if (cookie.Domain is { } domain) builder.Append("; Domain=").Append(domain);
        if (cookie.Path is { } path) builder.Append("; Path=").Append(path);
        if (cookie.Expires is { } expires) builder.Append("; Expires=").Append(expires.ToString("R", CultureInfo.InvariantCulture));
        if (cookie.Secure) builder.Append("; Secure");
        builder.Append("; SameSite=").Append(sameSite);
        if (partitioned) builder.Append("; Partitioned");
        return builder.ToString();
    }
}
