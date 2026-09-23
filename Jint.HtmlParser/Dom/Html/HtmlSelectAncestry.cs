namespace Jint.HtmlParser;

/// <summary>HTML Living Standard, get the nearest ancestor select.</summary>
internal static class HtmlSelectAncestry
{
    internal static Element? GetNearestSelect(Element element, CancellationToken cancellationToken)
        => GetNearestSelect(element, null, cancellationToken);

    // The checkpoint makes a long final ascent deterministically cancellable in tests.
    internal static Element? GetNearestSelect(Element element, Action<int>? checkpoint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(element);
        var work = new HtmlDisabledWork(cancellationToken, checkpoint);
        work.Check();
        var result = GetNearestSelect(element, ref work);
        work.Check();
        return result;
    }

    internal static Element? GetNearestSelect(Element element, ref HtmlDisabledWork work)
    {
        var sawOptgroup = false;
        for (var ancestor = element.ParentNode; ancestor is not null; ancestor = ancestor.ParentNode)
        {
            work.Step();
            if (ancestor is not Element { NamespaceUri: Namespaces.Html } html)
            {
                continue;
            }

            switch (html.LocalName)
            {
                case "datalist" or "hr" or "option":
                    return null;
                case "optgroup":
                    if (sawOptgroup)
                    {
                        return null;
                    }

                    sawOptgroup = true;
                    break;
                case "select":
                    return html;
            }
        }

        return null;
    }
}
