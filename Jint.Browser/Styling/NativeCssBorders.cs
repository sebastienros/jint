using Jint.HtmlParser;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Selectors;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Math;
using Jint.HtmlParser.Css.Values.Properties;

namespace Jint.Browser.Styling;

internal sealed partial class NativeCssQuery
{
    private static bool IsLineWidth(string name) =>
        CssPropertyRegistry.Find(name, CssDeclarationContext.Style)?.Grammar == CssPropertyGrammar.BorderWidth;

    // https://drafts.csswg.org/css-values-4/#snap-as-a-line-width
    private CssPropertyValue SnapLineWidth(string name, CssPropertyValue value)
    {
        if (value.Kind != CssPropertyValueKind.Numeric || value.Numeric.Unit != CssUnit.Px)
            throw new CssIncompleteGrammarException(name, "C6:unresolved-line-width", value.Span);
        var number = CssMathNumbers.ParseFinite(value.Numeric.Number, value.Numeric.Unit, _work);
        var scale = _media.Resolution;
        if (!double.IsFinite(scale) || scale <= 0)
            throw new CssIncompleteGrammarException(name, "C6:device-resolution", value.Span);
        var pixels = number * scale;
        var snapped = pixels > 0 && pixels < 1 ? 1 / scale :
            double.IsFinite(pixels) ? System.Math.Floor(pixels) / scale : number;
        return Number(name, new CssMathNumeric(snapped, CssNumericKind.Dimension, CssUnit.Px, value.Span));
    }

    private string PhysicalBorderName(Element element, string logical, ref SelectorMatchWork matching) =>
        CssBorderPropertyParser.PhysicalName(logical, GetProperty(element, "writing-mode", ref matching).Text,
            GetProperty(element, "direction", ref matching).Text);

    // The reset-only members participate in the cascade even before their non-wide grammar
    // is implemented. Reading border must not demand an unrelated border-image renderer.
    private bool BorderImageResetIsInitial(Element element, string name, ref SelectorMatchWork matching)
    {
        for (var current = element; current is not null; current = InheritanceParent(current))
        {
            _work.Charge(1);
            var candidate = Winner(StateOf(current, ref matching), name, ref matching, substitute: true);
            var value = candidate is { WasSubstituted: true } ? candidate.Resolved : candidate?.Declaration.Value;
            if (value is null || value is { Kind: CssPropertyValueKind.Keyword, Text: "initial" or "unset" }) return true;
            if (value is not { Kind: CssPropertyValueKind.Keyword, Text: "inherit" }) return false;
        }
        return true;
    }
}
