using System.Globalization;
using System.Xml;
using System.Xml.XPath;
using AngleSharp.Dom;
using AngleSharp.XPath;
using Jint.Browser.Runtime;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.WebApi.DomException;
using CompiledExpression = System.Xml.XPath.XPathExpression;

namespace Jint.Browser.Dom.Views;

/// <summary>
/// DOM §7's XPath: <c>XPathEvaluator</c>, <c>XPathExpression</c>, <c>XPathResult</c> and the three members
/// <c>Document</c> gains from <c>XPathEvaluatorBase</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>The engine is <c>System.Xml.XPath</c>, over <c>AngleSharp.XPath</c>'s navigator.</b> That package —
/// the AngleSharp project's own, MIT, referenced for this and nothing else — is an
/// <c>XPathNavigator</c> implementation over an AngleSharp tree, which is exactly the seam the BCL's XPath 1.0
/// evaluator takes. Writing an XPath engine here instead would be the thing this package is not for.
/// <b>It carries no <c>[DomName]</c> anywhere</b>, and neither does AngleSharp: there is no
/// <c>IXPathEvaluator</c>, no <c>evaluate</c> on <c>IDocument</c>, nothing for the generator to project. So
/// these three interfaces are hand-written host interfaces beside <c>DOMParser</c> and <c>Selection</c>, and
/// the three <c>Document</c> members are <c>overrides.json</c> <c>additions</c>.
/// </para>
/// <para>
/// <b>Namespaces are ignored, and that is what makes <c>//div</c> match.</b> An HTML element is in the XHTML
/// namespace, so an XPath 1.0 name test with no prefix — which is what every page writes — would match
/// nothing at all if the navigator reported it. <c>AngleSharp.XPath</c>'s own default is the same choice, and
/// a browser reaches it from the other side (HTML documents get a name test that matches the HTML namespace).
/// The consequence is stated rather than hidden: a <i>prefixed</i> name test (<c>svg:circle</c>) compiles,
/// because a resolver the page supplied is consulted for the prefix, and then matches nothing, because the
/// navigator answers no namespace for any node.
/// </para>
/// <para>
/// <b>A node-set is materialized at evaluation.</b> DOM's iterator is live and becomes invalid when the
/// document is mutated under it; this takes the snapshot immediately, so <c>invalidIteratorState</c> is
/// always <see langword="false"/> and an iterator survives a mutation instead of raising
/// <c>InvalidStateError</c>. That is the one divergence in the result object, and it is the safe direction:
/// the failure mode it removes is a page throwing, and the one it introduces is a page seeing a node that has
/// since moved.
/// </para>
/// </remarks>
internal static class XPathEvaluation
{
    /// <summary>https://dom.spec.whatwg.org/#dom-xpathresult-any_type and the nine after it.</summary>
    internal const int AnyType = 0;

    internal const int NumberType = 1;
    internal const int StringType = 2;
    internal const int BooleanType = 3;
    internal const int UnorderedNodeIteratorType = 4;
    internal const int OrderedNodeIteratorType = 5;
    internal const int UnorderedNodeSnapshotType = 6;
    internal const int OrderedNodeSnapshotType = 7;
    internal const int AnyUnorderedNodeType = 8;
    internal const int FirstOrderedNodeType = 9;

