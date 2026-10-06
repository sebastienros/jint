using Jint.Browser.Geometry;
using Jint.HtmlParser;
using Jint.HtmlParser.Svg;
using Jint.Native;

namespace Jint.Browser.Dom.Svg;

/// <summary>Attribute geometry without rendering: basic shapes, ancestor transforms and path-length queries.</summary>
/// <remarks>
/// https://svgwg.org/svg2-draft/types.html#InterfaceSVGGraphicsElement and
/// https://svgwg.org/svg2-draft/types.html#InterfaceSVGGeometryElement.
/// Rectangles ignore corner rounding; ellipses use a 256-segment perimeter. Paths have no geometry.
/// Other bounding boxes use the existing synthetic layout box, or zero. CSS transforms, stroke,
/// markers, clipping, viewport fitting and text shaping do not participate.
/// </remarks>
internal static class SvgGeometry
{
    internal static double AttributeLength(SvgRealm owner, Element element, string name)
    {
        var direction = name switch
        {
            "y" or "y1" or "y2" or "cy" or "ry" or "height" => SvgLengthDirection.Vertical,
            "r" => SvgLengthDirection.Diagonal,
            _ => SvgLengthDirection.Horizontal,
        };
        var attribute = owner.Attribute(element, name, SvgValueKind.Length, "0", direction);
        attribute.Refresh();
        var cell = attribute.Items[0];
        return cell.Data.A * SvgValues.Factor(cell, cell.Data.Unit);
    }

    internal static JsValue Bounds(SvgRealm owner, Element element)
    {
        double x, y, width, height;
        switch (element.LocalName)
        {
            case "rect":
            case "image":
            case "foreignObject":
                x = AttributeLength(owner, element, "x");
                y = AttributeLength(owner, element, "y");
                width = Math.Max(0, AttributeLength(owner, element, "width"));
                height = Math.Max(0, AttributeLength(owner, element, "height"));
                break;
            case "circle":
            case "ellipse":
                var rx = Math.Max(0, AttributeLength(owner, element, element.LocalName == "circle" ? "r" : "rx"));
                var ry = element.LocalName == "circle" ? rx : Math.Max(0, AttributeLength(owner, element, "ry"));
                x = AttributeLength(owner, element, "cx") - rx;
                y = AttributeLength(owner, element, "cy") - ry;
                width = 2 * rx;
                height = 2 * ry;
                break;
            case "line":
            case "polyline":
            case "polygon":
                var points = Vertices(owner, element);
                if (points.Length == 0) return owner.Dom.Geometry.CreateRect(true, 0, 0, 0, 0);
                x = points[0].X;
                y = points[0].Y;
                var right = x;
                var bottom = y;
                foreach (var point in points)
                {
                    owner.Checkpoint();
                    x = Math.Min(x, point.X);
                    y = Math.Min(y, point.Y);
                    right = Math.Max(right, point.X);
                    bottom = Math.Max(bottom, point.Y);
                }
                width = right - x;
                height = bottom - y;
                break;
            default:
                return Layout.LayoutMembers.BoundingClientRect(owner.Dom, element);
        }
        return owner.Dom.Geometry.CreateRect(true, x, y, width, height);
    }

    internal static JsDomMatrix Ctm(SvgRealm owner, Element element)
    {
        var matrix = GeometryMatrix.Identity();
        for (var current = element; current is not null; current = current.ParentNode as Element)
        {
            owner.Checkpoint();
            if (current.NamespaceUri != Namespaces.Svg) continue;
            var transform = owner.Attribute(current, "transform", SvgValueKind.TransformList);
            transform.Refresh();
            var local = GeometryMatrix.Identity();
            foreach (var cell in transform.Items)
            {
                owner.Checkpoint();
                GeometryMatrix.PostMultiply(local, SvgTransforms.Matrix(cell.Data));
            }
            GeometryMatrix.PreMultiply(matrix, local);
        }
        return owner.Dom.Geometry.CreateMatrix(true, matrix, true);
    }

