using Jint.HtmlParser;
using Jint.Browser.Dom;

namespace Jint.Browser.CustomElements;

/// <summary>
/// Collects native tree and attribute mutation records for custom-element reactions.
/// </summary>
/// <remarks>
/// Tree subscriptions watch documents and shadow roots. Per-element attribute subscriptions also watch
/// detached elements and retain old values. Trusted pending-record callbacks only enqueue work;
/// FlushNativeMutations consumes records and the reaction drain decides when script may run.
/// A DOM mutator must finish before delivering reactions, and parser-thread arrivals never run script.
/// </remarks>
internal sealed partial class CustomElementRegistry
{
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<Node, MutationSubscription> _trees = new();
    private readonly List<WeakReference<MutationSubscription>> _nativeSubscriptions = [];
    private readonly Queue<MutationSubscription> _pendingNativeMutations = new();
    private readonly HashSet<MutationSubscription> _pendingNativeSubscriptions = new();

    internal void EnsureWatching(Document document)
    {
        EnsureWatchingTree(document);
    }

    internal void EnsureWatchingNode(Node node)
    {
        if ((node as Document ?? node.OwnerDocument) is { } document) EnsureWatching(document);
        var work = new DomReadWork(_ => _runtime.Engine.Constraints.Check(), _runtime.Dom.CancellationToken);
        work.Check();
        if (work.Root(node) is ShadowRoot shadow) EnsureWatchingTree(shadow);
    }

    private void EnsureWatchingTree(Node root)
    {
        if (_trees.TryGetValue(root, out _)) return;
        var subscription = (root as Document ?? root.OwnerDocument!).ObserveMutations(root,
            new MutationObserverOptions { ChildList = true, Subtree = true });
        subscription.PendingRecord = QueueNativeMutation;
        _nativeSubscriptions.Add(new WeakReference<MutationSubscription>(subscription));
        _trees.Add(root, subscription);
    }

    // Trusted native scheduling only. Reactions are delivered after the complete DOM call.
    private void QueueNativeMutation(MutationSubscription source)
    {
        if (_pendingNativeSubscriptions.Add(source)) _pendingNativeMutations.Enqueue(source);
        Schedule();
    }

    private void ObserveAttributes(Element element, CustomElementRecord record)
    {
        EnsureWatching(element.OwnerDocument!);
        var work = new DomReadWork(_ => _runtime.Engine.Constraints.Check(), _runtime.Dom.CancellationToken);
        work.Check();
        var root = work.Root(element);
        if (root is ShadowRoot) EnsureWatchingTree(root);
        if (record.NativeAttributes is not null) return;
        var subscription = element.OwnerDocument!.ObserveMutations(element,
            new MutationObserverOptions { Attributes = true, AttributeOldValue = true });
        subscription.PendingRecord = QueueNativeMutation;
        record.NativeAttributes = subscription;
        _nativeSubscriptions.Add(new WeakReference<MutationSubscription>(subscription));
    }

    private void ReleaseNativeSubscriptions()
    {
        foreach (var weak in _nativeSubscriptions)
        {
            if (weak.TryGetTarget(out var subscription)) subscription.Dispose();
        }
        _nativeSubscriptions.Clear();
        _pendingNativeMutations.Clear();
        _pendingNativeSubscriptions.Clear();
        _trees.Clear();
    }

    internal void FlushNativeMutations()
    {
        while (_pendingNativeMutations.TryDequeue(out var source))
        {
            _pendingNativeSubscriptions.Remove(source);
            var records = source.TakeRecordsForDelivery();
            for (var i = 0; i < records.Count; i++)
            {
                _runtime.Engine.Constraints.Check();
                var mutation = records[i];
                if (mutation.Kind == MutationRecordKind.ChildList)
                {
                    if (!mutation.TargetWasConnected) continue;
                    foreach (var removed in mutation.RemovedNodes) Walk(removed, Disconnected);
                    foreach (var added in mutation.AddedNodes) Walk(added, Connected);
                }
                else if (mutation.Kind == MutationRecordKind.Attributes && mutation.Target is Element element
                    && TryGetRecord(element) is { State: CustomElementState.Custom } record)
                {
                    var name = mutation.AttributeName!;
                    var value = element.GetAttributeNS(mutation.AttributeNamespace, name);
                    // The next record's old value is this mutation's new value, including removals.
                    for (var j = i + 1; j < records.Count; j++)
                    {
                        if ((j & 255) == 0) _runtime.Engine.Constraints.Check();
                        var next = records[j];
                        if (ReferenceEquals(next.Target, element) && next.AttributeName == name
                            && next.AttributeNamespace == mutation.AttributeNamespace)
                        {
                            value = next.OldValue;
                            break;
                        }
                    }
                    EnqueueCallback(element, record, CustomElementReactionKind.AttributeChanged,
                        name, mutation.OldValue, value);
                }
            }
        }
    }

