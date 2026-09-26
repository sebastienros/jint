using Jint.HtmlParser.Css.Values.Math;
using Jint.HtmlParser.Css.Values.Properties;

namespace Jint.HtmlParser.Css.Values.Transforms;

// https://drafts.csswg.org/css-transforms-2/#individual-transform-serialization
internal static class CssTransformSerializer
{
    internal static string Serialize(CssTransformValue value, CssValueWork work)
    {
        work.CheckCancellation();
        var x = value.X.Serialize();
        var y = value.Y.Serialize();
        var z = value.Z.Serialize();
        string text;
        if (value.Kind == CssTransformKind.Translate)
        {
            var omitZ = Is(value.Z, 0, CssNumericKind.Dimension, CssUnit.Px, work);
            text = omitZ && Is(value.Y, 0, CssNumericKind.Dimension, CssUnit.Px, work) ? x :
                omitZ ? x + " " + y : x + " " + y + " " + z;
        }
        else if (value.Kind == CssTransformKind.Scale)
        {
            var omitZ = Is(value.Z, 1, CssNumericKind.Number, CssUnit.None, work);
            text = omitZ && Equal(value.X, value.Y, work) ? x :
                omitZ ? x + " " + y : x + " " + y + " " + z;
        }
        else
        {
            var angle = value.Angle.Serialize();
            var zeroX = Is(value.X, 0, CssNumericKind.Number, CssUnit.None, work);
            var zeroY = Is(value.Y, 0, CssNumericKind.Number, CssUnit.None, work);
            var zeroZ = Is(value.Z, 0, CssNumericKind.Number, CssUnit.None, work);
            var axis = zeroX && zeroY && !zeroZ ? value.Z :
                zeroY && zeroZ && !zeroX ? value.X : zeroX && zeroZ && !zeroY ? value.Y : null;
            if (axis is not null && TryNumber(axis, work, out var numeric) && double.IsFinite(numeric.Value))
            {
                if (numeric.Value < 0) angle = NegatedAngle(value.Angle, work);
                text = zeroX && zeroY ? angle : (zeroY && zeroZ ? "x " : "y ") + angle;
            }
            else text = x + " " + y + " " + z + " " + angle;
        }
        work.Charge(text.Length);
        work.CheckCancellation();
        return text;
    }

    private static string NegatedAngle(CssPropertyValue angle, CssValueWork work)
    {
        if (TryNumber(angle, work, out var numeric) && double.IsFinite(numeric.Value))
            return CssTransformParser.Constant(-numeric.Value, numeric.Unit, angle.Span, work).Serialize();
        return "calc(-1 * " + angle.Serialize() + ")";
    }

    private static bool Equal(CssPropertyValue left, CssPropertyValue right, CssValueWork work) =>
        ReferenceEquals(left, right) || TryNumber(left, work, out var a) && TryNumber(right, work, out var b) &&
        a.Kind == b.Kind && a.Unit == b.Unit && a.Value == b.Value;

    private static bool Is(CssPropertyValue value, double expected, CssNumericKind kind, CssUnit unit,
        CssValueWork work) => TryNumber(value, work, out var numeric) &&
        numeric.Kind == kind && numeric.Unit == unit && numeric.Value == expected;

    private static bool TryNumber(CssPropertyValue value, CssValueWork work, out CssMathNumeric numeric)
    {
        if (value.Kind == CssPropertyValueKind.Numeric)
        {
            var atom = value.Numeric;
            numeric = new(CssMathNumbers.ParseFinite(atom.Number, atom.Unit, work), atom.Kind,
                CssMathNumbers.CanonicalUnit(atom.Unit), atom.Span);
            return true;
        }
        var root = value.Math.GetNode(value.Math.RootIndex);
        numeric = root.Kind == CssMathNodeKind.Numeric ? root.Numeric : default;
        return root.Kind == CssMathNodeKind.Numeric;
    }
}
