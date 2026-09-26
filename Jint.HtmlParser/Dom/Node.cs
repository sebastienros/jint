namespace Jint.HtmlParser;

/// <summary>The native kinds of nodes in a document tree.</summary>
public enum NodeType
{
    Element = 1,
    Text = 3,
    CDataSection = 4,
    ProcessingInstruction = 7,
    Comment = 8,
    Document = 9,
    DocumentType = 10,
    DocumentFragment = 11
}

/// <summary>A stable-identity node in a mutable document tree.</summary>
/// <remarks>A document and all its nodes are single-owner mutable objects. A host must serialize access.</remarks>
public abstract partial class Node
{
    private Document? _ownerDocument;
    internal HtmlFormIndex? FormIndex;
    internal HtmlRadioGroupIndex? RadioIndex;
    internal HtmlFormWorkProbe? FormWorkProbe;
    // Stored distribution and manual intent are separate DOM concepts. A manual
    // link is weak so a detached slottable does not retain an otherwise dead slot.
    internal Element? StoredAssignedSlot;
    internal WeakReference<Element>? ManualSlot;
    internal ShadowRoot? TreeShadowRoot;

    internal EndpointBucket? RangeEndpoints;
    internal List<WeakReference<DomNodeIterator>>? RootIterators;
    internal int IteratorRootSweepCursor;

    internal Node(Document? ownerDocument) => _ownerDocument = ownerDocument;

    public abstract NodeType NodeType { get; }
    public Document? OwnerDocument => _ownerDocument;
    public Node? ParentNode { get; private set; }
    public Node? FirstChild { get; private set; }
    public Node? LastChild { get; private set; }
    public Node? PreviousSibling { get; private set; }
    public Node? NextSibling { get; private set; }
    public int ChildCount { get; private set; }

    /// <summary>Enumerates the current child links. Mutation during enumeration is unsupported.</summary>
    public IEnumerable<Node> ChildNodes
    {
        get
        {
            for (var child = FirstChild; child is not null; child = child.NextSibling)
            {
                yield return child;
            }
        }
    }

    /// <summary>Creates a detached copy of this node, optionally including descendants.</summary>
    public Node CloneNode(bool deep = false) => NodeCloner.Clone(this, this as Document ?? _ownerDocument!, deep);

    // A clone is already valid by its source tree. Preserve the insertion's
    // assignment steps without repeating public ancestor validation.
    internal void AppendClonedChild(Node child, CancellationToken cancellationToken = default)
        => AppendClonedChild(child, null, cancellationToken);
    internal void AppendClonedChild(Node child, HtmlSelectWorkContext? context, CancellationToken cancellationToken)
    {
        using var rangeMutation = new RangeMutationScope(this as Document ?? _ownerDocument!, child.OwnerDocument);
        LiveTraversalTracking.Insert(this, null, 1);
        LinkBefore(child, null);
        SlotAssignment.AfterInsertion(this, child, null);
        HtmlFormAssociation.Inserted(child);
        HtmlSelectMutations.InsertedWithWork(child, markDocument: false, context, cancellationToken);
        HtmlTextAreaMutations.ChildrenChanged(this, mayShorten: false, markDocument: false);
    }

    // Trusted fresh-node parser insertion. The caller has established the full
    // document shape and host-inclusive cycle conditions before this O(1) link.
    internal void AppendParsedChild(Node child)
    {
        using var rangeMutation = new RangeMutationScope(this as Document ?? _ownerDocument!, child?.OwnerDocument);
        ArgumentNullException.ThrowIfNull(child);
        EnsureContainer();
        EnsureFreshParsedChild(child);
        InsertValidated(child, null);
    }

    // DOM Standard §4.2.3 insertion steps. The parser has already proved
    // document shape and host-inclusive acyclicity.
    // Only directly inspectable preconditions are checked here; an ancestor walk
    // would repeat the same prefix for every fostered insertion.
    internal void InsertParsedBefore(Node child, Node? referenceChild, CancellationToken cancellationToken)
        => InsertParsedBefore(child, referenceChild, null, cancellationToken);

