namespace Jint.HtmlParser;

/// <summary>HTML §4.10.20 text selection/replacement, using the input's one UTF-16 selection.</summary>
internal static class HtmlInputTextOperations
{
    internal static HtmlTextSelection? GetSelection(HtmlInputValueState state, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        cancellationToken.ThrowIfCancellationRequested();
        if (!state.HasSelectionApi) return null;
        state.RequireAvailable();
        return state.Selection;
    }

    internal static void SetSelectionStart(HtmlInputValueState state, uint? value, CancellationToken cancellationToken)
    {
        RequirePublicSelection(state, cancellationToken);
        var start = Math.Min(value ?? 0, state.GetTextLength(cancellationToken));
        SetSelectionRange(state, start, Math.Max(start, state.Selection.End), DirectionString(state.Selection.Direction), cancellationToken);
    }
    internal static void SetSelectionEnd(HtmlInputValueState state, uint? value, CancellationToken cancellationToken)
    {
        RequirePublicSelection(state, cancellationToken);
        var end = Math.Min(value ?? 0, state.GetTextLength(cancellationToken));
        SetSelectionRange(state, Math.Min(state.Selection.Start, end), end, DirectionString(state.Selection.Direction), cancellationToken);
    }
    internal static void SetSelectionDirection(HtmlInputValueState state, string? direction, CancellationToken cancellationToken)
    {
        RequirePublicSelection(state, cancellationToken);
        SetSelectionRange(state, state.Selection.Start, state.Selection.End, direction, cancellationToken);
    }
    internal static void SetSelectionRange(HtmlInputValueState state, uint start, uint end, string? direction,
        CancellationToken cancellationToken)
    {
        RequirePublicSelection(state, cancellationToken);
        var next = Normalize(start, end, direction, state.GetTextLength(cancellationToken));
        cancellationToken.ThrowIfCancellationRequested();
        state.SetSelection(next);
    }
    internal static void Select(HtmlInputValueState state, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        cancellationToken.ThrowIfCancellationRequested();
        if (!HtmlInputValueState.IsTextType(state.Type))
        {
            if (HtmlInputValueState.IsSupportedType(state.Type) || !HtmlInputTypes.Info(state.Type).SelectApplies) return;
            state.RequireAvailable();
            return;
        }
        state.RequireAvailable();
        state.SetSelection(Normalize(0, state.GetTextLength(cancellationToken), null, state.GetTextLength(cancellationToken)));
    }

