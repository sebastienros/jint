#if NET8_0_OR_GREATER
using System.Text;

namespace Jint.WebApi.Url.Parsing;

/// <summary>
/// The host parser, https://url.spec.whatwg.org/#host-parsing, and the serializers that go with it.
/// </summary>
/// <remarks>
/// Validation errors are not reported. The URL Standard defines them for conformance checkers and states that
/// "a validation error does not mean that the parser terminates"; the only ones this implementation has to
/// observe are the ones the algorithm text also returns failure for, and those are all here.
/// </remarks>
internal static class HostParser
{
    /// <summary>
    /// https://url.spec.whatwg.org/#concept-host-parser
    /// </summary>
    /// <param name="input">A scalar value string.</param>
    /// <param name="isOpaque">True when the URL being parsed is not special.</param>
    /// <param name="host">The parsed host.</param>
    /// <returns><see langword="false"/> for the spec's failure value.</returns>
    internal static bool TryParse(string input, bool isOpaque, out UrlHost host)
    {
        host = default;

        if (input.Length > 0 && input[0] == '[')
        {
            if (input[input.Length - 1] != ']')
            {
                return false;
            }

            return TryParseIpv6(input.AsSpan(1, input.Length - 2), out host);
        }

        if (isOpaque)
        {
            return TryParseOpaqueHost(input, out host);
        }

        // Step 4 asserts input is not the empty string; every caller in the URL parser has already dealt with
        // an empty buffer by that point.
        var domain = PercentEncoding.DecodeToString(input);
        if (!TryParseDomain(domain, out var asciiDomain))
        {
            return false;
        }

        if (EndsInANumber(asciiDomain))
        {
            return TryParseIpv4(asciiDomain, out host);
        }

        host = new UrlHost(UrlHostKind.Domain, asciiDomain);
        return true;
    }

    /// <summary>
    /// The domain parser, https://url.spec.whatwg.org/#concept-domain-to-ascii, with beStrict false — the only
    /// way the host parser ever calls it.
    /// </summary>
    /// <remarks>
    /// Step 4 is the ASCII branch: an ASCII domain is returned lowercased "regardless of Unicode ToASCII's
    /// outcome, due to web compatibility", so IDNA is consulted only for a domain that actually needs it. See
    /// <see cref="Idna"/> for what that costs in fidelity.
    /// </remarks>
    private static bool TryParseDomain(string domain, out string result)
    {
        if (UrlCharacters.IsAsciiString(domain))
        {
            result = UrlCharacters.AsciiLowercase(domain);
        }
        else if (!Idna.TryToAscii(domain, out result))
        {
            return false;
        }

        // Steps 6 and 7. The forbidden domain code points are a subset of what UseSTD3ASCIIRules would reject,
        // which is why they are re-checked here rather than delegated to IDNA.
        return result.Length != 0 && !UrlCharacters.ContainsForbiddenDomain(result);
    }

    /// <summary>
    /// The opaque-host parser, https://url.spec.whatwg.org/#concept-opaque-host-parser.
    /// </summary>
    private static bool TryParseOpaqueHost(string input, out UrlHost host)
    {
        host = default;
        if (UrlCharacters.ContainsForbiddenHost(input.AsSpan()))
        {
            return false;
        }

        var encoded = PercentEncoding.Encode(input.AsSpan(), PercentEncodeSet.C0Control);

        // An opaque host is by definition non-empty; the empty result is the empty host instead, which is what
        // a URL that is not special gets for "foo:///".
        host = encoded.Length == 0 ? UrlHost.Empty : new UrlHost(UrlHostKind.Opaque, encoded);
        return true;
    }

    /// <summary>
    /// The "ends in a number" checker, https://url.spec.whatwg.org/#ends-in-a-number-checker.
    /// </summary>
    internal static bool EndsInANumber(string input)
    {
        // Strictly splitting on U+002E (.) and then dropping an empty last item is the same thing as ignoring
        // one trailing dot and taking what follows the dot before it.
        var span = input.AsSpan();
        if (span.Length > 0 && span[span.Length - 1] == '.')
        {
            span = span.Slice(0, span.Length - 1);
        }

        var lastDot = span.LastIndexOf('.');
        var last = lastDot < 0 ? span : span.Slice(lastDot + 1);

        if (last.Length != 0)
        {
            var allDigits = true;
            foreach (var c in last)
            {
                if (!UrlCharacters.IsAsciiDigit(c))
                {
                    allDigits = false;
                    break;
                }
            }

            if (allDigits)
            {
                return true;
            }
        }

        // Equivalent to checking that last is "0X" or "0x" followed by zero or more ASCII hex digits.
        return TryParseIpv4Number(last, out _);
    }

