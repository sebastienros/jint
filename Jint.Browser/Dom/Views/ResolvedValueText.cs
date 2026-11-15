using System.Globalization;
using System.Text;
using Jint.Browser.Dom.Canvas;
using Jint.Browser.Styling;

namespace Jint.Browser.Dom.Views;

/// <summary>
/// Textual resolution of the image and grid-track values a renderless computed declaration can still answer:
/// gradient color stops become sRGB, and a grid container's explicit column tracks become pixel widths.
/// Anything outside that subset is returned as its computed text unchanged.
/// </summary>
internal static class ResolvedValueText
{
    internal static bool IsImage(string name) => name is "background-image" or "mask-image"
        or "list-style-image" or "border-image-source" or "mask-border-source";

    // CSS Images 3 §3 / CSS Color 4 §15.2: a computed <gradient> holds its color stops as computed colors,
    // serialized in the legacy rgb()/rgba() form; layers and arguments are serialized with ", ".
    internal static string Image(string value, NativeCssComputedStyle style)
    {
        if (value.IndexOf("gradient(", StringComparison.OrdinalIgnoreCase) < 0) return value;
        style.Work.Charge(value.Length);
        var builder = new StringBuilder(value.Length + 32);
        var first = true;
        foreach (var layer in Split(value, ','))
        {
            if (!first) builder.Append(", ");
            first = false;
            AppendLayer(builder, layer.Trim());
        }
        return builder.ToString();
    }

    private static void AppendLayer(StringBuilder builder, string layer)
    {
        var open = layer.IndexOf('(');
        if (open <= 0 || !layer.EndsWith(')') ||
            !layer.AsSpan(0, open).EndsWith("gradient", StringComparison.OrdinalIgnoreCase) ||
            !Closes(layer, open))
        {
            builder.Append(layer);
            return;
        }

        builder.Append(layer, 0, open + 1);
        var first = true;
        foreach (var argument in Split(layer.Substring(open + 1, layer.Length - open - 2), ','))
        {
            if (!first) builder.Append(", ");
            first = false;
            var firstToken = true;
            foreach (var token in Split(argument, ' '))
            {
                if (token.Length == 0) continue;
                if (!firstToken) builder.Append(' ');
                firstToken = false;
                builder.Append(CanvasColor.TryParse(token, out var r, out var g, out var b, out var alpha)
                    ? ResolvedStyle.Serialize(r, g, b, alpha)
                    : token);
            }
        }
        builder.Append(')');
    }

    // True when the parenthesis at `open` is the one the layer's final character closes.
    private static bool Closes(string layer, int open)
    {
        var depth = 0;
        for (var i = open; i < layer.Length; i++)
        {
            if (layer[i] == '(') depth++;
            else if (layer[i] == ')' && --depth == 0) return i == layer.Length - 1;
        }
        return false;
    }

