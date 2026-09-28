namespace Jint.HtmlParser.Css.Values.Properties;

// CSS Text 4 §§7.1/7.3/7.4: declaration/computed values, with no text layout claim.
internal static class CssTextAlignPropertyParser
{

    internal static CssPropertyResult Parse(CssPropertyGrammar grammar, List<CssComponentValue> parts, CssValueWork work)
    {
        if (parts.Count != 1) return CssPropertyResult.Rejected(CssPropertyStatus.Invalid);
        var part = parts[0];
        if ((grammar is CssPropertyGrammar.TextAlign or CssPropertyGrammar.TextAlignAll) &&
            part.Kind == CssComponentKind.Token && part.Token.Kind == CssTokenKind.String)
            return CssPropertyResult.Rejected(CssPropertyStatus.UnimplementedGrammar, "text-align:alignment-string");
        var keywords = grammar == CssPropertyGrammar.TextAlign ? CssKeywordSet.StartEndLeftRightEtc
            : grammar == CssPropertyGrammar.TextAlignLast ? CssKeywordSet.StartEndLeftRightEtc2 : CssKeywordSet.StartEndLeftRightEtc3;
        var text = CssPropertyParser.Keyword(part, keywords, work);
        if (text is null) return CssPropertyResult.Rejected(CssPropertyStatus.Invalid);
        if (grammar != CssPropertyGrammar.TextAlign)
            return CssPropertyResult.Accepted(CssPropertyValue.Keyword(text, part.Span));
        var all = text == "justify-all" ? "justify" : text;
        var last = text switch { "justify-all" => "justify", "match-parent" => "match-parent", _ => "auto" };
        return CssPropertyResult.Accepted(CssPropertyValue.Shorthand(text, part.Span,
            CssPropertyValue.Keyword(all, part.Span), CssPropertyValue.Keyword(last, part.Span)));
    }

    internal static string Serialize(string all, string last, CssValueWork work)
    {
        work.CheckCancellation();
        var text = (all, last) switch
        {
            ("match-parent", "auto") => "",
            (_, "auto") => all,
            ("justify", "justify") => "justify-all",
            ("match-parent", "match-parent") => "match-parent",
            _ => ""
        };
        work.Charge(text.Length);
        work.CheckCancellation();
        return text;
    }
}
