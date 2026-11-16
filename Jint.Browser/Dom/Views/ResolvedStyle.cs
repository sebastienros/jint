using System.Globalization;
using Jint.Browser.Dom.Canvas;
using Jint.Browser.Runtime;
using Jint.Browser.Styling;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Values.Properties;

namespace Jint.Browser.Dom.Views;

internal static class ResolvedStyle
{
    // Renderless CSSOM: width/height, grid column tracks and transform/perspective origins use synthetic geometry,
    // colors (also in gradients) resolve to sRGB, and lengths are absolutized; other values stay text.
    internal static string ValueOf(string propertyName, NativeCssComputedStyle style, Element element, PageRuntime? runtime)
    {
        style.VerifyRead();
        var name = CssPropertyRegistry.NormalizeName(propertyName, style.Work);
        var property = style.GetNormalizedProperty(name);
        var value = property.Text;
        if (IsColor(name))
        {
            value = ResolvedText(name, value, style);
            style.VerifyRead();
            return value;
        }
        var viewport = runtime?.Viewport ?? Viewport.Default;
        if (ResolvedValueText.IsImage(name) || IsPosition(name))
        {
            value = ResolvedText(name, ResolvedLengths.Resolve(name, property, style, viewport), style);
            style.VerifyRead();
            return value;
        }
        // A pseudo-element has no box in the flat model, so its width, height and tracks stay computed values.
        if ((name is "width" or "height" || name == "grid-template-columns" && ResolvedValueText.IsGridContainer(style)) &&
            runtime is not null && style.Pseudo is null && ReferenceEquals(runtime.Document, element.OwnerDocument))
        {
            var sizes = style.ReadContext is { } context ? runtime.Layout.MeasureSizes(context) : runtime.Layout.MeasureSizes();
            if (sizes.HasBox(element))
            {
                value = name == "grid-template-columns"
                    ? ResolvedValueText.GridColumns(ResolvedLengths.Resolve(name, property, style, viewport), style, sizes.Width(element),
                        () => ResolvedLengths.Resolve("column-gap", style.GetNormalizedProperty("column-gap"), style, viewport))
                    : ResolvedLengths.Serialize(name == "width" ? sizes.Width(element) : sizes.Height(element)) + "px";
                style.VerifyRead();
                return value;
            }
        }
        // CSSOM §9: a computed value holds absolute lengths and reduced math (CSS Values 4 §6, §10.13).
        value = ResolvedLengths.Resolve(name, property, style, viewport);
        if (name == "grid-template-rows" && style.Pseudo is null)
        {
            value = ResolvedValueText.GridRows(value, style);
        }
        // CSSOM §9: transform's resolved value is its matrix. A list that needs a box to resolve stays text.
        if (name == "transform" && value != "none" &&
            Geometry.GeometryConversion.TryParse(value, style.Work, out var matrix, out var is2D))
        {
            value = Geometry.GeometryConversion.SerializeResolved(matrix, is2D);
        }
        else if (name is "transform-origin" or "perspective-origin")
        {
            value = ResolvedOrigin.Resolve(name, value, style, element, runtime, viewport);
        }
        style.VerifyRead();
        return value;
    }

    /// <summary>
    /// A settled property's resolved value where it needs no box: absolute lengths and reduced math, a color
    /// (gradient stops included) or a position; its text unchanged otherwise. DevTools' CSS domain reports it.
    /// </summary>
    internal static string SettledValueOf(NativeCssProperty property, NativeCssComputedStyle style)
    {
        var document = style.Element.OwnerDocument;
        var host = document is null ? null : NativeCssStyleSheets.RealmOf(document);
        var viewport = (host is null ? null : PageRuntime.FindBrowsingContext(host.Engine, document))?.Viewport ?? Viewport.Default;
        var value = ResolvedText(property.Name, ResolvedLengths.Resolve(property.Name, property, style, viewport), style);
        style.VerifyRead();
        return value;
    }

    // A settled color (gradient stops included) or position; the CSSOM and the DevTools CSS domain share it.
    private static string ResolvedText(string name, string value, NativeCssComputedStyle style) =>
        IsColor(name) ? Color(name, value, style)
        : ResolvedValueText.IsImage(name) ? ResolvedValueText.Image(value, style)
        : IsPosition(name) ? CssBoxShorthands.ComputedPosition(value, style.Work) ?? value
        : value;

    // CSS Masking 1 §7.4: mask-position computes like background-position, each axis a percentage or an
    // offset (CSS Backgrounds 3 §2.6), which is what Chromium reports. A value that is not one stays text.
    // background-position-x/-y hold one axis each and still report declared text.
    private static bool IsPosition(string name) => name == "mask-position";

    // CSSOM §9 (resolved value): color properties report the used color; CSS Color 4 §4.6 makes
    // currentcolor the element's color, and on 'color' itself the inherited one.
    private static string Color(string name, string value, NativeCssComputedStyle style)
    {
        var text = value.Trim();
        if (name == "caret-color" && text.Equals("auto", StringComparison.OrdinalIgnoreCase)) text = "currentcolor";
        if (text.Equals("currentcolor", StringComparison.OrdinalIgnoreCase))
        {
            if (name != "color") return Color("color", style.GetNormalizedProperty("color").Text, style);
            // A pseudo-element's parent is its originating element.
            return (style.Pseudo is not null ? style.Element : ParentOf(style.Element)) is { } parent
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
