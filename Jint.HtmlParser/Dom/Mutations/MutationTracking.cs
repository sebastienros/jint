using System.Collections.ObjectModel;

namespace Jint.HtmlParser;

internal static class MutationTracking
{
    internal static MutationMatches? Match(Node target, MutationRecordKind kind,
        string? attributeName = null, string? attributeNamespace = null, Node? changedChild = null)
    {
        // DOM Standard §4.3.2: collect interested observers in inclusive-ancestor
        // order, then deduplicate each subscription and project oldValue per observer.
        // https://dom.spec.whatwg.org/#queue-a-mutation-record
        if (!(target as Document ?? target.OwnerDocument!).MayHaveMutationRegistrations)
        {
            return null;
        }

        MutationMatches? matches = null;
        Node root = target;
        // Tri-state: computed on the first subscription that asks.
        var inert = 0;
        for (var ancestor = target; ancestor is not null; ancestor = ancestor.ParentNode)
        {
            root = ancestor;
            if (ancestor.MutationRegistrations is not { } registrations)
            {
                continue;
            }

            for (var r = 0; r < registrations.Count; r++)
            {
                var entry = registrations[r];
                var options = entry.Registration.Options;
                if ((!ReferenceEquals(ancestor, target) && !options.Subtree) ||
                    !options.Matches(kind, attributeName, attributeNamespace))
                {
                    continue;
                }

                if (entry.Registration.Subscription.OmitInertCharacterRecords)
                {
                    if (inert == 0) inert = IsInertCharacterChange(target, kind, changedChild) ? 1 : -1;
                    if (inert > 0) continue;
                }

                (matches ??= new MutationMatches()).Add(entry.Registration.Subscription,
                    kind == MutationRecordKind.Attributes && options.AttributeOldValue ||
                    kind == MutationRecordKind.CharacterData && options.CharacterDataOldValue,
                    kind == MutationRecordKind.ChildList && entry.Registration.Subscription.CaptureHtmlMetaInsertions);
            }
        }

        if (kind == MutationRecordKind.ChildList && matches is not null)
        {
            // Reuse the observer match's ordinary ancestor walk. Resolve only a
            // shadow root's host chain, without widening observer matching across it.
            while (root is ShadowRoot shadow)
            {
                root = shadow.Host;
                while (root.ParentNode is { } parent) root = parent;
            }
            matches.TargetWasConnected = root is Document;
        }

        return matches;
    }

    private static bool IsInertCharacterChange(Node target, MutationRecordKind kind, Node? changedChild)
    {
        Node? parent;
        if (kind == MutationRecordKind.CharacterData) parent = target.ParentNode;
        else if (kind == MutationRecordKind.ChildList && changedChild is Text or Comment or ProcessingInstruction or CDataSection) parent = target;
        else return false;
        return parent is not Element { LocalName: "style" or "script", NamespaceUri: Namespaces.Html or Namespaces.Svg };
    }

    internal static void CaptureTransients(Node parent, Node removed)
    {
        // DOM Standard §4.3.2: removal keeps subtree registrations active until
        // the observer's next notification boundary.
        // https://dom.spec.whatwg.org/#concept-node-remove
        if (!(parent as Document ?? parent.OwnerDocument!).MayHaveMutationRegistrations)
        {
            return;
        }

        for (var ancestor = parent; ancestor is not null; ancestor = ancestor.ParentNode)
        {
            if (ancestor.MutationRegistrations is not { } registrations)
            {
                continue;
            }

            // A transient on the removed root is a copy of a registration that
            // observed the old ancestor path, not a parent/Host link.
            for (var r = 0; r < registrations.Count; r++)
            {
                var entry = registrations[r];
                if (entry.Registration.Options.Subtree)
                {
                    removed.AddMutationRegistration(entry.Registration, transient: true);
                }
            }
        }
    }

    internal static void QueueChildList(Node target, Node? added, Node? removed,
        Node? previousSibling, Node? nextSibling, MutationMatches? matches = null,
        IReadOnlyList<HtmlMetaInsertion>? htmlMetaInsertions = null)
    {
        matches ??= Match(target, MutationRecordKind.ChildList,
            changedChild: added is null ? removed : removed is null ? added : null);
        if (matches is null)
        {
            return;
        }

        var addedNodes = added is null ? MutationRecord.EmptyNodes : SnapshotSingle(added);
        var removedNodes = removed is null ? MutationRecord.EmptyNodes : SnapshotSingle(removed);
        EmitChildList(target, addedNodes, removedNodes, previousSibling, nextSibling, matches, htmlMetaInsertions);
    }

    internal static void QueueChildList(Node target, IReadOnlyList<Node>? added, IReadOnlyList<Node>? removed,
        Node? previousSibling, Node? nextSibling, MutationMatches? matches = null,
        IReadOnlyList<HtmlMetaInsertion>? htmlMetaInsertions = null)
    {
        matches ??= Match(target, MutationRecordKind.ChildList);
        if (matches is null)
        {
            return;
        }

        var addedNodes = Snapshot(added);
        var removedNodes = Snapshot(removed);
        EmitChildList(target, addedNodes, removedNodes, previousSibling, nextSibling, matches, htmlMetaInsertions);
    }

