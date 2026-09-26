namespace Jint.HtmlParser.Css.Values.Math;

// CSS Values 4 §10.6, Editor's Draft 20 August 2026.
internal static class CssMathSign
{
    internal static double Abs(double value, CssValueWork work)
    {
        ArgumentNullException.ThrowIfNull(work);
        work.CheckCancellation();
        var result = System.Math.Abs(value);
        work.CheckCancellation();
        return result;
    }

    internal static double Sign(double value, CssValueWork work)
    {
        ArgumentNullException.ThrowIfNull(work);
        work.CheckCancellation();
        var result = double.IsNaN(value) ? double.NaN :
            value == 0 ? value : value < 0 ? -1d : 1d;
        work.CheckCancellation();
        return result;
    }
}
