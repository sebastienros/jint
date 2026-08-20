#if NET8_0_OR_GREATER
using System.Text;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.WebApi.Url;
using Jint.WebApi.Url.Parsing;

namespace Jint.NodeCompat;

/// <summary>
/// Builds the JavaScript surface of <c>node:url</c>.
/// <para>
/// https://nodejs.org/api/url.html
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <c>URL</c> and <c>URLSearchParams</c> are the engine's own WHATWG implementations, re-exported rather than
/// reimplemented — so <c>new (await import('node:url')).URL(…) instanceof URL</c> holds against the global one
/// whenever the host also enabled <c>WebApiFeatures.Url</c>, and the module works when it did not. Importing
/// this module is itself the opt-in: the interface objects are per-realm intrinsics that exist whether or not
/// anything installed them as globals.
/// </para>
/// <para>
/// Four functions are the module's own, and each is the reason a Node script imports it at all: the two path
/// conversions, which are how an ES module turns <c>import.meta.url</c> into something the file system
/// understands, and the two IDNA mappings, which run over the same <see cref="Idna"/> the URL host parser
/// uses.
/// </para>
/// <para>
/// What is absent, and why. The <b>legacy URL API</b> — <c>url.parse</c>, <c>url.format(string)</c>,
/// <c>url.resolve</c>, <c>Url</c> — is documented by Node as legacy and its parser diverges from the WHATWG
/// one in ways that have been a source of security advisories; the WHATWG <c>URL</c> beside it does the same
/// job. <c>urlToHttpOptions</c> shapes an options object for <c>node:http</c>, which Jint does not have.
/// <c>fileURLToPathBuffer</c> answers with a <c>Buffer</c>. <c>URLPattern</c> is a separate specification, and
/// <c>url.format(URL, options)</c> is left for a later slice rather than guessed at.
/// </para>
/// </remarks>
internal static class NodeUrlModule
{
    internal static List<KeyValuePair<string, JsValue>> CreateExports(Engine engine, NodeBuiltinModuleConfiguration configuration)
    {
        var realm = engine.Realm;
        var platformIsWindows = configuration.PlatformIsWindows;
        var workingDirectory = configuration.WorkingDirectory;
        var posixWorkingDirectory = configuration.PosixWorkingDirectory;

        var entries = new List<KeyValuePair<string, JsValue>>
        {
            new("URL", realm.Intrinsics.WebApiUrl),
            new("URLSearchParams", realm.Intrinsics.WebApiUrlSearchParams),

            new("domainToASCII", NodeBuiltinHelpers.Operation(engine, realm, "domainToASCII", 1, (_, arguments) =>
                JsString.Create(DomainToAscii(realm, arguments)))),

            new("domainToUnicode", NodeBuiltinHelpers.Operation(engine, realm, "domainToUnicode", 1, (_, arguments) =>
                JsString.Create(DomainToUnicode(realm, arguments)))),

            new("fileURLToPath", NodeBuiltinHelpers.Operation(engine, realm, "fileURLToPath", 1, (_, arguments) =>
                JsString.Create(FileUrlToPath(realm, platformIsWindows, arguments)))),

            new("pathToFileURL", NodeBuiltinHelpers.Operation(engine, realm, "pathToFileURL", 1, (_, arguments) =>
                PathToFileUrl(engine, realm, platformIsWindows, workingDirectory, posixWorkingDirectory, arguments))),
        };

        var moduleObject = JsObject.CreateFromEntries(engine, entries);

        var exports = new List<KeyValuePair<string, JsValue>>(entries.Count + 1);
        exports.AddRange(entries);
        exports.Add(new KeyValuePair<string, JsValue>("default", moduleObject));
        return exports;
    }

