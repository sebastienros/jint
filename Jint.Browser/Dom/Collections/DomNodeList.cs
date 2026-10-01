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
    private readonly WeakReference<Node> _cursor = new(null!);
    private readonly WeakReference<Document> _cursorDocument = new(null!);
    private int _cursorIndex;
    private ulong _cursorStamp;

    private DomChildNodeList(object owner) => _parent = owner as Node;

    internal static DomChildNodeList Of(object owner) => Lists.GetValue(owner, static value => new DomChildNodeList(value));

    internal override int Length => _parent?.ChildCount ?? 0;

    internal override Node this[int index]
        => ReadItem(unchecked((uint) index), null, default) ?? throw new ArgumentOutOfRangeException(nameof(index));

    internal override Node? ReadItem(uint index, Action<int>? checkpoint, CancellationToken token)
    {
        var work = new DomReadWork(checkpoint, token);
        work.Check();
        if (index >= (uint) Length) return null;
        var document = _parent as Document ?? _parent!.OwnerDocument!;
        var stamp = document.MutationStamp;
        Node current;
        uint position;
        if (stamp != ulong.MaxValue && _cursorStamp == stamp && _cursorDocument.TryGetTarget(out var previousDocument)
            && ReferenceEquals(previousDocument, document) && index >= (uint) _cursorIndex && _cursor.TryGetTarget(out var remembered))
        {
            current = remembered;
            position = (uint) _cursorIndex;
        }
        else
        {
            current = _parent!.FirstChild!;
            position = 0;
        }
        while (position < index)
        {
            work.Step();
            current = current.NextSibling!;
            position++;
        }
        work.Check();
        _cursor.SetTarget(current);
        _cursorDocument.SetTarget(document);
        _cursorIndex = (int) index;
        _cursorStamp = stamp;
        return current;
    }
}
