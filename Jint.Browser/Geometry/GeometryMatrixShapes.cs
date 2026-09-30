using Jint.Browser.Dom;
using Jint.Native;
using Jint.Runtime;

namespace Jint.Browser.Geometry;

/// <summary>The engine-independent prototype shapes of <c>DOMMatrixReadOnly</c> and <c>DOMMatrix</c>, and their operations.</summary>
/// <remarks>
/// https://drafts.fxtf.org/geometry/#DOMMatrix. Every read-only operation is its mutable counterpart run on a
/// fresh copy, which is how the specification defines each of them.
/// </remarks>
internal static class GeometryMatrixShapes
{
    /// <summary>The attributes in IDL order, with the element each reads.</summary>
    private static readonly (string Name, int Index)[] _attributes =
    [
        ("a", GeometryMatrix.M11), ("b", GeometryMatrix.M12), ("c", GeometryMatrix.M21),
        ("d", GeometryMatrix.M22), ("e", GeometryMatrix.M41), ("f", GeometryMatrix.M42),
        ("m11", GeometryMatrix.M11), ("m12", GeometryMatrix.M12), ("m13", GeometryMatrix.M13), ("m14", GeometryMatrix.M14),
        ("m21", GeometryMatrix.M21), ("m22", GeometryMatrix.M22), ("m23", GeometryMatrix.M23), ("m24", GeometryMatrix.M24),
        ("m31", GeometryMatrix.M31), ("m32", GeometryMatrix.M32), ("m33", GeometryMatrix.M33), ("m34", GeometryMatrix.M34),
        ("m41", GeometryMatrix.M41), ("m42", GeometryMatrix.M42), ("m43", GeometryMatrix.M43), ("m44", GeometryMatrix.M44),
    ];

    private static readonly JsObjectLayout _json = BuildJsonLayout();

    internal static readonly JsObjectShape MatrixReadOnly = BuildMatrixReadOnly();
    internal static readonly JsObjectShape Matrix = BuildMatrix();

    private static JsObjectLayout BuildJsonLayout()
    {
        var builder = new JsObjectLayout.Builder();
        foreach (var (name, _) in _attributes)
        {
            builder.Add(name);
        }

        return builder.Add("is2D").Add("isIdentity").Build();
    }

    private static JsDomMatrix Read(JsValue t, string member) => GeometryBrand.Of<JsDomMatrix>(t, "DOMMatrixReadOnly", member);

    private static JsDomMatrix Write(JsValue t, string member) => GeometryBrand.Of<JsDomMatrix>(t, "DOMMatrix", member, requireMutable: true);

    /// <summary>A mutable copy of <paramref name="source"/>, in its realm, for a read-only operation to run a Self operation on.</summary>
    private static JsDomMatrix Copy(JsDomMatrix source)
        => source.Owner.CreateMatrix(true, (double[]) source.Elements.Clone(), source.Is2D);

