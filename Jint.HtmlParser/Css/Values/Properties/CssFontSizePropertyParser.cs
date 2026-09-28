namespace Jint.HtmlParser.Css.Values.Properties;

// CSS Fonts 4 §2.5: font-size values, independently of font shorthand and MathML scaling.
internal static class CssFontSizePropertyParser
{
    internal static CssPropertyResult Parse(List<CssComponentValue> parts, int maximumDepth, CssValueWork work)
    {
        if (parts.Count != 1) return CssPropertyResult.Rejected(CssPropertyStatus.Invalid);
        var part = parts[0];
        if (CssPropertyParser.Keyword(part, CssKeywordSet.XxSmallXSmallSmallMediumEtc, work) is { } keyword)
            return CssPropertyResult.Accepted(CssPropertyValue.Keyword(keyword, part.Span));
        if (CssPropertyParser.Keyword(part, CssKeywordSet.Math, work) is not null)
            return CssPropertyResult.Rejected(CssPropertyStatus.UnimplementedGrammar, "font-size:mathml-scaling");
        return CssSizingPropertyParser.Numeric(part, false, maximumDepth, work);
    }
}