    // Per-invocation checkpoint permits a test to cancel after the complete
    // semantic commit. It is neither stored nor used by production parsing.
    internal void InsertParsedBefore(Node child, Node? referenceChild, Action? afterCommitCheckpoint,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(child);
        EnsureContainer();
        if (referenceChild is not null && !ReferenceEquals(referenceChild.ParentNode, this))
        {
            throw new InvalidOperationException("Parsed insertion requires a child of this destination as its reference.");
        }

        EnsureFreshParsedChild(child);
        cancellationToken.ThrowIfCancellationRequested();
        InsertValidated(child, referenceChild);
        cancellationToken.ThrowIfCancellationRequested();
        afterCommitCheckpoint?.Invoke();
        cancellationToken.ThrowIfCancellationRequested();
    }

    private void EnsureFreshParsedChild(Node child)
    {
        if (ReferenceEquals(child, this) ||
            ReferenceEquals((this as DocumentFragment)?.Host, child) ||
            child is Document or DocumentFragment || child.ParentNode is not null ||
            child.FirstChild is not null || child.LastChild is not null || child.ChildCount != 0 ||
            child.PreviousSibling is not null || child.NextSibling is not null ||
            child.MutationRegistrations is not null ||
            !ReferenceEquals(child.OwnerDocument, this as Document ?? _ownerDocument))
        {
            throw new InvalidOperationException("Parsed insertion requires a fresh detached node with this owner.");
        }
    }

    // DOM Standard §4.2.3: pre-insert, replace and remove algorithms. Validation
    // precedes link changes so a failed insertion leaves both trees intact.
    public Node AppendChild(Node child) => InsertBefore(child, null);

    internal void EnsurePreInsert(Node child, Node? referenceChild)
    {
        EnsureContainer();
        if (child is ShadowRoot) throw DomException.Hierarchy();
        if (referenceChild is not null && !ReferenceEquals(referenceChild.ParentNode, this)) throw DomException.NotFound();
        RejectAncestor(child);
        ValidateInsertion(CollectIncoming(child), referenceChild, null);
    }

    public Node InsertBefore(Node child, Node? referenceChild)
    {
        using var rangeMutation = new RangeMutationScope(this as Document ?? _ownerDocument!, child?.OwnerDocument);
        ArgumentNullException.ThrowIfNull(child);
        EnsureContainer();
        if (referenceChild is not null && referenceChild.ParentNode != this)
        {
            throw DomException.NotFound();
        }

        if (ReferenceEquals(child, referenceChild))
        {
            referenceChild = child.NextSibling;
        }

        RejectAncestor(child);

        var incoming = CollectIncoming(child);
        ValidateInsertion(incoming, referenceChild, null);
        var destinationDocument = this as Document ?? _ownerDocument!;
        if (child is DocumentFragment)
        {
            if (incoming.Count == 0)
            {
                return child;
            }

            var previousSibling = referenceChild is null ? LastChild : referenceChild.PreviousSibling;
            for (var i = 0; i < incoming.Count; i++)
            {
                Detach(incoming[i], suppressRecord: true);
            }

            MutationTracking.QueueChildList(child, (IReadOnlyList<Node>?) null, incoming.Many, null, null);
            LiveTraversalTracking.Insert(this, referenceChild, (uint) incoming.Count);
            for (var i = 0; i < incoming.Count; i++)
            {
                var node = incoming[i];
                Adopt(node, destinationDocument);
                InsertValidated(node, referenceChild, suppressRecord: true, suppressSemantic: true, suppressLiveInsertion: true);
            }

            HtmlTextAreaMutations.ChildrenChanged(this, mayShorten: false);

            MutationTracking.QueueChildList(this, incoming.Many, null, previousSibling, referenceChild);
        }
        else
        {
            Detach(child);
            Adopt(child, destinationDocument);
            InsertValidated(child, referenceChild);
        }

        return child;
    }

