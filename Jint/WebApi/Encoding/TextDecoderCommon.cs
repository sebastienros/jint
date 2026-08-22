#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;

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
/// same string by construction, and what gives <c>TextDecoderStream</c> every label <c>TextDecoder</c>
/// resolves — the legacy single-byte table included.
/// </para>
/// <para>
/// The specification's decoder state — the encoding's decoder instance, the bytes of an incomplete
/// sequence, the "do not flush" flag and the "BOM seen" flag — maps onto a
/// <see cref="TextDecoderHandler"/> plus two booleans. The handler is what holds anything carried from one
/// <c>decode(…, { stream: true })</c> call to the next, which is why it is reset only when a non-streaming
/// call ends the stream.
/// </para>
/// </remarks>
internal sealed class TextDecoderCommon
{
    private static readonly JsString _fatal = new("fatal");
    private static readonly JsString _ignoreBom = new("ignoreBOM");

    /// <summary>U+FEFF, the code point "serialize I/O queue" drops when it leads the stream.</summary>
    private const char ByteOrderMark = (char) 0xFEFF;

    private readonly TextDecoderHandler _handler;
    private readonly string _encodingName;
    private readonly bool _stripsByteOrderMark;

    private bool _doNotFlush;
    private bool _bomSeen;

    internal TextDecoderCommon(in EncodingEntry encoding, bool fatal, bool ignoreBom)
    {
        Name = JsString.Create(encoding.Name);
        Fatal = fatal;
        IgnoreBom = ignoreBom;
        _encodingName = encoding.Name;
        _handler = TextDecoderHandler.Create(in encoding, fatal);

        // "Serialize I/O queue" step 2.3 applies to UTF-8 and UTF-16BE/LE and to nothing else, so a legacy
        // decoder hands a leading U+FEFF straight through. That is not observable for the legacy encodings
        // implemented here — no single-byte index maps a byte to U+FEFF and x-user-defined cannot produce
        // one either — which is exactly why the condition is the specification's own rather than something
        // derived from what happens to be decodable today: a legacy multi-byte decoder can produce a U+FEFF,
        // and would silently lose it.
        _stripsByteOrderMark = encoding.Kind is EncodingKind.Utf8 or EncodingKind.Utf16Le or EncodingKind.Utf16Be;
    }

    /// <summary>The encoding's name, already ASCII-lowercase — https://encoding.spec.whatwg.org/#dom-textdecoder-encoding.</summary>
    internal JsString Name { get; }

    /// <summary>https://encoding.spec.whatwg.org/#dom-textdecoder-fatal</summary>
    internal bool Fatal { get; }

    /// <summary>https://encoding.spec.whatwg.org/#dom-textdecoder-ignorebom</summary>
    internal bool IgnoreBom { get; }

    /// <summary>
    /// The decoder "set up a text decoder stream"
    /// (https://encoding.spec.whatwg.org/#set-up-a-text-decoder-stream) builds when every one of its
    /// optional arguments is left at its default: UTF-8, the "replacement" error mode, and
    /// <c>ignoreBOM</c> false. That is the state <c>new TextDecoderStream()</c> produces, and it is what
    /// https://w3c.github.io/FileAPI/#dom-blob-textstream step 3 — "set up decoder with UTF-8" — asks for.
    /// </summary>
    /// <remarks>
    /// No label is resolved: the caller names the encoding, so there is nothing here that could fail and no
    /// <c>RangeError</c> to raise.
    /// </remarks>
    internal static TextDecoderCommon Utf8() => new(in EncodingLabels.Utf8Encoding, fatal: false, ignoreBom: false);

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

        // Step 1: get an encoding from the label, and refuse anything the table does not name.
        if (!EncodingLabels.TryLookup(labelText, out var encoding))
        {
            Throw.RangeError(realm, interfaceName + ": the encoding label provided ('" + labelText + "') is invalid");
        }

        // Step 2: a label for the replacement encoding is a RangeError too. The encoding exists to keep a
        // charset the server and the client disagree about from being decoded at all, so handing out a
        // decoder for it — even one that only ever errors — is precisely what must not happen.
        if (encoding.Kind == EncodingKind.Replacement)
        {
            Throw.RangeError(realm, interfaceName + ": the encoding label provided ('" + labelText + "') is a label for the replacement encoding");
        }

        // Jint's own deviation, kept apart from the two steps above because it is not one of them: the
        // legacy multi-byte encodings are named by the table but not implemented.
        if (encoding.Kind == EncodingKind.Unsupported)
        {
            Throw.RangeError(realm, interfaceName + ": the encoding '" + encoding.Name + "' is not supported");
        }

        return new TextDecoderCommon(in encoding, fatal, ignoreBom);
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
        // Step 1: a decode that follows a non-streaming one starts a new stream, which is what resetting
        // the handler does — the next call holds no bytes over and has seen no BOM.
        if (!_doNotFlush)
        {
            _handler.Reset();
            _bomSeen = false;
        }

        _doNotFlush = stream;

        if (!_handler.TryDecode(input, flush: !stream, out var decoded))
        {
            // The instance is reset rather than left holding a decoder whose state after a fatal failure
            // is not defined, so the next decode starts a clean stream.
            _handler.Reset();
            _doNotFlush = false;
            _bomSeen = false;
            Throw.TypeError(realm, "The encoded data was not valid for encoding " + _encodingName);
            return null!;
        }

        // "Serialize I/O queue" step 2.3: for utf-8, utf-16le and utf-16be — and for nothing else, per the
        // step's own condition — the very first scalar value of the stream is dropped when it is U+FEFF.
        // BOM seen is set by looking, not by finding one, so at most one leading BOM is ever removed and
        // only once per stream, which is what makes a BOM split across two chunks work.
        if (_stripsByteOrderMark && !IgnoreBom && !_bomSeen && decoded.Length > 0)
        {
            _bomSeen = true;
            if (decoded[0] == ByteOrderMark)
            {
                decoded = decoded.Slice(1);
            }
        }

        return decoded.Length == 0 ? JsString.Empty : JsString.Create(decoded.ToString());
    }
}
#endif
