namespace Jint.HtmlParser;

/// <summary>HTML Living Standard §4.10.11 textarea value, reset, selection and replacement state.</summary>
internal sealed class HtmlTextAreaState
{
    private readonly Element _element;
    private string _rawValue = string.Empty;
    private bool _rawFromChildren = true;
    private bool _rawAlignedWithChildren = true;
    private long _rawRevision;
    private long _apiRevision = -1;
    private string? _apiValue;
    private HtmlTextSelection _selection;

    internal HtmlTextAreaState(Element element) => _element = element;

    internal bool DirtyValue { get; private set; }
    internal HtmlValueChangeOrigin LastValueChangeOrigin { get; private set; }
    internal bool UserValidity { get; private set; }
    internal HtmlTextSelection Selection => _selection;

    internal void SetUserValidity(bool value)
    {
        if (UserValidity == value) return;
        UserValidity = value;
        MarkStateChange();
    }

    internal string GetDefaultValue(CancellationToken cancellationToken)
        => HtmlTextAreaMutations.CollectChildText(_element, cancellationToken);

    internal string GetValue(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_apiRevision == _rawRevision && _apiValue is { } cached) return cached;
        var raw = GetRawValue(cancellationToken);
        var api = HtmlTextSanitizer.NormalizeTextAreaValue(raw, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        _apiValue = api;
        _apiRevision = _rawRevision;
        return api;
    }

    internal uint GetTextLength(CancellationToken cancellationToken)
        => (uint) GetValue(cancellationToken).Length;

    internal string GetSubmissionValue(CancellationToken cancellationToken)
    {
        var value = GetValue(cancellationToken);
        var wrap = HtmlTextControlAttributes.GetEffectiveTextAreaWrap(_element, cancellationToken);
        var columns = HtmlTextControlAttributes.GetEffectiveTextAreaColumns(_element, cancellationToken);
        return HtmlTextSanitizer.GetTextAreaSubmissionValue(value, wrap, columns, cancellationToken);
    }

    internal void SetValue(string value, CancellationToken cancellationToken)
        => SetValue(value, null, cancellationToken);

    internal void SetValue(string value, Action<int>? checkpoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value);
        var normalized = HtmlTextSanitizer.NormalizeTextAreaValue(value, checkpoint, cancellationToken);
        var previous = GetValue(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        SetRawValue(value, normalized);
        SetDirty(true);
        SetOrigin(HtmlValueChangeOrigin.NonUser);
        if (!string.Equals(previous, normalized, StringComparison.Ordinal))
        {
            ClampSelection((uint) normalized.Length);
            SetSelectionRange((uint) normalized.Length, (uint) normalized.Length, null, CancellationToken.None);
        }
    }