    internal static double Length(SvgRealm owner, Element element)
    {
        if (element.LocalName == "circle")
            return 2 * Math.PI * Math.Max(0, AttributeLength(owner, element, "r"));
        var points = Vertices(owner, element);
        double length = 0;
        for (var i = 1; i < points.Length; i++)
        {
            owner.Checkpoint();
            length += Distance(points[i - 1], points[i]);
        }
        return length;
    }

    internal static JsDomPoint Point(SvgRealm owner, Element element, double distance)
    {
        if (element.LocalName == "circle")
        {
            var radius = Math.Max(0, AttributeLength(owner, element, "r"));
            var angle = radius == 0 ? 0 : Math.Clamp(distance, 0, 2 * Math.PI * radius) / radius;
            return owner.Dom.Geometry.CreatePoint(true, AttributeLength(owner, element, "cx") + radius * Math.Cos(angle),
                AttributeLength(owner, element, "cy") + radius * Math.Sin(angle), 0, 1);
        }
        var points = Vertices(owner, element);
        distance = Math.Max(0, distance);
        for (var i = 1; i < points.Length; i++)
        {
            owner.Checkpoint();
            var length = Distance(points[i - 1], points[i]);
            if (distance <= length && length > 0)
            {
                var ratio = distance / length;
                return owner.Dom.Geometry.CreatePoint(true, points[i - 1].X + ratio * (points[i].X - points[i - 1].X),
                    points[i - 1].Y + ratio * (points[i].Y - points[i - 1].Y), 0, 1);
            }
            distance -= length;
        }
        var last = points.Length == 0 ? default : points[^1];
        return owner.Dom.Geometry.CreatePoint(true, last.X, last.Y, 0, 1);
    }

    private static double Distance(SvgPoint a, SvgPoint b)
    {
        var x = b.X - a.X;
        var y = b.Y - a.Y;
        return Math.Sqrt(x * x + y * y);
    }

    private static SvgPoint[] Vertices(SvgRealm owner, Element element)
    {
        switch (element.LocalName)
        {
            case "line":
                return [new(AttributeLength(owner, element, "x1"), AttributeLength(owner, element, "y1")),
                    new(AttributeLength(owner, element, "x2"), AttributeLength(owner, element, "y2"))];
            case "rect":
                var x = AttributeLength(owner, element, "x");
                var y = AttributeLength(owner, element, "y");
                var width = Math.Max(0, AttributeLength(owner, element, "width"));
                var height = Math.Max(0, AttributeLength(owner, element, "height"));
                return width == 0 || height == 0 ? [] : [new(x, y), new(x + width, y), new(x + width, y + height), new(x, y + height), new(x, y)];
            case "ellipse":
                var cx = AttributeLength(owner, element, "cx");
                var cy = AttributeLength(owner, element, "cy");
                var rx = Math.Max(0, AttributeLength(owner, element, "rx"));
                var ry = Math.Max(0, AttributeLength(owner, element, "ry"));
                if (rx == 0 || ry == 0) return [];
                var ellipse = new SvgPoint[257];
                for (var i = 0; i < ellipse.Length; i++)
                {
                    if ((i & 31) == 0) owner.Checkpoint();
                    var angle = i * 2 * Math.PI / 256;
                    ellipse[i] = new SvgPoint(cx + rx * Math.Cos(angle), cy + ry * Math.Sin(angle));
                }
                return ellipse;
            case "polyline":
            case "polygon":
                var attribute = owner.Attribute(element, "points", SvgValueKind.PointList);
                attribute.Refresh();
                var count = attribute.Items.Count;
                if (count == 0) return [];
                var points = new SvgPoint[count + (element.LocalName == "polygon" ? 1 : 0)];
                for (var i = 0; i < count; i++)
                {
                    owner.Checkpoint();
                    var d = attribute.Items[i].Data;
                    points[i] = new SvgPoint(d.A, d.B);
                }
                if (points.Length != count) points[^1] = points[0];
                return points;
            default:
                return [];
        }
    }
}
