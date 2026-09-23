namespace Jint.HtmlParser.Css.Values.Math;

// CSS Values 4 §10.3.1 and §6 line-width snapping, Editor's Draft 20 August 2026.
internal static class CssMathStepped
{
    internal static double Round(double value, double step, CssRoundingStrategy strategy, CssValueWork work)
    {
        if (strategy is < CssRoundingStrategy.Nearest or > CssRoundingStrategy.ToZero)
            throw new ArgumentOutOfRangeException(nameof(strategy));
        ArgumentNullException.ThrowIfNull(work);
        work.CheckCancellation();
        var result = RoundCore(value, step, strategy, false, work);
        work.CheckCancellation();
        return result;
    }

    internal static double Mod(double value, double step, CssValueWork work) => Remainder(value, step, true, work);
    internal static double Rem(double value, double step, CssValueWork work) => Remainder(value, step, false, work);

    internal static double RoundLineWidth(double valuePx, double? stepPx, double devicePixelSizePx, CssValueWork work)
    {
        ArgumentNullException.ThrowIfNull(work);
        work.CheckCancellation();
        if (!double.IsFinite(devicePixelSizePx) || devicePixelSizePx <= 0)
            throw new ArgumentOutOfRangeException(nameof(devicePixelSizePx));
        var value = stepPx is { } step ? RoundCore(valuePx, step, CssRoundingStrategy.Nearest, true, work) : valuePx;
        if (!double.IsFinite(value) || value == 0) { work.CheckCancellation(); return value; }
        var magnitude = System.Math.Abs(value);
        if (magnitude < devicePixelSizePx)
            value = System.Math.CopySign(devicePixelSizePx, value);
        else if (magnitude > devicePixelSizePx)
        {
            // Avoid a quotient that overflows even though the snapped length is finite.
            work.CheckCancellation();
            var remainder = magnitude % devicePixelSizePx;
            work.CheckCancellation();
            if (remainder != 0) value = System.Math.CopySign(magnitude - remainder, value);
        }
        work.CheckCancellation();
        return value;
    }

    private static double RoundCore(double value, double step, CssRoundingStrategy strategy,
        bool lineWidth, CssValueWork work)
    {
        if (double.IsNaN(value) || double.IsNaN(step) || step == 0) return double.NaN;
        if (double.IsInfinity(value)) return double.IsInfinity(step) ? double.NaN : value;
        if (double.IsInfinity(step))
        {
            if (lineWidth && value != 0) return System.Math.CopySign(double.PositiveInfinity, value);
            if (strategy == CssRoundingStrategy.Up && value > 0) return double.PositiveInfinity;
            if (strategy == CssRoundingStrategy.Down && value < 0) return double.NegativeInfinity;
            return System.Math.CopySign(0, value);
        }
        var spacing = System.Math.Abs(step);
        work.CheckCancellation();
        var remainder = value % spacing;
        work.CheckCancellation();
        if (remainder == 0) return value;
        var baseValue = value - remainder;
        var lower = value > 0 ? baseValue : baseValue - spacing;
        var upper = value > 0 ? baseValue + spacing : baseValue;
        if (lower == 0) lower = 0d;
        if (upper == 0) upper = -0d;
        if (lineWidth && (lower == 0 || upper == 0)) return lower == 0 ? upper : lower;
        return strategy switch
        {
            CssRoundingStrategy.Up => upper,
            CssRoundingStrategy.Down => lower,
            CssRoundingStrategy.ToZero => baseValue == 0 ? System.Math.CopySign(0, value) : baseValue,
            _ => value > 0
                ? remainder < spacing - remainder ? lower : upper
                : -remainder <= spacing + remainder ? upper : lower
        };
    }

    private static double Remainder(double value, double step, bool modulus, CssValueWork work)
    {
        ArgumentNullException.ThrowIfNull(work);
        work.CheckCancellation();
        if (double.IsNaN(value) || double.IsNaN(step) || double.IsInfinity(value) || step == 0)
        { work.CheckCancellation(); return double.NaN; }
        if (double.IsInfinity(step))
        {
            var result = modulus && double.IsNegative(value) != double.IsNegative(step) ? double.NaN : value;
            work.CheckCancellation();
            return result;
        }
        work.CheckCancellation();
        var remainder = value % step;
        work.CheckCancellation();
        if (!modulus) { work.CheckCancellation(); return remainder; }
        if (remainder == 0) { work.CheckCancellation(); return System.Math.CopySign(0, step); }
        if (double.IsNegative(remainder) != double.IsNegative(step))
        {
            var corrected = remainder + step;
            if (corrected == step) corrected = System.Math.BitDecrement(System.Math.Abs(step)) *
                (double.IsNegative(step) ? -1 : 1);
            remainder = corrected;
        }
        work.CheckCancellation();
        return remainder;
    }
}
