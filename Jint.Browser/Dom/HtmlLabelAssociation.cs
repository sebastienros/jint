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
        var work = new DomReadWork(checkpoint, token);
        work.Check();
        var labels = new List<Element>();
        if (!IsLabelable(control, work)) return labels;
        var root = work.Root(control);
        var id = work.Attribute(control, "id") ?? "";
        var isFirstWithId = id.Length > 0 && ReferenceEquals(FirstElementWithId(root, id, work), control);
        foreach (var label in InclusiveElements(root, work))
        {
            if (!work.Equal(label.NamespaceUri, Namespaces.Html) || !work.Equal(label.LocalName, "label")) continue;
            if (work.Attribute(label, "for") is { } targetId)
            {
                if (isFirstWithId && work.Equal(targetId, id)) labels.Add(label);
            }
            else if (IsAncestorOf(label, control, work)
                && ReferenceEquals(FirstLabelableDescendant(label, work), control)) labels.Add(label);
        }
        work.Check();
        return labels;
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