    public Node ReplaceChild(Node child, Node oldChild)
    {
        using var rangeMutation = new RangeMutationScope(this as Document ?? _ownerDocument!, child?.OwnerDocument);
        ArgumentNullException.ThrowIfNull(child);
        ArgumentNullException.ThrowIfNull(oldChild);
        EnsureContainer();
        if (oldChild.ParentNode != this)
        {
            throw DomException.NotFound();
        }

        RejectAncestor(child);

        var incoming = CollectIncoming(child);
        ValidateInsertion(incoming, oldChild, oldChild);
        var destinationDocument = this as Document ?? _ownerDocument!;
        var previous = oldChild.PreviousSibling;
        var anchor = oldChild.NextSibling;
        if (ReferenceEquals(anchor, child))
        {
            anchor = anchor.NextSibling;
        }

        var targetMatches = MutationTracking.Match(this, MutationRecordKind.ChildList);
        if (child is DocumentFragment)
        {
            Adopt(child, destinationDocument);
        }
        else
        {
            Detach(child);
            Adopt(child, destinationDocument);
        }

        var removed = oldChild.ParentNode is not null;
        if (removed)
        {
            Detach(oldChild, suppressRecord: true);
        }

        if (child is DocumentFragment)
        {
            for (var i = 0; i < incoming.Count; i++)
            {
                Detach(incoming[i], suppressRecord: true);
            }

            if (incoming.Count != 0)
            {
                MutationTracking.QueueChildList(child, (IReadOnlyList<Node>?) null, incoming.Many, null, null);
            }

            LiveTraversalTracking.Insert(this, anchor, (uint) incoming.Count);
            for (var i = 0; i < incoming.Count; i++)
            {
                var node = incoming[i];
                Adopt(node, destinationDocument);
                InsertValidated(node, anchor, suppressRecord: true, suppressSemantic: true, suppressLiveInsertion: true);
            }

            if (incoming.Count != 0) HtmlTextAreaMutations.ChildrenChanged(this, mayShorten: false);

            if (targetMatches is not null)
            {
                MutationTracking.QueueChildList(this, incoming.Many,
                    removed ? new[] { oldChild } : null, previous, anchor, targetMatches);
            }
        }
        else
        {
            InsertValidated(child, anchor, suppressRecord: true);
            MutationTracking.QueueChildList(this, child, removed ? oldChild : null,
                previous, anchor, targetMatches);
        }

        return oldChild;
    }

    public Node RemoveChild(Node child)
    {
        using var rangeMutation = new RangeMutationScope(this as Document ?? _ownerDocument!, child?.OwnerDocument);
        ArgumentNullException.ThrowIfNull(child);
        if (child.ParentNode != this)
        {
            throw DomException.NotFound();
        }

        Detach(child);
        return child;
    }

    /// <summary>Replaces all children with one node or a fragment's children, or clears them.</summary>
    public void ReplaceChildren(Node? replacement = null)
    {
        using var rangeMutation = new RangeMutationScope(this as Document ?? _ownerDocument!, replacement?.OwnerDocument);
        EnsureContainer();
        if (replacement is not null)
        {
            RejectAncestor(replacement);
        }

        var incoming = replacement is null ? default : CollectIncoming(replacement);
        ValidateReplacement(incoming);
        var destinationDocument = this as Document ?? _ownerDocument!;
        var targetMatches = MutationTracking.Match(this, MutationRecordKind.ChildList);
        List<Node>? removed = null;
        if (targetMatches is not null && ChildCount != 0)
        {
            removed = new List<Node>(ChildCount);
            for (var current = FirstChild; current is not null; current = current.NextSibling)
            {
                removed.Add(current);
            }
        }

        var removalLengths = HtmlTextAreaMutations.RemovalSuffixLengths(this);
        var removalIndex = 0;
        while (FirstChild is { } child)
        {
            Detach(child, suppressRecord: true, suppressSemantic: true);
            var remainingLength = removalLengths is null ? null :
                (uint?) (removalIndex + 1 < removalLengths.Length ? removalLengths[removalIndex + 1] : 0);
            HtmlTextAreaMutations.ChildrenChanged(this, knownApiLength: remainingLength);
            removalIndex++;
        }

        if (replacement is DocumentFragment && incoming.Count != 0)
        {
            for (var i = 0; i < incoming.Count; i++)
            {
                Detach(incoming[i], suppressRecord: true);
            }

            MutationTracking.QueueChildList(replacement, (IReadOnlyList<Node>?) null,
                incoming.Many, null, null);
        }

        for (var i = 0; i < incoming.Count; i++)
        {
            var node = incoming[i];
            if (replacement is not DocumentFragment)
            {
                Detach(node);
            }
            Adopt(node, destinationDocument);
            InsertValidated(node, null, suppressRecord: true, suppressSemantic: true);
        }

        if (incoming.Count != 0) HtmlTextAreaMutations.ChildrenChanged(this, mayShorten: false);

        if (targetMatches is not null && (incoming.Count != 0 || removed is { Count: > 0 }))
        {
            if (replacement is DocumentFragment)
            {
                MutationTracking.QueueChildList(this, incoming.Many, removed, null, null, targetMatches);
            }
            else if (incoming.Count != 0)
            {
                MutationTracking.QueueChildList(this, new[] { incoming[0] }, removed, null, null, targetMatches);
            }
            else
            {
                MutationTracking.QueueChildList(this, (IReadOnlyList<Node>?) null, removed, null, null, targetMatches);
            }
        }
    }

