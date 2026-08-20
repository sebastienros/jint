#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Runtime;

namespace Jint.WebApi.Fetch;

/// <summary>
/// The value conversions and small grammars the <c>Headers</c>, <c>Request</c> and <c>Response</c> bindings
/// share.
/// </summary>
internal static class FetchValues
{
    /// <summary>
    /// WebIDL's <c>ByteString</c> conversion, https://webidl.spec.whatwg.org/#es-ByteString: <c>ToString</c>,
    /// then a <c>TypeError</c> for any code unit above 0x00FF. Every header name, header value, method and
    /// status text is a <c>ByteString</c>, so this is the one door they come in through.
    /// </summary>
    internal static string ToByteString(Realm realm, JsValue value)
    {
        var text = TypeConverter.ToString(value);
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] > 0xFF)
            {
                Throw.TypeError(
                    realm,
                    $"Cannot convert argument to a ByteString because the character at index {i} has a value of {(int) text[i]}, which is greater than 255");
            }
        }

        return text;
    }

    /// <summary>
    /// https://fetch.spec.whatwg.org/#concept-method — a method is an RFC 9110 token.
    /// </summary>
    internal static bool IsMethod(string method) => HeaderList.IsName(method);

    /// <summary>
    /// https://fetch.spec.whatwg.org/#forbidden-method — <c>CONNECT</c>, <c>TRACE</c> and <c>TRACK</c>,
    /// byte-case-insensitively.
    /// </summary>
    /// <remarks>
    /// A browser refuses these because they are the shapes of a proxy tunnel and of a cross-site tracing
    /// attack; the reasons survive server-side, where a script that could open a <c>CONNECT</c> tunnel through
    /// the host's network position would be a far larger hole than in a browser. So unlike the forbidden
    /// <i>header</i> lists — which Jint deliberately does not enforce, see <see cref="HeadersGuard"/> — this
    /// one is kept.
    /// </remarks>
    internal static bool IsForbiddenMethod(string method)
        => method.Equals("CONNECT", StringComparison.OrdinalIgnoreCase)
        || method.Equals("TRACE", StringComparison.OrdinalIgnoreCase)
        || method.Equals("TRACK", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// https://fetch.spec.whatwg.org/#concept-method-normalize — the six methods whose casing is corrected,
    /// and only those: <c>patch</c> stays lowercase, exactly as in a browser.
    /// </summary>
    internal static string NormalizeMethod(string method)
    {
        foreach (var known in _normalizedMethods)
        {
            if (method.Equals(known, StringComparison.OrdinalIgnoreCase))
            {
                return known;
            }
        }

        return method;
    }

    private static readonly string[] _normalizedMethods = ["DELETE", "GET", "HEAD", "OPTIONS", "POST", "PUT"];

    /// <summary>
    /// RFC 9110's <c>reason-phrase</c> production, https://www.rfc-editor.org/rfc/rfc9110#name-status-line —
    /// HTAB, SP, VCHAR and obs-text. What <c>Response</c>'s <c>statusText</c> is checked against.
    /// </summary>
    internal static bool IsReasonPhrase(string value)
    {
        foreach (var c in value)
        {
            var ok = c is '\t' or ' ' || (c >= 0x21 && c <= 0x7E) || (c >= 0x80 && c <= 0xFF);
            if (!ok)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// https://fetch.spec.whatwg.org/#null-body-status — a status a response may not carry a body with.
    /// </summary>
    internal static bool IsNullBodyStatus(int status) => status is 101 or 103 or 204 or 205 or 304;

    /// <summary>
    /// https://fetch.spec.whatwg.org/#redirect-status
    /// </summary>
    internal static bool IsRedirectStatus(int status) => status is 301 or 302 or 303 or 307 or 308;

    /// <summary>
    /// WebIDL's <c>unsigned short</c> conversion, https://webidl.spec.whatwg.org/#idl-unsigned-short: truncate
    /// towards zero and wrap modulo 2^16. It is what <c>ResponseInit</c>'s <c>status</c> and
    /// <c>Response.redirect</c>'s <c>status</c> are declared as, so <c>{ status: 65736 }</c> wraps to 200
    /// rather than being out of range.
    /// </summary>
    internal static int ToUnsignedShort(JsValue value)
    {
        var number = TypeConverter.ToNumber(value);
        if (!double.IsFinite(number))
        {
            return 0;
        }

        var truncated = System.Math.Truncate(number) % 65536.0;
        if (truncated < 0)
        {
            truncated += 65536.0;
        }

        return (int) truncated;
    }
}
#endif