    // Splits at `separator` outside parentheses and quotes; whitespace splitting treats any white space as one.
    private static List<string> Split(string text, char separator)
    {
        var parts = new List<string>();
        var depth = 0;
        var quote = '\0';
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quote != '\0')
            {
                if (c == '\\') i++;
                else if (c == quote) quote = '\0';
                continue;
            }
            if (c is '"' or '\'') quote = c;
            else if (c == '(') depth++;
            else if (c == ')') depth = Math.Max(0, depth - 1);
            else if (depth == 0 && (separator == ' ' ? char.IsWhiteSpace(c) : c == separator))
            {
                parts.Add(text.Substring(start, i - start).Trim());
                start = i + 1;
            }
        }
        parts.Add(text.Substring(start).Trim());
        return parts;
    }

    // CSS Grid 2 §7.2.6 (resolved value): a grid container reports used track sizes. Without a layout engine
    // only an explicit list of <length>px, <percentage> and <flex> tracks (optionally in an integer repeat())
    // over an empty or text-free container is resolvable: fixed tracks keep their size and §12.7.1 shares the
    // leftover space by flex factor. Any other track list (minmax, auto, names, auto-fill) keeps its text.
    internal static string GridColumns(string value, NativeCssComputedStyle style, double containerWidth)
    {
        if (!IsGridContainer(style)) return value;
        style.Work.Charge(value.Length);

        var tracks = new List<(double Size, bool Flexible)>();
        if (!Expand(value, tracks, containerWidth, allowRepeat: true) || tracks.Count == 0) return value;
        if (!TryLength(style.GetNormalizedProperty("column-gap").Text.Trim(), containerWidth, out var gap, normalIsZero: true))
            return value;

        var leftover = containerWidth - gap * (tracks.Count - 1);
        var flex = 0d;
        foreach (var (size, flexible) in tracks)
        {
            if (flexible) flex += size;
            else leftover -= size;
        }
        var unit = flex > 0 ? Math.Max(0, leftover) / Math.Max(1, flex) : 0;

        style.Work.Charge(tracks.Count * 8);
        var builder = new StringBuilder();
        foreach (var (size, flexible) in tracks)
        {
            if (builder.Length > 0) builder.Append(' ');
            builder.Append((flexible ? size * unit : size).ToString("0.######", CultureInfo.InvariantCulture)).Append("px");
        }
        return builder.ToString();
    }

    internal static bool IsGridContainer(NativeCssComputedStyle style)
    {
        var display = style.GetNormalizedProperty("display").Text.Trim();
        return display.Equals("grid", StringComparison.OrdinalIgnoreCase) ||
            display.Equals("inline-grid", StringComparison.OrdinalIgnoreCase);
    }

    private static bool Expand(string text, List<(double, bool)> tracks, double containerWidth, bool allowRepeat)
    {
        foreach (var token in Split(text, ' '))
        {
            if (token.Length == 0) continue;
            if (tracks.Count > 1000) return false;
            if (allowRepeat && token.StartsWith("repeat(", StringComparison.OrdinalIgnoreCase) && token.EndsWith(')'))
            {
                var arguments = Split(token.Substring(7, token.Length - 8), ',');
                if (arguments.Count != 2 ||
                    !int.TryParse(arguments[0], NumberStyles.None, CultureInfo.InvariantCulture, out var count) ||
                    count is < 1 or > 1000) return false;
                var start = tracks.Count;
                if (!Expand(arguments[1], tracks, containerWidth, allowRepeat: false)) return false;
                var pattern = tracks.Count - start;
                if (pattern == 0 || tracks.Count + pattern * (count - 1) > 1000) return false;
                for (var i = 1; i < count; i++)
                {
                    for (var j = 0; j < pattern; j++) tracks.Add(tracks[start + j]);
                }
                continue;
            }
            if (token.EndsWith("fr", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryNumber(token.AsSpan(0, token.Length - 2), out var factor)) return false;
                tracks.Add((factor, true));
                continue;
            }
            if (!TryLength(token, containerWidth, out var size, normalIsZero: false)) return false;
            tracks.Add((size, false));
        }
        return true;
    }

    private static bool TryLength(string text, double basis, out double length, bool normalIsZero)
    {
        length = 0;
        if (normalIsZero && (text.Length == 0 || text.Equals("normal", StringComparison.OrdinalIgnoreCase))) return true;
        if (text == "0") return true;
        if (text.EndsWith("px", StringComparison.OrdinalIgnoreCase)) return TryNumber(text.AsSpan(0, text.Length - 2), out length);
        if (text.EndsWith('%') && TryNumber(text.AsSpan(0, text.Length - 1), out var percent))
        {
            length = basis * percent / 100;
            return true;
        }
        return false;
    }

    private static bool TryNumber(ReadOnlySpan<char> text, out double number) =>
        double.TryParse(text.ToString(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out number) &&
        !double.IsInfinity(number);
}
