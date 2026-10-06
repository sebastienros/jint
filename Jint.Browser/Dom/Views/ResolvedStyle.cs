using System.Globalization;
using Jint.Browser.Dom.Canvas;
using Jint.Browser.Runtime;
using Jint.Browser.Styling;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Values.Properties;

namespace Jint.Browser.Dom.Views;

internal static class ResolvedStyle
{
    // Renderless CSSOM: width/height use synthetic geometry and colors resolve to sRGB; other values stay text.
    internal static string ValueOf(string propertyName, NativeCssComputedStyle style, Element element, PageRuntime? runtime)
    {
        style.VerifyRead();
        var name = CssPropertyRegistry.NormalizeName(propertyName, style.Work);
        var value = style.GetNormalizedProperty(name).Text;
        if (IsColor(name))
        {
            value = Color(name, value, style);
            style.VerifyRead();
            return value;
        }
        if (name is not ("width" or "height") || runtime is null ||
            !ReferenceEquals(runtime.Document, element.OwnerDocument)) return value;
        var sizes = style.ReadContext is { } context ? runtime.Layout.MeasureSizes(context) : runtime.Layout.MeasureSizes();
        if (sizes.HasBox(element))
        {
            var number = name == "width" ? sizes.Width(element) : sizes.Height(element);
            value = number.ToString("0.######", CultureInfo.InvariantCulture) + "px";
        }
        style.VerifyRead();
        return value;
    }

    /// <summary>A settled property's resolved color, or its text unchanged when it is not color-valued.</summary>
    internal static string ColorOf(string name, string value, NativeCssComputedStyle style) =>
        IsColor(name) ? Color(name, value, style) : value;

    // CSSOM §9 (resolved value): color properties report the used color; CSS Color 4 §4.6 makes
    // currentcolor the element's color, and on 'color' itself the inherited one.
    private static string Color(string name, string value, NativeCssComputedStyle style)
    {
        var text = value.Trim();
        if (name == "caret-color" && text.Equals("auto", StringComparison.OrdinalIgnoreCase)) text = "currentcolor";
        if (text.Equals("currentcolor", StringComparison.OrdinalIgnoreCase))
        {
            if (name != "color") return Color("color", style.GetNormalizedProperty("color").Text, style);
            return ParentOf(style.Element) is { } parent
                ? Color("color", style.For(parent).GetNormalizedProperty("color").Text, style.For(parent))
                : "rgb(0, 0, 0)";
        }
        style.Work.Charge(text.Length);
        return CanvasColor.TryParse(text, out var r, out var g, out var b, out var alpha) ? Serialize(r, g, b, alpha) : value;
    }

    private static Element? ParentOf(Element element) => element.ParentNode switch
    {
        ShadowRoot root => root.Host,
        Element parent => parent,
        _ => null
    };

    // CSS Color 4 §15.2 (serializing sRGB values): legacy rgb()/rgba() syntax; alpha takes the
    // fewest decimals (two, else three) that still round-trip its 8-bit value.
    internal static string Serialize(int r, int g, int b, double alpha)
    {
        if (alpha >= 1) return string.Create(CultureInfo.InvariantCulture, $"rgb({r}, {g}, {b})");
        var shortest = Math.Round(alpha, 2);
        if (Math.Round(shortest * 255) != Math.Round(alpha * 255)) shortest = Math.Round(alpha, 3);
        return string.Create(CultureInfo.InvariantCulture, $"rgba({r}, {g}, {b}, {shortest:0.###})");
    }

    private static bool IsColor(string name) => name switch
    {
        "color" or "background-color" or "outline-color" or "text-decoration-color" or "text-emphasis-color"
            or "column-rule-color" or "caret-color" or "accent-color" or "flood-color" or "lighting-color"
            or "stop-color" or "fill" or "stroke" or "-webkit-text-fill-color" or "-webkit-text-stroke-color" => true,
        _ => name.StartsWith("border-", StringComparison.Ordinal) && name.EndsWith("-color", StringComparison.Ordinal)
            && name != "border-color"
    };
}
