using Jint.HtmlParser.Css.Values.Math;

namespace Jint.HtmlParser.Css.Values.Colors;

// Evaluate a typed arena without an environment or recursion. Relative dimensions
// which survive simplification are an obligation, not an invented pixel value.
internal static class CssColorMath
{
    internal static bool TryEvaluate(CssMathValue math, CssValueWork work, out double result)
    {
        work.CheckCancellation();
        var values = new double[math.NodeCount];
        var visited = new bool[math.NodeCount];
        work.CheckCancellation();
        var stack = new Stack<(int Index, bool Exit)>();
        stack.Push((math.RootIndex, false));
        result = 0;
        while (stack.Count != 0)
        {
            work.Charge(1);
            var (index, exit) = stack.Pop();
            if (visited[index]) continue;
            var node = math.GetNode(index);
            if (node.Kind == CssMathNodeKind.Round && node.RoundingStrategy == CssRoundingStrategy.LineWidth)
            {
                // CSS Values 4 §6: even absolute lengths need a device-pixel size
                // for line-width snapping. The ordinary stepped helper cannot resolve it.
                work.CheckCancellation();
                return false;
            }
            if (!exit)
            {
                stack.Push((index, true));
                for (var i = node.ChildCount - 1; i >= 0; i--)
                {
                    work.Charge(1);
                    stack.Push((math.GetChild(node.ChildStart + i), false));
                }
                continue;
            }
            if (node.Kind == CssMathNodeKind.Numeric)
            {
                var numeric = node.Numeric;
                if (numeric.Kind == CssNumericKind.Dimension && CssMathNumbers.CanonicalUnit(numeric.Unit) == numeric.Unit &&
                    numeric.Unit is not (CssUnit.Px or CssUnit.Deg or CssUnit.S or CssUnit.Hz or CssUnit.Dppx or CssUnit.Fr))
                { work.CheckCancellation(); return false; }
                values[index] = numeric.Value;
            }
            else values[index] = Evaluate(node, math, values, work);
            visited[index] = true;
        }
        result = values[math.RootIndex];
        work.CheckCancellation();
        return true;
    }

    private static double Evaluate(CssMathNode node, CssMathValue math, double[] values, CssValueWork work)
    {
        double Child(int i)
        {
            work.Charge(1);
            return values[math.GetChild(node.ChildStart + i)];
        }
        switch (node.Kind)
        {
            case CssMathNodeKind.Sum:
            case CssMathNodeKind.Product:
            case CssMathNodeKind.Min:
            case CssMathNodeKind.Max:
                var result = Child(0);
                for (var i = 1; i < node.ChildCount; i++)
                {
                    var next = Child(i);
                    result = node.Kind switch
                    {
                        CssMathNodeKind.Sum => result + next,
                        CssMathNodeKind.Product => result * next,
                        CssMathNodeKind.Min => CssMathNumbers.CssMin(result, next),
                        _ => CssMathNumbers.CssMax(result, next)
                    };
                }
                return result;
            case CssMathNodeKind.Negate: return -Child(0);
            case CssMathNodeKind.Invert: return 1 / Child(0);
            case CssMathNodeKind.Abs: return CssMathSign.Abs(Child(0), work);
            case CssMathNodeKind.Sign: return CssMathSign.Sign(Child(0), work);
            case CssMathNodeKind.AbsentBound: return 0;
            case CssMathNodeKind.Clamp:
                var clamped = Child(1);
                if (math.GetNode(math.GetChild(node.ChildStart + 2)).Kind != CssMathNodeKind.AbsentBound)
                    clamped = CssMathNumbers.CssMin(clamped, Child(2));
                if (math.GetNode(math.GetChild(node.ChildStart)).Kind != CssMathNodeKind.AbsentBound)
                    clamped = CssMathNumbers.CssMax(clamped, Child(0));
                return clamped;
            case CssMathNodeKind.Round: return CssMathStepped.Round(Child(0), node.ChildCount == 1 ? 1 : Child(1), node.RoundingStrategy, work);
            case CssMathNodeKind.Mod: return CssMathStepped.Mod(Child(0), Child(1), work);
            case CssMathNodeKind.Rem: return CssMathStepped.Rem(Child(0), Child(1), work);
            case CssMathNodeKind.Sin:
            case CssMathNodeKind.Cos:
            case CssMathNodeKind.Tan:
            case CssMathNodeKind.Asin:
            case CssMathNodeKind.Acos:
            case CssMathNodeKind.Atan:
                var function = node.Kind switch
                {
                    CssMathNodeKind.Sin => CssMathFunction.Sin,
                    CssMathNodeKind.Cos => CssMathFunction.Cos,
                    CssMathNodeKind.Tan => CssMathFunction.Tan,
                    CssMathNodeKind.Asin => CssMathFunction.Asin,
                    CssMathNodeKind.Acos => CssMathFunction.Acos,
                    _ => CssMathFunction.Atan
                };
                return CssMathTrigonometric.Evaluate(function, Child(0), math.GetNode(math.GetChild(node.ChildStart)).Type.Angle == 1, work);
            case CssMathNodeKind.Atan2: return CssMathTrigonometric.Atan2(Child(0), Child(1), work);
            case CssMathNodeKind.Pow: return CssMathExponential.Pow(Child(0), Child(1), work);
            case CssMathNodeKind.Sqrt: return CssMathExponential.Sqrt(Child(0), work);
            case CssMathNodeKind.Log: return CssMathExponential.Log(Child(0), node.ChildCount == 1 ? null : Child(1), work);
            case CssMathNodeKind.Exp: return CssMathExponential.Exp(Child(0), work);
            case CssMathNodeKind.Hypot:
                work.CheckCancellation();
                var legs = new double[node.ChildCount];
                work.CheckCancellation();
                for (var i = 0; i < legs.Length; i++) legs[i] = Child(i);
                return CssMathExponential.Hypot(legs, work);
            default: throw new InvalidOperationException("Unknown typed math node.");
        }
    }
}
