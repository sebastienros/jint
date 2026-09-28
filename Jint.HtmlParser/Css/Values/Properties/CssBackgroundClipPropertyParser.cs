using System.Text;

namespace Jint.HtmlParser.Css.Values.Properties;

// https://drafts.csswg.org/css-backgrounds-4/#the-background-clip
// <visual-box> | [ border-area || text ], repeated as a comma-separated list.
internal static class CssBackgroundClipPropertyParser
{
    internal static CssPropertyResult Parse(List<CssComponentValue> parts, CssValueWork work)
    {
        if (parts.Count == 0) return Invalid();
        var layers = new List<CssPropertyValue>();
        var text = new StringBuilder();
        var index = 0;
        while (index < parts.Count)
        {
            work.Charge(1);
            var first = parts[index++];
            work.Charge("border-box padding-box content-box border-area text".Length);
            var layer = CssPropertyParser.Keyword(first, CssKeywordSet.BorderBoxPaddingBoxContentBoxBorderAreaEtc, work);
            if (layer is null) return Invalid();
            var layerSpan = first.Span;
            if ((CssBorderAreaTextNames.Match(layer)) && index < parts.Count && !Comma(parts[index]))
            {
                work.Charge(1);
                var component = parts[index++];
                work.Charge("border-area text".Length);
                var second = CssPropertyParser.Keyword(component, CssKeywordSet.BorderAreaText, work);
                work.Charge(layer.Length);
                if (second is null || second == layer) return Invalid();
                layer = "border-area text";
                layerSpan = new CssSourceSpan(first.Span.Start, component.Span.Start + component.Span.Length - first.Span.Start);
            }
            if (layers.Count != 0) { work.Charge(2); text.Append(", "); }
            work.Charge(layer.Length);
            text.Append(layer);
            layers.Add(CssPropertyValue.Keyword(layer, layerSpan));
            if (index == parts.Count) break;
            work.Charge(1);
            if (!Comma(parts[index++]) || index == parts.Count) return Invalid();
        }
        var spelling = text.ToString();
        work.Charge(spelling.Length);
        var firstSpan = parts[0].Span;
        var lastSpan = parts[^1].Span;
        var span = new CssSourceSpan(firstSpan.Start, lastSpan.Start + lastSpan.Length - firstSpan.Start);
        var value = CssPropertyValue.KeywordList(spelling, span, layers, work);
        work.CheckCancellation();
        return CssPropertyResult.Accepted(value);
    }

    private static bool Comma(CssComponentValue value) =>
        value.Kind == CssComponentKind.Token && value.Token.Kind == CssTokenKind.Comma;

    private static CssPropertyResult Invalid() => CssPropertyResult.Rejected(CssPropertyStatus.Invalid);
}
