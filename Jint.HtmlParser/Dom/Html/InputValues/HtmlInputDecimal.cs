using System.Globalization;
using System.Numerics;

namespace Jint.HtmlParser;

// Exact D(x), the approved shortest-decimal rational, not the source spelling or base-two fraction.
// Ordinary coefficients use checked Int64 arithmetic; fallback sizes derive only from finite
// binary64 digits, fixed scales and counts, never from an unbounded author's exponent.
internal readonly struct HtmlInputDecimal : IComparable<HtmlInputDecimal>
{
    private readonly long _small;
    private readonly BigInteger _extended;

    internal HtmlInputDecimal(long coefficient, int exponent) { _small = coefficient; Exponent = exponent; }
    internal HtmlInputDecimal(BigInteger coefficient, int exponent)
    {
        if (coefficient >= long.MinValue && coefficient <= long.MaxValue) _small = (long) coefficient;
        else _extended = coefficient;
        Exponent = exponent;
    }

    internal BigInteger Coefficient => _extended.IsZero ? _small : _extended;
    internal int Exponent { get; }
    internal bool IsPositive => _extended.IsZero ? _small > 0 : _extended.Sign > 0;

    internal static HtmlInputDecimal FromDouble(double value)
    {
        Span<char> text = stackalloc char[32];
        var length = HtmlInputNumberFormatter.WriteFinite(value, text);
        Span<char> digits = stackalloc char[32];
        var negative = text[0] == '-';
        var afterPoint = false;
        var exponent = 0;
        var count = 0;
        for (var i = negative ? 1 : 0; i < length; i++)
        {
            var c = text[i];
            if (c == '.') afterPoint = true;
            else if (c == 'e')
            {
                exponent += int.Parse(text[(i + 1)..length], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
                break;
            }
            else
            {
                digits[count++] = c;
                if (afterPoint) exponent--;
            }
        }
        while (count > 1 && digits[count - 1] == '0') { count--; exponent++; }
        long coefficient = 0;
        // Shortest binary64 has at most 17 significant digits, regardless of its ECMA layout.
        for (var i = 0; i < count; i++) coefficient = checked(coefficient * 10 + digits[i] - '0');
        return new(negative ? -coefficient : coefficient, exponent);
    }

    internal static HtmlInputDecimal FromInteger(BigInteger integer) => new(integer, 0);
    internal static HtmlInputDecimal FromInteger(long integer) => new(integer, 0);

    private static bool TryScaleSmall(long value, int power, out long result)
    {
        result = value;
        if (value == 0) return true;
        if (power > 18) return false;
        try
        {
            for (var i = 0; i < power; i++) result = checked(result * 10);
            return true;
        }
        catch (OverflowException) { return false; }
    }

    private bool TryAlignSmall(HtmlInputDecimal other, out long a, out long b, out int exponent)
    {
        exponent = Math.Min(Exponent, other.Exponent);
        a = b = 0;
        return _extended.IsZero && other._extended.IsZero
            && TryScaleSmall(_small, Exponent - exponent, out a)
            && TryScaleSmall(other._small, other.Exponent - exponent, out b);
    }

    private static BigInteger Scale(BigInteger value, int power) => power == 0 ? value : value * BigInteger.Pow(10, power);

    private static void Align(HtmlInputDecimal left, HtmlInputDecimal right, out BigInteger a, out BigInteger b, out int exponent)
    {
        exponent = Math.Min(left.Exponent, right.Exponent);
        a = Scale(left.Coefficient, left.Exponent - exponent);
        b = Scale(right.Coefficient, right.Exponent - exponent);
    }

    public int CompareTo(HtmlInputDecimal other)
    {
        if (TryAlignSmall(other, out var smallA, out var smallB, out _)) return smallA.CompareTo(smallB);
        Align(this, other, out var a, out var b, out _);
        return a.CompareTo(b);
    }

    internal HtmlInputDecimal Add(HtmlInputDecimal other)
    {
        if (TryAlignSmall(other, out var smallA, out var smallB, out var smallExponent))
        {
            try { return new(checked(smallA + smallB), smallExponent); }
            catch (OverflowException) { /* bounded integer fallback */ }
        }
        Align(this, other, out var a, out var b, out var exponent);
        return new(a + b, exponent);
    }

    internal HtmlInputDecimal Subtract(HtmlInputDecimal other)
    {
        if (TryAlignSmall(other, out var smallA, out var smallB, out var smallExponent))
        {
            try { return new(checked(smallA - smallB), smallExponent); }
            catch (OverflowException) { /* bounded integer fallback */ }
        }
        Align(this, other, out var a, out var b, out var exponent);
        return new(a - b, exponent);
    }

    internal HtmlInputDecimal Multiply(BigInteger integer)
    {
        if (_extended.IsZero && integer >= long.MinValue && integer <= long.MaxValue)
        {
            try { return new(checked(_small * (long) integer), Exponent); }
            catch (OverflowException) { /* bounded integer fallback */ }
        }
        return new(Coefficient * integer, Exponent);
    }

    internal HtmlInputDecimal Half() => Multiply(5).WithExponent(Exponent - 1);
    private HtmlInputDecimal WithExponent(int exponent) => _extended.IsZero ? new(_small, exponent) : new(_extended, exponent);

    internal BigInteger FloorQuotient(HtmlInputDecimal positiveDivisor, out bool exact)
    {
        if (TryAlignSmall(positiveDivisor, out var smallA, out var smallB, out _))
        {
            var quotient = Math.DivRem(smallA, smallB, out var remainder);
            exact = remainder == 0;
            return remainder < 0 ? quotient - 1 : quotient;
        }
        Align(this, positiveDivisor, out var numerator, out var denominator, out _);
        var bigQuotient = BigInteger.DivRem(numerator, denominator, out var bigRemainder);
        exact = bigRemainder.IsZero;
        return bigRemainder.Sign < 0 ? bigQuotient - 1 : bigQuotient;
    }

    internal bool TryFloorInt64(out long integer)
    {
        integer = 0;
        if (!_extended.IsZero) return false;
        if (Exponent >= 0) return TryScaleSmall(_small, Exponent, out integer);
        if (-Exponent > 18) { integer = _small < 0 ? -1 : 0; return true; }
        TryScaleSmall(1, -Exponent, out var divisor);
        integer = HtmlInputCalendar.FloorDivide(_small, divisor);
        return true;
    }

    internal BigInteger FloorInteger()
    {
        if (TryFloorInt64(out var integer)) return integer;
        if (Exponent >= 0) return Scale(Coefficient, Exponent);
        var quotient = BigInteger.DivRem(Coefficient, BigInteger.Pow(10, -Exponent), out var remainder);
        return remainder.Sign < 0 ? quotient - 1 : quotient;
    }

    internal bool TryPublish(out double value)
    {
        // Parsing this bounded exact decimal once gives nearest/ties-even, including overflow rejection.
        // Normalize the decimal point/exponent in the number parser before calling the platform parser.
        var text = _extended.IsZero
            ? string.Create(CultureInfo.InvariantCulture, $"{_small}e{Exponent}")
            : string.Create(CultureInfo.InvariantCulture, $"{_extended}e{Exponent}");
        return HtmlInputNumberSyntax.TryParseValue(text, out value);
    }
}
