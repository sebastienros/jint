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

    internal static Element? First(Node root)
    {
        for (var child = root.FirstChild; child is not null; child = child.NextSibling)
            if (child is Element element) return element;
        return null;
    }

    internal static Element? Last(Node root)
    {
        for (var child = root.LastChild; child is not null; child = child.PreviousSibling)
            if (child is Element element) return element;
        return null;
    }

    public override IEnumerator<Element> GetEnumerator()
    {
        for (var child = root.FirstChild; child is not null; child = child.NextSibling)
            if (child is Element element) yield return element;
    }
}