    /// <summary>
    /// The IPv4 parser, https://url.spec.whatwg.org/#concept-ipv4-parser.
    /// </summary>
    private static bool TryParseIpv4(string input, out UrlHost host)
    {
        host = default;

        var parts = input.Split('.');
        var count = parts.Length;
        if (parts[count - 1].Length == 0 && count > 1)
        {
            count--;
        }

        if (count > 4)
        {
            return false;
        }

        Span<ulong> numbers = stackalloc ulong[4];
        for (var i = 0; i < count; i++)
        {
            if (!TryParseIpv4Number(parts[i].AsSpan(), out numbers[i]))
            {
                return false;
            }
        }

        for (var i = 0; i < count - 1; i++)
        {
            if (numbers[i] > 255)
            {
                return false;
            }
        }

        // 256 ** (5 - count), computed as a shift so the saturated parse above cannot wrap it.
        var limit = 1UL << (8 * (5 - count));
        if (numbers[count - 1] >= limit)
        {
            return false;
        }

        var ipv4 = numbers[count - 1];
        for (var i = 0; i < count - 1; i++)
        {
            ipv4 += numbers[i] << (8 * (3 - i));
        }

        host = new UrlHost(UrlHostKind.Ipv4, SerializeIpv4((uint) ipv4));
        return true;
    }

