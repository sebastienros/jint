using Jint.HtmlParser.Css;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Math;
using Jint.HtmlParser.Css.Values.Properties;

namespace Jint.Browser.Styling;

internal sealed partial class NativeCssQuery
{
    // CSS Fonts 4 §2.2.1: fractional inherited weights use the same interval table.
    private CssPropertyValue RelativeFontWeight(string keyword, double inherited, CssSourceSpan span)
    {
        var number = keyword == "bolder" ? inherited switch
        {
            < 350 => 400,
            < 550 => 700,
            < 900 => 900,
            _ => inherited
        } : inherited switch
        {
            < 100 => inherited,
            < 550 => 100,
            < 750 => 400,
            _ => 700
        };
        return FontWeightNumber(number, span);
    }

    private CssPropertyValue FontWeightNumber(double number, CssSourceSpan span) =>
        Number("font-weight", new CssMathNumeric(number, CssNumericKind.Number, CssUnit.None, span));
}
