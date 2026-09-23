namespace Jint.HtmlParser.Css.Values.Math;

internal enum CssMathProduction
{
    Number, Integer, Percentage, NumberOrPercentage, Length, Angle, Time, Frequency, Resolution, Flex,
    LengthPercentage, AnglePercentage, TimePercentage, FrequencyPercentage, ResolutionPercentage, FlexPercentage
}

internal enum CssMathPercentageMode { Forbidden, Raw, Length, Angle, Time, Frequency, Resolution, Flex }

internal readonly struct CssMathRange
{
    internal CssMathRange(double? lower = null, double? upper = null)
    {
        if (lower is { } lo && !double.IsFinite(lo)) throw new ArgumentOutOfRangeException(nameof(lower));
        if (upper is { } hi && !double.IsFinite(hi)) throw new ArgumentOutOfRangeException(nameof(upper));
        if (lower > upper) throw new ArgumentException("The lower bound exceeds the upper bound.");
        Lower = lower;
        Upper = upper;
    }

    internal double? Lower { get; }
    internal double? Upper { get; }
}

internal readonly struct CssMathContext
{
    private readonly bool _initialized;

    internal CssMathContext(CssMathProduction expected, CssMathPercentageMode percentages,
        CssMathRange range = default, int maximumNestingDepth = 0, int ancestorNestingDepth = 0)
    {
        if (!Enum.IsDefined(expected)) throw new ArgumentOutOfRangeException(nameof(expected));
        if (!Enum.IsDefined(percentages)) throw new ArgumentOutOfRangeException(nameof(percentages));
        ArgumentOutOfRangeException.ThrowIfNegative(maximumNestingDepth);
        ArgumentOutOfRangeException.ThrowIfNegative(ancestorNestingDepth);
        var required = expected switch
        {
            CssMathProduction.Percentage or CssMathProduction.NumberOrPercentage => CssMathPercentageMode.Raw,
            CssMathProduction.LengthPercentage => CssMathPercentageMode.Length,
            CssMathProduction.AnglePercentage => CssMathPercentageMode.Angle,
            CssMathProduction.TimePercentage => CssMathPercentageMode.Time,
            CssMathProduction.FrequencyPercentage => CssMathPercentageMode.Frequency,
            CssMathProduction.ResolutionPercentage => CssMathPercentageMode.Resolution,
            CssMathProduction.FlexPercentage => CssMathPercentageMode.Flex,
            _ => CssMathPercentageMode.Forbidden
        };
        var directWithBasis = expected switch
        {
            CssMathProduction.Number => percentages == CssMathPercentageMode.Raw,
            CssMathProduction.Length => percentages == CssMathPercentageMode.Length,
            CssMathProduction.Angle => percentages == CssMathPercentageMode.Angle,
            CssMathProduction.Time => percentages == CssMathPercentageMode.Time,
            CssMathProduction.Frequency => percentages == CssMathPercentageMode.Frequency,
            CssMathProduction.Resolution => percentages == CssMathPercentageMode.Resolution,
            CssMathProduction.Flex => percentages == CssMathPercentageMode.Flex,
            _ => false
        };
        if (percentages != required && !directWithBasis)
            throw new ArgumentException("Percentage mode is inconsistent with the production.", nameof(percentages));
        _expected = expected;
        _percentages = percentages;
        _range = range;
        _maximumNestingDepth = maximumNestingDepth;
        _ancestorNestingDepth = ancestorNestingDepth;
        _initialized = true;
    }

    internal CssMathProduction Expected { get { Guard(); return _expected; } }
    private readonly CssMathProduction _expected;
    internal CssMathPercentageMode Percentages { get { Guard(); return _percentages; } }
    private readonly CssMathPercentageMode _percentages;
    private readonly CssMathRange _range;
    private readonly int _maximumNestingDepth;
    private readonly int _ancestorNestingDepth;
    internal CssMathRange Range { get { Guard(); return _range; } }
    internal int MaximumNestingDepth { get { Guard(); return _maximumNestingDepth; } }
    internal int AncestorNestingDepth { get { Guard(); return _ancestorNestingDepth; } }
    internal void Guard()
    {
        if (!_initialized) throw new InvalidOperationException("Uninitialized math context.");
    }
}
