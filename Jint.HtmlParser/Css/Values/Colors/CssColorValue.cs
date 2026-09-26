using Jint.HtmlParser.Css.Values.Math;

namespace Jint.HtmlParser.Css.Values.Colors;

internal enum CssColorKind { Named, Transparent, CurrentColor, System, DeprecatedSystem, Absolute }
internal enum CssColorSpace { Rgb, Hsl, Hwb, Srgb }
internal enum CssColorChannelKind { Uninitialized, Numeric, Math, Missing, Implicit }
internal enum CssColorParseStatus { Uninitialized, Match, NoMatch, RequiresLaterGrammar }

// Channels keep their typed specified input even when Color 4 §15.1 serialization
// resolves it. Percentages are still in their original percentage coordinate here.
internal readonly struct CssColorChannel
{
    private readonly CssNumericAtom _numeric;
    private readonly CssMathValue? _math;
    private readonly double _resolved;
    private readonly bool _percentage;
    private CssColorChannel(CssColorChannelKind kind, double resolved, bool percentage,
        CssSourceSpan span, CssNumericAtom numeric = default, CssMathValue? math = null)
    { Kind = kind; _resolved = resolved; _percentage = percentage; Span = span; _numeric = numeric; _math = math; }
    internal CssColorChannelKind Kind { get; }
    internal CssSourceSpan Span { get; }
    internal double Resolved => Kind != CssColorChannelKind.Uninitialized ? _resolved : throw new InvalidOperationException();
    internal bool IsPercentage => Kind != CssColorChannelKind.Uninitialized ? _percentage : throw new InvalidOperationException();
    internal CssNumericAtom Numeric => Kind == CssColorChannelKind.Numeric ? _numeric : throw new InvalidOperationException();
    internal CssMathValue Math => Kind == CssColorChannelKind.Math ? _math! : throw new InvalidOperationException();
    internal static CssColorChannel Number(CssNumericAtom numeric, double resolved) =>
        new(CssColorChannelKind.Numeric, resolved, numeric.Kind == CssNumericKind.Percentage, numeric.Span, numeric);
    internal static CssColorChannel Calculation(CssMathValue math, double resolved) =>
        new(CssColorChannelKind.Math, resolved, math.Type.Percent == 1, math.Span, math: math);
    internal static CssColorChannel Missing(CssSourceSpan span) => new(CssColorChannelKind.Missing, 0, false, span);
    internal static CssColorChannel Implicit(double value, CssSourceSpan span) => new(CssColorChannelKind.Implicit, value, false, span);
}

internal sealed class CssColorValue
{
    private readonly string? _keyword;
    private readonly uint _namedRgb;
    private readonly CssColorSpace _space;
    private readonly CssColorChannel _first, _second, _third, _alpha;
    private CssColorValue(CssColorKind kind, CssSourceSpan span, string? keyword = null, uint namedRgb = 0,
        CssColorSpace space = default, CssColorChannel first = default, CssColorChannel second = default,
        CssColorChannel third = default, CssColorChannel alpha = default, bool legacy = false)
    {
        Kind = kind; Span = span; _keyword = keyword; _namedRgb = namedRgb; _space = space;
        _first = first; _second = second; _third = third; _alpha = alpha; IsLegacySyntax = legacy;
    }
    internal CssColorKind Kind { get; }
    internal CssSourceSpan Span { get; }
    internal bool IsLegacySyntax { get; }
    internal string Keyword => Kind != CssColorKind.Absolute ? _keyword! : throw new InvalidOperationException();
    internal uint NamedRgb => Kind == CssColorKind.Named ? _namedRgb : throw new InvalidOperationException();
    internal CssColorSpace Space => Kind == CssColorKind.Absolute ? _space : throw new InvalidOperationException();
    internal CssColorChannel GetChannel(int index) => Kind == CssColorKind.Absolute ? index switch
    {
        0 => _first,
        1 => _second,
        2 => _third,
        3 => _alpha,
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    } : throw new InvalidOperationException();
    internal static CssColorValue Identity(CssColorKind kind, string keyword, CssSourceSpan span, uint namedRgb = 0) =>
        new(kind, span, keyword, namedRgb);
    internal static CssColorValue Absolute(CssColorSpace space, CssColorChannel first, CssColorChannel second,
        CssColorChannel third, CssColorChannel alpha, CssSourceSpan span, bool legacy) =>
        new(CssColorKind.Absolute, span, space: space, first: first, second: second, third: third, alpha: alpha, legacy: legacy);
}

internal readonly struct CssColorParseResult
{
    private readonly CssColorValue? _value;
    private CssColorParseResult(CssColorParseStatus status, CssColorValue? value = null, string? blocker = null)
    { Status = status; _value = value; Blocker = blocker; }
    internal CssColorParseStatus Status { get; }
    internal string? Blocker { get; }
    internal CssColorValue Value => Status == CssColorParseStatus.Match ? _value! : throw new InvalidOperationException();
    internal static CssColorParseResult Match(CssColorValue value) => new(CssColorParseStatus.Match, value);
    internal static CssColorParseResult NoMatch() => new(CssColorParseStatus.NoMatch);
    internal static CssColorParseResult Pending(string blocker) => new(CssColorParseStatus.RequiresLaterGrammar, blocker: blocker);
}
