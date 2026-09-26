using Jint.HtmlParser;

namespace Jint.Browser.Dom;

/// <summary>HTML's association between a <c>label</c> and its labeled control.</summary>
internal static class HtmlLabelAssociation
{
    /// <summary>
    /// https://html.spec.whatwg.org/multipage/forms.html#labeled-control — the control a label labels.
    /// </summary>
    internal static Element? ControlFor(Element label)
    {
        if (label.GetAttributeNode("for") is not null)
        {
            var id = label.GetAttribute("for") ?? string.Empty;
            return id.Length > 0 && FirstElementWithId(RootOf(label), id) is Element html && IsLabelable(html)
                ? html
                : null;
        }

        return FirstLabelableDescendant(label);
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/forms.html#dom-lfe-labels — the labels whose labeled control is
    /// <paramref name="control"/>, in tree order.
    /// </summary>
    internal static List<Element> LabelsFor(Element control)
    {
        var labels = new List<Element>();
        if (!IsLabelable(control))
        {
            return labels;
        }

        var root = RootOf(control);
        var id = control.GetAttributeNode("id") is not null ? control.GetAttribute("id") ?? string.Empty : string.Empty;
        var isFirstWithId = id.Length > 0 && ReferenceEquals(FirstElementWithId(root, id), control);

        foreach (var node in InclusiveDescendants(root))
        {
            if (node is not Element { NamespaceUri: Namespaces.Html, LocalName: "label" } label)
            {
                continue;
            }

            if (label.GetAttributeNode("for") is not null)
            {
                if (isFirstWithId && string.Equals(label.GetAttribute("for"), id, StringComparison.Ordinal))
                {
                    labels.Add(label);
                }
            }
            else if (IsAncestorOf(label, control) && ReferenceEquals(FirstLabelableDescendant(label), control))
            {
                labels.Add(label);
            }
        }

        return labels;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/forms.html#category-label — the seven labelable element kinds.
    /// A hidden input is the one exception the list carries with it.
    /// </summary>
    internal static bool IsLabelable(Element element)
        => element.NamespaceUri == Namespaces.Html && (element.LocalName switch
        {
            "input" => !string.Equals(element.GetAttribute("type"), "hidden", StringComparison.OrdinalIgnoreCase),
            "button" or "select" or "textarea" or "meter" or "output" or "progress" => true,
            _ => false,
        });

    private static Element? FirstElementWithId(Node root, string id)
    {
        foreach (var node in InclusiveDescendants(root))
        {
            if (node is Element candidate
                && candidate.GetAttributeNode("id") is not null
                && string.Equals(candidate.GetAttribute("id"), id, StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        return null;
    }

    private static Element? FirstLabelableDescendant(Node root)
    {
        foreach (var node in Descendants(root))
        {
            if (node is Element candidate && IsLabelable(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool IsAncestorOf(Node ancestor, Node node)
    {
        for (var parent = node.ParentNode; parent is not null; parent = parent.ParentNode)
        {
            if (ReferenceEquals(parent, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    private static Node RootOf(Node node)
    {
        while (node.ParentNode is { } parent)
        {
            node = parent;
        }

        return node;
    }

    private static IEnumerable<Node> InclusiveDescendants(Node root)
    {
        yield return root;

        foreach (var descendant in Descendants(root))
        {
            yield return descendant;
        }
    }

    private static IEnumerable<Node> Descendants(Node root)
    {
        var current = root.FirstChild;
        while (current is not null)
        {
            yield return current;
            if (current.FirstChild is { } child)
            {
                current = child;
                continue;
            }
            while (current.NextSibling is null && !ReferenceEquals(current.ParentNode, root))
            {
                current = current.ParentNode;
                if (current is null) yield break;
            }
            current = current.NextSibling;
        }
    }
}
