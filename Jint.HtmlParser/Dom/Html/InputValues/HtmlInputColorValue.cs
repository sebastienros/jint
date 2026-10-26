using System.Drawing;
using Jint.HtmlParser.Css.Values.Colors;

namespace Jint.HtmlParser;

/// <summary>https://html.spec.whatwg.org/multipage/input.html#color-state-(type=color)</summary>
internal static class HtmlInputColorValue
{
    internal static void RequireConfiguration(bool alpha, string? colorSpace)
    {
        if (alpha || string.Equals(colorSpace, "display-p3", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("Native color inputs currently support opaque limited-sRGB values, not alpha or display-p3.");
    }

    internal static string Sanitize(string value, ref HtmlInputValueWork work)
    {
        for (var i = 0; i < value.Length; i++) work.Step();
        var text = value.Trim();
        var open = text.IndexOf('(');
        if (open >= 0)
        {
            var name = text[..open].ToLowerInvariant();
            if (name is "color" or "lab" or "lch" or "oklab" or "oklch" or "color-mix" or "light-dark"
                or "device-cmyk" or "contrast-color"
                || text.Contains("from", StringComparison.OrdinalIgnoreCase)
                || text.Contains("calc(", StringComparison.OrdinalIgnoreCase)
                || text.Contains("none", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException("This CSS color requires color-space or dependent-color resolution.");
        }
        else if (Color.FromName(text).IsSystemColor
            || text.ToLowerInvariant() is "accentcolor" or "accentcolortext" or "activetext" or "buttonborder"
                or "canvas" or "canvastext" or "field" or "fieldtext" or "linktext" or "mark" or "marktext"
                or "selecteditem" or "selecteditemtext" or "visitedtext")
            throw new NotSupportedException("System colors require a native initial color palette.");

        string result;
        if (text.Equals("currentcolor", StringComparison.OrdinalIgnoreCase)) result = "#000000";
        else if (CssSrgbColor.TryParse(value, out var r, out var g, out var b, out _))
            result = $"#{r:x2}{g:x2}{b:x2}";
        else result = "#000000";
        work.Check();
        return result;
    }
}
