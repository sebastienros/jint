using System.Globalization;
using Jint.Browser.Layout;
using Jint.Browser.Runtime;
using Jint.Browser.Styling;
using Jint.HtmlParser;

namespace Jint.Browser.Dom.Views;

/// <summary>
/// The resolved value of <c>transform-origin</c> and <c>perspective-origin</c>: CSSOM §9 lists both as
/// resolved-value-special properties whose resolved value is the used value, so their keywords and
/// percentages become pixel offsets into the element's reference box.
/// </summary>
/// <remarks>
/// The box is the flat model's synthetic one, the same dimensions <c>width</c> and <c>height</c> report.
/// A pseudo-element, or an element with no box, uses its own computed absolute width or height instead. A
/// component that needs a dimension neither source has, and any value outside the grammar, keeps its text.
/// </remarks>
internal static class ResolvedOrigin
{
    internal static string Resolve(string name, string value, NativeCssComputedStyle style, Element element,
        PageRuntime? runtime, Viewport viewport)
    {
        var text = value.Trim();
        if (text.Length == 0 || text.Contains('(')) return value;
        style.Work.Charge(text.Length);
        var parts = text.Split((char[]?) null, StringSplitOptions.RemoveEmptyEntries);
        var transform = name == "transform-origin";
        if (parts.Length > (transform ? 3 : 2)) return value;

        // CSS Transforms 1 §6 / §12 (<position>-like grammar): a single value names one axis and centres the
        // other; two keywords may come in either order; a third, transform-origin only, is the z length.
        string x, y;
        if (parts.Length == 1)
        {
            if (IsVertical(parts[0])) (x, y) = ("center", parts[0]);
            else (x, y) = (parts[0], "center");
        }
        else if (IsVertical(parts[0]) || IsHorizontal(parts[1])) (x, y) = (parts[1], parts[0]);
        else (x, y) = (parts[0], parts[1]);
        if (IsVertical(x) || IsHorizontal(y)) return value;

        var z = 0d;
        if (parts.Length == 3 && !ResolvedLengths.TryPixels(parts[2], out z)) return value;

        FlatLayout.SizeQuery? sizes = null;
        var measured = false;
        if (!TryOffset(x, "left", "right", width: true, out var left) ||
            !TryOffset(y, "top", "bottom", width: false, out var top)) return value;

        var result = Format(left) + " " + Format(top);
        return z != 0 ? result + " " + Format(z) : result;

        bool TryOffset(string component, string start, string end, bool width, out double offset)
        {
            offset = 0;
            double fraction;
            if (component.Equals(start, StringComparison.OrdinalIgnoreCase)) return true;
            if (component.Equals(end, StringComparison.OrdinalIgnoreCase)) fraction = 1;
            else if (component.Equals("center", StringComparison.OrdinalIgnoreCase)) fraction = 0.5;
            else if (component.EndsWith('%') && TryNumber(component.AsSpan(0, component.Length - 1), out var percent))
                fraction = percent / 100;
            else return ResolvedLengths.TryPixels(component, out offset);
            if (fraction == 0) return true;
            if (!TryDimension(width, out var size)) return false;
            offset = size * fraction;
            return true;
        }

        bool TryDimension(bool width, out double size)
        {
            if (runtime is not null && style.Pseudo is null && ReferenceEquals(runtime.Document, element.OwnerDocument))
            {
                if (!measured)
                {
                    sizes = style.ReadContext is { } context ? runtime.Layout.MeasureSizes(context) : runtime.Layout.MeasureSizes();
                    measured = true;
                }
                if (sizes is { } query && query.HasBox(element))
                {
                    size = width ? query.Width(element) : query.Height(element);
                    return true;
                }
            }
            var dimension = width ? "width" : "height";
            var computed = ResolvedLengths.Resolve(dimension, style.GetNormalizedProperty(dimension), style, viewport);
            return ResolvedLengths.TryPixels(computed.Trim(), out size);
        }
    }

    private static bool IsHorizontal(string part) =>
        part.Equals("left", StringComparison.OrdinalIgnoreCase) || part.Equals("right", StringComparison.OrdinalIgnoreCase);

    private static bool IsVertical(string part) =>
        part.Equals("top", StringComparison.OrdinalIgnoreCase) || part.Equals("bottom", StringComparison.OrdinalIgnoreCase);

    private static bool TryNumber(ReadOnlySpan<char> text, out double number) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number) && double.IsFinite(number);

    private static string Format(double value) => ResolvedLengths.Serialize(value) + "px";
}
