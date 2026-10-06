namespace Jint.HtmlParser;

/// <summary>Root-local IDs and explicit form references; it never owns another tree.</summary>
internal sealed class HtmlFormIndex
{
    private readonly Node _root;
    internal HtmlFormWorkProbe? Probe => _root.FormWorkProbe;
    private readonly Dictionary<string, IdBucket> _ids = new(StringComparer.Ordinal);
    private readonly HashSet<Element> _resetCandidates = [];
    private Element[]? _orderedResetCandidates;
    private readonly Dictionary<string, HashSet<Element>> _references = new(StringComparer.Ordinal);

    private HtmlFormIndex(Node root)
    {
        _root = root;
        if (root is Element element)
        {
            Add(element, ordered: true);
        }

        foreach (var descendant in NodeTraversal.DescendantElements(root, default))
        {
            root.FormWorkProbe?.Visit();
            Add(descendant, ordered: true);
        }
    }

    internal static HtmlFormIndex GetOrCreate(Node root)
    {
        if (root.FormIndex is not { } index)
        {
            root.FormIndex = index = new HtmlFormIndex(root);
            (root as Document ?? root.OwnerDocument)!.HasFormIndex = true;
        }

        return index;
    }

    internal Element? FirstWithId(string id)
    {
        _root.FormWorkProbe?.Visit();
        if (!_ids.TryGetValue(id, out var bucket))
        {
            return null;
        }

        if (!bucket.Dirty)
        {
            return bucket.First;
        }

        // A moved/removed first duplicate invalidates only this ID. Rebuild once
        // in tree order; subsequent controls read the same first candidate in O(1).
        if (_root is Element rootElement && bucket.Candidates.Contains(rootElement))
        {
            bucket.First = rootElement;
        }
        else
        {
            bucket.First = null;
            foreach (var element in NodeTraversal.DescendantElements(_root, default))
            {
                _root.FormWorkProbe?.Visit();
                if (bucket.Candidates.Contains(element))
                {
                    bucket.First = element;
                    break;
                }
            }
        }

        bucket.Dirty = false;
        return bucket.First;
    }

    internal Element[] Referencing(string id)
    {
        _root.FormWorkProbe?.Visit();
        if (!_references.TryGetValue(id, out var elements))
        {
            return [];
        }

        var result = new Element[elements.Count];
        var i = 0;
        foreach (var element in elements)
        {
            _root.FormWorkProbe?.Visit();
            result[i++] = element;
        }

        return result;
    }

    internal Element[] ResetCandidates()
    {
        if (_orderedResetCandidates is { } cached) return cached;
        if (_resetCandidates.Count == 0) return _orderedResetCandidates = [];
        var result = new List<Element>(_resetCandidates.Count);
        // One ordinary preorder costs O(N + F), including unrelated nodes. This
        // fallback avoids F log F repeated depth/sibling scans. Reuse the order
        // until candidate membership/order changes, independent of unrelated mutations.
        var work = new HtmlCheckedWork(null, default);
        for (Node? current = _root; current is not null; current = HtmlRadioGroupIndex.Next(current, _root, ref work))
        {
            _root.FormWorkProbe?.Visit();
            if (current is Element element && _resetCandidates.Contains(element)) result.Add(element);
        }
        return _orderedResetCandidates = result.ToArray();
    }

    internal void Add(Element element, bool ordered = false)
    {
        _root.FormWorkProbe?.Visit();
        if (element.GetAttributeNodeNS(null, "id")?.Value is { Length: > 0 } id)
        {
            AddId(id, element, ordered);
        }

        if (HtmlFormState.IsListed(element) && element.GetAttributeNodeNS(null, "form") is { } attribute)
        {
            if (_resetCandidates.Add(element)) _orderedResetCandidates = null;
            if (attribute.Value.Length > 0) Add(_references, attribute.Value, element);
        }
    }

    internal void Remove(Element element)
    {
        if (_resetCandidates.Remove(element)) _orderedResetCandidates = null;
        _root.FormWorkProbe?.Visit();
        if (element.GetAttributeNodeNS(null, "id")?.Value is { Length: > 0 } id)
        {
            RemoveId(id, element);
        }

        if (HtmlFormState.IsListed(element) &&
            element.GetAttributeNodeNS(null, "form")?.Value is { Length: > 0 } reference)
        {
            Remove(_references, reference, element);
        }
    }

    internal void ChangeId(Element element, string? oldValue, string? newValue)
    {
        _root.FormWorkProbe?.Visit();
        if (!string.IsNullOrEmpty(oldValue))
        {
            RemoveId(oldValue, element);
        }

        if (!string.IsNullOrEmpty(newValue))
        {
            AddId(newValue, element, ordered: false);
        }
    }

    internal void ChangeReference(Element element, string? oldValue, string? newValue)
    {
        if (newValue is null)
        {
            if (_resetCandidates.Remove(element)) _orderedResetCandidates = null;
        }
        else if (_resetCandidates.Add(element)) _orderedResetCandidates = null;
        _root.FormWorkProbe?.Visit();
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

    private void AddId(string id, Element element, bool ordered)
    {
        _root.FormWorkProbe?.Visit();
        if (!_ids.TryGetValue(id, out var bucket))
        {
            _ids.Add(id, new IdBucket(element));
            return;
        }

        if (!bucket.Candidates.Add(element))
        {
            return;
        }

        if (!ordered && !bucket.Dirty && bucket.First is { } first &&
            Precedes(element, first, _root.FormWorkProbe))
        {
            bucket.First = element;
        }
    }

    private void RemoveId(string id, Element element)
    {
        _root.FormWorkProbe?.Visit();
        if (!_ids.TryGetValue(id, out var bucket) || !bucket.Candidates.Remove(element))
        {
            return;
        }

        if (bucket.Candidates.Count == 0)
        {
            _ids.Remove(id);
        }
        else if (ReferenceEquals(bucket.First, element))
        {
            bucket.Dirty = true;
        }
    }

    private static void Remove(Dictionary<string, HashSet<Element>> buckets, string key, Element element)
    {
        if (buckets.TryGetValue(key, out var elements) && elements.Remove(element) && elements.Count == 0)
        {
            buckets.Remove(key);
        }
    }

    // Equal-root nodes: compare the first divergent siblings, not allocation history.
    private static bool Precedes(Node left, Node right, HtmlFormWorkProbe? probe)
    {
        var leftPath = new List<Node>();
        var rightPath = new List<Node>();
        for (Node? current = left; current is not null; current = current.ParentNode)
        {
            probe?.Visit();
            leftPath.Add(current);
        }

        for (Node? current = right; current is not null; current = current.ParentNode)
        {
            probe?.Visit();
            rightPath.Add(current);
        }

        var i = leftPath.Count - 1;
        var j = rightPath.Count - 1;
        while (i >= 0 && j >= 0 && ReferenceEquals(leftPath[i], rightPath[j]))
        {
            probe?.Visit();
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
            probe?.Visit();
            if (ReferenceEquals(sibling, rightPath[j]))
            {
                return true;
            }
        }

        return false;
    }

    private sealed class IdBucket
    {
        internal IdBucket(Element first)
        {
            Candidates = [first];
            First = first;
        }

        internal HashSet<Element> Candidates { get; }
        internal Element? First { get; set; }
        internal bool Dirty { get; set; }
    }
}
