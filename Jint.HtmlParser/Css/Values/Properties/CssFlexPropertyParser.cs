namespace Jint.HtmlParser.Css.Values.Properties;

// Flexbox 1 §§5.1–5.3/7.1; Writing Modes 3 §2.1, drafts checked 2026-09-25.
internal static class CssFlexPropertyParser
{
    internal static CssPropertyResult Parse(CssPropertyGrammar grammar, List<CssComponentValue> parts,
        int maximumDepth, CssValueWork work)
    {
        switch (grammar)
        {
            case CssPropertyGrammar.Flex:
                return Flex(parts, maximumDepth, work);
            case CssPropertyGrammar.FlexFlow:
                return Flow(parts, work);
        }
        if (parts.Count != 1) return Invalid();
        if (grammar == CssPropertyGrammar.FlexFactor)
            return CssSizingPropertyParser.Numeric(parts[0], true, maximumDepth, work);
        var choices = grammar switch
        {
            CssPropertyGrammar.FlexDirection => CssKeywordSet.RowRowReverseColumnColumnReverse,
            CssPropertyGrammar.FlexWrap => CssKeywordSet.NowrapWrapWrapReverse,
            _ => CssKeywordSet.LtrRtl
        };
        var keyword = CssPropertyParser.Keyword(parts[0], choices, work);
        return keyword is null ? Invalid() : CssPropertyResult.Accepted(CssPropertyValue.Keyword(keyword, parts[0].Span));
    }

    private static CssPropertyResult Flow(List<CssComponentValue> parts, CssValueWork work)
    {
        if (parts.Count is < 1 or > 2) return Invalid();
        CssPropertyValue? direction = null, wrap = null;
        foreach (var part in parts)
        {
            if (direction is null && CssPropertyParser.Keyword(part, CssKeywordSet.RowRowReverseColumnColumnReverse, work) is { } d)
                direction = CssPropertyValue.Keyword(d, part.Span);
            else if (wrap is null && CssPropertyParser.Keyword(part, CssKeywordSet.NowrapWrapWrapReverse, work) is { } w)
                wrap = CssPropertyValue.Keyword(w, part.Span);
            else return Invalid();
        }
        direction ??= CssPropertyValue.Keyword("row", default);
        wrap ??= CssPropertyValue.Keyword("nowrap", default);
        return CssPropertyResult.Accepted(CssPropertyValue.Shorthand(direction.Text + " " + wrap.Text,
            parts[0].Span, direction, wrap));
    }

    private static CssPropertyResult Flex(List<CssComponentValue> parts, int maximumDepth, CssValueWork work)
    {
        if (parts.Count is < 1 or > 3) return Invalid();
        var span = parts[0].Span;
        if (parts.Count == 1 && CssPropertyParser.Keyword(parts[0], CssKeywordSet.NoneAuto, work) is { } special)
        {
            var grow = DefaultNumber(special == "none" ? "0" : "1", false, work);
            var shrink = DefaultNumber(special == "none" ? "0" : "1", false, work);
            var basis = CssPropertyValue.Keyword("auto", span);
            return CssPropertyResult.Accepted(CssPropertyValue.Shorthand(
                grow.Text + " " + shrink.Text + " auto", span, grow, shrink, basis));
        }
        // The factors form one ordered group: basis can precede or follow that group.
        // Try the longest factor group first so a unitless zero is a factor unless two
        // factors already precede it (Flexbox §7.1).
        CssPropertyResult? pending = null;
        for (var factorCount = System.Math.Min(2, parts.Count); factorCount >= 0; factorCount--)
        {
            if (parts.Count - factorCount > 1) continue;
            for (var basisFirst = 0; basisFirst <= (parts.Count > factorCount && factorCount > 0 ? 1 : 0); basisFirst++)
            {
                work.Charge(1);
                var offset = basisFirst;
                CssPropertyValue grow = DefaultNumber("1", false, work), shrink = DefaultNumber("1", false, work);
                var failed = false;
                for (var i = 0; i < factorCount; i++)
                {
                    var factor = CssSizingPropertyParser.Numeric(parts[offset + i], true, maximumDepth, work);
                    switch (factor.Status)
                    {
                        case CssPropertyStatus.UnimplementedGrammar:
                            pending = factor;
                            failed = true;
                            break;
                        case CssPropertyStatus.Valid:
                            if (i == 0) grow = factor.Value;
                            else shrink = factor.Value;
                            continue;
                        default:
                            failed = true;
                            break;
                    }
                    break;
                }
                if (failed) continue;
                // Omitted basis is zero LENGTH, not a percentage: indefinite bases differ.
                CssPropertyValue basis = DefaultNumber("0", true, work);
                if (parts.Count > factorCount)
                {
                    if (basisFirst == 1 && parts[0].Kind == CssComponentKind.Token &&
                        parts[0].Token.Kind == CssTokenKind.Number) continue;
                    var value = CssSizingPropertyParser.Parse(CssPropertyGrammar.FlexBasis,
                        [parts[basisFirst == 1 ? 0 : factorCount]], maximumDepth, work);
                    switch (value.Status)
                    {
                        case CssPropertyStatus.UnimplementedGrammar:
                            pending = value;
                            continue;
                        case CssPropertyStatus.Valid:
                            basis = value.Value;
                            break;
                        default:
                            continue;
                    }
                }
                return CssPropertyResult.Accepted(CssPropertyValue.Shorthand(
                    grow.Serialize() + " " + shrink.Serialize() + " " + basis.Serialize(), span, grow, shrink, basis));
            }
        }
        return pending ?? Invalid();
    }

    private static CssPropertyValue DefaultNumber(string text, bool length, CssValueWork work) =>
        CssPropertyValue.Number(new CssNumericAtom(length ? CssNumericKind.Dimension : CssNumericKind.Number,
            CssNumber.FromValidatedToken(text, work), length ? CssUnit.Px : CssUnit.None, true, default),
            length ? text + "px" : text);

    private static CssPropertyResult Invalid() => CssPropertyResult.Rejected(CssPropertyStatus.Invalid);
}
