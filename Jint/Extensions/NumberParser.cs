using System.Numerics;
using System.Runtime.CompilerServices;

namespace Jint.Extensions;

/// <summary>
/// Reads decimal number text as the <see cref="double"/> nearest the value it denotes, alike on every
/// target framework.
/// </summary>
/// <remarks>
/// .NET Framework's <c>double.Parse</c> is not IEEE correctly-rounded and lands one ULP off for about one
/// in sixty-five 18-digit operands; it also reports an overflow by failing rather than by saturating to an
/// infinity, and loses the sign of a negative zero. ECMA-262 makes every string-to-number conversion one
/// rounding to the nearest Number, so the answer may not depend on which target framework an embedder
/// loaded, and every lane that turns number text into a double reads the digits here rather than asking
/// the platform. The same rule catches a lane that does its own arithmetic: <c>parseInt</c> accumulated
/// digits into a <c>double</c> and rounded once per digit where the spec rounds once overall, in every
/// radix and on every target framework (sebastienros/jint#3534). Everything here keeps the value exact
/// until it can decide the answer, and rounds at the end.
/// </remarks>
internal static class NumberParser
{
    // The exponent of the smallest positive double, 2^-1074: nothing rounds below it.
    private const int MinBinaryExponent = -1074;

    // 10^0 .. 10^22 are the powers of ten a double holds exactly; past 10^22 the constant itself is
    // rounded and scaling by it would round twice.
    private const int MaxExactPowerOfTen = 22;

    // 19 decimal digits are the most that always fit a ulong (10^19 - 1 < 2^64 - 1).
    private const int MaxAccumulatedDigits = 19;

    // A double's rounding boundary - the midpoint between two adjacent doubles - is a dyadic rational
    // whose decimal expansion never runs past 769 significant digits (the widest sits at the smallest
    // normal, where the midpoint carries 5^1074). Keeping 800 therefore places every boundary inside the
    // digits actually read, so a dropped tail can only push the value strictly past a boundary it already
    // sits on, which is what the truncated flag records.
    private const int MaxSignificantDigits = 800;

    private const ulong MaxExactMantissa = 1UL << 53;

    private static readonly double[] ExactPowersOfTen =
    {
        1e0, 1e1, 1e2, 1e3, 1e4, 1e5, 1e6, 1e7, 1e8, 1e9, 1e10, 1e11,
        1e12, 1e13, 1e14, 1e15, 1e16, 1e17, 1e18, 1e19, 1e20, 1e21, 1e22,
    };

    /// <summary>
    /// The digit count at which an integer in a given radix has certainly passed every finite double.
    /// Indexed by radix; 0 and 1 are not digit bases and hold zero.
    /// </summary>
    /// <remarks>
    /// An n-digit integer with a non-zero leading digit is at least R^(n-1), so once n reaches
    /// ceil(1024 / log2 R) + 2 the value is above 2^1024 and no further digit can bring it back. This is
    /// what bounds the exact accumulation: a value that is not an infinity never carries more than about
    /// 1030 bits, however long the text is, and the scan stops at the count rather than reading the rest.
    /// </remarks>
    private static readonly int[] DigitsThatOverflow = BuildDigitsThatOverflow();

    private static readonly BigInteger Ten = new BigInteger(10);
    private static readonly BigInteger TenPow19 = new BigInteger(10000000000000000000UL);
    private static readonly BigInteger Two52 = BigInteger.One << 52;
    private static readonly BigInteger Two53 = BigInteger.One << 53;

