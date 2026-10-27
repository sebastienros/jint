using Jint.HtmlParser.Css.Values.Colors;

namespace Jint.Browser.Dom.Canvas;

internal static class CanvasColor
{
    internal static bool TryParse(string value, out string serialized) => CssSrgbColor.TryParse(value, out serialized);
    internal static bool TryParse(string value, out int r, out int g, out int b, out double alpha)
        => CssSrgbColor.TryParse(value, out r, out g, out b, out alpha);
    internal static bool TryComponent(string text, double scale, out double value)
        => CssSrgbColor.TryComponent(text, scale, out value);
    internal static bool TryHue(string text, out double degrees) => CssSrgbColor.TryHue(text, out degrees);
}
