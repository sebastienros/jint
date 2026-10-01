using Jint.Browser.Dom;
using Jint.HtmlParser.Css.Values;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Browser.Geometry;

/// <summary>
/// The WebIDL conversions Geometry's constructors and operations take: its four dictionaries, the
/// <c>(DOMString or sequence&lt;unrestricted double&gt;)</c> matrix initializer, and the typed arrays.
/// </summary>
/// <remarks>
/// A dictionary's members are read in lexicographic order, an inherited dictionary's before its own
/// (https://webidl.spec.whatwg.org/#es-dictionary), and each nested dictionary is converted as soon as it is
/// read, so a page's getters observe exactly the order an engine would.
/// </remarks>
internal static class GeometryConversion
{
    private static readonly string[] _matrix2DMembers = ["a", "b", "c", "d", "e", "f", "m11", "m12", "m21", "m22", "m41", "m42"];
    private static readonly string[] _matrix3DMembers = ["m13", "m14", "m23", "m24", "m31", "m32", "m33", "m34", "m43", "m44"];
    private static readonly int[] _matrix3DIndexes =
    [
        GeometryMatrix.M13, GeometryMatrix.M14, GeometryMatrix.M23, GeometryMatrix.M24, GeometryMatrix.M31,
        GeometryMatrix.M32, GeometryMatrix.M33, GeometryMatrix.M34, GeometryMatrix.M43, GeometryMatrix.M44,
    ];

    /// <summary><c>undefined</c> and <c>null</c> are the empty dictionary; any other non-object is a <c>TypeError</c>.</summary>
    private static ObjectInstance? Dictionary(Realm realm, JsValue value, string dictionary)
    {
        if (value.IsNullOrUndefined())
        {
            return null;
        }

        if (value is not ObjectInstance instance)
        {
            Throw.TypeError(realm, "The provided value is not of type '" + dictionary + "'.");
            return null;
        }

        return instance;
    }

    private static double? Member(ObjectInstance? dictionary, string name)
    {
        if (dictionary is null)
        {
            return null;
        }

        var value = dictionary.Get(name);
        return value.IsUndefined() ? null : TypeConverter.ToNumber(value);
    }

    /// <summary>https://drafts.fxtf.org/geometry/#dictdef-dompointinit: <c>w = 1, x = 0, y = 0, z = 0</c>.</summary>
    internal static (double X, double Y, double Z, double W) PointInit(Realm realm, JsValue value)
    {
        var dictionary = Dictionary(realm, value, "DOMPointInit");
        var w = Member(dictionary, "w") ?? 1;
        var x = Member(dictionary, "x") ?? 0;
        var y = Member(dictionary, "y") ?? 0;
        var z = Member(dictionary, "z") ?? 0;
        return (x, y, z, w);
    }

    /// <summary>https://drafts.fxtf.org/geometry/#dictdef-domrectinit: every member defaults to 0.</summary>
    internal static (double X, double Y, double Width, double Height) RectInit(Realm realm, JsValue value)
    {
        var dictionary = Dictionary(realm, value, "DOMRectInit");
        var height = Member(dictionary, "height") ?? 0;
        var width = Member(dictionary, "width") ?? 0;
        var x = Member(dictionary, "x") ?? 0;
        var y = Member(dictionary, "y") ?? 0;
        return (x, y, width, height);
    }

    /// <summary>https://drafts.fxtf.org/geometry/#dictdef-domquadinit: four <c>DOMPointInit</c>s.</summary>
    internal static (double X, double Y, double Z, double W)[] QuadInit(Realm realm, JsValue value)
    {
        var dictionary = Dictionary(realm, value, "DOMQuadInit");
        var points = new (double, double, double, double)[4];
        for (var i = 0; i < 4; i++)
        {
            points[i] = PointInit(realm, dictionary is null ? JsValue.Undefined : dictionary.Get("p" + (i + 1)));
        }

        return points;
    }

