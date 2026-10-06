using Jint.HtmlParser.Css.Syntax;
using Jint.WebApi.Encoding;
using Jint.WebApi.Fetch;

namespace Jint.Browser.Runtime.Parsing;

/// <summary>
/// CSS Syntax §3.2 "decode a stylesheet's stream of bytes": the parser determines the fallback encoding
/// from the transport label, the <c>@charset "…";</c> declaration and the environment encoding, then the
/// Encoding Standard's "decode" lets a byte order mark override it.
/// </summary>
/// <remarks>
/// Labels resolve through the engine's WHATWG table. The seven legacy multi-byte encodings the engine does
/// not implement count as failure, so resolution falls through to the next source rather than decoding as
/// something else — the same deviation <c>TextDecoder</c> makes.
/// </remarks>
internal static class CssStyleSheetDecoding
{
    private static readonly Func<string, string?> GetEncoding = static label
        => EncodingLabels.TryLookup(label, out var entry) && entry.Kind != EncodingKind.Unsupported
            ? entry.LowercasedName : null;

    /// <param name="bytes">The response body.</param>
    /// <param name="contentType">The response's <c>Content-Type</c>, whose <c>charset</c> is the protocol label.</param>
    /// <param name="environmentEncoding">The referring document's or parent sheet's encoding.</param>
    /// <returns>The decoded text and the encoding it was decoded with, which an imported sheet inherits.</returns>
    internal static (string Text, string Encoding) Decode(ReadOnlySpan<byte> bytes, string? contentType,
        string? environmentEncoding)
    {
        var protocolLabel = contentType is null ? null : MimeType.Parse(contentType)?.GetParameter("charset");
        var fallback = CssStyleSheetEncoding.DetermineFallback(bytes, protocolLabel, environmentEncoding, GetEncoding);
        if (!EncodingLabels.TryLookup(fallback, out var encoding) || encoding.Kind == EncodingKind.Unsupported)
            encoding = EncodingLabels.Utf8Encoding;

        // https://encoding.spec.whatwg.org/#decode: BOM sniff wins, and the BOM is consumed.
        var bomLength = 0;
        if (bytes is [0xEF, 0xBB, 0xBF, ..]) { encoding = EncodingLabels.Utf8Encoding; bomLength = 3; }
        else if (bytes is [0xFE, 0xFF, ..] && EncodingLabels.TryLookup(EncodingLabels.Utf16Be, out var be)) { encoding = be; bomLength = 2; }
        else if (bytes is [0xFF, 0xFE, ..] && EncodingLabels.TryLookup(EncodingLabels.Utf16Le, out var le)) { encoding = le; bomLength = 2; }

        if (encoding.Kind == EncodingKind.Replacement)
            return (bytes.Length == 0 ? string.Empty : "\uFFFD", encoding.LowercasedName);
        TextDecoderHandler.Create(in encoding, fatal: false).TryDecode(bytes[bomLength..], flush: true, out var text);
        return (text.ToString(), encoding.LowercasedName);
    }
}