    private static JsObjectShape BuildMatrixReadOnly()
    {
        var builder = new JsObjectShape.Builder()
            .PerRealmSlot("constructor")
            .ToStringTag("DOMMatrixReadOnly");

        foreach (var (name, index) in _attributes)
        {
            builder.Accessor(name, (t, _) => JsNumber.Create(Read(t, name).Elements[index]));
        }

        return builder
            .Accessor("is2D", static (t, _) => Read(t, "is2D").Is2D ? JsBoolean.True : JsBoolean.False)
            .Accessor("isIdentity", static (t, _) => GeometryMatrix.IsIdentity(Read(t, "isIdentity").Elements) ? JsBoolean.True : JsBoolean.False)
            .Method("translate", static (t, args) => TranslateSelf(Copy(Read(t, "translate")), args))
            .Method("scale", static (t, args) => ScaleSelf(Copy(Read(t, "scale")), args))
            .Method("scaleNonUniform", static (t, args) =>
            {
                // https://drafts.fxtf.org/geometry/#dom-dommatrixreadonly-scalenonuniform
                var result = Copy(Read(t, "scaleNonUniform"));
                Scale(result, GeometryShapes.Number(args, 0, 1), GeometryShapes.Number(args, 1, 1), 1, 0, 0, 0);
                return result;
            })
            .Method("scale3d", static (t, args) => Scale3dSelf(Copy(Read(t, "scale3d")), args))
            .Method("rotate", static (t, args) => RotateSelf(Copy(Read(t, "rotate")), args))
            .Method("rotateFromVector", static (t, args) => RotateFromVectorSelf(Copy(Read(t, "rotateFromVector")), args))
            .Method("rotateAxisAngle", static (t, args) => RotateAxisAngleSelf(Copy(Read(t, "rotateAxisAngle")), args))
            .Method("skewX", static (t, args) => SkewSelf(Copy(Read(t, "skewX")), args, x: true))
            .Method("skewY", static (t, args) => SkewSelf(Copy(Read(t, "skewY")), args, x: false))
            .Method("multiply", static (t, args) => MultiplySelf(Copy(Read(t, "multiply")), args, pre: false))
            .Method("flipX", static (t, _) =>
            {
                var result = Copy(Read(t, "flipX"));
                GeometryMatrix.Scale(result.Elements, -1, 1, 1);
                return result;
            })
            .Method("flipY", static (t, _) =>
            {
                var result = Copy(Read(t, "flipY"));
                GeometryMatrix.Scale(result.Elements, 1, -1, 1);
                return result;
            })
            .Method("inverse", static (t, _) => InvertSelf(Copy(Read(t, "inverse"))))
            .Method("transformPoint", static (t, args) =>
            {
                // https://drafts.fxtf.org/geometry/#dom-dommatrixreadonly-transformpoint
                var matrix = Read(t, "transformPoint");
                var (x, y, z, w) = GeometryConversion.PointInit(matrix.Owner.Realm, GeometryShapes.At(args, 0));
                return GeometryShapes.Transform(matrix.Owner, matrix.Elements, x, y, z, w);
            })
            .Method("toFloat32Array", static (t, _) =>
            {
                var matrix = Read(t, "toFloat32Array");
                Span<float> values = stackalloc float[16];
                for (var i = 0; i < 16; i++)
                {
                    values[i] = (float) matrix.Elements[i];
                }

                return matrix.Owner.Realm.Intrinsics.Float32Array.Construct(values);
            })
            .Method("toFloat64Array", static (t, _) =>
            {
                var matrix = Read(t, "toFloat64Array");
                return matrix.Owner.Realm.Intrinsics.Float64Array.Construct(matrix.Elements);
            })
            .Method("toJSON", static (t, _) =>
            {
                var matrix = Read(t, "toJSON");
                var values = new JsValue[_attributes.Length + 2];
                for (var i = 0; i < _attributes.Length; i++)
                {
                    values[i] = JsNumber.Create(matrix.Elements[_attributes[i].Index]);
                }

                values[^2] = matrix.Is2D ? JsBoolean.True : JsBoolean.False;
                values[^1] = GeometryMatrix.IsIdentity(matrix.Elements) ? JsBoolean.True : JsBoolean.False;
                return JsObject.Create(matrix.Engine, _json, values);
            })
            .Method("toString", static (t, _) => Stringify(Read(t, "toString")))
            .Build();
    }

