namespace Jint.HtmlParser;

internal readonly record struct HtmlInputValueFacts(HtmlInputType Type, bool IsAvailable, bool DirtyValue,
    HtmlValueChangeOrigin LastValueChangeOrigin, bool UserValidity, bool HasTextBuffer,
    bool HasSelectionApi, bool ReadOnly, uint? TextLength);

/// <summary>HTML §4.10.5: one current value and provenance for implemented native input families.</summary>
internal sealed partial class HtmlInputValueState
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
    internal bool HasEditingBuffer => IsAvailable && (IsTextType(Type) || Type == HtmlInputType.Number);
    internal bool HasSelectionApi => HtmlInputTypes.Info(Type).HasSelectionApi;
    internal bool ReadOnly => HtmlInputTypes.Info(Type).ReadOnlyApplies && _readOnly;
    internal HtmlTextSelection Selection => _selection;

    internal static bool IsTextType(HtmlInputType type) => type is HtmlInputType.Text or HtmlInputType.Search
        or HtmlInputType.Tel or HtmlInputType.Url or HtmlInputType.Email or HtmlInputType.Password;
    internal static bool IsSupportedType(HtmlInputType type) => IsTextType(type) || IsNumericType(type) || type is
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
        Attr? minimum = null;
        Attr? maximum = null;
        Attr? step = null;
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
                case "min": minimum = attribute; break;
                case "max": maximum = attribute; break;
                case "step": step = attribute; break;
            }
        }
        var parsed = HtmlInputTypes.Parse(type);
        var supported = IsSupportedType(parsed);
        var initial = supported && HtmlInputTypes.Info(parsed).ValueMode == HtmlInputValueMode.Value
            ? SanitizeFamily(parsed, valueAttribute?.Value ?? string.Empty, multiple,
                minimum?.Value, maximum?.Value, step?.Value, valueAttribute?.Value, null, cancellationToken)
            : null;
        work.Check();
        Type = parsed;
        _valueAttribute = valueAttribute;
        _multiple = multiple;
        _readOnly = readOnly;
        _minimumAttribute = minimum;
        _maximumAttribute = maximum;
        _stepAttribute = step;
        _numeric = null;
        _numberPresentation = null;
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
        _minimumAttribute = prepared._minimumAttribute;
        _maximumAttribute = prepared._maximumAttribute;
        _stepAttribute = prepared._stepAttribute;
        _numeric = null;
        _numberPresentation = null;
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
        var prepared = Sanitize(value, checkpoint, cancellationToken);
        var changed = !IsAvailable || !string.Equals(_value, prepared, StringComparison.Ordinal);
        var clearedDisplay = Type == HtmlInputType.Number && _numberPresentation is not null;
        cancellationToken.ThrowIfCancellationRequested();
        MakeAvailable();
        CommitValue(prepared, HtmlValueChangeOrigin.NonUser);
        ClearNumberPresentation();
        SetDirty(true);
        if (changed)
        {
            if (HasEditingBuffer)
            {
                ClampSelection((uint) prepared.Length);
                SetSelection(HtmlInputTextOperations.Normalize((uint) prepared.Length, (uint) prepared.Length, null, (uint) prepared.Length));
            }
            else SetSelection(default);
        }
        else if (clearedDisplay) ClampSelection((uint) prepared.Length);
    }

    internal void SetDefaultValue(string value, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value);
        cancellationToken.ThrowIfCancellationRequested();
        var prepared = IsAvailable && ValueMode == HtmlInputValueMode.Value && !DirtyValue
            ? SanitizeFamily(Type, value, _multiple, _minimumAttribute?.Value, _maximumAttribute?.Value,
                _stepAttribute?.Value, value, null, cancellationToken) : null;
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
        var prepared = ValueMode == HtmlInputValueMode.Value ? Sanitize(GetDefaultValue(cancellationToken), cancellationToken) : null;
        cancellationToken.ThrowIfCancellationRequested();
        MakeAvailable();
        if (prepared is not null) CommitValue(prepared, HtmlValueChangeOrigin.NonUser);
        else SetOrigin(HtmlValueChangeOrigin.NonUser);
        SetDirty(false);
        SetUserValidity(false);
        ClearNumberPresentation();
        if (Type == HtmlInputType.Number) SetSelection(default);
        ClampSelection((uint) (prepared?.Length ?? 0));
    }

    internal void AttributeChanged(string localName, string? oldValue, string? newValue)
    {
        switch (localName)
        {
            case "value":
                _valueAttribute = newValue is null ? null : Element.GetAttributeNodeNS(null, "value");
                InvalidateNumericConstraints();
                if (!_typeTransition && IsAvailable && ValueMode == HtmlInputValueMode.Value && !DirtyValue)
                {
                    var prepared = _preparedDefaultValue ?? Sanitize(newValue ?? string.Empty, default);
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
            case "min":
            case "max":
            case "step":
                var attribute = newValue is null ? null : Element.GetAttributeNodeNS(null, localName);
                if (localName == "min") _minimumAttribute = attribute;
                else if (localName == "max") _maximumAttribute = attribute;
                else _stepAttribute = attribute;
                InvalidateNumericConstraints();
                if (Type == HtmlInputType.Range && IsAvailable && !_typeTransition)
                    CommitAutomaticValue(Sanitize(_value!, default));
                break;
        }
    }

    internal void TypeChanged(HtmlInputType nextType, Action checkedTypeSignal)
    {
        ArgumentNullException.ThrowIfNull(checkedTypeSignal);
        if (nextType == Type) return;
        var oldMode = ValueMode;
        var leavingNumber = Type == HtmlInputType.Number;
        var oldRelevantValue = IsAvailable ? GetValue(default) : null;
        var nextMode = HtmlInputTypes.Info(nextType).ValueMode;
        var enteredSelection = !HasSelectionApi && HtmlInputTypes.Info(nextType).HasSelectionApi;
        var nextAvailable = IsSupportedType(nextType) && (IsAvailable || oldMode != HtmlInputValueMode.Value);
        var candidate = nextMode == HtmlInputValueMode.Value && nextAvailable
            ? oldMode == HtmlInputValueMode.Value ? _value! : GetDefaultValue(default) : null;
        // Prepare the sanitizer before semantic mutation, publish it at its specified step.
        var prepared = candidate is not null ? SanitizeFamily(nextType, candidate, _multiple,
            _minimumAttribute?.Value, _maximumAttribute?.Value, _stepAttribute?.Value, _valueAttribute?.Value, null, default) : null;
        _typeTransition = true;
        try
        {
            if (oldMode == HtmlInputValueMode.Value && nextMode is HtmlInputValueMode.Default or HtmlInputValueMode.DefaultOn && IsAvailable && _value!.Length != 0)
                Element.SetAttribute("value", _value);
            if (oldMode != HtmlInputValueMode.Value && nextMode == HtmlInputValueMode.Value) SetDirty(false);
            Type = nextType;
            _numeric = null;
            ClearNumberPresentation();
            if (leavingNumber) SetSelection(default);
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
            else if (!IsTextType(nextType)) SetSelection(default);
            else if (enteredSelection) SetSelection(default);
        }
        finally { _typeTransition = false; }
    }

    internal string Sanitize(string value, CancellationToken cancellationToken)
        => Sanitize(value, null, cancellationToken);
    private string Sanitize(string value, Action<int>? checkpoint, CancellationToken cancellationToken)
    {
        if (Type != HtmlInputType.Range)
            return SanitizeFamily(Type, value, _multiple, _minimumAttribute?.Value, _maximumAttribute?.Value,
                _stepAttribute?.Value, _valueAttribute?.Value, checkpoint, cancellationToken);
        var numericCheckpoint = AdaptCheckpoint(checkpoint);
        return HtmlInputRangeValue.Sanitize(value, GetNumericConstraints(cancellationToken, numericCheckpoint),
            numericCheckpoint, cancellationToken);
    }
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
        _numeric = null;
        _numberPresentation = null;
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
        if (changed) { _value = value; InvalidateNumericValue(); MarkChanged(); }
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
