using Jint.HtmlParser;

namespace Jint.Browser.CustomElements;

/// <summary>
/// Where a reaction comes from: AngleSharp's mutation records for the tree, and its
/// <c>IAttributeObserver</c> service for an attribute.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two channels, because neither covers the other's half.</b> A mutation record is what says an element
/// entered or left the document — the observer is registered on the document, so a record only ever arrives
/// for a node in the document tree, which is exactly when connectedness changes. But
/// <c>Document.QueueMutation</c> walks a node's inclusive ancestors, so an attribute written on a
/// <i>detached</i> element produces no record at all, and <c>el.setAttribute</c> before insertion is the
/// commonest thing a component does. The <c>IAttributeObserver</c> service is called for every element with
/// an owner document, attached or not, which is what makes <c>attributeChangedCallback</c> answerable.
/// </para>
/// <para>
/// <b>Both are arrivals, never deliveries.</b> AngleSharp has no <c>IEventLoop</c> registered — the same
/// decision <c>Observers/JsMutationObserver</c> argues — so both callbacks run inline, inside the DOM
/// operation that caused them and on whichever thread that operation was on. They enqueue; the drain decides
/// whether anything runs now, and it refuses to run script on the parser's thread.
/// </para>
/// <para>
/// <b>Two gaps this leaves, both AngleSharp's and both recorded in <c>Jint.Browser/Dom/AGENTS.md</c>.</b> A
/// write through <c>classList</c> notifies neither channel, so an observed <c>class</c> attribute changed
/// that way reports nothing; and a namespaced <c>setAttributeNS</c> notifies only the record channel, so it
/// reports only for a connected element. Every ordinary attribute write — <c>setAttribute</c>,
/// <c>removeAttribute</c>, <c>id</c>, <c>className</c>, an attribute node — reaches the service.
/// </para>
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
        if (record.NativeAttributes is not null) return;
        var subscription = element.OwnerDocument!.ObserveMutations(element,
            new MutationObserverOptions { Attributes = true, AttributeOldValue = true });
        subscription.PendingRecord = QueueNativeMutation;
        record.NativeAttributes = subscription;
        _nativeSubscriptions.Add(new WeakReference<MutationSubscription>(subscription));
        EnsureWatching(element.OwnerDocument!);
        var root = element as Node;
        while (root.ParentNode is { } parent) root = parent;
        if (root is ShadowRoot) EnsureWatchingTree(root);
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
    /// <b>The member is the door because a mutation record is not one.</b> The obvious alternative was to
    /// read an adoption off the removal channel — a node that left the observed document and now belongs
    /// to another one has been adopted — and it does not work: measured against the pinned AngleSharp, a
    /// removal record is delivered <i>before</i> the node's owner changes, so at the moment the record
    /// arrives the node is still this document's and there is nothing to report. The old document has to be
    /// read before the call, which only the member can do.
    /// </para>
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
    /// https://dom.spec.whatwg.org/#handle-attribute-changes — what AngleSharp's <c>IAttributeObserver</c>
    /// reports, turned into an <c>attributeChangedCallback</c> reaction for an observed name.
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
    /// It is the <i>detached</i> half of the picture. A connected element's <c>innerHTML</c> produces a
    /// mutation record, which upgraded and connected everything before AngleSharp's own call returned; a
    /// detached one produces none, and HTML upgrades there too — <c>div.innerHTML = '&lt;my-el&gt;'</c> on an
    /// element that is nowhere runs the constructor. So the subtree is walked here as well, which is a
    /// second, idempotent pass for the connected case: every element it finds is already custom.
    /// </para>
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

        registry.UpgradeSubtree(root);
        registry.Drain();
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#concept-node-clone: a copy is created with <b>node's is value</b>, and
    /// then upgraded the way <see cref="SubtreeCreated"/> upgrades anything else a member just made.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The is value is a slot, not the <c>is</c> content attribute</b>, and the difference is the whole
    /// of this method. <c>document.createElement('button', { is: 'x-y' })</c> and <c>new XY()</c> set the
    /// slot and add no attribute, so AngleSharp's clone — which copies attributes and nothing else — handed
    /// back an element with no way to find its definition, and <c>customized.cloneNode()</c> answered a plain
    /// built-in. An element whose <c>is</c> attribute says something <i>else</i> is the same rule read from
    /// the other side: the slot wins, and DOM says so.
    /// </para>
    /// <para>
    /// The two trees are walked in lockstep rather than the copy alone, because only the source knows what
    /// each element's slot held. An explicit stack for the reason <see cref="Walk"/> has one — the depth is
    /// a stranger's document — and pairing by index is what AngleSharp's own clone produces.
    /// </para>
    /// </remarks>
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
