using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values;

namespace Jint.HtmlParser.Css.Serialization;

internal static class CssRuleSerializer
{
    internal static string Serialize(CssRule rule) => Serialize(rule, new CssValueWork(default));

    internal static string Serialize(CssRule rule, CssValueWork work)
    {
        work.CheckCancellation();
        var builder = new ValueStringBuilder(stackalloc char[256]);
        Append(ref builder, [rule], null, work);
        var text = builder.ToString();
        work.Charge(text.Length);
        work.CheckCancellation();
        return text;
    }

    internal static CssSerializationSnapshot SerializeSheet(CssStyleSheet sheet, CssValueWork work)
    {
        var builder = new ValueStringBuilder(stackalloc char[256]);
        var ranges = new Dictionary<CssRule, CssTextRange>(ReferenceEqualityComparer.Instance);
        Append(ref builder, sheet.Rules, ranges, work);
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

    private static void Append(ref ValueStringBuilder builder, IReadOnlyList<CssRule> rules,
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
            if (rule is CssImportRule import)
            {
                builder.Append("@import url(");
                CssSyntaxSerializer.AppendString(ref builder, import.Href, work);
                builder.Append(')');
                var mediaText = import.Media.Serialize(work);
                if (mediaText.Length != 0)
                {
                    builder.Append(' ');
                    builder.Append(mediaText);
                }
                builder.Append(';');
                ranges?.Add(rule, new CssTextRange(start, builder.Length));
            }
            else if (rule is CssLayerStatementRule statement)
            {
                builder.Append("@layer ");
                for (var i = 0; i < statement.Names.Count; i++)
                {
                    if (i != 0) builder.Append(", ");
                    work.Charge(statement.Names[i].Text.Length + 1);
                    builder.Append(statement.Names[i].Text);
                }
                builder.Append(';');
                ranges?.Add(rule, new CssTextRange(start, builder.Length));
            }
            else if (rule is CssLayerBlockRule layer)
            {
                builder.Append("@layer");
                if (layer.Name.Length != 0)
                {
                    builder.Append(' ');
                    builder.Append(layer.Name);
                }
                work.Charge(layer.Name.Length + 1);
                builder.Append(" {");
                if (layer.Rules.Count != 0) builder.Append('\n');
                frames.Push(new Frame(layer.Rules, layer, start));
            }
            else if (rule is CssConditionRule conditionRule)
            {
                var condition = conditionRule is CssMediaRule media ? media.Media.Serialize(work) : conditionRule.ConditionText;
                work.Charge(condition.Length);
                builder.Append(conditionRule is CssMediaRule ? "@media " : "@supports ");
                builder.Append(condition);
                builder.Append(" {");
                if (conditionRule.Rules.Count != 0) builder.Append('\n');
                frames.Push(new Frame(conditionRule.Rules, conditionRule, start));
            }
            else if (rule is CssGenericRule generic)
            {
                work.Charge(generic.Text.Length);
                builder.Append(generic.Text);
                ranges?.Add(rule, new CssTextRange(start, builder.Length));
            }
            else if (rule is CssFontFaceRule fontFace)
            {
                // Fonts 4 §12.1; serialize the declaration block, including font-display.
                builder.Append("@font-face { ");
                var declarations = fontFace.Style.AppendTo(ref builder, work);
                work.Charge(declarations);
                if (declarations != 0) builder.Append(' ');
                builder.Append('}');
                ranges?.Add(rule, new CssTextRange(start, builder.Length));
            }
            else if (rule is CssStyleRule style)
            {
                // Finite nesting checkpoint: retain validated author text. CSS Nesting §6's
                // absolute selector serialization (& insertion for relative branches) is deferred.
                // https://drafts.csswg.org/css-nesting-1/#cssom
                builder.Append(style.SelectorText);
                builder.Append(" { ");
                work.Charge(style.SelectorText.Length);
                var declarations = style.Style.AppendTo(ref builder, work);
                work.Charge(declarations);
                if (declarations != 0) builder.Append(' ');
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
