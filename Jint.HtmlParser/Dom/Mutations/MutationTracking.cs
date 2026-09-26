using System.Collections.ObjectModel;

namespace Jint.HtmlParser;

internal static class MutationTracking
{
    internal static MutationMatches? Match(Node target, MutationRecordKind kind,
        string? attributeName = null, string? attributeNamespace = null)
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
        for (var ancestor = target; ancestor is not null; ancestor = ancestor.ParentNode)
        {
            root = ancestor;
            if (ancestor.MutationRegistrations is not { } registrations)
            {
                continue;
            }

            foreach (var entry in registrations)
            {
                var options = entry.Registration.Options;
                if ((!ReferenceEquals(ancestor, target) && !options.Subtree) ||
                    !options.Matches(kind, attributeName, attributeNamespace))
                {
                    continue;
                }

                (matches ??= new MutationMatches()).Add(entry.Registration.Subscription,
                    kind == MutationRecordKind.Attributes && options.AttributeOldValue ||
                    kind == MutationRecordKind.CharacterData && options.CharacterDataOldValue);
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
            foreach (var entry in registrations)
            {
                if (entry.Registration.Options.Subtree)
                {
                    removed.AddMutationRegistration(entry.Registration, transient: true);
                }
            }
        }
    }

    internal static void QueueChildList(Node target, Node? added, Node? removed,
        Node? previousSibling, Node? nextSibling, MutationMatches? matches = null)
    {
        matches ??= Match(target, MutationRecordKind.ChildList);
        if (matches is null)
        {
            return;
        }

        var addedNodes = added is null ? MutationRecord.EmptyNodes : SnapshotSingle(added);
        var removedNodes = removed is null ? MutationRecord.EmptyNodes : SnapshotSingle(removed);
        EmitChildList(target, addedNodes, removedNodes, previousSibling, nextSibling, matches);
    }

    internal static void QueueChildList(Node target, IReadOnlyList<Node>? added, IReadOnlyList<Node>? removed,
        Node? previousSibling, Node? nextSibling, MutationMatches? matches = null)
    {
        matches ??= Match(target, MutationRecordKind.ChildList);
        if (matches is null)
        {
            return;
        }

        var addedNodes = Snapshot(added);
        var removedNodes = Snapshot(removed);
        EmitChildList(target, addedNodes, removedNodes, previousSibling, nextSibling, matches);
    }

    internal static void QueueAttribute(Element target, string localName, string? namespaceUri,
        string? oldValue, MutationMatches? matches = null)
    {
        matches ??= Match(target, MutationRecordKind.Attributes, localName, namespaceUri);
        if (matches is null)
        {
            return;
        }

        foreach (var entry in matches.Entries)
        {
            entry.Subscription.Queue(new MutationRecord(MutationRecordKind.Attributes, target,
                attributeName: localName, attributeNamespace: namespaceUri,
                oldValue: entry.OldValue ? oldValue : null));
        }
    }

    internal static void QueueCharacterData(Node target, string? oldValue, MutationMatches? matches = null)
    {
        matches ??= Match(target, MutationRecordKind.CharacterData);
        if (matches is null)
        {
            return;
        }

        foreach (var entry in matches.Entries)
        {
            entry.Subscription.Queue(new MutationRecord(MutationRecordKind.CharacterData, target,
                oldValue: entry.OldValue ? oldValue : null));
        }
    }

    private static ReadOnlyCollection<Node> SnapshotSingle(Node node) => Array.AsReadOnly(new[] { node });

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
        Node? previousSibling, Node? nextSibling, MutationMatches matches)
    {
        foreach (var entry in matches.Entries)
        {
            entry.Subscription.Queue(new MutationRecord(MutationRecordKind.ChildList, target,
                added, removed, previousSibling, nextSibling, targetWasConnected: matches.TargetWasConnected));
        }
    }
}

internal sealed class MutationMatches
{
    private readonly List<MutationMatch> _entries = [];
    internal IReadOnlyList<MutationMatch> Entries => _entries;
    internal bool NeedsOldValue { get; private set; }
    internal bool TargetWasConnected { get; set; }

    internal void Add(MutationSubscription subscription, bool oldValue)
    {
        NeedsOldValue |= oldValue;
        for (var i = 0; i < _entries.Count; i++)
        {
            if (ReferenceEquals(_entries[i].Subscription, subscription))
            {
                if (oldValue)
                {
                    _entries[i] = new MutationMatch(subscription, true);
                }

                return;
            }
        }

        _entries.Add(new MutationMatch(subscription, oldValue));
    }
}

internal readonly record struct MutationMatch(MutationSubscription Subscription, bool OldValue);