    /// <summary>
    /// Parses the grammar <c>double.TryParse</c> accepts under <see cref="System.Globalization.NumberStyles.Float"/>
    /// and the invariant culture, minus its <c>Infinity</c> and <c>NaN</c> spellings, which every caller
    /// recognises before it gets here.
    /// </summary>
    /// <remarks>
    /// The padding it tolerates is JavaScript's <c>StrWhiteSpace</c> and not the framework's, which is the
    /// difference between accepting a trailing U+0085 and rejecting a leading U+FEFF. Every caller but
    /// <c>TypeConverter.ToNumber</c> hands over a span it scanned itself and that carries no padding at all.
    /// </remarks>
    internal static bool TryParseDouble(ReadOnlySpan<char> text, out double result)
    {
        result = 0;
        var length = text.Length;
        var i = 0;

        while (i < length && text[i].IsJsWhiteSpace())
        {
            i++;
        }

        var negative = false;
        if (i < length && (text[i] == '+' || text[i] == '-'))
        {
            negative = text[i] == '-';
            i++;
        }

        var significandStart = i;
        ulong mantissa = 0;
        var digits = 0;
        long exponent = 0;
        var truncated = false;
        var sawDigit = false;

        while (i < length && IsDigit(text[i]))
        {
            sawDigit = true;
            var digit = (ulong) (text[i] - '0');
            if (digits == 0 && digit == 0)
            {
                // A leading zero is not a significant digit and shifts nothing.
            }
            else if (digits < MaxAccumulatedDigits)
            {
                mantissa = mantissa * 10 + digit;
                digits++;
            }
            else
            {
                exponent++;
                truncated |= digit != 0;
            }

            i++;
        }

        if (i < length && text[i] == '.')
        {
            i++;
            while (i < length && IsDigit(text[i]))
            {
                sawDigit = true;
                var digit = (ulong) (text[i] - '0');
                if (digits == 0 && digit == 0)
                {
                    exponent--;
                }
                else if (digits < MaxAccumulatedDigits)
                {
                    mantissa = mantissa * 10 + digit;
                    digits++;
                    exponent--;
                }
                else
                {
                    truncated |= digit != 0;
                }

                i++;
            }
        }

        if (!sawDigit)
        {
            return false;
        }

        var significandEnd = i;
        long literalExponent = 0;
        if (i < length && (text[i] | 0x20) == 'e')
        {
            i++;
            var exponentNegative = false;
            if (i < length && (text[i] == '+' || text[i] == '-'))
            {
                exponentNegative = text[i] == '-';
                i++;
            }

            if (i >= length || !IsDigit(text[i]))
            {
                return false;
            }

            long scanned = 0;
            while (i < length && IsDigit(text[i]))
            {
                // Past a million the exponent has already decided the answer and the digits left only
                // make an infinity more infinite, so stop accumulating rather than overflow on a long run.
                if (scanned < 1000000)
                {
                    scanned = scanned * 10 + (text[i] - '0');
                }

                i++;
            }

            literalExponent = exponentNegative ? -scanned : scanned;
            exponent += literalExponent;
        }

        while (i < length && text[i].IsJsWhiteSpace())
        {
            i++;
        }

        if (i != length)
        {
            return false;
        }

        if (mantissa == 0)
        {
            // Every digit read was a zero, whatever the exponent says.
            result = negative ? -0.0 : 0.0;
            return true;
        }

        // 10^(magnitude - 1) <= |value| < 10^magnitude. The two scans keep a different number of
        // significant digits, and this is the figure both of them agree on.
        var magnitude = exponent + digits;
        if (magnitude > 309)
        {
            result = negative ? double.NegativeInfinity : double.PositiveInfinity;
            return true;
        }

        if (magnitude < -323)
        {
            // Under 10^-324, which is less than half the smallest positive double.
            result = negative ? -0.0 : 0.0;
            return true;
        }

        if (!truncated && TryScaleExactly(mantissa, exponent, negative, out result))
        {
            return true;
        }

        result = ParseExact(text.Slice(significandStart, significandEnd - significandStart), literalExponent, negative);
        return true;
    }

    /// <summary>
    /// The cases one floating-point operation on two exact operands settles, and which are therefore
    /// correctly rounded: an integer, or an exact significand scaled by a power of ten a double holds.
    /// </summary>
    private static bool TryScaleExactly(ulong mantissa, long exponent, bool negative, out double result)
    {
        if (exponent == 0)
        {
            result = Signed(UInt64ToDouble(mantissa), negative);
            return true;
        }

        if (mantissa > MaxExactMantissa)
        {
            result = 0;
            return false;
        }

        if (exponent > 0 && exponent <= MaxExactPowerOfTen)
        {
            result = Signed(mantissa * ExactPowersOfTen[exponent], negative);
            return true;
        }

        if (exponent < 0 && exponent >= -MaxExactPowerOfTen)
        {
            result = Signed(mantissa / ExactPowersOfTen[-exponent], negative);
            return true;
        }

        result = 0;
        return false;
    }

