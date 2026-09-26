namespace Jint.HtmlParser;

internal sealed partial class HtmlInputValueState
{
    // These operations read already materialized strings/flags or update bounded
    // offsets. Their checkpoint is invocation-owned and precedes any mutation.
    private static void CheckInvocation(Action<int>? checkpoint, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        checkpoint?.Invoke(1);
        token.ThrowIfCancellationRequested();
    }
    internal string GetValue(Action<int>? checkpoint, CancellationToken token)
    { CheckInvocation(checkpoint, token); return GetValue(token); }
    internal string GetDefaultValue(Action<int>? checkpoint, CancellationToken token)
    { CheckInvocation(checkpoint, token); return GetDefaultValue(token); }
    internal string GetEditingValue(Action<int>? checkpoint, CancellationToken token)
    { CheckInvocation(checkpoint, token); return GetEditingValue(token); }
    internal HtmlInputValueFacts GetFacts(Action<int>? checkpoint, CancellationToken token)
    { CheckInvocation(checkpoint, token); return GetFacts(token); }
    internal uint GetTextLength(Action<int>? checkpoint, CancellationToken token)
        => (uint) GetValue(checkpoint, token).Length;
    internal HtmlTextSelection? GetSelection(Action<int>? checkpoint, CancellationToken token)
    { CheckInvocation(checkpoint, token); return GetSelection(token); }
    internal HtmlTextSelection? GetEditingSelection(Action<int>? checkpoint, CancellationToken token)
    { CheckInvocation(checkpoint, token); return GetEditingSelection(token); }
    internal void SetSelectionStart(uint? value, Action<int>? checkpoint, CancellationToken token)
    { CheckInvocation(checkpoint, token); SetSelectionStart(value, token); }
    internal void SetSelectionEnd(uint? value, Action<int>? checkpoint, CancellationToken token)
    { CheckInvocation(checkpoint, token); SetSelectionEnd(value, token); }
    internal void SetSelectionDirection(string? value, Action<int>? checkpoint, CancellationToken token)
    { CheckInvocation(checkpoint, token); SetSelectionDirection(value, token); }
    internal void SetSelectionRange(uint start, uint end, string? direction, Action<int>? checkpoint, CancellationToken token)
    { CheckInvocation(checkpoint, token); SetSelectionRange(start, end, direction, token); }
    internal bool SetEditingSelection(uint start, uint end, string? direction, Action<int>? checkpoint, CancellationToken token)
    { CheckInvocation(checkpoint, token); return SetEditingSelection(start, end, direction, token); }
    internal void Select(Action<int>? checkpoint, CancellationToken token)
    { CheckInvocation(checkpoint, token); Select(token); }
    internal void SetRangeText(string replacement, Action<int>? checkpoint, CancellationToken token)
        => HtmlInputTextOperations.SetRangeText(this, replacement, checkpoint, token);
    internal void SetRangeText(string replacement, uint start, uint end, HtmlRangeTextMode mode,
        Action<int>? checkpoint, CancellationToken token)
        => HtmlInputTextOperations.SetRangeText(this, replacement, start, end, mode, checkpoint, token);
}
