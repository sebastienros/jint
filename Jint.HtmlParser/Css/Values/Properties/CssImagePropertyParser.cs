namespace Jint.HtmlParser.Css.Values.Properties;

// https://drafts.csswg.org/css-backgrounds-3/#the-background-image
internal static class CssImagePropertyParser
{
    internal static CssPropertyResult Parse(List<CssComponentValue> parts, CssValueWork work)
    {
        if (parts.Count == 0 || (parts.Count & 1) == 0) return Invalid();
        var layers = new List<CssPropertyValue>();
        for (var i = 0; i < parts.Count; i++)
        {
            work.Charge(1);
            var part = parts[i];
            if ((i & 1) != 0)
            {
                if (part.Kind != CssComponentKind.Token || part.Token.Kind != CssTokenKind.Comma) return Invalid();
                continue;
            }
            if (CssPropertyParser.Keyword(part, CssKeywordSet.None, work) is { } keyword)
                layers.Add(CssPropertyValue.Keyword(keyword, part.Span));
            else if (CssUrlValue.IsUrl(part))
            {
                var url = CssUrlValue.Parse(part, "V1:image-url-modifiers", work);
                if (url.Status != CssPropertyStatus.Valid) return url;
                layers.Add(url.Value);
            }
            else if (part.Kind == CssComponentKind.Function)
            {
                var name = CssPropertyRegistry.NormalizeName(part.FunctionName, work);
                return name is
                    "linear-gradient" or "repeating-linear-gradient" or "radial-gradient" or "repeating-radial-gradient" or
                    "conic-gradient" or "repeating-conic-gradient" or "image" or "image-set" or
                    "-webkit-image-set" or "cross-fade" or "element" or "paint"
                    ? CssPropertyResult.Rejected(CssPropertyStatus.UnimplementedGrammar, "V1:image:" + name)
                    : Invalid();
            }
            else return Invalid();
        }
        var span = new CssSourceSpan(parts[0].Span.Start, parts[^1].Span.Start + parts[^1].Span.Length - parts[0].Span.Start);
        return CssPropertyResult.Accepted(CssPropertyValue.ImageList(layers, span, work));
    }

    private static CssPropertyResult Invalid() => CssPropertyResult.Rejected(CssPropertyStatus.Invalid);
}