    internal void AdoptInto(Document destination)
    {
        using var rangeMutation = new RangeMutationScope(this as Document ?? _ownerDocument!, destination);
        Detach(this);
        Adopt(this, destination);
    }

    private void EnsureContainer()
    {
        if (this is not Document and not Element and not DocumentFragment)
        {
            throw DomException.Hierarchy();
        }
    }

    private void RejectAncestor(Node candidate)
    {
        for (Node? ancestor = this; ancestor is not null; ancestor = ancestor.ParentNode ?? (ancestor as DocumentFragment)?.Host)
        {
            if (ReferenceEquals(ancestor, candidate))
            {
                throw DomException.Hierarchy();
            }
        }
    }

    private static Incoming CollectIncoming(Node child)
    {
        if (child is not DocumentFragment)
        {
            return new Incoming(child);
        }

        var nodes = new List<Node>(child.ChildCount);
        for (var current = child.FirstChild; current is not null; current = current.NextSibling)
        {
            nodes.Add(current);
        }

        return new Incoming(nodes);
    }

    private void ValidateInsertion(Incoming incoming, Node? referenceChild, Node? replacedChild)
    {
        for (var i = 0; i < incoming.Count; i++)
        {
            var node = incoming[i];
            if (node is Document || node is DocumentType && this is not Document)
            {
                throw DomException.Hierarchy();
            }

            if (node is Text or CDataSection && this is Document)
            {
                throw DomException.Hierarchy();
            }

        }

        if (this is Document)
        {
            ValidateDocumentOrder(incoming, referenceChild, replacedChild);
        }
    }

    private void ValidateReplacement(Incoming incoming)
    {
        var seenElement = false;
        var seenDoctype = false;
        for (var i = 0; i < incoming.Count; i++)
        {
            var node = incoming[i];
            if (node is Document || node is DocumentType && this is not Document ||
                node is Text or CDataSection && this is Document)
            {
                throw DomException.Hierarchy();
            }

            if (this is Document)
            {
                if (node is Element)
                {
                    if (seenElement)
                    {
                        throw DomException.Hierarchy();
                    }

                    seenElement = true;
                }
                else if (node is DocumentType)
                {
                    if (seenDoctype || seenElement)
                    {
                        throw DomException.Hierarchy();
                    }

                    seenDoctype = true;
                }
            }
        }
    }

    private void ValidateDocumentOrder(Incoming incoming, Node? referenceChild, Node? replacedChild)
    {
        // Appending comments and processing instructions is common while parsing a
        // document, including in fragments. They cannot affect its one-element/doctype
        // order, so avoid a whole-document copy for every such append.
        if (referenceChild is null && replacedChild is null)
        {
            var ancillaryOnly = true;
            for (var i = 0; i < incoming.Count; i++)
            {
                if (incoming[i] is not Comment and not ProcessingInstruction)
                {
                    ancillaryOnly = false;
                    break;
                }
            }

            if (ancillaryOnly)
            {
                return;
            }
        }

        var resulting = new List<Node>(ChildCount + incoming.Count);
        var moved = incoming.Count > 8 ? incoming.ToSet() : null;
        var insertionIndex = 0;
        for (var current = FirstChild; current is not null; current = current.NextSibling)
        {
            if (ReferenceEquals(current, referenceChild))
            {
                insertionIndex = resulting.Count;
            }

            if (!ReferenceEquals(current, replacedChild) && !(moved?.Contains(current) ?? incoming.Contains(current)))
            {
                resulting.Add(current);
            }
        }

        // A moved node before the insertion point was removed from the candidate list.
        if (referenceChild is null)
        {
            insertionIndex = resulting.Count;
        }

        incoming.InsertInto(resulting, insertionIndex);
        var seenElement = false;
        var seenDoctype = false;
        foreach (var node in resulting)
        {
            switch (node)
            {
                case Element:
                    if (seenElement)
                    {
                        throw DomException.Hierarchy();
                    }

                    seenElement = true;
                    break;
                case DocumentType:
                    if (seenDoctype || seenElement)
                    {
                        throw DomException.Hierarchy();
                    }

                    seenDoctype = true;
                    break;
            }
        }
    }

