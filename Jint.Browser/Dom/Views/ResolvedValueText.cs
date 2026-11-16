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
    // leftover space by flex factor, minmax(<length-percentage>, <flex>) with its floor. Any other track list
    // (other minmax forms, auto, names, auto-fill) keeps its text.
    internal static string GridColumns(string value, NativeCssComputedStyle style, double containerWidth, Func<string> columnGap)
    {
        if (!IsGridContainer(style)) return value;
        style.Work.Charge(value.Length);

        if (ColumnTracks(value, containerWidth, columnGap(), out _) is not { } tracks) return value;

        style.Work.Charge(tracks.Length * 8);
        var builder = new StringBuilder();
        foreach (var size in tracks)
        {
            if (builder.Length > 0) builder.Append(' ');
            builder.Append(size.ToString("0.######", CultureInfo.InvariantCulture)).Append("px");
        }
        return builder.ToString();
    }

    private static readonly System.Text.RegularExpressions.Regex s_trackOpen = new(@"\(\s+");
    private static readonly System.Text.RegularExpressions.Regex s_trackClose = new(@"\s+\)");
    private static readonly System.Text.RegularExpressions.Regex s_trackComma = new(@"\s*,\s*");

    /// <summary>
    /// The used widths of a column track list in a container <paramref name="containerWidth"/> wide: absolute
    /// lengths as they are, and the space they and the gaps leave shared between the <c>fr</c> tracks. Null for
    /// a track list it cannot size without content — <c>auto</c>, a <c>minmax()</c> whose maximum is not
    /// <c>fr</c>, an <c>auto-fill</c> repeat.
    /// </summary>
    internal static double[]? ColumnTracks(string value, double containerWidth, string columnGap, out double gap)
    {
        gap = 0;
        var tracks = new List<(double Size, bool Flexible, double Min)>();

        // A computed value keeps the author's spacing inside repeat(), which a substituted var() leaves too.
        value = s_trackComma.Replace(s_trackClose.Replace(s_trackOpen.Replace(value.Trim(), "("), ")"), ", ");
        if (!Expand(value, tracks, containerWidth, allowRepeat: true) || tracks.Count == 0) return null;
        if (!TryLength(columnGap.Trim(), containerWidth, out gap, normalIsZero: true)) return null;

        // §12.7.1 "find the size of an fr": share the space left by the fixed tracks and the gaps by flex
        // factor; a flexible track whose share is below its minmax() floor is treated as inflexible at that
        // floor, and the rest is shared again among the others (each pass freezes one track at least).
        var frozen = new bool[tracks.Count];
        var unit = 0d;
        for (var pass = 0; pass <= tracks.Count; pass++)
        {
            var leftover = containerWidth - gap * (tracks.Count - 1);
            var flex = 0d;
            for (var i = 0; i < tracks.Count; i++)
            {
                var (size, flexible, min) = tracks[i];
                if (flexible && !frozen[i]) flex += size;
                else leftover -= flexible ? min : size;
            }
            unit = flex > 0 ? Math.Max(0, leftover) / Math.Max(1, flex) : 0;

            var changed = false;
            for (var i = 0; i < tracks.Count; i++)
            {
                if (tracks[i].Flexible && !frozen[i] && tracks[i].Size * unit < tracks[i].Min)
                {
                    frozen[i] = true;
                    changed = true;
                }
            }
            if (!changed) break;
        }

        var sizes = new double[tracks.Count];
        for (var i = 0; i < sizes.Length; i++)
        {
            var (size, flexible, min) = tracks[i];
            sizes[i] = !flexible ? size : frozen[i] ? min : Math.Max(min, size * unit);
        }
        return sizes;
    }

    /// <summary>
    /// CSS Grid 2 §7.2.6 for rows: a row track list made only of absolute lengths, optionally in an integer
    /// <c>repeat()</c>, has used sizes equal to those lengths whatever the container's height, so it resolves
    /// without a layout. Percentages and flexible tracks need a definite height the flat model does not
    /// have, and keep their text, as every other track list does.
    /// </summary>
    internal static string GridRows(string value, NativeCssComputedStyle style)
    {
        if (!IsGridContainer(style)) return value;
        style.Work.Charge(value.Length);

        var tracks = new List<(double Size, bool Flexible, double Min)>();
        if (!Expand(value, tracks, double.NaN, allowRepeat: true) || tracks.Count == 0) return value;

        var builder = new StringBuilder();
        foreach (var (size, flexible, _) in tracks)
        {
            if (flexible || double.IsNaN(size)) return value;
            if (builder.Length > 0) builder.Append(' ');
            builder.Append(size.ToString("0.######", CultureInfo.InvariantCulture)).Append("px");
        }

        style.Work.Charge(tracks.Count * 8);
        return builder.ToString();
    }

    internal static bool IsGridContainer(NativeCssComputedStyle style)
    {
        var display = style.GetNormalizedProperty("display").Text.Trim();
        return display.Equals("grid", StringComparison.OrdinalIgnoreCase) ||
            display.Equals("inline-grid", StringComparison.OrdinalIgnoreCase);
    }

    private static bool Expand(string text, List<(double Size, bool Flexible, double Min)> tracks, double containerWidth, bool allowRepeat)
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
            if (token.StartsWith("minmax(", StringComparison.OrdinalIgnoreCase) && token.EndsWith(')'))
            {
                // minmax(<length-percentage>, <flex>): a flexible track that never shrinks below its floor, which
                // minmax(0, 1fr) leaves at zero, the usual way to keep long content from widening a column.
                // Other minmax() forms need content to size and keep their text.
                var bounds = Split(token.Substring(7, token.Length - 8), ',');
                if (bounds.Count != 2 || !bounds[1].EndsWith("fr", StringComparison.OrdinalIgnoreCase) ||
                    !TryNumber(bounds[1].AsSpan(0, bounds[1].Length - 2), out var flexFactor) ||
                    !TryLength(bounds[0], containerWidth, out var floor, normalIsZero: false)) return false;
                tracks.Add((flexFactor, true, floor));
                continue;
            }
            if (token.EndsWith("fr", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryNumber(token.AsSpan(0, token.Length - 2), out var factor)) return false;
                tracks.Add((factor, true, 0));
                continue;
            }
            if (!TryLength(token, containerWidth, out var size, normalIsZero: false)) return false;
            tracks.Add((size, false, 0));
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
