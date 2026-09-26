namespace Jint.HtmlParser;

internal sealed partial class HtmlInputValueState
{
    // Raw attribute identities are inexpensive metadata. Exact constraints and
    // coordinates are derived only on demand, never a second current-value store.
    private Attr? _minimumAttribute;
    private Attr? _maximumAttribute;
    private Attr? _stepAttribute;
    private NumericCache? _numeric;

    private sealed class NumericCache
    {
        internal HtmlInputNumericConstraints? Constraints;
        internal HtmlInputNumericFacts? Facts;
        internal double? Number;
        internal HtmlInputDateResult? Date;
    }

    internal bool HasNumericConstraints => _numeric?.Constraints.HasValue ?? false;
    internal bool HasNumericCoordinate => _numeric?.Number.HasValue ?? false;
    internal bool HasDateCoordinate => _numeric?.Date.HasValue ?? false;

    internal static bool IsNumericType(HtmlInputType type) => type is HtmlInputType.Number or HtmlInputType.Range
        || HtmlInputTemporalSyntax.IsTemporal(type);
    private static bool HasDateApi(HtmlInputType type) => type is HtmlInputType.Date or HtmlInputType.Month
        or HtmlInputType.Week or HtmlInputType.Time;

    private static string SanitizeFamily(HtmlInputType type, string value, bool multiple,
        string? minimum, string? maximum, string? step, string? defaultValue,
        Action<int>? checkpoint, CancellationToken cancellationToken)
    {
        if (IsTextType(type)) return HtmlTextSanitizer.SanitizeInput(type, value, multiple, checkpoint, cancellationToken);
        var numericCheckpoint = AdaptCheckpoint(checkpoint);
        if (type == HtmlInputType.Number)
            return HtmlInputNumberSyntax.TryParseValue(value, out _, numericCheckpoint, cancellationToken) ? value : string.Empty;
        if (HtmlInputTemporalSyntax.IsTemporal(type))
            return HtmlInputTemporalSyntax.Sanitize(type, value, numericCheckpoint, cancellationToken);
        if (type == HtmlInputType.Range)
        {
            var constraints = HtmlInputNumericConstraints.Create(type, minimum, maximum, step, defaultValue, numericCheckpoint, cancellationToken);
            var result = HtmlInputRangeValue.Sanitize(value, constraints, numericCheckpoint, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }
        throw new NotSupportedException("This input family has no native value sanitizer.");
    }
    private static Action<long>? AdaptCheckpoint(Action<int>? checkpoint)
        => checkpoint is null ? null : AdaptNonNullCheckpoint(checkpoint);
    private static Action<long> AdaptNonNullCheckpoint(Action<int> checkpoint)
        => units => checkpoint((int) Math.Min(units, int.MaxValue));

    /// <summary>HTML §4.10.5.4 valueAsNumber: inapplicability and failed conversion yield NaN.</summary>
    internal double GetValueAsNumber(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsNumericType(Type)) return double.NaN;
        RequireAvailable();
        if (_numeric?.Number is { } cached) return cached;
        var parsed = HtmlInputNumericConstraints.TryParse(Type, _value!, out var number, cancellationToken)
            ? number : double.NaN;
        cancellationToken.ThrowIfCancellationRequested();
        (_numeric ??= new()).Number = parsed;
        return parsed;
    }

    /// <summary>Infinity precedes applicability; Browser maps this argument error to TypeError.</summary>
    internal void SetValueAsNumber(double value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (double.IsInfinity(value)) throw new ArgumentException("An infinite input value is not permitted.", nameof(value));
        if (!IsNumericType(Type)) throw NumericInvalidState();
        var prepared = double.IsNaN(value) ? string.Empty
            : Type is HtmlInputType.Number or HtmlInputType.Range ? HtmlInputNumberFormatter.FormatFinite(value)
            : HtmlInputTemporalSyntax.FormatNumber(Type, value, cancellationToken);
        SetValue(prepared, cancellationToken);
    }

    /// <summary>Returns a Date slot result, including a present invalid Date outside TimeClip.</summary>
    internal HtmlInputDateResult GetValueAsDate(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!HasDateApi(Type)) return default;
        RequireAvailable();
        if (_numeric?.Date is { } cached) return cached;
        var parsed = HtmlInputTemporalSyntax.GetDate(Type, _value!, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        (_numeric ??= new()).Date = parsed;
        return parsed;
    }

    // Browser owns WebIDL object?/Date-brand order and passes the actual clipped
    // slot or null. No CLR date, object coercion, or user getTime call belongs here.
    internal void SetValueAsDate(double? utcMilliseconds, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!HasDateApi(Type)) throw NumericInvalidState();
        var prepared = utcMilliseconds is { } value && double.IsFinite(value)
            ? HtmlInputTemporalSyntax.FormatDate(Type, value, cancellationToken) : string.Empty;
        SetValue(prepared, cancellationToken);
    }

    internal HtmlInputNumericFacts GetNumericFacts(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsNumericType(Type)) return default;
        RequireAvailable();
        if (_numeric?.Facts is { } cached) return cached;
        var facts = GetNumericConstraints(cancellationToken).GetFacts(_value!, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        _numeric!.Facts = facts;
        return facts;
    }

    private HtmlInputNumericConstraints GetNumericConstraints(CancellationToken cancellationToken, Action<long>? checkpoint = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_numeric?.Constraints is { } cached) return cached;
        var constraints = HtmlInputNumericConstraints.Create(Type, _minimumAttribute?.Value, _maximumAttribute?.Value,
            _stepAttribute?.Value, _valueAttribute?.Value, checkpoint, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        (_numeric ??= new()).Constraints = constraints;
        return constraints;
    }

    internal void StepUp(int count, CancellationToken cancellationToken) => Step(count, false, null, cancellationToken);
    internal void StepDown(int count, CancellationToken cancellationToken) => Step(count, true, null, cancellationToken);
    internal void Step(int count, bool down, Action<long>? checkpoint, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsNumericType(Type)) throw NumericInvalidState();
        RequireAvailable();
        var result = GetNumericConstraints(cancellationToken, checkpoint).GetStep(_value!, count, down, checkpoint, cancellationToken);
        if (result.Status is HtmlInputStepStatus.Inapplicable or HtmlInputStepStatus.NoAllowedStep) throw NumericInvalidState();
        if (result.Status == HtmlInputStepStatus.Unchanged) return;
        // Equal successful writes still dirty; early returns above preserve every flag.
        SetValue(result.Value!, cancellationToken);
    }

    private void InvalidateNumericValue()
    {
        if (_numeric is not { } cache) return;
        cache.Number = null;
        cache.Date = null;
        cache.Facts = null;
    }
    private void InvalidateNumericConstraints()
    {
        if (_numeric is not { } cache) return;
        cache.Constraints = null;
        cache.Facts = null;
    }
    private static DomException NumericInvalidState() => new("InvalidStateError", "This input does not permit the numeric operation.");
}
