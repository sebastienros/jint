using System.Collections.ObjectModel;
using System.Xml.XPath;

namespace Jint.HtmlParser;

// A published result retains native identities and copied scalar values, never a live cursor.
internal sealed class NativeXPathResult
{
    private readonly double _number;
    private readonly string? _string;
    private readonly bool _boolean;
    private readonly IReadOnlyList<object>? _nodes;
    private readonly string? _firstNodeStringValue;

    private NativeXPathResult(XPathResultType type, double number = 0, string? text = null,
        bool boolean = false, IReadOnlyList<object>? nodes = null, string? firstNodeStringValue = null)
    {
        ResultType = type;
        _number = number;
        _string = text;
        _boolean = boolean;
        _nodes = nodes;
        _firstNodeStringValue = firstNodeStringValue;
    }

    internal XPathResultType ResultType { get; }
    internal double NumberValue => ResultType == XPathResultType.Number ? _number : throw WrongKind();
    internal string StringValue => ResultType == XPathResultType.String ? _string! : throw WrongKind();
    internal bool BooleanValue => ResultType == XPathResultType.Boolean ? _boolean : throw WrongKind();
    internal IReadOnlyList<object> Nodes => ResultType == XPathResultType.NodeSet ? _nodes! : throw WrongKind();
    internal string FirstNodeStringValue => ResultType == XPathResultType.NodeSet ? _firstNodeStringValue! : throw WrongKind();

    internal static NativeXPathResult Number(double value) => new(XPathResultType.Number, number: value);
    internal static NativeXPathResult String(string value) => new(XPathResultType.String, text: value);
    internal static NativeXPathResult Boolean(bool value) => new(XPathResultType.Boolean, boolean: value);
    internal static NativeXPathResult NodeSet(object[] values, string firstNodeStringValue)
    {
        ReadOnlyCollection<object> readOnly = Array.AsReadOnly(values);
        return new NativeXPathResult(XPathResultType.NodeSet, nodes: readOnly,
            firstNodeStringValue: firstNodeStringValue);
    }

    private static InvalidOperationException WrongKind() => new("The XPath result has a different kind.");
}