    private static JsObjectShape BuildMatrix()
    {
        var builder = new JsObjectShape.Builder()
            .PerRealmSlot("constructor")
            .ToStringTag("DOMMatrix");

        foreach (var (name, index) in _attributes)
        {
            // https://drafts.fxtf.org/geometry/#dom-dommatrix-m13 — a 3D component set off its identity value
            // makes the matrix 3D; setting it back does not make it 2D again.
            var identity = index % 5 == 0 ? 1 : 0;
            var threeD = index is not (GeometryMatrix.M11 or GeometryMatrix.M12 or GeometryMatrix.M21
                or GeometryMatrix.M22 or GeometryMatrix.M41 or GeometryMatrix.M42);
            builder.Accessor(
                name,
                (t, _) => JsNumber.Create(Write(t, name).Elements[index]),
                (t, args) =>
                {
                    var matrix = Write(t, name);
                    var value = GeometryShapes.Assigned(args);
                    matrix.Elements[index] = value;
                    if (threeD && value != identity)
                    {
                        matrix.Is2D = false;
                    }

                    return JsValue.Undefined;
                });
        }

        return builder
            .Method("multiplySelf", static (t, args) => MultiplySelf(Write(t, "multiplySelf"), args, pre: false))
            .Method("preMultiplySelf", static (t, args) => MultiplySelf(Write(t, "preMultiplySelf"), args, pre: true))
            .Method("translateSelf", static (t, args) => TranslateSelf(Write(t, "translateSelf"), args))
            .Method("scaleSelf", static (t, args) => ScaleSelf(Write(t, "scaleSelf"), args))
            .Method("scale3dSelf", static (t, args) => Scale3dSelf(Write(t, "scale3dSelf"), args))
            .Method("rotateSelf", static (t, args) => RotateSelf(Write(t, "rotateSelf"), args))
            .Method("rotateFromVectorSelf", static (t, args) => RotateFromVectorSelf(Write(t, "rotateFromVectorSelf"), args))
            .Method("rotateAxisAngleSelf", static (t, args) => RotateAxisAngleSelf(Write(t, "rotateAxisAngleSelf"), args))
            .Method("skewXSelf", static (t, args) => SkewSelf(Write(t, "skewXSelf"), args, x: true))
            .Method("skewYSelf", static (t, args) => SkewSelf(Write(t, "skewYSelf"), args, x: false))
            .Method("invertSelf", static (t, _) => InvertSelf(Write(t, "invertSelf")))
            .Method("setMatrixValue", static (t, args) =>
            {
                // https://drafts.fxtf.org/geometry/#dom-dommatrix-setmatrixvalue
                var matrix = Write(t, "setMatrixValue");
                var source = TypeConverter.ToString(GeometryShapes.At(args, 0));
                var (elements, is2D) = GeometryConversion.Parse(matrix.Owner.Dom, source, "DOMMatrix.setMatrixValue");
                Array.Copy(elements, matrix.Elements, 16);
                matrix.Is2D = is2D;
                return matrix;
            }, length: 1)
            .Build();
    }

    // https://drafts.fxtf.org/geometry/#dom-dommatrix-translateself
    private static JsDomMatrix TranslateSelf(JsDomMatrix matrix, JsValue[] args)
    {
        var tz = GeometryShapes.Number(args, 2, 0);
        GeometryMatrix.Translate(matrix.Elements, GeometryShapes.Number(args, 0, 0), GeometryShapes.Number(args, 1, 0), tz);
        if (tz != 0)
        {
            matrix.Is2D = false;
        }

        return matrix;
    }

    // https://drafts.fxtf.org/geometry/#dom-dommatrix-scaleself
    private static JsDomMatrix ScaleSelf(JsDomMatrix matrix, JsValue[] args)
    {
        var scaleX = GeometryShapes.Number(args, 0, 1);
        var scaleY = GeometryShapes.Optional(args, 1) ?? scaleX;
        Scale(
            matrix,
            scaleX,
            scaleY,
            GeometryShapes.Number(args, 2, 1),
            GeometryShapes.Number(args, 3, 0),
            GeometryShapes.Number(args, 4, 0),
            GeometryShapes.Number(args, 5, 0));
        return matrix;
    }

    private static void Scale(JsDomMatrix matrix, double scaleX, double scaleY, double scaleZ, double originX, double originY, double originZ)
    {
        GeometryMatrix.Translate(matrix.Elements, originX, originY, originZ);
        GeometryMatrix.Scale(matrix.Elements, scaleX, scaleY, scaleZ);
        GeometryMatrix.Translate(matrix.Elements, -originX, -originY, -originZ);
        if (scaleZ != 1 || originZ != 0)
        {
            matrix.Is2D = false;
        }
    }

    // https://drafts.fxtf.org/geometry/#dom-dommatrix-scale3dself
    private static JsDomMatrix Scale3dSelf(JsDomMatrix matrix, JsValue[] args)
    {
        var scale = GeometryShapes.Number(args, 0, 1);
        var originX = GeometryShapes.Number(args, 1, 0);
        var originY = GeometryShapes.Number(args, 2, 0);
        var originZ = GeometryShapes.Number(args, 3, 0);
        GeometryMatrix.Translate(matrix.Elements, originX, originY, originZ);
        GeometryMatrix.Scale(matrix.Elements, scale, scale, scale);
        GeometryMatrix.Translate(matrix.Elements, -originX, -originY, -originZ);
        if (scale != 1 || originZ != 0)
        {
            matrix.Is2D = false;
        }

        return matrix;
    }

