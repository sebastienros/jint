using Jint.HtmlParser.Css.Values.Colors;
using Jint.HtmlParser.Css.Values.Math;

namespace Jint.HtmlParser.Css.Values.Properties;

// Text Decoration 4 §§2.1–2.6; Borders 4 <line-width>. Components are classified once.
internal static class CssTextDecorationPropertyParser
{
    private const string Lines = "none underline overline line-through blink spelling-error grammar-error";
    private const string Styles = "solid double dotted dashed wavy";
    private const string ThicknessKeywords = "auto from-font hairline thin medium thick";

    internal static CssPropertyResult Parse(CssPropertyGrammar grammar, List<CssComponentValue> parts,
        int maximumDepth, CssValueWork work)
    {
        if (parts.Count == 0 || parts.Count > (grammar == CssPropertyGrammar.TextDecoration ? 7 :
                grammar == CssPropertyGrammar.TextDecorationLine ? 4 : 1)) return Invalid();
        if (grammar == CssPropertyGrammar.TextDecorationThickness) return Thickness(parts[0], maximumDepth, work);
        if (grammar == CssPropertyGrammar.TextDecorationStyle)
            return CssPropertyParser.Keyword(parts[0], Styles, work) is { } style
                ? CssPropertyResult.Accepted(CssPropertyValue.Keyword(style, parts[0].Span)) : Invalid();
        var index = 0;
        if (grammar == CssPropertyGrammar.TextDecorationLine)
        {
            var parsed = Line(parts, ref index, work);
            return index == parts.Count ? parsed : Invalid();
        }
        CssPropertyValue? line = null, thickness = null, decorationStyle = null, color = null;
        while (index < parts.Count)
        {
            work.Charge(1);
            var part = parts[index];
            if (CssPropertyParser.Keyword(part, Lines, work) is not null)
            {
                if (line is not null) return Invalid(); // A nested line group cannot resume after another group.
                var parsed = Line(parts, ref index, work);
                if (parsed.Status != CssPropertyStatus.Valid) return parsed;
                line = parsed.Value;
                continue;
            }
            index++;
            if (CssPropertyParser.Keyword(part, Styles, work) is { } style)
            {
                if (decorationStyle is not null) return Invalid();
                decorationStyle = CssPropertyValue.Keyword(style, part.Span);
            }
            else if (CssPropertyParser.Keyword(part, ThicknessKeywords, work) is not null ||
                (part.Kind == CssComponentKind.Token && part.Token.Kind is CssTokenKind.Number or CssTokenKind.Dimension or CssTokenKind.Percentage) ||
                (part.Kind == CssComponentKind.Function && CssMathParser.Recognize(part.FunctionName) != CssMathFunction.None))
            {
                if (thickness is not null) return Invalid();
                var parsed = Thickness(part, maximumDepth, work);
                if (parsed.Status != CssPropertyStatus.Valid) return parsed;
                thickness = parsed.Value;
            }
            else
            {
                if (color is not null) return Invalid();
                var parsed = CssColorParser.Parse(new CssComponentValueList([part]), maximumDepth, work);
                if (parsed.Status == CssColorParseStatus.RequiresLaterGrammar)
                    return CssPropertyResult.Rejected(CssPropertyStatus.UnimplementedGrammar, parsed.Blocker);
                if (parsed.Status != CssColorParseStatus.Match) return Invalid();
                color = CssPropertyValue.ColorValue(parsed.Value, CssColorSerializer.SerializeSpecified(parsed.Value, work));
            }
        }
        var span = parts[0].Span;
        line ??= CssPropertyValue.Keyword("none", span);
        thickness ??= CssPropertyValue.Keyword("auto", span);
        decorationStyle ??= CssPropertyValue.Keyword("solid", span);
        color ??= CssPropertyValue.ColorValue(CssColorValue.Identity(CssColorKind.CurrentColor, "currentcolor", span), "currentcolor");
        return CssPropertyResult.Accepted(CssPropertyValue.Shorthand(
            Serialize(line.Serialize(), thickness.Serialize(), decorationStyle.Serialize(), color.Serialize(), work),
            span, line, thickness, decorationStyle, color));
    }

    private static CssPropertyResult Thickness(CssComponentValue part, int maximumDepth, CssValueWork work) =>
        CssPropertyParser.Keyword(part, ThicknessKeywords, work) is { } keyword
            ? CssPropertyResult.Accepted(CssPropertyValue.Keyword(keyword, part.Span))
            : CssSizingPropertyParser.Numeric(part, false, maximumDepth, work, nonnegative: false);

    private static CssPropertyResult Line(List<CssComponentValue> parts, ref int index, CssValueWork work)
    {
        var flags = 0;
        var span = parts[index].Span;
        while (index < parts.Count)
        {
            work.Charge(1);
            var bit = CssPropertyParser.Keyword(parts[index], Lines, work) switch
            {
                "underline" => 1, "overline" => 2, "line-through" => 4, "blink" => 8,
                "none" => 16, "spelling-error" => 32, "grammar-error" => 64, _ => 0
            };
            if (bit == 0) break;
            if ((flags & bit) != 0 || flags != 0 && (flags > 15 || bit > 15)) return Invalid();
            flags |= bit;
            index++;
        }
        var text = flags switch
        {
            0 => null, 16 => "none", 32 => "spelling-error", 64 => "grammar-error", _ => LineText(flags)
        };
        return text is null ? Invalid() : CssPropertyResult.Accepted(CssPropertyValue.Keyword(text, span));
    }

    private static string LineText(int flags)
    {
        var values = new List<string>(4);
        if ((flags & 1) != 0) values.Add("underline");
        if ((flags & 2) != 0) values.Add("overline");
        if ((flags & 4) != 0) values.Add("line-through");
        if ((flags & 8) != 0) values.Add("blink");
        return string.Join(" ", values);
    }

    internal static string Serialize(string line, string thickness, string style, string color, CssValueWork work)
    {
        work.CheckCancellation();
        var text = string.Concat(line, " ", thickness, " ", style, " ", color);
        work.Charge(text.Length);
        work.CheckCancellation();
        return text;
    }

    private static CssPropertyResult Invalid() => CssPropertyResult.Rejected(CssPropertyStatus.Invalid);
}