    internal void SetDefaultValue(string value, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value);
        cancellationToken.ThrowIfCancellationRequested();
        var replacement = value.Length == 0 ? null : _element.OwnerDocument!.CreateTextNode(value);
        cancellationToken.ThrowIfCancellationRequested();
        _element.ReplaceChildren(replacement);
    }

    internal void Reset(CancellationToken cancellationToken)
    {
        var raw = GetDefaultValue(cancellationToken);
        var normalized = HtmlTextSanitizer.NormalizeTextAreaValue(raw, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        SetRawValue(raw, normalized);
        _rawAlignedWithChildren = true;
        SetDirty(false);
        SetOrigin(HtmlValueChangeOrigin.NonUser);
        SetUserValidity(false);
        ClampSelection((uint) normalized.Length);
    }

    internal void ChildrenChanged(bool mayShorten, bool markDocument, uint? knownApiLength)
    {
        if (DirtyValue) return;
        mayShorten |= !_rawAlignedWithChildren;
        _rawFromChildren = true;
        _rawAlignedWithChildren = true;
        _rawRevision++;
        _apiValue = null;
        if (markDocument) MarkStateChange();
        // Append is monotone after newline normalization; it cannot put an existing
        // endpoint beyond the new value. Destructive steps clamp immediately.
        if (mayShorten && (_selection.Start != 0 || _selection.End != 0))
        {
            ClampSelection(knownApiLength ?? (uint) GetValue(CancellationToken.None).Length);
        }
    }

    internal bool NeedsRemovalLengths => !DirtyValue && (_selection.Start != 0 || _selection.End != 0);

    internal void CopyFrom(HtmlTextAreaState source, CancellationToken cancellationToken = default)
        => CopyFrom(source, null, cancellationToken);

    internal void CopyFrom(HtmlTextAreaState source, Action<int>? checkpoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        var raw = source.GetRawValue(checkpoint, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        _rawValue = raw;
        _rawFromChildren = false;
        _rawAlignedWithChildren = false;
        DirtyValue = source.DirtyValue;
        LastValueChangeOrigin = HtmlValueChangeOrigin.NonUser;
        _rawRevision++;
        _apiRevision = -1;
        _apiValue = null;
    }

    internal HtmlTextSelection GetSelection(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _selection;
    }

    internal void SetSelectionStart(uint value, CancellationToken cancellationToken)
    {
        var end = _selection.End;
        var start = Clamp(value, GetTextLength(cancellationToken));
        SetSelectionRange(start, Math.Max(start, end), DirectionString(_selection.Direction), cancellationToken);
    }

    internal void SetSelectionEnd(uint value, CancellationToken cancellationToken)
    {
        var end = Clamp(value, GetTextLength(cancellationToken));
        SetSelectionRange(Math.Min(_selection.Start, end), end, DirectionString(_selection.Direction), cancellationToken);
    }

    internal void SetSelectionDirection(string? direction, CancellationToken cancellationToken)
        => SetSelectionRange(_selection.Start, _selection.End, direction, cancellationToken);

    internal void SetSelectionRange(uint start, uint end, string? direction, CancellationToken cancellationToken)
    {
        var length = GetTextLength(cancellationToken);
        var selectedEnd = Clamp(end, length);
        var selectedStart = Clamp(start, length);
        if (selectedEnd <= selectedStart) selectedStart = selectedEnd;
        var next = new HtmlTextSelection(selectedStart, selectedEnd, ParseDirection(direction));
        cancellationToken.ThrowIfCancellationRequested();
        SetSelection(next);
    }

    internal void Select(CancellationToken cancellationToken)
        => SetSelectionRange(0, GetTextLength(cancellationToken), null, cancellationToken);

    internal void SetRangeText(string replacement, CancellationToken cancellationToken)
        => SetRangeText(replacement, _selection.Start, _selection.End, HtmlRangeTextMode.Preserve, cancellationToken);

    internal void SetRangeText(string replacement, uint start, uint end, HtmlRangeTextMode mode,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        cancellationToken.ThrowIfCancellationRequested();
        if (start > end)
        {
            // Dirtiness freezes the current raw value even when it was still a
            // lazy projection of children. A later child mutation changes only
            // defaultValue after this algorithm step.
            GetRawValue(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            SetDirty(true);
            throw new DomException("IndexSizeError", "The start offset exceeds the end offset.");
        }

        var oldValue = GetValue(cancellationToken);
        var oldSelection = _selection;
        start = Clamp(start, (uint) oldValue.Length);
        end = Clamp(end, (uint) oldValue.Length);
        var newLength = checked((long) oldValue.Length - (end - start) + replacement.Length);
        cancellationToken.ThrowIfCancellationRequested();
        var prepared = string.Create(checked((int) newLength),
            (oldValue, replacement, start, end, cancellationToken), static (span, state) =>
        {
            var work = new HtmlTextWork(state.cancellationToken);
            var destination = 0;
            for (var i = 0; i < state.start; i++)
            {
                work.Step();
                span[destination++] = state.oldValue[i];
            }
            for (var i = 0; i < state.replacement.Length; i++)
            {
                work.Step();
                span[destination++] = state.replacement[i];
            }
            for (var i = (int) state.end; i < state.oldValue.Length; i++)
            {
                work.Step();
                span[destination++] = state.oldValue[i];
            }
            work.Check();
        });
        var normalized = HtmlTextSanitizer.NormalizeTextAreaValue(prepared, cancellationToken);
        var insertedEnd = checked((long) start + replacement.Length);
        var delta = checked((long) replacement.Length - (end - start));
        var next = mode switch
        {
            HtmlRangeTextMode.Select => new HtmlTextSelection(start, (uint) insertedEnd, HtmlSelectionDirection.None),
            HtmlRangeTextMode.Start => new HtmlTextSelection(start, start, HtmlSelectionDirection.None),
            HtmlRangeTextMode.End => new HtmlTextSelection((uint) insertedEnd, (uint) insertedEnd, HtmlSelectionDirection.None),
            HtmlRangeTextMode.Preserve => new HtmlTextSelection(
                MapEndpoint(oldSelection.Start, start, end, insertedEnd, delta, false),
                MapEndpoint(oldSelection.End, start, end, insertedEnd, delta, true),
                HtmlSelectionDirection.None),
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
        cancellationToken.ThrowIfCancellationRequested();
        SetRawValue(prepared, normalized);
        SetDirty(true);
        SetOrigin(HtmlValueChangeOrigin.NonUser);
        ClampSelection((uint) normalized.Length);
        SetSelection(NormalizeSelection(next, (uint) normalized.Length));
    }

    internal HtmlTextSelection GetEditingSelection(CancellationToken cancellationToken)
        => GetSelection(cancellationToken);

    internal bool SetEditingSelection(uint start, uint end, string? direction, CancellationToken cancellationToken)
    {
        SetSelectionRange(start, end, direction, cancellationToken);
        return true;
    }

    internal bool ApplyUserValue(string value, HtmlTextSelection selection, CancellationToken cancellationToken)
        => ApplyUserValue(value, selection, null, cancellationToken);

    internal bool ApplyUserValue(string value, HtmlTextSelection selection, Action<int>? checkpoint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value);
        var work = new HtmlTextWork(cancellationToken, checkpoint);
        work.Check();
        var disabledWork = new HtmlDisabledWork(cancellationToken, checkpoint, work.Steps);
        var disabled = HtmlDisabledness.GetState(_element, ref disabledWork);
        work.ContinueFrom(disabledWork.Steps);
        if (disabled != HtmlDisabledState.Enabled || IsReadOnly(ref work))
        {
            work.Finish();
            return false;
        }
        var normalized = HtmlTextSanitizer.NormalizeTextAreaValue(value, ref work);
        var old = _apiRevision == _rawRevision && _apiValue is { } cached ? cached
            : HtmlTextSanitizer.NormalizeTextAreaValue(_rawFromChildren
                ? HtmlTextAreaMutations.CollectChildText(_element, ref work) : _rawValue, ref work);
        var changed = !work.StringEquals(old, normalized);
        var rawChanged = changed && !_rawFromChildren && !work.StringEquals(_rawValue, value);
        work.Step();
        var next = NormalizeSelection(selection, (uint) normalized.Length);
        work.Finish();
        if (changed)
        {
            SetRawValue(value, normalized, rawChanged);
            SetDirty(true);
            SetOrigin(HtmlValueChangeOrigin.User);
            ClampSelection((uint) normalized.Length);
        }
        SetSelection(next);
        return true;
    }

    private string GetRawValue(CancellationToken cancellationToken)
        => GetRawValue(null, cancellationToken);

    private string GetRawValue(Action<int>? checkpoint, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_rawFromChildren) return _rawValue;
        var value = HtmlTextAreaMutations.CollectChildText(_element, checkpoint, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        _rawValue = value;
        _rawFromChildren = false;
        return value;
    }

    private void SetRawValue(string raw, string normalized)
        => SetRawValue(raw, normalized, !_rawFromChildren && !string.Equals(_rawValue, raw, StringComparison.Ordinal));

    private void SetRawValue(string raw, string normalized, bool changed)
    {
        // A clean lazy child projection already denotes this raw text. Resolving it
        // during reset is a representation change, not a value mutation.
        _rawValue = raw;
        _rawFromChildren = false;
        _rawAlignedWithChildren = false;
        _rawRevision++;
        _apiValue = normalized;
        _apiRevision = _rawRevision;
        if (changed) MarkStateChange();
    }

    private void SetDirty(bool value)
    {
        if (DirtyValue == value) return;
        DirtyValue = value;
        MarkStateChange();
    }

    private void SetOrigin(HtmlValueChangeOrigin origin)
    {
        if (LastValueChangeOrigin == origin) return;
        LastValueChangeOrigin = origin;
        MarkStateChange();
    }

    private void SetSelection(HtmlTextSelection selection)
    {
        if (_selection == selection) return;
        _selection = selection;
        MarkStateChange();
    }

    private void ClampSelection(uint length)
        => SetSelection(NormalizeSelection(_selection, length));

    private static HtmlTextSelection NormalizeSelection(HtmlTextSelection selection, uint length)
    {
        var end = Math.Min(selection.End, length);
        var start = Math.Min(selection.Start, end);
        return new HtmlTextSelection(start, end, selection.Direction);
    }

    private static uint MapEndpoint(uint old, uint start, uint end, long insertedEnd, long delta, bool isEnd)
    {
        if (old > end) return checked((uint) ((long) old + delta));
        if (old > start) return checked((uint) (isEnd ? insertedEnd : start));
        return old;
    }

    private static uint Clamp(uint value, uint length) => Math.Min(value, length);
    private bool IsReadOnly(ref HtmlTextWork work)
    {
        work.Check();
        foreach (var attribute in _element.Attributes)
        {
            work.Step();
            if (attribute.NamespaceUri is null && attribute.LocalName == "readonly")
            {
                work.Check();
                return true;
            }
        }

        work.Check();
        return false;
    }
    private static HtmlSelectionDirection ParseDirection(string? value) => value switch
    {
        "forward" => HtmlSelectionDirection.Forward,
        "backward" => HtmlSelectionDirection.Backward,
        _ => HtmlSelectionDirection.None
    };
    private static string? DirectionString(HtmlSelectionDirection direction) => direction switch
    {
        HtmlSelectionDirection.Forward => "forward",
        HtmlSelectionDirection.Backward => "backward",
        _ => null
    };
    private void MarkStateChange() => _element.OwnerDocument!.MarkMutation();
}
