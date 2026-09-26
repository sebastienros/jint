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

    private sealed class Frame(IReadOnlyList<CssRule> rules, CssRule? owner = null, int start = 0)
    {
        internal readonly IReadOnlyList<CssRule> Rules = rules;
        internal readonly CssRule? Owner = owner;
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
                    if (frame.Rules.Count != 0 || owner is CssKeyframesRule) builder.Append('\n');
                    builder.Append('}');
                    ranges?.Add(owner, new CssTextRange(frame.Start, builder.Length));
                }
                frames.Pop();
                continue;
            }
            if (frame.Index != 0) builder.Append('\n');
            var rule = frame.Rules[frame.Index++];
            var start = builder.Length;
            if (rule is CssImportRule import)
            {
                builder.Append("@import url(");
                CssSyntaxSerializer.AppendString(builder, import.Href, work);
                builder.Append(')');
                var mediaText = import.Media.Serialize(work);
                if (mediaText.Length != 0) builder.Append(' ').Append(mediaText);
                builder.Append(';');
                ranges?.Add(rule, new CssTextRange(start, builder.Length));
            }
            else if (rule is CssConditionRule conditionRule)
            {
                var condition = conditionRule is CssMediaRule media ? media.Media.Serialize(work) : conditionRule.ConditionText;
                work.Charge(condition.Length);
                builder.Append(conditionRule is CssMediaRule ? "@media " : "@supports ").Append(condition).Append(" {");
                if (conditionRule.Rules.Count != 0) builder.Append('\n');
                frames.Push(new Frame(conditionRule.Rules, conditionRule, start));
            }
            else if (rule is CssKeyframesRule keyframes)
            {
                builder.Append("@keyframes ").Append(keyframes.SerializeName(work)).Append(" { ");
                frames.Push(new Frame(keyframes.Rules, keyframes, start));
            }
            else if (rule is CssKeyframeRule keyframe)
            {
                if (frame.Owner is CssKeyframesRule) builder.Append("  ");
                // Ranges identify each rule's own text, without its containing rule's indent.
                start = builder.Length;
                var declarations = keyframe.Style.Serialize(work);
                work.Charge(keyframe.KeyText.Length + declarations.Length);
                builder.Append(keyframe.KeyText).Append(" { ").Append(declarations);
                if (declarations.Length != 0) builder.Append(' ');
                builder.Append('}');
                ranges?.Add(rule, new CssTextRange(start, builder.Length));
            }
            else if (rule is CssStyleRule style)
            {
                // Finite nesting checkpoint: retain validated author text. CSS Nesting §6's
                // absolute selector serialization (& insertion for relative branches) is deferred.
                // https://drafts.csswg.org/css-nesting-1/#cssom
                builder.Append(style.SelectorText).Append(" { ");
                work.Charge(style.SelectorText.Length);
                var declarations = style.Style.Serialize(work);
                builder.Append(declarations);
                work.Charge(declarations.Length);
                if (declarations.Length != 0) builder.Append(' ');
                if (style.Rules.Count != 0)
                {
                    builder.Append('\n');
                    frames.Push(new Frame(style.Rules, style, start));
                }
                else
                {
                    builder.Append('}');
                    ranges?.Add(rule, new CssTextRange(start, builder.Length));
                }
            }
            else throw new InvalidOperationException("Unknown validated rule kind.");
        }
        work.CheckCancellation();
    }
}
