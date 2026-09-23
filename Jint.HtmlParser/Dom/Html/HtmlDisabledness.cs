namespace Jint.HtmlParser;

/// <summary>HTML Living Standard §4.15, actually disabled elements.</summary>
internal static class HtmlDisabledness
{
    internal static HtmlDisabledState GetState(Element element, CancellationToken cancellationToken)
        => GetState(element, null, cancellationToken);

    // Per-call checkpoint for deterministic work and cancellation tests.
    internal static HtmlDisabledState GetState(Element element, Action<int>? checkpoint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(element);
        var work = new HtmlDisabledWork(cancellationToken, checkpoint);
        work.Check();
        var state = GetState(element, ref work);
        work.Check();
        return state;
    }

    internal static bool IsOptionDisabled(Element option, CancellationToken cancellationToken)
        => IsOptionDisabled(option, null, cancellationToken);

    internal static bool IsOptionDisabled(Element option, Action<int>? checkpoint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(option);
        if (!IsHtml(option, "option"))
        {
            throw new ArgumentException("An HTML option element is required.", nameof(option));
        }

        var work = new HtmlDisabledWork(cancellationToken, checkpoint);
        work.Check();
        var disabled = IsOptionDisabled(option, ref work);
        work.Check();
        return disabled;
    }

    // Called at both native child-list boundaries, before form-only early returns.
    internal static void DirectChildChanged(Node child, Node? parent)
    {
        if (child is Element { NamespaceUri: Namespaces.Html, LocalName: "legend" } &&
            parent is Element { NamespaceUri: Namespaces.Html, LocalName: "fieldset" } fieldset)
        {
            fieldset.GetHtmlState()!.InvalidateFirstLegend();
        }
    }

    private static HtmlDisabledState GetState(Element element, ref HtmlDisabledWork work)
    {
        if (element.NamespaceUri != Namespaces.Html)
        {
            return HtmlDisabledState.Inapplicable;
        }

        bool disabled;
        switch (element.LocalName)
        {
            case "button" or "input" or "select" or "textarea" or "fieldset":
                disabled = IsDisabledControl(element, ref work);
                break;
            case "optgroup":
                disabled = HasDisabledAttribute(element, ref work) || IsNearestSelectDisabled(element, ref work);
                break;
            case "option":
                disabled = IsOptionDisabled(element, ref work) || IsNearestSelectDisabled(element, ref work);
                break;
            default:
                return HtmlDisabledState.Inapplicable;
        }

        return disabled ? HtmlDisabledState.Disabled : HtmlDisabledState.Enabled;
    }

    private static bool IsDisabledControl(Element element, ref HtmlDisabledWork work)
    {
        if (HasDisabledAttribute(element, ref work))
        {
            return true;
        }

        Node child = element;
        for (var ancestor = element.ParentNode; ancestor is not null; child = ancestor, ancestor = ancestor.ParentNode)
        {
            work.Step();
            if (ancestor is Element { NamespaceUri: Namespaces.Html, LocalName: "fieldset" } fieldset &&
                HasDisabledAttribute(fieldset, ref work) &&
                !ReferenceEquals(child, fieldset.GetHtmlState()!.FirstLegend(ref work)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsNearestSelectDisabled(Element element, ref HtmlDisabledWork work)
        => HtmlSelectAncestry.GetNearestSelect(element, ref work) is { } select &&
           IsDisabledControl(select, ref work);

    private static bool IsOptionDisabled(Element option, ref HtmlDisabledWork work)
    {
        if (HasDisabledAttribute(option, ref work))
        {
            return true;
        }

        for (var ancestor = option.ParentNode; ancestor is not null; ancestor = ancestor.ParentNode)
        {
            work.Step();
            if (ancestor is not Element { NamespaceUri: Namespaces.Html } html)
            {
                continue;
            }

            switch (html.LocalName)
            {
                case "select" or "hr" or "datalist" or "option":
                    return false;
                case "optgroup":
                    return HasDisabledAttribute(html, ref work);
            }
        }

        return false;
    }

    private static bool HasDisabledAttribute(Element element, ref HtmlDisabledWork work)
    {
        if (element.AttributeCount == 0)
        {
            return false;
        }

        foreach (var attribute in element.Attributes)
        {
            work.Step();
            if (attribute.NamespaceUri is null && attribute.LocalName == "disabled")
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsHtml(Element element, string localName)
        => element.NamespaceUri == Namespaces.Html && element.LocalName == localName;
}
