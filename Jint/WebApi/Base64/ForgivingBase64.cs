#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;

namespace Jint.WebApi.Base64;

/// <summary>
/// "Forgiving-base64 decode", https://infra.spec.whatwg.org/#forgiving-base64-decode — the algorithm
/// behind <c>atob</c>.
/// </summary>
/// <remarks>
/// <para>
/// It is deliberately hand-written rather than delegated to <see cref="Convert.FromBase64String"/>, which
/// is not the same function: <c>Convert</c> accepts only whitespace it happens to skip, insists on
/// padding to a multiple of four, and rejects several inputs the algorithm accepts — <c>"YQ"</c> and
/// <c>"YWJj\ndA=="</c> both decode here and both throw there. The differences run in the other direction
/// too, so neither "try Convert first" nor "clean the string up and hand it to Convert" is correct.
/// </para>
/// <para>
/// The padding rule is the subtle part and is exactly as written: trailing <c>=</c> is removed <b>only</b>
/// when the whitespace-stripped length is already a multiple of four, and at most two are removed. So
/// <c>"YQ=="</c> decodes, while <c>"YQ="</c> (length 3) keeps its <c>=</c>, fails the alphabet check and
/// is a failure — which is what a browser does.
/// </para>
/// </remarks>
internal static class ForgivingBase64
{
    /// <summary>
    /// Decodes <paramref name="data"/>, or reports the specification's "failure", which <c>atob</c> turns
    /// into an <c>InvalidCharacterError</c>.
    /// </summary>
    internal static bool TryDecode(string data, [NotNullWhen(true)] out byte[]? output)
    {
        output = null;

        // Step 1, and the part of steps 2 and 3 that needs the whitespace-stripped length.
        var length = 0;
        foreach (var c in data)
        {
            if (!IsAsciiWhitespace(c))
            {
                length++;
            }
        }

        // Step 2: only a length that already divides by four sheds its padding, and never more than two
        // code points of it.
        if (length % 4 == 0)
        {
            var padding = 0;
            for (var i = data.Length - 1; i >= 0 && padding < 2; i--)
            {
                var c = data[i];
                if (IsAsciiWhitespace(c))
                {
                    continue;
                }

                if (c != '=')
                {
                    break;
                }

                padding++;
            }

            length -= padding;
        }

        // Step 3.
        if (length % 4 == 1)
        {
            return false;
        }

        var result = new byte[length / 4 * 3 + (length % 4) * 3 / 4];

        var buffer = 0;
        var bits = 0;
        var written = 0;
        var consumed = 0;
        foreach (var c in data)
        {
            if (IsAsciiWhitespace(c))
            {
                continue;
            }

            if (consumed == length)
            {
                // Whatever is left can only be the one or two '=' step 2 removed: any other trailing code
                // point would have left the length unchanged and been validated below.
                break;
            }

            consumed++;

            // Step 4, checked per code point as it is consumed rather than in a separate pass.
            var sextet = Sextet(c);
            if (sextet < 0)
            {
                return false;
            }

            buffer = (buffer << 6) | sextet;
            bits += 6;
            if (bits == 24)
            {
                result[written++] = (byte) (buffer >> 16);
                result[written++] = (byte) (buffer >> 8);
                result[written++] = (byte) buffer;
                buffer = 0;
                bits = 0;
            }
        }

        // Step 9: 12 bits carry one byte (the last four are discarded), 18 bits carry two (the last two
        // are). Six is impossible, because step 3 refused a length that leaves a remainder of one.
        if (bits == 12)
        {
            result[written] = (byte) (buffer >> 4);
        }
        else if (bits == 18)
        {
            result[written++] = (byte) (buffer >> 10);
            result[written] = (byte) (buffer >> 2);
        }

        output = result;
        return true;
    }

    /// <summary>
    /// The value of a code point in the base 64 alphabet of RFC 4648 table 1, or <c>-1</c> for anything
    /// outside it — U+002B (+), U+002F (/) and ASCII alphanumeric, which is step 4's list exactly.
    /// </summary>
    private static int Sextet(char c)
    {
        if (char.IsAsciiLetterUpper(c))
        {
            return c - 'A';
        }

        if (char.IsAsciiLetterLower(c))
        {
            return c - 'a' + 26;
        }

        if (char.IsAsciiDigit(c))
        {
            return c - '0' + 52;
        }

        return c switch
        {
            '+' => 62,
            '/' => 63,
            _ => -1,
        };
    }

    /// <summary>
    /// https://infra.spec.whatwg.org/#ascii-whitespace — TAB, LF, FF, CR and SPACE. U+000B VT is not one,
    /// so it is a decode failure rather than something to skip.
    /// </summary>
    private static bool IsAsciiWhitespace(char c) => c is '\t' or '\n' or '\f' or '\r' or ' ';
}
#endif
