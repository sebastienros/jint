using Jint.Browser.Dom;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Browser.Geometry;

/// <summary>
/// One realm's Geometry Interfaces: the seven interface objects and their prototypes, built together the
/// first time a page reaches any of them, and the factories every geometry value is created through.
/// </summary>
/// <remarks>
/// <para>
/// https://drafts.fxtf.org/geometry/. One per <see cref="DomRealm"/>, so a frame's <c>DOMRect</c> is its
/// own, and so the bindings a <c>DomTestFixture</c> installs have them without a page behind them.
/// </para>
/// <para>
/// The three read-only/mutable pairs inherit as the IDL says — <c>DOMPoint.prototype</c>'s prototype is
/// <c>DOMPointReadOnly.prototype</c> and <c>DOMPoint</c>'s is <c>DOMPointReadOnly</c> — so each pair is
/// built in one step. A value is created in the realm of the object whose member created it, which is what
/// <see cref="JsDomPoint.Owner"/> and its siblings carry.
/// </para>
/// </remarks>
internal sealed class GeometryRealm
{
    /// <summary>The globals <see cref="DomBindings.InstallOn"/> installs, lazily, in declaration order.</summary>
    internal static readonly string[] InterfaceNames =
    [
        "DOMPointReadOnly", "DOMPoint", "DOMRectReadOnly", "DOMRect", "DOMQuad", "DOMMatrixReadOnly", "DOMMatrix",
    ];

    /// <summary>https://drafts.fxtf.org/geometry/#dommatrix — <c>[LegacyWindowAlias=WebKitCSSMatrix]</c>.</summary>
    internal const string MatrixAlias = "WebKitCSSMatrix";
    internal const string SvgMatrixAlias = "SVGMatrix";

    private (ObjectInstance Prototype, HostInterfaceObject Interface) _pointReadOnly;
    private (ObjectInstance Prototype, HostInterfaceObject Interface) _point;
    private (ObjectInstance Prototype, HostInterfaceObject Interface) _rectReadOnly;
    private (ObjectInstance Prototype, HostInterfaceObject Interface) _rect;
    private (ObjectInstance Prototype, HostInterfaceObject Interface) _quad;
    private (ObjectInstance Prototype, HostInterfaceObject Interface) _matrixReadOnly;
    private (ObjectInstance Prototype, HostInterfaceObject Interface) _matrix;

    internal GeometryRealm(DomRealm dom)
    {
        Dom = dom;
    }

    internal DomRealm Dom { get; }

    internal Engine Engine => Dom.Engine;

    internal Realm Realm => Dom.OwningRealm;

    internal ObjectInstance PointReadOnlyPrototype => Points().ReadOnly.Prototype;

    internal ObjectInstance PointPrototype => Points().Mutable.Prototype;

    internal ObjectInstance RectReadOnlyPrototype => Rects().ReadOnly.Prototype;

    internal ObjectInstance RectPrototype => Rects().Mutable.Prototype;

    internal ObjectInstance QuadPrototype => Quads().Prototype;

    internal ObjectInstance MatrixReadOnlyPrototype => Matrices().ReadOnly.Prototype;

    internal ObjectInstance MatrixPrototype => Matrices().Mutable.Prototype;

    /// <summary>The interface object a global named <paramref name="name"/> answers.</summary>
    internal JsValue InterfaceObject(string name) => name switch
    {
        "DOMPointReadOnly" => Points().ReadOnly.Interface,
        "DOMPoint" => Points().Mutable.Interface,
        "DOMRectReadOnly" => Rects().ReadOnly.Interface,
        "DOMRect" => Rects().Mutable.Interface,
        "DOMQuad" => Quads().Interface,
        "DOMMatrixReadOnly" => Matrices().ReadOnly.Interface,
        "DOMMatrix" or MatrixAlias or SvgMatrixAlias => Matrices().Mutable.Interface,
        "SVGPoint" => Points().Mutable.Interface,
        "SVGRect" => Rects().Mutable.Interface,
        _ => JsValue.Undefined,
    };

    internal JsDomPoint CreatePoint(bool mutable, double x, double y, double z, double w) => new(this, mutable, x, y, z, w);

    internal JsDomPoint CreatePoint(bool mutable, (double X, double Y, double Z, double W) point)
        => new(this, mutable, point.X, point.Y, point.Z, point.W);

