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
public abstract class Node
{
    private Document? _ownerDocument;

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

    // A clone is already validated by its source tree. Link it directly so copying a
    // deep chain does not repeat the ancestor walk performed by public insertion.
    internal void AppendClonedChild(Node child) => LinkBefore(child, null);

    // Trusted fresh-node parser insertion. The caller has established the full
    // document shape and host-inclusive cycle conditions before this O(1) link.
    internal void AppendParsedChild(Node child)
    {
        ArgumentNullException.ThrowIfNull(child);
        EnsureContainer();
        if (child is Document or DocumentFragment || child.ParentNode is not null ||
            child.FirstChild is not null || child.LastChild is not null || child.ChildCount != 0 ||
            child.PreviousSibling is not null || child.NextSibling is not null ||
            !ReferenceEquals(child.OwnerDocument, this as Document ?? _ownerDocument))
        {
            throw new InvalidOperationException("Parsed insertion requires a fresh detached node with this owner.");
        }

        InsertValidated(child, null);
    }

    // DOM Standard §4.2.3: pre-insert, replace and remove algorithms. Validation
    // precedes link changes so a failed insertion leaves both trees intact.
    public Node AppendChild(Node child) => InsertBefore(child, null);

    public Node InsertBefore(Node child, Node? referenceChild)
    {
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
        for (var i = 0; i < incoming.Count; i++)
        {
            var node = incoming[i];
            Detach(node);
            Adopt(node, destinationDocument);
            InsertValidated(node, referenceChild);
        }

        return child;
    }

    public Node ReplaceChild(Node child, Node oldChild)
    {
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
        if (child is DocumentFragment)
        {
            Adopt(child, destinationDocument);
        }

        var anchor = oldChild.NextSibling;
        while (anchor is not null && incoming.Contains(anchor))
        {
            anchor = anchor.NextSibling;
        }

        Detach(oldChild);
        for (var i = 0; i < incoming.Count; i++)
        {
            var node = incoming[i];
            Detach(node);
            Adopt(node, destinationDocument);
            InsertValidated(node, anchor);
        }

        return oldChild;
    }

    public Node RemoveChild(Node child)
    {
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
        EnsureContainer();
        if (replacement is not null)
        {
            RejectAncestor(replacement);
        }

        var incoming = replacement is null ? default : CollectIncoming(replacement);
        ValidateReplacement(incoming);
        var destinationDocument = this as Document ?? _ownerDocument!;

        while (FirstChild is { } child)
        {
            Detach(child);
        }

        for (var i = 0; i < incoming.Count; i++)
        {
            var node = incoming[i];
            Detach(node);
            Adopt(node, destinationDocument);
            InsertValidated(node, null);
        }
    }

    internal void AdoptInto(Document destination)
    {
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
        for (Node? ancestor = this; ancestor is not null; ancestor = ancestor.ParentNode)
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

    private static void Detach(Node node)
    {
        var parent = node.ParentNode;
        if (parent is null)
        {
            return;
        }

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

    private void InsertValidated(Node node, Node? referenceChild)
    {
        LinkBefore(node, referenceChild);
        // Native insertion semantics and mutation delivery share this boundary.
    }

    private static void Adopt(Node node, Document destination)
    {
        if (ReferenceEquals(node._ownerDocument, destination))
        {
            return;
        }

        var pending = new Stack<Node>();
        pending.Push(node);
        while (pending.TryPop(out var current))
        {
            current._ownerDocument = destination;
            if (current is Element element)
            {
                element.AdoptAttributes(destination);
            }

            for (var child = current.FirstChild; child is not null; child = child.NextSibling)
            {
                pending.Push(child);
            }
        }
    }

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
