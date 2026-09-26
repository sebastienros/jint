using System.Text;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values;

namespace Jint.HtmlParser.Css.Serialization;

internal static class CssRuleSerializer
{
    internal static string Serialize(CssRule rule)
    {
        var builder = new StringBuilder();
        Append(builder, rule, new CssValueWork(default));
        return builder.ToString();
    }

    internal static CssSerializationSnapshot SerializeSheet(CssStyleSheet sheet, CssValueWork work)
    {
        var builder = new StringBuilder();
        var ranges = new Dictionary<CssRule, CssTextRange>(ReferenceEqualityComparer.Instance);
        for (var i = 0; i < sheet.Rules.Count; i++)
        {
            work.Charge(1);
            if (i != 0) builder.Append('\n');
            var start = builder.Length;
            Append(builder, sheet.Rules[i], work);
            ranges.Add(sheet.Rules[i], new CssTextRange(start, builder.Length));
        }
        work.CheckCancellation();
        var text = builder.ToString();
        work.Charge(text.Length);
        work.CheckCancellation();
        return new CssSerializationSnapshot(text, ranges);
    }

    private static void Append(StringBuilder builder, CssRule rule, CssValueWork work)
    {
        work.CheckCancellation();
        if (rule is not CssStyleRule style) throw new InvalidOperationException("Unknown validated rule kind.");
        builder.Append(style.SelectorText).Append(" { ");
        work.Charge(style.SelectorText.Length);
        var declarations = style.Style.Serialize(work);
        builder.Append(declarations);
        work.Charge(declarations.Length);
        if (declarations.Length != 0) builder.Append(' ');
        builder.Append('}');
        work.CheckCancellation();
    }
}