    /// <summary>Removes empty Text nodes and merges adjacent exclusive Text nodes in this ordinary subtree.</summary>
    /// <remarks>CDATA sections separate merge runs. Live ranges and mutation records follow the DOM normalization steps.</remarks>
    public void Normalize() => NativeCharacterData.Normalize(this);

    internal void RemoveForNormalization(uint index)
    {
        using var rangeMutation = new RangeMutationScope(_ownerDocument!);
        Detach(this, knownIndex: index);
    }

    private static void Detach(Node node, bool suppressRecord = false, bool suppressSemantic = false, uint? knownIndex = null)
    {
        var parent = node.ParentNode;
        if (parent is null)
        {
            return;
        }

        LiveTraversalTracking.Remove(node, parent, knownIndex);
        IteratorTracking.Remove(node, parent as Document ?? parent.OwnerDocument!);
        var formRemoval = HtmlFormAssociation.BeforeRemoval(node, parent);
        var previousSibling = node.PreviousSibling;
        var nextSibling = node.NextSibling;
        var matches = suppressRecord ? null : MutationTracking.Match(parent, MutationRecordKind.ChildList);
        MutationTracking.CaptureTransients(parent, node);

        if (node.PreviousSibling is { } previous)
        {
            previous.NextSibling = node.NextSibling;
        }
        else
        {
            parent.FirstChild = node.NextSibling;
        }

        if (node.NextSibling is { } next)
        {
            next.PreviousSibling = node.PreviousSibling;
        }
        else
        {
            parent.LastChild = node.PreviousSibling;
        }

        parent.ChildCount--;
        node.ParentNode = null;
        node.PreviousSibling = null;
        node.NextSibling = null;
        (parent as Document ?? parent._ownerDocument!).MarkMutation();
        SlotAssignment.AfterRemoval(parent, node);
        HtmlFormAssociation.Removed(node, formRemoval);
        HtmlSelectMutations.Removed(node, parent);
        if (!suppressSemantic) HtmlTextAreaMutations.ChildrenChanged(parent);
        if (!suppressRecord)
        {
            MutationTracking.QueueChildList(parent, null, node, previousSibling, nextSibling, matches);
        }
    }

    private void LinkBefore(Node node, Node? referenceChild)
    {
        node.ParentNode = this;
        node.NextSibling = referenceChild;
        node.PreviousSibling = referenceChild?.PreviousSibling ?? (referenceChild is null ? LastChild : null);
        if (node.PreviousSibling is { } previous)
        {
            previous.NextSibling = node;
        }
        else
        {
            FirstChild = node;
        }

        if (referenceChild is not null)
        {
            referenceChild.PreviousSibling = node;
        }
        else
        {
            LastChild = node;
        }

        ChildCount++;
    }

    private void InsertValidated(Node node, Node? referenceChild, bool suppressRecord = false,
        bool suppressSemantic = false, bool suppressLiveInsertion = false)
    {
        using var rangeMutation = new RangeMutationScope(this as Document ?? _ownerDocument!);
        if (!suppressLiveInsertion) LiveTraversalTracking.Insert(this, referenceChild, 1);
        var previousSibling = referenceChild is null ? LastChild : referenceChild.PreviousSibling;
        LinkBefore(node, referenceChild);
        (this as Document ?? _ownerDocument!).MarkMutation();
        SlotAssignment.AfterInsertion(this, node, referenceChild);
        HtmlFormAssociation.Inserted(node);
        HtmlSelectMutations.Inserted(node);
        if (!suppressSemantic) HtmlTextAreaMutations.ChildrenChanged(this, mayShorten: false);
        if (!suppressRecord)
        {
            MutationTracking.QueueChildList(this, node, null, previousSibling, referenceChild);
        }
    }

