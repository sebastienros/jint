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
        var work = HtmlInputValueWork.ForNative(checkpoint, cancellationToken);
        var result = SanitizeFamily(type, value, multiple, minimum, maximum, step, defaultValue, ref work);
        work.Finish();
        return result;
    }
    private static string SanitizeFamily(HtmlInputType type, string value, bool multiple,
        string? minimum, string? maximum, string? step, string? defaultValue, ref HtmlInputValueWork work)
    {
        if (IsTextType(type))
        {
            var textWork = new HtmlTextWork(work.Token, work.NativeCheckpoint, unchecked((int) work.Units));
            var result = HtmlTextSanitizer.SanitizeInput(type, value, multiple, ref textWork);
            work.ContinueFrom(textWork.Steps);
            return result;
        }
        if (type == HtmlInputType.Number)
            return HtmlInputNumberSyntax.TryGetNumber(value, true, out _, ref work) == HtmlInputNumericParseResult.Success ? value : string.Empty;
        if (HtmlInputTemporalSyntax.IsTemporal(type))
            return HtmlInputTemporalSyntax.Sanitize(type, value, ref work);
        if (type == HtmlInputType.Range)
        {
            var constraints = HtmlInputNumericConstraints.Create(type, minimum, maximum, step, defaultValue, ref work);
            var result = HtmlInputRangeValue.Sanitize(value, constraints, ref work);
            work.Check();
            return result;
        }
        throw new NotSupportedException("This input family has no native value sanitizer.");
    }

    /// <summary>HTML §4.10.5.4 valueAsNumber: inapplicability and failed conversion yield NaN.</summary>
    internal double GetValueAsNumber(CancellationToken cancellationToken)
        => GetValueAsNumber(null, cancellationToken);
    internal double GetValueAsNumber(Action<int>? checkpoint, CancellationToken cancellationToken)
    {
        var work = HtmlInputValueWork.ForNative(checkpoint, cancellationToken);
        work.Check(); work.Step();
        if (!IsNumericType(Type)) { work.Finish(); return double.NaN; }
        RequireAvailable();
        if (_numeric?.Number is { } cached) { work.Finish(); return cached; }
        var parsed = HtmlInputNumericConstraints.TryParse(Type, _value!, out var number, ref work)
            ? number : double.NaN;
        work.Finish();
        (_numeric ??= new()).Number = parsed;
        return parsed;
    }

    /// <summary>Infinity precedes applicability; Browser maps this argument error to TypeError.</summary>
    internal void SetValueAsNumber(double value, CancellationToken cancellationToken)
        => SetValueAsNumber(value, null, cancellationToken);
    internal void SetValueAsNumber(double value, Action<int>? checkpoint, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (double.IsInfinity(value)) throw new ArgumentException("An infinite input value is not permitted.", nameof(value));
        if (!IsNumericType(Type)) throw NumericInvalidState();
        var prepared = double.IsNaN(value) ? string.Empty
            : Type is HtmlInputType.Number or HtmlInputType.Range ? HtmlInputNumberFormatter.FormatFinite(value)
            : HtmlInputTemporalSyntax.FormatNumber(Type, value, cancellationToken);
        SetValue(prepared, checkpoint, cancellationToken);
    }

    /// <summary>Returns a Date slot result, including a present invalid Date outside TimeClip.</summary>
    internal HtmlInputDateResult GetValueAsDate(CancellationToken cancellationToken)
        => GetValueAsDate(null, cancellationToken);
    internal HtmlInputDateResult GetValueAsDate(Action<int>? checkpoint, CancellationToken cancellationToken)
    {
        var work = HtmlInputValueWork.ForNative(checkpoint, cancellationToken);
        work.Check(); work.Step();
        if (!HasDateApi(Type)) { work.Finish(); return default; }
        RequireAvailable();
        if (_numeric?.Date is { } cached) { work.Finish(); return cached; }
        var parsed = HtmlInputTemporalSyntax.GetDate(Type, _value!, ref work);
        work.Finish();
        (_numeric ??= new()).Date = parsed;
        return parsed;
    }

    // Browser owns WebIDL object?/Date-brand order and passes the actual clipped
    // slot or null. No CLR date, object coercion, or user getTime call belongs here.
    internal void SetValueAsDate(double? utcMilliseconds, CancellationToken cancellationToken)
        => SetValueAsDate(utcMilliseconds, null, cancellationToken);
    internal void SetValueAsDate(double? utcMilliseconds, Action<int>? checkpoint, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!HasDateApi(Type)) throw NumericInvalidState();
        var prepared = utcMilliseconds is { } value && double.IsFinite(value)
            ? HtmlInputTemporalSyntax.FormatDate(Type, value, cancellationToken) : string.Empty;
        SetValue(prepared, checkpoint, cancellationToken);
    }

    internal HtmlInputNumericFacts GetNumericFacts(CancellationToken cancellationToken)
        => GetNumericFacts(null, cancellationToken);
    internal HtmlInputNumericFacts GetNumericFacts(Action<int>? checkpoint, CancellationToken cancellationToken)
    {
        var work = HtmlInputValueWork.ForNative(checkpoint, cancellationToken);
        work.Check(); work.Step();
        if (!IsNumericType(Type)) { work.Finish(); return default; }
        RequireAvailable();
        if (_numeric?.Facts is { } cached) { work.Finish(); return cached; }
        var facts = GetNumericConstraints(ref work).GetFacts(_value!, ref work);
        work.Finish();
        _numeric!.Facts = facts;
        return facts;
    }

    private HtmlInputNumericConstraints GetNumericConstraints(ref HtmlInputValueWork work)
    {
        work.Check();
        if (_numeric?.Constraints is { } cached) return cached;
        var constraints = HtmlInputNumericConstraints.Create(Type, _minimumAttribute?.Value, _maximumAttribute?.Value,
            _stepAttribute?.Value, _valueAttribute?.Value, ref work);
        work.Check();
        (_numeric ??= new()).Constraints = constraints;
        return constraints;
    }

    internal void StepUp(int count, CancellationToken cancellationToken) => Step(count, false, null, cancellationToken);
    internal void StepDown(int count, CancellationToken cancellationToken) => Step(count, true, null, cancellationToken);
    internal void StepUp(int count, Action<int>? checkpoint, CancellationToken cancellationToken)
    {
        var work = HtmlInputValueWork.ForNative(checkpoint, cancellationToken);
        Step(count, false, ref work);
    }
    internal void StepDown(int count, Action<int>? checkpoint, CancellationToken cancellationToken)
    {
        var work = HtmlInputValueWork.ForNative(checkpoint, cancellationToken);
        Step(count, true, ref work);
    }
    internal void Step(int count, bool down, Action<long>? checkpoint, CancellationToken cancellationToken)
    {
        var work = new HtmlInputValueWork(checkpoint, cancellationToken);
        Step(count, down, ref work);
    }
    private void Step(int count, bool down, ref HtmlInputValueWork work)
    {
        work.Check();
        if (!IsNumericType(Type)) throw NumericInvalidState();
        RequireAvailable();
        var result = GetNumericConstraints(ref work).GetStep(_value!, count, down, ref work);
        if (result.Status is HtmlInputStepStatus.Inapplicable or HtmlInputStepStatus.NoAllowedStep) throw NumericInvalidState();
        if (result.Status == HtmlInputStepStatus.Unchanged) { work.Finish(); return; }
        // Equal successful writes still dirty; early returns above preserve every flag.
        SetValue(result.Value!, ref work);
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
