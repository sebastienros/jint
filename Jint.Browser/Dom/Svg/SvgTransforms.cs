using Jint.Browser.Geometry;
using Jint.HtmlParser.Svg;
using Jint.Native;
using Jint.Native.Object;

namespace Jint.Browser.Dom.Svg;

/// <summary>SVG transform arithmetic uses the same column-vector matrices as Geometry.</summary>
/// <remarks>https://svgwg.org/svg2-draft/coords.html#InterfaceSVGTransform</remarks>
internal static class SvgTransforms
{
    internal static double[] Matrix(SvgValueData data)
    {
        var matrix = GeometryMatrix.Identity();
        switch ((SvgTransformKind) data.Unit)
        {
            case SvgTransformKind.Matrix:
                matrix[0] = data.A;
                matrix[1] = data.B;
                matrix[4] = data.C;
                matrix[5] = data.D;
                matrix[12] = data.E;
                matrix[13] = data.F;
                break;
            case SvgTransformKind.Translate:
                GeometryMatrix.Translate(matrix, data.A, data.B, 0);
                break;
            case SvgTransformKind.Scale:
                GeometryMatrix.Scale(matrix, data.A, data.B, 1);
                break;
            case SvgTransformKind.Rotate:
                GeometryMatrix.Translate(matrix, data.B, data.C, 0);
                GeometryMatrix.RotateAboutAxis(matrix, 2, data.A);
                GeometryMatrix.Translate(matrix, -data.B, -data.C, 0);
                break;
            case SvgTransformKind.SkewX:
                GeometryMatrix.Skew(matrix, data.A, 0);
                break;
            case SvgTransformKind.SkewY:
                GeometryMatrix.Skew(matrix, 0, data.A);
                break;
        }
        return matrix;
    }

    internal static SvgValueData FromMatrix(double[] m) => new(m[0], m[1], m[4], m[5], m[12], m[13], (ushort) SvgTransformKind.Matrix);

    internal static SvgValueData MatrixArgument(SvgRealm owner, JsValue value)
    {
        var (m, is2D) = GeometryConversion.MatrixInit(owner.Realm, value);
        if (!is2D) DomFailures.Refuse(owner.Dom, "SVGTransform.setMatrix", "InvalidStateError", "An SVG transform must be two-dimensional.");
        foreach (var n in m)
        {
            if (!double.IsFinite(n)) Jint.Runtime.Throw.TypeError(owner.Realm, "An SVG matrix must be finite.");
        }
        return FromMatrix(m);
    }
}

/// <summary>A live SVGTransform and its SameObject DOMMatrix, including matrix mutation write-through.</summary>
/// <remarks>https://svgwg.org/svg2-draft/coords.html#InterfaceSVGTransform</remarks>
internal sealed class JsSvgTransform : ObjectInstance, IGeometryMatrixBinding
{
    private JsDomMatrix? _matrix;
    private SvgValueData? _matrixSource;
    internal JsSvgTransform(SvgValueCell cell, bool readOnly) : base(cell.Owner.Engine)
    {
        Cell = cell;
        ReadOnly = readOnly;
        Prototype = cell.Owner.Prototype("SVGTransform");
    }
    internal SvgValueCell Cell { get; }
    internal bool ReadOnly { get { Cell.Read(); return field && Cell.Attribute is not null; } }
    internal JsDomMatrix Matrix => _matrix ??= new JsDomMatrix(Cell.Owner.Dom.Geometry, true, GeometryMatrix.Identity(), true) { Binding = this };

    internal JsValue Invoke(string method, JsValue[] args)
    {
        var owner = Cell.Owner;
        SvgValues.Require(owner, args, method switch { "setTranslate" or "setScale" => 2, "setRotate" => 3, _ => 1 });
        SvgValues.Writable(owner, ReadOnly);
        SvgValueData data;
        if (method == "setMatrix") data = SvgTransforms.MatrixArgument(owner, args[0]);
        else
        {
            var a = SvgValues.Number(owner, args[0]);
            var b = method is "setTranslate" or "setScale" or "setRotate" ? SvgValues.Number(owner, args[1]) : 0;
            var c = method == "setRotate" ? SvgValues.Number(owner, args[2]) : 0;
            var kind = method switch
            {
                "setTranslate" => SvgTransformKind.Translate,
                "setScale" => SvgTransformKind.Scale,
                "setRotate" => SvgTransformKind.Rotate,
                "setSkewX" => SvgTransformKind.SkewX,
                _ => SvgTransformKind.SkewY,
            };
            data = new SvgValueData(a, b, c, Unit: (ushort) kind);
        }
        Cell.Write(data, ReadOnly);
        return JsValue.Undefined;
    }

    public void Refresh(double[] elements)
    {
        var data = Cell.Read();
        if (_matrixSource == data) return;
        SvgTransforms.Matrix(data).CopyTo(elements, 0);
        _matrixSource = data;
    }

    public void CheckWritable() => SvgValues.Writable(Cell.Owner, ReadOnly);

    public void Commit(double[] elements, bool is2D)
    {
        SvgValues.Writable(Cell.Owner, ReadOnly);
        if (!is2D || elements.Any(static n => !double.IsFinite(n)))
        {
            _matrixSource = null;
            if (_matrix is not null) _matrix.Is2D = true;
            DomFailures.Refuse(Cell.Owner.Dom, "SVGTransform.matrix", "InvalidStateError", "An SVG transform must be finite and two-dimensional.");
        }
        Cell.Write(SvgTransforms.FromMatrix(elements), ReadOnly);
        _matrixSource = Cell.Data;
    }
}
