#if NET8_0_OR_GREATER
using System.Text;

namespace Jint.WebApi.Url.Parsing;

/// <summary>
/// A URL record, https://url.spec.whatwg.org/#concept-url — the in-memory form the parser produces and every
/// serializer and API getter reads.
/// </summary>
/// <remarks>
/// <para>
/// This type and everything else under <c>Jint.WebApi.Url.Parsing</c> is deliberately engine-free: it mentions
/// no <c>Engine</c>, <c>Realm</c> or <c>JsValue</c>, so the parser can be tested against the Web Platform Tests
/// corpus without building an engine per row, and so <c>fetch</c> can consume it later without going through
/// the JavaScript binding.
/// </para>
/// <para>
/// The spec's path is "either a URL path segment or a list of zero or more URL path segments"; the two cases
/// are <see cref="OpaquePath"/> and <see cref="Path"/> here, and <see cref="HasOpaquePath"/> is which one is
/// live. The blob URL entry is not modelled: this implementation has no blob URL store, so it is always null,
/// which is exactly what the origin getter's first blob step then falls through.
/// </para>
/// </remarks>
internal sealed class UrlRecord
{
    /// <summary>https://url.spec.whatwg.org/#concept-url-scheme</summary>
    internal string Scheme = string.Empty;

    /// <summary>https://url.spec.whatwg.org/#concept-url-username</summary>
    internal string Username = string.Empty;

    /// <summary>https://url.spec.whatwg.org/#concept-url-password</summary>
    internal string Password = string.Empty;

    /// <summary>https://url.spec.whatwg.org/#concept-url-host — null when the URL has no host.</summary>
    internal UrlHost? Host;

    /// <summary>https://url.spec.whatwg.org/#concept-url-port</summary>
    internal int? Port;

    /// <summary>
    /// https://url.spec.whatwg.org/#concept-url-path in its list form. Only meaningful when
    /// <see cref="HasOpaquePath"/> is false.
    /// </summary>
    internal List<string> Path = [];

    /// <summary>
    /// https://url.spec.whatwg.org/#concept-url-path in its single-segment form; non-null exactly when the URL
    /// has an opaque path.
    /// </summary>
    internal string? OpaquePath;

    /// <summary>https://url.spec.whatwg.org/#concept-url-query</summary>
    internal string? Query;

    /// <summary>https://url.spec.whatwg.org/#concept-url-fragment</summary>
    internal string? Fragment;

    /// <summary>https://url.spec.whatwg.org/#url-opaque-path</summary>
    internal bool HasOpaquePath => OpaquePath is not null;

    /// <summary>https://url.spec.whatwg.org/#is-special</summary>
    internal bool IsSpecial => IsSpecialScheme(Scheme);

    /// <summary>https://url.spec.whatwg.org/#include-credentials</summary>
    internal bool IncludesCredentials => Username.Length != 0 || Password.Length != 0;

    /// <summary>https://url.spec.whatwg.org/#cannot-have-a-username-password-port</summary>
    internal bool CannotHaveCredentialsOrPort
        => Host is null || Host.Value.IsEmpty || string.Equals(Scheme, "file", StringComparison.Ordinal);

    /// <summary>
    /// https://url.spec.whatwg.org/#special-scheme — and its default port, null for every scheme not listed.
    /// </summary>
    internal static bool IsSpecialScheme(string scheme) => scheme switch
    {
        "ftp" or "file" or "http" or "https" or "ws" or "wss" => true,
        _ => false,
    };

    /// <summary>https://url.spec.whatwg.org/#default-port</summary>
    internal static int? DefaultPort(string scheme) => scheme switch
    {
        "ftp" => 21,
        "http" => 80,
        "https" => 443,
        "ws" => 80,
        "wss" => 443,
        _ => null,
    };

    /// <summary>
    /// https://url.spec.whatwg.org/#shorten-a-urls-path
    /// </summary>
    internal void ShortenPath()
    {
        if (string.Equals(Scheme, "file", StringComparison.Ordinal)
            && Path.Count == 1
            && UrlCharacters.IsNormalizedWindowsDriveLetter(Path[0].AsSpan()))
        {
            return;
        }

        if (Path.Count > 0)
        {
            Path.RemoveAt(Path.Count - 1);
        }
    }

