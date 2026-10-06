using Jint.HtmlParser;

namespace Jint.Browser.Dom;

internal static class DomDocumentReads
{
    internal static Element? ById(DomRealm realm, Node root, string id)
    {
        if (id.Length == 0) return null;
        var work = new Jint.HtmlParser.Css.Selectors.SelectorMatchWork(root, realm.CancellationToken,
            () => realm.NativeReadCheckpoint(256));
        return NativeIdIndex.Find(root, id, ref work);
    }

    // Page and protocol consumers use the same actual title algorithm as the binding.
    internal static string Title(DomRealm realm, Document? document)
        => document is null ? "" : DomTitleMembers.Get(realm, document);
}