    /// <summary>
    /// Re-reads the significand at full width and rounds the exact decimal value once.
    /// </summary>
    /// <param name="significand">The digits and at most one decimal point, with no sign and no exponent.</param>
    /// <param name="literalExponent">The value of the text's own exponent part, if it had one.</param>
    /// <param name="negative">Whether the text carried a minus sign.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static double ParseExact(ReadOnlySpan<char> significand, long literalExponent, bool negative)
    {
        var value = BigInteger.Zero;
        ulong chunk = 0;
        var chunkDigits = 0;
        var digits = 0;
        var exponent = literalExponent;
        var truncated = false;
        var afterPoint = false;

        foreach (var c in significand)
        {
            if (c == '.')
            {
                afterPoint = true;
                continue;
            }

            var digit = c - '0';
            if (digits == 0 && digit == 0)
            {
                if (afterPoint)
                {
                    exponent--;
                }

                continue;
            }

            if (digits < MaxSignificantDigits)
            {
                chunk = chunk * 10 + (ulong) digit;
                chunkDigits++;
                if (chunkDigits == MaxAccumulatedDigits)
                {
                    value = value * TenPow19 + chunk;
                    chunk = 0;
                    chunkDigits = 0;
                }

                digits++;
                if (afterPoint)
                {
                    exponent--;
                }
            }
            else
            {
                truncated |= digit != 0;
                if (!afterPoint)
                {
                    exponent++;
                }
            }
        }

        if (chunkDigits > 0)
        {
            value = value * BigInteger.Pow(Ten, chunkDigits) + chunk;
        }

        return Round(value, exponent, truncated, negative);
    }

    /// <summary>
    /// The double nearest <paramref name="significand"/> times 10^<paramref name="exponent"/>, ties to
    /// even unless <paramref name="truncated"/> says the true value sits strictly above the boundary.
    /// </summary>
    private static double Round(BigInteger significand, long exponent, bool truncated, bool negative)
    {
        BigInteger numerator, denominator;
        if (exponent >= 0)
        {
            numerator = significand * BigInteger.Pow(Ten, (int) exponent);
            denominator = BigInteger.One;
        }
        else
        {
            numerator = significand;
            denominator = BigInteger.Pow(Ten, (int) -exponent);
        }

        // Aim the quotient straight at 53 significant bits; the two loops below absorb the one place a
        // bit-length estimate can be out by.
        var binaryExponent = BitLength(numerator) - BitLength(denominator) - 53;
        if (binaryExponent < MinBinaryExponent)
        {
            binaryExponent = MinBinaryExponent;
        }

        Divide(numerator, denominator, binaryExponent, out var quotient, out var remainder, out var scaled);
        while (quotient >= Two53)
        {
            binaryExponent++;
            Divide(numerator, denominator, binaryExponent, out quotient, out remainder, out scaled);
        }

        while (quotient < Two52 && binaryExponent > MinBinaryExponent)
        {
            binaryExponent--;
            Divide(numerator, denominator, binaryExponent, out quotient, out remainder, out scaled);
        }

        var comparison = (remainder << 1).CompareTo(scaled);
        if (comparison > 0 || (comparison == 0 && (truncated || !quotient.IsEven)))
        {
            quotient += BigInteger.One;
            if (quotient >= Two53)
            {
                quotient >>= 1;
                binaryExponent++;
            }
        }

        return Signed(Compose(quotient, binaryExponent), negative);
    }

    private static double Compose(BigInteger quotient, int binaryExponent)
    {
        if (quotient.IsZero)
        {
            return 0.0;
        }

        if (quotient < Two52)
        {
            // Only reachable at the exponent floor, where the significand is the whole encoding.
            return BitConverter.Int64BitsToDouble((long) quotient);
        }

        var biased = binaryExponent + 1075;
        if (biased >= 2047)
        {
            return double.PositiveInfinity;
        }

        return BitConverter.Int64BitsToDouble(((long) biased << 52) | (long) (quotient - Two52));
    }

    private static void Divide(
        BigInteger numerator,
        BigInteger denominator,
        int binaryExponent,
        out BigInteger quotient,
        out BigInteger remainder,
        out BigInteger scaledDenominator)
    {
        if (binaryExponent >= 0)
        {
            scaledDenominator = denominator << binaryExponent;
            quotient = BigInteger.DivRem(numerator, scaledDenominator, out remainder);
        }
        else
        {
            scaledDenominator = denominator;
            quotient = BigInteger.DivRem(numerator << -binaryExponent, denominator, out remainder);
        }
    }