    /// <summary>
    /// The URL serializer, https://url.spec.whatwg.org/#concept-url-serializer — what the <c>href</c> getter
    /// returns.
    /// </summary>
    internal string Serialize(bool excludeFragment = false)
    {
        var builder = new ValueStringBuilder(stackalloc char[128]);
        try
        {
            builder.Append(Scheme);
            builder.Append(':');

            if (Host is { } host)
            {
                builder.Append("//");

                if (IncludesCredentials)
                {
                    builder.Append(Username);
                    if (Password.Length != 0)
                    {
                        builder.Append(':');
                        builder.Append(Password);
                    }

                    builder.Append('@');
                }

                builder.Append(host.Serialized);

                if (Port is { } port)
                {
                    builder.Append(':');
                    builder.Append(port);
                }
            }
            else if (!HasOpaquePath && Path.Count > 1 && Path[0].Length == 0)
            {
                // Without this, "web+demo:/.//not-a-host/" would serialize as "web+demo://not-a-host/" and
                // parse back as something with a host.
                builder.Append("/.");
            }

            AppendPath(ref builder);

            if (Query is not null)
            {
                builder.Append('?');
                builder.Append(Query);
            }

            if (!excludeFragment && Fragment is not null)
            {
                builder.Append('#');
                builder.Append(Fragment);
            }

            return builder.AsSpan().ToString();
        }
        finally
        {
            builder.Dispose();
        }
    }

    /// <summary>
    /// The URL path serializer, https://url.spec.whatwg.org/#url-path-serializer — what the <c>pathname</c>
    /// getter returns.
    /// </summary>
    internal string SerializePath()
    {
        if (OpaquePath is { } opaque)
        {
            return opaque;
        }

        if (Path.Count == 0)
        {
            return string.Empty;
        }

        var builder = new ValueStringBuilder(stackalloc char[128]);
        try
        {
            AppendPath(ref builder);
            return builder.AsSpan().ToString();
        }
        finally
        {
            builder.Dispose();
        }
    }

    private void AppendPath(ref ValueStringBuilder builder)
    {
        if (OpaquePath is { } opaque)
        {
            builder.Append(opaque);
            return;
        }

        foreach (var segment in Path)
        {
            builder.Append('/');
            builder.Append(segment);
        }
    }

    /// <summary>
    /// The <c>host</c> getter, https://url.spec.whatwg.org/#dom-url-host — host and port together.
    /// </summary>
    internal string SerializeHostAndPort()
    {
        if (Host is not { } host)
        {
            return string.Empty;
        }

        if (Port is not { } port)
        {
            return host.Serialized;
        }

        return host.Serialized + ":" + port.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The <c>hostname</c> getter, https://url.spec.whatwg.org/#dom-url-hostname.
    /// </summary>
    internal string SerializeHost() => Host?.Serialized ?? string.Empty;

    /// <summary>
    /// The <c>port</c> getter, https://url.spec.whatwg.org/#dom-url-port.
    /// </summary>
    internal string SerializePort()
        => Port is { } port ? port.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty;

    /// <summary>
    /// The <c>search</c> getter, https://url.spec.whatwg.org/#dom-url-search.
    /// </summary>
    internal string SerializeSearch() => Query is null || Query.Length == 0 ? string.Empty : "?" + Query;

    /// <summary>
    /// The <c>hash</c> getter, https://url.spec.whatwg.org/#dom-url-hash.
    /// </summary>
    internal string SerializeHash() => Fragment is null || Fragment.Length == 0 ? string.Empty : "#" + Fragment;

    /// <summary>
    /// The <c>protocol</c> getter, https://url.spec.whatwg.org/#dom-url-protocol.
    /// </summary>
    internal string SerializeProtocol() => Scheme + ":";

    /// <summary>
    /// The origin of a URL, https://url.spec.whatwg.org/#concept-url-origin, already serialized per
    /// https://html.spec.whatwg.org/multipage/browsers.html#ascii-serialisation-of-an-origin. An opaque origin
    /// serializes as "null".
    /// </summary>
    /// <remarks>
    /// The "blob" branch re-parses the path, which is how <c>blob:https://whatwg.org/…</c> reports the origin
    /// of the URL it wraps. Its first step reads the blob URL entry, which is always null here because this
    /// implementation has no blob URL store, so the re-parse is always what answers. A "file" URL's origin is
    /// "left as an exercise to the reader" by the spec, with the advice to return an opaque origin; that is
    /// what happens both directly and through the blob branch.
    /// </remarks>
    internal string SerializeOrigin()
    {
        if (string.Equals(Scheme, "blob", StringComparison.Ordinal))
        {
            var inner = UrlParser.Parse(SerializePath());
            if (inner is null)
            {
                return "null";
            }

            return inner.Scheme is "http" or "https" or "file" ? inner.SerializeOrigin() : "null";
        }

        return Scheme switch
        {
            "ftp" or "http" or "https" or "ws" or "wss" => SerializeTupleOrigin(),
            _ => "null",
        };
    }

    private string SerializeTupleOrigin()
    {
        var builder = new ValueStringBuilder(stackalloc char[64]);
        try
        {
            builder.Append(Scheme);
            builder.Append("://");
            builder.Append(SerializeHost());
            if (Port is { } port)
            {
                builder.Append(':');
                builder.Append(port);
            }

            return builder.AsSpan().ToString();
        }
        finally
        {
            builder.Dispose();
        }
    }
}
#endif
