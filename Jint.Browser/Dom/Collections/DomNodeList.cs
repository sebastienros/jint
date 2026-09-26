using System.Runtime.CompilerServices;
using Jint.HtmlParser;

namespace Jint.Browser.Dom.Collections;

/// <summary>A NodeList view over native identities. Its wrapper family is shared by live and static lists.</summary>
internal abstract class DomNodeList
{
    internal abstract int Length { get; }
    internal abstract Node this[int index] { get; }
}

/// <summary>DOM §4.2.10's live child-node collection, associated with its actual node identity.</summary>
internal sealed class DomChildNodeList : DomNodeList
{
    private static readonly ConditionalWeakTable<object, DomChildNodeList> Lists = new();
    private readonly Node? _parent;
    private readonly WeakReference<Node> _cursor = new(null!);
    private int _cursorIndex;
    private ulong _cursorStamp;

    private DomChildNodeList(object owner) => _parent = owner as Node;

    internal static DomChildNodeList Of(object owner) => Lists.GetValue(owner, static value => new DomChildNodeList(value));

    internal override int Length => _parent?.ChildCount ?? 0;

    internal override Node this[int index]
    {
        get
        {
            if ((uint) index >= (uint) Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            var document = _parent as Document ?? _parent!.OwnerDocument!;
            var stamp = document.MutationStamp;
            Node current;
            int position;
            if (stamp != ulong.MaxValue && _cursorStamp == stamp && index >= _cursorIndex && _cursor.TryGetTarget(out var remembered))
            {
                current = remembered;
                position = _cursorIndex;
            }
            else
            {
                current = _parent!.FirstChild!;
                position = 0;
            }

            while (position < index)
            {
                current = current.NextSibling!;
                position++;
            }

            // A weak cursor permits sequential indexing over native linked children without retaining
            // a removed child for this live collection's lifetime.
            _cursor.SetTarget(current);
            _cursorIndex = index;
            _cursorStamp = stamp;
            return current;
        }
    }
}
