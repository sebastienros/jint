namespace Jint.HtmlParser.Css.Values.Properties;

// Box Alignment 3 §§4.1/4.2/6/7, draft checked 2026-09-25. No layout evaluation.
internal static class CssAlignmentPropertyParser
{
    internal static CssPropertyResult Parse(CssPropertyGrammar grammar, List<CssComponentValue> parts, CssValueWork work)
    {
        if (grammar is not (CssPropertyGrammar.PlaceItems or CssPropertyGrammar.PlaceSelf))
            return Match(grammar, parts, 0, parts.Count, work);
        if (parts.Count is < 1 or > 4) return Invalid();
        var self = grammar == CssPropertyGrammar.PlaceSelf;
        var firstGrammar = self ? CssPropertyGrammar.AlignSelf : CssPropertyGrammar.AlignItems;
        var secondGrammar = self ? CssPropertyGrammar.JustifySelf : CssPropertyGrammar.JustifyItems;
        // A longhand can consume two words (safe center, last baseline, legacy left).
        // Trying boundaries, rather than splitting on words, also validates omitted copying.
        for (var boundary = System.Math.Min(2, parts.Count); boundary > 0; boundary--)
        {
            var first = Match(firstGrammar, parts, 0, boundary, work);
            if (first.Status == CssPropertyStatus.Invalid) continue;
            var second = boundary == parts.Count
                ? Match(secondGrammar, parts, 0, boundary, work)
                : Match(secondGrammar, parts, boundary, parts.Count - boundary, work);
            if (second.Status == CssPropertyStatus.Invalid) continue;
            if (first.Status == CssPropertyStatus.UnimplementedGrammar) return first;
            if (second.Status == CssPropertyStatus.UnimplementedGrammar) return second;
            var text = first.Value.Text == second.Value.Text ? first.Value.Text : first.Value.Text + " " + second.Value.Text;
            return CssPropertyResult.Accepted(CssPropertyValue.Shorthand(text, parts[0].Span, first.Value, second.Value));
        }
        return Invalid();
    }

    private static CssPropertyResult Match(CssPropertyGrammar grammar, List<CssComponentValue> parts,
        int start, int count, CssValueWork work)
    {
        if (count is < 1 or > 2) return Invalid();
        var self = grammar is CssPropertyGrammar.AlignSelf or CssPropertyGrammar.JustifySelf;
        var justify = grammar is CssPropertyGrammar.JustifyItems or CssPropertyGrammar.JustifySelf;
        // Anchor Positioning 1 §4.2 adds this singleton only to self-alignment.
        if (self && count == 1 && CssPropertyParser.Keyword(parts[start], "anchor-center", work) is not null)
            return CssPropertyResult.Rejected(CssPropertyStatus.UnimplementedGrammar, "alignment:anchor-center");
        var first = CssPropertyParser.Keyword(parts[start],
            "auto normal stretch baseline first last safe unsafe center start end self-start self-end flex-start flex-end left right legacy", work);
        if (first is null) return Invalid();
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

    private static CssPropertyResult Invalid() => CssPropertyResult.Rejected(CssPropertyStatus.Invalid);
}
