namespace Jint.HtmlParser;

/// <summary>The stable native HTML view of an element.</summary>
internal sealed class HtmlElementState
{
    internal HtmlElementState(Element element) => Element = element;

    internal Element Element { get; }
    internal Element? FormOwner => HtmlFormState.GetOwner(Element);

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
