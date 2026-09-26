using Jint.HtmlParser.Css;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Math;
using Jint.HtmlParser.Css.Values.Properties;

namespace Jint.Browser.Styling;

// Explicit host metrics: absence is a named dependency, never a guessed font or container.
internal sealed record NativeCssMetrics
{
    internal double? FontSize { get; init; }
    internal double? RootFontSize { get; init; }
    internal double? XHeight { get; init; }
    internal double? RootXHeight { get; init; }
    internal double? CapHeight { get; init; }
    internal double? RootCapHeight { get; init; }
    internal double? ZeroAdvance { get; init; }
    internal double? RootZeroAdvance { get; init; }
    internal double? IdeographicAdvance { get; init; }
    internal double? RootIdeographicAdvance { get; init; }
    internal double? LineHeight { get; init; }
    internal double? RootLineHeight { get; init; }
    internal bool? VerticalWritingMode { get; init; }
}

internal sealed partial class NativeCssQuery
{
    private CssPropertyValue Compute(string name, CssPropertyValue value, NativeCssMetrics? metrics = null,
        double? percentageBasis = null)
    {
        metrics ??= _metrics;
        if (value.Kind == CssPropertyValueKind.Transform)
            return ComputeTransform(name, value.Transform, metrics, percentageBasis);
        if (name == "font-weight" && value.Kind == CssPropertyValueKind.Keyword)
            return FontWeightNumber(value.Text == "bold" ? 700 : 400, value.Span);
        if (value.Kind == CssPropertyValueKind.FitContent)
            return CssPropertyValue.FitContent(Compute(name, value.Components[0], metrics, percentageBasis), value.Span);
        if (value.Kind == CssPropertyValueKind.Numeric)
        {
            // Integer-token digits remain exact until arithmetic is actually requested.
            if (name == "z-index") return value;
            var atom = value.Numeric;
            var number = CssMathNumbers.ParseFinite(atom.Number, atom.Unit, _work);
            var numeric = new CssMathNumeric(number, atom.Kind, CssMathNumbers.CanonicalUnit(atom.Unit), atom.Span);
            return Number(name, Convert(name, numeric, metrics, percentageBasis));
        }
        if (value.Kind != CssPropertyValueKind.Math) return value;
        var math = value.Math;
        var builder = new CssMathBuilder(_work);
        var mapped = new int[math.NodeCount];
        // Freeze publishes parents before children; reconstitute a postorder builder iteratively.
        for (var i = math.NodeCount - 1; i >= 0; i--)
        {
            _work.Charge(1);
            var node = math.GetNode(i);
            var children = new List<int>(node.ChildCount);
            for (var j = 0; j < node.ChildCount; j++)
            {
                _work.Charge(1);
                children.Add(mapped[math.GetChild(node.ChildStart + j)]);
            }
            var numeric = node.Kind == CssMathNodeKind.Numeric ? Convert(name, node.Numeric, metrics, percentageBasis) : default;
            var type = node.Type;
            if (name is "opacity" or "font-size")
                type = new CssNumericType(type.Length, type.Angle, type.Time, type.Frequency,
                    type.Resolution, type.Flex);
            mapped[i] = builder.Add(node.Kind, type, node.Span, numeric, children,
                node.Kind == CssMathNodeKind.Round ? node.RoundingStrategy : CssRoundingStrategy.Nearest);
        }
        var simplified = CssMathSimplifier.Freeze(builder, mapped[math.RootIndex], math.Context, math.Span, _work);
        var root = simplified.GetNode(simplified.RootIndex);
        if (root.Kind == CssMathNodeKind.Numeric) return Number(name, root.Numeric);
        if (name == "font-size")
            throw new CssIncompleteGrammarException(name, "C6:unresolved-font-size-calculation", value.Span);
        if (name is "opacity" or "z-index" or "flex-grow" or "flex-shrink" or "font-weight" or "scale" or "rotate")
            throw new CssIncompleteGrammarException(name, "C6:unresolved-number-calculation", value.Span);
        return CssPropertyValue.Calculation(simplified, CssMathSerializer.SerializeSpecified(simplified, _work));
    }

