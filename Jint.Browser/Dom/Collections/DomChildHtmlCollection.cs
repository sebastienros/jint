using System.Runtime.CompilerServices;
using Jint.HtmlParser;

namespace Jint.Browser.Dom.Collections;

/// <summary>DOM §4.2.6's live, SameObject collection of immediate element children.</summary>
internal sealed class DomChildHtmlCollection(Node root) : DomHtmlCollection<Element>
{
    private static readonly ConditionalWeakTable<Node, DomChildHtmlCollection> Collections = new();
    internal static DomChildHtmlCollection Of(Node root) => Collections.GetValue(root, static node => new(node));

    internal override bool TryGetCountWitness(out Document? document, out ulong stamp)
    {
        document = root as Document ?? root.OwnerDocument;
        stamp = document?.MutationStamp ?? ulong.MaxValue;
        return document is not null && stamp != ulong.MaxValue;
    }

    internal override int Length
    {
        get
        {
            var count = 0;
            for (var child = root.FirstChild; child is not null; child = child.NextSibling)
                if (child is Element) count++;
            return count;
        }
    }

    internal override int GetLength(DomRealm realm)
    {
        var count = 0;
        foreach (var unused in Read(realm)) count++;
        return count;
    }

    /// <summary>
    /// The element child at <paramref name="index"/>, walked to from the last one read when the tree has not
    /// changed since.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why a cursor.</b> <c>for (let i = 0; i &lt; el.children.length; i++) el.children[i]</c> is how every
    /// framework and every audit walks a list, and reading the i-th child from the first one made that loop
    /// quadratic in the list — a 700-item list is 245 000 sibling steps for one pass, and axe-core makes several
    /// (#4619). <see cref="DomLiveHtmlCollection"/> keeps the same kind of cursor, for the same reason.
    /// </para>
    /// <para>
    /// <b>Only a cursor the tree vouches for is followed.</b> It is used when the document is the same one and
    /// its <see cref="Document.MutationStamp"/> has not moved since it was taken, which is the witness the
    /// collection's cached <c>length</c> already rests on, and it holds its element weakly so a removed child
    /// is never kept alive by a collection that read it once. The record is replaced, never mutated, because
    /// one collection per node is shared process-wide.
    /// </para>
    /// </remarks>
    internal override Element? GetItem(DomRealm realm, uint index)
    {
        var result = ReadItem(index, realm.NativeReadCheckpoint, realm.CancellationToken);
        realm.Engine.Constraints.Check();
        return result;
    }

    /// <inheritdoc cref="GetItem(DomRealm, uint)"/>
    /// <param name="index">The position among the element children.</param>
    /// <param name="checkpoint">Charged with every sibling step, as every native read is.</param>
    /// <param name="token">Observed at each check.</param>
    internal Element? ReadItem(uint index, Action<int>? checkpoint, CancellationToken token)
    {
        var work = new DomReadWork(checkpoint, token);
        work.Check();

        var witnessed = TryGetCountWitness(out var owner, out var stamp);
        Element? current = null;
        uint position = 0;

        if (witnessed && _cursor is { } cursor && cursor.Stamp == stamp
            && cursor.Owner.TryGetTarget(out var cursorOwner) && ReferenceEquals(cursorOwner, owner)
            && cursor.Element.TryGetTarget(out var remembered)
            // Behind the cursor and nearer the start than the cursor: walk from the start instead.
            && (index >= cursor.Index || cursor.Index - index <= index))
        {
            current = remembered;
            position = cursor.Index;
        }
        else
        {
            current = NextElement(root.FirstChild, work);
        }

        while (current is not null && position < index)
        {
            current = NextElement(current.NextSibling, work);
            position++;
        }

        while (current is not null && position > index)
        {
            current = PreviousElement(current.PreviousSibling, work);
            position--;
        }

        work.Check();

        if (current is not null && witnessed && TryGetCountWitness(out var after, out var afterStamp)
            && ReferenceEquals(after, owner) && afterStamp == stamp)
        {
            _cursor = new Cursor(new WeakReference<Element>(current), position, new WeakReference<Document>(owner!), stamp);
        }

        return current;
    }

    private Cursor? _cursor;

    /// <summary>The last child read, its position among the element children, and the tree it was read in.</summary>
    private sealed record Cursor(WeakReference<Element> Element, uint Index, WeakReference<Document> Owner, ulong Stamp);

    private static Element? NextElement(Node? node, DomReadWork work)
    {
        for (; node is not null; node = node.NextSibling)
        {
            work.Step();
            if (node is Element element) return element;
        }

        return null;
    }

    private static Element? PreviousElement(Node? node, DomReadWork work)
    {
        for (; node is not null; node = node.PreviousSibling)
        {
            work.Step();
            if (node is Element element) return element;
        }

        return null;
    }

    internal override IEnumerable<Element> Read(DomRealm realm)
    {
        var work = new DomReadWork(realm.NativeReadCheckpoint, realm.CancellationToken);
        work.Check();
        for (var child = root.FirstChild; child is not null; child = child.NextSibling)
        {
            work.Step();
            if (child is Element element) yield return element;
        }
        work.Check();
    }

    internal static Element? First(Node root, Action<int>? checkpoint = null, CancellationToken token = default)
    {
        var work = new DomReadWork(checkpoint, token);
        work.Check();
        for (var child = root.FirstChild; child is not null; child = child.NextSibling)
        {
            work.Step();
            if (child is not Element element) continue;
            work.Check();
            return element;
        }
        work.Check();
        return null;
    }

    internal static Element? Last(Node root, Action<int>? checkpoint = null, CancellationToken token = default)
    {
        var work = new DomReadWork(checkpoint, token);
        work.Check();
        for (var child = root.LastChild; child is not null; child = child.PreviousSibling)
        {
            work.Step();
            if (child is not Element element) continue;
            work.Check();
            return element;
        }
        work.Check();
        return null;
    }

    public override IEnumerator<Element> GetEnumerator()
    {
        for (var child = root.FirstChild; child is not null; child = child.NextSibling)
            if (child is Element element) yield return element;
    }
}
