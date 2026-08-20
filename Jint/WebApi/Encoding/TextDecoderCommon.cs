#if NET8_0_OR_GREATER
using System.Text;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using SystemEncoding = System.Text.Encoding;

namespace Jint.WebApi.Encoding;

/// <summary>
/// The <c>TextDecoderCommon</c> mixin: the encoding, the error mode, the ignore-BOM flag, the encoding's
/// decoder and the I/O queue holding the bytes of an incomplete sequence.
/// <para>
/// https://encoding.spec.whatwg.org/#textdecodercommon
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// Both interfaces that include the mixin — <c>TextDecoder</c> and <c>TextDecoderStream</c> — keep their
/// state here rather than each carrying a copy, which is what makes the two decode the same bytes to the
/// same string by construction. The specification's decoder state maps onto a BCL <see cref="Decoder"/>
/// plus two booleans: the <see cref="Decoder"/> is what holds the bytes of a sequence split across two
/// calls, which is why it is created once per stream and kept until a non-streaming call ends it.
/// </para>
/// <para>
/// <c>fatal</c> is the BCL's <see cref="DecoderExceptionFallback"/> and the default error mode is its
/// <see cref="DecoderReplacementFallback"/>, so the U+FFFD substitution follows the Unicode
/// "maximal subpart" recommendation that https://encoding.spec.whatwg.org/#error-mode also describes.
/// </para>
/// </remarks>
internal sealed class TextDecoderCommon
{
    // One encoding object per (encoding, error mode). They are immutable and thread-safe; only the
    // Decoder they hand out is stateful, and that one is per instance.
    private static readonly SystemEncoding _utf8Replacement = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);
    private static readonly SystemEncoding _utf8Fatal = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly SystemEncoding _utf16LeReplacement = new UnicodeEncoding(bigEndian: false, byteOrderMark: false, throwOnInvalidBytes: false);
    private static readonly SystemEncoding _utf16LeFatal = new UnicodeEncoding(bigEndian: false, byteOrderMark: false, throwOnInvalidBytes: true);
    private static readonly SystemEncoding _utf16BeReplacement = new UnicodeEncoding(bigEndian: true, byteOrderMark: false, throwOnInvalidBytes: false);
    private static readonly SystemEncoding _utf16BeFatal = new UnicodeEncoding(bigEndian: true, byteOrderMark: false, throwOnInvalidBytes: true);

    private static readonly JsString _fatal = new("fatal");
    private static readonly JsString _ignoreBom = new("ignoreBOM");

    /// <summary>U+FEFF, the code point "serialize I/O queue" drops when it leads the stream.</summary>
    private const char ByteOrderMark = (char) 0xFEFF;

    private readonly SystemEncoding _encoding;
    private readonly string _encodingName;

    private Decoder? _decoder;
    private bool _doNotFlush;
    private bool _bomSeen;

    internal TextDecoderCommon(string encodingName, bool fatal, bool ignoreBom)
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
    /// The <c>(label, options)</c> pair both constructors take:
    /// <c>constructor(optional DOMString label = "utf-8", optional TextDecoderOptions options = {})</c>.
    /// </summary>
    /// <remarks>
    /// The two arguments are converted first and the constructor steps run after, which is the order WebIDL
    /// specifies and the reason a bad <c>label</c> raises its <c>RangeError</c> only once <c>options</c> has
    /// been read — a getter on <c>options</c> runs even when the label is nonsense.
    /// </remarks>
    /// <param name="realm">The realm the conversion's exceptions belong to.</param>
    /// <param name="label">The first argument, an encoding label.</param>
    /// <param name="options">The second argument, a <c>TextDecoderOptions</c> dictionary.</param>
    /// <param name="interfaceName">Which of the two interfaces is being constructed, for the messages.</param>
    internal static TextDecoderCommon Create(Realm realm, JsValue label, JsValue options, string interfaceName)
    {
        // `optional DOMString label = "utf-8"`: an omitted argument and an explicitly passed undefined
        // both take the default.
        var labelText = label.IsUndefined() ? EncodingLabels.Utf8 : TypeConverter.ToString(label);

        // The dictionary conversion, https://webidl.spec.whatwg.org/#es-dictionary: undefined and null
        // are the empty dictionary, anything else that is not an object is a TypeError, and the members
        // are read in lexicographic order — "fatal" before "ignoreBOM".
        var fatal = false;
        var ignoreBom = false;
        if (!options.IsUndefined() && !options.IsNull())
        {
            if (options is not ObjectInstance optionsObject)
            {
                Throw.TypeError(realm, interfaceName + ": options must be an object");
                return null!;
            }

            fatal = TypeConverter.ToBoolean(optionsObject.Get(_fatal));
            ignoreBom = TypeConverter.ToBoolean(optionsObject.Get(_ignoreBom));
        }

        // Step 1 and 2: get an encoding from the label, and refuse anything the table does not name.
        var encoding = EncodingLabels.Lookup(labelText);
        if (encoding is null)
        {
            Throw.RangeError(realm, interfaceName + ": the encoding label provided ('" + labelText + "') is invalid");
        }

        return new TextDecoderCommon(encoding!, fatal, ignoreBom);
    }

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
