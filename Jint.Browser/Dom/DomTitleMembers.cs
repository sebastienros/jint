using System.Text;
using Jint.HtmlParser;
using Jint.Native;

namespace Jint.Browser.Dom;

/// <summary>HTML §3.1.7 title selection and child text over the authoritative native tree.</summary>
internal static class DomTitleMembers
{
    // https://html.spec.whatwg.org/multipage/dom.html#document.title
    internal static string Get(DomRealm realm, Document document)
        => Get(document, realm.NativeReadCheckpoint, realm.CancellationToken);

    internal static string Get(Document document, Action<int>? checkpoint, CancellationToken token)
    {
        var work = new DomReadWork(checkpoint, token);
        var title = Find(document, work);
        return title is null ? string.Empty : ChildText(title, work, collapse: true);
    }

    internal static JsValue Set(DomRealm realm, Document document, string value)
    {
        var work = new DomReadWork(realm.NativeReadCheckpoint, realm.CancellationToken);
        work.Check();
        var root = Root(document, work);
        if (root is null) return JsValue.Undefined;
        var svg = root is { NamespaceUri: Namespaces.Svg, LocalName: "svg" };
        if (!svg && root.NamespaceUri != Namespaces.Html) return JsValue.Undefined;
        var title = Find(document, work);
        if (title is null)
        {
            Element? parent = svg ? root : null;
            if (!svg && root.LocalName == "html")
                foreach (var child in Children(root, work))
                    if (child is { NamespaceUri: Namespaces.Html, LocalName: "head" }) { parent = child; break; }
            work.Check();
            if (parent is null) return JsValue.Undefined;
            title = document.CreateElementNS(svg ? Namespaces.Svg : Namespaces.Html, "title");
            parent.InsertBefore(title, svg ? parent.FirstChild : null);
        }
        return SetText(realm, title, value);
    }

    internal static string Text(DomRealm realm, Element title)
        => ChildText(title, new DomReadWork(realm.NativeReadCheckpoint, realm.CancellationToken), collapse: false);

    internal static JsValue SetText(DomRealm realm, Element title, string value)
        => SetText(title, value, realm.NativeReadCheckpoint, realm.CancellationToken);

    internal static JsValue SetText(Element title, string value, Action<int>? checkpoint, CancellationToken token)
    {
        var work = new DomReadWork(checkpoint, token);
        work.Check();
        title.ReplaceChildren(value.Length == 0 ? null : title.OwnerDocument!.CreateTextNode(value));
        work.Check();
        return JsValue.Undefined;
    }

    private static Element? Find(Document document, DomReadWork work)
    {
        work.Check();
        if (Root(document, work) is Element { NamespaceUri: Namespaces.Svg, LocalName: "svg" } svg)
        {
            foreach (var child in Children(svg, work))
                if (child is { NamespaceUri: Namespaces.Svg, LocalName: "title" }) { work.Check(); return child; }
        }
        else
        {
            foreach (var element in NodeTraversal.DescendantElements(document, work.Check, work.Token))
                if (element is { NamespaceUri: Namespaces.Html, LocalName: "title" }) { work.Check(); return element; }
        }
        work.Check();
        return null;
    }

    private static Element? Root(Document document, DomReadWork work)
    {
        foreach (var element in Children(document, work))
        {
            work.Check();
            return element;
        }
        work.Check();
        return null;
    }

    private static IEnumerable<Element> Children(Node parent, DomReadWork work)
    {
        for (var child = parent.FirstChild; child is not null; child = child.NextSibling)
        {
            work.Step();
            if (child is Element element) yield return element;
        }
        work.Check();
    }

    private static string ChildText(Element title, DomReadWork work, bool collapse)
    {
        work.Check();
        var result = new StringBuilder();
        var pendingSpace = false;
        for (var child = title.FirstChild; child is not null; child = child.NextSibling)
        {
            work.Step();
            if (child is Text text)
                for (var i = 0; i < text.DataLength; i++) Append(text.DataAt(i));
            else if (child is CDataSection data)
                foreach (var character in data.Data) Append(character);
        }
        work.Check();
        var value = result.ToString();
        work.Check();
        return value;

        void Append(char character)
        {
            work.Step();
            if (collapse && character is ' ' or '\t' or '\n' or '\r' or '\f')
            {
                pendingSpace = result.Length != 0;
                return;
            }
            if (pendingSpace) { result.Append(' '); pendingSpace = false; }
            result.Append(character);
        }
    }
}
