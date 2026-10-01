using System.Drawing;
using System.Globalization;

namespace Jint.Browser.Dom.Canvas;

/// <summary>CSS named, hex, RGB, HSL and HWB colors serialized as canvas sRGB colors.</summary>
/// <remarks>https://html.spec.whatwg.org/multipage/canvas.html#serialisation-of-a-color</remarks>
internal static class CanvasColor
{
    internal static bool TryParse(string value, out string serialized)
    {
        serialized = "";
        if (value.Trim().Equals("currentcolor", StringComparison.OrdinalIgnoreCase)) { serialized = "#000000"; return true; }
        if (!TryParse(value, out var r, out var g, out var b, out var alpha)) return false;
        serialized = Rgba(r, g, b, alpha);
        return true;
    }

    /// <summary>Parses an absolute sRGB color; <c>currentcolor</c> and system colors are the caller's to resolve.</summary>
    internal static bool TryParse(string value, out int r, out int g, out int b, out double alpha)
    {
        var text = value.Trim().ToLowerInvariant();
        r = g = b = 0;
        alpha = 1;
        if (text is "transparent") { alpha = 0; return true; }
        if (text is "rebeccapurple") { r = 0x66; g = 0x33; b = 0x99; return true; }
        if (text is "grey") text = "gray";
        else if (text.Contains("grey", StringComparison.Ordinal)) text = text.Replace("grey", "gray", StringComparison.Ordinal);
        var known = Color.FromName(text);
        if (known.IsKnownColor && !known.IsSystemColor)
        {
            (r, g, b, alpha) = (known.R, known.G, known.B, known.A / 255d);
            return true;
        }
        if (text.StartsWith('#'))
        {
            var hex = text.AsSpan(1);
            if (hex.Length is not (3 or 4 or 6 or 8)) return false;
            foreach (var c in hex) if (!char.IsAsciiHexDigit(c)) return false;
            var bits = uint.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            if (hex.Length is 3 or 4)
            {
                if (hex.Length == 4) { alpha = (bits & 15) / 15d; bits >>= 4; }
                r = (int) ((bits >> 8) & 15) * 17; g = (int) ((bits >> 4) & 15) * 17; b = (int) (bits & 15) * 17;
            }
            else
            {
                if (hex.Length == 8) { alpha = (bits & 255) / 255d; bits >>= 8; }
                r = (int) ((bits >> 16) & 255); g = (int) ((bits >> 8) & 255); b = (int) (bits & 255);
            }
            return true;
        }
        var open = text.IndexOf('(');
        if (open < 0 || !text.EndsWith(')')) return false;
        var name = text[..open];
        if (name is not ("rgb" or "rgba" or "hsl" or "hsla" or "hwb")) return false;
        var body = text[(open + 1)..^1];
        var comma = body.Contains(',');
        string[] channels;
        string? alphaText = null;
        if (comma)
        {
            if (body.Contains('/') || name == "hwb") return false;
            channels = body.Split(',').Select(static p => p.Trim()).ToArray();
            if (channels.Length == 4) { alphaText = channels[3]; channels = channels[..3]; }
        }
        else
        {
            var slash = body.Split('/');
            if (slash.Length > 2) return false;
            if (slash.Length == 2) alphaText = slash[1].Trim();
            channels = slash[0].Split([' ', '\t', '\n', '\r', '\f'], StringSplitOptions.RemoveEmptyEntries);
        }
        if (channels.Length != 3) return false;
        var a = 1d;
        if (alphaText is not null && !TryComponent(alphaText, 1, out a)) return false;
        double red, green, blue;
        if (name is "rgb" or "rgba")
        {
            if (comma && (channels[0].EndsWith('%') != channels[1].EndsWith('%') || channels[0].EndsWith('%') != channels[2].EndsWith('%'))) return false;
            if (!TryComponent(channels[0], 255, out red) || !TryComponent(channels[1], 255, out green) || !TryComponent(channels[2], 255, out blue)) return false;
        }
        else
        {
            if (!TryHue(channels[0], out var h) || !channels[1].EndsWith('%') || !channels[2].EndsWith('%')
                || !TryComponent(channels[1], 1, out var s) || !TryComponent(channels[2], 1, out var l)) return false;
            h = (h % 360 + 360) % 360 / 60;
            s = Math.Clamp(s, 0, 1); l = Math.Clamp(l, 0, 1);
            var chroma = name == "hwb" ? 1 : (1 - Math.Abs(2 * l - 1)) * s;
            var x = chroma * (1 - Math.Abs(h % 2 - 1));
            (red, green, blue) = h switch
            {
                < 1 => (chroma, x, 0d),
                < 2 => (x, chroma, 0d),
                < 3 => (0d, chroma, x),
                < 4 => (0d, x, chroma),
                < 5 => (x, 0d, chroma),
                _ => (chroma, 0d, x),
            };
            if (name == "hwb")
            {
                if (s + l >= 1) red = green = blue = s / (s + l);
                else { red = red * (1 - s - l) + s; green = green * (1 - s - l) + s; blue = blue * (1 - s - l) + s; }
            }
            else { var m = l - chroma / 2; red += m; green += m; blue += m; }
            red *= 255; green *= 255; blue *= 255;
        }
        (r, g, b, alpha) = (Byte(red), Byte(green), Byte(blue), Math.Clamp(a, 0, 1));
        return true;
    }

    internal static bool TryComponent(string text, double scale, out double value)
    {
        var percent = text.EndsWith('%');
        var number = percent ? text.AsSpan(0, text.Length - 1) : text.AsSpan();
        if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out value) || !double.IsFinite(value)) return false;
        if (percent) value *= scale / 100;
        return true;
    }

    internal static bool TryHue(string text, out double degrees)
    {
        var scale = 1d;
        if (text.EndsWith("deg", StringComparison.Ordinal)) text = text[..^3];
        else if (text.EndsWith("grad", StringComparison.Ordinal)) { text = text[..^4]; scale = 0.9; }
        else if (text.EndsWith("rad", StringComparison.Ordinal)) { text = text[..^3]; scale = 180 / Math.PI; }
        else if (text.EndsWith("turn", StringComparison.Ordinal)) { text = text[..^4]; scale = 360; }
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out degrees) || !double.IsFinite(degrees)) return false;
        degrees *= scale;
        return double.IsFinite(degrees);
    }

    private static int Byte(double number) => (int) Math.Floor(Math.Clamp(number, 0, 255) + 0.5);
    private static string Rgba(int r, int g, int b, double alpha) => alpha == 1
        ? $"#{r:x2}{g:x2}{b:x2}"
        : string.Create(CultureInfo.InvariantCulture, $"rgba({r}, {g}, {b}, {alpha:0.###})");
}
