using System.Xml.XPath;

namespace Jint.HtmlParser;

// XPath 1.0 data model: https://www.w3.org/TR/1999/REC-xpath-19991116/#data-model
internal static class NativeXPath
{
    internal static XPathNavigator CreateNavigator(Node context, CancellationToken cancellationToken)
        => CreateNavigator(context, null, cancellationToken);

    internal static XPathNavigator CreateNavigator(Attr context, CancellationToken cancellationToken)
        => CreateNavigator(context, null, cancellationToken);

    // A per-invocation checkpoint is used by adversarial tests; it is never retained on the DOM.
    internal static XPathNavigator CreateNavigator(Node context, Action<XPathWorkStage, int>? checkpoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context is DocumentType || context.NodeType is not (NodeType.Document or NodeType.DocumentFragment or
            NodeType.Element or NodeType.Text or NodeType.CDataSection or NodeType.Comment or NodeType.ProcessingInstruction))
        {
            throw new ArgumentException("The native node has no XPath position.", nameof(context));
        }

        if (context is Text { Data.Length: 0 } or CDataSection { Data.Length: 0 })
        {
            throw new ArgumentException("An empty text node has no XPath position.", nameof(context));
        }

        var session = new XPathReadSession(context, checkpoint, cancellationToken);
        return new NativeXPathNavigator(session, session.Representative(context));
    }

    internal static XPathNavigator CreateNavigator(Attr context, Action<XPathWorkStage, int>? checkpoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.NamespaceUri == Namespaces.Xmlns)
        {
            throw new ArgumentException("An XMLNS declaration has no XPath attribute position.", nameof(context));
        }

        if (context.OwnerElement is not { } owner)
        {
            throw new NotSupportedException("Detached attribute XPath contexts require a mutation contract.");
        }

        var session = new XPathReadSession(owner, checkpoint, cancellationToken);
        return new NativeXPathNavigator(session, context);
    }
}