    /// <summary>
    /// The <see cref="double"/> nearest the integer the longest radix-<paramref name="radix"/> digit
    /// prefix of <paramref name="text"/> denotes, without a sign.
    /// </summary>
    /// <param name="text">Digits, and whatever follows the first character that is not one.</param>
    /// <param name="radix">The base the digits are read in, between 2 and 36.</param>
    /// <param name="result">The value read, non-negative; the caller applies the sign.</param>
    /// <returns><see langword="true"/> when at least one digit was read.</returns>
    internal static bool TryParseRadixInteger(ReadOnlySpan<char> text, int radix, out double result)
    {
        var length = text.Length;
        var i = 0;

        // A leading zero is a digit that carries no magnitude, so it is skipped rather than counted; that
        // is what makes the digit count below a statement about the value rather than about the text.
        while (i < length && text[i] == '0')
        {
            i++;
        }

        var sawDigit = i > 0;
        var start = i;

        // The index the value has passed every finite double at, whatever the digits there say.
        var overflowIndex = start + DigitsThatOverflow[radix] - 1;

        var accumulator = 0UL;
        var accumulatorLimit = ulong.MaxValue / (uint) radix;
        var accumulated = true;

        while (i < length)
        {
            var digit = DigitValue(text[i]);
            if (digit < 0 || digit >= radix)
            {
                break;
            }

            if (i >= overflowIndex)
            {
                result = double.PositiveInfinity;
                return true;
            }

            if (accumulated)
            {
                if (accumulator > accumulatorLimit)
                {
                    accumulated = false;
                }
                else
                {
                    var next = accumulator * (uint) radix + (uint) digit;
                    if (next < accumulator)
                    {
                        accumulated = false;
                    }
                    else
                    {
                        accumulator = next;
                    }
                }
            }

            sawDigit = true;
            i++;
        }

        if (!sawDigit)
        {
            result = 0;
            return false;
        }

        if (accumulated)
        {
            // Everything under 2^64, which is every digit run a program is likely to hand parseInt.
            result = UInt64ToDouble(accumulator);
            return true;
        }

        var digits = text.Slice(start, i - start);

        // A radix that is a power of two lays its digits straight onto the bits, so the exact value is a
        // shift and everything dropped is a sticky bit; any other radix needs the value itself.
        result = (radix & (radix - 1)) == 0
            ? ParsePowerOfTwoRadix(digits, radix)
            : ParseExactRadixInteger(digits, radix);

        return true;
    }

    /// <summary>
    /// Reads digits whose radix is a power of two, keeping the leading bits and a sticky flag for the
    /// rest, which is all the rounding can need and costs no <see cref="BigInteger"/> at all.
    /// </summary>
    private static double ParsePowerOfTwoRadix(ReadOnlySpan<char> digits, int radix)
    {
        var bitsPerDigit = BitLength((ulong) radix) - 1;
        var ceiling = ulong.MaxValue >> bitsPerDigit;

        var significand = 0UL;
        long binaryExponent = 0;
        var sticky = false;

        foreach (var c in digits)
        {
            var digit = (ulong) DigitValue(c);
            if (significand <= ceiling)
            {
                significand = (significand << bitsPerDigit) | digit;
            }
            else
            {
                // The accumulator is full, and it holds at least 60 bits by then - well past the 53 the
                // answer keeps and the one more that decides which way it rounds.
                binaryExponent += bitsPerDigit;
                sticky |= digit != 0;
            }
        }

        return RoundScaled(significand, binaryExponent, sticky);
    }

    /// <summary>
    /// Reads digits in a radix that is not a power of two into the exact integer they denote, in chunks as
    /// wide as a <c>ulong</c> holds.
    /// </summary>
    /// <remarks>
    /// The caller has already ruled out everything that would not fit: the digit count is under
    /// <see cref="DigitsThatOverflow"/>, so the value built here is below 2^1024 and about 130 bytes wide,
    /// whatever the input's length was.
    /// </remarks>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static double ParseExactRadixInteger(ReadOnlySpan<char> digits, int radix)
    {
        var chunkCapacity = 1;
        var chunkScale = (ulong) radix;
        while (chunkScale <= ulong.MaxValue / (uint) radix)
        {
            chunkScale *= (uint) radix;
            chunkCapacity++;
        }

        var scale = new BigInteger(chunkScale);
        var value = BigInteger.Zero;
        var chunk = 0UL;
        var chunkDigits = 0;

        foreach (var c in digits)
        {
            chunk = chunk * (uint) radix + (uint) DigitValue(c);
            chunkDigits++;
            if (chunkDigits == chunkCapacity)
            {
                value = value * scale + chunk;
                chunk = 0;
                chunkDigits = 0;
            }
        }

        if (chunkDigits > 0)
        {
            var tail = 1UL;
            for (var i = 0; i < chunkDigits; i++)
            {
                tail *= (uint) radix;
            }

            value = value * new BigInteger(tail) + chunk;
        }

        // An integer is its own significand at a decimal exponent of zero, with nothing dropped to be
        // sticky about, so the rounding the decimal lane already does is the rounding this one needs.
        return Round(value, exponent: 0, truncated: false, negative: false);
    }

