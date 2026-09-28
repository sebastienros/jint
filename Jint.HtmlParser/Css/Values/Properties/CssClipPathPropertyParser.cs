namespace Jint.HtmlParser.Css.Values.Properties;

// https://drafts.fxtf.org/css-masking-1/#the-clip-path
internal static class CssClipPathPropertyParser
{
    internal static CssPropertyResult Parse(List<CssComponentValue> parts, CssValueWork work)
    {
        if (parts.Count == 1)
        {
            var part = parts[0];
            if (CssUrlValue.IsUrl(part)) return CssUrlValue.Parse(part, "V7:clip-path-url-modifiers", work);
            if (part.Kind == CssComponentKind.Token && part.Token.Kind == CssTokenKind.Ident)
            {
                var keyword = CssPropertyRegistry.NormalizeName(part.Token.Text, work);
                if (keyword is "none" or "margin-box" or "border-box" or "padding-box" or "content-box" or
                    "fill-box" or "stroke-box" or "view-box")
                    return CssPropertyResult.Accepted(CssPropertyValue.Keyword(keyword, part.Span));
            }
        }
        // Shapes are a separate typed grammar, not opaque strings admitted as valid clip paths.
        foreach (var part in parts)
        {
            work.Charge(1);
            if (part.Kind != CssComponentKind.Function) continue;
            var name = CssPropertyRegistry.NormalizeName(part.FunctionName, work);
            if (name is "inset" or "rect" or "xywh" or "circle" or "ellipse" or "polygon" or "path" or "shape")
                return CssPropertyResult.Rejected(CssPropertyStatus.UnimplementedGrammar, "V7:clip-path:" + name);
        }
        return CssPropertyResult.Rejected(CssPropertyStatus.Invalid);
    }
}
