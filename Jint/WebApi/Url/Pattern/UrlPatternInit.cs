#if NET8_0_OR_GREATER
using System.Globalization;
using Jint.Runtime;
using Jint.WebApi.Url.Parsing;

namespace Jint.WebApi.Url.Pattern;

/// <summary>
/// Which of the two jobs https://urlpattern.spec.whatwg.org/#process-a-urlpatterninit is doing.
/// </summary>
internal enum UrlPatternProcessType
{
    /// <summary>
    /// The values are pattern strings being compiled, so they are left alone — canonicalization happens later,
    /// per piece of fixed text, inside the pattern parser.
    /// </summary>
    Pattern,

    /// <summary>
    /// The values are the components of a URL being matched, so each is canonicalized whole, right here.
    /// </summary>
    Url,
}

/// <summary>
/// https://urlpattern.spec.whatwg.org/#dictdef-urlpatterninit — a component-by-component pattern or URL, with
/// every member independently present or absent.
/// </summary>
/// <remarks>
/// <see langword="null"/> is how "does not exist" is spelled; the difference matters throughout
/// <see cref="Process"/>, where a member that exists suppresses inheritance of every less specific component
/// from the base URL.
/// </remarks>
internal sealed class UrlPatternInit
{
    internal string? Protocol;
    internal string? Username;
    internal string? Password;
    internal string? Hostname;
    internal string? Port;
    internal string? Pathname;
    internal string? Search;
    internal string? Hash;
    internal string? BaseUrl;

    /// <summary>
    /// https://urlpattern.spec.whatwg.org/#process-a-urlpatterninit — fills in whatever the init leaves out from
    /// the base URL, then canonicalizes what the init did supply.
    /// </summary>
    /// <remarks>
    /// The inheritance rules are the note in the specification made mechanical: a component is inherited only
    /// when the init specifies nothing at least as specific as it, along "protocol, hostname, port, pathname,
    /// search, hash" and "protocol, hostname, port, username, password". So an init carrying only a pathname
    /// keeps the base URL's protocol, hostname and port but not its search or hash, and username and password
    /// are never inherited while compiling a pattern.
    /// </remarks>
    internal static UrlPatternInit Process(
        Realm realm,
        UrlPatternInit init,
        UrlPatternProcessType type,
        string? protocol,
        string? username,
        string? password,
        string? hostname,
        string? port,
        string? pathname,
        string? search,
        string? hash)
    {
        var result = new UrlPatternInit
        {
            Protocol = protocol,
            Username = username,
            Password = password,
            Hostname = hostname,
            Port = port,
            Pathname = pathname,
            Search = search,
            Hash = hash,
        };

        UrlRecord? baseUrl = null;
        if (init.BaseUrl is { } baseUrlString)
        {
            baseUrl = UrlParser.Parse(baseUrlString);
            if (baseUrl is null)
            {
                Throw.TypeError(realm, $"Invalid URLPattern baseURL: {baseUrlString}");
            }

            if (init.Protocol is null)
            {
                result.Protocol = ProcessBaseUrlString(baseUrl.Scheme, type);
            }

            if (type != UrlPatternProcessType.Pattern
                && init.Protocol is null && init.Hostname is null && init.Port is null && init.Username is null)
            {
                result.Username = ProcessBaseUrlString(baseUrl.Username, type);
            }

            if (type != UrlPatternProcessType.Pattern
                && init.Protocol is null && init.Hostname is null && init.Port is null && init.Username is null && init.Password is null)
            {
                result.Password = ProcessBaseUrlString(baseUrl.Password, type);
            }

            if (init.Protocol is null && init.Hostname is null)
            {
                result.Hostname = ProcessBaseUrlString(baseUrl.SerializeHost(), type);
            }

            if (init.Protocol is null && init.Hostname is null && init.Port is null)
            {
                // A port is ASCII digits or nothing, so it is the one inherited component that never needs
                // escaping into a pattern string.
                result.Port = baseUrl.Port is { } basePort ? basePort.ToString(CultureInfo.InvariantCulture) : string.Empty;
            }

            if (init.Protocol is null && init.Hostname is null && init.Port is null && init.Pathname is null)
            {
                result.Pathname = ProcessBaseUrlString(baseUrl.SerializePath(), type);
            }

            if (init.Protocol is null && init.Hostname is null && init.Port is null && init.Pathname is null && init.Search is null)
            {
                result.Search = ProcessBaseUrlString(baseUrl.Query ?? string.Empty, type);
            }

            if (init.Protocol is null && init.Hostname is null && init.Port is null && init.Pathname is null
                && init.Search is null && init.Hash is null)
            {
                result.Hash = ProcessBaseUrlString(baseUrl.Fragment ?? string.Empty, type);
            }
        }

        if (init.Protocol is { } initProtocol)
        {
            result.Protocol = ProcessProtocolForInit(realm, initProtocol, type);
        }

        if (init.Username is { } initUsername)
        {
            result.Username = type == UrlPatternProcessType.Pattern
                ? initUsername
                : UrlPatternCanonicalization.CanonicalizeUsername(realm, initUsername);
        }

        if (init.Password is { } initPassword)
        {
            result.Password = type == UrlPatternProcessType.Pattern
                ? initPassword
                : UrlPatternCanonicalization.CanonicalizePassword(realm, initPassword);
        }

        if (init.Hostname is { } initHostname)
        {
            result.Hostname = type == UrlPatternProcessType.Pattern
                ? initHostname
                : UrlPatternCanonicalization.CanonicalizeHostname(realm, initHostname);
        }

        var resultProtocolString = result.Protocol ?? string.Empty;

        if (init.Port is { } initPort)
        {
            result.Port = type == UrlPatternProcessType.Pattern
                ? initPort
                : UrlPatternCanonicalization.CanonicalizePort(realm, initPort, resultProtocolString);
        }

        if (init.Pathname is { } initPathname)
        {
            result.Pathname = initPathname;

            if (baseUrl is not null && !baseUrl.HasOpaquePath && !IsAbsolutePathname(result.Pathname, type))
            {
                // A relative pathname resolves against the base URL's directory, exactly as a relative URL does.
                var baseUrlPath = ProcessBaseUrlString(baseUrl.SerializePath(), type);
                var slashIndex = baseUrlPath.LastIndexOf('/');
                if (slashIndex >= 0)
                {
                    result.Pathname = string.Concat(baseUrlPath.AsSpan(0, slashIndex + 1), result.Pathname);
                }
            }

            result.Pathname = ProcessPathnameForInit(realm, result.Pathname, resultProtocolString, type);
        }

        if (init.Search is { } initSearch)
        {
            var strippedValue = initSearch.Length != 0 && initSearch[0] == '?' ? initSearch.Substring(1) : initSearch;
            result.Search = type == UrlPatternProcessType.Pattern
                ? strippedValue
                : UrlPatternCanonicalization.CanonicalizeSearch(realm, strippedValue);
        }

        if (init.Hash is { } initHash)
        {
            var strippedValue = initHash.Length != 0 && initHash[0] == '#' ? initHash.Substring(1) : initHash;
            result.Hash = type == UrlPatternProcessType.Pattern
                ? strippedValue
                : UrlPatternCanonicalization.CanonicalizeHash(realm, strippedValue);
        }

        return result;
    }

