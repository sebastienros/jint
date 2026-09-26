namespace Jint.HtmlParser;

// A namespace-axis position is not an Attr and has no native DOM node.
internal sealed class XPathNamespaceBinding(Element ownerElement, string prefix, string namespaceUri)
{
    internal Element OwnerElement { get; } = ownerElement;
    internal string Prefix { get; } = prefix;
    internal string NamespaceUri { get; } = namespaceUri;
}
