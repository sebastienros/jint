using System.Xml;
using System.Xml.XPath;

namespace Jint.HtmlParser;

// XPath 1.0 data model: https://www.w3.org/TR/1999/REC-xpath-19991116/#data-model
internal static class NativeXPath
{
    internal static NativeXPathExpression Compile(string source, IXmlNamespaceResolver? resolver, CancellationToken cancellationToken)
        => Compile(source, resolver, null, cancellationToken);

    internal static NativeXPathExpression Compile(string source, IXmlNamespaceResolver? resolver,
        Action<XPathWorkStage, int>? checkpoint, CancellationToken cancellationToken)
        => NativeXPathExpression.Compile(source, resolver, checkpoint, cancellationToken);

    internal static NativeXPathResult Evaluate(Node context, NativeXPathExpression expression, CancellationToken cancellationToken)
        => Evaluate(context, expression, null, cancellationToken);

    internal static NativeXPathResult Evaluate(Attr context, NativeXPathExpression expression, CancellationToken cancellationToken)
        => Evaluate(context, expression, null, cancellationToken);

    internal static NativeXPathResult Evaluate(Node context, NativeXPathExpression expression,
        Action<XPathWorkStage, int>? checkpoint, CancellationToken cancellationToken)
        => Execute(context, expression, checkpoint, selectOnly: false, cancellationToken);

    internal static NativeXPathResult Evaluate(Attr context, NativeXPathExpression expression,
        Action<XPathWorkStage, int>? checkpoint, CancellationToken cancellationToken)
        => Execute(context, expression, checkpoint, selectOnly: false, cancellationToken);

    internal static IReadOnlyList<object> Select(Node context, NativeXPathExpression expression, CancellationToken cancellationToken)
        => Select(context, expression, null, cancellationToken);

    internal static IReadOnlyList<object> Select(Attr context, NativeXPathExpression expression, CancellationToken cancellationToken)
        => Select(context, expression, null, cancellationToken);

    internal static IReadOnlyList<object> Select(Node context, NativeXPathExpression expression,
        Action<XPathWorkStage, int>? checkpoint, CancellationToken cancellationToken)
        => Execute(context, expression, checkpoint, selectOnly: true, cancellationToken).Nodes;

    internal static IReadOnlyList<object> Select(Attr context, NativeXPathExpression expression,
        Action<XPathWorkStage, int>? checkpoint, CancellationToken cancellationToken)
        => Execute(context, expression, checkpoint, selectOnly: true, cancellationToken).Nodes;

    internal static NativeXPathResult Evaluate(Node context, string source, IXmlNamespaceResolver? resolver,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Evaluate(context, Compile(source, resolver, cancellationToken), cancellationToken);
    }

    internal static NativeXPathResult Evaluate(Attr context, string source, IXmlNamespaceResolver? resolver,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Evaluate(context, Compile(source, resolver, cancellationToken), cancellationToken);
    }

    internal static IReadOnlyList<object> Select(Node context, string source, IXmlNamespaceResolver? resolver,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Select(context, Compile(source, resolver, cancellationToken), cancellationToken);
    }

    internal static IReadOnlyList<object> Select(Attr context, string source, IXmlNamespaceResolver? resolver,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Select(context, Compile(source, resolver, cancellationToken), cancellationToken);
    }

    private static NativeXPathResult Execute(object context, NativeXPathExpression expression,
        Action<XPathWorkStage, int>? checkpoint, bool selectOnly, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(expression);
        token.ThrowIfCancellationRequested();
        var navigator = context switch
        {
            Node node => (NativeXPathNavigator) CreateNavigator(node, checkpoint, token),
            Attr attribute => (NativeXPathNavigator) CreateNavigator(attribute, checkpoint, token),
            _ => throw new ArgumentException("The native context has no XPath position.", nameof(context))
        };
        navigator.CheckRead();
        var raw = navigator.EvaluatePrepared(expression);
        navigator.CheckRead();
        if (raw is XPathNodeIterator iterator)
        {
            var values = new List<object>();
            var firstValue = "";
            while (true)
            {
                navigator.CheckRead();
                var hasNext = iterator.MoveNext();
                navigator.CheckRead();
                if (!hasNext) break;
                var position = iterator.Current ?? throw new XPathException("The XPath iterator has no current node.");
                navigator.ResultWork();
                var underlying = position.UnderlyingObject;
                if (underlying is not (Node or Attr or XPathNamespaceBinding))
                {
                    throw new XPathException("The XPath result has an unsupported native position.");
                }

                navigator.CheckRead();
                values.Add(underlying);
                navigator.CheckRead();
                if (!selectOnly && values.Count == 1)
                {
                    firstValue = position.Value;
                    navigator.ResultWork(firstValue.Length);
                    navigator.CheckRead();
                }
            }

            navigator.CheckRead();
            var array = values.ToArray();
            navigator.ResultWork(array.Length);
            navigator.CheckRead();
            var answer = NativeXPathResult.NodeSet(array, firstValue);
            navigator.CheckRead();
            return answer;
        }

        if (selectOnly) throw new XPathException("The XPath expression does not return a node-set.");
        NativeXPathResult scalar = raw switch
        {
            double number => NativeXPathResult.Number(number),
            string text => NativeXPathResult.String(text),
            bool boolean => NativeXPathResult.Boolean(boolean),
            _ => throw new XPathException("The XPath expression returned an unsupported result type.")
        };
        if (raw is string resultText) navigator.ResultWork(resultText.Length);
        navigator.CheckRead();
        return scalar;
    }

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

        var session = context.OwnerElement is { } owner
            ? new XPathReadSession(owner, checkpoint, cancellationToken)
            : new XPathReadSession(context, checkpoint, cancellationToken);
        return new NativeXPathNavigator(session, context);
    }
}
