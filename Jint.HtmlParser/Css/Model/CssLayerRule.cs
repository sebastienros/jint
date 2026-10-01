using Jint.HtmlParser.Css.Serialization;
using Jint.HtmlParser.Css.Values;

namespace Jint.HtmlParser.Css.Model;

// https://drafts.csswg.org/css-cascade-5/#layer-names
internal sealed class CssLayerName
{
    private CssLayerName(string[] segments, string text)
    {
        Segments = Array.AsReadOnly(segments);
        Text = text;
    }

    internal IReadOnlyList<string> Segments { get; }
    internal string Text { get; }

    internal static CssLayerName[]? Parse(CssComponentValueList values, CssValueWork work)
    {
        var names = new List<CssLayerName>();
        var index = 0;
        White();
        while (index < values.Count)
        {
            var segments = new List<string>();
            while (true)
            {
                work.Charge(1);
                if (index == values.Count) return null;
                var value = values[index++];
                if (value.Kind != CssComponentKind.Token || value.Token.Kind != CssTokenKind.Ident) return null;
                var text = value.Token.Text;
                work.Charge(text.Length);
                if (CssWideKeywords.Recognize(text) != CssWideKeyword.None) return null;
                segments.Add(text);
                if (index == values.Count || values[index].Kind != CssComponentKind.Token ||
                    values[index].Token.Kind != CssTokenKind.Delim || values[index].Token.Delimiter != '.') break;
                index++;
            }
            var serialized = new string[segments.Count];
            for (var i = 0; i < serialized.Length; i++)
                serialized[i] = CssSyntaxSerializer.SerializeIdentifier(segments[i], work);
            var name = string.Join(".", serialized);
            work.Charge(name.Length);
            names.Add(new CssLayerName(segments.ToArray(), name));
            White();
            if (index == values.Count) break;
            if (values[index].Kind != CssComponentKind.Token || values[index++].Token.Kind != CssTokenKind.Comma) return null;
            White();
            if (index == values.Count) return null;
        }
        work.CheckCancellation();
        return names.ToArray();

        void White()
        {
            while (index < values.Count && values[index].Kind == CssComponentKind.Token &&
                values[index].Token.Kind == CssTokenKind.Whitespace)
            {
                work.Charge(1);
                index++;
            }
        }
    }
}

internal sealed class CssLayerBlockRule(CssLayerName? layerName, CssSourceSpan span) : CssGroupingRule(span)
{
    internal override CssRuleType Type => CssRuleType.Layer;
    internal CssLayerName? LayerName { get; } = layerName;
    internal string Name => LayerName?.Text ?? "";
}

internal sealed class CssLayerStatementRule(CssLayerName[] names, CssSourceSpan span) : CssRule(span)
{
    internal override CssRuleType Type => CssRuleType.Layer;
    internal IReadOnlyList<CssLayerName> Names { get; } = Array.AsReadOnly(names);
}
