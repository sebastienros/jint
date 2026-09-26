using Jint.HtmlParser;
using Jint.HtmlParser.Css;
using Jint.HtmlParser.Css.Selectors;
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

    // Text 4 §7.3: convert the corresponding parent's logical alignment using its direction.
    private CssPropertyValue MatchParentAlignment(Element element, CssPropertyValue parentValue, CssSourceSpan span,
        ref SelectorMatchWork matching)
    {
        var text = parentValue.Text;
        if (text is "start" or "end" && InheritanceParent(element) is { } parent)
        {
            var rtl = GetProperty(parent, "direction", ref matching).Text == "rtl";
            text = (text == "start") != rtl ? "left" : "right";
        }
        return CssPropertyValue.Keyword(text, span);
    }

    private CssPropertyValue ComputeForElement(Element element, string name, CssPropertyValue value,
        ref SelectorMatchWork matching)
    {
        if (name == "font-size")
            return ComputeFontSize(element, value, (FontDependencies(value, true) & 1) != 0 ? InitialFontSize() : 0, ref matching);
        var dependencies = FontDependencies(value, false);
        if (dependencies == 0) return Compute(name, value);
        var metrics = _metrics with
        {
            FontSize = (dependencies & 1) != 0 ? ComputedFontSize(element, ref matching) : _metrics.FontSize,
            RootFontSize = (dependencies & 2) != 0 ? RootFontSize(element, false, ref matching) : _metrics.RootFontSize
        };
        return Compute(name, value, metrics);
    }

    // Fonts 4 §2.5 and Values 4 §6.1.1: font-size percentages/em use the parent;
    // rem uses the actual root, except on the root's font-size where it uses the initial size.
    private CssPropertyValue ComputeFontSize(Element element, CssPropertyValue value, double parentSize,
        ref SelectorMatchWork matching)
    {
        if (value.Kind == CssPropertyValueKind.Keyword)
        {
            var initial = value.Text is "larger" or "smaller" ? 0 : InitialFontSize();
            var size = value.Text switch
            {
                "xx-small" => ScaleFontSize(initial, 3, 5),
                "x-small" => ScaleFontSize(initial, 3, 4),
                "small" => ScaleFontSize(initial, 8, 9),
                "medium" => initial,
                "large" => ScaleFontSize(initial, 6, 5),
                "x-large" => ScaleFontSize(initial, 3, 2),
                "xx-large" => ScaleFontSize(initial, 2, 1),
                "xxx-large" => ScaleFontSize(initial, 3, 1),
                "larger" => ScaleFontSize(parentSize, 6, 5),
                "smaller" => ScaleFontSize(parentSize, 5, 6),
                _ => throw new InvalidOperationException("Unvalidated font-size keyword.")
            };
            return Number("font-size", new CssMathNumeric(size, CssNumericKind.Dimension, CssUnit.Px, value.Span));
        }
        var dependencies = FontDependencies(value, true);
        if (dependencies == 0) return Compute("font-size", value, percentageBasis: parentSize);
        var metrics = _metrics with
        {
            FontSize = parentSize,
            RootFontSize = (dependencies & 2) != 0 ? RootFontSize(element, true, ref matching) : _metrics.RootFontSize
        };
        return Compute("font-size", value, metrics, parentSize);
    }

    private double InitialFontSize() => Metric("font-size", _media.InitialFontSize, "initial-font-size");

    // Separate binary exponents so neither multiplication nor division loses a finite
    // representable result before the final scale. Keep zero/nonfinite math on its existing path.
    internal static double ScaleFontSize(double value, double basis, double divisor)
    {
        if (value == 0 || basis == 0 || divisor == 0 ||
            !double.IsFinite(value) || !double.IsFinite(basis) || !double.IsFinite(divisor))
            return value * basis / divisor;
        var valueExponent = System.Math.ILogB(value);
        var basisExponent = System.Math.ILogB(basis);
        var divisorExponent = System.Math.ILogB(divisor);
        var mantissa = System.Math.ScaleB(value, -valueExponent) * System.Math.ScaleB(basis, -basisExponent)
            / System.Math.ScaleB(divisor, -divisorExponent);
        return System.Math.ScaleB(mantissa, valueExponent + basisExponent - divisorExponent);
    }

    private double ComputedFontSize(Element element, ref SelectorMatchWork matching)
    {
        var value = GetProperty(element, "font-size", ref matching).Value!;
        return CssMathNumbers.ParseFinite(value.Numeric.Number, value.Numeric.Unit, _work);
    }

    private double RootFontSize(Element element, bool computingFontSize, ref SelectorMatchWork matching) =>
        _document.DocumentElement is not { } root || computingFontSize && ReferenceEquals(root, element)
            ? InitialFontSize() : ComputedFontSize(root, ref matching);

    // Bit 1: own font size (parent for font-size). Bit 2: root font size.
    // Inspect only typed values; absolute declarations and unrelated properties never warm font ancestors.
    private int FontDependencies(CssPropertyValue value, bool fontSize)
    {
        var result = 0;
        while (value.Kind == CssPropertyValueKind.FitContent)
        {
            _work.Charge(1);
            value = value.Components[0];
        }
        if (fontSize && value is { Kind: CssPropertyValueKind.Keyword, Text: "larger" or "smaller" }) result |= 1;
        if (value.Kind == CssPropertyValueKind.Numeric)
            Observe(value.Numeric.Kind, value.Numeric.Unit);
        else if (value.Kind == CssPropertyValueKind.Math)
        {
            for (var i = 0; i < value.Math.NodeCount; i++)
            {
                _work.Charge(1);
                var node = value.Math.GetNode(i);
                if (node.Kind == CssMathNodeKind.Numeric) Observe(node.Numeric.Kind, node.Numeric.Unit);
            }
        }
        return result;

        void Observe(CssNumericKind kind, CssUnit unit)
        {
            if (unit == CssUnit.Em || fontSize && kind == CssNumericKind.Percentage) result |= 1;
            if (unit == CssUnit.Rem) result |= 2;
        }
    }
}
