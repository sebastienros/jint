using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Browser.Geometry;

/// <summary>A <c>DOMPointReadOnly</c> or, when <see cref="Mutable"/>, a <c>DOMPoint</c>.</summary>
/// <remarks>https://drafts.fxtf.org/geometry/#DOMPoint</remarks>
internal sealed class JsDomPoint : ObjectInstance
{
    internal JsDomPoint(GeometryRealm owner, bool mutable, double x, double y, double z, double w) : base(owner.Engine)
    {
        Owner = owner;
        Prototype = mutable ? owner.PointPrototype : owner.PointReadOnlyPrototype;
        Mutable = mutable;
        X = x;
        Y = y;
        Z = z;
        W = w;
    }

    internal GeometryRealm Owner { get; }
    internal bool Mutable { get; }
    internal IGeometryCoordinates? Binding { get; set; }
    internal double X { get => Binding?.Get(0) ?? field; set { if (Binding is { } b) b.Set(0, value); else field = value; } }
    internal double Y { get => Binding?.Get(1) ?? field; set { if (Binding is { } b) b.Set(1, value); else field = value; } }
    internal double Z { get => Binding?.Get(2) ?? field; set { if (Binding is { } b) b.Set(2, value); else field = value; } }
    internal double W { get => Binding?.Get(3) ?? field; set { if (Binding is { } b) b.Set(3, value); else field = value; } }

    public override string ToString() => Mutable ? "[object DOMPoint]" : "[object DOMPointReadOnly]";
}

/// <summary>A <c>DOMRectReadOnly</c> or, when <see cref="Mutable"/>, a <c>DOMRect</c>.</summary>
/// <remarks>https://drafts.fxtf.org/geometry/#DOMRect</remarks>
internal sealed class JsDomRect : ObjectInstance
{
    internal JsDomRect(GeometryRealm owner, bool mutable, double x, double y, double width, double height) : base(owner.Engine)
    {
        Owner = owner;
        Prototype = mutable ? owner.RectPrototype : owner.RectReadOnlyPrototype;
        Mutable = mutable;
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    internal GeometryRealm Owner { get; }
    internal bool Mutable { get; }
    internal IGeometryCoordinates? Binding { get; init; }
    internal double X { get => Binding?.Get(0) ?? field; set { if (Binding is { } b) b.Set(0, value); else field = value; } }
    internal double Y { get => Binding?.Get(1) ?? field; set { if (Binding is { } b) b.Set(1, value); else field = value; } }
    internal double Width { get => Binding?.Get(2) ?? field; set { if (Binding is { } b) b.Set(2, value); else field = value; } }
    internal double Height { get => Binding?.Get(3) ?? field; set { if (Binding is { } b) b.Set(3, value); else field = value; } }

    // https://drafts.fxtf.org/geometry/#dom-domrectreadonly-top; Math.Min/Max propagate NaN as ECMAScript's do.
    internal double Top => Math.Min(Y, Y + Height);
    internal double Right => Math.Max(X, X + Width);
    internal double Bottom => Math.Max(Y, Y + Height);
    internal double Left => Math.Min(X, X + Width);

    public override string ToString() => Mutable ? "[object DOMRect]" : "[object DOMRectReadOnly]";
}

/// <summary>A <c>DOMQuad</c>: four <c>DOMPoint</c>s, each the same object on every read.</summary>
/// <remarks>https://drafts.fxtf.org/geometry/#DOMQuad</remarks>
internal sealed class JsDomQuad : ObjectInstance
{
    internal JsDomQuad(GeometryRealm owner, JsDomPoint p1, JsDomPoint p2, JsDomPoint p3, JsDomPoint p4) : base(owner.Engine)
    {
        Owner = owner;
        Prototype = owner.QuadPrototype;
        P1 = p1;
        P2 = p2;
        P3 = p3;
        P4 = p4;
    }

    internal GeometryRealm Owner { get; }
    internal JsDomPoint P1 { get; }
    internal JsDomPoint P2 { get; }
    internal JsDomPoint P3 { get; }
    internal JsDomPoint P4 { get; }

    public override string ToString() => "[object DOMQuad]";
}

/// <summary>A <c>DOMMatrixReadOnly</c> or, when <see cref="Mutable"/>, a <c>DOMMatrix</c>.</summary>
/// <remarks>https://drafts.fxtf.org/geometry/#DOMMatrix; the elements are laid out as <see cref="GeometryMatrix"/> says.</remarks>
internal sealed class JsDomMatrix : ObjectInstance
{
    internal JsDomMatrix(GeometryRealm owner, bool mutable, double[] elements, bool is2D) : base(owner.Engine)
    {
        Owner = owner;
        Prototype = mutable ? owner.MatrixPrototype : owner.MatrixReadOnlyPrototype;
        Mutable = mutable;
        Elements = elements;
        Is2D = is2D;
    }

    internal GeometryRealm Owner { get; }
    internal bool Mutable { get; }
    internal IGeometryMatrixBinding? Binding { get; init; }
    internal double[] Elements { get { Binding?.Refresh(field); return field; } }
    internal bool Is2D { get; set; }

    internal JsDomMatrix Commit()
    {
        Binding?.Commit(Elements, Is2D);
        return this;
    }

    public override string ToString() => Mutable ? "[object DOMMatrix]" : "[object DOMMatrixReadOnly]";
}

/// <summary>An optional live coordinate source for SVG DOMPoints and viewBox DOMRects.</summary>
/// <remarks>https://svgwg.org/svg2-draft/types.html#SVGDOMOverview</remarks>
internal interface IGeometryCoordinates
{
    double Get(int index);
    void Set(int index, double value);
}

/// <summary>An optional live SVGTransform matrix source; ordinary Geometry values have none.</summary>
/// <remarks>https://svgwg.org/svg2-draft/coords.html#InterfaceSVGTransform</remarks>
internal interface IGeometryMatrixBinding
{
    void Refresh(double[] elements);
    void CheckWritable();
    void Commit(double[] elements, bool is2D);
}

/// <summary>The receiver checks every geometry member starts with.</summary>
internal static class GeometryBrand
{
    internal static T Of<T>(JsValue thisObject, string interfaceName, string member, bool requireMutable = false) where T : ObjectInstance
    {
        if (thisObject is T instance && (!requireMutable || IsMutable(instance)))
        {
            return instance;
        }

        var message = "Failed to execute '" + member + "' on '" + interfaceName + "': Illegal invocation";
        if (thisObject is ObjectInstance other)
        {
            Throw.TypeError(other.Engine.Realm, message);
        }

        Throw.TypeErrorNoEngine(message);
        return null!;
    }

    private static bool IsMutable(ObjectInstance instance) => instance switch
    {
        JsDomPoint point => point.Mutable,
        JsDomRect rect => rect.Mutable,
        JsDomMatrix matrix => matrix.Mutable,
        _ => true,
    };
}
