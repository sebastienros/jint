#if NET8_0_OR_GREATER
using System.Globalization;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.WebApi.Url.Parsing;

namespace Jint.WebApi.Url.Pattern;

/// <summary>
/// https://urlpattern.spec.whatwg.org/#url-pattern — a compiled pattern: one <see cref="UrlPatternComponent"/>
/// per URL component, matched independently and all of which must match.
/// </summary>
/// <remarks>
/// This is the specification's <i>URL pattern</i> struct rather than the <c>URLPattern</c> object, and it holds
/// no JavaScript state beyond the eight compiled regular expressions — which is what lets
/// <see cref="Match"/> be the whole of <c>test()</c> and <c>exec()</c>.
/// </remarks>
internal sealed class UrlPatternRecord
{
    private UrlPatternRecord(
        UrlPatternComponent protocol,
        UrlPatternComponent username,
        UrlPatternComponent password,
        UrlPatternComponent hostname,
        UrlPatternComponent port,
        UrlPatternComponent pathname,
        UrlPatternComponent search,
        UrlPatternComponent hash)
    {
        Protocol = protocol;
        Username = username;
        Password = password;
        Hostname = hostname;
        Port = port;
        Pathname = pathname;
        Search = search;
        Hash = hash;
    }

    internal UrlPatternComponent Protocol { get; }
    internal UrlPatternComponent Username { get; }
    internal UrlPatternComponent Password { get; }
    internal UrlPatternComponent Hostname { get; }
    internal UrlPatternComponent Port { get; }
    internal UrlPatternComponent Pathname { get; }
    internal UrlPatternComponent Search { get; }
    internal UrlPatternComponent Hash { get; }

    /// <summary>https://urlpattern.spec.whatwg.org/#url-pattern-has-regexp-groups</summary>
    internal bool HasRegexpGroups
        => Protocol.HasRegexpGroups || Username.HasRegexpGroups || Password.HasRegexpGroups || Hostname.HasRegexpGroups
            || Port.HasRegexpGroups || Pathname.HasRegexpGroups || Search.HasRegexpGroups || Hash.HasRegexpGroups;

