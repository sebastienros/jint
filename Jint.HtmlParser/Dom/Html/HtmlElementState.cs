namespace Jint.HtmlParser;

/// <summary>The stable native HTML view of an element.</summary>
internal sealed class HtmlElementState
{
    internal HtmlElementState(Element element) => Element = element;

    internal Element Element { get; }
    internal Element? FormOwner => HtmlFormState.GetOwner(Element);
    private HtmlInputCheckedState? _checkedState;
    internal HtmlInputCheckedState? CheckedState
    {
        get
        {
            var work = new HtmlCheckedWork(Element.OwnerDocument?.CheckedWorkProbe, default);
            return GetCheckedState(ref work);
        }
    }
    internal HtmlInputCheckedState? GetCheckedState(ref HtmlCheckedWork work)
        => Element is { NamespaceUri: Namespaces.Html, LocalName: "input" }
            ? _checkedState ??= new HtmlInputCheckedState(Element, ref work) : null;
    internal HtmlInputCheckedState? ExistingCheckedState => _checkedState;

    private HtmlInputValueState? _inputValue;
    internal HtmlInputValueState? InputValue => GetInputValueState(default);
    internal HtmlInputValueState? GetInputValueState(CancellationToken token)
        => GetInputValueState(null, token);
    internal HtmlInputValueState? GetInputValueState(Action<int>? checkpoint, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (Element is not { NamespaceUri: Namespaces.Html, LocalName: "input" }) return null;
        if (_inputValue is not null) { checkpoint?.Invoke(1); token.ThrowIfCancellationRequested(); return _inputValue; }
        return _inputValue ??= new HtmlInputValueState(Element, null, checkpoint, token);
    }
    internal HtmlInputValueState? ExistingInputValue => _inputValue;
    internal void InitializeInputValue(HtmlInputValueState prepared)
    {
        if (_inputValue is null) _inputValue = prepared;
        else _inputValue.InitializeFrom(prepared);
    }

    private HtmlTextAreaState? _textArea;
    internal HtmlTextAreaState? TextArea => Element is { NamespaceUri: Namespaces.Html, LocalName: "textarea" }
        ? _textArea ??= new HtmlTextAreaState(Element) : null;
    internal HtmlTextAreaState? ExistingTextArea => _textArea;

    internal bool SelectedContentDisabled { get; set; }

    private HtmlSelectState? _select;
    internal HtmlSelectState? Select => GetSelectState(default);
    internal HtmlSelectState? GetSelectState(CancellationToken token)
        => GetSelectStateWithWork((HtmlSelectWorkContext?) null, token);
    internal HtmlSelectState? GetSelectStateWithWork(HtmlSelectWorkContext? context, CancellationToken token)
        => Element is { NamespaceUri: Namespaces.Html, LocalName: "select" }
            ? _select ??= new HtmlSelectState(Element, context, token) : null;
    internal HtmlSelectState InitializeSelect(HtmlSelectMetadata metadata)
    {
        if (_select is null) _select = new HtmlSelectState(Element, metadata);
        else _select.ApplyMetadata(metadata);
        return _select;
    }
    internal HtmlSelectState? ExistingSelect => _select;
    private HtmlOptionState? _option;
    internal HtmlOptionState? Option => GetOptionState(default);
    internal HtmlOptionState? GetOptionState(CancellationToken token)
        => GetOptionStateWithWork((HtmlSelectWorkContext?) null, token);
    internal HtmlOptionState? GetOptionStateWithWork(HtmlSelectWorkContext? context, CancellationToken token)
        => Element is { NamespaceUri: Namespaces.Html, LocalName: "option" }
            ? _option ??= new HtmlOptionState(Element, context, token) : null;
    internal HtmlOptionState InitializeOption(HtmlOptionMetadata metadata)
    {
        if (_option is null) _option = new HtmlOptionState(Element, metadata);
        else _option.ApplyMetadata(metadata);
        return _option;
    }
    internal HtmlOptionState? ExistingOption => _option;

    private HtmlScriptState? _script;
    internal HtmlScriptState? Script => Element is { NamespaceUri: Namespaces.Html, LocalName: "script" }
        ? _script ??= new HtmlScriptState() : null;

    private bool _firstLegendKnown;
    private Element? _firstLegend;

    internal HtmlDisabledState GetDisabledState(CancellationToken cancellationToken)
        => HtmlDisabledness.GetState(Element, cancellationToken);

    internal HtmlDisabledState GetDisabledStateWithWork(HtmlSelectWorkContext? context, CancellationToken token)
        => HtmlDisabledness.GetStateWithWork(Element, context, token);

    internal Element? FirstLegend(ref HtmlDisabledWork work)
    {
        if (_firstLegendKnown)
        {
            return _firstLegend;
        }

        Element? found = null;
        for (var child = Element.FirstChild; child is not null; child = child.NextSibling)
        {
            work.Step();
            if (child is Element { NamespaceUri: Namespaces.Html, LocalName: "legend" } legend)
            {
                found = legend;
                break;
            }
        }

        // A canceled scan cannot publish a result, including a known-null result.
        work.Check();
        _firstLegend = found;
        _firstLegendKnown = true;
        return found;
    }

    internal void InvalidateFirstLegend()
    {
        _firstLegend = null;
        _firstLegendKnown = false;
    }

    internal HtmlSelectState? GetSelectState(Action<int>? checkpoint, CancellationToken token)
    {
        var context = HtmlSelectWorkContext.Create(checkpoint, token);
        context?.Check();
        return GetSelectStateWithWork(context, token);
    }
    internal HtmlOptionState? GetOptionState(Action<int>? checkpoint, CancellationToken token)
    {
        var context = HtmlSelectWorkContext.Create(checkpoint, token);
        context?.Check();
        return GetOptionStateWithWork(context, token);
    }
}
