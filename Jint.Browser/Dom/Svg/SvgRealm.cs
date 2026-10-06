using System.Runtime.CompilerServices;
using Jint.HtmlParser;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Browser.Dom.Svg;

/// <summary>Lazy SVG interfaces and weak, per-element live-attribute identities in their creation realm.</summary>
/// <remarks>https://svgwg.org/svg2-draft/types.html#SVGDOMOverview</remarks>
internal sealed class SvgRealm(DomRealm dom)
{
    internal static readonly string[] InterfaceNames =
    [
        "SVGAnimatedString", "SVGAnimatedBoolean", "SVGAnimatedNumber", "SVGAnimatedInteger", "SVGAnimatedEnumeration",
        "SVGAnimatedLength", "SVGAnimatedLengthList", "SVGAnimatedNumberList", "SVGAnimatedRect",
        "SVGAnimatedPreserveAspectRatio", "SVGAnimatedTransformList", "SVGAnimatedAngle",
        "SVGLength", "SVGNumber", "SVGAngle", "SVGPreserveAspectRatio", "SVGTransform",
        "SVGLengthList", "SVGNumberList", "SVGPointList", "SVGTransformList", "SVGStringList", "SVGUnitTypes",
    ];

    private readonly Dictionary<string, (ObjectInstance Prototype, HostInterfaceObject Interface)> _interfaces = new(StringComparer.Ordinal);
    private readonly ConditionalWeakTable<Element, ElementState> _elements = new();
    internal DomRealm Dom { get; } = dom;
    internal Realm Realm => Dom.OwningRealm;
    internal Engine Engine => Dom.Engine;
    internal Action Checkpoint { get; } = dom.Engine.Constraints.Check;

    internal ObjectInstance Prototype(string name)
    {
        if (_interfaces.TryGetValue(name, out var entry)) return entry.Prototype;
        using var scope = new RealmScope(Engine, Realm);
        var prototype = SvgValueShapes.For(name).Instantiate(Engine, Realm.Intrinsics.Object.PrototypeObject);
        JsObjectShape.SetHostState(prototype, Dom);
        var iface = new HostInterfaceObject(Engine, Realm, name, prototype, 0, constants: SvgValueShapes.Constants(name));
        prototype.DefineOwnPropertyUnchecked("constructor", new PropertyDescriptor(iface, PropertyFlag.NonEnumerable));
        _interfaces.Add(name, (prototype, iface));
        return prototype;
    }

    internal JsValue InterfaceObject(string name)
    {
        _ = Prototype(name);
        return _interfaces[name].Interface;
    }

    internal SvgAttribute Attribute(Element element, string name, SvgValueKind kind, string fallback = "",
        SvgLengthDirection direction = SvgLengthDirection.Horizontal, string? enumeration = null)
    {
        var state = _elements.GetValue(element, static _ => new ElementState());
        var key = (name, kind);
        if (!state.Attributes.TryGetValue(key, out var attribute))
        {
            attribute = new SvgAttribute(this, element, name, kind, fallback, direction, enumeration);
            state.Attributes.Add(key, attribute);
        }
        return attribute;
    }

    internal ElementState State(Element element) => _elements.GetValue(element, static _ => new ElementState());

    /// <summary>State is allocated only when script reaches an SVG member, never by the native parser.</summary>
    /// <remarks>https://svgwg.org/svg2-draft/struct.html#InterfaceSVGSVGElement</remarks>
    internal sealed class ElementState
    {
        internal readonly Dictionary<(string, SvgValueKind), SvgAttribute> Attributes = new();
        internal double CurrentScale = 1;
        internal Geometry.JsDomPoint? CurrentTranslate;
        internal bool AnimationsPaused;
        internal double CurrentTime;
    }
}

/// <summary>SVG DOM value grammars, kept distinct from their interface names.</summary>
/// <remarks>https://svgwg.org/svg2-draft/types.html#SVGDOMOverview</remarks>
internal enum SvgValueKind
{
    String, Boolean, Number, Integer, Enumeration, Length, LengthList, NumberList, Rect,
    PreserveAspectRatio, TransformList, PointList, Angle, StringList, Transform, Point,
}

/// <summary>The viewport dimension used by an SVG percentage length.</summary>
/// <remarks>https://svgwg.org/svg2-draft/coords.html#Units</remarks>
internal enum SvgLengthDirection { Horizontal, Vertical, Diagonal }
