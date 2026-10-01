namespace Jint.HtmlParser;

/// <summary>An immutable captured namespace-axis position, distinct from an XMLNS attribute.</summary>
public sealed class XPathNamespaceBinding
{
    internal XPathNamespaceBinding(Element ownerElement, string prefix, string namespaceUri)
    {
        OwnerElement = ownerElement;
        Prefix = prefix;
        NamespaceUri = namespaceUri;
    }

    /// <summary>The element whose namespace axis supplied this position.</summary>
    public Element OwnerElement { get; }
    /// <summary>The bound prefix, or the empty string for a default namespace.</summary>
    public string Prefix { get; }
    /// <summary>The namespace URI captured at evaluation.</summary>
    public string NamespaceUri { get; }
}
