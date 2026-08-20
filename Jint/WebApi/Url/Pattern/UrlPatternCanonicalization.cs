#if NET8_0_OR_GREATER
using System.Text;
using Jint.Runtime;
using Jint.WebApi.Url.Parsing;

namespace Jint.WebApi.Url.Pattern;

/// <summary>
/// The encoding callbacks of https://urlpattern.spec.whatwg.org/#canon-encoding-callbacks — one per URL
/// component, each canonicalizing a piece of fixed text the way the URL parser would canonicalize that component.
/// </summary>
/// <remarks>
/// <para>
/// Every one of them is defined in terms of the URL Standard's own parser, run over a throwaway
/// "<c>https://dummy.invalid/</c>" record, so this is a thin skin over <see cref="UrlParser"/> and
/// <see cref="UrlSetters"/> rather than a second canonicalizer. Applying them only to the <i>fixed text</i> of a
/// pattern is the whole point: "<c>:name</c>", "<c>*</c>" and "<c>(regexp)</c>" must survive verbatim, which is
/// why the constructor string parser cannot simply run the URL parser over the input.
/// </para>
/// <para>
/// A failure here is a <c>TypeError</c> from the <c>URLPattern</c> constructor. Where the spec runs the parser
/// without checking for failure — the pathname, search and hash callbacks — so does this, because those three
/// states cannot fail.
/// </para>
/// </remarks>
internal static class UrlPatternCanonicalization
{
    /// <summary>https://urlpattern.spec.whatwg.org/#url-pattern-create-a-dummy-url</summary>
    private static UrlRecord CreateDummyUrl() => UrlParser.Parse("https://dummy.invalid/")!;

    /// <summary>https://urlpattern.spec.whatwg.org/#canonicalize-a-protocol</summary>
    internal static string CanonicalizeProtocol(Realm realm, string value)
    {
        if (value.Length == 0)
        {
            return value;
        }

        // A state override is deliberately not used: its extra restrictions belong to the URL protocol setter,
        // not to a scheme being canonicalized on its own.
        var parseResult = UrlParser.Parse(value + "://dummy.invalid/");
        if (parseResult is null)
        {
            Throw.TypeError(realm, $"Invalid URLPattern protocol: {value}");
        }

        return parseResult.Scheme;
    }

    /// <summary>https://urlpattern.spec.whatwg.org/#canonicalize-a-username</summary>
    internal static string CanonicalizeUsername(Realm realm, string value)
    {
        if (value.Length == 0)
        {
            return value;
        }

        var dummyUrl = CreateDummyUrl();
        UrlSetters.SetUsername(dummyUrl, value);
        return dummyUrl.Username;
    }

    /// <summary>https://urlpattern.spec.whatwg.org/#canonicalize-a-password</summary>
    internal static string CanonicalizePassword(Realm realm, string value)
    {
        if (value.Length == 0)
        {
            return value;
        }

        var dummyUrl = CreateDummyUrl();
        UrlSetters.SetPassword(dummyUrl, value);
        return dummyUrl.Password;
    }

    /// <summary>https://urlpattern.spec.whatwg.org/#canonicalize-a-hostname</summary>
    internal static string CanonicalizeHostname(Realm realm, string value)
    {
        if (value.Length == 0)
        {
            return value;
        }

        var dummyUrl = CreateDummyUrl();
        if (!UrlParser.ParseInto(value, dummyUrl, UrlParserState.Hostname))
        {
            Throw.TypeError(realm, $"Invalid URLPattern hostname: {value}");
        }

        return dummyUrl.SerializeHost();
    }

    /// <summary>
    /// https://urlpattern.spec.whatwg.org/#canonicalize-an-ipv6-hostname — an IPv6 literal is lowercased and
    /// otherwise left alone, because the host parser would collapse a pattern such as "<c>[::*]</c>" that is not
    /// a valid address in the first place.
    /// </summary>
    internal static string CanonicalizeIpv6Hostname(Realm realm, string value)
    {
        var result = new ValueStringBuilder(stackalloc char[48]);
        try
        {
            foreach (var c in value)
            {
                if (!UrlCharacters.IsAsciiHexDigit(c) && c != '[' && c != ']' && c != ':')
                {
                    Throw.TypeError(realm, $"Invalid URLPattern IPv6 hostname: {value}");
                }

                result.Append(c is >= 'A' and <= 'Z' ? (char) (c | 0x20) : c);
            }

            return result.AsSpan().ToString();
        }
        finally
        {
            result.Dispose();
        }
    }

