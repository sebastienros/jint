namespace Jint.HtmlParser.Svg;

/// <summary>The numeric unit codes exposed by SVGLength.</summary>
/// <remarks>https://svgwg.org/svg2-draft/types.html#InterfaceSVGLength</remarks>
internal enum SvgLengthUnit : ushort
{
    Unknown, Number, Percentage, Ems, Exs, Px, Cm, Mm, In, Pt, Pc,
}

/// <summary>A length in its specified units, independent of a viewport or font.</summary>
/// <remarks>https://svgwg.org/svg2-draft/types.html#InterfaceSVGLength</remarks>
internal readonly record struct SvgLength(double Value, SvgLengthUnit Unit = SvgLengthUnit.Number);

/// <summary>A point in an SVG points attribute.</summary>
/// <remarks>https://svgwg.org/svg2-draft/shapes.html#DataTypePoints</remarks>
internal readonly record struct SvgPoint(double X, double Y);

/// <summary>An SVG viewBox, with nonnegative dimensions.</summary>
/// <remarks>https://svgwg.org/svg2-draft/coords.html#ViewBoxAttribute</remarks>
internal readonly record struct SvgViewBox(double X, double Y, double Width, double Height);

/// <summary>The alignment and scaling mode of preserveAspectRatio.</summary>
/// <remarks>https://svgwg.org/svg2-draft/coords.html#InterfaceSVGPreserveAspectRatio</remarks>
internal readonly record struct SvgAspectRatio(ushort Align = 6, ushort MeetOrSlice = 1);

/// <summary>The SVGTransform type codes.</summary>
/// <remarks>https://svgwg.org/svg2-draft/coords.html#InterfaceSVGTransform</remarks>
internal enum SvgTransformKind : ushort
{
    Unknown, Matrix, Translate, Scale, Rotate, SkewX, SkewY,
}

/// <summary>An SVG transform's normalized arguments; rotation retains its optional center.</summary>
/// <remarks>https://svgwg.org/svg2-draft/coords.html#InterfaceSVGTransform</remarks>
internal readonly record struct SvgTransform(SvgTransformKind Kind, double A, double B = 0, double C = 0,
    double D = 0, double E = 0, double F = 0);
