using Jint.HtmlParser.Css.Syntax;

namespace Jint.HtmlParser.Css.Values.Properties;

// CSS Backgrounds 3 §2.11 (background), §2.6 (background-position) and §4.4-§4.5 (border):
// shorthands whose components are optional and order-free, so whitespace splitting cannot place them.
// https://drafts.csswg.org/css-backgrounds-3/#background
// Renderless: a component is classified by its token shape only and keeps its declared text.
// Anything the box keywords, lengths and image functions do not claim is taken as the color.
internal static class CssBoxShorthands
{
    internal static readonly string[] Sides = ["top", "right", "bottom", "left"];

    internal static readonly string[] BackgroundLonghands =
    [
        "background-image", "background-position-x", "background-position-y", "background-size",
        "background-repeat", "background-attachment", "background-origin", "background-clip", "background-color"
    ];

    private static readonly string[] BackgroundInitials =
        ["none", "0%", "0%", "auto", "repeat", "scroll", "padding-box", "border-box", "transparent"];

    internal static bool Handles(string name) => name is "background" or "background-position" or "border" or "border-color" ||
        name.StartsWith("border-", StringComparison.Ordinal) && Array.IndexOf(Sides, name[7..]) >= 0;

    /// <summary>The longhand values in registry order, or null when the text is not a valid value.</summary>
    internal static string[]? Expand(string name, string text, CssValueWork work)
    {
        var layers = Layers(text, work);
        if (layers is null) return null;
        if (name == "background") return Background(layers, work);
        if (layers.Count != 1) return null;
        if (name == "background-position")
            return Position(layers[0], 0, layers[0].Count, work) is { } position ? [position.X, position.Y] : null;
        if (name == "border-color") return Colors(layers[0], work);
        if (Line(layers[0], work) is not { } line) return null;
        if (name != "border") return line;
        var result = new string[12];
        for (var i = 0; i < 4; i++)
        {
            work.Charge(1);
            result[i] = line[0];
            result[4 + i] = line[1];
            result[8 + i] = line[2];
        }
        return result;
    }

    /// <summary>The shortest text that expands back to these values, or empty when there is none.</summary>
    internal static string Serialize(string name, string[] values, CssValueWork work)
    {
        work.Charge(values.Length);
        if (name == "background-position") return values[0] + " " + values[1];
        if (name == "background") return SerializeBackground(values, work);
        if (name == "border-color")
        {
            var count = values[3] == values[1] ? values[2] == values[0] ? values[1] == values[0] ? 1 : 2 : 3 : 4;
            return Joined(values.Take(count), work);
        }
        if (name == "border")
        {
            for (var i = 1; i < 4; i++)
            {
                if (values[i] != values[0] || values[4 + i] != values[4] || values[8 + i] != values[8]) return "";
            }
            return SerializeLine(values[0], values[4], values[8], work);
        }
        return SerializeLine(values[0], values[1], values[2], work);
    }

    // §4.3 border-color: one to four colors, assigned top, right, bottom, left.
    private static string[]? Colors(List<Item> items, CssValueWork work)
    {
        if (items.Count is < 1 or > 4) return null;
        foreach (var item in items)
        {
            if (!IsColor(item, work)) return null;
        }
        var c = items.Select(static item => item.Text).ToArray();
        return [c[0], c.Length > 1 ? c[1] : c[0], c.Length > 2 ? c[2] : c[0], c.Length > 3 ? c[3] : c.Length > 1 ? c[1] : c[0]];
    }

    // <line-width> || <line-style> || <color>, each at most once.
    private static string[]? Line(List<Item> items, CssValueWork work)
    {
        string? width = null, style = null, color = null;
        foreach (var item in items)
        {
            work.Charge(1);
            if (width is null && IsLineWidth(item, work)) width = item.Text;
            else if (style is null && IsLineStyle(item.Keyword)) style = item.Text;
            else if (color is null && IsColor(item, work)) color = item.Text;
            else return null;
        }
        return items.Count == 0 ? null : [width ?? "medium", style ?? "none", color ?? "currentcolor"];
    }

