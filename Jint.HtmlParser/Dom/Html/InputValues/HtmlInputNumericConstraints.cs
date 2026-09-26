namespace Jint.HtmlParser;

internal readonly record struct HtmlInputNumericFacts(bool Applies, bool HasMinimum, bool HasMaximum,
    bool HasReversedRange, bool HasAllowedStep, bool ValueParses, bool Underflow, bool Overflow, bool StepMismatch);

internal enum HtmlInputStepStatus { Write, Unchanged, Inapplicable, NoAllowedStep }
internal readonly record struct HtmlInputStepResult(HtmlInputStepStatus Status, string? Value);

/// <summary>HTML §4.10.5 min/max/step, exact decimal lattice, and ordered stepUp/stepDown algorithms.</summary>
internal readonly struct HtmlInputNumericConstraints
{
    private HtmlInputNumericConstraints(HtmlInputType type, bool applies, HtmlInputDecimal? minimum,
        HtmlInputDecimal? maximum, HtmlInputDecimal? step, HtmlInputDecimal @base)
    { Type = type; Applies = applies; Minimum = minimum; Maximum = maximum; Step = step; Base = @base; }

    internal HtmlInputType Type { get; }
    internal bool Applies { get; }
    internal HtmlInputDecimal? Minimum { get; }
    internal HtmlInputDecimal? Maximum { get; }
    internal HtmlInputDecimal? Step { get; }
    internal HtmlInputDecimal Base { get; }

    internal static HtmlInputNumericConstraints Create(HtmlInputType type, string? minimum, string? maximum,
        string? step, string? defaultValue, CancellationToken cancellationToken = default)
        => Create(type, minimum, maximum, step, defaultValue, null, cancellationToken);

    internal static HtmlInputNumericConstraints Create(HtmlInputType type, string? minimum, string? maximum,
        string? step, string? defaultValue, Action<long>? checkpoint, CancellationToken cancellationToken)
    {
        var work = new HtmlInputValueWork(checkpoint, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var applies = type is HtmlInputType.Number or HtmlInputType.Range || HtmlInputTemporalSyntax.IsTemporal(type);
        if (!applies) return new(type, false, null, null, null, default);
        var parsedMinimum = ParseAttribute(type, minimum, ref work);
        var parsedMaximum = ParseAttribute(type, maximum, ref work);
        var @base = parsedMinimum ?? ParseAttribute(type, defaultValue, ref work)
            ?? HtmlInputDecimal.FromDouble(type == HtmlInputType.Week ? -259200000 : 0);
        if (type == HtmlInputType.Range)
        {
            parsedMinimum ??= HtmlInputDecimal.FromDouble(0);
            parsedMaximum ??= HtmlInputDecimal.FromDouble(100);
        }
        HtmlInputDecimal? allowedStep = null;
        if (!string.Equals(step, "any", StringComparison.OrdinalIgnoreCase))
        {
            var stepValue = type is HtmlInputType.Time or HtmlInputType.DateTimeLocal ? 60d : 1d;
            if (step is not null && HtmlInputNumberSyntax.TryGetNumber(step, false, out var parsed,
                ref work) == HtmlInputNumericParseResult.Success && parsed > 0) stepValue = parsed;
            var scale = type switch
            {
                HtmlInputType.Date => 86400000,
                HtmlInputType.Week => 604800000,
                HtmlInputType.Time or HtmlInputType.DateTimeLocal => 1000,
                _ => 1
            };
            allowedStep = HtmlInputDecimal.FromDouble(stepValue).Multiply(scale);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return new(type, true, parsedMinimum, parsedMaximum, allowedStep, @base);
    }

    private static HtmlInputDecimal? ParseAttribute(HtmlInputType type, string? source, ref HtmlInputValueWork work)
        => source is not null && TryParse(type, source, out var number, ref work)
            ? HtmlInputDecimal.FromDouble(number) : null;

    internal static bool TryParse(HtmlInputType type, string source, out double number, CancellationToken cancellationToken = default)
        => TryParse(type, source, out number, null, cancellationToken);
    internal static bool TryParse(HtmlInputType type, string source, out double number, Action<long>? checkpoint,
        CancellationToken cancellationToken)
    {
        var work = new HtmlInputValueWork(checkpoint, cancellationToken);
        return TryParse(type, source, out number, ref work);
    }
    private static bool TryParse(HtmlInputType type, string source, out double number, ref HtmlInputValueWork work)
    {
        if (type is HtmlInputType.Number or HtmlInputType.Range)
            return HtmlInputNumberSyntax.TryGetNumber(source, false, out number, ref work) == HtmlInputNumericParseResult.Success;
        return HtmlInputTemporalSyntax.TryGetNumber(type, source, out number, ref work) == HtmlInputNumericParseResult.Success;
    }

    internal HtmlInputNumericFacts GetFacts(string value, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        cancellationToken.ThrowIfCancellationRequested();
        var reversed = Type == HtmlInputType.Time && Minimum is { } min && Maximum is { } max && min.CompareTo(max) > 0;
        var number = 0d;
        var parses = Applies && TryParse(Type, value, out number, cancellationToken);
        var underflow = false;
        var overflow = false;
        var mismatch = false;
        if (parses)
        {
            var current = HtmlInputDecimal.FromDouble(number);
            underflow = Minimum is { } lower && current.CompareTo(lower) < 0;
            overflow = Maximum is { } upper && current.CompareTo(upper) > 0;
            if (reversed) underflow = overflow = underflow && overflow;
            mismatch = IsMismatch(current);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return new(Applies, Minimum.HasValue, Maximum.HasValue, reversed, Step.HasValue, parses, underflow, overflow, mismatch);
    }

    internal bool IsMismatch(HtmlInputDecimal value)
    {
        if (Step is not { } step) return false;
        value.Subtract(Base).FloorQuotient(step, out var exact);
        return !exact;
    }

    internal HtmlInputDecimal AlignUp(HtmlInputDecimal value)
    {
        var step = Step!.Value;
        var quotient = value.Subtract(Base).FloorQuotient(step, out var exact);
        return Base.Add(step.Multiply(exact ? quotient : quotient + 1));
    }

    internal HtmlInputDecimal AlignDown(HtmlInputDecimal value)
    {
        var step = Step!.Value;
        var quotient = value.Subtract(Base).FloorQuotient(step, out _);
        return Base.Add(step.Multiply(quotient));
    }

    internal bool HasGridPoint()
        => Minimum is not { } min || Maximum is not { } max || AlignUp(min).CompareTo(max) <= 0;

    internal HtmlInputStepResult GetStep(string value, int count, bool down, CancellationToken cancellationToken = default)
        => GetStep(value, count, down, null, cancellationToken);

    internal HtmlInputStepResult GetStep(string value, int count, bool down, Action<long>? checkpoint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value);
        var work = new HtmlInputValueWork(checkpoint, cancellationToken);
        work.Check();
        if (!Applies) return new(HtmlInputStepStatus.Inapplicable, null);
        if (Step is not { } step) return new(HtmlInputStepStatus.NoAllowedStep, null);
        work.Step(); work.Check();
        if ((Minimum is { } minimum && Maximum is { } maximum && minimum.CompareTo(maximum) > 0) || !HasGridPoint())
            return new(HtmlInputStepStatus.Unchanged, null);
        work.Step(); work.Check();
        var before = HtmlInputDecimal.FromDouble(TryParse(Type, value, out var number, ref work) ? number : 0);
        work.Step(); work.Check();
        var candidate = IsMismatch(before)
            ? down ? AlignDown(before) : AlignUp(before)
            : before.Add(step.Multiply(down ? -(long) count : count));
        work.Step(); work.Check();
        if (Minimum is { } min && candidate.CompareTo(min) < 0) candidate = AlignUp(min);
        if (Maximum is { } max && candidate.CompareTo(max) > 0) candidate = AlignDown(max);
        work.Step(); work.Check();
        var direction = candidate.CompareTo(before);
        if ((down && direction > 0) || (!down && direction < 0) || !candidate.TryPublish(out var published))
            return new(HtmlInputStepStatus.Unchanged, null);
        work.Step(); work.Check();
        var text = Type is HtmlInputType.Number or HtmlInputType.Range
            ? HtmlInputNumberFormatter.FormatFinite(published)
            : candidate.TryFloorInt64(out var integer)
                ? HtmlInputTemporalSyntax.FormatInteger(Type, integer, cancellationToken)
                : HtmlInputTemporalSyntax.FormatInteger(Type, candidate.FloorInteger(), cancellationToken);
        work.Step(); work.Check();
        return new(HtmlInputStepStatus.Write, text);
    }
}
