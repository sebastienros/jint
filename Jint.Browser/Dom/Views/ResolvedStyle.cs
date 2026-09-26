using System.Globalization;
using Jint.Browser.Runtime;
using Jint.HtmlParser;

namespace Jint.Browser.Dom.Views;

// CSSOM §9 resolved values: used geometry belongs to Browser, not property syntax or cascade.
internal static class ResolvedStyle
{
    internal static string? ValueOf(string property, Element element, PageRuntime runtime)
    {
        if (property is not ("width" or "height")) return null;
        var sizes = runtime.Layout.MeasureSizes();
        if (!sizes.HasBox(element)) return "auto";
        var value = property == "width" ? sizes.Width(element) : sizes.Measure(element).Height;
        return value.ToString("0.####", CultureInfo.InvariantCulture) + "px";
    }
}
