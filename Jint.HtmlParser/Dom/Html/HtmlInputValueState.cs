namespace Jint.HtmlParser;

internal readonly record struct HtmlInputValueFacts(HtmlInputType Type, bool IsAvailable, bool DirtyValue,
    HtmlValueChangeOrigin LastValueChangeOrigin, bool UserValidity, bool HasTextBuffer,
    bool HasSelectionApi, bool ReadOnly, uint? TextLength);

/// <summary>HTML §4.10.5: one current value and provenance for implemented native input families.</summary>
internal sealed class HtmlInputValueState
{
    private string? _value;
    private Attr? _valueAttribute;
    private bool _multiple;
    private bool _readOnly;
    private bool _typeTransition;
    private string? _preparedDefaultValue;
    private HtmlTextSelection _selection;
    private HtmlInputType? _unavailableFamily;

    internal HtmlInputValueState(Element element, IReadOnlyList<Attr>? initialAttributes = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (element is not { NamespaceUri: Namespaces.Html, LocalName: "input" })
            throw new ArgumentException("An HTML input element is required.", nameof(element));
        Element = element;
        InitializeMetadata(initialAttributes, cancellationToken);
    }

    internal Element Element { get; }
    internal HtmlInputType Type { get; private set; }
    internal HtmlInputValueMode ValueMode => HtmlInputTypes.Info(Type).ValueMode;
    internal bool IsAvailable { get; private set; }
    internal bool DirtyValue { get; private set; }
    internal HtmlValueChangeOrigin LastValueChangeOrigin { get; private set; }
    internal bool UserValidity { get; private set; }
    internal bool HasTextBuffer => IsAvailable && IsTextType(Type);
    internal bool HasSelectionApi => HtmlInputTypes.Info(Type).HasSelectionApi;
    internal bool ReadOnly => HtmlInputTypes.Info(Type).ReadOnlyApplies && _readOnly;
    internal HtmlTextSelection Selection => _selection;

    internal static bool IsTextType(HtmlInputType type) => type is HtmlInputType.Text or HtmlInputType.Search
        or HtmlInputType.Tel or HtmlInputType.Url or HtmlInputType.Email or HtmlInputType.Password;
    internal static bool IsSupportedType(HtmlInputType type) => IsTextType(type) || type is
        HtmlInputType.Hidden or HtmlInputType.Submit or HtmlInputType.Image or HtmlInputType.Reset or
        HtmlInputType.Button or HtmlInputType.Checkbox or HtmlInputType.Radio;

    internal void InitializeMetadata(CancellationToken cancellationToken)
        => InitializeMetadata(null, cancellationToken);

    private void InitializeMetadata(IReadOnlyList<Attr>? initialAttributes, CancellationToken cancellationToken)
    {
        var work = new HtmlTextWork(cancellationToken);
        work.Check();
        string? type = null;
        Attr? valueAttribute = null;
        var multiple = false;
        var readOnly = false;
        var count = initialAttributes?.Count ?? Element.AttributeCount;
        for (var i = 0; i < count; i++)
        {
            work.Step();
            var attribute = initialAttributes is null ? Element.GetAttributeAt((uint) i)! : initialAttributes[i];
            if (attribute.NamespaceUri is not null) continue;
            switch (attribute.LocalName)
            {
                case "type": type = attribute.Value; break;
                case "value": valueAttribute = attribute; break;
                case "multiple": multiple = true; break;
                case "readonly": readOnly = true; break;
            }
        }
        var parsed = HtmlInputTypes.Parse(type);
        var supported = IsSupportedType(parsed);
        var initial = supported && IsTextType(parsed)
            ? HtmlTextSanitizer.SanitizeInput(parsed, valueAttribute?.Value ?? string.Empty, multiple, cancellationToken)
            : null;
        work.Check();
        Type = parsed;
        _valueAttribute = valueAttribute;
        _multiple = multiple;
        _readOnly = readOnly;
        _value = initial;
        IsAvailable = supported;
        _unavailableFamily = supported ? null : parsed;
    }

