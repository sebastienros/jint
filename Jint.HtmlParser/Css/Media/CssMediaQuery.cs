using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Syntax;

namespace Jint.HtmlParser.Css.Media;

internal enum CssMediaTruth { False, True, Unknown }
internal enum CssMediaOperation { Feature, Unknown, Not, And, Or }
internal enum CssMediaComparison { Boolean, Equal, Less, LessEqual, Greater, GreaterEqual }

internal sealed record CssMediaFeature(string Name, CssMediaComparison Comparison,
    double Number, CssUnit Unit, string? Keyword)
{
    internal CssMediaTruth Evaluate(CssMediaEnvironment environment)
    {
        if (Keyword is not null)
        {
            var actual = Name switch
            {
                "orientation" => environment.Width > environment.Height ? "landscape" : "portrait",
                "pointer" => environment.Pointer,
                "hover" => environment.Hover,
                "prefers-color-scheme" => environment.ColorScheme,
                "prefers-reduced-motion" => environment.ReducedMotion,
                "prefers-reduced-transparency" => environment.ReducedTransparency,
                "prefers-contrast" => environment.Contrast,
                "forced-colors" => environment.ForcedColors,
                "prefers-reduced-data" => environment.ReducedData,
                "scripting" => environment.Scripting,
                _ => throw new InvalidOperationException("Unvalidated media feature.")
            };
            return Truth(Comparison == CssMediaComparison.Boolean
                ? actual is not ("none" or "no-preference") : actual == Keyword);
        }
        var value = Name switch
        {
            "width" => environment.Width,
            "height" => environment.Height,
            "aspect-ratio" => environment.Height == 0 ? double.PositiveInfinity : environment.Width / environment.Height,
            "resolution" => environment.Resolution,
            "color" => environment.Color,
            "color-index" => environment.ColorIndex,
            "monochrome" => environment.Monochrome,
            "grid" => environment.Grid ? 1 : 0,
            _ => throw new InvalidOperationException("Unvalidated media feature.")
        };
        var expected = Number * (Unit switch
        {
            CssUnit.Cm => 96 / 2.54,
            CssUnit.Mm => 96 / 25.4,
            CssUnit.Q => 96 / 101.6,
            CssUnit.In => 96,
            CssUnit.Pt => 96.0 / 72,
            CssUnit.Pc => 16,
            CssUnit.Em or CssUnit.Rem => environment.InitialFontSize,
            CssUnit.Dpi => 1.0 / 96,
            CssUnit.Dpcm => 2.54 / 96,
            _ => 1
        });
        return Truth(Comparison switch
        {
            CssMediaComparison.Boolean => value != 0,
            CssMediaComparison.Equal => value == expected,
            CssMediaComparison.Less => value < expected,
            CssMediaComparison.LessEqual => value <= expected,
            CssMediaComparison.Greater => value > expected,
            CssMediaComparison.GreaterEqual => value >= expected,
            _ => false
        });
    }

    private static CssMediaTruth Truth(bool value) => value ? CssMediaTruth.True : CssMediaTruth.False;
}

internal sealed record CssMediaInstruction(CssMediaOperation Operation, CssMediaFeature? Feature = null);

// A private immutable postfix condition program: both grammar traversal and evaluation are iterative.
internal sealed class CssMediaQuery(string text, string? type, bool negate, CssMediaInstruction[] condition)
{
    internal string Text { get; } = text;

    internal bool Matches(CssMediaEnvironment environment, CssValueWork work)
    {
        var stack = new List<CssMediaTruth>();
        foreach (var instruction in condition)
        {
            work.Charge(1);
            if (instruction.Operation == CssMediaOperation.Feature) stack.Add(instruction.Feature!.Evaluate(environment));
            else if (instruction.Operation == CssMediaOperation.Unknown) stack.Add(CssMediaTruth.Unknown);
            else if (instruction.Operation == CssMediaOperation.Not) stack[^1] = Not(stack[^1]);
            else
            {
                var right = stack[^1];
                stack.RemoveAt(stack.Count - 1);
                stack[^1] = Combine(stack[^1], right, instruction.Operation);
            }
        }
        var result = stack.Count == 0 ? CssMediaTruth.True : stack[0];
        if (type is not null)
            result = Combine(type == "all" || (type is "screen" or "print" && CssAscii.EqualsIgnoreCase(type, environment.Type))
                ? CssMediaTruth.True : CssMediaTruth.False, result, CssMediaOperation.And);
        work.CheckCancellation();
        return (negate ? Not(result) : result) == CssMediaTruth.True;
    }

    internal static CssMediaTruth Not(CssMediaTruth value) => value switch
    {
        CssMediaTruth.True => CssMediaTruth.False,
        CssMediaTruth.False => CssMediaTruth.True,
        _ => CssMediaTruth.Unknown
    };

    internal static CssMediaTruth Combine(CssMediaTruth left, CssMediaTruth right, CssMediaOperation operation) =>
        operation == CssMediaOperation.And
            ? left == CssMediaTruth.False || right == CssMediaTruth.False ? CssMediaTruth.False
                : left == CssMediaTruth.Unknown || right == CssMediaTruth.Unknown ? CssMediaTruth.Unknown : CssMediaTruth.True
            : left == CssMediaTruth.True || right == CssMediaTruth.True ? CssMediaTruth.True
                : left == CssMediaTruth.Unknown || right == CssMediaTruth.Unknown ? CssMediaTruth.Unknown : CssMediaTruth.False;
}
