namespace Jint.HtmlParser;

internal sealed partial class HtmlInputValueState
{
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
        var numericWork = new HtmlInputValueWork(AdaptCheckpoint(checkpoint), cancellationToken, work.Steps,
            cadencedCheckpoint: true);
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
