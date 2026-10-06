using Jint.Native;
using Jint.Runtime;

namespace Jint.Browser.Geometry;

/// <summary>
/// The engine-independent prototype shapes of <c>DOMPointReadOnly</c>, <c>DOMPoint</c>,
/// <c>DOMRectReadOnly</c>, <c>DOMRect</c> and <c>DOMQuad</c>, and the argument readers every geometry member shares.
/// </summary>
/// <remarks>
/// A mutable interface redeclares its parent's attributes as writable
/// (https://drafts.fxtf.org/geometry/#DOMPoint), so its prototype has its own accessor pair for each and
/// brands the receiver as the mutable one, while the read-only prototype's getter accepts either.
/// </remarks>
internal static class GeometryShapes
{
    private static readonly JsObjectLayout _pointJson = new JsObjectLayout.Builder().Add("x").Add("y").Add("z").Add("w").Build();

    private static readonly JsObjectLayout _rectJson = new JsObjectLayout.Builder()
        .Add("x").Add("y").Add("width").Add("height").Add("top").Add("right").Add("bottom").Add("left").Build();

    private static readonly JsObjectLayout _quadJson = new JsObjectLayout.Builder().Add("p1").Add("p2").Add("p3").Add("p4").Build();

    internal static readonly JsObjectShape PointReadOnly = BuildPointReadOnly();
    internal static readonly JsObjectShape Point = BuildPoint();
    internal static readonly JsObjectShape RectReadOnly = BuildRectReadOnly();
    internal static readonly JsObjectShape Rect = BuildRect();
    internal static readonly JsObjectShape Quad = BuildQuad();

    internal static JsValue At(JsValue[] args, int index) => index < args.Length ? args[index] : JsValue.Undefined;

    /// <summary>An <c>optional unrestricted double</c> with a default: <c>undefined</c> is the default.</summary>
    internal static double Number(JsValue[] args, int index, double fallback)
        => index < args.Length && !args[index].IsUndefined() ? TypeConverter.ToNumber(args[index]) : fallback;

    /// <summary>An <c>optional unrestricted double</c> without one: <c>undefined</c> is "not passed".</summary>
    internal static double? Optional(JsValue[] args, int index)
        => index < args.Length && !args[index].IsUndefined() ? TypeConverter.ToNumber(args[index]) : null;

    /// <summary>The <c>unrestricted double</c> an attribute setter receives.</summary>
    internal static double Assigned(JsValue[] args) => TypeConverter.ToNumber(At(args, 0));

    private static JsDomPoint ReadPoint(JsValue t, string member) => GeometryBrand.Of<JsDomPoint>(t, "DOMPointReadOnly", member);

    private static JsDomPoint WritePoint(JsValue t, string member) => GeometryBrand.Of<JsDomPoint>(t, "DOMPoint", member, requireMutable: true);

    private static JsDomRect ReadRect(JsValue t, string member) => GeometryBrand.Of<JsDomRect>(t, "DOMRectReadOnly", member);

    private static JsDomRect WriteRect(JsValue t, string member) => GeometryBrand.Of<JsDomRect>(t, "DOMRect", member, requireMutable: true);

    private static JsDomQuad ReadQuad(JsValue t, string member) => GeometryBrand.Of<JsDomQuad>(t, "DOMQuad", member);

    /// <summary>https://drafts.fxtf.org/geometry/#dompointreadonly</summary>
    private static JsObjectShape BuildPointReadOnly() => new JsObjectShape.Builder()
        .PerRealmSlot("constructor")
        .ToStringTag("DOMPointReadOnly")
        .Accessor("x", static (t, _) => JsNumber.Create(ReadPoint(t, "x").X))
        .Accessor("y", static (t, _) => JsNumber.Create(ReadPoint(t, "y").Y))
        .Accessor("z", static (t, _) => JsNumber.Create(ReadPoint(t, "z").Z))
        .Accessor("w", static (t, _) => JsNumber.Create(ReadPoint(t, "w").W))
        .Method("matrixTransform", static (t, args) =>
        {
            // https://drafts.fxtf.org/geometry/#dom-dompointreadonly-matrixtransform
            var point = ReadPoint(t, "matrixTransform");
            var (elements, _) = GeometryConversion.MatrixInit(point.Owner.Realm, At(args, 0));
            return Transform(point.Owner, elements, point.X, point.Y, point.Z, point.W);
        })
        .Method("toJSON", static (t, _) =>
        {
            var point = ReadPoint(t, "toJSON");
            return JsObject.Create(point.Engine, _pointJson,
                [JsNumber.Create(point.X), JsNumber.Create(point.Y), JsNumber.Create(point.Z), JsNumber.Create(point.W)]);
        })
        .Build();

    /// <summary>https://drafts.fxtf.org/geometry/#dompoint</summary>
    private static JsObjectShape BuildPoint() => new JsObjectShape.Builder()
        .PerRealmSlot("constructor")
        .ToStringTag("DOMPoint")
        .Accessor("x", static (t, _) => JsNumber.Create(WritePoint(t, "x").X), static (t, args) =>
        {
            WritePoint(t, "x").X = Assigned(args);
            return JsValue.Undefined;
        })
        .Accessor("y", static (t, _) => JsNumber.Create(WritePoint(t, "y").Y), static (t, args) =>
        {
            WritePoint(t, "y").Y = Assigned(args);
            return JsValue.Undefined;
        })
        .Accessor("z", static (t, _) => JsNumber.Create(WritePoint(t, "z").Z), static (t, args) =>
        {
            WritePoint(t, "z").Z = Assigned(args);
            return JsValue.Undefined;
        })
        .Accessor("w", static (t, _) => JsNumber.Create(WritePoint(t, "w").W), static (t, args) =>
        {
            WritePoint(t, "w").W = Assigned(args);
            return JsValue.Undefined;
        })
        .Build();

