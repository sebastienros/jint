namespace Jint.HtmlParser;

internal readonly record struct HtmlMetaInsertion(Document Document, string Content);

// DOM Standard §4.2.3 insertion steps and HTML's default-style pragma insertion facts.
// https://dom.spec.whatwg.org/#concept-node-insert
// A preparation owner, never retained by a published record. Arrays freeze at publication;
// only committed links and their captured facts enter the published views.
internal sealed class HtmlMetaInsertionCapture
{
    private readonly Document _document;
    private readonly MutationMatches _matches;
    private readonly MutationNotificationTicket _ticket;
    private readonly List<Node> _added;
    private readonly PreparedFacts _facts = new();
    private readonly MutationRecord[] _reserved;
    private int _committedFacts;
    private bool _published;
    internal IReadOnlyList<HtmlMetaInsertion> Facts => _facts;

    private HtmlMetaInsertionCapture(Node target, MutationMatches matches, int capacity,
        IReadOnlyList<Node>? removed, Node? previous, Node? next)
    {
        _document = target as Document ?? target.OwnerDocument!;
        _matches = matches;
        _ticket = new MutationNotificationTicket(matches);
        _added = new List<Node>(capacity);
        var added = _added.AsReadOnly();
        _reserved = new MutationRecord[matches.Entries.Count];
        for (var i = 0; i < _reserved.Length; i++)
        {
            var entry = matches.Entries[i];
            _reserved[i] = new MutationRecord(MutationRecordKind.ChildList, target, added, removed,
                previous, next, targetWasConnected: matches.TargetWasConnected,
                htmlMetaInsertions: entry.CaptureHtmlMetaInsertions ? Facts : null);
        }
    }

    internal static HtmlMetaInsertionCapture? Reserve(Node target, MutationMatches? matches, int capacity,
        IReadOnlyList<Node>? removed = null, Node? previous = null, Node? next = null)
        => capacity > 0 && matches is { CaptureHtmlMetaInsertions: true, TargetWasConnected: true }
            ? new HtmlMetaInsertionCapture(target, matches, capacity, removed, previous, next) : null;

    internal static HtmlMetaInsertionCapture? ReserveWithRemovedNode(Node target, MutationMatches? matches,
        int capacity, Node? removed, Node? previous, Node? next)
        => capacity > 0 && matches is { CaptureHtmlMetaInsertions: true, TargetWasConnected: true }
            ? new HtmlMetaInsertionCapture(target, matches, capacity,
                removed is null ? null : Array.AsReadOnly(new[] { removed }), previous, next) : null;

    internal void Prepare(Node destination, Node incoming)
    {
        CaptureConnectedMetaInsertions(destination, incoming, _matches, _facts);
    }

    internal void Committed(Node node)
    {
        _added.Add(node); // Capacity was reserved before any destination link.
        _committedFacts = _facts.Count;
    }

    internal void MarkPublished()
    {
        _facts.Freeze(_committedFacts);
        _published = true;
    }

    internal void PublishFailure()
    {
        if (_published) return;
        _published = true;
        _facts.Freeze(_committedFacts);
        if (_added.Count == 0 && _reserved[0].RemovedNodes.Count == 0) return;
        for (var i = 0; i < _reserved.Length; i++) _matches.Entries[i].Subscription.Enqueue(_reserved[i]);
        // Allocation-free publication. Notifications run only at a later healthy boundary.
        _document.PublishMutationNotifications(_ticket);
    }

