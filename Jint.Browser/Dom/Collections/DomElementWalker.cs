using System.Runtime.CompilerServices;
using AngleSharp.Dom;

namespace Jint.Browser.Dom.Collections;

/// <summary>
/// A depth-first pre-order walk over the <b>element</b> descendants of one node, in tree order, allocating
/// nothing.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the binding has one at all.</b> The obvious spelling of a live collection's filter is
/// <c>root.Descendants&lt;IElement&gt;().Where(…)</c>, and that is what these collections used to be. It costs,
/// per read: a <c>Stack&lt;INode&gt;</c> and its backing array, which grows to the <i>width</i> of the tree
/// even when the very first element matches; a covariance store check on every push, because the stack holds
/// a reference type; three chained iterators (<c>GetDescendantsAndSelf</c>, <c>Skip(1)</c>,
/// <c>OfType</c>) plus the <c>Where</c>, so four virtual <c>MoveNext</c> calls and an interface type test per
/// node; and a delegate invocation per element. A profile of
/// <c>for (i = 0; i &lt; c.length; i++) c[i]</c> over four live collections on a 1,500-element document put
/// <c>NodeExtensions.GetDescendantsAndSelf.MoveNext</c> at 33.6% inclusive of the page thread, with the array
/// covariance helper alone at 6.1% self.
/// </para>
/// <para>
/// <b>Why an index stack and not a node stack.</b> This is the design AngleSharp's own internal
/// <c>ElementTreeEnumerator</c> arrived at, for the reason its comment gives: walking parent links and
/// keeping a stack of <i>child indices</i> stores value types, needs no covariance check, and is bounded by
/// the <i>depth</i> of the tree rather than its width. That type is internal to AngleSharp and cannot be
/// referenced, so this is the same shape written against the public <c>INode</c> surface —
/// <c>ChildNodes</c>, the indexer and <c>Parent</c> — and rooted at a node that need not itself be an
/// element, because <c>getElementsByTagName</c> is called on documents and fragments too.
/// </para>
/// <para>
/// <b>Why the index stack allocates nothing.</b> The first <see cref="InlineDepth"/> levels live in an
/// inline array inside this struct, which is where every real document's element depth fits; a deeper tree
/// copies them once into a heap array and grows that. The root is never yielded — these collections are over
/// <i>descendants</i> — and only elements are descended into, which is equivalent to visiting every
/// descendant and keeping the elements, since no non-element node has element descendants.
/// </para>
/// <para>
/// The walk reads the tree as it goes, so it must not be suspended across anything that can mutate the
/// document. Nothing between a caller's first <see cref="MoveNext"/> and its last runs script: the element
/// wrapper a read answers with is created after the walk has found it.
/// </para>
/// </remarks>
internal struct DomElementWalker
{
    /// <summary>Element depths at or below this cost no allocation at all.</summary>
    private const int InlineDepth = 16;

    private INode _current;
    private IndexBuffer _inline;
    private int[]? _overflow;
    private int _depth;
    private bool _finished;

    internal DomElementWalker(INode root)
    {
        _current = root;
        _inline = default;
        _overflow = null;
        _depth = 0;
        _finished = false;
    }

    /// <summary>The element the walk is positioned on. Undefined before the first <see cref="MoveNext"/>.</summary>
    internal readonly IElement Current => (IElement) _current;

    /// <summary>Advances to the next element descendant in tree order.</summary>
    internal bool MoveNext()
    {
        if (_finished)
        {
            return false;
        }

        var child = FirstElementFrom(_current, 0, out var childIndex);

        if (child is not null)
        {
            Push(childIndex);
            _current = child;
            return true;
        }

        while (_depth > 0)
        {
            // Only elements are ever descended into and the walk never leaves the root, so the parent of the
            // node it is positioned on is always the element it descended from -- unless the tree moved under
            // the walk, which ends it rather than dereferencing null.
            var parent = _current.Parent;

            if (parent is null)
            {
                break;
            }

            var sibling = FirstElementFrom(parent, TopIndex() + 1, out var siblingIndex);

            if (sibling is not null)
            {
                SetTopIndex(siblingIndex);
                _current = sibling;
                return true;
            }

            _depth--;
            _current = parent;
        }

        _finished = true;
        return false;
    }

    /// <summary>The first element child of <paramref name="parent"/> at or after <paramref name="start"/>.</summary>
    private static IElement? FirstElementFrom(INode parent, int start, out int index)
    {
        var children = parent.ChildNodes;
        var length = children.Length;

        for (var i = start; i < length; i++)
        {
            if (children[i] is IElement element)
            {
                index = i;
                return element;
            }
        }

        index = -1;
        return null;
    }

    private readonly int TopIndex() => _overflow is null ? _inline[_depth - 1] : _overflow[_depth - 1];

    private void SetTopIndex(int index)
    {
        if (_overflow is null)
        {
            _inline[_depth - 1] = index;
        }
        else
        {
            _overflow[_depth - 1] = index;
        }
    }

    private void Push(int index)
    {
        var depth = _depth;

        if (_overflow is null)
        {
            if (depth < InlineDepth)
            {
                _inline[depth] = index;
                _depth = depth + 1;
                return;
            }

            var promoted = new int[InlineDepth * 2];
            for (var i = 0; i < InlineDepth; i++)
            {
                promoted[i] = _inline[i];
            }

            _overflow = promoted;
        }
        else if (depth == _overflow.Length)
        {
            Array.Resize(ref _overflow, depth * 2);
        }

        _overflow[depth] = index;
        _depth = depth + 1;
    }

    /// <summary>The inline half of the index stack: <see cref="InlineDepth"/> child indices, no allocation.</summary>
    [InlineArray(InlineDepth)]
    private struct IndexBuffer
    {
#pragma warning disable CS0169, IDE0051 // The inline array's single field is reached through the indexer.
        private int _element0;
#pragma warning restore CS0169, IDE0051
    }
}