    /// <summary>
    /// The IPv4 number parser, https://url.spec.whatwg.org/#ipv4-number-parser. The spec's second return value
    /// is a validation-error flag, which nothing here reports.
    /// </summary>
    /// <remarks>
    /// The spec's "mathematical integer value" is unbounded. Saturating one past <see cref="uint.MaxValue"/> is
    /// equivalent for every use: the largest value any range check accepts is 256⁴ − 1, so a saturated result
    /// fails exactly the checks an exact one would.
    /// </remarks>
    private static bool TryParseIpv4Number(ReadOnlySpan<char> input, out ulong number)
    {
        number = 0;
        if (input.Length == 0)
        {
            return false;
        }

        var radix = 10;
        if (input.Length >= 2 && input[0] == '0' && (input[1] | 0x20) == 'x')
        {
            input = input.Slice(2);
            radix = 16;
        }
        else if (input.Length >= 2 && input[0] == '0')
        {
            input = input.Slice(1);
            radix = 8;
        }

        if (input.Length == 0)
        {
            return true;
        }

        const ulong Saturated = (ulong) uint.MaxValue + 1;
        foreach (var c in input)
        {
            int digit;
            if (radix == 16)
            {
                if (!UrlCharacters.IsAsciiHexDigit(c))
                {
                    return false;
                }

                digit = UrlCharacters.HexValue(c);
            }
            else
            {
                if (!UrlCharacters.IsAsciiDigit(c) || (radix == 8 && c > '7'))
                {
                    return false;
                }

                digit = c - '0';
            }

            if (number < Saturated)
            {
                number = number * (ulong) radix + (ulong) digit;
                if (number > uint.MaxValue)
                {
                    number = Saturated;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// The IPv6 parser, https://url.spec.whatwg.org/#concept-ipv6-parser. <paramref name="input"/> is the
    /// bracketed host with its brackets already removed.
    /// </summary>
    private static bool TryParseIpv6(ReadOnlySpan<char> input, out UrlHost host)
    {
        host = default;

        Span<ushort> address = stackalloc ushort[8];
        address.Clear();
        var pieceIndex = 0;
        var compress = -1;
        var pointer = 0;

        if (pointer < input.Length && input[pointer] == ':')
        {
            if (pointer + 1 >= input.Length || input[pointer + 1] != ':')
            {
                return false;
            }

            pointer += 2;
            pieceIndex++;
            compress = pieceIndex;
        }

        while (pointer < input.Length)
        {
            if (pieceIndex == 8)
            {
                return false;
            }

            if (input[pointer] == ':')
            {
                if (compress >= 0)
                {
                    return false;
                }

                pointer++;
                pieceIndex++;
                compress = pieceIndex;
                continue;
            }

            var value = 0;
            var length = 0;
            while (length < 4 && pointer < input.Length && UrlCharacters.IsAsciiHexDigit(input[pointer]))
            {
                value = value * 0x10 + UrlCharacters.HexValue(input[pointer]);
                pointer++;
                length++;
            }

            if (pointer < input.Length && input[pointer] == '.')
            {
                if (length == 0 || pieceIndex > 6)
                {
                    return false;
                }

                pointer -= length;

                var numbersSeen = 0;
                while (pointer < input.Length)
                {
                    var ipv4Piece = -1;
                    if (numbersSeen > 0)
                    {
                        if (input[pointer] == '.' && numbersSeen < 4)
                        {
                            pointer++;
                        }
                        else
                        {
                            return false;
                        }
                    }

                    if (pointer >= input.Length || !UrlCharacters.IsAsciiDigit(input[pointer]))
                    {
                        return false;
                    }

                    while (pointer < input.Length && UrlCharacters.IsAsciiDigit(input[pointer]))
                    {
                        var number = input[pointer] - '0';
                        if (ipv4Piece < 0)
                        {
                            ipv4Piece = number;
                        }
                        else if (ipv4Piece == 0)
                        {
                            // A leading zero in an embedded IPv4 part.
                            return false;
                        }
                        else
                        {
                            ipv4Piece = ipv4Piece * 10 + number;
                        }

                        if (ipv4Piece > 255)
                        {
                            return false;
                        }

                        pointer++;
                    }

                    address[pieceIndex] = (ushort) (address[pieceIndex] * 0x100 + ipv4Piece);
                    numbersSeen++;

                    if (numbersSeen == 2 || numbersSeen == 4)
                    {
                        pieceIndex++;
                    }
                }

                if (numbersSeen != 4)
                {
                    return false;
                }

                break;
            }

            if (pointer < input.Length && input[pointer] == ':')
            {
                pointer++;
                if (pointer >= input.Length)
                {
                    return false;
                }
            }
            else if (pointer < input.Length)
            {
                return false;
            }

            address[pieceIndex] = (ushort) value;
            pieceIndex++;
        }

        if (compress >= 0)
        {
            var swaps = pieceIndex - compress;
            pieceIndex = 7;
            while (pieceIndex != 0 && swaps > 0)
            {
                (address[pieceIndex], address[compress + swaps - 1]) = (address[compress + swaps - 1], address[pieceIndex]);
                pieceIndex--;
                swaps--;
            }
        }
        else if (pieceIndex != 8)
        {
            return false;
        }

        var builder = new ValueStringBuilder(stackalloc char[48]);
        try
        {
            builder.Append('[');
            AppendIpv6(ref builder, address);
            builder.Append(']');
            host = new UrlHost(UrlHostKind.Ipv6, builder.AsSpan().ToString());
        }
        finally
        {
            builder.Dispose();
        }

        return true;
    }

    /// <summary>
    /// The IPv4 serializer, https://url.spec.whatwg.org/#concept-ipv4-serializer.
    /// </summary>
    private static string SerializeIpv4(uint address)
    {
        var builder = new ValueStringBuilder(stackalloc char[16]);
        try
        {
            builder.Append((int) (address >> 24));
            builder.Append('.');
            builder.Append((int) ((address >> 16) & 0xFF));
            builder.Append('.');
            builder.Append((int) ((address >> 8) & 0xFF));
            builder.Append('.');
            builder.Append((int) (address & 0xFF));
            return builder.AsSpan().ToString();
        }
        finally
        {
            builder.Dispose();
        }
    }

    /// <summary>
    /// The IPv6 serializer, https://url.spec.whatwg.org/#concept-ipv6-serializer, without the brackets the
    /// host serializer adds.
    /// </summary>
    private static void AppendIpv6(ref ValueStringBuilder builder, ReadOnlySpan<ushort> address)
    {
        var compress = FindCompressedPieceIndex(address);
        var ignore0 = false;

        for (var pieceIndex = 0; pieceIndex < 8; pieceIndex++)
        {
            if (ignore0 && address[pieceIndex] == 0)
            {
                continue;
            }

            ignore0 = false;

            if (compress == pieceIndex)
            {
                builder.Append(pieceIndex == 0 ? "::" : ":");
                ignore0 = true;
                continue;
            }

            AppendLowercaseHex(ref builder, address[pieceIndex]);

            if (pieceIndex != 7)
            {
                builder.Append(':');
            }
        }
    }

    /// <summary>
    /// https://url.spec.whatwg.org/#find-the-ipv6-address-compressed-piece-index — the longest run of two or
    /// more zero pieces, earliest run winning a tie.
    /// </summary>
    private static int FindCompressedPieceIndex(ReadOnlySpan<ushort> address)
    {
        var longestIndex = -1;
        var longestSize = 1;
        var foundIndex = -1;
        var foundSize = 0;

        for (var pieceIndex = 0; pieceIndex < 8; pieceIndex++)
        {
            if (address[pieceIndex] != 0)
            {
                if (foundSize > longestSize)
                {
                    longestIndex = foundIndex;
                    longestSize = foundSize;
                }

                foundIndex = -1;
                foundSize = 0;
                continue;
            }

            if (foundIndex < 0)
            {
                foundIndex = pieceIndex;
            }

            foundSize++;
        }

        return foundSize > longestSize ? foundIndex : longestIndex;
    }

    private static void AppendLowercaseHex(ref ValueStringBuilder builder, ushort value)
    {
        const string Digits = "0123456789abcdef";

        var started = false;
        for (var shift = 12; shift >= 0; shift -= 4)
        {
            var digit = (value >> shift) & 0xF;
            if (digit == 0 && !started && shift != 0)
            {
                continue;
            }

            started = true;
            builder.Append(Digits[digit]);
        }
    }
}
#endif
