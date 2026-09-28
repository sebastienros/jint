using Jint.HtmlParser.Css.Values.Colors;
using Jint.HtmlParser.Css.Syntax;

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

        string url;
        var usesSrc = false;
        switch (first.Kind)
        {
            case CssComponentKind.Token when first.Token.Kind == CssTokenKind.Url:
                url = first.Token.Text;
                break;
            case CssComponentKind.Function when CssAscii.EqualsIgnoreCase(first.FunctionName, "url") ||
                CssAscii.EqualsIgnoreCase(first.FunctionName, "src"):
                usesSrc = CssAscii.EqualsIgnoreCase(first.FunctionName, "src");
                var arguments = CssPropertyParser.Significant(first.Values, work);
                if (arguments.Count == 0 || arguments[0].Kind != CssComponentKind.Token ||
                    arguments[0].Token.Kind != CssTokenKind.String) return Invalid();
                if (arguments.Count > 1)
                {
                    for (var i = 1; i < arguments.Count; i++)
                    {
                        work.Charge(1);
                        var modifier = arguments[i];
                        if (modifier.Kind != CssComponentKind.Function &&
                            (modifier.Kind != CssComponentKind.Token || modifier.Token.Kind != CssTokenKind.Ident)) return Invalid();
                    }
                    return CssPropertyResult.Rejected(CssPropertyStatus.UnimplementedGrammar, "V7:paint-url-modifiers");
                }
                url = arguments[0].Token.Text;
                break;
            default:
                return Color(parts, maximumDepth, work);
        }

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
        return CssPropertyResult.Accepted(CssPropertyValue.PaintServer(url, fallback, first.Span, work, usesSrc));
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
