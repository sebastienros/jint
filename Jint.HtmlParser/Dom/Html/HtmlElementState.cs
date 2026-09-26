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

    private HtmlTextAreaState? _textArea;
    internal HtmlTextAreaState? TextArea => Element is { NamespaceUri: Namespaces.Html, LocalName: "textarea" }
        ? _textArea ??= new HtmlTextAreaState(Element) : null;
    internal HtmlTextAreaState? ExistingTextArea => _textArea;

    private HtmlScriptState? _script;
    internal HtmlScriptState? Script => Element is { NamespaceUri: Namespaces.Html, LocalName: "script" }
        ? _script ??= new HtmlScriptState() : null;

    private bool _firstLegendKnown;
    private Element? _firstLegend;

    internal HtmlDisabledState GetDisabledState(CancellationToken cancellationToken)
        => HtmlDisabledness.GetState(Element, cancellationToken);

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
}