    // https://drafts.fxtf.org/geometry/#dom-dommatrix-rotateself
    private static JsDomMatrix RotateSelf(JsDomMatrix matrix, JsValue[] args)
    {
        var rotX = GeometryShapes.Number(args, 0, 0);
        var rotY = GeometryShapes.Optional(args, 1);
        var rotZ = GeometryShapes.Optional(args, 2);
        if (rotY is null && rotZ is null)
        {
            rotZ = rotX;
            rotX = 0;
            rotY = 0;
        }

        var y = rotY ?? 0;
        var z = rotZ ?? 0;
        if (rotX != 0 || y != 0)
        {
            matrix.Is2D = false;
        }

        GeometryMatrix.RotateAboutAxis(matrix.Elements, 2, z);
        GeometryMatrix.RotateAboutAxis(matrix.Elements, 1, y);
        GeometryMatrix.RotateAboutAxis(matrix.Elements, 0, rotX);
        return matrix;
    }

    // https://drafts.fxtf.org/geometry/#dom-dommatrix-rotatefromvectorself
    private static JsDomMatrix RotateFromVectorSelf(JsDomMatrix matrix, JsValue[] args)
    {
        var x = GeometryShapes.Number(args, 0, 0);
        var y = GeometryShapes.Number(args, 1, 0);
        var degrees = x == 0 && y == 0 ? 0 : Math.Atan2(y, x) * 180 / Math.PI;
        GeometryMatrix.RotateAboutAxis(matrix.Elements, 2, degrees);
        return matrix;
    }

    // https://drafts.fxtf.org/geometry/#dom-dommatrix-rotateaxisangleself
    private static JsDomMatrix RotateAxisAngleSelf(JsDomMatrix matrix, JsValue[] args)
    {
        var x = GeometryShapes.Number(args, 0, 0);
        var y = GeometryShapes.Number(args, 1, 0);
        var z = GeometryShapes.Number(args, 2, 0);
        GeometryMatrix.Rotate3d(matrix.Elements, x, y, z, GeometryShapes.Number(args, 3, 0));
        if (x != 0 || y != 0)
        {
            matrix.Is2D = false;
        }

        return matrix;
    }

    // https://drafts.fxtf.org/geometry/#dom-dommatrix-skewxself
    private static JsDomMatrix SkewSelf(JsDomMatrix matrix, JsValue[] args, bool x)
    {
        var angle = GeometryShapes.Number(args, 0, 0);
        GeometryMatrix.Skew(matrix.Elements, x ? angle : 0, x ? 0 : angle);
        return matrix;
    }

    // https://drafts.fxtf.org/geometry/#dom-dommatrix-multiplyself
    private static JsDomMatrix MultiplySelf(JsDomMatrix matrix, JsValue[] args, bool pre)
    {
        var (other, otherIs2D) = GeometryConversion.MatrixInit(matrix.Owner.Realm, GeometryShapes.At(args, 0));
        if (pre)
        {
            GeometryMatrix.PreMultiply(matrix.Elements, other);
        }
        else
        {
            GeometryMatrix.PostMultiply(matrix.Elements, other);
        }

        if (!otherIs2D)
        {
            matrix.Is2D = false;
        }

        return matrix;
    }

    // https://drafts.fxtf.org/geometry/#dom-dommatrix-invertself
    private static JsDomMatrix InvertSelf(JsDomMatrix matrix)
    {
        if (!GeometryMatrix.Invert(matrix.Elements, matrix.Is2D))
        {
            Array.Fill(matrix.Elements, double.NaN);
            matrix.Is2D = false;
        }

        return matrix;
    }

    /// <summary>https://drafts.fxtf.org/geometry/#dommatrixreadonly-stringification-behavior</summary>
    private static JsString Stringify(JsDomMatrix matrix)
    {
        foreach (var element in matrix.Elements)
        {
            if (!double.IsFinite(element))
            {
                DomFailures.Refuse(matrix.Owner.Dom, "DOMMatrixReadOnly.toString", "InvalidStateError", "The matrix contains non-finite values.");
            }
        }

        var m = matrix.Elements;
        var builder = new System.Text.StringBuilder(matrix.Is2D ? "matrix(" : "matrix3d(");
        ReadOnlySpan<int> twoD = [GeometryMatrix.M11, GeometryMatrix.M12, GeometryMatrix.M21, GeometryMatrix.M22, GeometryMatrix.M41, GeometryMatrix.M42];
        var count = matrix.Is2D ? 6 : 16;
        for (var i = 0; i < count; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            builder.Append(TypeConverter.ToString(m[matrix.Is2D ? twoD[i] : i]));
        }

        return JsString.Create(builder.Append(')').ToString());
    }
}