    /// <summary>
    /// https://urlpattern.spec.whatwg.org/#canonicalize-a-port, in the form the pattern parser uses it as an
    /// encoding callback — with no protocol to normalize a default port against.
    /// </summary>
    internal static string CanonicalizePort(Realm realm, string value) => CanonicalizePort(realm, value, protocolValue: null);

    /// <summary>https://urlpattern.spec.whatwg.org/#canonicalize-a-port</summary>
    internal static string CanonicalizePort(Realm realm, string portValue, string? protocolValue)
    {
        if (portValue.Length == 0)
        {
            return portValue;
        }

        var dummyUrl = CreateDummyUrl();

        // The scheme is set so that the port state recognizes a default port and drops it.
        dummyUrl.Scheme = protocolValue ?? string.Empty;
        dummyUrl.Port = null;

        if (!UrlParser.ParseInto(portValue, dummyUrl, UrlParserState.Port))
        {
            Throw.TypeError(realm, $"Invalid URLPattern port: {portValue}");
        }

        return dummyUrl.SerializePort();
    }

    /// <summary>https://urlpattern.spec.whatwg.org/#canonicalize-a-pathname</summary>
    internal static string CanonicalizePathname(Realm realm, string value)
    {
        if (value.Length == 0)
        {
            return value;
        }

        // The URL parser prepends a leading slash to a path, which is wrong here because this runs on pieces of a
        // pathname rather than on a whole one. Supplying "/-" disables the prepending; the "-" keeps a leading dot
        // in the value from being read as the "/." single-dot segment. Both are cut off again below.
        var leadingSlash = value[0] == '/';
        var modifiedValue = leadingSlash ? value : "/-" + value;

        var dummyUrl = CreateDummyUrl();
        dummyUrl.Path.Clear();
        UrlParser.ParseInto(modifiedValue, dummyUrl, UrlParserState.PathStart);

        var result = dummyUrl.SerializePath();
        return leadingSlash ? result : result.Substring(2);
    }

    /// <summary>https://urlpattern.spec.whatwg.org/#canonicalize-an-opaque-pathname</summary>
    internal static string CanonicalizeOpaquePathname(Realm realm, string value)
    {
        if (value.Length == 0)
        {
            return value;
        }

        var dummyUrl = CreateDummyUrl();
        dummyUrl.Path.Clear();
        dummyUrl.OpaquePath = string.Empty;

        if (!UrlParser.ParseInto(value, dummyUrl, UrlParserState.OpaquePath))
        {
            Throw.TypeError(realm, $"Invalid URLPattern pathname: {value}");
        }

        return dummyUrl.SerializePath();
    }

    /// <summary>https://urlpattern.spec.whatwg.org/#canonicalize-a-search</summary>
    internal static string CanonicalizeSearch(Realm realm, string value)
    {
        if (value.Length == 0)
        {
            return value;
        }

        var dummyUrl = CreateDummyUrl();
        dummyUrl.Query = string.Empty;
        UrlParser.ParseInto(value, dummyUrl, UrlParserState.Query);
        return dummyUrl.Query ?? string.Empty;
    }

    /// <summary>https://urlpattern.spec.whatwg.org/#canonicalize-a-hash</summary>
    internal static string CanonicalizeHash(Realm realm, string value)
    {
        if (value.Length == 0)
        {
            return value;
        }

        var dummyUrl = CreateDummyUrl();
        dummyUrl.Fragment = string.Empty;
        UrlParser.ParseInto(value, dummyUrl, UrlParserState.Fragment);
        return dummyUrl.Fragment ?? string.Empty;
    }
}
#endif
