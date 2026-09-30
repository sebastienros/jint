using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Browser.Dom.Svg;

/// <summary>Engine-independent WebIDL shapes for SVG animated values, scalar values and lists.</summary>
/// <remarks>https://svgwg.org/svg2-draft/types.html#DOMInterfacesForBasicDataTypes</remarks>
internal static class SvgValueShapes
{
    private static readonly Dictionary<string, JsObjectShape> Shapes = Build();

    internal static JsObjectShape For(string name) => Shapes[name];

    internal static (string Name, int Value)[] Constants(string name)
    {
        string prefix;
        string[] values;
        switch (name)
        {
            case "SVGLength":
                prefix = "SVG_LENGTHTYPE_";
                values = ["UNKNOWN", "NUMBER", "PERCENTAGE", "EMS", "EXS", "PX", "CM", "MM", "IN", "PT", "PC"];
                break;
            case "SVGAngle":
                prefix = "SVG_ANGLETYPE_";
                values = ["UNKNOWN", "UNSPECIFIED", "DEG", "RAD", "GRAD"];
                break;
            case "SVGTransform":
                prefix = "SVG_TRANSFORM_";
                values = ["UNKNOWN", "MATRIX", "TRANSLATE", "SCALE", "ROTATE", "SKEWX", "SKEWY"];
                break;
            case "SVGPreserveAspectRatio":
                return Names("SVG_PRESERVEASPECTRATIO_", ["UNKNOWN", "NONE", "XMINYMIN", "XMIDYMIN", "XMAXYMIN",
                    "XMINYMID", "XMIDYMID", "XMAXYMID", "XMINYMAX", "XMIDYMAX", "XMAXYMAX"])
                    .Concat(Names("SVG_MEETORSLICE_", ["UNKNOWN", "MEET", "SLICE"])).ToArray();
            case "SVGUnitTypes":
                prefix = "SVG_UNIT_TYPE_";
                values = ["UNKNOWN", "USERSPACEONUSE", "OBJECTBOUNDINGBOX"];
                break;
            default:
                return [];
        }
        return Names(prefix, values);
    }

    private static (string, int)[] Names(string prefix, string[] names)
        => names.Select((name, index) => (prefix + name, index)).ToArray();

    private static Dictionary<string, JsObjectShape> Build()
    {
        var shapes = new Dictionary<string, JsObjectShape>(StringComparer.Ordinal);
        foreach (var name in SvgRealm.InterfaceNames)
        {
            var builder = new JsObjectShape.Builder().PerRealmSlot("constructor").ToStringTag(name);
            foreach (var (constant, value) in Constants(name)) builder.Constant(constant, JsNumber.Create(value));
            if (name.StartsWith("SVGAnimated", StringComparison.Ordinal))
            {
                var primitive = name is "SVGAnimatedString" or "SVGAnimatedBoolean" or "SVGAnimatedNumber" or "SVGAnimatedInteger" or "SVGAnimatedEnumeration";
                builder.Accessor("baseVal", (t, _) => Animated(t, name).Attribute.Read(false),
                    primitive ? (t, a) => Animated(t, name).Attribute.Assign(a.At(0)) : null);
                builder.Accessor("animVal", (t, _) => Animated(t, name).Attribute.Read(true),
                    (t, _) =>
                    {
                        SvgValues.Writable(Animated(t, name).Attribute.Owner, true);
                        return JsValue.Undefined;
                    });
            }
            else if (name.EndsWith("List", StringComparison.Ordinal))
            {
                builder.Accessor("numberOfItems", (t, _) => JsNumber.Create(List(t, name).Length));
                builder.Accessor("length", (t, _) => JsNumber.Create(List(t, name).Length));
                foreach (var (method, length) in new (string, int)[]
                {
                    ("clear", 0), ("initialize", 1), ("getItem", 1), ("insertItemBefore", 2),
                    ("replaceItem", 2), ("removeItem", 1), ("appendItem", 1),
                })
                    builder.Method(method, (t, a) => List(t, name).Invoke(method, a), length);
                if (name == "SVGTransformList")
                {
                    builder.Method("consolidate", (t, a) => List(t, name).Invoke("consolidate", a));
                    builder.Method("createSVGTransformFromMatrix", (t, a) => List(t, name).Invoke("createSVGTransformFromMatrix", a), 1);
                }
                builder.PerRealmSlot(Jint.Native.Symbol.GlobalSymbolRegistry.Iterator, Collections.DomIterator.ArrayValues);
            }
            else if (name == "SVGTransform")
            {
                builder.Accessor("type", static (t, _) => JsNumber.Create(Transform(t).Cell.Read().Unit));
                builder.Accessor("matrix", static (t, _) => Transform(t).Matrix);
                builder.Accessor("angle", static (t, _) =>
                {
                    var data = Transform(t).Cell.Read();
                    return JsNumber.Create(data.Unit is 4 or 5 or 6 ? data.A : 0);
                });
                foreach (var (method, length) in new (string, int)[] { ("setMatrix", 1), ("setTranslate", 2), ("setScale", 2),
                    ("setRotate", 3), ("setSkewX", 1), ("setSkewY", 1) })
                    builder.Method(method, (t, a) => Transform(t).Invoke(method, a), length);
            }
            else if (name != "SVGUnitTypes")
            {
                var properties = name switch
                {
                    "SVGPreserveAspectRatio" => new[] { "align", "meetOrSlice" },
                    "SVGNumber" => ["value"],
                    _ => ["unitType", "value", "valueInSpecifiedUnits", "valueAsString"],
                };
                foreach (var property in properties)
                {
                    builder.Accessor(property, (t, _) => Value(t, name).Get(property),
                        property == "unitType" ? null : (t, a) => Value(t, name).Set(property, a.At(0)));
                }
                if (name is "SVGLength" or "SVGAngle")
                {
                    builder.Method("newValueSpecifiedUnits", (t, a) => Value(t, name).Units(false, a), 2);
                    builder.Method("convertToSpecifiedUnits", (t, a) => Value(t, name).Units(true, a), 1);
                }
            }
            shapes.Add(name, builder.Build());
        }
        return shapes;
    }

    private static T Brand<T>(JsValue value, string name) where T : ObjectInstance
    {
        if (value is T result) return result;
        IllegalInvocation(value, name);
        return null!;
    }
    private static JsSvgValue Value(JsValue t, string name)
    {
        var value = Brand<JsSvgValue>(t, name);
        if ("SVG" + value.Cell.Kind != name) IllegalInvocation(t, name);
        return value;
    }
    private static JsSvgAnimated Animated(JsValue t, string name)
    {
        var value = Brand<JsSvgAnimated>(t, name);
        if ("SVGAnimated" + value.Attribute.Kind != name) IllegalInvocation(t, name);
        return value;
    }
    private static JsSvgList List(JsValue t, string name)
    {
        var value = Brand<JsSvgList>(t, name);
        if ("SVG" + value.Attribute.Kind != name) IllegalInvocation(t, name);
        return value;
    }
    private static JsSvgTransform Transform(JsValue t) => Brand<JsSvgTransform>(t, "SVGTransform");

    private static void IllegalInvocation(JsValue value, string name)
    {
        if (value is ObjectInstance instance) Throw.TypeError(instance.Engine.Realm, "Illegal invocation of " + name);
        Throw.TypeErrorNoEngine("Illegal invocation of " + name);
    }
}