    internal static void SetRangeText(HtmlInputValueState state, string replacement, CancellationToken cancellationToken)
    {
        RequirePublicSelection(state, cancellationToken);
        SetRangeText(state, replacement, state.Selection.Start, state.Selection.End, HtmlRangeTextMode.Preserve, cancellationToken);
    }
    internal static void SetRangeText(HtmlInputValueState state, string replacement, uint start, uint end,
        HtmlRangeTextMode mode, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        RequirePublicSelection(state, cancellationToken);
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        if (start > end)
        {
            state.SetDirty(true);
            throw new DomException("IndexSizeError", "The start offset exceeds the end offset.");
        }
        var oldValue = state.GetValue(cancellationToken);
        var oldSelection = state.Selection;
        start = Math.Min(start, (uint) oldValue.Length);
        end = Math.Min(end, (uint) oldValue.Length);
        var length = checked((long) oldValue.Length - (end - start) + replacement.Length);
        cancellationToken.ThrowIfCancellationRequested();
        var spliced = string.Create(checked((int) length),
            (oldValue, replacement, start, end, cancellationToken), static (span, input) =>
        {
            var work = new HtmlTextWork(input.cancellationToken);
            var destination = 0;
            for (var i = 0; i < input.start; i++) { work.Step(); span[destination++] = input.oldValue[i]; }
            foreach (var character in input.replacement) { work.Step(); span[destination++] = character; }
            for (var i = (int) input.end; i < input.oldValue.Length; i++) { work.Step(); span[destination++] = input.oldValue[i]; }
            work.Check();
        });
        var prepared = state.Sanitize(spliced, cancellationToken);
        var insertedEnd = checked((long) start + replacement.Length);
        var delta = checked((long) replacement.Length - (end - start));
        var next = mode switch
        {
            HtmlRangeTextMode.Select => new HtmlTextSelection(start, checked((uint) insertedEnd), HtmlSelectionDirection.None),
            HtmlRangeTextMode.Start => new HtmlTextSelection(start, start, HtmlSelectionDirection.None),
            HtmlRangeTextMode.End => new HtmlTextSelection(checked((uint) insertedEnd), checked((uint) insertedEnd), HtmlSelectionDirection.None),
            HtmlRangeTextMode.Preserve => new HtmlTextSelection(MapEndpoint(oldSelection.Start, start, end, insertedEnd, delta, false),
                MapEndpoint(oldSelection.End, start, end, insertedEnd, delta, true), HtmlSelectionDirection.None),
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
        cancellationToken.ThrowIfCancellationRequested();
        state.CommitValue(prepared, HtmlValueChangeOrigin.NonUser);
        state.SetDirty(true);
        state.ClampSelection((uint) prepared.Length);
        state.SetSelection(Normalize(next.Start, next.End, null, (uint) prepared.Length));
    }

    internal static HtmlTextSelection? GetEditingSelection(HtmlInputValueState state, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        cancellationToken.ThrowIfCancellationRequested();
        return state.HasTextBuffer ? state.Selection : null;
    }
    internal static bool SetEditingSelection(HtmlInputValueState state, uint start, uint end, string? direction,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        cancellationToken.ThrowIfCancellationRequested();
        if (!state.HasTextBuffer) return false;
        var next = Normalize(start, end, direction, state.GetTextLength(cancellationToken));
        cancellationToken.ThrowIfCancellationRequested();
        state.SetSelection(next);
        return true;
    }
    internal static bool ApplyUserValue(HtmlInputValueState state, string value, HtmlTextSelection selection,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(value);
        cancellationToken.ThrowIfCancellationRequested();
        if (!state.HasTextBuffer || state.ReadOnly ||
            state.Element.GetHtmlState()!.GetDisabledState(cancellationToken) != HtmlDisabledState.Enabled) return false;
        var prepared = state.Sanitize(value, cancellationToken);
        var changed = !string.Equals(state.GetValue(cancellationToken), prepared, StringComparison.Ordinal);
        var next = Normalize(selection.Start, selection.End, DirectionString(selection.Direction), (uint) prepared.Length);
        cancellationToken.ThrowIfCancellationRequested();
        if (changed)
        {
            state.CommitValue(prepared, HtmlValueChangeOrigin.User);
            state.SetDirty(true);
            state.ClampSelection((uint) prepared.Length);
        }
        state.SetSelection(next);
        return true;
    }

    private static void RequirePublicSelection(HtmlInputValueState state, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        cancellationToken.ThrowIfCancellationRequested();
        if (!state.HasSelectionApi) throw new DomException("InvalidStateError", "This input type does not support public text selection.");
        state.RequireAvailable();
    }
    internal static HtmlTextSelection Normalize(uint start, uint end, string? direction, uint length)
    {
        end = Math.Min(end, length);
        start = Math.Min(start, end);
        return new(start, end, direction switch
        {
            "forward" => HtmlSelectionDirection.Forward,
            "backward" => HtmlSelectionDirection.Backward,
            _ => HtmlSelectionDirection.None
        });
    }
    internal static string? DirectionString(HtmlSelectionDirection direction) => direction switch
    {
        HtmlSelectionDirection.Forward => "forward",
        HtmlSelectionDirection.Backward => "backward",
        _ => null
    };
    private static uint MapEndpoint(uint old, uint start, uint end, long insertedEnd, long delta, bool isEnd)
    {
        if (old > end) return checked((uint) ((long) old + delta));
        if (old > start) return checked((uint) (isEnd ? insertedEnd : start));
        return old;
    }
}