    /// <summary>
    /// https://drafts.fxtf.org/geometry/#transform-a-point-with-a-matrix — the point is a column vector
    /// pre-multiplied by the matrix, and the answer is always a new <c>DOMPoint</c>.
    /// </summary>
    internal static JsDomPoint Transform(GeometryRealm owner, double[] m, double x, double y, double z, double w)
        => owner.CreatePoint(
            true,
            m[GeometryMatrix.M11] * x + m[GeometryMatrix.M21] * y + m[GeometryMatrix.M31] * z + m[GeometryMatrix.M41] * w,
            m[GeometryMatrix.M12] * x + m[GeometryMatrix.M22] * y + m[GeometryMatrix.M32] * z + m[GeometryMatrix.M42] * w,
            m[GeometryMatrix.M13] * x + m[GeometryMatrix.M23] * y + m[GeometryMatrix.M33] * z + m[GeometryMatrix.M43] * w,
            m[GeometryMatrix.M14] * x + m[GeometryMatrix.M24] * y + m[GeometryMatrix.M34] * z + m[GeometryMatrix.M44] * w);

    /// <summary>https://drafts.fxtf.org/geometry/#domrectreadonly</summary>
    private static JsObjectShape BuildRectReadOnly() => new JsObjectShape.Builder()
        .PerRealmSlot("constructor")
        .ToStringTag("DOMRectReadOnly")
        .Accessor("x", static (t, _) => JsNumber.Create(ReadRect(t, "x").X))
        .Accessor("y", static (t, _) => JsNumber.Create(ReadRect(t, "y").Y))
        .Accessor("width", static (t, _) => JsNumber.Create(ReadRect(t, "width").Width))
        .Accessor("height", static (t, _) => JsNumber.Create(ReadRect(t, "height").Height))
        .Accessor("top", static (t, _) => JsNumber.Create(ReadRect(t, "top").Top))
        .Accessor("right", static (t, _) => JsNumber.Create(ReadRect(t, "right").Right))
        .Accessor("bottom", static (t, _) => JsNumber.Create(ReadRect(t, "bottom").Bottom))
        .Accessor("left", static (t, _) => JsNumber.Create(ReadRect(t, "left").Left))
        .Method("toJSON", static (t, _) =>
        {
            var rect = ReadRect(t, "toJSON");
            return JsObject.Create(rect.Engine, _rectJson,
            [
                JsNumber.Create(rect.X), JsNumber.Create(rect.Y), JsNumber.Create(rect.Width), JsNumber.Create(rect.Height),
                JsNumber.Create(rect.Top), JsNumber.Create(rect.Right), JsNumber.Create(rect.Bottom), JsNumber.Create(rect.Left),
            ]);
        })
        .Build();

    /// <summary>https://drafts.fxtf.org/geometry/#domrect</summary>
    private static JsObjectShape BuildRect() => new JsObjectShape.Builder()
        .PerRealmSlot("constructor")
        .ToStringTag("DOMRect")
        .Accessor("x", static (t, _) => JsNumber.Create(WriteRect(t, "x").X), static (t, args) =>
        {
            WriteRect(t, "x").X = Assigned(args);
            return JsValue.Undefined;
        })
        .Accessor("y", static (t, _) => JsNumber.Create(WriteRect(t, "y").Y), static (t, args) =>
        {
            WriteRect(t, "y").Y = Assigned(args);
            return JsValue.Undefined;
        })
        .Accessor("width", static (t, _) => JsNumber.Create(WriteRect(t, "width").Width), static (t, args) =>
        {
            WriteRect(t, "width").Width = Assigned(args);
            return JsValue.Undefined;
        })
        .Accessor("height", static (t, _) => JsNumber.Create(WriteRect(t, "height").Height), static (t, args) =>
        {
            WriteRect(t, "height").Height = Assigned(args);
            return JsValue.Undefined;
        })
        .Build();

    /// <summary>https://drafts.fxtf.org/geometry/#domquad</summary>
    private static JsObjectShape BuildQuad() => new JsObjectShape.Builder()
        .PerRealmSlot("constructor")
        .ToStringTag("DOMQuad")
        .Accessor("p1", static (t, _) => ReadQuad(t, "p1").P1)
        .Accessor("p2", static (t, _) => ReadQuad(t, "p2").P2)
        .Accessor("p3", static (t, _) => ReadQuad(t, "p3").P3)
        .Accessor("p4", static (t, _) => ReadQuad(t, "p4").P4)
        .Method("getBounds", static (t, _) =>
        {
            // https://drafts.fxtf.org/geometry/#dom-domquad-getbounds — Math.Min/Max propagate NaN.
            var quad = ReadQuad(t, "getBounds");
            var left = Math.Min(Math.Min(quad.P1.X, quad.P2.X), Math.Min(quad.P3.X, quad.P4.X));
            var top = Math.Min(Math.Min(quad.P1.Y, quad.P2.Y), Math.Min(quad.P3.Y, quad.P4.Y));
            var right = Math.Max(Math.Max(quad.P1.X, quad.P2.X), Math.Max(quad.P3.X, quad.P4.X));
            var bottom = Math.Max(Math.Max(quad.P1.Y, quad.P2.Y), Math.Max(quad.P3.Y, quad.P4.Y));
            return quad.Owner.CreateRect(true, left, top, right - left, bottom - top);
        })
        .Method("toJSON", static (t, _) =>
        {
            var quad = ReadQuad(t, "toJSON");
            return JsObject.Create(quad.Engine, _quadJson, [quad.P1, quad.P2, quad.P3, quad.P4]);
        })
        .Build();
}
