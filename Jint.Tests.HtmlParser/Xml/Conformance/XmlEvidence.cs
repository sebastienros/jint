#nullable enable
using System.Text;
using System.Text.Json;
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Xml.Conformance;

internal static class XmlEvidence
{
    private static readonly IComparer<string> ScalarOrder = Comparer<string>.Create(CompareUnicodeScalars);
    // W3C XML Test Suite, xmlconf/sun/cxml.html, Second XML Canonical Form.
    // The public DOM does not expose DTD notation declarations. Reject that
    // comparison explicitly rather than treating a binary accept as an output pass.
    internal static string SecondCanonicalForm(Document document, ReadOnlySpan<byte> expected)
    {
        if (expected.StartsWith("<!DOCTYPE "u8))
            throw new XmlOutputObservationGapException("DTD notation declarations are not exposed by the native DOM");
        var result = new StringBuilder();
        var stack = new Stack<(Node Node, bool Closing)>();
        for (var child = document.LastChild; child is not null; child = child.PreviousSibling)
            stack.Push((child, false));
        while (stack.Count > 0)
        {
            var (node, closing) = stack.Pop();
            if (closing)
            {
                var element = (Element)node;
                result.Append("</").Append(element.TagName).Append('>');
                continue;
            }
            switch (node)
            {
                case Element element:
                    result.Append('<').Append(element.TagName);
                    foreach (var attribute in element.Attributes.OrderBy(item => item.Name, ScalarOrder))
                    {
                        result.Append(' ').Append(attribute.Name).Append("=\"");
                        AppendData(result, attribute.Value);
                        result.Append('"');
                    }
                    result.Append('>');
                    stack.Push((node, true));
                    for (var child = node.LastChild; child is not null; child = child.PreviousSibling)
                        stack.Push((child, false));
                    break;
                case Text text:
                    AppendData(result, text.Data);
                    break;
                case CDataSection cdata:
                    AppendData(result, cdata.Data);
                    break;
                case ProcessingInstruction instruction:
                    result.Append("<?").Append(instruction.Target).Append(' ').Append(instruction.Data).Append("?>");
                    break;
                case Comment:
                case DocumentType:
                    break;
                default:
                    throw new InvalidDataException($"Unexpected node kind in XML output: {node.NodeType}");
            }
        }
        return result.ToString();
    }

    private static void AppendData(StringBuilder output, string data)
    {
        foreach (var character in data)
        {
            output.Append(character switch
            {
                '&' => "&amp;",
                '<' => "&lt;",
                '>' => "&gt;",
                '"' => "&quot;",
                '\t' => "&#9;",
                '\n' => "&#10;",
                '\r' => "&#13;",
                _ => character.ToString()
            });
        }
    }

    private static int CompareUnicodeScalars(string? left, string? right)
    {
        if (ReferenceEquals(left, right)) return 0;
        if (left is null) return -1;
        if (right is null) return 1;
        var leftIndex = 0;
        var rightIndex = 0;
        while (leftIndex < left.Length && rightIndex < right.Length)
        {
            var a = NextScalar(left, ref leftIndex);
            var b = NextScalar(right, ref rightIndex);
            if (a != b) return a.CompareTo(b);
        }
        return (left.Length - leftIndex).CompareTo(right.Length - rightIndex);
    }

    private static int NextScalar(string source, ref int index)
    {
        var first = source[index++];
        if (char.IsHighSurrogate(first) && index < source.Length && char.IsLowSurrogate(source[index]))
            return char.ConvertToUtf32(first, source[index++]);
        return first;
    }

    // A flat, iterative projection preserves ordering and CDATA identity.
    // Expected JSON is reviewed by hand; it is never parsed with Jint.
    internal static string Projection(Document document)
    {
        var entries = new List<XmlProjectionEntry>();
        var stack = new Stack<(Node Node, int Depth)>();
        for (var child = document.LastChild; child is not null; child = child.PreviousSibling)
            stack.Push((child, 0));
        while (stack.Count > 0)
        {
            var (node, depth) = stack.Pop();
            entries.Add(new XmlProjectionEntry(
                depth,
                node.NodeType.ToString(),
                node switch
                {
                    Element element => element.TagName,
                    ProcessingInstruction instruction => instruction.Target,
                    DocumentType type => type.Name,
                    _ => null
                },
                node is Element named ? named.NamespaceUri : null,
                node is Element prefixed ? prefixed.Prefix : null,
                node switch
                {
                    Text text => text.Data,
                    CDataSection cdata => cdata.Data,
                    Comment comment => comment.Data,
                    ProcessingInstruction instruction => instruction.Data,
                    _ => null
                },
                node is Element withAttributes
                    ? withAttributes.Attributes.Select(attribute => new XmlProjectionAttribute(
                        attribute.Name, attribute.NamespaceUri, attribute.Prefix, attribute.Value)).ToArray()
                    : null,
                node is DocumentType doctype ? doctype.PublicId : null,
                node is DocumentType namedType ? namedType.SystemId : null));
            for (var child = node.LastChild; child is not null; child = child.PreviousSibling)
                stack.Push((child, depth + 1));
        }
        return JsonSerializer.Serialize(entries);
    }
}

internal sealed class XmlOutputObservationGapException(string message) : Exception(message);

internal sealed record XmlProjectionEntry(int Depth, string Kind, string? Name, string? NamespaceUri,
    string? Prefix, string? Value, XmlProjectionAttribute[]? Attributes, string? PublicId, string? SystemId);

internal sealed record XmlProjectionAttribute(string Name, string? NamespaceUri, string? Prefix, string Value);