    internal static void QueueAttribute(Element target, Attr attribute,
        string? oldValue, MutationMatches? matches = null, Attr? replaced = null)
    {
        matches ??= Match(target, MutationRecordKind.Attributes, attribute.LocalName, attribute.NamespaceUri);
        if (matches is null || matches.Entries.Count == 0)
        {
            return;
        }

        // Capture the actual qualified name only when records are needed. Attr.Name reuses LocalName
        // when unprefixed; a prefixed name is built once and shared by all interested subscriptions.
        var qualifiedName = attribute.Name;
        var previousQualifiedName = replaced is not null && !string.Equals(replaced.Prefix, attribute.Prefix, StringComparison.Ordinal)
            ? replaced.Name : null;
        // Notification invokes a trusted pending callback. Freeze the transition before the first
        // subscription can reenter and change or remove the attribute for later subscriptions.
        var newValue = ReferenceEquals(attribute.OwnerElement, target) ? attribute.Value : null;
        for (var index = 0; index < matches.Count; index++)
        {
            var entry = matches[index];
            entry.Subscription.Enqueue(new MutationRecord(MutationRecordKind.Attributes, target,
                attributeName: attribute.LocalName, attributeNamespace: attribute.NamespaceUri,
                oldValue: entry.OldValue ? oldValue : null, attributeQualifiedName: qualifiedName,
                attributePreviousQualifiedName: previousQualifiedName, attributeNewValue: newValue));
        }
        Notify(matches);
    }

    internal static void QueueCharacterData(Node target, string? oldValue, MutationMatches? matches = null)
    {
        matches ??= Match(target, MutationRecordKind.CharacterData);
        if (matches is null)
        {
            return;
        }

        for (var index = 0; index < matches.Count; index++)
        {
            var entry = matches[index];
            entry.Subscription.Enqueue(new MutationRecord(MutationRecordKind.CharacterData, target,
                oldValue: entry.OldValue ? oldValue : null));
        }
        Notify(matches);
    }

    private static SingleNodeList SnapshotSingle(Node node) => new SingleNodeList(node);

    // One allocation instead of an array plus its read-only wrapper, for the parser's per-node record.
    private sealed class SingleNodeList(Node node) : IReadOnlyList<Node>
    {
        public Node this[int index] => index == 0 ? node : throw new ArgumentOutOfRangeException(nameof(index));
        public int Count => 1;

        public IEnumerator<Node> GetEnumerator()
        {
            yield return node;
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private static IReadOnlyList<Node> Snapshot(IReadOnlyList<Node>? source)
    {
        if (source is null || source.Count == 0)
        {
            return MutationRecord.EmptyNodes;
        }

        var nodes = new Node[source.Count];
        for (var i = 0; i < nodes.Length; i++)
        {
            nodes[i] = source[i];
        }

        return Array.AsReadOnly(nodes);
    }

    private static void EmitChildList(Node target, IReadOnlyList<Node> added, IReadOnlyList<Node> removed,
        Node? previousSibling, Node? nextSibling, MutationMatches matches, IReadOnlyList<HtmlMetaInsertion>? htmlMetaInsertions)
    {
        for (var index = 0; index < matches.Count; index++)
        {
            var entry = matches[index];
            entry.Subscription.Enqueue(new MutationRecord(MutationRecordKind.ChildList, target,
                added, removed, previousSibling, nextSibling, targetWasConnected: matches.TargetWasConnected,
                htmlMetaInsertions: entry.CaptureHtmlMetaInsertions ? htmlMetaInsertions : null));
        }
        Notify(matches);
    }

    private static void Notify(MutationMatches matches)
    {
        // DOM §4.3.2 queues each interested observer's record before scheduling delivery.
        // Reuse the captured matches so reentrant notifications cannot reorder the outer
        // record behind nested records in a later subscription. Host exceptions still
        // propagate immediately and stop subsequent signals; all records are already queued.
        for (var i = 0; i < matches.Count; i++) matches[i].Subscription.NotifyIfPending();
    }
}

internal sealed class MutationMatches : IReadOnlyList<MutationMatch>
{
    // Nearly every match has one interested subscription; keep it inline, with no list or array.
    private MutationMatch _first;
    private List<MutationMatch>? _rest;
    private int _count;
    internal IReadOnlyList<MutationMatch> Entries => this;
    internal bool NeedsOldValue { get; private set; }
    internal bool CaptureHtmlMetaInsertions { get; private set; }
    internal bool TargetWasConnected { get; set; }

    public int Count => _count;

    public MutationMatch this[int index]
    {
        get
        {
            if ((uint) index >= (uint) _count) throw new ArgumentOutOfRangeException(nameof(index));
            return index == 0 ? _first : _rest![index - 1];
        }
    }

    internal void Add(MutationSubscription subscription, bool oldValue, bool captureHtmlMetaInsertions)
    {
        NeedsOldValue |= oldValue;
        CaptureHtmlMetaInsertions |= captureHtmlMetaInsertions;
        for (var i = 0; i < _count; i++)
        {
            var existing = this[i];
            if (ReferenceEquals(existing.Subscription, subscription))
            {
                var merged = new MutationMatch(subscription, existing.OldValue || oldValue,
                    existing.CaptureHtmlMetaInsertions || captureHtmlMetaInsertions);
                if (i == 0) _first = merged;
                else _rest![i - 1] = merged;

                return;
            }
        }

        var match = new MutationMatch(subscription, oldValue, captureHtmlMetaInsertions);
        if (_count == 0) _first = match;
        else (_rest ??= []).Add(match);
        _count++;
    }

    public IEnumerator<MutationMatch> GetEnumerator()
    {
        for (var i = 0; i < _count; i++) yield return this[i];
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

internal readonly record struct MutationMatch(MutationSubscription Subscription, bool OldValue, bool CaptureHtmlMetaInsertions);