    private void Connected(Element element)
    {
        if (TryGetRecord(element) is { State: CustomElementState.Custom } record)
        {
            ObserveAttributes(element, record);
            EnqueueCallback(element, record, CustomElementReactionKind.Connected);
            return;
        }

        TryUpgrade(element);
    }

    private void Disconnected(Element element)
    {
        if (TryGetRecord(element) is { State: CustomElementState.Custom } record)
        {
            EnqueueCallback(element, record, CustomElementReactionKind.Disconnected);
        }
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-document-adoptnode: <c>Adopt</c>, and then
    /// <a href="https://dom.spec.whatwg.org/#concept-node-adopt">adopt</a>'s step 3.2 — "for each
    /// inclusiveDescendant ... that is custom, enqueue a custom element callback reaction with callback name
    /// <c>adoptedCallback</c> and « oldDocument, document »".
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What that leaves is the adoption a page performs by inserting</b> —
    /// <c>otherDocument.body.appendChild(el)</c> and its siblings, where DOM's pre-insert adopts on the way
    /// past. Those enqueue no reaction here, and it is half of a larger gap rather than a hole of its own:
    /// an element inserted into a document this page does not observe gets no <c>connectedCallback</c>
    /// either, so the sequence <c>custom-elements/reactions/</c> asks for — disconnected, adopted,
    /// connected — needs a second observed document and not a second reaction. Ten rows of
    /// <c>WptBrowserExclusions</c>'s "one [CEReactions] member per file" group are that sequence, and they
    /// stay excluded.
    /// </para>
    /// <para>
    /// The removal that adopting a <i>connected</i> node performs still reports itself the ordinary way, so
    /// <c>disconnectedCallback</c> runs from the record — inside <c>Adopt</c> — and
    /// <c>adoptedCallback</c> is enqueued after it returns, which is DOM's own order.
    /// </para>
    /// </remarks>
    internal static Node Adopt(Dom.DomRealm realm, Document document, Node node)
    {
        if (Of(realm.Engine) is not { HasDefinitions: true } registry)
        {
            return document.AdoptNode(node);
        }

        var oldDocument = node.OwnerDocument;
        var adopted = document.AdoptNode(node);
        registry.FlushNativeMutations();

        if (oldDocument is not null && !ReferenceEquals(oldDocument, document))
        {
            registry.Adopted(adopted, oldDocument, document);
            registry.Drain();
        }

        return adopted;
    }

    /// <summary>Step 3.2 itself, over the adopted node's subtree in tree order.</summary>
    private void Adopted(Node root, Document oldDocument, Document newDocument)
    {
        Walk(root, element =>
        {
            if (TryGetRecord(element) is { State: CustomElementState.Custom } record)
            {
                EnqueueCallback(element, record, CustomElementReactionKind.Adopted, oldDocument: oldDocument, newDocument: newDocument);
            }
        });
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#handle-attribute-changes — queues attributeChangedCallback for an observed attribute name.
    /// </summary>
    /// <remarks>
    /// The service reports the element, the local name and the <b>new</b> value; the old one comes from the
    /// record's snapshot, which the upgrade seeded and every call since has kept current. A namespace is
    /// always reported as <see langword="null"/>, because the service does not carry one — see this file's
    /// remarks.
    /// </remarks>
    internal void AttributeChanged(Element element, string name, string? value)
    {
        if (_byName.Count == 0 || TryGetRecord(element) is not { State: CustomElementState.Custom } record)
        {
            return;
        }

        if (record.Definition is not { } definition || !definition.Observes(name))
        {
            return;
        }

        var values = record.Attributes ??= new Dictionary<string, string?>(StringComparer.Ordinal);
        values.TryGetValue(name, out var old);
        values[name] = value;

        EnqueueCallback(element, record, CustomElementReactionKind.AttributeChanged, name, old, value);
        Drain();
    }

    /// <summary>
    /// Upgrades whatever a member just created — a parsed fragment, a clone — then runs the reactions
    /// before that member returns.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The walk is skipped outright when nothing has been defined, which is what keeps <c>innerHTML</c> free
    /// for every page that has no custom elements.
    /// </para>
    /// </remarks>
    internal static void SubtreeCreated(Dom.DomRealm realm, Node? root)
    {
        if (root is null || Of(realm.Engine) is not { HasDefinitions: true } registry)
        {
            return;
        }

        // Translate completed removal/insertion records before queuing upgrades for the
        // new subtree, so old disconnection reactions precede new connection reactions.
        registry.FlushNativeMutations();
        registry.UpgradeSubtree(root);
        registry.Drain();
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#concept-node-clone: a copy is created with <b>node's is value</b>, and
    /// then upgraded the way <see cref="SubtreeCreated"/> upgrades anything else a member just made.
    /// </summary>
    internal static void Cloned(Dom.DomRealm realm, Node source, Node copy)
    {
        if (Of(realm.Engine) is not { } registry)
        {
            return;
        }

        if (registry.HasDefinitions)
        {
            registry.UpgradeSubtree(copy);
            registry.Drain();
        }
    }

}
