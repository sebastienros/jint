using Jint.HtmlParser;
using Jint.HtmlParser.Css.Selectors;

namespace Jint.Browser.Styling;

// CSS Color Adjustment 1 §3.1 (https://drafts.csswg.org/css-color-adjust-1/#forced-colors-properties): with
// forced colors active, the properties below take a forced computed value on every element whose
// forced-color-adjust is auto. The forced value is the one cached and inherited, so a descendant that opts out
// still inherits its parent's forced text-shadow, as in Chromium. The <color> properties are not forced
// (Dom/divergences.md).
internal sealed partial class NativeCssQuery
{
    private readonly bool _forcedColors;

    private static bool IsForcedColorsProperty(string name) => name is
        "background-image" or "box-shadow" or "text-shadow" or "color-scheme" or "accent-color" or "scrollbar-color";

    private NativeCssProperty ForcedColors(State state, NativeCssProperty property, ref SelectorMatchWork matching)
    {
        var name = property.Name;
        if (!IsForcedColorsProperty(name)) return property;
        // CSS Color Adjustment 1 §3.2: none opts out, and preserve-parent-color behaves as none for all
        // of these (it differs from none only for color, which is not forced here).
        var adjust = ReadProperty(state.Element, state.Pseudo, "forced-color-adjust", ref matching).Text.Trim();
        _work.Charge(adjust.Length);
        if (adjust.Equals("none", StringComparison.OrdinalIgnoreCase) ||
            adjust.Equals("preserve-parent-color", StringComparison.OrdinalIgnoreCase))
            return property;
        var text = name switch
        {
            // "UAs must not force background-image when it is a url": like Chromium, a list with any url() layer
            // is kept whole, gradients included, and a list without one computes to none.
            "background-image" => HasUrl(property.Text) ? property.Text : "none",
            "box-shadow" or "text-shadow" => "none",
            "color-scheme" => "light dark",
            _ => "auto"
        };
        return text == property.Text ? property : property with { Text = text };
    }

    private bool HasUrl(string text)
    {
        _work.Charge(text.Length);
        for (var at = text.IndexOf("url(", StringComparison.OrdinalIgnoreCase); at >= 0;
             at = text.IndexOf("url(", at + 4, StringComparison.OrdinalIgnoreCase))
        {
            // A whole function name: not the tail of another identifier such as a custom function.
            if (at == 0 || !(char.IsAsciiLetterOrDigit(text[at - 1]) || text[at - 1] is '-' or '_')) return true;
        }
        return false;
    }
}
