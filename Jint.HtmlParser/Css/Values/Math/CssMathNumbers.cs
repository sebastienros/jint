using System.Globalization;

namespace Jint.HtmlParser.Css.Values.Math;

internal static class CssMathNumbers
{
    internal static readonly double AngleLimit = System.Math.ScaleB(360d, 1014);

    // CSS Values 4 §10.10.1 and §10.9.2, Editor's Draft 20 August 2026.
    // The 17-significant-decimal-digit conversion is Jint's finite representation policy.
    internal static CssMathNumeric FromToken(CssToken token, CssMathPercentageMode mode,
        CssValueWork work)
    {
        work.CheckCancellation();
        var kind = token.Kind switch
        {
            CssTokenKind.Number => CssNumericKind.Number,
            CssTokenKind.Percentage => CssNumericKind.Percentage,
            _ => CssNumericKind.Dimension
        };
        var unit = kind == CssNumericKind.Dimension ? CssUnits.Recognize(token.Unit, work) : CssUnit.None;
        if (kind == CssNumericKind.Dimension && unit == CssUnit.None)
            throw new ArgumentException("Unrecognized dimension token.", nameof(token));
        if (kind == CssNumericKind.Percentage && mode == CssMathPercentageMode.Forbidden)
            throw new ArgumentException("Percentages are forbidden in this context.", nameof(mode));
        var provenance = CssNumber.FromValidatedToken(token.NumberText, work);
        var value = ParseFinite(provenance, unit, work);
        var canonicalUnit = CanonicalUnit(unit);
        work.CheckCancellation();
        return new CssMathNumeric(value, kind, canonicalUnit, token.Span, provenance);
    }

    internal static CssUnit CanonicalUnit(CssUnit unit) => unit switch
    {
        >= CssUnit.Px and <= CssUnit.Pc => CssUnit.Px,
        >= CssUnit.Deg and <= CssUnit.Turn => CssUnit.Deg,
        CssUnit.Ms => CssUnit.S,
        CssUnit.Khz => CssUnit.Hz,
        >= CssUnit.Dpi and <= CssUnit.X => CssUnit.Dppx,
        _ => unit
    };

    internal static double ParseFinite(CssNumber number, CssUnit unit, CssValueWork work)
    {
        var text = number.Spelling.AsSpan();
        if (number.Sign == 0) return 0d;
        var negative = number.Sign < 0;
        var index = text[0] is '+' or '-' ? 1 : 0;
        var beforeDot = 0;
        var firstOrdinal = -1;
        var ordinal = 0;
        var dotSeen = false;
        Span<char> digits = stackalloc char[18];
        var stored = 0;
        var sticky = false;
        while (index < text.Length && text[index] is not ('e' or 'E'))
        {
            work.Charge(1);
            var ch = text[index++];
            if (ch == '.') { dotSeen = true; continue; }
            if (!dotSeen) beforeDot++;
            if (firstOrdinal < 0)
            {
                if (ch == '0') { ordinal++; continue; }
                firstOrdinal = ordinal;
            }
            if (stored < digits.Length) digits[stored++] = ch;
            else if (ch != '0') sticky = true;
            ordinal++;
        }
        var cap = (long) text.Length + 4096;
        long explicitExponent = 0;
        if (index < text.Length)
        {
            index++;
            var expNegative = index < text.Length && text[index] == '-';
            if (index < text.Length && text[index] is '+' or '-') index++;
            while (index < text.Length)
            {
                work.Charge(1);
                var digit = text[index++] - '0';
                explicitExponent = System.Math.Min(cap, explicitExponent * 10 + digit);
            }
            if (expNegative) explicitExponent = -explicitExponent;
        }
        var decimalExponent = explicitExponent + beforeDot - firstOrdinal - 1L;
        if (stored == 18)
        {
            var guard = digits[17];
            var roundUp = guard > '5' || guard == '5' && (sticky || ((digits[16] - '0') & 1) != 0);
            stored = 17;
            if (roundUp)
            {
                var carry = 16;
                while (carry >= 0 && digits[carry] == '9') digits[carry--] = '0';
                if (carry < 0) { digits[0] = '1'; decimalExponent++; }
                else digits[carry]++;
            }
        }
        Span<char> normalized = stackalloc char[64];
        var used = 0;
        normalized[used++] = digits[0];
        if (stored > 1)
        {
            normalized[used++] = '.';
            digits.Slice(1, stored - 1).CopyTo(normalized[used..]);
            used += stored - 1;
        }
        var (factor, scale) = UnitFactor(unit);
        decimalExponent += scale;
        var limit = unit.Category() == CssUnitCategory.Angle ? AngleLimit : double.MaxValue;
        if (factor == 1d)
        {
            var direct = ParseBounded(normalized[..used], decimalExponent, limit, work);
            return negative ? -direct : direct;
        }
        work.CheckCancellation();
        double significand = double.Parse(normalized[..used], CultureInfo.InvariantCulture);
        significand *= factor;
        while (significand >= 10) { significand /= 10; decimalExponent++; }
        while (significand < 1) { significand *= 10; decimalExponent--; }
        var scaled = ParseBounded(significand.ToString("R", CultureInfo.InvariantCulture).AsSpan(),
            decimalExponent, limit, work);
        return negative ? -scaled : scaled;
    }

    private static double ParseBounded(ReadOnlySpan<char> mantissa, long decimalExponent,
        double limit, CssValueWork work)
    {
        if (decimalExponent > 309) return limit;
        if (decimalExponent < -330) return 0d;
        var exponentText = decimalExponent.ToString(CultureInfo.InvariantCulture);
        var numeric = string.Concat(mantissa, "E".AsSpan(), exponentText.AsSpan());
        work.Charge(numeric.Length);
        work.CheckCancellation();
        if (!double.TryParse(numeric, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            value = limit;
        value = System.Math.Min(value, limit);
        work.CheckCancellation();
        return value;
    }

    private static (double Factor, int DecimalScale) UnitFactor(CssUnit unit) => unit switch
    {
        CssUnit.Cm => (96d / 2.54d, 0),
        CssUnit.Mm => (96d / 25.4d, 0),
        CssUnit.Q => (96d / 101.6d, 0),
        CssUnit.In => (96d, 0),
        CssUnit.Pt => (96d / 72d, 0),
        CssUnit.Pc => (16d, 0),
        CssUnit.Grad => (0.9d, 0),
        CssUnit.Rad => (180d / System.Math.PI, 0),
        CssUnit.Turn => (360d, 0),
        CssUnit.Ms => (1d, -3),
        CssUnit.Khz => (1d, 3),
        CssUnit.Dpi => (1d / 96d, 0),
        CssUnit.Dpcm => (2.54d / 96d, 0),
        _ => (1d, 0)
    };

    internal static double CssMin(double a, double b) => double.IsNaN(a) || double.IsNaN(b) ? double.NaN :
        a == 0 && b == 0 ? (double.IsNegative(a) || double.IsNegative(b) ? -0d : 0d) : System.Math.Min(a, b);
    internal static double CssMax(double a, double b) => double.IsNaN(a) || double.IsNaN(b) ? double.NaN :
        a == 0 && b == 0 ? (!double.IsNegative(a) || !double.IsNegative(b) ? 0d : -0d) : System.Math.Max(a, b);
}
