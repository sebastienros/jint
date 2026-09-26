using Jint.HtmlParser;

namespace Jint.Browser.Dom;

/// <summary>DOM §4.5 element creation on the document's actual native tree.</summary>
internal static class DomElementFactory
{
    // https://dom.spec.whatwg.org/#dom-document-createelement
    internal static Element Create(Document document, string localName)
        => document.CreateElement(localName);

    // https://dom.spec.whatwg.org/#internal-createelementns-steps
    internal static Element CreateNamespaced(Document document, string? namespaceUri, string qualifiedName)
        => document.CreateElementNS(namespaceUri, qualifiedName);

    internal static string? NamespaceFor(Document document)
        => document.ContentType == DomContentType.Xhtml ? Namespaces.Html : null;
}
