#if NET8_0_OR_GREATER
using System.Text;
using SystemEncoding = System.Text.Encoding;
using Jint.WebApi.Base64;
using Jint.WebApi.Url.Parsing;

namespace Jint.WebApi.Fetch;

/// <summary>
/// What a <c>data:</c> URL carries, https://fetch.spec.whatwg.org/#data-url-struct — a MIME type and a body.
/// </summary>
/// <param name="Body">The bytes, percent-decoded and then base64-decoded when the URL said so.</param>
/// <param name="MimeType">
/// The type the URL claimed for them, already defaulted to <c>text/plain;charset=US-ASCII</c> when it
/// claimed one that does not parse.
/// </param>
internal readonly record struct DataUrlContent(byte[] Body, MimeType MimeType);

/// <summary>
/// The <c>data:</c> URL processor, https://fetch.spec.whatwg.org/#data-url-processor.
/// </summary>
/// <remarks>
/// <para>
/// <b>It reaches no transport and it is not a policy decision.</b> A <c>data:</c> URL names bytes that are
/// already in hand, so — exactly as <c>BlobUrlFetch</c> does for the <c>blob</c> arm of scheme fetch — there
/// is nothing here for an <c>HttpClient</c>, a URL filter, a redirect budget or an observer to decide. What a
/// caller still owes is its own size ceiling: the body is as large as the URL, and a URL can be very large.
/// </para>
/// <para>
/// <b>It is deliberately not <c>Uri.UnescapeDataString</c> plus <see cref="Convert"/>.</b> The two halves of
/// the algorithm are WHATWG's own and neither BCL method is it: percent-decoding leaves a lone <c>%</c>
/// alone where <c>Uri.UnescapeDataString</c> throws on one, and forgiving-base64 accepts padding and
/// whitespace <see cref="Convert.FromBase64String"/> refuses — <c>data:;base64,YQ</c> decodes in a browser
/// and throws there. <see cref="ForgivingBase64"/> already had that half.
/// </para>
/// </remarks>
internal static class DataUrl
{
    /// <summary>The scheme, as the URL parser records it.</summary>
    internal const string Scheme = "data";

    /// <summary>The MIME type step 14 falls back to when the URL claimed one that does not parse.</summary>
    private const string DefaultMimeType = "text/plain;charset=US-ASCII";

    /// <summary>Whether <paramref name="url"/> is a <c>data:</c> URL.</summary>
    internal static bool Is(UrlRecord url) => string.Equals(url.Scheme, Scheme, StringComparison.Ordinal);

    /// <summary>
    /// Runs the processor over <paramref name="url"/>, answering <see langword="false"/> for the
    /// specification's failure — a URL with no comma, or a base64 payload that does not decode.
    /// </summary>
    internal static bool TryProcess(UrlRecord url, out DataUrlContent content)
    {
        content = default;

        // Steps 2 and 3: the input is the serialization with the fragment excluded, past "data:". The
        // fragment is therefore no part of the body, however much it looks like one — the URL parser split
        // it off at the first "#" and it stays split off here.
        var input = url.Serialize(excludeFragment: true);
        if (!input.StartsWith(Scheme + ":", StringComparison.Ordinal))
        {
            return false;
        }

        input = input[(Scheme.Length + 1)..];

        // Steps 5 to 8: everything up to the first "," is the MIME type; no "," at all is failure.
        var comma = input.IndexOf(',', StringComparison.Ordinal);
        if (comma < 0)
        {
            return false;
        }

        // Step 6.
        var mimeType = input[..comma].Trim(AsciiWhitespace);

        // Steps 9 and 10: string percent-decode, which is UTF-8 encode followed by percent-decode.
        var body = PercentEncoding.Decode(SystemEncoding.UTF8.GetBytes(input[(comma + 1)..]));

        // Step 11: ";" then optional spaces then a case-insensitive "base64", and only at the very end.
        if (Base64Suffix(mimeType) is { } kept)
        {
            if (!ForgivingBase64.TryDecode(IsomorphicDecode(body), out var decoded))
            {
                return false;
            }

            body = decoded;
            mimeType = mimeType[..kept];
        }

        // Step 12: a MIME type that is only parameters gets the type the specification gives it.
        if (mimeType.StartsWith(';'))
        {
            mimeType = "text/plain" + mimeType;
        }

        // Steps 13 and 14.
        content = new DataUrlContent(body, MimeType.Parse(mimeType) ?? MimeType.Parse(DefaultMimeType)!);
        return true;
    }

    /// <summary>https://infra.spec.whatwg.org/#ascii-whitespace, as the trim set step 6 uses.</summary>
    private static readonly char[] AsciiWhitespace = ['\t', '\n', '\f', '\r', ' '];

    /// <summary>
    /// The length step 11 leaves the MIME type at when it ends with the base64 marker — the "base64", the
    /// spaces before it and the ";" all removed — or <see langword="null"/> when it does not end with one.
    /// </summary>
    private static int? Base64Suffix(string mimeType)
    {
        // Step 11's condition and its steps 11.4 to 11.6 read the same suffix from opposite ends; measuring
        // it once is what keeps them from disagreeing about how many spaces there were.
        const string Marker = "base64";

        if (mimeType.Length < Marker.Length + 1
            || !mimeType.AsSpan(mimeType.Length - Marker.Length).Equals(Marker, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var end = mimeType.Length - Marker.Length;
        while (end > 0 && mimeType[end - 1] == ' ')
        {
            end--;
        }

        return end > 0 && mimeType[end - 1] == ';' ? end - 1 : null;
    }

    /// <summary>https://infra.spec.whatwg.org/#isomorphic-decode — one code point per byte.</summary>
    private static string IsomorphicDecode(byte[] bytes)
    {
        return string.Create(bytes.Length, bytes, static (span, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                span[i] = (char) source[i];
            }
        });
    }
}
#endif