    /// <summary>
    /// https://urlpattern.spec.whatwg.org/#url-pattern-create — exactly one of <paramref name="stringInput"/> and
    /// <paramref name="initInput"/> is given, which is the two constructor overloads after WebIDL has resolved
    /// them.
    /// </summary>
    internal static UrlPatternRecord Create(
        Engine engine,
        string? stringInput,
        UrlPatternInit? initInput,
        string? baseUrl,
        bool ignoreCase)
    {
        var realm = engine.Realm;
        UrlPatternInit init;

        if (stringInput is not null)
        {
            init = UrlPatternConstructorString.Parse(engine, stringInput);

            if (baseUrl is null && init.Protocol is null)
            {
                Throw.TypeError(realm, $"Relative URLPattern constructor string requires a base URL: {stringInput}");
            }

            if (baseUrl is not null)
            {
                init.BaseUrl = baseUrl;
            }
        }
        else
        {
            if (baseUrl is not null)
            {
                Throw.TypeError(realm, "A URLPattern built from a URLPatternInit cannot also take a base URL argument");
            }

            init = initInput!;
        }

        var processedInit = UrlPatternInit.Process(
            realm, init, UrlPatternProcessType.Pattern, null, null, null, null, null, null, null, null);

        // A component the author said nothing about matches anything.
        processedInit.Protocol ??= "*";
        processedInit.Username ??= "*";
        processedInit.Password ??= "*";
        processedInit.Hostname ??= "*";
        processedInit.Port ??= "*";
        processedInit.Pathname ??= "*";
        processedInit.Search ??= "*";
        processedInit.Hash ??= "*";

        if (UrlRecord.DefaultPort(processedInit.Protocol) is { } defaultPort
            && string.Equals(processedInit.Port, defaultPort.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
        {
            // "https://example.com:443" is the same origin as "https://example.com", so the port pattern is the
            // empty string a URL record would have serialized.
            processedInit.Port = string.Empty;
        }

        var protocol = UrlPatternComponent.Compile(
            engine, processedInit.Protocol, UrlPatternCanonicalization.CanonicalizeProtocol, UrlPatternCompileOptions.Default());
        var username = UrlPatternComponent.Compile(
            engine, processedInit.Username, UrlPatternCanonicalization.CanonicalizeUsername, UrlPatternCompileOptions.Default());
        var password = UrlPatternComponent.Compile(
            engine, processedInit.Password, UrlPatternCanonicalization.CanonicalizePassword, UrlPatternCompileOptions.Default());

        var hostname = UrlPatternComponent.Compile(
            engine,
            processedInit.Hostname,
            HostnamePatternIsIpv6Address(processedInit.Hostname)
                ? UrlPatternCanonicalization.CanonicalizeIpv6Hostname
                : UrlPatternCanonicalization.CanonicalizeHostname,
            UrlPatternCompileOptions.Hostname());

        var port = UrlPatternComponent.Compile(
            engine, processedInit.Port, UrlPatternCanonicalization.CanonicalizePort, UrlPatternCompileOptions.Default());

        // Only the last three components honour ignoreCase; protocol, hostname and port are already lowercased by
        // their own canonicalization, and username and password are case-sensitive by the URL Standard.
        var compileOptions = UrlPatternCompileOptions.Default(ignoreCase);

        var pathname = protocol.MatchesSpecialScheme()
            ? UrlPatternComponent.Compile(
                engine, processedInit.Pathname, UrlPatternCanonicalization.CanonicalizePathname, UrlPatternCompileOptions.Pathname(ignoreCase))
            : UrlPatternComponent.Compile(
                engine, processedInit.Pathname, UrlPatternCanonicalization.CanonicalizeOpaquePathname, compileOptions);

        var search = UrlPatternComponent.Compile(
            engine, processedInit.Search, UrlPatternCanonicalization.CanonicalizeSearch, compileOptions);
        var hash = UrlPatternComponent.Compile(
            engine, processedInit.Hash, UrlPatternCanonicalization.CanonicalizeHash, compileOptions);

        return new UrlPatternRecord(protocol, username, password, hostname, port, pathname, search, hash);
    }

    /// <summary>
    /// https://urlpattern.spec.whatwg.org/#url-pattern-match — the whole of <c>test()</c> and <c>exec()</c>.
    /// Returns <see cref="JsValue.Null"/> for the specification's null, and a <c>URLPatternResult</c> otherwise.
    /// </summary>
    internal JsValue Match(Engine engine, string? stringInput, UrlPatternInit? initInput, string? baseUrlString)
    {
        var realm = engine.Realm;

        string protocol, username, password, hostname, port, pathname, search, hash;
        JsValue firstInput;
        JsValue? secondInput = null;

        if (initInput is not null)
        {
            firstInput = CreateInitObject(engine, initInput);

            if (baseUrlString is not null)
            {
                Throw.TypeError(realm, "A URLPatternInit input cannot also take a base URL argument");
            }

            UrlPatternInit applyResult;
            try
            {
                applyResult = UrlPatternInit.Process(
                    realm, initInput, UrlPatternProcessType.Url, "", "", "", "", "", "", "", "");
            }
            catch (JavaScriptException)
            {
                // A component that does not canonicalize — a port that is not a number, a hostname the host
                // parser rejects — is not a failure of the call, only a non-match. The catch is narrow on
                // purpose: everything this can raise is one of this file's own TypeErrors (the values are
                // already-coerced strings, so nothing user-written runs inside), and the sibling JintExceptions
                // that bound execution must never become a quiet null.
                return JsValue.Null;
            }

            protocol = applyResult.Protocol!;
            username = applyResult.Username!;
            password = applyResult.Password!;
            hostname = applyResult.Hostname!;
            port = applyResult.Port!;
            pathname = applyResult.Pathname!;
            search = applyResult.Search!;
            hash = applyResult.Hash!;
        }
        else
        {
            firstInput = JsString.Create(stringInput!);

            UrlRecord? baseUrl = null;
            if (baseUrlString is not null)
            {
                baseUrl = UrlParser.Parse(baseUrlString);
                if (baseUrl is null)
                {
                    return JsValue.Null;
                }

                secondInput = JsString.Create(baseUrlString);
            }

            var url = UrlParser.Parse(stringInput!, baseUrl);
            if (url is null)
            {
                return JsValue.Null;
            }

            protocol = url.Scheme;
            username = url.Username;
            password = url.Password;
            hostname = url.SerializeHost();
            port = url.SerializePort();
            pathname = url.SerializePath();
            search = url.Query ?? string.Empty;
            hash = url.Fragment ?? string.Empty;
        }

        var protocolExecResult = Protocol.Exec(protocol);
        var usernameExecResult = Username.Exec(username);
        var passwordExecResult = Password.Exec(password);
        var hostnameExecResult = Hostname.Exec(hostname);
        var portExecResult = Port.Exec(port);
        var pathnameExecResult = Pathname.Exec(pathname);
        var searchExecResult = Search.Exec(search);
        var hashExecResult = Hash.Exec(hash);

        if (protocolExecResult.IsNull() || usernameExecResult.IsNull() || passwordExecResult.IsNull()
            || hostnameExecResult.IsNull() || portExecResult.IsNull() || pathnameExecResult.IsNull()
            || searchExecResult.IsNull() || hashExecResult.IsNull())
        {
            return JsValue.Null;
        }

        var inputs = realm.Intrinsics.Array.ConstructFast(
            secondInput is null ? new[] { firstInput } : new[] { firstInput, secondInput });

        return JsObject.Create(engine, UrlPatternResultLayouts.Result,
        [
            inputs,
            Protocol.CreateComponentMatchResult(engine, protocol, protocolExecResult),
            Username.CreateComponentMatchResult(engine, username, usernameExecResult),
            Password.CreateComponentMatchResult(engine, password, passwordExecResult),
            Hostname.CreateComponentMatchResult(engine, hostname, hostnameExecResult),
            Port.CreateComponentMatchResult(engine, port, portExecResult),
            Pathname.CreateComponentMatchResult(engine, pathname, pathnameExecResult),
            Search.CreateComponentMatchResult(engine, search, searchExecResult),
            Hash.CreateComponentMatchResult(engine, hash, hashExecResult),
        ]);
    }

    /// <summary>
    /// https://urlpattern.spec.whatwg.org/#hostname-pattern-is-an-ipv6-address — a cheap syntactic test for a
    /// bracketed IPv6 literal, which is canonicalized by lowercasing rather than by the host parser.
    /// </summary>
    private static bool HostnamePatternIsIpv6Address(string input)
    {
        if (input.Length < 2)
        {
            return false;
        }

        return input[0] == '['
            || (input[0] == '{' && input[1] == '[')
            || (input[0] == '\\' && input[1] == '[');
    }

    /// <summary>
    /// The <c>URLPatternInit</c> that went into a match, converted back to a JavaScript object for
    /// <c>URLPatternResult</c>'s <c>inputs</c>: a WebIDL dictionary becomes an ordinary object carrying exactly
    /// the members that are present, in declaration order.
    /// </summary>
    private static JsObject CreateInitObject(Engine engine, UrlPatternInit init)
    {
        var result = ObjectInstance.OrdinaryObjectCreate(engine, engine.Realm.Intrinsics.Object.PrototypeObject);

        AddIfPresent(result, UrlPatternProperties.Protocol, init.Protocol);
        AddIfPresent(result, UrlPatternProperties.Username, init.Username);
        AddIfPresent(result, UrlPatternProperties.Password, init.Password);
        AddIfPresent(result, UrlPatternProperties.Hostname, init.Hostname);
        AddIfPresent(result, UrlPatternProperties.Port, init.Port);
        AddIfPresent(result, UrlPatternProperties.Pathname, init.Pathname);
        AddIfPresent(result, UrlPatternProperties.Search, init.Search);
        AddIfPresent(result, UrlPatternProperties.Hash, init.Hash);
        AddIfPresent(result, UrlPatternProperties.BaseUrl, init.BaseUrl);

        return result;
    }

    private static void AddIfPresent(ObjectInstance target, JsString name, string? value)
    {
        if (value is not null)
        {
            target.CreateDataPropertyOrThrow(name, JsString.Create(value));
        }
    }
}

/// <summary>
/// The <c>URLPatternInit</c> and <c>URLPatternOptions</c> member names, interned once.
/// </summary>
internal static class UrlPatternProperties
{
    internal static readonly JsString Protocol = new("protocol");
    internal static readonly JsString Username = new("username");
    internal static readonly JsString Password = new("password");
    internal static readonly JsString Hostname = new("hostname");
    internal static readonly JsString Port = new("port");
    internal static readonly JsString Pathname = new("pathname");
    internal static readonly JsString Search = new("search");
    internal static readonly JsString Hash = new("hash");
    internal static readonly JsString BaseUrl = new("baseURL");
    internal static readonly JsString IgnoreCase = new("ignoreCase");
}
#endif
