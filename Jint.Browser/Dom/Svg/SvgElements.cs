using Jint.Browser.Geometry;
using Jint.HtmlParser;
using Jint.Native;
using Jint.Runtime;

namespace Jint.Browser.Dom.Svg;

/// <summary>The SVG element contract's live attributes, factories and non-rendering operations.</summary>
/// <remarks>
/// https://svgwg.org/svg2-draft/types.html#InterfaceSVGElement,
/// https://svgwg.org/svg2-draft/struct.html#InterfaceSVGSVGElement and
/// https://svgwg.org/svg2-draft/text.html#InterfaceSVGTextContentElement.
/// Text metrics are zero without shaping or layout; character counts use Unicode scalar values.
/// </remarks>
internal static class SvgElements
{
    internal static JsValue Animated(DomRealm realm, Element element, string name, SvgValueKind kind, string fallback = "",
        SvgLengthDirection direction = SvgLengthDirection.Horizontal, string? enumeration = null)
        => realm.Svg.Attribute(element, name, kind, fallback, direction, enumeration).Animated;

    internal static JsValue List(DomRealm realm, Element element, string name, SvgValueKind kind, bool readOnly)
        => realm.Svg.Attribute(element, name, kind).List(readOnly);

    internal static Element? OwnerSvg(Element element)
    {
        for (var parent = element.ParentNode as Element; parent is not null; parent = parent.ParentNode as Element)
            if (parent.NamespaceUri == Namespaces.Svg && parent.LocalName == "svg") return parent;
        return null;
    }

    internal static JsValue SetText(DomRealm realm, Element element, string name, JsValue[] args)
    {
        var text = TypeConverter.ToString(args.At(0));
        DomFailures.PrepareMutation(realm, element);
        using var mutation = realm.MutateLayout();
        element.SetAttribute(name, text);
        DomFailures.CompleteMutation(realm, element);
        return JsValue.Undefined;
    }

    internal static JsValue Current(DomRealm realm, Element element, string name)
    {
        var state = realm.Svg.State(element);
        return name == "currentScale" ? JsNumber.Create(state.CurrentScale)
            : state.CurrentTranslate ??= realm.Geometry.CreatePoint(true, 0, 0, 0, 1);
    }

    internal static JsValue SetCurrent(DomRealm realm, Element element, string name, JsValue[] args)
    {
        var value = SvgValues.Number(realm.Svg, args.At(0));
        if (value <= 0) Throw.TypeError(realm.OwningRealm, "currentScale must be positive.");
        realm.Svg.State(element).CurrentScale = value;
        return JsValue.Undefined;
    }

    internal static JsValue Invoke(DomRealm realm, Element element, string method, JsValue[] args)
    {
        var owner = realm.Svg;
        switch (method)
        {
            case "getBBox":
                return SvgGeometry.Bounds(owner, element);
            case "getCTM":
            case "getScreenCTM":
                return SvgGeometry.Ctm(owner, element);
            case "getTotalLength":
                return JsNumber.Create(SvgGeometry.Length(owner, element));
            case "getPointAtLength":
                SvgValues.Require(owner, args, 1);
                return SvgGeometry.Point(owner, element, SvgValues.Number(owner, args[0]));
            case "isPointInFill":
            case "isPointInStroke":
                _ = GeometryConversion.PointInit(owner.Realm, args.At(0));
                return JsBoolean.False;
            case "createSVGPoint":
                return realm.Geometry.CreatePoint(true, 0, 0, 0, 1);
            case "createSVGRect":
                return realm.Geometry.CreateRect(true, 0, 0, 0, 0);
            case "createSVGMatrix":
                return realm.Geometry.CreateMatrix(true, GeometryMatrix.Identity(), true);
            case "createSVGLength":
                return new SvgValueCell(owner, SvgValueKind.Length, new SvgValueData(Unit: 1)).Wrap(false);
            case "createSVGNumber":
                return new SvgValueCell(owner, SvgValueKind.Number, default).Wrap(false);
            case "createSVGAngle":
                return new SvgValueCell(owner, SvgValueKind.Angle, new SvgValueData(Unit: 1)).Wrap(false);
            case "createSVGTransform":
                return new SvgValueCell(owner, SvgValueKind.Transform, default).Wrap(false);
            case "createSVGTransformFromMatrix":
                SvgValues.Require(owner, args, 1);
                return new SvgValueCell(owner, SvgValueKind.Transform, SvgTransforms.MatrixArgument(owner, args[0])).Wrap(false);
            case "getElementById":
                SvgValues.Require(owner, args, 1);
                var id = TypeConverter.ToString(args[0]);
                return realm.WrapNodeValue(FindById(owner, element, id));
            case "suspendRedraw":
                SvgValues.Require(owner, args, 1);
                _ = TypeConverter.ToUint32(args[0]);
                return JsNumber.PositiveOne;
            case "unsuspendRedraw":
                SvgValues.Require(owner, args, 1);
                _ = TypeConverter.ToUint32(args[0]);
                return JsValue.Undefined;
            case "unsuspendRedrawAll":
            case "forceRedraw":
                return JsValue.Undefined;
            case "pauseAnimations":
                owner.State(element).AnimationsPaused = true;
                return JsValue.Undefined;
            case "unpauseAnimations":
                owner.State(element).AnimationsPaused = false;
                return JsValue.Undefined;
            case "animationsPaused":
                return JsBoolean.Create(owner.State(element).AnimationsPaused);
            case "getCurrentTime":
                return JsNumber.Create(owner.State(element).CurrentTime);
            case "setCurrentTime":
                SvgValues.Require(owner, args, 1);
                owner.State(element).CurrentTime = SvgValues.Number(owner, args[0]);
                return JsValue.Undefined;
            case "setOrientToAuto":
                return SetText(realm, element, "orient", [JsString.Create("auto")]);
            case "setOrientToAngle":
                SvgValues.Require(owner, args, 1);
                if (args[0] is not JsSvgValue { Cell.Kind: SvgValueKind.Angle } angle)
                    return DomFailures.Refuse(realm, method, "TypeError", "Expected an SVGAngle.");
                return SetText(realm, element, "orient", [JsString.Create(SvgValues.Serialize(SvgValueKind.Angle, angle.Cell.Read()))]);
            case "getNumberOfChars":
                return JsNumber.Create(CharacterCount(realm, element));
            case "getComputedTextLength":
                return JsNumber.PositiveZero;
            case "getSubStringLength":
                SvgValues.Require(owner, args, 2);
                var start = TypeConverter.ToUint32(args[0]);
                _ = TypeConverter.ToUint32(args[1]);
                if (start >= CharacterCount(realm, element))
                    return DomFailures.Refuse(realm, method, "IndexSizeError", "The character index is out of range.");
                return JsNumber.PositiveZero;
            default:
                throw new InvalidOperationException("Unknown SVG operation: " + method);
        }
    }

    private static int CharacterCount(DomRealm realm, Element element)
    {
        var text = DomDescendantText.Read(element, realm.NativeReadCheckpoint, realm.CancellationToken);
        var count = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if ((count & 255) == 0) realm.Engine.Constraints.Check();
            count++;
        }
        return count;
    }

    private static Element? FindById(SvgRealm owner, Element root, string id)
    {
        for (Node? node = root.FirstChild; node is not null;)
        {
            owner.Checkpoint();
            if (node is Element element && element.GetAttribute("id") == id && id.Length != 0) return element;
            if (node.FirstChild is { } child) { node = child; continue; }
            while (node != root && node.NextSibling is null) node = node.ParentNode!;
            node = node == root ? null : node.NextSibling;
        }
        return null;
    }
}
