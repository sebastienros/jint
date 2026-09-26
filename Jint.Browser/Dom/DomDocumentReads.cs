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

    // Page and protocol consumers use the same actual title algorithm as the binding.
    internal static string Title(DomRealm realm, Document? document)
        => document is null ? "" : DomTitleMembers.Get(realm, document);
}
