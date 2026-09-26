namespace Jint.HtmlParser;

/// <summary>HTML Living Standard, get the nearest ancestor select.</summary>
internal static class HtmlSelectAncestry
{
    // The context carries ordinary ancestry through a subtree mutation once.
    // A nested select starts a new segment; barriers and a second optgroup end it.
    internal readonly record struct Context(Element? Select, bool SawOptgroup)
    {
        internal Context ForChildren(Node node)
        {
            if (node is not Element { NamespaceUri: Namespaces.Html } html) return this;
            return html.LocalName switch
            {
                "select" => new Context(html, false),
                "datalist" or "hr" or "option" => default,
                "optgroup" => SawOptgroup ? default : new Context(Select, true),
                _ => this
            };
        }
    }
    internal static Context GetContext(Node node, ref HtmlSelectWork work)
    {
        var sawOptgroup = false;
        for (var ancestor = node.ParentNode; ancestor is not null; ancestor = ancestor.ParentNode)
        {
            work.Step();
            if (ancestor is not Element { NamespaceUri: Namespaces.Html } html) continue;
            switch (html.LocalName)
            {
                case "datalist" or "hr" or "option": return default;
                case "optgroup": if (sawOptgroup) return default; sawOptgroup = true; break;
                case "select": return new Context(html, sawOptgroup);
            }
        }
        return new Context(null, sawOptgroup);
    }
    internal static Element? GetNearestSelectWithWork(Element element, HtmlSelectWorkContext? context, CancellationToken token)
    {
        var work = new HtmlSelectWork(element.OwnerDocument?.SelectWorkProbe, context, token);
        work.Check();
        var result = GetContext(element, ref work).Select;
        work.Check();
        return result;
    }

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