    private static string SerializeLine(string width, string style, string color, CssValueWork work)
    {
        var parts = new List<string>(3);
        if (width != "medium") parts.Add(width);
        if (style != "none") parts.Add(style);
        if (color != "currentcolor") parts.Add(color);
        return parts.Count == 0 ? "none" : Joined(parts, work);
    }

    private static string Joined(IEnumerable<string> parts, CssValueWork work, string separator = " ")
    {
        var result = string.Join(separator, parts);
        work.Charge(result.Length);
        return result;
    }

    // [ <bg-layer> , ]* <final-bg-layer>; only the final layer may carry a color.
    private static string[]? Background(List<List<Item>> layers, CssValueWork work)
    {
        var lists = new List<string>[BackgroundLonghands.Length - 1];
        for (var i = 0; i < lists.Length; i++) lists[i] = new(layers.Count);
        string? color = null;
        for (var layer = 0; layer < layers.Count; layer++)
        {
            var items = layers[layer];
            if (items.Count == 0) return null;
            string? image = null, x = null, y = null, size = null, repeat = null, attachment = null, origin = null, clip = null;
            for (var i = 0; i < items.Count; i++)
            {
                work.Charge(1);
                var item = items[i];
                var keyword = item.Keyword;
                if (IsPosition(item, work))
                {
                    if (x is not null) return null;
                    var end = i;
                    while (end < items.Count && end - i < 4 && IsPosition(items[end], work)) end++;
                    if (Position(items, i, end, work) is not { } position) return null;
                    (x, y) = position;
                    i = end - 1;
                    if (i + 1 < items.Count && items[i + 1].IsSlash)
                    {
                        if (Size(items, i + 2, work) is not { } parsed) return null;
                        (size, var count) = parsed;
                        i += 1 + count;
                    }
                }
                else if (item.IsSlash) return null;
                else if (image is null && IsImage(item)) image = item.Text;
                else if (repeat is null && keyword is "repeat-x" or "repeat-y") repeat = item.Text;
                else if (repeat is null && IsRepeat(keyword))
                {
                    repeat = item.Text;
                    if (i + 1 < items.Count && IsRepeat(items[i + 1].Keyword)) repeat += " " + items[++i].Text;
                }
                else if (attachment is null && keyword is "scroll" or "fixed" or "local") attachment = item.Text;
                else if (origin is null && keyword is "border-box" or "padding-box" or "content-box") origin = item.Text;
                else if (clip is null && keyword is "border-box" or "padding-box" or "content-box" or "text") clip = item.Text;
                else if (color is null && layer == layers.Count - 1 && IsColor(item, work)) color = item.Text;
                else return null;
            }
            // §2.11: one <visual-box> sets both background-origin and background-clip.
            if (origin is not null && clip is null) clip = origin;
            var values = new[] { image, x, y, size, repeat, attachment, origin, clip };
            for (var i = 0; i < lists.Length; i++)
            {
                work.Charge(1);
                lists[i].Add(values[i] ?? BackgroundInitials[i]);
            }
        }
        var result = new string[BackgroundLonghands.Length];
        for (var i = 0; i < lists.Length; i++) result[i] = Joined(lists[i], work, ", ");
        result[^1] = color ?? "transparent";
        return result;
    }

