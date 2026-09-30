using System.Globalization;
using Jint.HtmlParser;
using Jint.HtmlParser.Svg;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Browser.Dom.Svg;

/// <summary>SVG value conversion and serialization, with deterministic headless unit resolution.</summary>
/// <remarks>https://svgwg.org/svg2-draft/types.html#InterfaceSVGLength</remarks>
internal static class SvgValues
{
    private static readonly string[] LengthUnits = ["", "", "%", "em", "ex", "px", "cm", "mm", "in", "pt", "pc"];
    private static readonly string[] AngleUnits = ["", "", "deg", "rad", "grad"];

    internal static string Format(double value) => value.ToString("G", CultureInfo.InvariantCulture);
    internal static double Number(SvgRealm owner, JsValue value)
    {
        var number = TypeConverter.ToNumber(value);
        if (!double.IsFinite(number)) Throw.TypeError(owner.Realm, "An SVG number must be finite.");
        return number;
    }

    internal static void Require(SvgRealm owner, JsValue[] args, int count)
    {
        if (args.Length < count) Throw.TypeError(owner.Realm, "Not enough arguments for the SVG operation.");
    }

    internal static void Writable(SvgRealm owner, bool readOnly)
    {
        if (readOnly) DomFailures.Refuse(owner.Dom, "SVG", "NoModificationAllowedError", "An SVG animated value is read-only.");
    }

    internal static string Serialize(SvgValueKind kind, SvgValueData data) => kind switch
    {
        SvgValueKind.Length => Format(data.A) + LengthUnits[data.Unit],
        SvgValueKind.Angle => Format(data.A) + AngleUnits[data.Unit],
        SvgValueKind.Rect => $"{Format(data.A)} {Format(data.B)} {Format(data.C)} {Format(data.D)}",
        SvgValueKind.Point => $"{Format(data.A)},{Format(data.B)}",
        SvgValueKind.String => data.Text ?? "",
        SvgValueKind.PreserveAspectRatio => SvgParser.Alignments[data.Unit] + (data.A == 2 ? " slice" : " meet"),
        SvgValueKind.Transform => (SvgTransformKind) data.Unit switch
        {
            SvgTransformKind.Translate => $"translate({Format(data.A)} {Format(data.B)})",
            SvgTransformKind.Scale => $"scale({Format(data.A)} {Format(data.B)})",
            SvgTransformKind.Rotate => $"rotate({Format(data.A)} {Format(data.B)} {Format(data.C)})",
            SvgTransformKind.SkewX => $"skewX({Format(data.A)})",
            SvgTransformKind.SkewY => $"skewY({Format(data.A)})",
            SvgTransformKind.Matrix => $"matrix({Format(data.A)} {Format(data.B)} {Format(data.C)} {Format(data.D)} {Format(data.E)} {Format(data.F)})",
            _ => "matrix(1 0 0 1 0 0)",
        },
        _ => Format(data.A),
    };

    internal static SvgValueData? ParseAngle(string source)
    {
        var text = source.Trim();
        ushort unit = 1;
        if (text.EndsWith("grad", StringComparison.Ordinal)) { unit = 4; text = text[..^4]; }
        else if (text.EndsWith("rad", StringComparison.Ordinal)) { unit = 3; text = text[..^3]; }
        else if (text.EndsWith("deg", StringComparison.Ordinal)) { unit = 2; text = text[..^3]; }
        return SvgParser.ParseNumber(text) is { } number ? new SvgValueData(number, Unit: unit) : null;
    }

    internal static double Factor(SvgValueCell cell, ushort unit)
    {
        if (cell.Kind == SvgValueKind.Angle) return unit switch { 3 => 180 / Math.PI, 4 => .9, _ => 1 };
        return (SvgLengthUnit) unit == SvgLengthUnit.Percentage
            ? Viewport(cell.Attribute?.Element, cell.Attribute?.Direction ?? SvgLengthDirection.Horizontal, cell.Owner.Checkpoint) / 100
            : AbsoluteFactor((SvgLengthUnit) unit);
    }

    private static double AbsoluteFactor(SvgLengthUnit unit) => unit switch
    {
        SvgLengthUnit.Ems => 16,
        SvgLengthUnit.Exs => 8,
        SvgLengthUnit.Cm => 96 / 2.54,
        SvgLengthUnit.Mm => 96 / 25.4,
        SvgLengthUnit.In => 96,
        SvgLengthUnit.Pt => 96d / 72,
        SvgLengthUnit.Pc => 16,
        _ => 1,
    };

    internal static double Viewport(Element? element, SvgLengthDirection direction, Action? checkpoint = null)
    {
        if (direction == SvgLengthDirection.Diagonal)
        {
            var width = Viewport(element, SvgLengthDirection.Horizontal, checkpoint);
            var height = Viewport(element, SvgLengthDirection.Vertical, checkpoint);
            return Math.Sqrt((width * width + height * height) / 2);
        }
        double multiplier = 1;
        for (var node = element?.ParentNode as Element; node is not null; node = node.ParentNode as Element)
        {
            checkpoint?.Invoke();
            if (node.NamespaceUri != Namespaces.Svg || node.LocalName != "svg") continue;
            if (SvgParser.ParseViewBox(node.GetAttribute("viewBox") ?? "", checkpoint) is { } box)
                return multiplier * (direction == SvgLengthDirection.Horizontal ? box.Width : box.Height);
            var length = SvgParser.ParseLength(node.GetAttribute(direction == SvgLengthDirection.Horizontal ? "width" : "height") ?? "", checkpoint);
            if (length is not { } value || value.Value < 0) return 0;
            if (value.Unit != SvgLengthUnit.Percentage) return multiplier * value.Value * AbsoluteFactor(value.Unit);
            multiplier *= value.Value / 100;
        }
        return 0;
    }
}

