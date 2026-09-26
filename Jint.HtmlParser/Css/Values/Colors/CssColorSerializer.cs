using Jint.HtmlParser.Css.Values.Math;

namespace Jint.HtmlParser.Css.Values.Colors;

// CSS Color 4 §§15.1/16.1/16.2: declared CSS values only. No used-color,
// palette, cascade, HTML-compatible serialization, or gamut mapping here.
internal static class CssColorSerializer
{
    internal static string SerializeSpecified(CssColorValue color, CssValueWork work)
    {
        work.CheckCancellation();
        if (color.Kind != CssColorKind.Absolute)
        {
            work.Charge(color.Keyword.Length);
            work.CheckCancellation();
            return color.Keyword;
        }
        if (color.Space == CssColorSpace.Srgb) return SerializeSrgb(color, work);
        Span<double> coordinates = stackalloc double[4];
        var missing = false;
        for (var i = 0; i < 4; i++)
        {
            work.Charge(1);
            var channel = color.GetChannel(i);
            missing |= channel.Kind == CssColorChannelKind.Missing;
            var value = channel.Resolved;
            if (i == 3) coordinates[i] = Clamp(channel.IsPercentage ? value / 100 : value, 1);
            else if (color.Space == CssColorSpace.Rgb) coordinates[i] = Clamp(channel.IsPercentage ? value / 100 * 255 : value, 255);
            else coordinates[i] = i == 0 ? Hue(value) : FiniteCoordinate(value);
        }
        if (color.Space == CssColorSpace.Hsl) coordinates[1] = System.Math.Max(0, coordinates[1]);
        string text;
        if (missing)
        {
            var name = color.Space switch { CssColorSpace.Rgb => "color(srgb", CssColorSpace.Hsl => "hsl(", _ => "hwb(" };
            var first = ModernChannel(color, 0, coordinates[0], work);
            var second = ModernChannel(color, 1, coordinates[1], work);
            var third = ModernChannel(color, 2, coordinates[2], work);
            var alpha = color.GetChannel(3).Kind == CssColorChannelKind.Missing ? " / none" :
                coordinates[3] == 1 ? "" : " / " + SerializeAlpha(coordinates[3], work);
            text = name + (color.Space == CssColorSpace.Rgb ? " " : "") + first + " " + second + " " + third + alpha + ")";
        }
        else
        {
            if (color.Space == CssColorSpace.Hsl)
                HslToRgb(coordinates[0], coordinates[1], coordinates[2], coordinates);
            else if (color.Space == CssColorSpace.Hwb)
                HwbToRgb(coordinates[0], coordinates[1], coordinates[2], coordinates);
            var alpha = coordinates[3];
            text = (alpha == 1 ? "rgb(" : "rgba(") + Number(Clamp(coordinates[0], 255), work) + ", " +
                Number(Clamp(coordinates[1], 255), work) + ", " + Number(Clamp(coordinates[2], 255), work) +
                (alpha == 1 ? "" : ", " + SerializeAlpha(alpha, work)) + ")";
        }
        work.Charge(text.Length);
        work.CheckCancellation();
        return text;
    }

    private static string SerializeSrgb(CssColorValue color, CssValueWork work)
    {
        string Channel(int index)
        {
            var channel = color.GetChannel(index);
            if (channel.Kind == CssColorChannelKind.Missing) return "none";
            if (channel.Kind == CssColorChannelKind.Math) return CssMathSerializer.SerializeSpecified(channel.Math, work);
            var value = channel.IsPercentage ? channel.Resolved / 100 : channel.Resolved;
            return index == 3 ? SerializeAlpha(Clamp(value, 1), work) : Number(value, work);
        }
        var alpha = color.GetChannel(3);
        var alphaText = alpha.Kind == CssColorChannelKind.Math || alpha.Kind == CssColorChannelKind.Missing ||
            Clamp(alpha.IsPercentage ? alpha.Resolved / 100 : alpha.Resolved, 1) != 1 ? " / " + Channel(3) : "";
        var text = "color(srgb " + Channel(0) + " " + Channel(1) + " " + Channel(2) + alphaText + ")";
        work.Charge(text.Length);
        work.CheckCancellation();
        return text;
    }

    private static string ModernChannel(CssColorValue color, int index, double value, CssValueWork work)
    {
        if (color.GetChannel(index).Kind == CssColorChannelKind.Missing) return "none";
        return color.Space == CssColorSpace.Rgb ? Number(value / 255, work) :
            Number(value, work) + (index == 0 ? "" : "%");
    }

    private static string Number(double value, CssValueWork work) => CssMathSerializer.SerializeFiniteNumber(value, work);

    // Binary64 alpha storage follows §16.1.1's non-byte branch and §16.1.2.
    // Six decimal places, nearest with ties toward +∞, then omit trailing zeroes.
    private static string SerializeAlpha(double value, CssValueWork work) =>
        Number(System.Math.Round(value, 6, MidpointRounding.AwayFromZero), work);

    private static double Clamp(double value, double maximum) => double.IsNaN(value) ? 0 : System.Math.Clamp(value, 0, maximum);
    private static double FiniteCoordinate(double value) => double.IsNaN(value) ? 0 :
        double.IsPositiveInfinity(value) ? double.MaxValue : double.IsNegativeInfinity(value) ? -double.MaxValue : value;
    private static double Hue(double value) => !double.IsFinite(value) ? 0 : (value % 360 + 360) % 360;

    // Color 4 §7.1: percentages have the same reference coordinate as numbers.
    private static void HslToRgb(double hue, double saturation, double lightness, Span<double> rgb)
    {
        var s = saturation / 100;
        var l = lightness / 100;
        var a = s * System.Math.Min(l, 1 - l);
        for (var i = 0; i < 3; i++)
        {
            var n = i switch { 0 => 0, 1 => 8, _ => 4 };
            var k = (n + hue / 30) % 12;
            rgb[i] = (l - a * System.Math.Max(-1, System.Math.Min(System.Math.Min(k - 3, 9 - k), 1))) * 255;
        }
    }

    // Color 4 §8.1. Scale the achromatic ratio before addition so finite,
    // very large whiteness/blackness preserve their proportions.
    private static void HwbToRgb(double hue, double white, double black, Span<double> rgb)
    {
        white /= 100;
        black /= 100;
        if (white + black >= 1)
        {
            var scale = System.Math.Max(System.Math.Abs(white), System.Math.Abs(black));
            var gray = white / scale / (white / scale + black / scale) * 255;
            rgb[0] = rgb[1] = rgb[2] = gray;
            return;
        }
        HslToRgb(hue, 100, 50, rgb);
        for (var i = 0; i < 3; i++) rgb[i] = rgb[i] * (1 - white - black) + white * 255;
    }
}
