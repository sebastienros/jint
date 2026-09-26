using System.Text;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values;

namespace Jint.HtmlParser.Css.Serialization;

internal static class CssRuleSerializer
{
    internal static string Serialize(CssRule rule) => Serialize(rule, new CssValueWork(default));

    internal static string Serialize(CssRule rule, CssValueWork work)
    {
        work.CheckCancellation();
        var builder = new StringBuilder();
        Append(builder, [rule], null, work);
        var text = builder.ToString();
        work.Charge(text.Length);
        work.CheckCancellation();
        return text;
    }

    internal static CssSerializationSnapshot SerializeSheet(CssStyleSheet sheet, CssValueWork work)
    {
        var builder = new StringBuilder();
        var ranges = new Dictionary<CssRule, CssTextRange>(ReferenceEqualityComparer.Instance);
        Append(builder, sheet.Rules, ranges, work);
        work.CheckCancellation();
        var text = builder.ToString();
        work.Charge(text.Length);
        work.CheckCancellation();
        return new CssSerializationSnapshot(text, ranges);
    }

    private sealed class Frame(IReadOnlyList<CssRule> rules, CssMediaRule? owner = null, int start = 0)
    {
        internal readonly IReadOnlyList<CssRule> Rules = rules;
        internal readonly CssMediaRule? Owner = owner;
        internal readonly int Start = start;
        internal int Index;
    }

    private static void Append(StringBuilder builder, IReadOnlyList<CssRule> rules,
        Dictionary<CssRule, CssTextRange>? ranges, CssValueWork work)
    {
        var frames = new Stack<Frame>();
        frames.Push(new Frame(rules));
        while (frames.TryPeek(out var frame))
        {
            work.Charge(1);
            if (frame.Index == frame.Rules.Count)
            {
                if (frame.Owner is { } owner)
                {
                    if (frame.Rules.Count != 0) builder.Append('\n');
                    builder.Append('}');
                    ranges?.Add(owner, new CssTextRange(frame.Start, builder.Length));
                }
                frames.Pop();
                continue;
            }
            if (frame.Index != 0) builder.Append('\n');
            var rule = frame.Rules[frame.Index++];
            var start = builder.Length;
            if (rule is CssMediaRule media)
            {
                var condition = media.Media.Serialize(work);
                builder.Append("@media ").Append(condition).Append(" {");
                if (media.Rules.Count != 0) builder.Append('\n');
                frames.Push(new Frame(media.Rules, media, start));
            }
            else if (rule is CssStyleRule style)
            {
                builder.Append(style.SelectorText).Append(" { ");
                work.Charge(style.SelectorText.Length);
                var declarations = style.Style.Serialize(work);
                builder.Append(declarations);
                work.Charge(declarations.Length);
                if (declarations.Length != 0) builder.Append(' ');
                builder.Append('}');
                ranges?.Add(rule, new CssTextRange(start, builder.Length));
            }
            else throw new InvalidOperationException("Unknown validated rule kind.");
        }
        work.CheckCancellation();
    }
}