    /// <summary>
    /// The ten result-type constants, by the names WebIDL gives them.
    /// </summary>
    /// <remarks>
    /// <a href="https://webidl.spec.whatwg.org/#es-constants">WebIDL</a> puts a constant on <b>both</b> the
    /// interface object and the interface prototype object, so <c>XPathResult.FIRST_ORDERED_NODE_TYPE</c> —
    /// the spelling every page actually writes — and <c>result.FIRST_ORDERED_NODE_TYPE</c> both answer. The
    /// prototype's copy comes from the shape; the list is here so that both copies are one table.
    /// </remarks>
    internal static (string Name, int Value)[] ResultConstants { get; } =
    [
        ("ANY_TYPE", AnyType),
        ("NUMBER_TYPE", NumberType),
        ("STRING_TYPE", StringType),
        ("BOOLEAN_TYPE", BooleanType),
        ("UNORDERED_NODE_ITERATOR_TYPE", UnorderedNodeIteratorType),
        ("ORDERED_NODE_ITERATOR_TYPE", OrderedNodeIteratorType),
        ("UNORDERED_NODE_SNAPSHOT_TYPE", UnorderedNodeSnapshotType),
        ("ORDERED_NODE_SNAPSHOT_TYPE", OrderedNodeSnapshotType),
        ("ANY_UNORDERED_NODE_TYPE", AnyUnorderedNodeType),
        ("FIRST_ORDERED_NODE_TYPE", FirstOrderedNodeType),
    ];

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-xpathevaluatorbase-createexpression — compiles, or raises the
    /// <c>SyntaxError</c> the standard names.
    /// </summary>
    internal static JsValue CreateExpression(PageRuntime runtime, JsValue[] arguments, string member)
    {
        var source = DomConvert.RequiredText(arguments, 0, member);
        var resolver = NamespaceResolver.From(runtime, arguments.At(1), member);

        return new JsXPathExpression(runtime, runtime.Views.XPathExpressionPrototype, Compile(runtime, source, resolver, member));
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-xpathevaluatorbase-creatensresolver — legacy, and the whole of it is
    /// "return the node".
    /// </summary>
    /// <remarks>
    /// DOM keeps the member only because pages call it, and its steps are one line: the node itself is the
    /// resolver, because a <c>Node</c> already has <c>lookupNamespaceURI</c>.
    /// </remarks>
    internal static JsValue CreateNSResolver(JsValue[] arguments, string member)
    {
        var node = DomBindings.Argument<INode>(arguments, 0, member);
        _ = node;
        return arguments.At(0);
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-xpathevaluatorbase-evaluate — compile and evaluate in one call.
    /// </summary>
    internal static JsValue Evaluate(PageRuntime runtime, JsValue[] arguments, string member)
    {
        var source = DomConvert.RequiredText(arguments, 0, member);
        var context = DomBindings.Argument<INode>(arguments, 1, member);
        var resolver = NamespaceResolver.From(runtime, arguments.At(2), member);
        var type = DomConvert.OptionalInt32(arguments, 3, AnyType);

        return Run(runtime, Compile(runtime, source, resolver, member), context, type, member);
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-xpathexpression-evaluate — the expression against one context node.
    /// </summary>
    internal static JsValue Run(PageRuntime runtime, CompiledExpression expression, INode context, int type, string member)
    {
        var document = context as IDocument ?? context.Owner;

        if (document is null)
        {
            Throw.TypeError(runtime.Engine._mainRealm, "Failed to execute '" + member + "': the context node has no document.");
            return JsValue.Undefined;
        }

        // The navigator is positioned at the context node, which is what makes a relative expression — htmx's
        // `.//*[…]` — mean what the page meant by it.
        var navigator = new HtmlDocumentNavigator(document, context, ignoreNamespaces: true);
        object evaluated;

        try
        {
            evaluated = navigator.Evaluate(expression.Clone());
        }
        catch (XPathException exception)
        {
            Throw.JavaScriptException(
                runtime.Engine,
                runtime.Engine._mainRealm.Intrinsics.DomException.CreateException(
                    DomExceptionNames.Syntax,
                    "Failed to execute '" + member + "': " + exception.Message),
                runtime.Engine.GetLastSyntaxElement()?.Location ?? default);
            return JsValue.Undefined;
        }

        return new JsXPathResult(runtime, runtime.Views.XPathResultPrototype, Coerce(runtime, evaluated, type, member));
    }

    /// <summary>Compiles one expression, turning a parse failure into the standard's <c>SyntaxError</c>.</summary>
    private static CompiledExpression Compile(PageRuntime runtime, string source, IXmlNamespaceResolver? resolver, string member)
    {
        try
        {
            var expression = CompiledExpression.Compile(source);

            if (resolver is not null)
            {
                expression.SetContext(resolver);
            }

            return expression;
        }
        catch (Exception exception) when (exception is XPathException or ArgumentException)
        {
            Throw.JavaScriptException(
                runtime.Engine,
                runtime.Engine._mainRealm.Intrinsics.DomException.CreateException(
                    DomExceptionNames.Syntax,
                    "Failed to execute '" + member + "': The string '" + source + "' is not a valid XPath expression."),
                runtime.Engine.GetLastSyntaxElement()?.Location ?? default);
            return null!;
        }
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#concept-evaluate — what the requested result type makes of what XPath
    /// answered.
    /// </summary>
    private static XPathAnswer Coerce(PageRuntime runtime, object evaluated, int type, string member)
    {
        if (evaluated is XPathNodeIterator nodes)
        {
            return NodeSet(runtime, nodes, type, member);
        }

        // A non-node-set can still be asked for as a number, a string or a boolean, and XPath 1.0's own
        // conversions are what the standard names.
        return type switch
        {
            AnyType => evaluated switch
            {
                double number => XPathAnswer.Number(NumberType, number),
                bool flag => XPathAnswer.Boolean(BooleanType, flag),
                _ => XPathAnswer.String(StringType, Text(evaluated)),
            },
            NumberType => XPathAnswer.Number(NumberType, Number(evaluated)),
            StringType => XPathAnswer.String(StringType, Text(evaluated)),
            BooleanType => XPathAnswer.Boolean(BooleanType, Boolean(evaluated)),
            _ => WrongType(runtime, member),
        };
    }

    private static XPathAnswer NodeSet(PageRuntime runtime, XPathNodeIterator nodes, int type, string member)
    {
        // The whole set, now: DOM's live iterator needs a mutation signal this has none of, and the class
        // summary states the divergence that follows.
        var found = new List<INode>();

        while (nodes.MoveNext())
        {
            if (nodes.Current is HtmlDocumentNavigator { CurrentNode: { } node })
            {
                found.Add(node);
            }
        }

        return type switch
        {
            AnyType => XPathAnswer.NodeSet(UnorderedNodeIteratorType, found),
            UnorderedNodeIteratorType or OrderedNodeIteratorType
                or UnorderedNodeSnapshotType or OrderedNodeSnapshotType
                or AnyUnorderedNodeType or FirstOrderedNodeType => XPathAnswer.NodeSet(type, found),

            // XPath 1.0's node-set conversions: a set is true when it is not empty, and its string value is
            // its first node's.
            NumberType => XPathAnswer.Number(NumberType, StringToNumber(FirstText(found))),
            StringType => XPathAnswer.String(StringType, FirstText(found)),
            BooleanType => XPathAnswer.Boolean(BooleanType, found.Count > 0),
            _ => WrongType(runtime, member),
        };
    }

    private static XPathAnswer WrongType(PageRuntime runtime, string member)
    {
        Throw.TypeError(
            runtime.Engine._mainRealm,
            "Failed to execute '" + member + "': The result is not a node set, and therefore cannot be converted to the desired type.");
        return default;
    }

    private static string FirstText(List<INode> nodes) => nodes.Count == 0 ? "" : nodes[0].TextContent ?? "";

    private static string Text(object value) => value switch
    {
        string text => text,
        double number => NumberToString(number),
        bool flag => flag ? "true" : "false",
        _ => value?.ToString() ?? "",
    };

    private static double Number(object value) => value switch
    {
        double number => number,
        bool flag => flag ? 1 : 0,
        string text => StringToNumber(text),
        _ => double.NaN,
    };

    private static bool Boolean(object value) => value switch
    {
        bool flag => flag,
        double number => number != 0 && !double.IsNaN(number),
        string text => text.Length > 0,
        _ => false,
    };

    /// <summary>XPath 1.0 §4.4's <c>number()</c>: a string that is not a number is <c>NaN</c>.</summary>
    private static double StringToNumber(string text)
        => double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : double.NaN;

    /// <summary>XPath 1.0 §4.2's <c>string()</c> of a number, which is not .NET's round-trip form.</summary>
    private static string NumberToString(double number)
    {
        if (double.IsNaN(number))
        {
            return "NaN";
        }

        if (double.IsPositiveInfinity(number))
        {
            return "Infinity";
        }

        if (double.IsNegativeInfinity(number))
        {
            return "-Infinity";
        }

        return number.ToString("R", CultureInfo.InvariantCulture);
    }
}

/// <summary>One evaluation's answer, in the shape <c>XPathResult</c> publishes it.</summary>
/// <param name="ResultType">The <c>XPathResult</c> constant this is.</param>
/// <param name="NumberValue">The number, for <c>NUMBER_TYPE</c>.</param>
/// <param name="StringValue">The string, for <c>STRING_TYPE</c>.</param>
/// <param name="BooleanValue">The boolean, for <c>BOOLEAN_TYPE</c>.</param>
/// <param name="Nodes">The node set, for the six node types.</param>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct XPathAnswer(
    int ResultType,
    double NumberValue,
    string StringValue,
    bool BooleanValue,
    List<INode>? Nodes)
{
    internal static XPathAnswer Number(int type, double value) => new(type, value, "", false, null);

    internal static XPathAnswer String(int type, string value) => new(type, double.NaN, value, false, null);

    internal static XPathAnswer Boolean(int type, bool value) => new(type, double.NaN, "", value, null);

    internal static XPathAnswer NodeSet(int type, List<INode> nodes) => new(type, double.NaN, "", false, nodes);
}

/// <summary>
/// https://dom.spec.whatwg.org/#interface-xpathevaluator — the constructible half of DOM's XPath.
/// </summary>
internal sealed class JsXPathEvaluator : ObjectInstance
{
    private readonly PageRuntime _runtime;

    internal JsXPathEvaluator(PageRuntime runtime, ObjectInstance prototype) : base(runtime.Engine)
    {
        _runtime = runtime;
        Prototype = prototype;
    }

    /// <inheritdoc />
    public override string ToString() => "[object XPathEvaluator]";

    /// <summary>The receiver check every member starts with.</summary>
    internal static JsXPathEvaluator Brand(JsValue thisObject, string member)
        => thisObject as JsXPathEvaluator ?? Illegal<JsXPathEvaluator>(thisObject, "XPathEvaluator", member);

    internal JsValue CreateExpression(JsValue[] arguments)
        => XPathEvaluation.CreateExpression(_runtime, arguments, "XPathEvaluator.createExpression");

    internal JsValue Evaluate(JsValue[] arguments)
        => XPathEvaluation.Evaluate(_runtime, arguments, "XPathEvaluator.evaluate");

    /// <summary>The <c>Illegal invocation</c> a host member answers a receiver it was not called on.</summary>
    internal static T Illegal<T>(JsValue thisObject, string interfaceName, string member) where T : class
    {
        var message = "Failed to execute '" + member + "' on '" + interfaceName + "': Illegal invocation";

        if (thisObject is ObjectInstance instance)
        {
            Throw.TypeError(instance.Engine.Realm, message);
        }

        Throw.TypeErrorNoEngine(message);
        return null!;
    }
}

/// <summary>https://dom.spec.whatwg.org/#interface-xpathexpression — one compiled expression.</summary>
internal sealed class JsXPathExpression : ObjectInstance
{
    private readonly PageRuntime _runtime;
    private readonly CompiledExpression _expression;

    internal JsXPathExpression(PageRuntime runtime, ObjectInstance prototype, CompiledExpression expression) : base(runtime.Engine)
    {
        _runtime = runtime;
        _expression = expression;
        Prototype = prototype;
    }

    /// <inheritdoc />
    public override string ToString() => "[object XPathExpression]";

    /// <summary>The receiver check the one member starts with.</summary>
    internal static JsXPathExpression Brand(JsValue thisObject, string member)
        => thisObject as JsXPathExpression ?? JsXPathEvaluator.Illegal<JsXPathExpression>(thisObject, "XPathExpression", member);

    /// <summary>https://dom.spec.whatwg.org/#dom-xpathexpression-evaluate.</summary>
    internal JsValue Evaluate(JsValue[] arguments)
    {
        var context = DomBindings.Argument<INode>(arguments, 0, "XPathExpression.evaluate");
        var type = DomConvert.OptionalInt32(arguments, 1, XPathEvaluation.AnyType);

        return XPathEvaluation.Run(_runtime, _expression, context, type, "XPathExpression.evaluate");
    }
}

/// <summary>https://dom.spec.whatwg.org/#interface-xpathresult — what one evaluation answered.</summary>
internal sealed class JsXPathResult : ObjectInstance
{
    private readonly PageRuntime _runtime;
    private readonly XPathAnswer _answer;

    private int _next;

    internal JsXPathResult(PageRuntime runtime, ObjectInstance prototype, XPathAnswer answer) : base(runtime.Engine)
    {
        _runtime = runtime;
        _answer = answer;
        Prototype = prototype;
    }

    /// <inheritdoc />
    public override string ToString() => "[object XPathResult]";

    /// <summary>The receiver check every member starts with.</summary>
    internal static JsXPathResult Brand(JsValue thisObject, string member)
        => thisObject as JsXPathResult ?? JsXPathEvaluator.Illegal<JsXPathResult>(thisObject, "XPathResult", member);

    /// <summary>https://dom.spec.whatwg.org/#dom-xpathresult-resulttype.</summary>
    internal JsValue ResultType => JsNumber.Create(_answer.ResultType);

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-xpathresult-invaliditeratorstate — always <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// The node set was taken whole at evaluation, so nothing a page does to the document afterwards can put
    /// this iterator into the invalid state DOM defines. <see cref="XPathEvaluation"/> argues the choice.
    /// </remarks>
    internal static JsValue InvalidIteratorState => JsBoolean.False;

    /// <summary>https://dom.spec.whatwg.org/#dom-xpathresult-numbervalue.</summary>
    internal JsValue NumberValue
        => JsNumber.Create(Require(XPathEvaluation.NumberType, "numberValue") ? _answer.NumberValue : double.NaN);

    /// <summary>https://dom.spec.whatwg.org/#dom-xpathresult-stringvalue.</summary>
    internal JsValue StringValue
        => JsString.Create(Require(XPathEvaluation.StringType, "stringValue") ? _answer.StringValue : "");

    /// <summary>https://dom.spec.whatwg.org/#dom-xpathresult-booleanvalue.</summary>
    internal JsValue BooleanValue
        => JsBoolean.Create(Require(XPathEvaluation.BooleanType, "booleanValue") && _answer.BooleanValue);

    /// <summary>https://dom.spec.whatwg.org/#dom-xpathresult-singlenodevalue.</summary>
    internal JsValue SingleNodeValue
    {
        get
        {
            if (_answer.ResultType is not (XPathEvaluation.AnyUnorderedNodeType or XPathEvaluation.FirstOrderedNodeType))
            {
                WrongType("singleNodeValue");
            }

            var nodes = _answer.Nodes;
            return nodes is { Count: > 0 } ? _runtime.Dom.WrapNode(nodes[0]) : JsValue.Null;
        }
    }

    /// <summary>https://dom.spec.whatwg.org/#dom-xpathresult-snapshotlength.</summary>
    internal JsValue SnapshotLength
    {
        get
        {
            if (!IsSnapshot)
            {
                WrongType("snapshotLength");
            }

            return JsNumber.Create(_answer.Nodes?.Count ?? 0);
        }
    }

    /// <summary>https://dom.spec.whatwg.org/#dom-xpathresult-iteratenext.</summary>
    internal JsValue IterateNext()
    {
        if (_answer.ResultType is not (XPathEvaluation.UnorderedNodeIteratorType or XPathEvaluation.OrderedNodeIteratorType))
        {
            WrongType("iterateNext");
        }

        var nodes = _answer.Nodes;
        return nodes is not null && _next < nodes.Count ? _runtime.Dom.WrapNode(nodes[_next++]) : JsValue.Null;
    }

    /// <summary>https://dom.spec.whatwg.org/#dom-xpathresult-snapshotitem.</summary>
    internal JsValue SnapshotItem(JsValue[] arguments)
    {
        if (!IsSnapshot)
        {
            WrongType("snapshotItem");
        }

        var index = DomConvert.RequiredUInt32(arguments, 0, "XPathResult.snapshotItem");
        var nodes = _answer.Nodes;

        return nodes is not null && index < (uint) nodes.Count ? _runtime.Dom.WrapNode(nodes[(int) index]) : JsValue.Null;
    }

    private bool IsSnapshot
        => _answer.ResultType is XPathEvaluation.UnorderedNodeSnapshotType or XPathEvaluation.OrderedNodeSnapshotType;

    private bool Require(int type, string member)
    {
        if (_answer.ResultType != type)
        {
            WrongType(member);
        }

        return true;
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#interface-xpathresult — reading a value the result is not is a
    /// <c>TypeError</c>.
    /// </summary>
    private void WrongType(string member)
        => Throw.TypeError(
            _runtime.Engine._mainRealm,
            "Failed to read the '" + member + "' property from 'XPathResult': The result type is not "
            + _answer.ResultType.ToString(CultureInfo.InvariantCulture) + "'s.");
}

/// <summary>
/// A page's <c>XPathNSResolver</c>, seen by <c>System.Xml</c> as an <see cref="IXmlNamespaceResolver"/>.
/// </summary>
/// <remarks>
/// WebIDL's callback interface admits both spellings — a function, and an object with a
/// <c>lookupNamespaceURI</c> method — and a `Node` handed back by <c>createNSResolver</c> is the second. The
/// resolver is consulted while the expression is <i>compiled</i>, which is the only thing that stops a
/// prefixed name test from being a <c>SyntaxError</c>; matching is a separate question the class summary of
/// <see cref="XPathEvaluation"/> answers.
/// </remarks>
internal sealed class NamespaceResolver : IXmlNamespaceResolver
{
    private readonly Engine _engine;
    private readonly JsValue _resolver;

    private NamespaceResolver(Engine engine, JsValue resolver)
    {
        _engine = engine;
        _resolver = resolver;
    }

    /// <summary>The resolver a page passed, or <see langword="null"/> when it passed none.</summary>
    internal static IXmlNamespaceResolver? From(PageRuntime runtime, JsValue resolver, string member)
    {
        if (resolver.IsNullOrUndefined())
        {
            return null;
        }

        if (resolver is not ObjectInstance)
        {
            Throw.TypeError(
                runtime.Engine._mainRealm,
                "Failed to execute '" + member + "': parameter is not an XPathNSResolver.");
        }

        return new NamespaceResolver(runtime.Engine, resolver);
    }

    /// <inheritdoc />
    public IDictionary<string, string> GetNamespacesInScope(XmlNamespaceScope scope)
        => new Dictionary<string, string>(StringComparer.Ordinal);

    /// <inheritdoc />
    public string? LookupNamespace(string prefix)
    {
        var argument = string.IsNullOrEmpty(prefix) ? JsValue.Null : JsString.Create(prefix);

        var answer = _resolver is ICallable callable
            ? callable.Call(JsValue.Undefined, argument)
            : _resolver.Get("lookupNamespaceURI") is ICallable method
                ? method.Call(_resolver, argument)
                : JsValue.Null;

        return answer.IsNullOrUndefined() ? null : TypeConverter.ToString(answer);
    }

    /// <inheritdoc />
    public string? LookupPrefix(string namespaceName) => null;
}
