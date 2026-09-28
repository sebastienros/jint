using Jint.HtmlParser.Css.Values.Colors;

namespace Jint.HtmlParser.Css.Values.Properties;

// https://svgwg.org/svg2-draft/painting.html#SpecifyingPaint
internal static class CssPaintPropertyParser
{
    internal static CssPropertyResult Parse(List<CssComponentValue> parts, int maximumDepth, CssValueWork work)
    {
        if (parts.Count == 0) return Invalid();
        var first = parts[0];
        if (parts.Count == 1 && first.Kind == CssComponentKind.Token && first.Token.Kind == CssTokenKind.Ident)
        {
            var keyword = CssPropertyRegistry.NormalizeName(first.Token.Text, work);
            if (keyword is "none" or "context-fill" or "context-stroke")
                return CssPropertyResult.Accepted(CssPropertyValue.Keyword(keyword, first.Span));
        }

        if (!CssUrlValue.IsUrl(first)) return Color(parts, maximumDepth, work);
        var reference = CssUrlValue.Parse(first, "V7:paint-url-modifiers", work);
        if (reference.Status != CssPropertyStatus.Valid) return reference;
        var url = reference.Value.Url;

        CssPropertyValue? fallback = null;
        if (parts.Count > 1)
        {
            var second = parts[1];
            if (parts.Count == 2 && CssPropertyParser.Keyword(second, CssKeywordSet.None, work) is not null)
                fallback = CssPropertyValue.Keyword("none", second.Span);
            else
            {
                var result = Color(parts.GetRange(1, parts.Count - 1), maximumDepth, work);
                if (result.Status != CssPropertyStatus.Valid) return result;
                fallback = result.Value;
            }
        }
        return CssPropertyResult.Accepted(CssPropertyValue.PaintServer(url.Url, fallback, first.Span, work, url.UsesSrc));
    }

    private static CssPropertyResult Color(List<CssComponentValue> parts, int maximumDepth, CssValueWork work)
    {
        work.Charge(parts.Count);
        var color = CssColorParser.Parse(new CssComponentValueList(parts.ToArray()), maximumDepth, work);
        return color.Status switch
        {
            CssColorParseStatus.Match => CssPropertyResult.Accepted(CssPropertyValue.ColorValue(color.Value,
                CssColorSerializer.SerializeSpecified(color.Value, work))),
            CssColorParseStatus.RequiresLaterGrammar => CssPropertyResult.Rejected(CssPropertyStatus.UnimplementedGrammar, color.Blocker),
            _ => Invalid()
        };
    }

    private static CssPropertyResult Invalid() => CssPropertyResult.Rejected(CssPropertyStatus.Invalid);
}