    internal JsDomRect CreateRect(bool mutable, double x, double y, double width, double height)
        => new(this, mutable, x, y, width, height);

    internal JsDomQuad CreateQuad((double X, double Y, double Z, double W)[] points)
        => new(this, CreatePoint(true, points[0]), CreatePoint(true, points[1]), CreatePoint(true, points[2]), CreatePoint(true, points[3]));

    internal JsDomMatrix CreateMatrix(bool mutable, double[] elements, bool is2D) => new(this, mutable, elements, is2D);

    private ((ObjectInstance Prototype, HostInterfaceObject Interface) ReadOnly, (ObjectInstance Prototype, HostInterfaceObject Interface) Mutable) Points()
    {
        if (_point.Prototype is null)
        {
            _pointReadOnly = Build(GeometryShapes.PointReadOnly, "DOMPointReadOnly", args => ConstructPoint(false, args), parent: default);
            _point = Build(GeometryShapes.Point, "DOMPoint", args => ConstructPoint(true, args), _pointReadOnly);
            Static(_pointReadOnly.Interface, "fromPoint", (_, args) => CreatePoint(false, GeometryConversion.PointInit(Realm, GeometryShapes.At(args, 0))), 0);
            Static(_point.Interface, "fromPoint", (_, args) => CreatePoint(true, GeometryConversion.PointInit(Realm, GeometryShapes.At(args, 0))), 0);
        }

        return (_pointReadOnly, _point);
    }

    private ((ObjectInstance Prototype, HostInterfaceObject Interface) ReadOnly, (ObjectInstance Prototype, HostInterfaceObject Interface) Mutable) Rects()
    {
        if (_rect.Prototype is null)
        {
            _rectReadOnly = Build(GeometryShapes.RectReadOnly, "DOMRectReadOnly", args => ConstructRect(false, args), parent: default);
            _rect = Build(GeometryShapes.Rect, "DOMRect", args => ConstructRect(true, args), _rectReadOnly);
            Static(_rectReadOnly.Interface, "fromRect", (_, args) => FromRect(false, args), 0);
            Static(_rect.Interface, "fromRect", (_, args) => FromRect(true, args), 0);
        }

        return (_rectReadOnly, _rect);
    }

    private (ObjectInstance Prototype, HostInterfaceObject Interface) Quads()
    {
        if (_quad.Prototype is null)
        {
            _quad = Build(GeometryShapes.Quad, "DOMQuad", ConstructQuad, parent: default);

            // https://drafts.fxtf.org/geometry/#dom-domquad-fromrect
            Static(_quad.Interface, "fromRect", (_, args) =>
            {
                var (x, y, width, height) = GeometryConversion.RectInit(Realm, GeometryShapes.At(args, 0));
                return CreateQuad([(x, y, 0, 1), (x + width, y, 0, 1), (x + width, y + height, 0, 1), (x, y + height, 0, 1)]);
            }, 0);
            Static(_quad.Interface, "fromQuad", (_, args) => CreateQuad(GeometryConversion.QuadInit(Realm, GeometryShapes.At(args, 0))), 0);
        }

        return _quad;
    }

    private ((ObjectInstance Prototype, HostInterfaceObject Interface) ReadOnly, (ObjectInstance Prototype, HostInterfaceObject Interface) Mutable) Matrices()
    {
        if (_matrix.Prototype is null)
        {
            _matrixReadOnly = Build(GeometryMatrixShapes.MatrixReadOnly, "DOMMatrixReadOnly", args => ConstructMatrix(false, args), parent: default);
            _matrix = Build(GeometryMatrixShapes.Matrix, "DOMMatrix", args => ConstructMatrix(true, args), _matrixReadOnly);
            foreach (var (iface, mutable) in new[] { (_matrixReadOnly.Interface, false), (_matrix.Interface, true) })
            {
                var name = mutable ? "DOMMatrix" : "DOMMatrixReadOnly";
                Static(iface, "fromMatrix", (_, args) =>
                {
                    var (elements, is2D) = GeometryConversion.MatrixInit(Realm, GeometryShapes.At(args, 0));
                    return CreateMatrix(mutable, elements, is2D);
                }, 0);
                Static(iface, "fromFloat32Array", (_, args) => FromFloatArray(mutable, args, true, name), 1);
                Static(iface, "fromFloat64Array", (_, args) => FromFloatArray(mutable, args, false, name), 1);
            }
        }

        return (_matrixReadOnly, _matrix);
    }