    /// <summary>
    /// https://urlpattern.spec.whatwg.org/#process-a-base-url-string — a value inherited from the base URL is
    /// fixed text, so when it is going to be compiled as a pattern every syntax code point in it is escaped.
    /// </summary>
    private static string ProcessBaseUrlString(string input, UrlPatternProcessType type)
        => type == UrlPatternProcessType.Pattern ? UrlPatternGenerator.EscapePatternString(input) : input;

    /// <summary>https://urlpattern.spec.whatwg.org/#process-protocol-for-init</summary>
    private static string ProcessProtocolForInit(Realm realm, string value, UrlPatternProcessType type)
    {
        var strippedValue = value.Length != 0 && value[value.Length - 1] == ':' ? value.Substring(0, value.Length - 1) : value;
        return type == UrlPatternProcessType.Pattern
            ? strippedValue
            : UrlPatternCanonicalization.CanonicalizeProtocol(realm, strippedValue);
    }

    /// <summary>https://urlpattern.spec.whatwg.org/#process-pathname-for-init</summary>
    private static string ProcessPathnameForInit(Realm realm, string pathnameValue, string protocolValue, UrlPatternProcessType type)
    {
        if (type == UrlPatternProcessType.Pattern)
        {
            return pathnameValue;
        }

        // An empty protocol means the init supplied none, and the common case by far is a hierarchical path, so
        // it is treated like a special scheme rather than like an opaque one.
        if (protocolValue.Length == 0 || UrlRecord.IsSpecialScheme(protocolValue))
        {
            return UrlPatternCanonicalization.CanonicalizePathname(realm, pathnameValue);
        }

        return UrlPatternCanonicalization.CanonicalizeOpaquePathname(realm, pathnameValue);
    }

    /// <summary>
    /// https://urlpattern.spec.whatwg.org/#is-an-absolute-pathname — a pattern may start its absolute path with an
    /// escaped or braced slash, which a plain URL pathname cannot.
    /// </summary>
    private static bool IsAbsolutePathname(string input, UrlPatternProcessType type)
    {
        if (input.Length == 0)
        {
            return false;
        }

        if (input[0] == '/')
        {
            return true;
        }

        if (type == UrlPatternProcessType.Url || input.Length < 2)
        {
            return false;
        }

        return (input[0] == '\\' && input[1] == '/') || (input[0] == '{' && input[1] == '/');
    }
}
#endif
