#if NET8_0_OR_GREATER
using System.Text;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using SystemEncoding = System.Text.Encoding;

namespace Jint.WebApi.Encoding;

/// <summary>
/// A <c>TextDecoder</c> instance, and the decoding state it carries between calls.
/// <para>
/// https://encoding.spec.whatwg.org/#interface-textdecoder
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// The specification's decoder state — the encoding's decoder instance, the I/O queue holding the bytes
/// of an incomplete sequence, the "do not flush" flag and the "BOM seen" flag — maps onto a BCL
/// <see cref="Decoder"/> plus two booleans. The <see cref="Decoder"/> is what holds the bytes of a
/// sequence split across two <c>decode(…, { stream: true })</c> calls, which is why it is created once
/// per stream and kept until a non-streaming call ends it.
/// </para>
/// <para>
/// <c>fatal</c> is the BCL's <see cref="DecoderExceptionFallback"/> and the default error mode is its
/// <see cref="DecoderReplacementFallback"/>, so the U+FFFD substitution follows the Unicode
/// "maximal subpart" recommendation that https://encoding.spec.whatwg.org/#error-mode also describes.
/// </para>
/// </remarks>
internal sealed class JsTextDecoder : ObjectInstance
{
    // One encoding object per (encoding, error mode). They are immutable and thread-safe; only the
    // Decoder they hand out is stateful, and that one is per instance.
    private static readonly SystemEncoding _utf8Replacement = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);
    private static readonly SystemEncoding _utf8Fatal = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly SystemEncoding _utf16LeReplacement = new UnicodeEncoding(bigEndian: false, byteOrderMark: false, throwOnInvalidBytes: false);
    private static readonly SystemEncoding _utf16LeFatal = new UnicodeEncoding(bigEndian: false, byteOrderMark: false, throwOnInvalidBytes: true);
    private static readonly SystemEncoding _utf16BeReplacement = new UnicodeEncoding(bigEndian: true, byteOrderMark: false, throwOnInvalidBytes: false);
    private static readonly SystemEncoding _utf16BeFatal = new UnicodeEncoding(bigEndian: true, byteOrderMark: false, throwOnInvalidBytes: true);

    /// <summary>U+FEFF, the code point "serialize I/O queue" drops when it leads the stream.</summary>
    private const char ByteOrderMark = '\uFEFF';

    private readonly SystemEncoding _encoding;
    private readonly string _encodingName;

    private Decoder? _decoder;
    private bool _doNotFlush;
    private bool _bomSeen;

    internal JsTextDecoder(Engine engine, string encodingName, bool fatal, bool ignoreBom) : base(engine, ObjectClass.Object)
    {
        Name = JsString.Create(encodingName);
        Fatal = fatal;
        IgnoreBom = ignoreBom;
        _encodingName = encodingName;
        _encoding = Resolve(encodingName, fatal);
    }

    /// <summary>The encoding's name, already ASCII-lowercase — https://encoding.spec.whatwg.org/#dom-textdecoder-encoding.</summary>
    internal JsString Name { get; }

    /// <summary>https://encoding.spec.whatwg.org/#dom-textdecoder-fatal</summary>
    internal bool Fatal { get; }

    /// <summary>https://encoding.spec.whatwg.org/#dom-textdecoder-ignorebom</summary>
    internal bool IgnoreBom { get; }

    /// <summary>
    /// https://encoding.spec.whatwg.org/#dom-textdecoder-decode, followed by "serialize I/O queue"
    /// (https://encoding.spec.whatwg.org/#concept-td-serialize) for the BOM handling.
    /// </summary>
    /// <param name="realm">The realm a <c>fatal</c> failure's <c>TypeError</c> belongs to.</param>
    /// <param name="input">The bytes to push onto the queue; empty when the argument was omitted.</param>
    /// <param name="stream">The <c>stream</c> option, which becomes the new "do not flush" flag.</param>
    internal JsString Decode(Realm realm, ReadOnlySpan<byte> input, bool stream)
    {
        // Step 1: a decode that follows a non-streaming one starts a new stream, which is what discarding
        // the decoder does — the next call builds a fresh one, holding no bytes over and having seen no BOM.
        if (!_doNotFlush)
        {
            _decoder = null;
            _bomSeen = false;
        }

        _doNotFlush = stream;

        var decoder = _decoder ??= _encoding.GetDecoder();

        char[] chars;
        int charCount;
        try
        {
            // GetCharCount does not advance the decoder, so this is the documented two-pass shape rather
            // than a double decode.
            chars = new char[decoder.GetCharCount(input, flush: !stream)];
            charCount = decoder.GetChars(input, chars, flush: !stream);
        }
        catch (DecoderFallbackException)
        {
            // The instance is reset rather than left holding a decoder whose state after a fallback
            // exception the BCL does not define, so the next decode starts a clean stream.
            _decoder = null;
            _doNotFlush = false;
            _bomSeen = false;
            Throw.TypeError(realm, "The encoded data was not valid for encoding " + _encodingName);
            return null!;
        }

        var decoded = chars.AsSpan(0, charCount);

        // "Serialize I/O queue" step 2.3: for utf-8, utf-16le and utf-16be — which is all three we
        // implement — the very first scalar value of the stream is dropped when it is U+FEFF. BOM seen is
        // set by looking, not by finding one, so at most one leading BOM is ever removed and only once per
        // stream, which is what makes a BOM split across two chunks work.
        if (!IgnoreBom && !_bomSeen && decoded.Length > 0)
        {
            _bomSeen = true;
            if (decoded[0] == ByteOrderMark)
            {
                decoded = decoded.Slice(1);
            }
        }

        return decoded.Length == 0 ? JsString.Empty : JsString.Create(decoded.ToString());
    }

    private static SystemEncoding Resolve(string encodingName, bool fatal)
    {
        if (string.Equals(encodingName, EncodingLabels.Utf16Le, StringComparison.Ordinal))
        {
            return fatal ? _utf16LeFatal : _utf16LeReplacement;
        }

        if (string.Equals(encodingName, EncodingLabels.Utf16Be, StringComparison.Ordinal))
        {
            return fatal ? _utf16BeFatal : _utf16BeReplacement;
        }

        return fatal ? _utf8Fatal : _utf8Replacement;
    }
}
#endif
