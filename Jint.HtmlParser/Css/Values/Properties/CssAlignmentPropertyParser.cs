namespace Jint.HtmlParser.Css.Values.Properties;

// https://drafts.csswg.org/css-align-3/#content-distribution and §§6/7. No layout evaluation.
internal static class CssAlignmentPropertyParser
{
    internal static CssPropertyResult Parse(CssPropertyGrammar grammar, List<CssComponentValue> parts, CssValueWork work)
    {
        if (grammar is not (CssPropertyGrammar.PlaceItems or CssPropertyGrammar.PlaceSelf or CssPropertyGrammar.PlaceContent))
            return Match(grammar, parts, 0, parts.Count, work);
        if (parts.Count is < 1 or > 4) return Invalid();
        var self = grammar == CssPropertyGrammar.PlaceSelf;
        var content = grammar == CssPropertyGrammar.PlaceContent;
        var firstGrammar = content ? CssPropertyGrammar.AlignContent : self ? CssPropertyGrammar.AlignSelf : CssPropertyGrammar.AlignItems;
        var secondGrammar = content ? CssPropertyGrammar.JustifyContent : self ? CssPropertyGrammar.JustifySelf : CssPropertyGrammar.JustifyItems;
        // A longhand can consume two words (safe center, last baseline, legacy left).
        // Trying boundaries, rather than splitting on words, also validates omitted copying.
        for (var boundary = System.Math.Min(2, parts.Count); boundary > 0; boundary--)
        {
            var first = Match(firstGrammar, parts, 0, boundary, work);
            if (first.Status == CssPropertyStatus.Invalid) continue;
            var second = boundary == parts.Count
                ? content && first.Status == CssPropertyStatus.Valid && IsBaseline(first.Value.Text)
                    ? CssPropertyResult.Accepted(CssPropertyValue.Keyword("start", parts[0].Span))
                    : Match(secondGrammar, parts, 0, boundary, work)
                : Match(secondGrammar, parts, boundary, parts.Count - boundary, work);
            if (second.Status == CssPropertyStatus.Invalid) continue;
            if (first.Status == CssPropertyStatus.UnimplementedGrammar) return first;
            if (second.Status == CssPropertyStatus.UnimplementedGrammar) return second;
            var text = content ? SerializeContent(first.Value.Text, second.Value.Text, work) :
                first.Value.Text == second.Value.Text ? first.Value.Text : first.Value.Text + " " + second.Value.Text;
            return CssPropertyResult.Accepted(CssPropertyValue.Shorthand(text, parts[0].Span, first.Value, second.Value));
        }
        return Invalid();
    }

    private static CssPropertyResult Match(CssPropertyGrammar grammar, List<CssComponentValue> parts,
        int start, int count, CssValueWork work)
    {
        if (count is < 1 or > 2) return Invalid();
        var self = grammar is CssPropertyGrammar.AlignSelf or CssPropertyGrammar.JustifySelf;
        var content = grammar is CssPropertyGrammar.AlignContent or CssPropertyGrammar.JustifyContent;
        var justify = grammar is CssPropertyGrammar.JustifyItems or CssPropertyGrammar.JustifySelf or CssPropertyGrammar.JustifyContent;
        // Anchor Positioning 1 §4.2 adds this singleton only to self-alignment.
        if (self && count == 1 && CssPropertyParser.Keyword(parts[start], "anchor-center", work) is not null)
            return CssPropertyResult.Rejected(CssPropertyStatus.UnimplementedGrammar, "alignment:anchor-center");
        var first = CssPropertyParser.Keyword(parts[start],
            "auto normal stretch baseline first last safe unsafe center start end self-start self-end flex-start flex-end left right legacy space-between space-around space-evenly", work);
        if (first is null) return Invalid();
        if (content && first is "auto" or "self-start" or "self-end" or "legacy" ||
            !content && first is "space-between" or "space-around" or "space-evenly" ||
            grammar == CssPropertyGrammar.JustifyContent && first is "baseline" or "first" or "last")
            return Invalid();
        string? text = null;
        if (count == 1)
        {
            if (first == "auto" && !self || first == "legacy" && grammar != CssPropertyGrammar.JustifyItems ||
                first is "first" or "last" or "safe" or "unsafe" || first is "left" or "right" && !justify) return Invalid();
            text = first;
        }
        else
        {
            var second = CssPropertyParser.Keyword(parts[start + 1],
                "baseline normal center start end self-start self-end flex-start flex-end left right legacy", work);
            if (content && second is "self-start" or "self-end" or "legacy") return Invalid();
            if (first is "first" or "last" && second == "baseline")
                text = first == "first" ? "baseline" : "last baseline";
            else if (first is "safe" or "unsafe" && second is not null &&
                (second is "center" or "start" or "end" or "self-start" or "self-end" or "flex-start" or "flex-end" ||
                    justify && second is "left" or "right" || self && second == "normal")) text = first + " " + second;
            else if (grammar == CssPropertyGrammar.JustifyItems &&
                (first == "legacy" && second is "left" or "right" or "center" ||
                    second == "legacy" && first is "left" or "right" or "center"))
                text = "legacy " + (first == "legacy" ? second : first);
        }
        return text is null ? Invalid() : CssPropertyResult.Accepted(CssPropertyValue.Keyword(text, parts[start].Span));
    }

    internal static string SerializeContent(string align, string justify, CssValueWork work)
    {
        var text = align == justify || IsBaseline(align) && justify == "start" ? align : align + " " + justify;
        work.Charge(text.Length);
        work.CheckCancellation();
        return text;
    }

    private static bool IsBaseline(string value) => value is "baseline" or "last baseline";

    private static CssPropertyResult Invalid() => CssPropertyResult.Rejected(CssPropertyStatus.Invalid);
}
