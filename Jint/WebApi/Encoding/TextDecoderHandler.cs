#if NET8_0_OR_GREATER
using System.Text;
using SystemEncoding = System.Text.Encoding;

namespace Jint.WebApi.Encoding;

/// <summary>
/// An instance of one encoding's <a href="https://encoding.spec.whatwg.org/#decoder">decoder</a>, together
/// with whatever state it carries between the chunks of a stream.
/// </summary>
/// <remarks>
/// <para>
/// The specification runs a decoder byte by byte and lets its handler answer <i>finished</i>, one or more
/// code points, or <i>error</i>. This seam is that algorithm applied to a whole chunk: a
/// <see langword="false"/> return is <i>error</i> reaching a decoder whose error mode is
/// <c>fatal</c> — the only case a caller has to do anything about, since the <c>replacement</c> error mode
/// substitutes U+FFFD inside the handler and keeps going.
/// </para>
/// <para>
/// Three implementations sit behind it: <see cref="BclDecoderHandler"/> for UTF-8,
/// <see cref="Utf16DecoderHandler"/> for the two UTF-16 encodings, and
/// <see cref="SingleByteDecoderHandler"/> / <see cref="XUserDefinedDecoderHandler"/> for the legacy ones,
/// which are stateless because a byte is a whole code point there. Keeping the seam uniform is what lets
/// <see cref="JsTextDecoder"/> hold one decoding algorithm and no branches per call.
/// </para>
/// </remarks>
internal abstract class TextDecoderHandler
{
    /// <summary>
    /// Builds the decoder an encoding's <c>decode()</c> runs, honouring the error mode it was constructed
    /// with.
    /// </summary>
    internal static TextDecoderHandler Create(in EncodingEntry encoding, bool fatal) => encoding.Kind switch
    {
        EncodingKind.SingleByte => new SingleByteDecoderHandler(encoding.Index, fatal),
        EncodingKind.XUserDefined => new XUserDefinedDecoderHandler(),
        EncodingKind.Utf16Le => new Utf16DecoderHandler(bigEndian: false, fatal),
        EncodingKind.Utf16Be => new Utf16DecoderHandler(bigEndian: true, fatal),
        _ => new BclDecoderHandler(fatal),
    };

    /// <summary>
    /// Decodes one chunk, appending to whatever an earlier streaming chunk left pending.
    /// </summary>
    /// <param name="input">The chunk's bytes; empty when <c>decode()</c> was called with no argument.</param>
    /// <param name="flush">
    /// Whether the stream ends here, which is <c>decode()</c> being called without <c>stream</c>. A
    /// trailing incomplete sequence is resolved — replaced or reported — only then.
    /// </param>
    /// <param name="output">The decoded scalar values, valid until the next call.</param>
    /// <returns><see langword="false"/> when the error mode is <c>fatal</c> and the input is in error.</returns>
    internal abstract bool TryDecode(ReadOnlySpan<byte> input, bool flush, out ReadOnlySpan<char> output);

    /// <summary>
    /// Throws away everything the decoder is holding, which is what starting a new stream means — and what
    /// a <c>fatal</c> failure has to do, since the state a decoder is left in after one is not defined.
    /// </summary>
    internal abstract void Reset();
}

/// <summary>
/// UTF-8, decoded by the BCL.
/// <para>
/// https://encoding.spec.whatwg.org/#utf-8-decoder
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="Decoder"/> is what holds the bytes of a sequence split across two
/// <c>decode(…, { stream: true })</c> calls, so it is created once per stream and kept until a
/// non-streaming call ends it. <c>fatal</c> is <see cref="DecoderExceptionFallback"/> and the default error
/// mode is <see cref="DecoderReplacementFallback"/>, so the U+FFFD substitution follows the Unicode
/// "maximal subpart" recommendation that https://encoding.spec.whatwg.org/#error-mode also describes.
/// </para>
/// <para>
/// The two UTF-16 encodings used to run through here as well, and no longer do: see
/// <see cref="Utf16DecoderHandler"/> for the end-of-queue step that made a <see cref="Decoder"/> the wrong
/// shape for them.
/// </para>
/// </remarks>
internal sealed class BclDecoderHandler : TextDecoderHandler
{
    // One encoding object per error mode. They are immutable and thread-safe; only the Decoder they hand
    // out is stateful, and that one is per instance.
    private static readonly SystemEncoding _utf8Replacement = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);
    private static readonly SystemEncoding _utf8Fatal = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private readonly SystemEncoding _encoding;
    private Decoder? _decoder;

    internal BclDecoderHandler(bool fatal)
    {
        _encoding = fatal ? _utf8Fatal : _utf8Replacement;
    }

    internal override bool TryDecode(ReadOnlySpan<byte> input, bool flush, out ReadOnlySpan<char> output)
    {
        var decoder = _decoder ??= _encoding.GetDecoder();

        try
        {
            // GetCharCount does not advance the decoder, so this is the documented two-pass shape rather
            // than a double decode.
            var chars = new char[decoder.GetCharCount(input, flush)];
            var charCount = decoder.GetChars(input, chars, flush);
            output = chars.AsSpan(0, charCount);
            return true;
        }
        catch (DecoderFallbackException)
        {
            output = default;
            return false;
        }
    }

    internal override void Reset() => _decoder = null;
}
#endif
