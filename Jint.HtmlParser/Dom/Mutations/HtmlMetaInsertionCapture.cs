namespace Jint.HtmlParser;

internal readonly record struct HtmlMetaInsertion(Document Document, string Content);

// DOM Standard §4.2.3 insertion steps and HTML's default-style pragma insertion facts.
// https://dom.spec.whatwg.org/#concept-node-insert
// A preparation owner, never retained by a published record. Arrays freeze at publication;
// only committed links and their captured facts enter the published views.
internal sealed class HtmlMetaInsertionCapture
{
    private Document _document;
    private readonly Node _target;
    private readonly Node _firstIncoming;
    private readonly List<Node>? _removed;
    private readonly MutationMatches _matches;
    private readonly MutationNotificationTicket _ticket;
    private readonly List<Node> _added;
    private readonly PreparedFacts _facts;
    private readonly MutationRecordQueueReservation[] _reserved;
    private List<(Action<int>? Checkpoint, CancellationToken Token)>? _initialBudgets;
    private int _initialUnits;
    private int _committedFacts;
    private bool _published;
    internal IReadOnlyList<HtmlMetaInsertion> Facts => _facts;

    private HtmlMetaInsertionCapture(Node target, Node firstIncoming, MutationMatches matches, int capacity,
        int removedCapacity, Node? previous, Node? next,
        List<(Action<int>? Checkpoint, CancellationToken Token)> budgets, ref TraversalWork work)
    {
        _initialBudgets = budgets;
        work.Check();
        _facts = new PreparedFacts();
        work.Check();
        _document = target as Document ?? target.OwnerDocument!;
        _target = target;
        _firstIncoming = firstIncoming;
        _matches = matches;
        _ticket = new MutationNotificationTicket(matches);
        work.Check();
        _added = new List<Node>(capacity);
        work.Check();
        var added = _added.AsReadOnly();
        work.Check();
        IReadOnlyList<Node> removed = MutationRecord.EmptyNodes;
        if (removedCapacity > 0)
        {
            _removed = new List<Node>(removedCapacity);
            work.Check();
            removed = _removed.AsReadOnly();
            work.Check();
        }
        _reserved = new MutationRecordQueueReservation[matches.Entries.Count];
        work.Check();
        for (var i = 0; i < _reserved.Length; i++)
        {
            var entry = matches.Entries[i];
            work.Step(); work.Check();
            var record = new MutationRecord(MutationRecordKind.ChildList, target, added, removed,
                previous, next, targetWasConnected: matches.TargetWasConnected,
                htmlMetaInsertions: entry.CaptureHtmlMetaInsertions ? Facts : null);
            work.Check();
            _reserved[i] = MutationSubscription.ReserveRecord(record);
            work.Check();
        }
        _initialUnits = work.Count;
    }

    internal static HtmlMetaInsertionCapture? Reserve(Node target, Node firstIncoming, MutationMatches? matches,
        int capacity, IReadOnlyList<Node>? removed = null, Node? previous = null, Node? next = null)
    {
        if (capacity == 0 || matches is not { CaptureHtmlMetaInsertions: true, TargetWasConnected: true }) return null;
        var budgets = CreateBudgets(target, firstIncoming, matches, out var work);
        work.Check();
        var capture = new HtmlMetaInsertionCapture(target, firstIncoming, matches, capacity, removed?.Count ?? 0, previous, next, budgets, ref work);
        work.Check();
        capture._initialUnits = work.Count;
        return capture;
    }

    internal static HtmlMetaInsertionCapture? ReserveWithRemovedNode(Node target, Node firstIncoming,
        MutationMatches? matches, int capacity, Node? removed, Node? previous, Node? next)
    {
        if (capacity == 0 || matches is not { CaptureHtmlMetaInsertions: true, TargetWasConnected: true }) return null;
        var budgets = CreateBudgets(target, firstIncoming, matches, out var work);
        work.Check();
        var capture = new HtmlMetaInsertionCapture(target, firstIncoming, matches, capacity,
            removed is null ? 0 : 1, previous, next, budgets, ref work);
        work.Check();
        capture._initialUnits = work.Count;
        return capture;
    }

