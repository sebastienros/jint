using Jint.HtmlParser;
using Jint.HtmlParser.Css;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Selectors;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Colors;
using Jint.HtmlParser.Css.Values.Math;
using Jint.HtmlParser.Css.Values.Properties;
using Jint.HtmlParser.Css.Values.References;

namespace Jint.Browser.Styling;

// An immutable, host-supplied palette. CSS syntax owns no device colors.
internal sealed class NativeCssSystemColors
{
    private readonly (string Name, CssColorValue Color)[] _colors;
    private NativeCssSystemColors((string Name, CssColorValue Color)[] colors) => _colors = colors;
    internal static NativeCssSystemColors Create(ReadOnlySpan<(string Name, CssColorValue Color)> colors, CssValueWork work)
    {
        work.CheckCancellation();
        var copy = new (string Name, CssColorValue Color)[colors.Length];
        for (var i = 0; i < copy.Length; i++)
        {
            work.Charge(1);
            var entry = colors[i];
            var name = CssPropertyRegistry.NormalizeName(entry.Name, work);
            if (CssColorKeywords.ContextualKind(name) is not (CssColorKind.System or CssColorKind.DeprecatedSystem))
                throw new ArgumentException("A palette key must be a system-color identity.", nameof(colors));
            if (entry.Color.Kind is not (CssColorKind.Named or CssColorKind.Transparent or CssColorKind.Absolute))
                throw new ArgumentException("A palette color must be absolute.", nameof(colors));
            for (var j = 0; j < i; j++)
            {
                work.Charge(1);
                if (CssSubstitutionArguments.Equals(name, copy[j].Name, work))
                    throw new ArgumentException("Duplicate palette color.", nameof(colors));
            }
            copy[i] = (name, entry.Color);
        }
        work.CheckCancellation();
        return new(copy);
    }
    internal CssColorValue? Find(string name, CssValueWork work)
    {
        foreach (var entry in _colors)
        {
            work.Charge(1);
            if (CssSubstitutionArguments.Equals(name, entry.Name, work)) return entry.Color;
        }
        work.CheckCancellation();
        return null;
    }
}

internal sealed partial class NativeCssQuery
{
    private CssPropertyValue ComputeColor(Element element, string name, CssPropertyValue value, ref SelectorMatchWork matching)
    {
        var color = value.Color;
        if (color.Kind == CssColorKind.CurrentColor)
        {
            if (name == "color")
                throw new InvalidOperationException("The color currentColor dependency must inherit before computation.");
            return GetProperty(element, "color", ref matching).Value!;
        }
        if (color.Kind is CssColorKind.System or CssColorKind.DeprecatedSystem)
            color = _systemColors?.Find(color.Keyword, _work) ??
                throw new CssIncompleteGrammarException(name, "C6:system-color:" + color.Keyword, color.Span);
        if (color.Kind is CssColorKind.Named or CssColorKind.Transparent)
        {
            var rgb = color.Kind == CssColorKind.Named ? color.NamedRgb : 0;
            color = CssColorValue.Absolute(CssColorSpace.Rgb,
                CssColorChannel.Implicit((rgb >> 16) & 255, color.Span),
                CssColorChannel.Implicit((rgb >> 8) & 255, color.Span),
                CssColorChannel.Implicit(rgb & 255, color.Span),
                CssColorChannel.Implicit(color.Kind == CssColorKind.Transparent ? 0 : 1, color.Span), color.Span, true);
        }
        else if (color.Kind == CssColorKind.Absolute)
        {
            // Specified math has already been evaluated by the color grammar. Computed values retain
            // missing coordinates and percentage coordinates, but serialize evaluated math numerically.
            var channels = new CssColorChannel[4];
            for (var i = 0; i < channels.Length; i++)
            {
                _work.Charge(1);
                var channel = color.GetChannel(i);
                if (channel.Kind == CssColorChannelKind.Math)
                {
                    var numeric = double.IsNaN(channel.Resolved) ? 0 : channel.Resolved;
                    if (double.IsPositiveInfinity(numeric)) numeric = double.MaxValue;
                    if (double.IsNegativeInfinity(numeric)) numeric = -double.MaxValue;
                    var text = CssMathSerializer.SerializeFiniteNumber(numeric, _work);
                    var atom = new CssNumericAtom(channel.IsPercentage ? CssNumericKind.Percentage : CssNumericKind.Number,
                        CssNumber.FromValidatedToken(text, _work), CssUnit.None, false, channel.Span);
                    channel = CssColorChannel.Number(atom, numeric);
                }
                channels[i] = channel;
            }
            color = CssColorValue.Absolute(color.Space, channels[0], channels[1], channels[2], channels[3], color.Span, color.IsLegacySyntax);
        }
        return CssPropertyValue.ColorValue(color, CssColorSerializer.SerializeSpecified(color, _work));
    }
}
