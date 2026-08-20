#if NET8_0_OR_GREATER
using System.Text;
using Jint.Native;
using Jint.Runtime;
using Jint.WebApi.Streams;
using SystemEncoding = System.Text.Encoding;

namespace Jint.WebApi.Encoding;

/// <summary>
/// A <c>TextEncoderStream</c> instance: a UTF-8 encoder, the leading surrogate carried between chunks, and
/// the transform stream that runs them.
/// <para>
/// https://encoding.spec.whatwg.org/#interface-textencoderstream
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// The one piece of state is the specification's <c>leading surrogate</c> slot, and it is the whole reason
/// this is not <c>TextEncoder.encode</c> in a loop. A chunk is a <c>DOMString</c> rather than a
/// <c>USVString</c> precisely so that a surrogate pair split across two chunks can be reassembled: a high
/// surrogate at the end of a chunk is held back, and the next chunk either completes it — one astral scalar
/// value — or does not, in which case the held surrogate becomes U+FFFD and the code unit that failed to
/// complete it is processed from the start.
/// </para>
/// <para>
/// Every other lone surrogate is U+FFFD, which is what .NET's UTF-8 encoder does with its replacement
/// fallback, so the conversion and the encoding happen in one pass over the rest of the chunk.
/// </para>
/// </remarks>
internal sealed class JsTextEncoderStream : JsGenericTransformStream
{
    /// <summary>U+FFFD REPLACEMENT CHARACTER in UTF-8, the specification's « 0xEF, 0xBF, 0xBD ».</summary>
    private static ReadOnlySpan<byte> ReplacementCharacterUtf8 => [0xEF, 0xBF, 0xBD];

    /// <summary>
    /// https://encoding.spec.whatwg.org/#textencoderstream-pending-high-surrogate — null, or the leading
    /// surrogate the previous chunk ended with.
    /// </summary>
    private char? _leadingSurrogate;

    internal JsTextEncoderStream(Engine engine, Realm realm) : base(engine, realm)
    {
    }

    /// <summary>
    /// "Encode and enqueue a chunk" — https://encoding.spec.whatwg.org/#encode-and-enqueue-a-chunk,
    /// together with "convert code unit to scalar value"
    /// (https://encoding.spec.whatwg.org/#convert-code-unit-to-scalar-value), which is what the carried
    /// surrogate below implements.
    /// </summary>
    internal void EncodeAndEnqueue(JsValue chunk)
    {
        var input = TypeConverter.ToString(chunk);
        var text = input.AsSpan();

        // At most one scalar value is produced before the chunk's own text: either the pair the carried
        // surrogate completes (four bytes) or the U+FFFD it becomes (three).
        Span<byte> carried = stackalloc byte[4];
        var carriedLength = 0;

        if (_leadingSurrogate is { } leadingSurrogate)
        {
            _leadingSurrogate = null;

            if (text.Length > 0 && char.IsLowSurrogate(text[0]))
            {
                // "Return a scalar value from surrogates given leadingSurrogate and item": the pair is one
                // scalar value, and the trailing surrogate is consumed with it.
                carriedLength = new Rune(leadingSurrogate, text[0]).EncodeToUtf8(carried);
                text = text.Slice(1);
            }
            else
            {
                // "Restore item to input" — the code unit is not consumed, so it is encoded below as
                // whatever it is — "and return U+FFFD".
                ReplacementCharacterUtf8.CopyTo(carried);
                carriedLength = ReplacementCharacterUtf8.Length;
            }
        }

        // A leading surrogate at the very end of the chunk is held rather than replaced: it may be the
        // first half of a pair the next chunk completes. Nothing else can be pending, since any earlier
        // leading surrogate was resolved by the code unit that followed it.
        if (text.Length > 0 && char.IsHighSurrogate(text[text.Length - 1]))
        {
            _leadingSurrogate = text[text.Length - 1];
            text = text.Slice(0, text.Length - 1);
        }

        var byteCount = carriedLength + SystemEncoding.UTF8.GetByteCount(text);
        if (byteCount == 0)
        {
            // "If output is not empty" — a chunk that produced nothing enqueues nothing, which is how the
            // empty string and a chunk that is a lone leading surrogate behave.
            return;
        }

        var bytes = new byte[byteCount];
        carried.Slice(0, carriedLength).CopyTo(bytes);
        SystemEncoding.UTF8.GetBytes(text, bytes.AsSpan(carriedLength));

        Enqueue(Realm.Intrinsics.Uint8Array.Construct(bytes));
    }

    /// <summary>
    /// "Encode and flush" — https://encoding.spec.whatwg.org/#encode-and-flush. A leading surrogate the
    /// stream ends on has nothing left to complete it, so it is the replacement character.
    /// </summary>
    internal void EncodeAndFlush()
    {
        if (_leadingSurrogate is null)
        {
            return;
        }

        _leadingSurrogate = null;
        Enqueue(Realm.Intrinsics.Uint8Array.Construct(ReplacementCharacterUtf8));
    }
}
#endif
