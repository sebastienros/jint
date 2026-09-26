namespace Jint.HtmlParser.Css.Values.Math;

// CSS Values 4 §§10.4–10.4.1, Editor's Draft 20 August 2026.
internal static class CssMathTrigonometric
{
    private const double DegreesPerRadian = 180d / System.Math.PI;

    internal static double Evaluate(CssMathFunction function, double value, bool isAngle, CssValueWork work)
    {
        ArgumentNullException.ThrowIfNull(work);
        if (function is not (CssMathFunction.Sin or CssMathFunction.Cos or CssMathFunction.Tan or
            CssMathFunction.Asin or CssMathFunction.Acos or CssMathFunction.Atan) ||
            isAngle && function is CssMathFunction.Asin or CssMathFunction.Acos or CssMathFunction.Atan)
            throw new ArgumentOutOfRangeException(nameof(function));
        work.CheckCancellation();
        double result;
        if (double.IsNaN(value)) result = double.NaN;
        else if (function is CssMathFunction.Sin or CssMathFunction.Cos or CssMathFunction.Tan)
        {
            if (double.IsInfinity(value)) result = double.NaN;
            else
            {
                // Canonical angles are degrees. Reduce before converting so very large
                // finite whole turns keep their periodic meaning in binary64.
                var degrees = isAngle ? value % 360d : 0d;
                if (isAngle && degrees % 90d == 0d)
                {
                    result = function switch
                    {
                        CssMathFunction.Sin => degrees switch
                        {
                            90d or -270d => 1d,
                            -90d or 270d => -1d,
                            _ => System.Math.CopySign(0d, degrees)
                        },
                        CssMathFunction.Cos => degrees is 90d or -90d or 270d or -270d ? 0d :
                            degrees is 180d or -180d ? -1d : 1d,
                        _ => degrees switch
                        {
                            90d or -270d => double.PositiveInfinity,
                            -90d or 270d => double.NegativeInfinity,
                            _ => System.Math.CopySign(0d, degrees)
                        }
                    };
                }
                else
                {
                    var radians = isAngle ? degrees * (System.Math.PI / 180d) : value;
                    result = function switch
                    {
                        CssMathFunction.Sin => System.Math.Sin(radians),
                        CssMathFunction.Cos => System.Math.Cos(radians),
                        _ => System.Math.Tan(radians)
                    };
                }
            }
        }
        else result = function switch
        {
            CssMathFunction.Asin => value is < -1d or > 1d ? double.NaN :
                value == 0d ? value : System.Math.Asin(value) * DegreesPerRadian,
            CssMathFunction.Acos => value is < -1d or > 1d ? double.NaN :
                value == 1d ? 0d : System.Math.Acos(value) * DegreesPerRadian,
            _ => value == 0d ? value : System.Math.Atan(value) * DegreesPerRadian
        };
        work.CheckCancellation();
        return double.IsNaN(result) ? double.NaN : result;
    }

    internal static double Atan2(double y, double x, CssValueWork work)
    {
        ArgumentNullException.ThrowIfNull(work);
        work.CheckCancellation();
        double result;
        if (double.IsNaN(y) || double.IsNaN(x)) result = double.NaN;
        else if (double.IsInfinity(y) && double.IsInfinity(x))
            result = y < 0 ? x < 0 ? -135d : -45d : x < 0 ? 135d : 45d;
        else if (y == 0d && double.IsNegative(x))
            result = double.IsNegative(y) ? -180d : 180d;
        else if (x == 0d && y == 0d || double.IsPositiveInfinity(x))
            result = System.Math.CopySign(0d, y);
        else if (double.IsNegativeInfinity(x))
            result = double.IsNegative(y) ? -180d : 180d;
        else if (x == 0d || double.IsInfinity(y))
            result = double.IsNegative(y) ? -90d : 90d;
        else result = System.Math.Atan2(y, x) * DegreesPerRadian;
        work.CheckCancellation();
        return double.IsNaN(result) ? double.NaN : result;
    }
}
