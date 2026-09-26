using System.Runtime.CompilerServices;
using Jint.HtmlParser;

namespace Jint.Browser.Dom.Collections;

/// <summary>DOM §4.2.6's live, SameObject collection of immediate element children.</summary>
internal sealed class DomChildHtmlCollection(Node root) : DomHtmlCollection<Element>
{
    private static readonly ConditionalWeakTable<Node, DomChildHtmlCollection> Collections = new();
    internal static DomChildHtmlCollection Of(Node root) => Collections.GetValue(root, static node => new(node));

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

    internal override Element? GetItem(DomRealm realm, uint index)
    {
        Element? result = null;
        foreach (var element in Read(realm))
        {
            if (index-- != 0) continue;
            result = element;
            break;
        }
        realm.Engine.Constraints.Check();
        return result;
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
