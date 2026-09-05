using AngleSharp.Dom;
using AngleSharp.Html.Dom;

namespace Jint.Browser.Dom;

/// <summary>HTML's association between a <c>label</c> and its labeled control.</summary>
internal static class HtmlLabelAssociation
{
    /// <summary>
    /// https://html.spec.whatwg.org/multipage/forms.html#labeled-control — the control a label labels.
    /// </summary>
    internal static IHtmlElement? ControlFor(IHtmlLabelElement label)
    {
        if (label.HasAttribute("for"))
        {
            var id = label.GetAttribute("for") ?? string.Empty;
            return id.Length > 0 && FirstElementWithId(RootOf(label), id) is IHtmlElement html && IsLabelable(html)
                ? html
                : null;
        }

        return FirstLabelableDescendant(label);
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/forms.html#dom-lfe-labels — the labels whose labeled control is
    /// <paramref name="control"/>, in tree order.
    /// </summary>
    internal static List<IHtmlLabelElement> LabelsFor(IHtmlElement control)
    {
        var labels = new List<IHtmlLabelElement>();
        if (!IsLabelable(control))
        {
            return labels;
        }

        var root = RootOf(control);
        var id = control.HasAttribute("id") ? control.Id ?? string.Empty : string.Empty;
        var isFirstWithId = id.Length > 0 && ReferenceEquals(FirstElementWithId(root, id), control);

        foreach (var node in InclusiveDescendants(root))
        {
            if (node is not IHtmlLabelElement label)
            {
                continue;
            }

            if (label.HasAttribute("for"))
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
    internal static bool IsLabelable(IHtmlElement element) => element switch
    {
        IHtmlInputElement input => !string.Equals(input.Type, "hidden", StringComparison.OrdinalIgnoreCase),
        IHtmlButtonElement or IHtmlSelectElement or IHtmlTextAreaElement => true,
        _ => element.LocalName is "meter" or "output" or "progress",
    };

    private static IElement? FirstElementWithId(INode root, string id)
    {
        foreach (var node in InclusiveDescendants(root))
        {
            if (node is IElement candidate
                && candidate.HasAttribute("id")
                && string.Equals(candidate.Id, id, StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        return null;
    }

    private static IHtmlElement? FirstLabelableDescendant(INode root)
    {
        foreach (var node in Descendants(root))
        {
            if (node is IHtmlElement candidate && IsLabelable(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool IsAncestorOf(INode ancestor, INode node)
    {
        for (var parent = node.Parent; parent is not null; parent = parent.Parent)
        {
            if (ReferenceEquals(parent, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    private static INode RootOf(INode node)
    {
        while (node.Parent is { } parent)
        {
            node = parent;
        }

        return node;
    }

    private static IEnumerable<INode> InclusiveDescendants(INode root)
    {
        yield return root;

        foreach (var descendant in Descendants(root))
        {
            yield return descendant;
        }
    }

    private static IEnumerable<INode> Descendants(INode root)
    {
        foreach (var child in root.ChildNodes)
        {
            yield return child;

            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }
}
