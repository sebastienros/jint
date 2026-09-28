using Jint.HtmlParser.Css.Serialization;

namespace Jint.HtmlParser.Css.Values.Properties;

// https://drafts.csswg.org/css-conditional-5/#container-shorthand
// https://drafts.csswg.org/css-writing-modes-4/#propdef-writing-mode
internal static class CssContainerPropertyParser
{
    internal static CssPropertyResult Parse(CssPropertyGrammar grammar, List<CssComponentValue> parts, CssValueWork work)
    {
        if (parts.Count == 0) return Invalid();
        switch (grammar)
        {
            case CssPropertyGrammar.ContainerName:
                return Names(parts, work);
            case CssPropertyGrammar.Container:
                return Shorthand(parts, work);
            case CssPropertyGrammar.WritingMode:
                {
                    if (parts.Count != 1) return Invalid();
                    var mode = CssPropertyParser.Keyword(parts[0], CssKeywordSet.HorizontalTbVerticalRlVerticalLrSidewaysRlEtc, work);
                    return mode is null ? Invalid() : CssPropertyResult.Accepted(CssPropertyValue.Keyword(mode, parts[0].Span));
                }
        }
        // scroll-state is retained as grammar; querying it is a separate C6 dependency.
        if (parts.Count > 2) return Invalid();
        string? size = null;
        var scroll = false;
        foreach (var part in parts)
        {
            var keyword = CssPropertyParser.Keyword(part, CssKeywordSet.NormalSizeInlineSizeScrollState, work);
            if (keyword is null || keyword == "normal" && parts.Count != 1) return Invalid();
            if (keyword == "scroll-state") { if (scroll) return Invalid(); scroll = true; }
            else { if (size is not null) return Invalid(); size = keyword; }
        }
        return CssPropertyResult.Accepted(CssPropertyValue.Keyword(
            size is null ? "scroll-state" : size + (scroll ? " scroll-state" : ""), parts[0].Span));
    }

    internal static bool IsName(CssComponentValue part, CssValueWork work)
    {
        if (part.Kind != CssComponentKind.Token || part.Token.Kind != CssTokenKind.Ident) return false;
        return CssPropertyParser.Keyword(part, CssKeywordSet.NoneAndOrNotEtc, work) is null;
    }

    private static CssPropertyResult Names(List<CssComponentValue> parts, CssValueWork work)
    {
        if (parts.Count == 0) return Invalid();
        if (parts.Count == 1 && CssPropertyParser.Keyword(parts[0], CssKeywordSet.None, work) is not null)
            return CssPropertyResult.Accepted(CssPropertyValue.Keyword("none", parts[0].Span));
        var names = new string[parts.Count];
        var spellings = new string[parts.Count];
        for (var i = 0; i < parts.Count; i++)
        {
            work.Charge(1);
            if (!IsName(parts[i], work)) return Invalid();
            names[i] = parts[i].Token.Text;
            spellings[i] = CssSyntaxSerializer.SerializeIdentifier(names[i], work);
        }
        return CssPropertyResult.Accepted(CssPropertyValue.IdentifierList(string.Join(" ", spellings), parts[0].Span, names));
    }

    private static CssPropertyResult Shorthand(List<CssComponentValue> parts, CssValueWork work)
    {
        var slash = -1;
        for (var i = 0; i < parts.Count; i++)
        {
            work.Charge(1);
            if (parts[i].Kind != CssComponentKind.Token || parts[i].Token.Kind != CssTokenKind.Delim || parts[i].Token.Delimiter != '/') continue;
            if (slash >= 0) return Invalid();
            slash = i;
        }
        var names = Names(slash < 0 ? parts : parts.GetRange(0, slash), work);
        if (names.Status != CssPropertyStatus.Valid) return names;
        var type = slash < 0 ? CssPropertyResult.Accepted(CssPropertyValue.Keyword("normal", default))
            : Parse(CssPropertyGrammar.ContainerType, parts.GetRange(slash + 1, parts.Count - slash - 1), work);
        if (type.Status != CssPropertyStatus.Valid) return type;
        return CssPropertyResult.Accepted(CssPropertyValue.Shorthand(names.Value.Text +
            (type.Value.Text == "normal" ? "" : " / " + type.Value.Text), parts[0].Span, names.Value, type.Value));
    }

    private static CssPropertyResult Invalid() => CssPropertyResult.Rejected(CssPropertyStatus.Invalid);
}
