using Jint.HtmlParser;

namespace Jint.Browser.Dom.Collections;

/// <summary>Allocation-free pre-order traversal of native element descendants.</summary>
internal struct DomElementWalker(Node root)
{
    private Node? _current;
    private bool _finished;
    internal readonly Element Current => (Element) _current!;

    internal bool MoveNext()
    {
        if (_finished)
        {
            return false;
        }
        var next = _current is null ? root.FirstChild : Next(_current);
        while (next is not null)
        {
            _current = next;
            if (next is Element)
            {
                return true;
            }
            next = Next(next);
        }
        _finished = true;
        return false;
    }

    private Node? Next(Node node)
    {
        if (node.FirstChild is { } child)
        {
            return child;
        }
        while (!ReferenceEquals(node, root))
        {
            if (node.NextSibling is { } sibling)
            {
                return sibling;
            }
            if (node.ParentNode is not { } parent)
            {
                return null;
            }
            node = parent;
        }
        return null;
    }
}
