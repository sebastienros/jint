using System.Globalization;
using System.Text;
using Jint.HtmlParser.Css.Syntax;

namespace Jint.HtmlParser.Css.Values;

/// <summary>The <c>@font-face</c> descriptors a <c>FontFace</c> attribute reads and writes.</summary>
internal enum CssFontFaceDescriptor : byte
{
    Style,
    Weight,
    Stretch,
    UnicodeRange,
    Variant,
    FeatureSettings,
    VariationSettings,
    Display,
    AscentOverride,
    DescentOverride,
    LineGapOverride,
    SizeAdjust,
}

/// <summary>One entry of an <c>@font-face</c> <c>src</c> list: a <c>url()</c>, or a <c>local()</c> family name.</summary>
internal readonly record struct CssFontSource(bool IsLocal, string Value, string? Format);

/// <summary>
/// Reads the grammars CSS Font Loading hands to script on demand: the <c>@font-face</c> descriptors a
/// <c>FontFace</c> is built from, its <c>src</c> list, and the <c>font</c> shorthand
/// <c>FontFaceSet.check()</c> and <c>load()</c> take.
/// </summary>
/// <remarks>
/// <para>
/// https://drafts.csswg.org/css-font-loading/#font-face-constructor parses each argument "according to the
/// grammars of the corresponding descriptors of the CSS @font-face rule"
/// (https://drafts.csswg.org/css-fonts-4/#font-face-rule), and each setter does the same and throws on
/// failure. A successful parse answers the value's serialization: keywords in lowercase, numbers in their
/// shortest form, a <c>unicode-range</c> as upper-case hexadecimal ranges, a feature tag quoted and its
/// default value <c>1</c> omitted.
/// </para>
/// <para>
/// Nothing in parsing calls it: a style sheet's <c>@font-face</c> descriptors stay text, per the
/// <a href="../../README.md#renderless-css-boundary">renderless boundary</a>. <c>font-variant</c> is read
/// laxly — any sequence of identifiers and functions that is not a CSS-wide keyword — because its grammar
/// is a dozen sub-properties' worth of keywords and nothing here matches on it.
/// </para>
/// </remarks>
internal static class CssFontFaceValues
{
    /// <summary>
    /// Parses <paramref name="source"/> as <paramref name="descriptor"/>'s grammar and answers its
    /// serialization, or <see langword="false"/> when it does not match.
    /// </summary>
    internal static bool TryParseDescriptor(CssFontFaceDescriptor descriptor, string source, CssValueWork work, out string serialization)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(work);
        serialization = "";
        if (descriptor == CssFontFaceDescriptor.UnicodeRange)
        {
            if (!TryParseUnicodeRange(source, work, out var ranges))
            {
                return false;
            }

            serialization = SerializeRanges(ranges);
            return true;
        }

        if (!TryItems(source, work, out var items) || items.Count == 0 || IsWideKeyword(items))
        {
            return false;
        }

        var result = descriptor switch
        {
            CssFontFaceDescriptor.Style => Style(items),
            CssFontFaceDescriptor.Weight => Weight(items),
            CssFontFaceDescriptor.Stretch => Stretch(items),
            CssFontFaceDescriptor.Variant => Variant(items),
            CssFontFaceDescriptor.FeatureSettings => FeatureSettings(items),
            CssFontFaceDescriptor.VariationSettings => VariationSettings(items),
            CssFontFaceDescriptor.Display => items.Count == 1 && Keyword(items[0], "auto", "block", "swap", "fallback", "optional") is { } display ? display : null,
            CssFontFaceDescriptor.AscentOverride or CssFontFaceDescriptor.DescentOverride or CssFontFaceDescriptor.LineGapOverride
                => MetricOverride(items),
            CssFontFaceDescriptor.SizeAdjust => items.Count == 1 && Percentage(items[0]) is { } size ? size : null,
            _ => null,
        };

        if (result is null)
        {
            return false;
        }

