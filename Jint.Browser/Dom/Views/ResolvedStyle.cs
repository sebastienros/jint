using System.Globalization;
using Jint.Browser.Runtime;
using Jint.Browser.Styling;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values.Properties;
using Jint.HtmlParser.Css.Values.Transforms;

namespace Jint.Browser.Dom.Views;

// CSSOM §9 resolved values: used geometry belongs to Browser, not property syntax or cascade.
internal static class ResolvedStyle
{
    // Only the transform list contributes: no origin, ancestors or individual transforms.
    // Transforms 2 §2.1 and Transforms 1 §6: HTML view-box/stroke-box use border-box.
    internal static string Transform(NativeCssComputedStyle style, NativeCssProperty property,
        Element element, PageRuntime? runtime)
    {
        style.VerifyRead();
        if (property.Value?.Kind != CssPropertyValueKind.TransformList) return property.Text;
        var list = property.Value.TransformList;
        var work = style.Work;
        double? width = null;
        double? height = null;
        if (CssTransformMatrix.NeedsReferenceBox(list, work))
        {
            if (runtime is null || !ReferenceEquals(runtime.Document, element.OwnerDocument))
                throw Missing("transform-current-document");
            if (element.NamespaceUri != Namespaces.Html) throw Missing("transform-svg-reference-box");
            var box = style.GetPropertyValue("transform-box");
            if (box is "content-box" or "fill-box") throw Missing("transform-content-box");
            if (box is not ("view-box" or "border-box" or "stroke-box")) throw Missing("transform-reference-box");
            style.VerifyRead();
            var sizes = runtime.Layout.MeasureSizes();
            if (!sizes.HasBox(element)) throw Missing("transform-reference-box");
            // These are untransformed synthetic flat dimensions (inherited/flex width and row height),
            // not a claim of full CSS padding/border geometry. Never Place or use FlatBox.Empty.
            var measured = sizes.Measure(element);
            if (!double.IsFinite(measured.Width) || measured.Width < 0 ||
                !double.IsFinite(measured.Height) || measured.Height < 0) throw Missing("transform-reference-box");
            width = measured.Width;
            height = measured.Height;
            style.VerifyRead();
        }
        var result = CssTransformMatrix.Resolve(list, work, width, height);
        style.VerifyRead();
        return result;

        CssIncompleteGrammarException Missing(string dependency) => new("transform", "C6:" + dependency, list.Span);
    }

    internal static string? ValueOf(string property, Element element, PageRuntime runtime)
    {
        if (property is not ("width" or "height")) return null;
        var sizes = runtime.Layout.MeasureSizes();
        if (!sizes.HasBox(element)) return "auto";
        var value = property == "width" ? sizes.Width(element) : sizes.Measure(element).Height;
        return value.ToString("0.####", CultureInfo.InvariantCulture) + "px";
    }
}
