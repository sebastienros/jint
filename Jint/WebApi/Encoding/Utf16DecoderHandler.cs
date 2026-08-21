#if NET8_0_OR_GREATER
namespace Jint.WebApi.Encoding;

/// <summary>
/// The decoder <c>utf-16le</c> and <c>utf-16be</c> share, which is one algorithm parameterised by
/// endianness.
/// <para>
/// https://encoding.spec.whatwg.org/#shared-utf-16-decoder
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// It is written out rather than delegated to <see cref="System.Text.UnicodeEncoding"/> because of the
/// end-of-queue step, which the BCL has no equivalent of: "If UTF-16 lead byte is non-null <b>or</b> UTF-16
/// lead surrogate is non-null, set UTF-16 lead byte and UTF-16 lead surrogate to null, and return error" —
/// <i>one</i> error however many of the two are pending. A <see cref="System.Text.Decoder"/> reports the
/// dangling byte and the unpaired lead surrogate separately, so <c>[0x00, 0xd8, 0x00]</c> as <c>utf-16le</c>
/// came back as two U+FFFD where the standard asks for one.
/// </para>
/// <para>
/// The two pieces of state are the specification's own: the first byte of a code unit whose second byte has
/// not arrived, and a lead surrogate whose trail has not. Both survive a
/// <c>decode(…, { stream: true })</c> call and are what <see cref="Reset"/> throws away.
/// </para>
/// </remarks>
internal sealed class Utf16DecoderHandler : TextDecoderHandler
{
    /// <summary>U+FFFD, what the "replacement" error mode pushes for an error.</summary>
    private const char ReplacementCharacter = '\uFFFD';

    /// <summary>The specification's <c>null</c> for the two pieces of state, which are byte/code-unit valued.</summary>
    private const int None = -1;

    private readonly bool _bigEndian;
    private readonly bool _fatal;

    private int _leadByte = None;
    private int _leadSurrogate = None;

    internal Utf16DecoderHandler(bool bigEndian, bool fatal)
    {
        _bigEndian = bigEndian;
        _fatal = fatal;
    }

    internal override bool TryDecode(ReadOnlySpan<byte> input, bool flush, out ReadOnlySpan<char> output)
    {
        // Two bytes make one code unit, and a code unit yields at most one character more than the last one
        // did: the only step that emits two is the one consuming a pending lead surrogate, and the step that
        // made it pending emitted none. Two spare slots cover a lead surrogate carried in from the previous
        // chunk and the end-of-queue error, so the buffer is sized once and never grown.
        var chars = new char[(input.Length + 1) / 2 + 3];
        var count = 0;

        for (var i = 0; i < input.Length; i++)
        {
            // Step 3: the first byte of a code unit is held until its partner arrives.
            if (_leadByte == None)
            {
                _leadByte = input[i];
                continue;
            }

            // Step 4, and step 5 sets the lead byte back to null.
            var codeUnit = _bigEndian ? (_leadByte << 8) + input[i] : (input[i] << 8) + _leadByte;
            _leadByte = None;

            // Step 6: a lead surrogate is waiting for a trail.
            if (_leadSurrogate != None)
            {
                var leadSurrogate = _leadSurrogate;
                _leadSurrogate = None;

                if (IsTrailSurrogate(codeUnit))
                {
                    // Step 6.3 returns the astral code point, which is these two units in UTF-16.
                    chars[count++] = (char) leadSurrogate;
                    chars[count++] = (char) codeUnit;
                    continue;
                }

                // Step 6.4 restores this code unit's bytes to the queue and reports the unpaired lead
                // surrogate. With the lead surrogate now null the restored bytes re-form the very same code
                // unit, so the steps below take it rather than the loop reading the bytes a second time.
                if (_fatal)
                {
                    output = default;
                    return false;
                }

                chars[count++] = ReplacementCharacter;
            }

            // Step 7: a lead surrogate waits for the next code unit.
            if (IsLeadSurrogate(codeUnit))
            {
                _leadSurrogate = codeUnit;
                continue;
            }

            // Step 8: a trail surrogate with nothing leading it is an error.
            if (IsTrailSurrogate(codeUnit))
            {
                if (_fatal)
                {
                    output = default;
                    return false;
                }

                chars[count++] = ReplacementCharacter;
                continue;
            }

            // Step 9.
            chars[count++] = (char) codeUnit;
        }

        // Step 1, the end-of-queue step: either piece of state being non-null is one error between them.
        if (flush && (_leadByte != None || _leadSurrogate != None))
        {
            _leadByte = None;
            _leadSurrogate = None;

            if (_fatal)
            {
                output = default;
                return false;
            }

            chars[count++] = ReplacementCharacter;
        }

        output = chars.AsSpan(0, count);
        return true;
    }

    internal override void Reset()
    {
        _leadByte = None;
        _leadSurrogate = None;
    }

    private static bool IsLeadSurrogate(int codeUnit) => codeUnit is >= 0xD800 and <= 0xDBFF;

    private static bool IsTrailSurrogate(int codeUnit) => codeUnit is >= 0xDC00 and <= 0xDFFF;
}
#endif
