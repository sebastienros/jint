#if NET8_0_OR_GREATER
namespace Jint.WebApi.Encoding;

/// <summary>
/// The decoder every legacy single-byte encoding shares.
/// <para>
/// https://encoding.spec.whatwg.org/#single-byte-decoder
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// The algorithm, per byte: an ASCII byte is its own code point; anything else is looked up at
/// <c>byte − 0x80</c> in the encoding's index; a null there is an error, which is U+FFFD under the default
/// error mode and a <c>TypeError</c> under <c>fatal</c>.
/// </para>
/// <para>
/// It holds no state at all, which has two consequences worth naming. Streaming is free — a chunk can
/// never end mid-sequence, so <see cref="TryDecode"/> ignores <c>flush</c> and <see cref="Reset"/> has
/// nothing to throw away — and the output is exactly as many characters as there were bytes, so the buffer
/// is allocated once at the right size and never grown. Every index maps into the BMP, so a code point is
/// always one <see cref="char"/>.
/// </para>
/// </remarks>
internal sealed class SingleByteDecoderHandler : TextDecoderHandler
{
    /// <summary>U+FFFD, what the "replacement" error mode pushes for an error.</summary>
    private const char ReplacementCharacter = '\uFFFD';

    private readonly SingleByteIndex _index;
    private readonly bool _fatal;

    internal SingleByteDecoderHandler(SingleByteIndex index, bool fatal)
    {
        _index = index;
        _fatal = fatal;
    }

    internal override bool TryDecode(ReadOnlySpan<byte> input, bool flush, out ReadOnlySpan<char> output)
    {
        if (input.IsEmpty)
        {
            output = default;
            return true;
        }

        var index = EncodingTables.IndexFor(_index);
        var chars = new char[input.Length];

        for (var i = 0; i < input.Length; i++)
        {
            var b = input[i];
            if (b < 0x80)
            {
                chars[i] = (char) b;
                continue;
            }

            var codePoint = index[b - 0x80];
            if (codePoint == 0)
            {
                if (_fatal)
                {
                    output = default;
                    return false;
                }

                chars[i] = ReplacementCharacter;
                continue;
            }

            chars[i] = (char) codePoint;
        }

        output = chars;
        return true;
    }

    internal override void Reset()
    {
    }
}

/// <summary>
/// <c>x-user-defined</c>, whose decoder is an arithmetic mapping rather than an index.
/// <para>
/// https://encoding.spec.whatwg.org/#x-user-defined-decoder
/// </para>
/// </summary>
/// <remarks>
/// An ASCII byte is its own code point and every other byte becomes <c>0xF780 + byte − 0x80</c>, a private
/// use area code point. There is no error step, so <c>fatal</c> has nothing to fire on and this decoder
/// never fails — which is the point of the encoding: it round-trips arbitrary bytes through a string.
/// </remarks>
internal sealed class XUserDefinedDecoderHandler : TextDecoderHandler
{
    /// <summary>The code point https://encoding.spec.whatwg.org/#x-user-defined-decoder maps byte 0x80 to.</summary>
    private const int PrivateUseBase = 0xF780;

    internal override bool TryDecode(ReadOnlySpan<byte> input, bool flush, out ReadOnlySpan<char> output)
    {
        if (input.IsEmpty)
        {
            output = default;
            return true;
        }

        var chars = new char[input.Length];
        for (var i = 0; i < input.Length; i++)
        {
            var b = input[i];
            chars[i] = b < 0x80 ? (char) b : (char) (PrivateUseBase + b - 0x80);
        }

        output = chars;
        return true;
    }

    internal override void Reset()
    {
    }
}
#endif
