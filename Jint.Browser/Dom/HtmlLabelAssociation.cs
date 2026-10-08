using Jint.HtmlParser;

namespace Jint.Browser.Dom;

/// <summary>HTML's association between a <c>label</c> and its labeled control.</summary>
internal static class HtmlLabelAssociation
{
    /// <summary>
    /// https://html.spec.whatwg.org/multipage/forms.html#labeled-control — the control a label labels.
    /// </summary>
    internal static Element? ControlFor(Element label, Action<int>? checkpoint = null, CancellationToken token = default)
    {
        var work = new DomReadWork(checkpoint, token);
        work.Check();
        if (work.Attribute(label, "for") is { } id)
        {
            if (id.Length == 0) return null;
            var root = work.Root(label);
            if (root is Element candidate && work.Equal(work.Attribute(candidate, "id"), id))
                return IsLabelable(candidate, work) ? candidate : null;
            foreach (var element in NodeTraversal.DescendantElements(root, work.Check, token))
            {
                if (work.Equal(work.Attribute(element, "id"), id))
                    return IsLabelable(element, work) ? element : null;
            }
            return null;
        }
        foreach (var element in NodeTraversal.DescendantElements(label, work.Check, token))
        {
            if (IsLabelable(element, work)) return element;
        }
        return null;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/forms.html#dom-lfe-labels — the labels whose labeled control is
    /// <paramref name="control"/>, in tree order.
    /// </summary>
    internal static List<Element> LabelsFor(Element control, Action<int>? checkpoint = null, CancellationToken token = default)
    {
        var labels = new List<Element>();
        ReadLabels(control, uint.MaxValue, labels, checkpoint, token, out _);
        return labels;
    }

    internal static int CountLabels(Element control, Action<int>? checkpoint, CancellationToken token)
        => ReadLabels(control, uint.MaxValue, null, checkpoint, token, out _);

    internal static Element? LabelAt(Element control, uint index, Action<int>? checkpoint, CancellationToken token)
    {
        ReadLabels(control, index, null, checkpoint, token, out var label);
        return label;
    }

    // A live collection reads current membership, not a retained result list. uint.MaxValue selects
    // a full count/collection; an indexed read stops at its match and still brackets that return with
    // the invocation's constraint/cancellation check. The association predicate is shared with accessible-name reads.
    private static int ReadLabels(Element control, uint index, List<Element>? labels,
        Action<int>? checkpoint, CancellationToken token, out Element? match)
    {
        var work = new DomReadWork(checkpoint, token);
        work.Check();
        match = null;
        if (!IsLabelable(control, work))
        {
            work.Check();
            return 0;
        }
        var root = work.Root(control);
        var id = work.Attribute(control, "id") ?? "";
        var isFirstWithId = id.Length > 0 && ReferenceEquals(FirstElementWithId(root, id, work), control);
        var count = 0;
        foreach (var label in InclusiveElements(root, work))
        {
            if (!work.Equal(label.NamespaceUri, Namespaces.Html) || !work.Equal(label.LocalName, "label")) continue;
            var associated = work.Attribute(label, "for") is { } targetId
                ? isFirstWithId && work.Equal(targetId, id)
                : IsAncestorOf(label, control, work)
                    && ReferenceEquals(FirstLabelableDescendant(label, work), control);
            if (!associated) continue;
            labels?.Add(label);
            if ((uint) count++ == index)
            {
                match = label;
                work.Check();
                return count;
            }
        }
        work.Check();
        return count;
    }

    /// <summary>https://html.spec.whatwg.org/multipage/forms.html#category-label.</summary>
    internal static bool IsLabelable(Element element, Action<int>? checkpoint = null, CancellationToken token = default)
    {
        var work = new DomReadWork(checkpoint, token);
        work.Check();
        var result = IsLabelable(element, work);
        work.Check();
        return result;
    }

    private static bool IsLabelable(Element element, DomReadWork work)
        => work.Equal(element.NamespaceUri, Namespaces.Html) && (element.LocalName switch
        {
            "input" => !work.EqualAsciiIgnoreCase(work.Attribute(element, "type"), "hidden"),
            "button" or "select" or "textarea" or "meter" or "output" or "progress" => true,
            _ => false,
        });

    private static Element? FirstElementWithId(Node root, string id, DomReadWork work)
    {
        foreach (var element in InclusiveElements(root, work))
        {
            if (work.Equal(work.Attribute(element, "id"), id)) return element;
        }
        return null;
    }

    private static Element? FirstLabelableDescendant(Node root, DomReadWork work)
    {
        foreach (var element in NodeTraversal.DescendantElements(root, work.Check, work.Token))
        {
            if (IsLabelable(element, work)) return element;
        }
        return null;
    }

    private static bool IsAncestorOf(Node ancestor, Node node, DomReadWork work)
    {
        for (var parent = node.ParentNode; parent is not null; parent = parent.ParentNode)
        {
            work.Step();
            if (ReferenceEquals(parent, ancestor)) return true;
        }
        return false;
    }

    private static IEnumerable<Element> InclusiveElements(Node root, DomReadWork work)
    {
        if (root is Element element) yield return element;
        foreach (var descendant in NodeTraversal.DescendantElements(root, work.Check, work.Token)) yield return descendant;
    }
}