    private static string SerializeBackground(string[] values, CssValueWork work)
    {
        var lists = new string[values.Length - 1][];
        for (var i = 0; i < lists.Length; i++)
        {
            lists[i] = SplitLayers(values[i], work);
            if (lists[i].Length != lists[0].Length) return "";
        }
        var layers = new List<string>(lists[0].Length);
        for (var layer = 0; layer < lists[0].Length; layer++)
        {
            work.Charge(1);
            string At(int index) => lists[index][layer];
            var parts = new List<string>();
            if (At(0) != "none") parts.Add(At(0));
            if (At(1) != "0%" || At(2) != "0%" || At(3) != "auto")
                parts.Add(At(3) == "auto" ? At(1) + " " + At(2) : At(1) + " " + At(2) + " / " + At(3));
            if (At(4) != "repeat") parts.Add(At(4));
            if (At(5) != "scroll") parts.Add(At(5));
            if (At(6) != "padding-box" || At(7) != "border-box")
                parts.Add(At(6) == At(7) ? At(6) : At(6) + " " + At(7));
            if (layer == lists[0].Length - 1 && values[^1] != "transparent") parts.Add(values[^1]);
            layers.Add(parts.Count == 0 ? "none" : Joined(parts, work));
        }
        return Joined(layers, work, ", ");
    }

    // §2.6 <bg-position>: one or two values, or three or four as keyword/offset pairs.
    private static (string X, string Y)? Position(List<Item> items, int start, int end, CssValueWork work)
    {
        var count = end - start;
        if (count is < 1 or > 4) return null;
        if (count == 1)
        {
            var only = items[start];
            return only.Keyword is "top" or "bottom" ? ("center", only.Text) : (only.Text, "center");
        }
        if (count == 2)
        {
            var (first, second) = (items[start], items[start + 1]);
            if (first.Keyword is "top" or "bottom" || second.Keyword is "left" or "right")
                (first, second) = (second, first);
            if (first.Keyword is "top" or "bottom" || second.Keyword is "left" or "right") return null;
            return (first.Text, second.Text);
        }
        string? x = null, y = null, pendingCenter = null;
        for (var i = start; i < end; i++)
        {
            work.Charge(1);
            var keyword = items[i].Keyword;
            if (keyword.Length == 0) return null;
            var text = items[i].Text;
            if (i + 1 < end && items[i + 1].Keyword.Length == 0)
            {
                if (keyword == "center") return null;
                text += " " + items[++i].Text;
            }
            if (keyword is "left" or "right" && x is null) x = text;
            else if (keyword is "top" or "bottom" && y is null) y = text;
            else if (keyword == "center" && pendingCenter is null) pendingCenter = text;
            else return null;
        }
        if (pendingCenter is not null)
        {
            if (x is null) x = pendingCenter;
            else if (y is null) y = pendingCenter;
            else return null;
        }
        return x is null || y is null ? null : (x, y);
    }

    // §2.9 <bg-size>: cover, contain, or one or two of auto and <length-percentage>.
    private static (string Size, int Count)? Size(List<Item> items, int start, CssValueWork work)
    {
        if (start >= items.Count) return null;
        if (items[start].Keyword is "cover" or "contain") return (items[start].Text, 1);
        if (!IsSize(items[start], work)) return null;
        if (start + 1 < items.Count && IsSize(items[start + 1], work))
            return (items[start].Text + " " + items[start + 1].Text, 2);
        return (items[start].Text, 1);
    }

    private static bool IsSize(Item item, CssValueWork work) => item.Keyword == "auto" || IsLengthPercentage(item, work, negative: false);
    private static bool IsRepeat(string keyword) => keyword is "repeat" or "space" or "round" or "no-repeat";

    private static bool IsPosition(Item item, CssValueWork work) =>
        item.Keyword is "left" or "right" or "center" or "top" or "bottom" || IsLengthPercentage(item, work);

    private static bool IsImage(Item item)
    {
        if (item.Keyword == "none" || item.Value.TokenKind == CssTokenKind.Url) return true;
        if (item.Value.Kind != CssComponentKind.Function) return false;
        var function = item.Value.FunctionName;
        if (function.Length > 24) return false;
        function = function.ToLowerInvariant();
        return function is "url" or "src" or "image" or "image-set" or "-webkit-image-set" or "cross-fade" or "element" or "paint" ||
            function.EndsWith("gradient", StringComparison.Ordinal);
    }