    internal void Prepare(Node destination, Node incoming)
    {
        var budgets = _initialBudgets;
        var initialUnits = _initialUnits;
        _initialBudgets = null;
        _initialUnits = 0;
        if (budgets is null)
        {
            budgets = CreateBudgets(destination, incoming, _matches, out var work);
            initialUnits = work.Count;
        }
        CaptureConnectedMetaInsertions(destination, incoming, budgets, initialUnits, _facts);
    }

    internal void PrepareRemoval()
    {
        if (_removed is null || _initialBudgets is null) return;
        var work = CreateWork(_target, _firstIncoming, _initialBudgets, _initialUnits);
        work.Step();
        if (_removed.Count == _removed.Capacity)
        {
            work.Check();
            _removed.EnsureCapacity(_removed.Count + 1);
            work.Check();
        }
        work.Check();
        _initialUnits = work.Count;
    }

    internal void CommittedRemoval(Node node)
    {
        if (_removed is null) return;
        if (_added.Count == 0 && _removed.Count == 0) _document = _target as Document ?? _target.OwnerDocument!;
        _removed.Add(node);
    }

    internal void Committed(Node node)
    {
        if (_added.Count == 0 && (_removed is null || _removed.Count == 0))
            _document = _target as Document ?? _target.OwnerDocument!;
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
        if (_added.Count == 0 && _reserved[0].Record.Record!.RemovedNodes.Count == 0) return;
        for (var i = 0; i < _reserved.Length; i++) _matches.Entries[i].Subscription.EnqueueReserved(_reserved[i]);
        // Allocation-free publication. Notifications run only at a later healthy boundary.
        _document.PublishMutationNotifications(_ticket);
    }

    private static List<(Action<int>? Checkpoint, CancellationToken Token)> CreateBudgets(Node destination,
        Node incoming, MutationMatches matches, out TraversalWork work)
    {
        var seedIndex = -1;
        (Action<int>? Checkpoint, CancellationToken Token) seed = default;
        for (var i = 0; i < matches.Entries.Count; i++)
        {
            var entry = matches.Entries[i];
            if (!entry.CaptureHtmlMetaInsertions || entry.Subscription.CreateCaptureWork is not { } create) continue;
            seedIndex = i; seed = create(); break;
        }
        var allocationWork = new TraversalWork(new(destination), new(incoming), seed.Checkpoint, seed.Token);
        allocationWork.Check();
        var budgets = new List<(Action<int>? Checkpoint, CancellationToken Token)>(matches.Entries.Count);
        allocationWork.Check();
        for (var i = 0; i < matches.Entries.Count; i++)
        {
            allocationWork.Step();
            var entry = matches.Entries[i];
            if (!entry.CaptureHtmlMetaInsertions) continue;
            if (i == seedIndex) budgets.Add(seed);
            else if (entry.Subscription.CreateCaptureWork is { } create)
            {
                allocationWork.Check();
                budgets.Add(create());
                allocationWork.Check();
            }
        }
        work = CreateWork(destination, incoming, budgets, allocationWork.Count);
        return budgets;
    }

    private static TraversalWork CreateWork(Node destination, Node incoming,
        List<(Action<int>? Checkpoint, CancellationToken Token)> budgets, int initialUnits)
    {
        void Check(int units)
        {
            foreach (var budget in budgets)
            {
                budget.Token.ThrowIfCancellationRequested();
                budget.Checkpoint?.Invoke(units);
                budget.Token.ThrowIfCancellationRequested();
            }
        }
        return new TraversalWork(new(destination), new(incoming), Check, default, initialUnits);
    }

    private static void CaptureConnectedMetaInsertions(Node destination, Node incoming,
        List<(Action<int>? Checkpoint, CancellationToken Token)> budgets, int initialUnits, PreparedFacts destinationFacts)
    {
        var work = CreateWork(destination, incoming, budgets, initialUnits);
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