    /// <summary>
    /// Reads a <c>DOMMatrixInit</c> and runs
    /// <a href="https://drafts.fxtf.org/geometry/#matrix-validate-and-fixup">validate and fixup</a> over it.
    /// </summary>
    internal static (double[] Elements, bool Is2D) MatrixInit(Realm realm, JsValue value)
    {
        var dictionary = Dictionary(realm, value, "DOMMatrixInit");
        Span<double?> twoD = stackalloc double?[_matrix2DMembers.Length];
        for (var i = 0; i < _matrix2DMembers.Length; i++)
        {
            twoD[i] = Member(dictionary, _matrix2DMembers[i]);
        }

        bool? is2D = null;
        if (dictionary is not null && dictionary.Get("is2D") is { } flag && !flag.IsUndefined())
        {
            is2D = TypeConverter.ToBoolean(flag);
        }

        Span<double?> threeD = stackalloc double?[_matrix3DMembers.Length];
        for (var i = 0; i < _matrix3DMembers.Length; i++)
        {
            threeD[i] = Member(dictionary, _matrix3DMembers[i]);
        }

        // https://drafts.fxtf.org/geometry/#validate-and-fixup-2d: a..f alias m11, m12, m21, m22, m41, m42.
        var elements = GeometryMatrix.Identity();
        ReadOnlySpan<int> aliased = [GeometryMatrix.M11, GeometryMatrix.M12, GeometryMatrix.M21, GeometryMatrix.M22, GeometryMatrix.M41, GeometryMatrix.M42];
        for (var i = 0; i < 6; i++)
        {
            var alias = twoD[i];
            var element = twoD[6 + i];
            if (alias is { } a && element is { } m && !SameValueZero(a, m))
            {
                Throw.TypeError(realm, "The '" + _matrix2DMembers[i] + "' and '" + _matrix2DMembers[6 + i] + "' members of the DOMMatrixInit must agree.");
            }

            elements[aliased[i]] = element ?? alias ?? elements[aliased[i]];
        }

        var threeDimensional = false;
        for (var i = 0; i < threeD.Length; i++)
        {
            if (threeD[i] is not { } member)
            {
                continue;
            }

            elements[_matrix3DIndexes[i]] = member;
            var identity = _matrix3DIndexes[i] is GeometryMatrix.M33 or GeometryMatrix.M44 ? 1 : 0;
            threeDimensional |= member != identity;
        }

        if (is2D == true && threeDimensional)
        {
            Throw.TypeError(realm, "A DOMMatrixInit whose is2D is true cannot set a 3D component.");
        }

        return (elements, is2D ?? !threeDimensional);
    }

    private static bool SameValueZero(double left, double right) => left == right || (double.IsNaN(left) && double.IsNaN(right));

    /// <summary>
    /// https://drafts.fxtf.org/geometry/#dom-dommatrixreadonly-dommatrixreadonly — the
    /// <c>(DOMString or sequence&lt;unrestricted double&gt;)</c> union: an object with an <c>@@iterator</c> is the
    /// sequence, anything else the string (https://webidl.spec.whatwg.org/#es-union).
    /// </summary>
    internal static (double[] Elements, bool Is2D) MatrixConstructorInit(DomRealm realm, JsValue value, string interfaceName)
    {
        var owning = realm.OwningRealm;
        if (value is ObjectInstance instance && instance.GetMethod(Native.Symbol.GlobalSymbolRegistry.Iterator) is not null)
        {
            var iterator = value.GetIterator(owning);
            var values = new List<double>(16);
            while (iterator.TryIteratorStepValue(out var item))
            {
                values.Add(TypeConverter.ToNumber(item));
            }

            return FromSequence(owning, values, "Failed to construct '" + interfaceName + "'");
        }

        return Parse(realm, TypeConverter.ToString(value), interfaceName);
    }

    /// <summary>6 values are a 2D matrix's a–f, 16 are m11–m44; any other count is a <c>TypeError</c>.</summary>
    internal static (double[] Elements, bool Is2D) FromSequence(Realm realm, IReadOnlyList<double> values, string context)
    {
        var elements = GeometryMatrix.Identity();
        if (values.Count == 6)
        {
            elements[GeometryMatrix.M11] = values[0];
            elements[GeometryMatrix.M12] = values[1];
            elements[GeometryMatrix.M21] = values[2];
            elements[GeometryMatrix.M22] = values[3];
            elements[GeometryMatrix.M41] = values[4];
            elements[GeometryMatrix.M42] = values[5];
            return (elements, true);
        }

        if (values.Count == 16)
        {
            for (var i = 0; i < 16; i++)
            {
                elements[i] = values[i];
            }

            return (elements, false);
        }

        Throw.TypeError(realm, context + ": The sequence must contain 6 elements for a 2D matrix or 16 elements for a 3D matrix.");
        return default;
    }

    /// <summary>
    /// https://drafts.fxtf.org/geometry/#parse-a-string-into-an-abstract-matrix, over the native
    /// <see cref="CssTransformList"/> reader.
    /// </summary>
    internal static (double[] Elements, bool Is2D) Parse(DomRealm realm, string transformList, string member)
    {
        if (transformList.Length == 0)
        {
            return (GeometryMatrix.Identity(), true);
        }

        if (!CssTransformList.TryParse(transformList, new CssValueWork(realm.CancellationToken), out var functions))
        {
            DomFailures.Refuse(realm, member, "SyntaxError", "Failed to parse '" + transformList + "'.");
        }

        var elements = GeometryMatrix.Identity();
        var is2D = true;
        foreach (var function in functions)
        {
            is2D &= function.Is2D;
            Apply(elements, function);
        }

        return (elements, is2D);
    }