    internal void InitializeFrom(HtmlInputValueState prepared)
    {
        Type = prepared.Type;
        _valueAttribute = prepared._valueAttribute;
        _multiple = prepared._multiple;
        _readOnly = prepared._readOnly;
        _value = prepared._value;
        IsAvailable = prepared.IsAvailable;
        _unavailableFamily = prepared._unavailableFamily;
    }

    internal string GetDefaultValue(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _valueAttribute?.Value ?? string.Empty;
    }

    internal string GetValue(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequireAvailable();
        return ValueMode switch
        {
            HtmlInputValueMode.Value => _value!,
            HtmlInputValueMode.Default => _valueAttribute?.Value ?? string.Empty,
            HtmlInputValueMode.DefaultOn => _valueAttribute?.Value ?? "on",
            _ => throw Unavailable()
        };
    }

    internal HtmlInputValueFacts GetFacts(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new(Type, IsAvailable, DirtyValue, LastValueChangeOrigin, UserValidity, HasTextBuffer,
            HasSelectionApi, ReadOnly, HasTextBuffer ? (uint) _value!.Length : null);
    }

    internal uint GetTextLength(CancellationToken cancellationToken) => (uint) GetValue(cancellationToken).Length;

    internal void SetValue(string value, CancellationToken cancellationToken)
        => SetValue(value, null, cancellationToken);
    internal void SetValue(string value, Action<int>? checkpoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value);
        cancellationToken.ThrowIfCancellationRequested();
        RequireSupportedType();
        if (ValueMode is HtmlInputValueMode.Default or HtmlInputValueMode.DefaultOn)
        {
            Element.SetAttribute("value", value);
            MakeAvailable();
            SetOrigin(HtmlValueChangeOrigin.NonUser);
            return;
        }
        var prepared = HtmlTextSanitizer.SanitizeInput(Type, value, _multiple, checkpoint, cancellationToken);
        var changed = !IsAvailable || !string.Equals(_value, prepared, StringComparison.Ordinal);
        cancellationToken.ThrowIfCancellationRequested();
        MakeAvailable();
        CommitValue(prepared, HtmlValueChangeOrigin.NonUser);
        SetDirty(true);
        if (changed)
        {
            ClampSelection((uint) prepared.Length);
            SetSelection(HtmlInputTextOperations.Normalize((uint) prepared.Length, (uint) prepared.Length, null, (uint) prepared.Length));
        }
    }

    internal void SetDefaultValue(string value, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value);
        cancellationToken.ThrowIfCancellationRequested();
        var prepared = IsAvailable && ValueMode == HtmlInputValueMode.Value && !DirtyValue
            ? HtmlTextSanitizer.SanitizeInput(Type, value, _multiple, cancellationToken) : null;
        cancellationToken.ThrowIfCancellationRequested();
        _preparedDefaultValue = prepared;
        try { Element.SetAttribute("value", value); }
        finally { _preparedDefaultValue = null; }
    }

    // A component step, not whole input reset (checkedness/files have separate owners).
    internal void ResetValue(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequireSupportedType();
        var prepared = IsTextType(Type) ? HtmlTextSanitizer.SanitizeInput(Type,
            GetDefaultValue(cancellationToken), _multiple, cancellationToken) : null;
        cancellationToken.ThrowIfCancellationRequested();
        MakeAvailable();
        if (prepared is not null) CommitValue(prepared, HtmlValueChangeOrigin.NonUser);
        else SetOrigin(HtmlValueChangeOrigin.NonUser);
        SetDirty(false);
        SetUserValidity(false);
        ClampSelection((uint) (prepared?.Length ?? 0));
    }

    internal void AttributeChanged(string localName, string? oldValue, string? newValue)
    {
        switch (localName)
        {
            case "value":
                _valueAttribute = newValue is null ? null : Element.GetAttributeNodeNS(null, "value");
                if (!_typeTransition && IsAvailable && ValueMode == HtmlInputValueMode.Value && !DirtyValue)
                {
                    var prepared = _preparedDefaultValue ?? HtmlTextSanitizer.SanitizeInput(Type,
                        newValue ?? string.Empty, _multiple, default);
                    CommitAutomaticValue(prepared);
                }
                else if (!_typeTransition && IsAvailable && ValueMode is HtmlInputValueMode.Default or HtmlInputValueMode.DefaultOn)
                {
                    var fallback = ValueMode == HtmlInputValueMode.DefaultOn ? "on" : string.Empty;
                    if (!string.Equals(oldValue ?? fallback, newValue ?? fallback, StringComparison.Ordinal)) SetOrigin(HtmlValueChangeOrigin.NonUser);
                }
                break;
            case "multiple":
                _multiple = newValue is not null;
                if (Type == HtmlInputType.Email && IsAvailable)
                    CommitAutomaticValue(HtmlTextSanitizer.SanitizeInput(Type, _value!, _multiple, default));
                break;
            case "readonly": _readOnly = newValue is not null; break;
        }
    }

    internal void TypeChanged(HtmlInputType nextType, Action checkedTypeSignal)
    {
        ArgumentNullException.ThrowIfNull(checkedTypeSignal);
        if (nextType == Type) return;
        var oldMode = ValueMode;
        var oldRelevantValue = IsAvailable ? GetValue(default) : null;
        var nextMode = HtmlInputTypes.Info(nextType).ValueMode;
        var enteredSelection = !HasSelectionApi && HtmlInputTypes.Info(nextType).HasSelectionApi;
        var nextAvailable = IsSupportedType(nextType) && (IsAvailable || oldMode != HtmlInputValueMode.Value && nextMode == HtmlInputValueMode.Value);
        var candidate = nextMode == HtmlInputValueMode.Value && nextAvailable
            ? oldMode == HtmlInputValueMode.Value ? _value! : GetDefaultValue(default) : null;
        // Prepare the sanitizer before semantic mutation, publish it at its specified step.
        var prepared = candidate is not null ? HtmlTextSanitizer.SanitizeInput(nextType, candidate, _multiple, default) : null;
        _typeTransition = true;
        try
        {
            if (oldMode == HtmlInputValueMode.Value && nextMode is HtmlInputValueMode.Default or HtmlInputValueMode.DefaultOn && IsAvailable && _value!.Length != 0)
                Element.SetAttribute("value", _value);
            if (oldMode != HtmlInputValueMode.Value && nextMode == HtmlInputValueMode.Value) SetDirty(false);
            Type = nextType;
            IsAvailable = nextAvailable;
            _unavailableFamily = nextAvailable ? null : IsSupportedType(nextType) ? _unavailableFamily : nextType;
            checkedTypeSignal();
            if (prepared is not null)
            {
                if (string.Equals(oldRelevantValue, prepared, StringComparison.Ordinal))
                {
                    _value = prepared;
                    ClampSelection((uint) prepared.Length);
                }
                else CommitAutomaticValue(prepared);
            }
            else
            {
                _value = null; // No history or text fallback across unavailable families.
                if (nextAvailable && !string.Equals(oldRelevantValue, GetValue(default), StringComparison.Ordinal))
                    SetOrigin(HtmlValueChangeOrigin.NonUser);
            }
            if (!IsAvailable) SetSelection(default);
            else if (enteredSelection) SetSelection(default);
        }
        finally { _typeTransition = false; }
    }

    internal string Sanitize(string value, CancellationToken cancellationToken)
        => HtmlTextSanitizer.SanitizeInput(Type, value, _multiple, cancellationToken);
    internal HtmlTextSelection? GetSelection(CancellationToken cancellationToken)
        => HtmlInputTextOperations.GetSelection(this, cancellationToken);
    internal void SetSelectionStart(uint? value, CancellationToken cancellationToken)
        => HtmlInputTextOperations.SetSelectionStart(this, value, cancellationToken);
    internal void SetSelectionEnd(uint? value, CancellationToken cancellationToken)
        => HtmlInputTextOperations.SetSelectionEnd(this, value, cancellationToken);
    internal void SetSelectionDirection(string? direction, CancellationToken cancellationToken)
        => HtmlInputTextOperations.SetSelectionDirection(this, direction, cancellationToken);
    internal void SetSelectionRange(uint start, uint end, string? direction, CancellationToken cancellationToken)
        => HtmlInputTextOperations.SetSelectionRange(this, start, end, direction, cancellationToken);
    internal void Select(CancellationToken cancellationToken) => HtmlInputTextOperations.Select(this, cancellationToken);
    internal void SetRangeText(string replacement, CancellationToken cancellationToken)
        => HtmlInputTextOperations.SetRangeText(this, replacement, cancellationToken);
    internal void SetRangeText(string replacement, uint start, uint end, HtmlRangeTextMode mode, CancellationToken cancellationToken)
        => HtmlInputTextOperations.SetRangeText(this, replacement, start, end, mode, cancellationToken);
    internal HtmlTextSelection? GetEditingSelection(CancellationToken cancellationToken)
        => HtmlInputTextOperations.GetEditingSelection(this, cancellationToken);
    internal bool SetEditingSelection(uint start, uint end, string? direction, CancellationToken cancellationToken)
        => HtmlInputTextOperations.SetEditingSelection(this, start, end, direction, cancellationToken);
    internal bool ApplyUserValue(string value, HtmlTextSelection selection, CancellationToken cancellationToken)
        => ApplyUserValue(value, selection, null, cancellationToken);
    internal bool ApplyUserValue(string value, HtmlTextSelection selection, Action<int>? checkpoint,
        CancellationToken cancellationToken)
        => HtmlInputTextOperations.ApplyUserValue(this, value, selection, checkpoint, cancellationToken);
    internal string Sanitize(string value, ref HtmlTextWork work)
        => HtmlTextSanitizer.SanitizeInput(Type, value, _multiple, ref work);

    internal void CopyFrom(HtmlInputValueState source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _value = source._value;
        IsAvailable = source.IsAvailable;
        _unavailableFamily = source._unavailableFamily;
        DirtyValue = source.DirtyValue;
        LastValueChangeOrigin = HtmlValueChangeOrigin.NonUser;
        UserValidity = false;
        _selection = default;
    }

    internal void SetUserValidity(bool value)
    {
        if (UserValidity == value) return;
        UserValidity = value;
        MarkChanged();
    }
    internal void SetDirty(bool value)
    {
        if (DirtyValue == value) return;
        DirtyValue = value;
        MarkChanged();
    }
    internal void CommitValue(string value, HtmlValueChangeOrigin origin)
        => CommitValue(value, origin, !string.Equals(_value, value, StringComparison.Ordinal));
    internal void CommitValue(string value, HtmlValueChangeOrigin origin, bool changed)
    {
        if (changed) { _value = value; MarkChanged(); }
        SetOrigin(origin);
    }
    private void CommitAutomaticValue(string value)
    {
        if (!string.Equals(_value, value, StringComparison.Ordinal)) CommitValue(value, HtmlValueChangeOrigin.NonUser);
        ClampSelection((uint) value.Length);
    }
    private void SetOrigin(HtmlValueChangeOrigin origin)
    {
        if (LastValueChangeOrigin == origin) return;
        LastValueChangeOrigin = origin;
        MarkChanged();
    }
    internal void SetSelection(HtmlTextSelection selection)
    {
        if (_selection == selection) return;
        _selection = selection;
        MarkChanged();
    }
    internal void ClampSelection(uint length)
        => SetSelection(HtmlInputTextOperations.Normalize(_selection.Start, _selection.End,
            HtmlInputTextOperations.DirectionString(_selection.Direction), length));
    internal void RequireAvailable() { if (!IsAvailable) throw Unavailable(); }
    private void MakeAvailable()
    {
        if (!IsAvailable) MarkChanged();
        IsAvailable = true;
        _unavailableFamily = null;
    }
    private void RequireSupportedType() { if (!IsSupportedType(Type)) throw Unavailable(); }
    private NotSupportedException Unavailable() => new($"Native input value state for current type '{HtmlInputTypes.Info(Type).Keyword}' is unavailable because the '{HtmlInputTypes.Info(_unavailableFamily ?? Type).Keyword}' family has not supplied its required value transition.");
    private void MarkChanged() => Element.OwnerDocument!.MarkMutation();
}
