using System.Text;
using Jint.HtmlParser;

namespace Jint.Browser.Dom;

internal static class DomDocumentReads
{
    internal static Element? ById(DomRealm realm, Node root, string id)
    {
        if (id.Length == 0) return null;
        var work = new DomReadWork(realm.NativeReadCheckpoint, realm.CancellationToken);
        work.Check();
        foreach (var element in NodeTraversal.DescendantElements(root, work.Check, work.Token))
        {
            if (!work.Equal(work.Attribute(element, "id"), id)) continue;
            work.Check();
            return element;
        }
        work.Check();
        return null;
    }

    // HTML §3.2.2: the first title in tree order, with ASCII whitespace stripped and collapsed.
    internal static string Title(DomRealm realm, Document? document)
    {
        if (document is null) return "";
        var work = new DomReadWork(realm.NativeReadCheckpoint, realm.CancellationToken);
        work.Check();
        Element? title = null;
        if (document.DocumentElement is { NamespaceUri: Namespaces.Svg, LocalName: "svg" } svg)
        {
            for (var child = svg.FirstChild; child is not null; child = child.NextSibling)
            {
                work.Step();
                if (child is not Element { NamespaceUri: Namespaces.Svg, LocalName: "title" } element) continue;
                title = element;
                break;
            }
        }
        else
        {
            foreach (var element in NodeTraversal.DescendantElements(document, work.Check, work.Token))
            {
                if (element is not { NamespaceUri: Namespaces.Html, LocalName: "title" }) continue;
                title = element;
                break;
            }
        }
        work.Check();
        if (title is null) return "";
        var text = DomDescendantText.Read(title, realm.NativeReadCheckpoint, work.Token);
        var result = new StringBuilder();
        var pendingSpace = false;
        foreach (var character in text)
        {
            work.Step();
            if (character is ' ' or '\t' or '\r' or '\n' or '\f')
            {
                pendingSpace = result.Length != 0;
                continue;
            }
            if (pendingSpace) result.Append(' ');
            pendingSpace = false;
            result.Append(character);
        }
        work.Check();
        var value = result.ToString();
        work.Check();
        return value;
    }
}