    /// <summary>https://drafts.csswg.org/css-transforms-2/#mathematical-description, post-multiplied in list order.</summary>
    private static void Apply(double[] m, CssTransformFunction function)
    {
        var args = function.Arguments;
        switch (function.Kind)
        {
            case CssTransformFunctionKind.Matrix:
                var matrix = GeometryMatrix.Identity();
                matrix[GeometryMatrix.M11] = args[0];
                matrix[GeometryMatrix.M12] = args[1];
                matrix[GeometryMatrix.M21] = args[2];
                matrix[GeometryMatrix.M22] = args[3];
                matrix[GeometryMatrix.M41] = args[4];
                matrix[GeometryMatrix.M42] = args[5];
                GeometryMatrix.PostMultiply(m, matrix);
                break;
            case CssTransformFunctionKind.Matrix3d:
                GeometryMatrix.PostMultiply(m, (double[]) args.Clone());
                break;
            case CssTransformFunctionKind.Translate:
                GeometryMatrix.Translate(m, args[0], args[1], 0);
                break;
            case CssTransformFunctionKind.Translate3d:
                GeometryMatrix.Translate(m, args[0], args[1], args[2]);
                break;
            case CssTransformFunctionKind.TranslateX:
                GeometryMatrix.Translate(m, args[0], 0, 0);
                break;
            case CssTransformFunctionKind.TranslateY:
                GeometryMatrix.Translate(m, 0, args[0], 0);
                break;
            case CssTransformFunctionKind.TranslateZ:
                GeometryMatrix.Translate(m, 0, 0, args[0]);
                break;
            case CssTransformFunctionKind.Scale:
                GeometryMatrix.Scale(m, args[0], args[1], 1);
                break;
            case CssTransformFunctionKind.Scale3d:
                GeometryMatrix.Scale(m, args[0], args[1], args[2]);
                break;
            case CssTransformFunctionKind.ScaleX:
                GeometryMatrix.Scale(m, args[0], 1, 1);
                break;
            case CssTransformFunctionKind.ScaleY:
                GeometryMatrix.Scale(m, 1, args[0], 1);
                break;
            case CssTransformFunctionKind.ScaleZ:
                GeometryMatrix.Scale(m, 1, 1, args[0]);
                break;
            case CssTransformFunctionKind.Rotate:
            case CssTransformFunctionKind.RotateZ:
                GeometryMatrix.RotateAboutAxis(m, 2, args[0]);
                break;
            case CssTransformFunctionKind.RotateX:
                GeometryMatrix.RotateAboutAxis(m, 0, args[0]);
                break;
            case CssTransformFunctionKind.RotateY:
                GeometryMatrix.RotateAboutAxis(m, 1, args[0]);
                break;
            case CssTransformFunctionKind.Rotate3d:
                GeometryMatrix.Rotate3d(m, args[0], args[1], args[2], args[3]);
                break;
            case CssTransformFunctionKind.Skew:
                GeometryMatrix.Skew(m, args[0], args[1]);
                break;
            case CssTransformFunctionKind.SkewX:
                GeometryMatrix.Skew(m, args[0], 0);
                break;
            case CssTransformFunctionKind.SkewY:
                GeometryMatrix.Skew(m, 0, args[0]);
                break;
            case CssTransformFunctionKind.Perspective:
                GeometryMatrix.Perspective(m, args[0]);
                break;
        }
    }

    /// <summary>The values of a <c>Float32Array</c> or <c>Float64Array</c>, refusing anything else.</summary>
    internal static List<double> FloatArray(Realm realm, JsValue value, bool float32, string context)
    {
        var expected = float32 ? "Float32Array" : "Float64Array";
        if (value is not JsTypedArray array
            || array._arrayElementType != (float32 ? Native.TypedArray.TypedArrayElementType.Float32 : Native.TypedArray.TypedArrayElementType.Float64))
        {
            Throw.TypeError(realm, context + ": parameter 1 is not of type '" + expected + "'.");
            return null!;
        }

        var length = array.Length;
        var values = new List<double>((int) Math.Min(length, 16));
        for (uint i = 0; i < length; i++)
        {
            values.Add(TypeConverter.ToNumber(array[i]));
        }

        return values;
    }
}
