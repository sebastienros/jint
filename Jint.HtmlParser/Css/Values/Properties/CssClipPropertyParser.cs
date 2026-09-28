namespace Jint.HtmlParser.Css.Values.Properties;

// https://www.w3.org/TR/CSS22/visufx.html#clipping
internal static class CssClipPropertyParser
{
    internal static CssPropertyResult Parse(List<CssComponentValue> parts, int maximumDepth, CssValueWork work)
    {
        if (parts.Count != 1) return CssPropertyResult.Rejected(CssPropertyStatus.Invalid);
        var part = parts[0];
        if (CssPropertyParser.Keyword(part, CssKeywordSet.Auto, work) is { } keyword)
            return CssPropertyResult.Accepted(CssPropertyValue.Keyword(keyword, part.Span));
        if (part.Kind != CssComponentKind.Function ||
            CssPropertyRegistry.NormalizeName(part.FunctionName, work) != "rect")
            return CssPropertyResult.Rejected(CssPropertyStatus.Invalid);

        var children = CssPropertyParser.Significant(part.Values, work);
        if (children.Count is not (4 or 7)) return CssPropertyResult.Rejected(CssPropertyStatus.Invalid);
        var stride = children.Count == 7 ? 2 : 1;
        var edges = new CssPropertyValue[4];
        for (var i = 0; i < edges.Length; i++)
        {
            work.Charge(1);
            if (i != 0 && stride == 2 &&
                children[i * stride - 1] is not { Kind: CssComponentKind.Token, Token.Kind: CssTokenKind.Comma })
                return CssPropertyResult.Rejected(CssPropertyStatus.Invalid);
            var edge = children[i * stride];
            if (CssPropertyParser.Keyword(edge, CssKeywordSet.Auto, work) is { } auto)
                edges[i] = CssPropertyValue.Keyword(auto, edge.Span);
            else
            {
                var parsed = CssSizingPropertyParser.Numeric(edge, false, maximumDepth, work,
                    ancestorDepth: 1, nonnegative: false, allowPercentage: false);
                if (parsed.Status != CssPropertyStatus.Valid) return parsed;
                edges[i] = parsed.Value;
            }
        }
        return CssPropertyResult.Accepted(CssPropertyValue.ClipRectangle(edges[0], edges[1], edges[2], edges[3], part.Span, work));
    }
}
