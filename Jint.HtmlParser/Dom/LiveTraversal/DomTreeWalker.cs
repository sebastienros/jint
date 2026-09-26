namespace Jint.HtmlParser;

// DOM §6.2. TreeWalker never retargets current on removal, nor constrains its setter.
// https://dom.spec.whatwg.org/#interface-treewalker
internal sealed class DomTreeWalker
{
    private DomNodeIdentity _current;
    private bool _active;
    internal DomTreeWalker(DomNodeIdentity root, uint whatToShow)
    {
        if (!root.IsValid) throw new ArgumentException("A valid identity is required.", nameof(root));
        Root = _current = root; WhatToShow = whatToShow;
    }
    internal DomNodeIdentity Root { get; }
    internal uint WhatToShow { get; }
    internal DomNodeIdentity Current
    {
        get => _current;
        set { if (!value.IsValid) throw new ArgumentException("A valid identity is required.", nameof(value)); _current = value; }
    }
    private ushort Filter(Node node, TraversalFilter? filter, ref TraversalWork work)
        => NativeFiltering.Filter(new(node), WhatToShow, filter, ref _active, ref work);
    private DomNodeIdentity Accept(Node node, ref TraversalWork work) { work.Check(); Current = new(node); return Current; }
    internal DomNodeIdentity? Parent(TraversalFilter? filter, CancellationToken cancellationToken)
    {
        var work = new TraversalWork(cancellationToken);
        var node = Current.Node;
        while (node is not null && !ReferenceEquals(node, Root.Node))
        {
            work.Step(); node = node.ParentNode;
            if (node is null) break;
            if (Filter(node, filter, ref work) == 1) return Accept(node, ref work);
        }
        work.Check(); return null;
    }
    internal DomNodeIdentity? FirstChild(TraversalFilter? filter, CancellationToken cancellationToken) => Children(true, filter, cancellationToken);
    internal DomNodeIdentity? LastChild(TraversalFilter? filter, CancellationToken cancellationToken) => Children(false, filter, cancellationToken);
    private DomNodeIdentity? Children(bool first, TraversalFilter? filter, CancellationToken token)
    {
        var work = new TraversalWork(token);
        var node = first ? Current.Node?.FirstChild : Current.Node?.LastChild;
        while (node is not null)
        {
            work.Step();
            var result = Filter(node, filter, ref work);
            if (result == 1) return Accept(node, ref work);
            if (result == 3 && (first ? node.FirstChild : node.LastChild) is { } child) { node = child; continue; }
            while (node is not null)
            {
                work.Step();
                if ((first ? node.NextSibling : node.PreviousSibling) is { } sibling) { node = sibling; break; }
                var parent = node.ParentNode;
                if (parent is null || ReferenceEquals(parent, Root.Node) || ReferenceEquals(parent, Current.Node)) { work.Check(); return null; }
                node = parent;
            }
        }
        work.Check(); return null;
    }
    internal DomNodeIdentity? NextSibling(TraversalFilter? filter, CancellationToken cancellationToken) => Siblings(true, filter, cancellationToken);
    internal DomNodeIdentity? PreviousSibling(TraversalFilter? filter, CancellationToken cancellationToken) => Siblings(false, filter, cancellationToken);
    private DomNodeIdentity? Siblings(bool next, TraversalFilter? filter, CancellationToken token)
    {
        var work = new TraversalWork(token);
        var node = Current.Node;
        if (node is null || ReferenceEquals(node, Root.Node)) { work.Check(); return null; }
        while (true)
        {
            work.Step();
            var sibling = next ? node.NextSibling : node.PreviousSibling;
            while (sibling is not null)
            {
                work.Step(); node = sibling;
                var result = Filter(node, filter, ref work);
                if (result == 1) return Accept(node, ref work);
                sibling = next ? node.FirstChild : node.LastChild;
                if (result == 2 || sibling is null) sibling = next ? node.NextSibling : node.PreviousSibling;
            }
            var parent = node.ParentNode;
            if (parent is null || ReferenceEquals(parent, Root.Node)) { work.Check(); return null; }
            node = parent;
            if (Filter(node, filter, ref work) == 1) { work.Check(); return null; }
        }
    }
    internal DomNodeIdentity? Next(TraversalFilter? filter, CancellationToken cancellationToken)
    {
        var work = new TraversalWork(cancellationToken);
        var node = Current.Node;
        if (node is null) { work.Check(); return null; }
        ushort result = 1;
        while (true)
        {
            work.Step();
            while (result != 2 && node.FirstChild is { } child)
            {
                work.Step(); node = child; result = Filter(node, filter, ref work);
                if (result == 1) return Accept(node, ref work);
            }
            Node? sibling = null;
            for (var temporary = node; temporary is not null; temporary = temporary.ParentNode)
            {
                work.Step();
                if (ReferenceEquals(temporary, Root.Node)) { work.Check(); return null; }
                sibling = temporary.NextSibling;
                if (sibling is not null) break;
            }
            if (sibling is null) { work.Check(); return null; }
            node = sibling; result = Filter(node, filter, ref work);
            if (result == 1) return Accept(node, ref work);
        }
    }
    internal DomNodeIdentity? Previous(TraversalFilter? filter, CancellationToken cancellationToken)
    {
        var work = new TraversalWork(cancellationToken);
        var node = Current.Node;
        while (node is not null && !ReferenceEquals(node, Root.Node))
        {
            work.Step(); var sibling = node.PreviousSibling;
            while (sibling is not null)
            {
                work.Step(); node = sibling;
                var result = Filter(node, filter, ref work);
                while (result != 2 && node.LastChild is { } child)
                {
                    work.Step(); node = child; result = Filter(node, filter, ref work);
                }
                if (result == 1) return Accept(node, ref work);
                sibling = node.PreviousSibling;
            }
            if (ReferenceEquals(node, Root.Node) || node.ParentNode is null) break;
            node = node.ParentNode;
            if (Filter(node, filter, ref work) == 1) return Accept(node, ref work);
        }
        work.Check(); return null;
    }
}