/// <summary>The animated wrapper keeps distinct writable base and read-only animated tear-offs.</summary>
/// <remarks>https://svgwg.org/svg2-draft/types.html#SVGDOMOverview</remarks>
internal sealed class JsSvgAnimated : ObjectInstance
{
    internal JsSvgAnimated(SvgAttribute attribute) : base(attribute.Owner.Engine)
    {
        Attribute = attribute;
        Prototype = attribute.Owner.Prototype("SVGAnimated" + attribute.Kind);
    }
    internal SvgAttribute Attribute { get; }
}

/// <summary>A live SVGLength, SVGNumber, SVGAngle or SVGPreserveAspectRatio value.</summary>
/// <remarks>
/// https://svgwg.org/svg2-draft/types.html#InterfaceSVGLength and
/// https://svgwg.org/svg2-draft/coords.html#InterfaceSVGPreserveAspectRatio.
/// </remarks>
internal sealed class JsSvgValue : ObjectInstance
{
    internal JsSvgValue(SvgValueCell cell, bool readOnly) : base(cell.Owner.Engine)
    {
        Cell = cell;
        ReadOnly = readOnly;
        Prototype = cell.Owner.Prototype("SVG" + cell.Kind);
    }
    internal SvgValueCell Cell { get; }
    internal bool ReadOnly { get { Cell.Read(); return field && Cell.Attribute is not null; } }

    internal JsValue Get(string name)
    {
        var data = Cell.Read();
        return name switch
        {
            "valueAsString" => JsString.Create(SvgValues.Serialize(Cell.Kind, data)),
            "unitType" or "align" => JsNumber.Create(data.Unit),
            "value" => JsNumber.Create(data.A * SvgValues.Factor(Cell, data.Unit)),
            _ => JsNumber.Create(data.A),
        };
    }

    internal JsValue Set(string name, JsValue value)
    {
        SvgValues.Writable(Cell.Owner, ReadOnly);
        SvgValueData data;
        if (name == "valueAsString")
        {
            var text = TypeConverter.ToString(value);
            var parsed = Cell.Kind == SvgValueKind.Angle ? SvgValues.ParseAngle(text)
                : SvgParser.ParseLength(text) is { } length ? new SvgValueData(length.Value, Unit: (ushort) length.Unit) : (SvgValueData?) null;
            if (parsed is null) DomFailures.Refuse(Cell.Owner.Dom, "SVG.valueAsString", "SyntaxError", "Invalid SVG value.");
            data = parsed!.Value;
        }
        else
        {
            var number = SvgValues.Number(Cell.Owner, value);
            data = Cell.Read();
            switch (name)
            {
                case "align":
                    if (number < 1 || number > 10 || number != Math.Truncate(number)) Throw.TypeError(Cell.Owner.Realm, "Invalid SVG alignment.");
                    data = data with { Unit = (ushort) number };
                    break;
                case "meetOrSlice":
                    if (number is not (1 or 2)) Throw.TypeError(Cell.Owner.Realm, "Invalid SVG scaling mode.");
                    data = data with { A = number };
                    break;
                case "value":
                    var factor = SvgValues.Factor(Cell, data.Unit);
                    if (factor == 0) DomFailures.Refuse(Cell.Owner.Dom, "SVG.value", "NotSupportedError", "The SVG percentage has no viewport.");
                    data = data with { A = number / factor };
                    break;
                default:
                    data = data with { A = number };
                    break;
            }
        }
        Cell.Write(data, ReadOnly);
        return JsValue.Undefined;
    }

    internal JsValue Units(bool convert, JsValue[] args)
    {
        SvgValues.Require(Cell.Owner, args, convert ? 1 : 2);
        var unit = TypeConverter.ToUint16(args[0]);
        var number = convert ? 0 : SvgValues.Number(Cell.Owner, args[1]);
        SvgValues.Writable(Cell.Owner, ReadOnly);
        if (unit == 0 || unit > (Cell.Kind == SvgValueKind.Angle ? 4 : 10))
            DomFailures.Refuse(Cell.Owner.Dom, "SVG.units", "NotSupportedError", "Invalid SVG unit type.");
        var data = Cell.Read();
        if (convert)
        {
            var factor = SvgValues.Factor(Cell, unit);
            if (factor == 0) DomFailures.Refuse(Cell.Owner.Dom, "SVG.units", "NotSupportedError", "The SVG percentage has no viewport.");
            number = data.A * SvgValues.Factor(Cell, data.Unit) / factor;
        }
        Cell.Write(data with { A = number, Unit = unit }, ReadOnly);
        return JsValue.Undefined;
    }
}

/// <summary>The live coordinate bridge used by SVG points and viewBox DOMRects.</summary>
/// <remarks>https://svgwg.org/svg2-draft/types.html#SVGDOMOverview</remarks>
internal sealed class SvgCoordinates(SvgValueCell cell, bool readOnly) : Geometry.IGeometryCoordinates
{
    internal SvgValueCell Cell => cell;
    internal bool ReadOnly { get { cell.Read(); return readOnly && cell.Attribute is not null; } }
    public double Get(int index)
    {
        var d = cell.Read();
        return index switch { 0 => d.A, 1 => d.B, 2 => d.C, _ => d.D };
    }
    public void Set(int index, double value)
    {
        SvgValues.Writable(cell.Owner, ReadOnly);
        if (!double.IsFinite(value)) Throw.TypeError(cell.Owner.Realm, "An SVG coordinate must be finite.");
        var d = cell.Read();
        cell.Write(index switch { 0 => d with { A = value }, 1 => d with { B = value }, 2 => d with { C = value }, _ => d with { D = value } }, ReadOnly);
    }
}