    // §4.2 <line-width>: a keyword or a non-negative length.
    private static bool IsLineWidth(Item item, CssValueWork work) =>
        item.Keyword is "thin" or "medium" or "thick" || IsLength(item, work, percentage: false, negative: false);

    private static bool IsLineStyle(string keyword) => CssLayoutValues.IsLineStyle(keyword);

    private static bool IsLengthPercentage(Item item, CssValueWork work, bool negative = true) =>
        IsLength(item, work, percentage: true, negative);

    private static bool IsLength(Item item, CssValueWork work, bool percentage, bool negative)
    {
        var value = item.Value;
        if (value.Kind == CssComponentKind.Function) return CssLayoutValues.Math(value);
        var kind = value.TokenKind;
        if (kind is not (CssTokenKind.Number or CssTokenKind.Percentage or CssTokenKind.Dimension)) return false;
        var sign = CssNumber.FromValidatedToken(value.Token.NumberText, work).Sign;
        if (!negative && sign < 0) return false;
        return kind switch
        {
            CssTokenKind.Number => sign == 0,
            CssTokenKind.Percentage => percentage,
            _ => CssUnits.Recognize(value.Token.Unit, work).Category() == CssUnitCategory.Length
        };
    }

    // A color is an identifier, a hash or a non-math, non-image function; the cascade keeps its text.
    // Keywords another component owns are never a color, so a repeated one leaves the value invalid.
    private static bool IsColor(Item item, CssValueWork work) => item.Value.Kind == CssComponentKind.Function
        ? !CssLayoutValues.Math(item.Value) && !IsImage(item)
        : item.Value.TokenKind == CssTokenKind.Hash ||
          item.Value.TokenKind == CssTokenKind.Ident && !CssText.IsWide(item.Keyword) &&
          !IsLineStyle(item.Keyword) && !IsRepeat(item.Keyword) && item.Keyword is not
              ("auto" or "thin" or "medium" or "thick" or "repeat-x" or "repeat-y" or "scroll" or "fixed" or "local" or
               "border-box" or "padding-box" or "content-box" or "text" or "cover" or "contain" or
               "left" or "right" or "center" or "top" or "bottom");

    // Top-level comma-separated layers of non-whitespace components, or null on a token no value accepts.
    internal static List<List<Item>>? Layers(string text, CssValueWork work)
    {
        using var parser = new CssSyntaxParser(text, null, work.Token, work.CheckCancellation);
        var values = parser.ParseComponentValues();
        var layers = new List<List<Item>> { new() };
        foreach (var value in values)
        {
            work.Charge(1);
            switch (value.TokenKind)
            {
                case CssTokenKind.Whitespace: continue;
                case CssTokenKind.Comma: layers.Add([]); continue;
                case CssTokenKind.Semicolon or CssTokenKind.BadString or CssTokenKind.BadUrl: return null;
            }
            if (value.Kind == CssComponentKind.SimpleBlock) return null;
            work.Charge(value.Span.Length);
            layers[^1].Add(new(value, text.Substring(value.Span.Start, value.Span.Length), CssLayoutValues.Keyword(value)));
        }
        return layers;
    }

    internal static string[] SplitLayers(string value, CssValueWork work)
    {
        var layers = new List<string>();
        var depth = 0;
        var start = 0;
        for (var i = 0; i < value.Length; i++)
        {
            work.Charge(1);
            switch (value[i])
            {
                case '(': depth++; break;
                case ')' when depth > 0: depth--; break;
                case ',' when depth == 0:
                    layers.Add(value[start..i].Trim());
                    start = i + 1;
                    break;
            }
        }
        layers.Add(value[start..].Trim());
        return layers.ToArray();
    }

    // Keyword is the ASCII-lowered identifier, or empty for anything else.
    internal readonly record struct Item(CssComponentValue Value, string Text, string Keyword)
    {
        internal bool IsSlash => Value.TokenKind == CssTokenKind.Delim && Value.Token.Delimiter == '/';
    }
}