    /// <summary>
    /// <c>url.domainToASCII(domain)</c>: "returns the Punycode ASCII serialization of the <c>domain</c>. If
    /// <c>domain</c> is an invalid domain, the empty string is returned."
    /// <para>
    /// https://nodejs.org/api/url.html#urldomaintoasciidomain
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para>
    /// An ASCII domain is lowercased without consulting IDNA at all, which is what
    /// https://url.spec.whatwg.org/#concept-domain-to-ascii step 4 requires — "return the result of ASCII
    /// lowercasing <em>domain</em> … regardless of Unicode ToASCII's outcome, due to web compatibility" — and
    /// what keeps this answering the way the engine's own URL host parser does for the same input.
    /// </para>
    /// <para>
    /// Node validates an ASCII domain as well, so its <c>domainToASCII('a b')</c> is the empty string where
    /// this answers <c>'a b'</c>, and it percent-decodes one, so its <c>domainToASCII('a%2Eb')</c> is
    /// <c>'a.b'</c>. Both come from running full UTS-46 with parameters <c>IdnMapping</c> cannot be
    /// given; the divergence is confined to domains that are not domains, and <see cref="Idna"/> documents the
    /// rest of the family it belongs to.
    /// </para>
    /// </remarks>
    private static string DomainToAscii(Realm realm, JsCallArguments arguments)
    {
        var domain = RequireDomain(realm, arguments);

        if (IsAscii(domain))
        {
            return domain.ToLowerInvariant();
        }

        return Idna.TryToAscii(domain, out var result) ? result : string.Empty;
    }

