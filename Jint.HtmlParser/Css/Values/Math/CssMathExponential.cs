namespace Jint.HtmlParser.Css.Values.Math;

// CSS Values 4 §§10.5–10.5.1, Editor's Draft 20 August 2026.
internal static class CssMathExponential
{
    internal static double Pow(double value, double exponent, CssValueWork work)
    {
        ArgumentNullException.ThrowIfNull(work);
        work.CheckCancellation();
        double result;
        if (double.IsNaN(value) || double.IsNaN(exponent)) result = double.NaN;
        else if (exponent == 0d) result = 1d;
        else if (double.IsInfinity(exponent))
        {
            var magnitude = System.Math.Abs(value);
            result = magnitude == 1d ? double.NaN :
                (magnitude > 1d) == (exponent > 0d) ? double.PositiveInfinity : 0d;
        }
        else if (value == 0d || double.IsInfinity(value))
        {
            var negative = double.IsNegative(value) && IsOddInteger(exponent);
            result = (value == 0d) == (exponent > 0d) ?
                (negative ? -0d : 0d) :
                (negative ? double.NegativeInfinity : double.PositiveInfinity);
        }
        else if (value < 0d && exponent != System.Math.Truncate(exponent)) result = double.NaN;
        else result = System.Math.Pow(value, exponent);
        work.CheckCancellation();
        return double.IsNaN(result) ? double.NaN : result;
    }

    private static bool IsOddInteger(double value) =>
        System.Math.Abs(value) < 9007199254740992d &&
        value == System.Math.Truncate(value) && System.Math.Abs(value % 2d) == 1d;

    internal static double Sqrt(double value, CssValueWork work)
    {
        ArgumentNullException.ThrowIfNull(work);
        work.CheckCancellation();
        var result = double.IsNaN(value) || value < 0d ? double.NaN :
            value == 0d ? value : System.Math.Sqrt(value);
        work.CheckCancellation();
        return result;
    }

    internal static double Hypot(ReadOnlySpan<double> values, CssValueWork work)
    {
        ArgumentNullException.ThrowIfNull(work);
        if (values.IsEmpty) throw new ArgumentException("Hypot needs at least one argument.", nameof(values));
        work.CheckCancellation();
        var maximum = 0d;
        var infinite = false;
        foreach (var value in values)
        {
            work.Charge(1);
            if (double.IsNaN(value)) { work.CheckCancellation(); return double.NaN; }
            if (double.IsInfinity(value)) infinite = true;
            else maximum = System.Math.Max(maximum, System.Math.Abs(value));
        }
        if (infinite || maximum == 0d)
        {
            work.CheckCancellation();
            return infinite ? double.PositiveInfinity : 0d;
        }
        // Power-of-two scaling avoids rounding the final factor upward across
        // double.MaxValue when the exact length is still representable.
        var exponent = System.Math.ILogB(maximum);
        var squares = 0d;
        var correction = 0d;
        foreach (var value in values)
        {
            work.Charge(1);
            var scaled = System.Math.ScaleB(value, -exponent);
            var square = scaled * scaled;
            var next = squares + square;
            // Keep both the multiplication and addition roundoff. Repeated
            // near-limit legs can otherwise round the sum up to a power of two
            // and turn a finite length into infinity after rescaling.
            var addRoundoff = squares >= square ? (squares - next) + square :
                (square - next) + squares;
            correction += addRoundoff + System.Math.FusedMultiplyAdd(scaled, scaled, -square);
            squares = next;
        }
        var result = System.Math.ScaleB(System.Math.Sqrt(squares + correction), exponent);
        work.CheckCancellation();
        return result;
    }

    internal static double Log(double value, double? basis, CssValueWork work)
    {
        ArgumentNullException.ThrowIfNull(work);
        work.CheckCancellation();
        var b = basis ?? System.Math.E;
        double result;
        if (double.IsNaN(value) || double.IsNaN(b) || b < 0d || b == 1d || value < 0d)
            result = double.NaN;
        else if (value == 0d) result = double.NegativeInfinity;
        else if (value == 1d) result = 0d;
        else if (double.IsPositiveInfinity(value)) result = double.PositiveInfinity;
        else result = System.Math.Log(value) / System.Math.Log(b);
        work.CheckCancellation();
        return double.IsNaN(result) ? double.NaN : result;
    }

    internal static double Exp(double value, CssValueWork work)
    {
        ArgumentNullException.ThrowIfNull(work);
        work.CheckCancellation();
        var result = double.IsNaN(value) ? double.NaN :
            double.IsNegativeInfinity(value) ? 0d : System.Math.Exp(value);
        work.CheckCancellation();
        return result;
    }
}
