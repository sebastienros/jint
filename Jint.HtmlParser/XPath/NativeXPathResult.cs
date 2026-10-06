using System.Collections.ObjectModel;
using System.Xml.XPath;

namespace Jint.HtmlParser;

/// <summary>A materialized XPath result retaining native identities and captured scalar values.</summary>
/// <remarks>Accessing a getter for another result kind throws <see cref="InvalidOperationException"/>; no conversion is performed.</remarks>
public sealed class NativeXPathResult
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

    /// <summary>The actual result kind: Number, String, Boolean or NodeSet.</summary>
    public XPathResultType ResultType { get; }
    /// <summary>The captured numeric result.</summary>
    public double NumberValue => ResultType == XPathResultType.Number ? _number : throw WrongKind();
    /// <summary>The captured string result.</summary>
    public string StringValue => ResultType == XPathResultType.String ? _string! : throw WrongKind();
    /// <summary>The captured Boolean result.</summary>
    public bool BooleanValue => ResultType == XPathResultType.Boolean ? _boolean : throw WrongKind();
    /// <summary>An immutable ordered snapshot containing only <see cref="Node"/>, <see cref="Attr"/> and <see cref="XPathNamespaceBinding"/> identities.</summary>
    public IReadOnlyList<object> Nodes => ResultType == XPathResultType.NodeSet ? _nodes! : throw WrongKind();
    /// <summary>The captured XPath string-value of the first selected position, or empty for an empty node-set.</summary>
    public string FirstNodeStringValue => ResultType == XPathResultType.NodeSet ? _firstNodeStringValue! : throw WrongKind();

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
