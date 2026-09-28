using System.Globalization;
using Jint.Browser.Runtime;
using Jint.Browser.Styling;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Values.Properties;

namespace Jint.Browser.Dom.Views;

internal static class ResolvedStyle
{
    // Renderless CSSOM: only width/height use synthetic geometry; other values stay text.
    internal static string ValueOf(string propertyName, NativeCssComputedStyle style, Element element, PageRuntime? runtime)
    {
        style.VerifyRead();
        var name = CssPropertyRegistry.NormalizeName(propertyName, style.Work);
        var value = style.GetNormalizedProperty(name).Text;
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
}