    private static void CaptureConnectedMetaInsertions(Node destination, Node incoming,
        MutationMatches matches, PreparedFacts destinationFacts)
    {
        var budgets = new List<(Action<int>? Checkpoint, CancellationToken Token)>();
        foreach (var entry in matches.Entries)
            if (entry.CaptureHtmlMetaInsertions && entry.Subscription.CreateCaptureWork is { } create)
                budgets.Add(create());
        void Check(int units)
        {
            foreach (var budget in budgets)
            {
                budget.Token.ThrowIfCancellationRequested();
                budget.Checkpoint?.Invoke(units);
                budget.Token.ThrowIfCancellationRequested();
            }
        }
        var work = new TraversalWork(new(destination), new(incoming), Check, default);
        Node root = destination;
        while (true)
        {
            work.Step();
            if (root.ParentNode is { } parent) root = parent;
            else if (root is ShadowRoot shadow) root = shadow.Host;
            else break;
        }
        work.Check();
        if (root is not Document document) return;
        work.Check();
        var pending = new Stack<Node>();
        work.Check();
        var capacity = 0;
        Push(pending, incoming, ref capacity, ref work);
        List<HtmlMetaInsertion>? result = null;
        while (pending.TryPop(out var node))
        {
            work.Step();
            if (node is Element element)
            {
                if (element.NamespaceUri == Namespaces.Html && element.LocalName == "meta")
                {
                    string? directive = null, content = null;
                    for (uint i = 0; i < (uint) element.AttributeCount; i++)
                    {
                        work.Step();
                        var attribute = element.GetAttributeAt(i)!;
                        if (attribute.NamespaceUri is not null) continue;
                        if (Equal(attribute.LocalName, "http-equiv", ref work)) directive = attribute.Value;
                        else if (Equal(attribute.LocalName, "content", ref work)) content = attribute.Value;
                    }
                    if (content is { Length: > 0 } && IsDefaultStyle(directive, ref work))
                    {
                        work.Check();
                        (result ??= []).Add(new HtmlMetaInsertion(document, content));
                        work.Check();
                    }
                }
                // TemplateContent is deliberately not an ordinary child edge.
            }
            for (var child = node.LastChild; child is not null; child = child.PreviousSibling)
            {
                work.Step();
                Push(pending, child, ref capacity, ref work);
            }
            if (node is Element { AttachedShadowRoot: { } attached })
            {
                work.Step();
                Push(pending, attached, ref capacity, ref work);
            }
        }
        work.Check();
        if (result is null) return;
        foreach (var fact in result)
        {
            work.Step();
            if (destinationFacts.NeedsGrowth)
            {
                work.Check();
                destinationFacts.Grow();
                work.Check();
            }
            destinationFacts.Add(fact);
        }
        work.Check();
    }

    // This private array is mutable only during preparation, when no record can reach it.
    // Freeze drops tentative facts before any record is enqueued and permanently ends writes.
    private sealed class PreparedFacts : IReadOnlyList<HtmlMetaInsertion>
    {
        private HtmlMetaInsertion[] _items = Array.Empty<HtmlMetaInsertion>();
        private bool _frozen;
        public int Count { get; private set; }
        public HtmlMetaInsertion this[int index] => (uint) index < (uint) Count
            ? _items[index] : throw new ArgumentOutOfRangeException(nameof(index));
        internal bool NeedsGrowth => Count == _items.Length;
        internal void Grow()
        {
            if (_frozen) throw new InvalidOperationException("Published mutation facts are immutable.");
            var capacity = _items.Length == 0 ? 4 : checked(_items.Length * 2);
            Array.Resize(ref _items, capacity);
        }
        internal void Add(HtmlMetaInsertion fact)
        {
            if (_frozen) throw new InvalidOperationException("Published mutation facts are immutable.");
            _items[Count++] = fact;
        }
        internal void Freeze(int committedCount)
        {
            if (_frozen) return;
            Array.Clear(_items, committedCount, Count - committedCount);
            Count = committedCount;
            _frozen = true;
        }
        public IEnumerator<HtmlMetaInsertion> GetEnumerator()
        {
            for (var i = 0; i < Count; i++) yield return _items[i];
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private static void Push(Stack<Node> pending, Node node, ref int capacity, ref TraversalWork work)
    {
        if (pending.Count == capacity)
        {
            work.Check();
            capacity = pending.EnsureCapacity(pending.Count + 1);
            work.Check();
        }
        pending.Push(node);
    }

    private static bool Equal(string value, string expected, ref TraversalWork work)
    {
        if (value.Length != expected.Length) return false;
        for (var i = 0; i < value.Length; i++)
        {
            work.Step();
            if (value[i] != expected[i]) return false;
        }
        return true;
    }

    private static bool IsDefaultStyle(string? value, ref TraversalWork work)
    {
        const string expected = "default-style";
        if (value is null || value.Length != expected.Length) return false;
        for (var i = 0; i < expected.Length; i++)
        {
            work.Step();
            var c = value[i];
            if (c is >= 'A' and <= 'Z') c = (char) (c + ('a' - 'A'));
            if (c != expected[i]) return false;
        }
        return true;
    }
}