    /// <summary>
    /// The double nearest <paramref name="significand"/> times 2^<paramref name="binaryExponent"/>, ties
    /// to even unless <paramref name="sticky"/> says the value sits strictly above the boundary.
    /// </summary>
    private static double RoundScaled(ulong significand, long binaryExponent, bool sticky)
    {
        var length = BitLength(significand);
        if (length > 53)
        {
            var drop = length - 53;
            var dropped = significand & ((1UL << drop) - 1);
            var half = 1UL << (drop - 1);

            significand >>= drop;
            binaryExponent += drop;

            if (dropped > half || (dropped == half && (sticky || (significand & 1) != 0)))
            {
                significand++;
                if (significand == 1UL << 53)
                {
                    significand >>= 1;
                    binaryExponent++;
                }
            }
        }

        return ComposeInteger(significand, binaryExponent);
    }

    /// <summary>
    /// Builds the double holding <paramref name="significand"/> times 2^<paramref name="binaryExponent"/>,
    /// saturating to an infinity rather than wrapping.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="Compose"/> this one has no subnormal case to serve: an integer read from digits
    /// is at least one, so its exponent never reaches the floor.
    /// </remarks>
    private static double ComposeInteger(ulong significand, long binaryExponent)
    {
        if (significand < 1UL << 52)
        {
            // Only reachable with a zero exponent: everything that dropped a bit normalised to 53 of them.
            return (long) significand;
        }

        // The significand carries 53 bits, so the value is 1.f x 2^(binaryExponent + 52).
        var biased = binaryExponent + 1075;
        if (biased >= 2047)
        {
            return double.PositiveInfinity;
        }

        return BitConverter.Int64BitsToDouble((biased << 52) | (long) (significand - (1UL << 52)));
    }

    private static int[] BuildDigitsThatOverflow()
    {
        var counts = new int[37];
        for (var radix = 2; radix <= 36; radix++)
        {
            // Rounded up and then one further, so a logarithm landing a hair low still names a count the
            // value has certainly passed 2^1024 at. Two extra digits cost the accumulation a few bits.
            var bitsPerDigit = System.Math.Log(radix) / System.Math.Log(2);
            counts[radix] = (int) System.Math.Ceiling(1024 / bitsPerDigit) + 2;
        }

        return counts;
    }

    private static int BitLength(ulong value)
    {
        var bits = 0;
        while (value != 0)
        {
            bits++;
            value >>= 1;
        }

        return bits;
    }

    private static int DigitValue(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'a' and <= 'z' => c - 'a' + 10,
        >= 'A' and <= 'Z' => c - 'A' + 10,
        _ => -1,
    };

    private static int BitLength(BigInteger value)
    {
        if (value.IsZero)
        {
            return 0;
        }

        var bytes = value.ToByteArray();
        var index = bytes.Length - 1;
        while (index > 0 && bytes[index] == 0)
        {
            index--;
        }

        var bits = index * 8;
        int top = bytes[index];
        while (top != 0)
        {
            bits++;
            top >>= 1;
        }

        return bits;
    }

    /// <summary>
    /// Converts an unsigned 64-bit integer to the nearest <see cref="double"/>, alike on every runtime.
    /// </summary>
    /// <remarks>
    /// The signed conversion is correctly rounded everywhere, so halve the operand to reach it, OR-ing
    /// the bit that falls off back in so a tie still reads as a tie, and double the result - which is
    /// exact - to get back. No runtime before .NET 9 rounds the unsigned conversion once for an operand
    /// with the high bit set (sebastienros/jint#3530, adams85/acornima#53).
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static double UInt64ToDouble(ulong value)
    {
        if (value < 1UL << 63)
        {
            return (long) value;
        }

        return (double) (long) ((value >> 1) | (value & 1)) * 2.0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double Signed(double value, bool negative) => negative ? -value : value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsDigit(char c) => c is >= '0' and <= '9';
}