    private (ObjectInstance Prototype, HostInterfaceObject Interface) Build(
        JsObjectShape shape,
        string name,
        Func<JsValue[], ObjectInstance> construct,
        (ObjectInstance? Prototype, HostInterfaceObject? Interface) parent)
    {
        using var scope = new RealmScope(Engine, Realm);
        var prototype = shape.Instantiate(Engine, parent.Prototype ?? Realm.Intrinsics.Object.PrototypeObject);
        JsObjectShape.SetHostState(prototype, Dom);
        var iface = new HostInterfaceObject(Engine, Realm, name, prototype, 0, construct, parent.Interface);
        prototype.DefineOwnPropertyUnchecked("constructor", new PropertyDescriptor(iface, PropertyFlag.NonEnumerable));
        return (prototype, iface);
    }

    /// <summary>https://webidl.spec.whatwg.org/#es-operations — a static operation is an own data property of the interface object.</summary>
    private void Static(HostInterfaceObject iface, string name, Func<JsValue, JsValue[], JsValue> body, int length)
    {
        iface.DefineOwnPropertyUnchecked(
            name,
            new PropertyDescriptor(
                new ClrFunction(Engine, Realm, name, body, length, PropertyFlag.Configurable),
                PropertyFlag.ConfigurableEnumerableWritable));
    }

    // https://drafts.fxtf.org/geometry/#dom-dompointreadonly-dompointreadonly
    private JsDomPoint ConstructPoint(bool mutable, JsValue[] args)
        => CreatePoint(
            mutable,
            GeometryShapes.Number(args, 0, 0),
            GeometryShapes.Number(args, 1, 0),
            GeometryShapes.Number(args, 2, 0),
            GeometryShapes.Number(args, 3, 1));

    // https://drafts.fxtf.org/geometry/#dom-domrectreadonly-domrectreadonly
    private JsDomRect ConstructRect(bool mutable, JsValue[] args)
        => CreateRect(
            mutable,
            GeometryShapes.Number(args, 0, 0),
            GeometryShapes.Number(args, 1, 0),
            GeometryShapes.Number(args, 2, 0),
            GeometryShapes.Number(args, 3, 0));

    private JsDomRect FromRect(bool mutable, JsValue[] args)
    {
        var (x, y, width, height) = GeometryConversion.RectInit(Realm, GeometryShapes.At(args, 0));
        return CreateRect(mutable, x, y, width, height);
    }

    // https://drafts.fxtf.org/geometry/#dom-domquad-domquad — each point converted in argument order.
    private JsDomQuad ConstructQuad(JsValue[] args)
    {
        var points = new (double, double, double, double)[4];
        for (var i = 0; i < 4; i++)
        {
            points[i] = GeometryConversion.PointInit(Realm, GeometryShapes.At(args, i));
        }

        return CreateQuad(points);
    }

    // https://drafts.fxtf.org/geometry/#dom-dommatrixreadonly-dommatrixreadonly
    private JsDomMatrix ConstructMatrix(bool mutable, JsValue[] args)
    {
        var init = GeometryShapes.At(args, 0);
        if (init.IsUndefined())
        {
            return CreateMatrix(mutable, GeometryMatrix.Identity(), true);
        }

        var name = mutable ? "DOMMatrix" : "DOMMatrixReadOnly";
        var (elements, is2D) = GeometryConversion.MatrixConstructorInit(Dom, init, name);
        return CreateMatrix(mutable, elements, is2D);
    }

    private JsDomMatrix FromFloatArray(bool mutable, JsValue[] args, bool float32, string interfaceName)
    {
        var member = float32 ? "fromFloat32Array" : "fromFloat64Array";
        var context = "Failed to execute '" + member + "' on '" + interfaceName + "'";
        var values = GeometryConversion.FloatArray(Realm, GeometryShapes.At(args, 0), float32, context);
        var (elements, is2D) = GeometryConversion.FromSequence(Realm, values, context);
        return CreateMatrix(mutable, elements, is2D);
    }
}
