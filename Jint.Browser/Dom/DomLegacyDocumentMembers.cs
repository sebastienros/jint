using Jint.HtmlParser;
using Jint.Native;

namespace Jint.Browser.Dom;

internal static class DomLegacyDocumentMembers
{
    // HTML §3.1.1: body is the first HTML body/frameset child of the document element.
    // Its setter uses the actual native tree operation, including hierarchy validation.
    internal static JsValue SetBody(DomRealm realm, Document document, Element? value)
    {
        var work = new DomReadWork(realm.NativeReadCheckpoint, realm.CancellationToken);
        work.Check();
        if (value is not { NamespaceUri: Namespaces.Html, LocalName: "body" or "frameset" })
            return DomFailures.Refuse(realm, "Document.body", "HierarchyRequestError", "The body must be an HTML body or frameset element.");
        Element? root = null;
        for (var node = document.FirstChild; node is not null; node = node.NextSibling)
        {
            work.Step();
            if (node is Element element) { root = element; break; }
        }
        Element? oldBody = null;
        for (var node = root is { NamespaceUri: Namespaces.Html, LocalName: "html" } ? root.FirstChild : null;
             node is not null; node = node.NextSibling)
        {
            work.Step();
            if (node is Element { NamespaceUri: Namespaces.Html, LocalName: "body" or "frameset" } body)
            { oldBody = body; break; }
        }
        work.Check();
        if (ReferenceEquals(value, oldBody)) return JsValue.Undefined;
        if (oldBody is not null) oldBody.ParentNode!.ReplaceChild(value, oldBody);
        else if (root is not null) root.AppendChild(value);
        else return DomFailures.Refuse(realm, "Document.body", "HierarchyRequestError", "The document has no document element.");
        work.Check();
        return JsValue.Undefined;
    }
}
