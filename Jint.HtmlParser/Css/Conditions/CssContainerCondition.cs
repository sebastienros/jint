using Jint.HtmlParser.Css.Media;
using Jint.HtmlParser.Css.Values;

namespace Jint.HtmlParser.Css.Conditions;

internal enum CssContainerAxis { Width, InlineSize, Height, BlockSize, Both, Style, ScrollState, Unknown }
internal sealed record CssContainerFeature(CssContainerAxis Axis, CssMediaComparison Comparison,
    double Pixels = 0, string? Dependency = null);
internal sealed record CssContainerInstruction(CssMediaOperation Operation, CssContainerFeature? Feature = null);

// Conditional 5 §§5.4/6.1. Immutable postfix program; no DOM, engine or layout ownership.
// https://drafts.csswg.org/css-conditional-5/#container-rule
internal sealed class CssContainerCondition(CssContainerInstruction[] instructions)
{
    internal IReadOnlyList<CssContainerInstruction> Instructions { get; } = Array.AsReadOnly(instructions);

    internal CssMediaTruth Evaluate(Func<CssContainerFeature, CssMediaTruth> feature, CssValueWork work)
    {
        var stack = new List<CssMediaTruth>();
        foreach (var instruction in Instructions)
        {
            work.Charge(1);
            if (instruction.Operation == CssMediaOperation.Feature) stack.Add(feature(instruction.Feature!));
            else if (instruction.Operation == CssMediaOperation.Unknown) stack.Add(CssMediaTruth.Unknown);
            else if (instruction.Operation == CssMediaOperation.Not) stack[^1] = CssMediaQuery.Not(stack[^1]);
            else
            {
                var right = stack[^1];
                stack.RemoveAt(stack.Count - 1);
                stack[^1] = CssMediaQuery.Combine(stack[^1], right, instruction.Operation);
            }
        }
        work.CheckCancellation();
        return stack[0];
    }
}
