namespace Jint.HtmlParser;

internal sealed partial class HtmlInputValueState
{
    // https://html.spec.whatwg.org/multipage/input.html#common-input-element-events
    // Picker controls commit a whole sanitized value, without a text selection.
    internal bool ApplyPickerUserValue(string value, Action<int>? checkpoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value);
        var work = HtmlInputValueWork.ForNative(checkpoint, cancellationToken);
        work.Check(); work.Step();
        if (!IsAvailable || ReadOnly || Type is not (HtmlInputType.Date or HtmlInputType.Month
            or HtmlInputType.Week or HtmlInputType.Time or HtmlInputType.DateTimeLocal))
        {
            work.Finish();
            return false;
        }
        if (HtmlDisabledness.GetState(Element, checkpoint, cancellationToken) != HtmlDisabledState.Enabled)
        {
            work.Finish();
            return false;
        }
        var prepared = Sanitize(value, ref work);
        var valid = value.Length == 0 || prepared.Length != 0;
        var changed = !work.StringEquals(_value!, prepared);
        work.Finish();
        if (!valid) throw new ArgumentException("The value is not valid for this input type.", nameof(value));
        CommitValue(prepared, HtmlValueChangeOrigin.User, changed);
        SetDirty(true);
        return true;
    }

    // Optional presentation only when it differs from _value. API/submission
    // value and UTF-16 selection remain the existing authoritative stores.
    private string? _numberPresentation;
    internal bool BadInput => IsAvailable && Type == HtmlInputType.Number
        && _numberPresentation is { Length: > 0 } && _value!.Length == 0;

    internal string GetEditingValue(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequireAvailable();
        return Type == HtmlInputType.Number ? _numberPresentation ?? _value! : GetValue(cancellationToken);
    }

    internal bool ApplyNumberUserValue(string value, HtmlTextSelection selection, Action<int>? checkpoint,
        CancellationToken cancellationToken)
    {
        var work = new HtmlTextWork(cancellationToken, checkpoint);
        work.Check(); work.Step();
        if (!HasEditingBuffer || ReadOnly) { work.Finish(); return false; }
        var disabledWork = new HtmlDisabledWork(cancellationToken, checkpoint, work.Steps);
        var disabled = HtmlDisabledness.GetState(Element, ref disabledWork);
        work.ContinueFrom(disabledWork.Steps);
        if (disabled != HtmlDisabledState.Enabled) { work.Finish(); return false; }

        // The UI accepts partial strings, but API value accepts only the full
        // strict finite grammar. No prefix conversion can make "1e" into 1.
        var numericWork = HtmlInputValueWork.ForNative(checkpoint, cancellationToken, work.Steps);
        var parsed = HtmlInputNumberSyntax.TryGetNumber(value, true, out var number, ref numericWork);
        numericWork.Step(); numericWork.Check();
        var prepared = parsed == HtmlInputNumericParseResult.Success ? HtmlInputNumberFormatter.FormatFinite(number) : string.Empty;
        numericWork.Step(); numericWork.Check();
        work.ContinueFrom(unchecked((int) numericWork.Units));
        var presentation = work.StringEquals(value, prepared) ? null : value;
        var changed = !work.StringEquals(_value!, prepared);
        var displayChanged = !work.StringEquals(GetEditingValue(cancellationToken), value);
        work.Step();
        var next = HtmlInputTextOperations.Normalize(selection.Start, selection.End,
            HtmlInputTextOperations.DirectionString(selection.Direction), (uint) value.Length);
        work.Finish();

        if (changed || displayChanged)
        {
            CommitValue(prepared, HtmlValueChangeOrigin.User, changed);
            _numberPresentation = presentation;
            // Changing an incomplete display can be observable while API stays
            // empty. It still dirties and invalidates derived value facts.
            if (displayChanged) { InvalidateNumericValue(); MarkChanged(); }
            SetDirty(true);
        }
        SetSelection(next);
        return true;
    }

    private void ClearNumberPresentation()
    {
        if (_numberPresentation is null) return;
        _numberPresentation = null;
        MarkChanged();
    }
}
