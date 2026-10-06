using System.Runtime.CompilerServices;
using Jint.HtmlParser;

namespace Jint.Browser.Dom.Collections;

/// <summary>A NodeList view over native identities. Its wrapper family is shared by live and static lists.</summary>
internal abstract class DomNodeList
{
    internal abstract int Length { get; }
    internal abstract Node this[int index] { get; }
    internal virtual int ReadLength(Action<int>? checkpoint, CancellationToken token) => Length;
    internal virtual Node? ReadItem(uint index, Action<int>? checkpoint, CancellationToken token)
        => index >= (uint) ReadLength(checkpoint, token) ? null : this[(int) index];
}

/// <summary>DOM §4.2.10's live child-node collection, associated with its actual node identity.</summary>
internal sealed class DomChildNodeList : DomNodeList
{
    private static readonly ConditionalWeakTable<object, DomChildNodeList> Lists = new();
    private readonly Node? _parent;
    private readonly ChildNodeCursor? _cursor;

    private DomChildNodeList(object owner)
    {
        _parent = owner as Node;
        if (_parent is not null) _cursor = ChildNodeCursor.Of(_parent);
    }

    internal static DomChildNodeList Of(object owner) => Lists.GetValue(owner, static value => new DomChildNodeList(value));

    internal override int Length => _parent?.ChildCount ?? 0;

    internal override Node this[int index]
        => ReadItem(unchecked((uint) index), null, default) ?? throw new ArgumentOutOfRangeException(nameof(index));

    internal override Node? ReadItem(uint index, Action<int>? checkpoint, CancellationToken token)
    {
        if (_parent is not null) return _cursor!.ReadItem(_parent, index, checkpoint, token);
        new DomReadWork(checkpoint, token).Check();
        return null;
    }
}
