namespace Jint.HtmlParser;

/// <summary>Root-local IDs and explicit form references; it never owns another tree.</summary>
internal sealed class HtmlFormIndex
{
    private readonly Dictionary<string, HashSet<Element>> _ids = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<Element>> _references = new(StringComparer.Ordinal);

    private HtmlFormIndex(Node root)
    {
        if (root is Element element)
        {
            Add(element);
        }

        foreach (var descendant in NodeTraversal.DescendantElements(root, default))
        {
            Add(descendant);
        }
    }

    internal static HtmlFormIndex GetOrCreate(Node root) => root.FormIndex ??= new HtmlFormIndex(root);

    internal Element? FirstWithId(string id)
    {
        if (!_ids.TryGetValue(id, out var candidates))
        {
            return null;
        }

        Element? first = null;
        foreach (var candidate in candidates)
        {
            if (first is null || Precedes(candidate, first))
            {
                first = candidate;
            }
        }

        return first;
    }

    internal Element[] Referencing(string id)
        => _references.TryGetValue(id, out var elements) ? [.. elements] : [];

    internal void Add(Element element)
    {
        if (element.GetAttributeNodeNS(null, "id")?.Value is { Length: > 0 } id)
        {
            Add(_ids, id, element);
        }

        if (HtmlFormState.IsListed(element) &&
            element.GetAttributeNodeNS(null, "form")?.Value is { Length: > 0 } reference)
        {
            Add(_references, reference, element);
        }
    }

    internal void Remove(Element element)
    {
        if (element.GetAttributeNodeNS(null, "id")?.Value is { Length: > 0 } id)
        {
            Remove(_ids, id, element);
        }

        if (HtmlFormState.IsListed(element) &&
            element.GetAttributeNodeNS(null, "form")?.Value is { Length: > 0 } reference)
        {
            Remove(_references, reference, element);
        }
    }

    internal void ChangeId(Element element, string? oldValue, string? newValue)
    {
        if (!string.IsNullOrEmpty(oldValue))
        {
            Remove(_ids, oldValue, element);
        }

        if (!string.IsNullOrEmpty(newValue))
        {
            Add(_ids, newValue, element);
        }
    }

    internal void ChangeReference(Element element, string? oldValue, string? newValue)
    {
        if (!string.IsNullOrEmpty(oldValue))
        {
            Remove(_references, oldValue, element);
        }

        if (!string.IsNullOrEmpty(newValue))
        {
            Add(_references, newValue, element);
        }
    }

    private static void Add(Dictionary<string, HashSet<Element>> buckets, string key, Element element)
    {
        if (!buckets.TryGetValue(key, out var elements))
        {
            elements = [];
            buckets.Add(key, elements);
        }

        elements.Add(element);
    }

    private static void Remove(Dictionary<string, HashSet<Element>> buckets, string key, Element element)
    {
        if (buckets.TryGetValue(key, out var elements) && elements.Remove(element) && elements.Count == 0)
        {
            buckets.Remove(key);
        }
    }

    // Equal-root nodes: compare the first divergent siblings, not allocation history.
    private static bool Precedes(Node left, Node right)
    {
        var leftPath = new List<Node>();
        var rightPath = new List<Node>();
        for (Node? current = left; current is not null; current = current.ParentNode)
        {
            leftPath.Add(current);
        }

        for (Node? current = right; current is not null; current = current.ParentNode)
        {
            rightPath.Add(current);
        }

        var i = leftPath.Count - 1;
        var j = rightPath.Count - 1;
        while (i >= 0 && j >= 0 && ReferenceEquals(leftPath[i], rightPath[j]))
        {
            i--;
            j--;
        }

        if (i < 0)
        {
            return true;
        }

        if (j < 0)
        {
            return false;
        }

        for (var sibling = leftPath[i].NextSibling; sibling is not null; sibling = sibling.NextSibling)
        {
            if (ReferenceEquals(sibling, rightPath[j]))
            {
                return true;
            }
        }

        return false;
    }
}