    private static bool IsAscii(string value)
    {
        foreach (var c in value)
        {
            if (c > 0x7F)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// <c>url.domainToUnicode(domain)</c>: "returns the Unicode serialization of the <c>domain</c>. If
    /// <c>domain</c> is an invalid domain, the empty string is returned. It performs the inverse operation to
    /// <c>url.domainToASCII()</c>."
    /// <para>
    /// https://nodejs.org/api/url.html#urldomaintounicodedomain
    /// </para>
    /// </summary>
    private static string DomainToUnicode(Realm realm, JsCallArguments arguments)
    {
        var domain = RequireDomain(realm, arguments);
        return Idna.TryToUnicode(domain, out var result) ? result : string.Empty;
    }

    /// <summary>
    /// Both mappings coerce their argument rather than validating it — Node's only check is that one was
    /// passed at all, which it reports as <c>ERR_MISSING_ARGS</c>.
    /// </summary>
    private static string RequireDomain(Realm realm, JsCallArguments arguments)
    {
        if (arguments.Length < 1)
        {
            Throw.TypeError(realm, "The \"domain\" argument must be specified");
        }

        return TypeConverter.ToString(arguments[0]);
    }

    /// <summary>
    /// <c>url.fileURLToPath(url[, options])</c>: "ensures that the percent-encoded characters are decoded
    /// correctly as well as ensuring a cross-platform valid absolute path string".
    /// <para>
    /// https://nodejs.org/api/url.html#urlfileurltopathurl-options
    /// </para>
    /// </summary>
    /// <remarks>
    /// Node's own security note applies unchanged, and is the reason it is repeated here: the function decodes
    /// percent-encoded dot segments, so <c>%2e%2e</c> becomes <c>..</c> in the answer. An encoded separator is
    /// refused, but an encoded traversal is not, and a host must validate the path it gets back rather than
    /// treating this as a sandbox check.
    /// </remarks>
    private static string FileUrlToPath(Realm realm, bool platformIsWindows, JsCallArguments arguments)
    {
        var windows = WindowsOption(arguments.At(1), platformIsWindows);
        var record = ToUrlRecord(realm, arguments.At(0));

        if (!string.Equals(record.Scheme, "file", StringComparison.Ordinal))
        {
            Throw.TypeError(realm, "The URL must be of scheme file");
        }

        var hostname = record.SerializeHost();
        var pathname = record.SerializePath();

        if (!windows)
        {
            if (hostname.Length != 0)
            {
                Throw.TypeError(realm, "File URL host must be \"localhost\" or empty on " + (platformIsWindows ? NodePlatform.Windows : NodePlatform.Linux));
            }

            RejectEncodedSeparators(realm, pathname, windows: false);
            return Decode(realm, pathname);
        }

        RejectEncodedSeparators(realm, pathname, windows: true);
        pathname = Decode(realm, pathname.Replace('/', '\\'));

        if (hostname.Length != 0)
        {
            // A host means a UNC path. The host is put back through IDNA in case it is an internationalized
            // name the URL parser stored in its Punycode form.
            var unicodeHost = Idna.TryToUnicode(hostname, out var decoded) ? decoded : string.Empty;
            return @"\\" + unicodeHost + pathname;
        }

        // Otherwise it is a local path, which needs a drive letter: "/C:/x" becomes "C:\x".
        var letter = pathname.Length > 1 ? (char) (pathname[1] | 0x20) : '\0';
        var separator = pathname.Length > 2 ? pathname[2] : '\0';
        if (letter < 'a' || letter > 'z' || separator != ':')
        {
            Throw.TypeError(realm, "File URL path must be absolute");
        }

        return pathname.Substring(1);
    }

    /// <summary>
    /// A percent-encoded separator would survive the decode and change which directory the path names, so it
    /// is refused rather than decoded — the one traversal check Node does make here.
    /// </summary>
    private static void RejectEncodedSeparators(Realm realm, string pathname, bool windows)
    {
        for (var i = 0; i < pathname.Length; i++)
        {
            if (pathname[i] != '%' || i + 2 >= pathname.Length)
            {
                continue;
            }

            var third = (char) (pathname[i + 2] | 0x20);
            if (pathname[i + 1] == '2' && third == 'f')
            {
                Throw.TypeError(realm, windows
                    ? "File URL path must not include encoded \\ or / characters"
                    : "File URL path must not include encoded / characters");
            }

            if (windows && pathname[i + 1] == '5' && third == 'c')
            {
                Throw.TypeError(realm, "File URL path must not include encoded \\ or / characters");
            }
        }
    }

    private static string Decode(Realm realm, string pathname)
    {
        if (pathname.IndexOf('%') < 0)
        {
            return pathname;
        }

        if (!NodeBuiltinHelpers.TryDecodeUriComponent(pathname, out var decoded))
        {
            // What decodeURIComponent raises, which Node lets escape from fileURLToPath unchanged.
            Throw.UriError(realm, "URI malformed");
        }

        return decoded;
    }

    /// <summary>
    /// <c>url.pathToFileURL(path[, options])</c>: "this function ensures that <c>path</c> is resolved
    /// absolutely, and that the URL control characters are correctly encoded when converting into a File URL."
    /// <para>
    /// https://nodejs.org/api/url.html#urlpathtofileurlpath-options
    /// </para>
    /// </summary>
    private static JsUrl PathToFileUrl(
        Engine engine,
        Realm realm,
        bool platformIsWindows,
        string workingDirectory,
        string posixWorkingDirectory,
        JsCallArguments arguments)
    {
        var windows = WindowsOption(arguments.At(1), platformIsWindows);
        var filePath = NodeBuiltinHelpers.RequireString(realm, arguments.At(0), "path");

        var isUnc = windows && filePath.StartsWith(@"\\", StringComparison.Ordinal);
        var resolved = isUnc
            ? filePath
            : windows
                ? NodeWin32Path.Resolve(workingDirectory, platformIsWindows, [filePath])
                : NodePosixPath.Resolve(posixWorkingDirectory, [filePath]);

        if (isUnc || (windows && resolved.StartsWith(@"\\", StringComparison.Ordinal)))
        {
            // UNC: \\server\share\resource, with the extended \\?\UNC\ prefix skipped when present.
            var isExtendedUnc = resolved.StartsWith(@"\\?\UNC\", StringComparison.Ordinal);
            var prefixLength = isExtendedUnc ? 8 : 2;
            var hostnameEnd = resolved.IndexOf('\\', prefixLength);
            if (hostnameEnd == -1)
            {
                Throw.TypeError(realm, "The argument 'path' must be a valid UNC path: missing UNC resource path");
            }

            if (hostnameEnd == 2)
            {
                Throw.TypeError(realm, "The argument 'path' must be a valid UNC path: empty UNC servername");
            }

            var host = resolved.Substring(prefixLength, hostnameEnd - prefixLength);
            return CreateUrl(engine, realm, host, resolved.Substring(hostnameEnd), windows: true);
        }

        // resolve() strips a trailing separator, and the URL is expected to keep it.
        var last = filePath.Length > 0 ? filePath[filePath.Length - 1] : '\0';
        var separator = windows ? NodeWin32Path.Separator : NodePosixPath.Separator;
        if ((last == '/' || (windows && last == '\\'))
            && resolved.Length > 0
            && resolved[resolved.Length - 1] != separator)
        {
            resolved += '/';
        }

        return CreateUrl(engine, realm, host: string.Empty, resolved, windows);
    }

    /// <summary>
    /// Builds the <c>file:</c> URL for a resolved path.
    /// </summary>
    /// <remarks>
    /// The characters escaped by hand are the ones that would otherwise change what the URL <em>means</em>
    /// rather than merely how it is spelled: <c>%</c> first, so an already-percent-looking sequence in the file
    /// name survives as itself, then <c>#</c> and <c>?</c>, which would start a fragment or a query, then the
    /// three whitespace characters the URL parser strips outright. A backslash is escaped on POSIX for the same
    /// reason: <c>file:</c> is a special scheme, so the parser treats a backslash as a separator, and a POSIX
    /// file name is allowed to contain one. Everything else — a space, a non-ASCII character — is left for the
    /// parser to percent-encode with the path set, which is what makes the result identical to what the URL
    /// class would produce for the same path.
    /// </remarks>
    private static JsUrl CreateUrl(Engine engine, Realm realm, string host, string path, bool windows)
    {
        var builder = new StringBuilder("file://");
        builder.Append(host);

        if (path.Length == 0 || path[0] != '/' && path[0] != '\\')
        {
            builder.Append('/');
        }

        foreach (var c in path)
        {
            switch (c)
            {
                case '%':
                    builder.Append("%25");
                    break;
                case '#':
                    builder.Append("%23");
                    break;
                case '?':
                    builder.Append("%3F");
                    break;
                case '\n':
                    builder.Append("%0A");
                    break;
                case '\r':
                    builder.Append("%0D");
                    break;
                case '\t':
                    builder.Append("%09");
                    break;
                case '\\':
                    builder.Append(windows ? "/" : "%5C");
                    break;
                default:
                    builder.Append(c);
                    break;
            }
        }

        var record = UrlParser.ParseApi(builder.ToString(), baseHref: null);
        if (record is null)
        {
            Throw.TypeError(realm, "The argument 'path' cannot be converted to a file URL");
        }

        return new JsUrl(engine, realm, record) { _prototype = realm.Intrinsics.WebApiUrl.PrototypeObject };
    }

    /// <summary>
    /// The <c>windows</c> option both conversions take: "<c>true</c> for windows path, <c>false</c> for posix
    /// path, <c>undefined</c> for system default".
    /// </summary>
    private static bool WindowsOption(JsValue options, bool platformIsWindows)
    {
        if (options is not ObjectInstance optionsObject)
        {
            return platformIsWindows;
        }

        var windows = optionsObject.Get("windows");
        return windows.IsUndefined() ? platformIsWindows : TypeConverter.ToBoolean(windows);
    }

    /// <summary>
    /// The <c>url</c> argument of <c>fileURLToPath</c>, which "may be a <c>URL</c> object or a string".
    /// </summary>
    private static UrlRecord ToUrlRecord(Realm realm, JsValue value)
    {
        if (value is JsUrl url)
        {
            return url.Url;
        }

        if (value is JsString text)
        {
            var record = UrlParser.ParseApi(text.ToString(), baseHref: null);
            if (record is null)
            {
                Throw.TypeError(realm, UrlConstructor.InvalidUrlMessage(text.ToString(), baseHref: null));
            }

            return record!;
        }

        Throw.TypeError(realm, "The \"path\" argument must be one of type string or URL");
        return null!;
    }
}
#endif
