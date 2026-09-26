using System.Text;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values.Math;
using Jint.HtmlParser.Css.Values.Properties;

namespace Jint.HtmlParser.Css.Values.Transforms;

// Value-only resolution. No element, viewport or layout state enters this layer.
// https://drafts.csswg.org/css-transforms-2/#resolved-value-of-transform
internal static class CssTransformMatrix
{
    internal static bool NeedsReferenceBox(CssTransformList list, CssValueWork work)
    {
        work.CheckCancellation();
        for (var i = 0; i < list.Count; i++)
        {
            work.Charge(1);
            var function = list[i];
            for (var j = 0; j < function.Arguments.Count; j++)
            {
                work.Charge(1);
                if (function.Descriptor.TranslationAxis(j) < 0) continue;
                var value = function.Arguments[j];
                if (value.Kind == CssPropertyValueKind.Numeric && value.Numeric.Kind == CssNumericKind.Percentage)
                    return true;
                if (value.Kind != CssPropertyValueKind.Math) continue;
                for (var k = 0; k < value.Math.NodeCount; k++)
                {
                    work.Charge(1);
                    var node = value.Math.GetNode(k);
                    if (node.Kind == CssMathNodeKind.Numeric && node.Numeric.Kind == CssNumericKind.Percentage)
                        return true;
                }
            }
        }
        work.CheckCancellation();
        return false;
    }

    internal static string Resolve(CssTransformList list, CssValueWork work, double? width = null, double? height = null)
    {
        work.CheckCancellation();
        // Column-major matrices; allocate a fixed scratch set once, independent of list length.
        Span<double> matrix = stackalloc double[16];
        Span<double> functionMatrix = stackalloc double[16];
        Span<double> product = stackalloc double[16];
        Span<double> arguments = stackalloc double[16];
        Identity(matrix);
        for (var i = 0; i < list.Count; i++)
        {
            work.Charge(1);
            var function = list[i];
            arguments.Clear();
            for (var j = 0; j < function.Arguments.Count; j++)
            {
                work.Charge(1);
                var basis = function.Descriptor.TranslationAxis(j) switch { 0 => width, 1 => height, _ => null };
                arguments[j] = Numeric(function.Arguments[j], basis, work);
            }
            Function(function, arguments, functionMatrix);
            for (var column = 0; column < 4; column++)
            {
                for (var row = 0; row < 4; row++)
                {
                    work.Charge(4);
                    double sum = 0;
                    for (var k = 0; k < 4; k++) sum += matrix[k * 4 + row] * functionMatrix[column * 4 + k];
                    if (!double.IsFinite(sum)) throw Missing("matrix-arithmetic", function.Span);
                    product[column * 4 + row] = sum;
                }
            }
            product.CopyTo(matrix);
        }
        var result = Serialize(matrix, work);
        work.CheckCancellation();
        return result;
    }

    private static double Numeric(CssPropertyValue value, double? basis, CssValueWork work)
    {
        if (value.Kind == CssPropertyValueKind.Keyword && value.Text == "none") return double.PositiveInfinity;
        CssMathNumeric numeric;
        if (value.Kind == CssPropertyValueKind.Numeric)
        {
            var atom = value.Numeric;
            numeric = new(CssMathNumbers.ParseFinite(atom.Number, atom.Unit, work), atom.Kind,
                CssMathNumbers.CanonicalUnit(atom.Unit), atom.Span);
        }
        else
        {
            var math = value.Math;
            var builder = new CssMathBuilder(work);
            var mapped = new int[math.NodeCount];
            for (var i = math.NodeCount - 1; i >= 0; i--)
            {
                work.Charge(1);
                var node = math.GetNode(i);
                var children = new List<int>(node.ChildCount);
                for (var j = 0; j < node.ChildCount; j++)
                {
                    work.Charge(1);
                    children.Add(mapped[math.GetChild(node.ChildStart + j)]);
                }
                var converted = node.Kind == CssMathNodeKind.Numeric ? Percentage(node.Numeric, basis) : default;
                var type = node.Type;
                mapped[i] = builder.Add(node.Kind, new(type.Length, type.Angle, type.Time, type.Frequency,
                    type.Resolution, type.Flex), node.Span, converted, children,
                    node.Kind == CssMathNodeKind.Round ? node.RoundingStrategy : CssRoundingStrategy.Nearest);
            }
            var simplified = CssMathSimplifier.Freeze(builder, mapped[math.RootIndex], math.Context, math.Span, work);
            var root = simplified.GetNode(simplified.RootIndex);
            if (root.Kind != CssMathNodeKind.Numeric) throw Missing("unresolved-transform-calculation", value.Span);
            numeric = new(CssMathNumbers.NormalizeTopLevel(root.Numeric.Value), root.Numeric.Kind,
                root.Numeric.Unit, root.Numeric.Span);
        }
        numeric = Percentage(numeric, basis);
        if (!double.IsFinite(numeric.Value)) throw Missing("matrix-arithmetic", value.Span);
        if (numeric.Kind == CssNumericKind.Dimension && numeric.Unit is not (CssUnit.Px or CssUnit.Deg))
            throw Missing("uncomputed-transform-component", value.Span);
        return numeric.Value;
    }