    private static void Adopt(Node node, Document destination)
    {
        // Only a changed node document runs template adoption hooks. Explicit
        // same-document AdoptNode still detaches the node before reaching here.
        if (ReferenceEquals(node._ownerDocument, destination))
        {
            return;
        }

        var pending = new Stack<(Node Node, Document Owner, bool TemplateBoundary)>();
        pending.Push((node, destination, false));
        while (pending.TryPop(out var current))
        {
            // An already-owned template content fragment is a separate inert-owner
            // boundary. An already-owned shadow root or ordinary descendant is
            // still visited: the outer host's changed-document adoption walks
            // every shadow-including descendant, regardless of individual owners.
            var sameOwner = ReferenceEquals(current.Node._ownerDocument, current.Owner);
            if (current.TemplateBoundary && sameOwner)
            {
                continue;
            }

            // A shadow or detached ordinary root can retain its form index across
            // adoption. Its new node document must know that ID edits can reach it.
            if (current.Node.FormIndex is not null)
            {
                current.Owner.HasFormIndex = true;
            }

            if (!sameOwner)
            {
                var oldDocument = current.Node._ownerDocument;
                current.Node._ownerDocument = current.Owner;
                LiveTraversalTracking.Rehome(current.Node.RangeEndpoints, current.Owner);
                IteratorTracking.Rehome(current.Node.RootIterators, current.Owner);
                if (current.Node.MutationRegistrations is not null)
                {
                    current.Owner.MarkMutationRegistrationsPresent();
                }

                oldDocument?.MarkMutation();
                current.Owner.MarkMutation();
            }

            if (current.Node is Element element)
            {
                element.AdoptAttributes(current.Owner);
                if (element.CustomElementRegistry is null || !element.CustomElementRegistry.IsScoped)
                {
                    var parent = element.ParentNode;
                    var registry = element.CustomElementRegistry is not null || parent is null ||
                                   parent is DocumentFragment and not ShadowRoot
                        ? current.Owner.CustomElementRegistry
                        : parent switch
                        {
                            Element parentElement => parentElement.CustomElementRegistry,
                            ShadowRoot parentShadow => parentShadow.CustomElementRegistry,
                            Document parentDocument => parentDocument.CustomElementRegistry,
                            _ => null
                        };
                    element.SetCustomElementRegistry(EffectiveGlobalRegistry(registry));
                }

                if (element.AttachedShadowRoot is { } shadowRoot)
                {
                    pending.Push((shadowRoot, current.Owner, false));
                }

                if (element.TemplateContent is { } content && content is not ShadowRoot)
                {
                    pending.Push((content, current.Owner.GetTemplateContentsOwnerDocument(), true));
                }
            }
            else if (current.Node is ShadowRoot shadow &&
                     (shadow.CustomElementRegistry is null && !shadow.KeepCustomElementRegistryNull ||
                      shadow.CustomElementRegistry is { IsScoped: false }))
            {
                shadow.SetCustomElementRegistry(EffectiveGlobalRegistry(current.Owner.CustomElementRegistry));
            }

            for (var child = current.Node.FirstChild; child is not null; child = child.NextSibling)
            {
                pending.Push((child, current.Owner, false));
            }
        }
    }

    private static CustomElementRegistryIdentity? EffectiveGlobalRegistry(CustomElementRegistryIdentity? registry)
        => registry is { IsScoped: false } ? registry : null;

    // The normal element append needs no incoming-node collection allocation. A fragment
    // snapshots its children because detaching them changes the linked list as it is consumed.
    private readonly struct Incoming
    {
        private readonly Node? _single;
        private readonly List<Node>? _many;

        internal Incoming(Node single) => _single = single;
        internal Incoming(List<Node> many) => _many = many;
        internal int Count => _many?.Count ?? (_single is null ? 0 : 1);
        internal Node this[int index] => _many is null ? index == 0 ? _single! : throw new ArgumentOutOfRangeException(nameof(index)) : _many[index];
        internal bool Contains(Node node) => _many?.Contains(node) ?? ReferenceEquals(_single, node);
        internal IReadOnlyList<Node>? Many => _many;
        internal HashSet<Node> ToSet() => _many is null ? [_single!] : new HashSet<Node>(_many);
        internal void InsertInto(List<Node> destination, int index)
        {
            if (_many is not null)
            {
                destination.InsertRange(index, _many);
            }
            else if (_single is not null)
            {
                destination.Insert(index, _single);
            }
        }
    }
}