    private CssMathNumeric Convert(string name, CssMathNumeric numeric, NativeCssMetrics metrics, double? percentageBasis)
    {
        if (name == "opacity" && numeric.Kind == CssNumericKind.Percentage)
            return new(numeric.Value / 100, CssNumericKind.Number, CssUnit.None, numeric.Span);
        if (name == "font-size" && numeric.Kind == CssNumericKind.Percentage)
            return new(ScaleFontSize(numeric.Value, Metric(name, percentageBasis, "parent-font-size"), 100),
                CssNumericKind.Dimension, CssUnit.Px, numeric.Span);
        if (name == "font-size" && numeric.Kind == CssNumericKind.Number && numeric.Value == 0)
            return new(0, CssNumericKind.Dimension, CssUnit.Px, numeric.Span);
        var unit = numeric.Unit;
        if (numeric.Kind != CssNumericKind.Dimension || unit.Category() != CssUnitCategory.Length || unit == CssUnit.Px)
            return numeric;
        var factor = unit switch
        {
            CssUnit.Em => Metric(name, metrics.FontSize, "font-size"),
            CssUnit.Rem => Metric(name, metrics.RootFontSize, "root-font-size"),
            CssUnit.Ex => Metric(name, _metrics.XHeight, "x-height"),
            CssUnit.Rex => Metric(name, _metrics.RootXHeight, "root-x-height"),
            CssUnit.Cap => Metric(name, _metrics.CapHeight, "cap-height"),
            CssUnit.Rcap => Metric(name, _metrics.RootCapHeight, "root-cap-height"),
            CssUnit.Ch => Metric(name, _metrics.ZeroAdvance, "zero-advance"),
            CssUnit.Rch => Metric(name, _metrics.RootZeroAdvance, "root-zero-advance"),
            CssUnit.Ic => Metric(name, _metrics.IdeographicAdvance, "ideographic-advance"),
            CssUnit.Ric => Metric(name, _metrics.RootIdeographicAdvance, "root-ideographic-advance"),
            CssUnit.Lh => Metric(name, _metrics.LineHeight, "line-height"),
            CssUnit.Rlh => Metric(name, _metrics.RootLineHeight, "root-line-height"),
            CssUnit.Vw or CssUnit.Svw or CssUnit.Lvw or CssUnit.Dvw => _media.Width / 100,
            CssUnit.Vh or CssUnit.Svh or CssUnit.Lvh or CssUnit.Dvh => _media.Height / 100,
            CssUnit.Vmin or CssUnit.Svmin or CssUnit.Lvmin or CssUnit.Dvmin => System.Math.Min(_media.Width, _media.Height) / 100,
            CssUnit.Vmax or CssUnit.Svmax or CssUnit.Lvmax or CssUnit.Dvmax => System.Math.Max(_media.Width, _media.Height) / 100,
            CssUnit.Vi or CssUnit.Svi or CssUnit.Lvi or CssUnit.Dvi => LogicalViewport(name, inline: true),
            CssUnit.Vb or CssUnit.Svb or CssUnit.Lvb or CssUnit.Dvb => LogicalViewport(name, inline: false),
            _ => throw new CssIncompleteGrammarException(name, "C6:container-length", numeric.Span)
        };
        return new(numeric.Value * factor, CssNumericKind.Dimension, CssUnit.Px, numeric.Span);
    }

    private double LogicalViewport(string name, bool inline)
    {
        if (_metrics.VerticalWritingMode is not { } vertical)
            throw new CssIncompleteGrammarException(name, "C6:writing-mode", default);
        return (inline != vertical ? _media.Width : _media.Height) / 100;
    }

    private static double Metric(string name, double? value, string dependency)
    {
        if (value is not { } metric || !double.IsFinite(metric) || metric < 0)
            throw new CssIncompleteGrammarException(name, "C6:" + dependency, default);
        return metric;
    }

    private CssPropertyValue Number(string name, CssMathNumeric numeric)
    {
        var number = numeric.Value;
        // Values 4 §10.12: NaN becomes zero at the top level; infinities clamp to supported range.
        if (double.IsNaN(number)) number = 0;
        else if (double.IsPositiveInfinity(number)) number = double.MaxValue;
        else if (double.IsNegativeInfinity(number)) number = -double.MaxValue;
        if (name == "opacity") number = System.Math.Clamp(number, 0, 1);
        else if (name == "font-weight") number = System.Math.Clamp(number, 1, 1000);
        else if (name is "width" or "height" or "min-width" or "min-height" or "max-width" or "max-height" or
            "padding-top" or "padding-right" or "padding-bottom" or "padding-left" or "flex-basis" or "flex-grow" or "flex-shrink" or "font-size")
            number = System.Math.Max(0, number);
        else if (name == "z-index") number = System.Math.Floor(number + 0.5);
        var text = CssMathSerializer.SerializeFiniteNumber(number, _work);
        var kind = numeric.Kind;
        var unit = numeric.Unit;
        // A math number serializes to a typed atom, not a second parse of the authored value.
        var provenance = CssNumber.FromValidatedToken(text, _work);
        if (kind == CssNumericKind.Percentage) text += "%";
        else if (kind == CssNumericKind.Dimension) text += unit.ToString().ToLowerInvariant();
        _work.Charge(text.Length);
        return CssPropertyValue.Number(new(kind, provenance, unit, kind == CssNumericKind.Number &&
            number == System.Math.Truncate(number), numeric.Span), text);
    }
}