    private static CssMathNumeric Percentage(CssMathNumeric numeric, double? basis)
    {
        if (numeric.Kind != CssNumericKind.Percentage) return numeric;
        if (basis is not { } size || !double.IsFinite(size) || size < 0)
            throw Missing("transform-reference-box", numeric.Span);
        // Divide before multiplication to avoid unnecessary intermediate overflow.
        return new(numeric.Value / 100 * size, CssNumericKind.Dimension, CssUnit.Px, numeric.Span);
    }

    private static void Identity(Span<double> matrix)
    {
        matrix.Clear();
        matrix[0] = matrix[5] = matrix[10] = matrix[15] = 1;
    }

    private static void Function(CssTransformFunction function, ReadOnlySpan<double> a, Span<double> m)
    {
        Identity(m);
        var kind = function.Descriptor.Kind;
        switch (kind)
        {
            case CssTransformFunctionKind.Matrix:
                m[0] = a[0]; m[1] = a[1]; m[4] = a[2]; m[5] = a[3]; m[12] = a[4]; m[13] = a[5]; break;
            case CssTransformFunctionKind.Matrix3d: a.CopyTo(m); break;
            case CssTransformFunctionKind.Translate:
                m[12] = a[0]; m[13] = a[1]; break;
            case CssTransformFunctionKind.TranslateX: m[12] = a[0]; break;
            case CssTransformFunctionKind.TranslateY: m[13] = a[0]; break;
            case CssTransformFunctionKind.TranslateZ: m[14] = a[0]; break;
            case CssTransformFunctionKind.Translate3d: m[12] = a[0]; m[13] = a[1]; m[14] = a[2]; break;
            case CssTransformFunctionKind.Scale: m[0] = a[0]; m[5] = function.Arguments.Count == 1 ? a[0] : a[1]; break;
            case CssTransformFunctionKind.ScaleX: m[0] = a[0]; break;
            case CssTransformFunctionKind.ScaleY: m[5] = a[0]; break;
            case CssTransformFunctionKind.ScaleZ: m[10] = a[0]; break;
            case CssTransformFunctionKind.Scale3d: m[0] = a[0]; m[5] = a[1]; m[10] = a[2]; break;
            case CssTransformFunctionKind.Rotate:
            case CssTransformFunctionKind.RotateZ: Rotation(0, 0, 1, a[0], m); break;
            case CssTransformFunctionKind.RotateX: Rotation(1, 0, 0, a[0], m); break;
            case CssTransformFunctionKind.RotateY: Rotation(0, 1, 0, a[0], m); break;
            case CssTransformFunctionKind.Rotate3d: Rotation(a[0], a[1], a[2], a[3], m); break;
            case CssTransformFunctionKind.Skew: m[4] = Tangent(a[0]); m[1] = Tangent(a[1]); break;
            case CssTransformFunctionKind.SkewX: m[4] = Tangent(a[0]); break;
            case CssTransformFunctionKind.SkewY: m[1] = Tangent(a[0]); break;
            case CssTransformFunctionKind.Perspective: m[11] = double.IsPositiveInfinity(a[0]) ? 0 : -1 / System.Math.Max(1, a[0]); break;
            default: throw new InvalidOperationException("Unknown transform function.");
        }
    }

    private static double Tangent(double degrees) => System.Math.Tan((degrees % 180) * (System.Math.PI / 180));

    private static void Rotation(double x, double y, double z, double degrees, Span<double> m)
    {
        var largest = System.Math.Max(System.Math.Abs(x), System.Math.Max(System.Math.Abs(y), System.Math.Abs(z)));
        if (largest == 0) return;
        x /= largest; y /= largest; z /= largest;
        var length = System.Math.Sqrt(x * x + y * y + z * z);
        x /= length; y /= length; z /= length;
        var radians = (degrees % 360) * (System.Math.PI / 180);
        var c = System.Math.Cos(radians);
        var s = System.Math.Sin(radians);
        var t = 1 - c;
        m[0] = t * x * x + c; m[4] = t * x * y - s * z; m[8] = t * x * z + s * y;
        m[1] = t * x * y + s * z; m[5] = t * y * y + c; m[9] = t * y * z - s * x;
        m[2] = t * x * z - s * y; m[6] = t * y * z + s * x; m[10] = t * z * z + c;
    }

    private static string Serialize(ReadOnlySpan<double> m, CssValueWork work)
    {
        var twoD = m[2] == 0 && m[3] == 0 && m[6] == 0 && m[7] == 0 && m[8] == 0 && m[9] == 0 &&
            m[10] == 1 && m[11] == 0 && m[14] == 0 && m[15] == 1;
        var text = new StringBuilder(twoD ? "matrix(" : "matrix3d(");
        ReadOnlySpan<int> indices = [0, 1, 4, 5, 12, 13];
        var count = twoD ? 6 : 16;
        for (var i = 0; i < count; i++)
        {
            work.Charge(1);
            if (i != 0) text.Append(", ");
            var number = CssMathSerializer.SerializeFiniteNumber(m[twoD ? indices[i] : i], work);
            work.Charge(number.Length);
            text.Append(number);
        }
        return text.Append(')').ToString();
    }

    private static CssIncompleteGrammarException Missing(string dependency, CssSourceSpan span) =>
        new("transform", "C6:" + dependency, span);
}
