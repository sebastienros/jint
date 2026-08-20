#if NET8_0_OR_GREATER
namespace Jint.WebApi.Url.Parsing;

/// <summary>
/// The setter halves of the <c>URL</c> class's attributes, https://url.spec.whatwg.org/#url-class — the part
/// of each that touches only the URL record.
/// </summary>
/// <remarks>
/// <para>
/// They live beside the parser rather than in the JavaScript binding for two reasons. Every one of them is
/// defined as "basic URL parse the given value with this's URL as url and <i>some</i> state as state override",
/// so they are parser algorithms wearing an API hat; and keeping them engine-free is what lets the Web
/// Platform Tests setter corpus run against them directly, one record per row and no engine at all.
/// </para>
/// <para>
/// Each takes the value <b>after</b> the USVString conversion, which is what the JavaScript binding does at its
/// boundary and what the parser folds into its own input preparation.
/// </para>
/// <para>
/// A setter whose parse fails leaves whatever the parser already committed in place. That is the specified
/// behaviour, not a missing rollback: WPT's <c>url.host = "example.com:65536"</c> row is titled "Port numbers
/// are 16 bit integers, overflowing is an error. Hostname is still set, though."
/// </para>
/// </remarks>
internal static class UrlSetters
{
    /// <summary>https://url.spec.whatwg.org/#dom-url-protocol</summary>
    internal static void SetProtocol(UrlRecord url, string value)
    {
        // Failure is deliberately ignored: only the Location object's protocol setter observes it.
        UrlParser.ParseInto(value + ":", url, UrlParserState.SchemeStart);
    }

    /// <summary>https://url.spec.whatwg.org/#dom-url-username and https://url.spec.whatwg.org/#set-the-username</summary>
    internal static void SetUsername(UrlRecord url, string value)
    {
        if (url.CannotHaveCredentialsOrPort)
        {
            return;
        }

        url.Username = PercentEncoding.Encode(value.AsSpan(), PercentEncodeSet.Userinfo);
    }

    /// <summary>https://url.spec.whatwg.org/#dom-url-password and https://url.spec.whatwg.org/#set-the-password</summary>
    internal static void SetPassword(UrlRecord url, string value)
    {
        if (url.CannotHaveCredentialsOrPort)
        {
            return;
        }

        url.Password = PercentEncoding.Encode(value.AsSpan(), PercentEncodeSet.Userinfo);
    }

    /// <summary>https://url.spec.whatwg.org/#dom-url-host</summary>
    internal static void SetHost(UrlRecord url, string value)
    {
        if (url.HasOpaquePath)
        {
            return;
        }

        UrlParser.ParseInto(value, url, UrlParserState.Host);
    }

    /// <summary>https://url.spec.whatwg.org/#dom-url-hostname</summary>
    internal static void SetHostname(UrlRecord url, string value)
    {
        if (url.HasOpaquePath)
        {
            return;
        }

        UrlParser.ParseInto(value, url, UrlParserState.Hostname);
    }

    /// <summary>https://url.spec.whatwg.org/#dom-url-port</summary>
    internal static void SetPort(UrlRecord url, string value)
    {
        if (url.CannotHaveCredentialsOrPort)
        {
            return;
        }

        if (value.Length == 0)
        {
            url.Port = null;
            return;
        }

        UrlParser.ParseInto(value, url, UrlParserState.Port);
    }

    /// <summary>https://url.spec.whatwg.org/#dom-url-pathname</summary>
    internal static void SetPathname(UrlRecord url, string value)
    {
        if (url.HasOpaquePath)
        {
            return;
        }

        url.Path.Clear();
        UrlParser.ParseInto(value, url, UrlParserState.PathStart);
    }

    /// <summary>
    /// https://url.spec.whatwg.org/#dom-url-search. Returns the list the caller's query object must take on,
    /// which is empty exactly when the query is cleared.
    /// </summary>
    internal static List<FormUrlEncodedEntry> SetSearch(UrlRecord url, string value)
    {
        if (value.Length == 0)
        {
            url.Query = null;
            return [];
        }

        var input = value[0] == '?' ? value.Substring(1) : value;
        url.Query = string.Empty;
        UrlParser.ParseInto(input, url, UrlParserState.Query);
        return FormUrlEncoded.Parse(input);
    }

    /// <summary>https://url.spec.whatwg.org/#dom-url-hash</summary>
    internal static void SetHash(UrlRecord url, string value)
    {
        if (value.Length == 0)
        {
            url.Fragment = null;
            return;
        }

        var input = value[0] == '#' ? value.Substring(1) : value;
        url.Fragment = string.Empty;
        UrlParser.ParseInto(input, url, UrlParserState.Fragment);
    }
}
#endif
