#nullable enable
using System.Text;
using System.Text.Json;
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Xml.Conformance;

internal static class XmlEvidence
{
    private static readonly IComparer<string> ScalarOrder = Comparer<string>.Create(CompareUnicodeScalars);
    private static readonly Uri CorpusOrigin = new("https://xmlconf.invalid/");

    internal static Uri CorpusInputBase(string inputPath)
    {
        XmlCorpus.EnsureSafePath(inputPath);
        var escapedSegments = inputPath.Split('/').Select(Uri.EscapeDataString);
        return new Uri(CorpusOrigin, string.Join('/', escapedSegments));
    }

    // W3C XML Test Suite, xmlconf/sun/cxml.html, Second XML Canonical Form.
    // Notation declarations come only from the public parse result, never from OUTPUT or input rescanning.
    internal static string SecondCanonicalForm(Document document, Uri inputBase)
    {
        var result = new StringBuilder();
        // The pinned IBM OUTPUTs emit DTD PI events before the reconstructed notation block.
        // These are parse records, not invented DOM children or rescanned source text.
        foreach (var instruction in document.XmlDtdProcessingInstructions)
            result.Append("<?").Append(instruction.Target).Append(' ').Append(instruction.Data).Append("?>");
        if (document.XmlNotations.Count > 0)
        {
            var doctype = document.ChildNodes.OfType<DocumentType>().SingleOrDefault()
                ?? throw new XmlOutputObservationGapException("Read notation metadata has no document type node");
            result.Append("<!DOCTYPE ").Append(doctype.Name).Append(" [\n");
            foreach (var notation in document.XmlNotations.OrderBy(item => item.Name, ScalarOrder))
            {
                result.Append("<!NOTATION ").Append(notation.Name).Append(' ');
                if (notation.PublicId is not null)
                {
                    result.Append("PUBLIC ");
                    AppendSingleQuoted(result, notation.PublicId);
                    if (notation.SystemId is not null)
                    {
                        result.Append(' ');
                        AppendSingleQuoted(result, CanonicalSystemId(notation.SystemId, inputBase));
                    }
                }
                else if (notation.SystemId is not null)
                {
                    result.Append("SYSTEM ");
                    AppendSingleQuoted(result, CanonicalSystemId(notation.SystemId, inputBase));
                }
                else
                {
                    throw new XmlOutputObservationGapException("Read notation has no external identifier");
                }
                result.Append(">\n");
            }
            result.Append("]>\n");
        }
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

    private static void AppendSingleQuoted(StringBuilder output, string identifier)
    {
        if (identifier.Contains('\''))
            throw new XmlOutputObservationGapException("Second Canonical Form cannot represent a literal apostrophe in a single-quoted identifier");
        output.Append('\'').Append(identifier).Append('\'');
    }

    // Sun's Second Canonical Form: remove fragments, escape non-ASCII as UTF-8,
    // and use the shortest relative reference when the identifier shares the input base.
    // The reserved corpus origin is provenance for comparison only; it is never resolved or fetched.
    internal static string CanonicalSystemId(string systemId, Uri inputBase)
    {
        if (!inputBase.IsAbsoluteUri)
            throw new XmlOutputObservationGapException("Second Canonical Form needs an absolute input base");
        var fragment = systemId.IndexOf('#');
        var withoutFragment = fragment < 0 ? systemId : systemId[..fragment];
        var escaped = EscapeNonAscii(withoutFragment);
        try
        {
            var colon = escaped.IndexOf(':');
            if (colon > 0 && Uri.CheckSchemeName(escaped[..colon]))
            {
                // Uri rejects some legal scheme-specific spellings such as file:/dev/null.
                // Preserve those absolute identifiers instead of inventing a relative one.
                return Uri.TryCreate(escaped, UriKind.Absolute, out var absolute) && SameOrigin(inputBase, absolute)
                    ? ShortestRelativeReference(inputBase, absolute)
                    : escaped;
            }
            var resolved = new Uri(inputBase, escaped);
            return SameOrigin(inputBase, resolved)
                ? ShortestRelativeReference(inputBase, resolved)
                : resolved.AbsoluteUri;
        }
        catch (UriFormatException error)
        {
            throw new XmlOutputObservationGapException($"Cannot canonicalize system identifier: {error.Message}");
        }
    }

    private static bool SameOrigin(Uri left, Uri right) =>
        left.Scheme.Equals(right.Scheme, StringComparison.OrdinalIgnoreCase) &&
        left.IdnHost.Equals(right.IdnHost, StringComparison.OrdinalIgnoreCase) &&
        left.Port == right.Port &&
        left.UserInfo.Equals(right.UserInfo, StringComparison.Ordinal);

    private static bool ResolvesTo(Uri inputBase, string reference, Uri target)
    {
        var resolved = new Uri(inputBase, reference);
        return SameOrigin(resolved, target) &&
               resolved.AbsolutePath.Equals(target.AbsolutePath, StringComparison.Ordinal) &&
               resolved.Query.Equals(target.Query, StringComparison.Ordinal) &&
               resolved.Fragment.Equals(target.Fragment, StringComparison.Ordinal);
    }

    private static string ShortestRelativeReference(Uri inputBase, Uri target)
    {
        var shortest = inputBase.MakeRelativeUri(target).OriginalString;
        if (!ResolvesTo(inputBase, shortest, target))
            throw new XmlOutputObservationGapException("Relative system identifier does not resolve to its target");
        void Consider(string candidate)
        {
            if (candidate.Length < shortest.Length && ResolvesTo(inputBase, candidate, target))
                shortest = candidate;
        }
        var rooted = target.AbsolutePath + target.Query;
        Consider(rooted);
        if (inputBase.AbsolutePath == target.AbsolutePath)
            Consider(target.Query);
        var queryStart = shortest.IndexOf('?');
        if (queryStart < 0)
            queryStart = shortest.Length;
        if (queryStart > 0 && shortest[queryStart - 1] == '/')
            Consider(shortest.Remove(queryStart - 1, 1));
        return shortest;
    }

    private static string EscapeNonAscii(string value)
    {
        var result = new StringBuilder(value.Length);
        Span<byte> utf8 = stackalloc byte[4];
        const string hex = "0123456789ABCDEF";
        foreach (var scalar in value.EnumerateRunes())
        {
            if (scalar.Value <= 0x7F)
            {
                result.Append((char)scalar.Value);
                continue;
            }
            var count = scalar.EncodeToUtf8(utf8);
            for (var index = 0; index < count; index++)
            {
                var octet = utf8[index];
                result.Append('%').Append(hex[octet >> 4]).Append(hex[octet & 0xF]);
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
