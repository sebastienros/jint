using System.Xml;
using System.Xml.XPath;

namespace Jint.HtmlParser;

/// <summary>Compiles and evaluates XPath 1.0 expressions over native nodes, attributes and namespace positions.</summary>
/// <remarks>
/// Namespaces are preserved: unprefixed element tests select elements in no namespace. Results are
/// materialized snapshots containing native identities, not copies of the tree. Concurrent mutation
/// during evaluation is unsupported; detected mutation throws <see cref="InvalidOperationException"/>.
/// Parsing and evaluation never fetch external resources.
/// </remarks>
public static class NativeXPath
{
    /// <summary>Compiles an expression for reuse across trees, binding the supplied namespace resolver.</summary>
    public static NativeXPathExpression Compile(string source, IXmlNamespaceResolver? resolver = null,
        CancellationToken cancellationToken = default)
        => Compile(source, resolver, null, cancellationToken);

    internal static NativeXPathExpression Compile(string source, IXmlNamespaceResolver? resolver,
        Action<XPathWorkStage, int>? checkpoint, CancellationToken cancellationToken)
        => NativeXPathExpression.Compile(source, resolver, checkpoint, cancellationToken);

    /// <summary>Evaluates a prepared expression at a node and captures its typed result.</summary>
    public static NativeXPathResult Evaluate(Node context, NativeXPathExpression expression, CancellationToken cancellationToken = default)
        => Evaluate(context, expression, null, cancellationToken);

    /// <summary>Evaluates a prepared expression at an attached or detached non-XMLNS attribute.</summary>
    public static NativeXPathResult Evaluate(Attr context, NativeXPathExpression expression, CancellationToken cancellationToken = default)
        => Evaluate(context, expression, null, cancellationToken);

    /// <summary>Evaluates at a captured namespace position, rejecting a binding whose URI is no longer in scope.</summary>
    public static NativeXPathResult Evaluate(XPathNamespaceBinding context, NativeXPathExpression expression,
        CancellationToken cancellationToken = default)
        => Execute(context, expression, null, selectOnly: false, cancellationToken);

    internal static NativeXPathResult Evaluate(Node context, NativeXPathExpression expression,
        Action<XPathWorkStage, int>? checkpoint, CancellationToken cancellationToken)
        => Execute(context, expression, checkpoint, selectOnly: false, cancellationToken);

    internal static NativeXPathResult Evaluate(Attr context, NativeXPathExpression expression,
        Action<XPathWorkStage, int>? checkpoint, CancellationToken cancellationToken)
        => Execute(context, expression, checkpoint, selectOnly: false, cancellationToken);

    /// <summary>Selects an immutable ordered snapshot of native identities; a scalar expression throws <see cref="XPathException"/>.</summary>
    public static IReadOnlyList<object> Select(Node context, NativeXPathExpression expression, CancellationToken cancellationToken = default)
        => Select(context, expression, null, cancellationToken);

    /// <summary>Selects native identities relative to an attached or detached non-XMLNS attribute.</summary>
    public static IReadOnlyList<object> Select(Attr context, NativeXPathExpression expression, CancellationToken cancellationToken = default)
        => Select(context, expression, null, cancellationToken);

    /// <summary>Selects native identities relative to a still-current captured namespace position.</summary>
    public static IReadOnlyList<object> Select(XPathNamespaceBinding context, NativeXPathExpression expression,
        CancellationToken cancellationToken = default)
        => Execute(context, expression, null, selectOnly: true, cancellationToken).Nodes;

    internal static IReadOnlyList<object> Select(Node context, NativeXPathExpression expression,
        Action<XPathWorkStage, int>? checkpoint, CancellationToken cancellationToken)
        => Execute(context, expression, checkpoint, selectOnly: true, cancellationToken).Nodes;

    internal static IReadOnlyList<object> Select(Attr context, NativeXPathExpression expression,
        Action<XPathWorkStage, int>? checkpoint, CancellationToken cancellationToken)
        => Execute(context, expression, checkpoint, selectOnly: true, cancellationToken).Nodes;

    /// <summary>Compiles and evaluates an expression at a node.</summary>
    public static NativeXPathResult Evaluate(Node context, string source, IXmlNamespaceResolver? resolver = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Evaluate(context, Compile(source, resolver, cancellationToken), cancellationToken);
    }

    /// <summary>Compiles and evaluates an expression at a non-XMLNS attribute.</summary>
    public static NativeXPathResult Evaluate(Attr context, string source, IXmlNamespaceResolver? resolver = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Evaluate(context, Compile(source, resolver, cancellationToken), cancellationToken);
    }

    /// <summary>Compiles and evaluates an expression at a still-current captured namespace position.</summary>
    public static NativeXPathResult Evaluate(XPathNamespaceBinding context, string source, IXmlNamespaceResolver? resolver = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Evaluate(context, Compile(source, resolver, cancellationToken), cancellationToken);
    }

    /// <summary>Compiles a node-set expression and selects its native identities at a node.</summary>
    public static IReadOnlyList<object> Select(Node context, string source, IXmlNamespaceResolver? resolver = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Select(context, Compile(source, resolver, cancellationToken), cancellationToken);
    }

    /// <summary>Compiles a node-set expression and selects its native identities at a non-XMLNS attribute.</summary>
    public static IReadOnlyList<object> Select(Attr context, string source, IXmlNamespaceResolver? resolver = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Select(context, Compile(source, resolver, cancellationToken), cancellationToken);
    }

    /// <summary>Compiles a node-set expression and selects at a still-current captured namespace position.</summary>
    public static IReadOnlyList<object> Select(XPathNamespaceBinding context, string source, IXmlNamespaceResolver? resolver = null,
        CancellationToken cancellationToken = default)
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
            XPathNamespaceBinding binding => CreateNavigator(binding, checkpoint, token),
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
            navigator.PublishResult();
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
        navigator.PublishResult();
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

    internal static NativeXPathNavigator CreateNavigator(XPathNamespaceBinding context,
        Action<XPathWorkStage, int>? checkpoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var session = new XPathReadSession(context.OwnerElement, checkpoint, cancellationToken);
        var binding = session.BindingFor(context.OwnerElement, context.Prefix, context.NamespaceUri)
            ?? throw new InvalidOperationException("The captured XPath namespace binding is no longer in scope.");
        session.Check();
        return new NativeXPathNavigator(session, binding);
    }
}
