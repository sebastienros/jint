namespace Jint.HtmlParser.Css.Values.Math;

internal enum CssPercentHint { None, Length, Angle, Time, Frequency, Resolution, Flex, Percent }

// CSS Typed OM §3.5 numeric types, Editor's Draft checked 2026-09-23.
internal readonly struct CssNumericType : IEquatable<CssNumericType>
{
    internal CssNumericType(int length = 0, int angle = 0, int time = 0, int frequency = 0,
        int resolution = 0, int flex = 0, int percent = 0, CssPercentHint hint = CssPercentHint.None)
    {
        Length = length; Angle = angle; Time = time; Frequency = frequency;
        Resolution = resolution; Flex = flex; Percent = percent; Hint = hint;
    }

    internal int Length { get; }
    internal int Angle { get; }
    internal int Time { get; }
    internal int Frequency { get; }
    internal int Resolution { get; }
    internal int Flex { get; }
    internal int Percent { get; }
    internal CssPercentHint Hint { get; }
    internal bool IsScalar => Length == 0 && Angle == 0 && Time == 0 && Frequency == 0 &&
        Resolution == 0 && Flex == 0 && Percent == 0;

    internal static CssNumericType FromUnit(CssUnit unit) => unit.Category() switch
    {
        CssUnitCategory.Length => new(length: 1),
        CssUnitCategory.Angle => new(angle: 1),
        CssUnitCategory.Time => new(time: 1),
        CssUnitCategory.Frequency => new(frequency: 1),
        CssUnitCategory.Resolution => new(resolution: 1),
        CssUnitCategory.Flex => new(flex: 1),
        _ => default
    };

    internal static CssNumericType Percentage(CssMathPercentageMode mode) => mode switch
    {
        CssMathPercentageMode.Length => new(length: 1, hint: CssPercentHint.Length),
        CssMathPercentageMode.Angle => new(angle: 1, hint: CssPercentHint.Angle),
        CssMathPercentageMode.Time => new(time: 1, hint: CssPercentHint.Time),
        CssMathPercentageMode.Frequency => new(frequency: 1, hint: CssPercentHint.Frequency),
        CssMathPercentageMode.Resolution => new(resolution: 1, hint: CssPercentHint.Resolution),
        CssMathPercentageMode.Flex => new(flex: 1, hint: CssPercentHint.Flex),
        _ => new(percent: 1, hint: CssPercentHint.Percent)
    };

    internal bool TryAdd(CssNumericType other, out CssNumericType result)
    {
        result = default;
        if (Hint != CssPercentHint.None && other.Hint != CssPercentHint.None && Hint != other.Hint) return false;
        var hint = Hint == CssPercentHint.None ? other.Hint : Hint;
        var left = ApplyHint(hint);
        var right = other.ApplyHint(hint);
        if (left.Length != right.Length || left.Angle != right.Angle || left.Time != right.Time ||
            left.Frequency != right.Frequency || left.Resolution != right.Resolution ||
            left.Flex != right.Flex || left.Percent != right.Percent) return false;
        result = new(left.Length, left.Angle, left.Time, left.Frequency, left.Resolution,
            left.Flex, left.Percent, hint);
        return true;
    }

    // CSS Values 4 §10.9: make a result type consistent with its input type.
    // Unlike addition, the input's dimensions do not become result dimensions.
    internal bool TryMakeConsistent(CssNumericType input, out CssNumericType result)
    {
        result = default;
        if (Hint != CssPercentHint.None && input.Hint != CssPercentHint.None && Hint != input.Hint)
            return false;
        result = new CssNumericType(Length, Angle, Time, Frequency, Resolution, Flex, Percent,
            Hint == CssPercentHint.None ? input.Hint : Hint);
        return true;
    }

    internal bool TryMultiply(CssNumericType other, out CssNumericType result)
    {
        result = default;
        if (Hint != CssPercentHint.None && other.Hint != CssPercentHint.None && Hint != other.Hint) return false;
        var hint = Hint == CssPercentHint.None ? other.Hint : Hint;
        var left = ApplyHint(hint);
        var right = other.ApplyHint(hint);
        result = new(checked(left.Length + right.Length), checked(left.Angle + right.Angle),
            checked(left.Time + right.Time), checked(left.Frequency + right.Frequency),
            checked(left.Resolution + right.Resolution), checked(left.Flex + right.Flex),
            checked(left.Percent + right.Percent), hint);
        return true;
    }

    internal CssNumericType Invert() => new(checked(-Length), checked(-Angle), checked(-Time),
        checked(-Frequency), checked(-Resolution), checked(-Flex), checked(-Percent), Hint);

    private CssNumericType ApplyHint(CssPercentHint hint) => hint switch
    {
        CssPercentHint.Length when Hint == CssPercentHint.None && Length == 0 => new(0, Angle, Time, Frequency, Resolution, Flex, Percent, hint),
        _ => new(Length, Angle, Time, Frequency, Resolution, Flex, Percent, hint)
    };

    internal bool Matches(CssMathContext context)
    {
        context.Guard();
        var hintOk = context.Percentages switch
        {
            CssMathPercentageMode.Forbidden => Hint == CssPercentHint.None,
            CssMathPercentageMode.Raw => Hint is CssPercentHint.None or CssPercentHint.Percent,
            CssMathPercentageMode.Length => Hint is CssPercentHint.None or CssPercentHint.Length,
            CssMathPercentageMode.Angle => Hint is CssPercentHint.None or CssPercentHint.Angle,
            CssMathPercentageMode.Time => Hint is CssPercentHint.None or CssPercentHint.Time,
            CssMathPercentageMode.Frequency => Hint is CssPercentHint.None or CssPercentHint.Frequency,
            CssMathPercentageMode.Resolution => Hint is CssPercentHint.None or CssPercentHint.Resolution,
            CssMathPercentageMode.Flex => Hint is CssPercentHint.None or CssPercentHint.Flex,
            _ => false
        };
        if (!hintOk) return false;
        var dimension = context.Expected switch
        {
            CssMathProduction.Number or CssMathProduction.Integer => 0,
            CssMathProduction.Percentage => 7,
            CssMathProduction.NumberOrPercentage => Percent == 1 ? 7 : 0,
            CssMathProduction.Length or CssMathProduction.LengthPercentage => 1,
            CssMathProduction.Angle or CssMathProduction.AnglePercentage => 2,
            CssMathProduction.Time or CssMathProduction.TimePercentage => 3,
            CssMathProduction.Frequency or CssMathProduction.FrequencyPercentage => 4,
            CssMathProduction.Resolution or CssMathProduction.ResolutionPercentage => 5,
            CssMathProduction.Flex or CssMathProduction.FlexPercentage => 6,
            _ => -1
        };
        if (context.Expected is CssMathProduction.LengthPercentage or CssMathProduction.AnglePercentage or
            CssMathProduction.TimePercentage or CssMathProduction.FrequencyPercentage or
            CssMathProduction.ResolutionPercentage or CssMathProduction.FlexPercentage)
        {
            if (Percent == 1 && Length == 0 && Angle == 0 && Time == 0 && Frequency == 0 && Resolution == 0 && Flex == 0)
                return true;
        }
        return Length == (dimension == 1 ? 1 : 0) && Angle == (dimension == 2 ? 1 : 0) &&
            Time == (dimension == 3 ? 1 : 0) && Frequency == (dimension == 4 ? 1 : 0) &&
            Resolution == (dimension == 5 ? 1 : 0) && Flex == (dimension == 6 ? 1 : 0) &&
            Percent == (dimension == 7 ? 1 : 0);
    }

    internal bool IsPermissibleScalar => IsScalar ||
        (Length == 1 && Angle == 0 && Time == 0 && Frequency == 0 && Resolution == 0 && Flex == 0 && Percent == 0) ||
        (Angle == 1 && Length == 0 && Time == 0 && Frequency == 0 && Resolution == 0 && Flex == 0 && Percent == 0) ||
        (Time == 1 && Length == 0 && Angle == 0 && Frequency == 0 && Resolution == 0 && Flex == 0 && Percent == 0) ||
        (Frequency == 1 && Length == 0 && Angle == 0 && Time == 0 && Resolution == 0 && Flex == 0 && Percent == 0) ||
        (Resolution == 1 && Length == 0 && Angle == 0 && Time == 0 && Frequency == 0 && Flex == 0 && Percent == 0) ||
        (Flex == 1 && Length == 0 && Angle == 0 && Time == 0 && Frequency == 0 && Resolution == 0 && Percent == 0) ||
        (Percent == 1 && Length == 0 && Angle == 0 && Time == 0 && Frequency == 0 && Resolution == 0 && Flex == 0);

    public bool Equals(CssNumericType other) => Length == other.Length && Angle == other.Angle &&
        Time == other.Time && Frequency == other.Frequency && Resolution == other.Resolution &&
        Flex == other.Flex && Percent == other.Percent && Hint == other.Hint;
    public override bool Equals(object? obj) => obj is CssNumericType other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Length, Angle, Time, Frequency, Resolution, Flex, Percent, Hint);
}