        serialization = result;
        return true;
    }

    /// <summary>
    /// https://drafts.csswg.org/css-fonts-4/#src-desc —
    /// <c>[ &lt;url&gt; [ format(...) ]? [ tech(...) ]? | local(&lt;family-name&gt;) ]#</c>.
    /// </summary>
    internal static bool TryParseSource(string source, CssValueWork work, out CssFontSource[] sources)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(work);
        sources = [];
        if (!TryItems(source, work, out var items) || items.Count == 0)
        {
            return false;
        }

        var list = new List<CssFontSource>();
        foreach (var group in SplitOnCommas(items))
        {
            work.Charge(1);
            if (group.Count == 0)
            {
                return false;
            }

            var first = group[0];
            if (first.Kind == CssComponentKind.Function && IsAscii(first.FunctionName, "local"))
            {
                if (group.Count != 1 || !TryFamilyName(Significant(first.Values), out var family))
                {
                    return false;
                }

                list.Add(new CssFontSource(true, family, null));
                continue;
            }

            if (!TryUrl(first, out var url))
            {
                return false;
            }

            string? format = null;
            var index = 1;
            if (index < group.Count && group[index].Kind == CssComponentKind.Function && IsAscii(group[index].FunctionName, "format"))
            {
                var arguments = Significant(group[index].Values);
                if (arguments.Count != 1 || arguments[0].Kind != CssComponentKind.Token
                    || arguments[0].Token.Kind is not (CssTokenKind.String or CssTokenKind.Ident))
                {
                    return false;
                }

                format = arguments[0].Token.Text;
                index++;
            }

            if (index < group.Count && group[index].Kind == CssComponentKind.Function && IsAscii(group[index].FunctionName, "tech"))
            {
                index++;
            }

            if (index != group.Count)
            {
                return false;
            }

            list.Add(new CssFontSource(false, url, format));
        }

        sources = [.. list];
        return true;
    }

    /// <summary>
    /// The family list of a <c>font</c> shorthand (https://drafts.csswg.org/css-fonts-4/#font-prop), which is
    /// all <c>FontFaceSet</c>'s "find the matching font faces" matches on. A system-font keyword succeeds with
    /// no families; a CSS-wide keyword, or anything without a size and a family, fails.
    /// </summary>
    internal static bool TryParseFontFamilies(string font, CssValueWork work, out string[] families)
    {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(work);
        families = [];
        if (!TryItems(font, work, out var items) || items.Count == 0 || IsWideKeyword(items))
        {
            return false;
        }

        if (items.Count == 1 && Keyword(items[0], "caption", "icon", "menu", "message-box", "small-caption", "status-bar") is not null)
        {
            return true;
        }

        var index = 0;
        var prefix = 0;
        while (index < items.Count && prefix < 4 && IsFontPrefix(items[index]))
        {
            work.Charge(1);
            var obliqueAngle = Keyword(items[index], "oblique") is not null && index + 1 < items.Count && Angle(items[index + 1]) is not null;
            index += obliqueAngle ? 2 : 1;
            prefix++;
        }

        if (index >= items.Count || !IsFontSize(items[index]))
        {
            return false;
        }

        index++;
        if (index < items.Count && items[index] is { Kind: CssComponentKind.Token } slash
            && slash.Token.Kind == CssTokenKind.Delim && slash.Token.Delimiter == '/')
        {
            if (index + 1 >= items.Count || !IsLineHeight(items[index + 1]))
            {
                return false;
            }

            index += 2;
        }

        if (index >= items.Count)
        {
            return false;
        }

        var names = new List<string>();
        foreach (var group in SplitOnCommas(items.GetRange(index, items.Count - index)))
        {
            work.Charge(1);
            if (!TryFamilyName(group, out var family))
            {
                return false;
            }

            names.Add(family);
        }

        families = [.. names];
        return true;
    }

    /// <summary>
    /// https://drafts.csswg.org/css-fonts-4/#unicode-range-desc — <c>&lt;urange&gt;#</c>, each a
    /// <c>U+</c> code point, range or wildcard pattern (https://drafts.csswg.org/css-syntax-3/#urange-syntax).
    /// </summary>
    internal static bool TryParseUnicodeRange(string source, CssValueWork work, out (int Start, int End)[] ranges)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(work);
        ranges = [];
        var list = new List<(int, int)>();
        foreach (var part in source.Split(','))
        {
            work.Charge(part.Length + 1);
            var text = part.Trim(' ', '\t', '\n', '\r', '\f');
            if (text.Length < 3 || text[0] is not ('u' or 'U') || text[1] != '+')
            {
                return false;
            }

            var body = text.AsSpan(2);
            var dash = body.IndexOf('-');
            int start;
            int end;
            if (dash >= 0)
            {
                if (!TryHex(body[..dash], out start) || !TryHex(body[(dash + 1)..], out end))
                {
                    return false;
                }
            }
            else
            {
                var wildcards = 0;
                while (wildcards < body.Length && body[body.Length - 1 - wildcards] == '?')
                {
                    wildcards++;
                }

                var digits = body[..(body.Length - wildcards)];
                if (body.Length > 6 || (digits.Length == 0 && wildcards == 0) || (digits.Length > 0 && !TryHex(digits, out _)))
                {
                    return false;
                }

                var prefix = digits.Length == 0 ? 0 : int.Parse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                var shift = wildcards * 4;
                start = prefix << shift;
                end = start | ((1 << shift) - 1);
            }

            if (start > 0x10FFFF || end > 0x10FFFF || start > end)
            {
                return false;
            }

            list.Add((start, end));
        }

        ranges = [.. list];
        return true;
    }

    private static string SerializeRanges((int Start, int End)[] ranges)
    {
        var builder = new StringBuilder();
        foreach (var (start, end) in ranges)
        {
            if (builder.Length != 0)
            {
                builder.Append(", ");
            }

            builder.Append("U+").Append(start.ToString("X", CultureInfo.InvariantCulture));
            if (end != start)
            {
                builder.Append('-').Append(end.ToString("X", CultureInfo.InvariantCulture));
            }
        }

        return builder.ToString();
    }

    private static bool TryHex(ReadOnlySpan<char> text, out int value)
    {
        value = 0;
        if (text.Length is 0 or > 6)
        {
            return false;
        }

        foreach (var c in text)
        {
            if (!char.IsAsciiHexDigit(c))
            {
                return false;
            }
        }

        value = int.Parse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
        return true;
    }

    // https://drafts.csswg.org/css-fonts-4/#font-prop-desc — auto | normal | italic | oblique <angle>{0,2}.
    private static string? Style(List<CssComponentValue> items)
    {
        if (items.Count == 1 && Keyword(items[0], "auto", "normal", "italic", "oblique") is { } keyword)
        {
            return keyword;
        }

        if (items.Count is 2 or 3 && Keyword(items[0], "oblique") is not null)
        {
            var parts = new List<string> { "oblique" };
            for (var i = 1; i < items.Count; i++)
            {
                if (Angle(items[i]) is not { } angle)
                {
                    return null;
                }

                parts.Add(angle);
            }

            return string.Join(' ', parts);
        }

        return null;
    }

    // https://drafts.csswg.org/css-fonts-4/#font-weight-desc — auto | <font-weight-absolute>{1,2}.
    private static string? Weight(List<CssComponentValue> items)
    {
        if (items.Count == 1 && Keyword(items[0], "auto") is not null)
        {
            return "auto";
        }

        return Pair(items, static item => Keyword(item, "normal", "bold")
            ?? (Number(item) is { } n && n.Value is >= 1 and <= 1000 ? n.Text : null));
    }

    // https://drafts.csswg.org/css-fonts-4/#font-stretch-desc — auto | <'font-width'>{1,2}.
    private static string? Stretch(List<CssComponentValue> items)
    {
        if (items.Count == 1 && Keyword(items[0], "auto") is not null)
        {
            return "auto";
        }

        return Pair(items, static item => Keyword(item, "normal", "ultra-condensed", "extra-condensed", "condensed",
                "semi-condensed", "semi-expanded", "expanded", "extra-expanded", "ultra-expanded")
            ?? Percentage(item));
    }

    private static string? Variant(List<CssComponentValue> items)
    {
        var parts = new List<string>();
        foreach (var item in items)
        {
            if (item.Kind == CssComponentKind.Function)
            {
                parts.Add(item.FunctionName.ToLowerInvariant() + "(" + Serialize(Significant(item.Values), ", ") + ")");
            }
            else if (item.Kind == CssComponentKind.Token && item.Token.Kind == CssTokenKind.Ident)
            {
                parts.Add(item.Token.Text.ToLowerInvariant());
            }
            else
            {
                return null;
            }
        }

        return string.Join(' ', parts);
    }

    // https://drafts.csswg.org/css-fonts-4/#font-feature-settings-prop — normal | [<opentype-tag> [<integer [0,∞]> | on | off]?]#.
    private static string? FeatureSettings(List<CssComponentValue> items)
    {
        if (items.Count == 1 && Keyword(items[0], "normal") is not null)
        {
            return "normal";
        }

        var parts = new List<string>();
        foreach (var group in SplitOnCommas(items))
        {
            if (group.Count is 0 or > 2 || Tag(group[0]) is not { } tag)
            {
                return null;
            }

            var value = "1";
            if (group.Count == 2)
            {
                if (Keyword(group[1], "on", "off") is { } toggle)
                {
                    value = toggle == "on" ? "1" : "0";
                }
                else if (Number(group[1]) is { } number && group[1].Token.IsInteger && number.Value >= 0)
                {
                    value = number.Text;
                }
                else
                {
                    return null;
                }
            }

            parts.Add(value == "1" ? tag : tag + " " + value);
        }

        return string.Join(", ", parts);
    }

    // https://drafts.csswg.org/css-fonts-4/#font-variation-settings-def — normal | [<opentype-tag> <number>]#.
    private static string? VariationSettings(List<CssComponentValue> items)
    {
        if (items.Count == 1 && Keyword(items[0], "normal") is not null)
        {
            return "normal";
        }

        var parts = new List<string>();
        foreach (var group in SplitOnCommas(items))
        {
            if (group.Count != 2 || Tag(group[0]) is not { } tag || Number(group[1]) is not { } number)
            {
                return null;
            }

            parts.Add(tag + " " + number.Text);
        }

        return string.Join(", ", parts);
    }

    // https://drafts.csswg.org/css-fonts-4/#descdef-font-face-ascent-override — normal | <percentage [0,∞]>{1,2}.
    private static string? MetricOverride(List<CssComponentValue> items)
    {
        if (items.Count == 1 && Keyword(items[0], "normal") is not null)
        {
            return "normal";
        }

        return Pair(items, Percentage);
    }

    private static string? Pair(List<CssComponentValue> items, Func<CssComponentValue, string?> one)
    {
        if (items.Count is 0 or > 2)
        {
            return null;
        }

        var first = one(items[0]);
        if (first is null)
        {
            return null;
        }

        if (items.Count == 1)
        {
            return first;
        }

        var second = one(items[1]);
        return second is null ? null : first + " " + second;
    }

    private static string? Tag(CssComponentValue item)
    {
        if (item.Kind != CssComponentKind.Token || item.Token.Kind != CssTokenKind.String)
        {
            return null;
        }

        var text = item.Token.Text;
        if (text.Length != 4)
        {
            return null;
        }

        foreach (var c in text)
        {
            if (c is < ' ' or > '~')
            {
                return null;
            }
        }

        return QuoteString(text);
    }

    private static string? Keyword(CssComponentValue item, params string[] keywords)
    {
        if (item.Kind != CssComponentKind.Token || item.Token.Kind != CssTokenKind.Ident)
        {
            return null;
        }

        foreach (var keyword in keywords)
        {
            if (IsAscii(item.Token.Text, keyword))
            {
                return keyword;
            }
        }

        return null;
    }

    private static (double Value, string Text)? Number(CssComponentValue item)
    {
        if (item.Kind != CssComponentKind.Token || item.Token.Kind != CssTokenKind.Number)
        {
            return null;
        }

        var value = Value(item.Token);
        return (value, FormatNumber(value));
    }

    private static string? Percentage(CssComponentValue item)
    {
        if (item.Kind != CssComponentKind.Token || item.Token.Kind != CssTokenKind.Percentage)
        {
            return null;
        }

        var value = Value(item.Token);
        return value >= 0 ? FormatNumber(value) + "%" : null;
    }

    private static string? Angle(CssComponentValue item)
    {
        if (item.Kind != CssComponentKind.Token || item.Token.Kind != CssTokenKind.Dimension)
        {
            return null;
        }

        var unit = item.Token.Unit;
        return IsAscii(unit, "deg") || IsAscii(unit, "grad") || IsAscii(unit, "rad") || IsAscii(unit, "turn")
            ? FormatNumber(Value(item.Token)) + unit.ToLowerInvariant()
            : null;
    }

    private static bool IsFontPrefix(CssComponentValue item)
        => Keyword(item, "normal", "italic", "oblique", "small-caps", "bold", "bolder", "lighter",
               "ultra-condensed", "extra-condensed", "condensed", "semi-condensed", "semi-expanded", "expanded",
               "extra-expanded", "ultra-expanded") is not null
           || Number(item) is { Value: >= 1 and <= 1000 };

    private static bool IsFontSize(CssComponentValue item)
    {
        if (item.Kind == CssComponentKind.Function)
        {
            return IsMathFunction(item.FunctionName);
        }

        if (item.Kind != CssComponentKind.Token)
        {
            return false;
        }

        return item.Token.Kind switch
        {
            CssTokenKind.Dimension => Value(item.Token) >= 0 && IsLengthUnit(item.Token.Unit),
            CssTokenKind.Percentage => Value(item.Token) >= 0,
            CssTokenKind.Number => Value(item.Token) == 0,
            CssTokenKind.Ident => Keyword(item, "xx-small", "x-small", "small", "medium", "large", "x-large", "xx-large",
                "xxx-large", "larger", "smaller", "math") is not null,
            _ => false,
        };
    }

    private static bool IsLineHeight(CssComponentValue item)
    {
        if (item.Kind == CssComponentKind.Function)
        {
            return IsMathFunction(item.FunctionName);
        }

        return item.Kind == CssComponentKind.Token && item.Token.Kind switch
        {
            CssTokenKind.Number or CssTokenKind.Percentage => Value(item.Token) >= 0,
            CssTokenKind.Dimension => Value(item.Token) >= 0 && IsLengthUnit(item.Token.Unit),
            CssTokenKind.Ident => Keyword(item, "normal") is not null,
            _ => false,
        };
    }

    private static bool IsMathFunction(string name)
        => IsAscii(name, "calc") || IsAscii(name, "min") || IsAscii(name, "max") || IsAscii(name, "clamp");

    private static bool IsLengthUnit(string unit)
        => CssUnits.Recognize(unit, new CssValueWork(CancellationToken.None)).Category() == CssUnitCategory.Length;

    /// <summary>
    /// https://drafts.csswg.org/css-fonts-4/#family-name-syntax — a string, or a sequence of identifiers
    /// joined by one space. Generic family keywords are names like any other here: they match no face.
    /// </summary>
    private static bool TryFamilyName(List<CssComponentValue> group, out string family)
    {
        family = "";
        if (group.Count == 1 && group[0].Kind == CssComponentKind.Token && group[0].Token.Kind == CssTokenKind.String)
        {
            family = group[0].Token.Text;
            return true;
        }

        if (group.Count == 0)
        {
            return false;
        }

        var parts = new List<string>();
        foreach (var item in group)
        {
            if (item.Kind != CssComponentKind.Token || item.Token.Kind != CssTokenKind.Ident)
            {
                return false;
            }

            parts.Add(item.Token.Text);
        }

        if (parts.Count == 1 && (CssText.IsWide(parts[0].ToLowerInvariant()) || IsAscii(parts[0], "default")))
        {
            return false;
        }

        family = string.Join(' ', parts);
        return true;
    }

    private static bool TryUrl(CssComponentValue item, out string url)
    {
        url = "";
        if (item.Kind == CssComponentKind.Token && item.Token.Kind == CssTokenKind.Url)
        {
            url = item.Token.Text;
            return true;
        }

        if (item.Kind == CssComponentKind.Function && (IsAscii(item.FunctionName, "url") || IsAscii(item.FunctionName, "src")))
        {
            var arguments = Significant(item.Values);
            if (arguments.Count >= 1 && arguments[0].Kind == CssComponentKind.Token && arguments[0].Token.Kind == CssTokenKind.String)
            {
                url = arguments[0].Token.Text;
                return true;
            }
        }

        return false;
    }

    private static bool IsWideKeyword(List<CssComponentValue> items)
        => items.Count == 1 && items[0].Kind == CssComponentKind.Token && items[0].Token.Kind == CssTokenKind.Ident
           && CssText.IsWide(items[0].Token.Text.ToLowerInvariant());

    /// <summary>The component values of <paramref name="source"/>, whitespace dropped; any bad token fails.</summary>
    private static bool TryItems(string source, CssValueWork work, out List<CssComponentValue> items)
    {
        work.CheckCancellation();
        CssComponentValueList values;
        using (var parser = new CssSyntaxParser(source, options: null, work.Token, work.CheckCancellation))
        {
            values = parser.ParseComponentValues();
        }

        items = new List<CssComponentValue>(values.Count);
        foreach (var value in values)
        {
            work.Charge(1);
            if (value.Kind == CssComponentKind.Token)
            {
                switch (value.Token.Kind)
                {
                    case CssTokenKind.Whitespace:
                        continue;
                    case CssTokenKind.BadString or CssTokenKind.BadUrl or CssTokenKind.Semicolon
                        or CssTokenKind.CloseParenthesis or CssTokenKind.CloseSquareBracket or CssTokenKind.CloseCurlyBracket:
                        return false;
                }
            }

            items.Add(value);
        }

        return true;
    }

    private static List<CssComponentValue> Significant(CssComponentValueList values)
    {
        var list = new List<CssComponentValue>(values.Count);
        foreach (var value in values)
        {
            if (value.Kind != CssComponentKind.Token || value.Token.Kind != CssTokenKind.Whitespace)
            {
                list.Add(value);
            }
        }

        return list;
    }

    private static List<List<CssComponentValue>> SplitOnCommas(List<CssComponentValue> items)
    {
        var groups = new List<List<CssComponentValue>> { new() };
        foreach (var item in items)
        {
            if (item.Kind == CssComponentKind.Token && item.Token.Kind == CssTokenKind.Comma)
            {
                groups.Add([]);
            }
            else
            {
                groups[^1].Add(item);
            }
        }

        return groups;
    }

    private static string Serialize(List<CssComponentValue> items, string separator)
    {
        var parts = new List<string>();
        foreach (var item in items)
        {
            if (item.Kind != CssComponentKind.Token)
            {
                continue;
            }

            var token = item.Token;
            parts.Add(token.Kind switch
            {
                CssTokenKind.Ident => token.Text.ToLowerInvariant(),
                CssTokenKind.String => QuoteString(token.Text),
                CssTokenKind.Number => FormatNumber(Value(token)),
                CssTokenKind.Percentage => FormatNumber(Value(token)) + "%",
                CssTokenKind.Dimension => FormatNumber(Value(token)) + token.Unit.ToLowerInvariant(),
                _ => "",
            });
        }

        parts.RemoveAll(static part => part.Length == 0);
        return string.Join(separator, parts);
    }

    private static string QuoteString(string text)
        => "\"" + text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    private static string FormatNumber(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static double Value(CssToken token)
        => double.Parse(token.NumberText, NumberStyles.Float, CultureInfo.InvariantCulture);

    private static bool IsAscii(string value, string lowercase) => CssAscii.EqualsIgnoreCase(value, lowercase);
}
