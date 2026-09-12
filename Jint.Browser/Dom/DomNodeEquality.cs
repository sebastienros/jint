using AngleSharp.Dom;

namespace Jint.Browser.Dom;

/// <summary>
/// DOM §4.4's <a href="https://dom.spec.whatwg.org/#concept-node-equals">node equality</a>, which is a
/// question about two trees and about nothing else.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is here because AngleSharp's <c>Node.Equals</c> answers a different question.</b> That method opens
/// by comparing the two nodes' <i>base URLs</i>, which the standard's algorithm does not mention at all, so
/// two structurally identical documents built different ways are unequal as soon as one of them inherited a
/// real page URL. In the other direction it compares neither a doctype's public and system identifiers nor a
/// <c>CharacterData</c>'s data, so a comment and a differently-worded comment are equal, and it compares an
/// attribute's <i>prefix</i>, which DOM deliberately leaves out. Six of
/// <c>dom/nodes/Node-isEqualNode.html</c>'s seven tests are those three defects. The divergence register
/// records every one of them; this is the standard's algorithm over the same tree.
/// </para>
/// <para>
/// <b>The walk is iterative.</b> DOM states the algorithm recursively, and a page is free to build a tree
/// as deep as its node budget allows — the whole of a document's depth is one <c>isEqualNode</c> frame per
/// level, on the page thread, where a stack overflow is not an exception anything can catch. A worklist of
/// pairs costs one allocation and cannot.
/// </para>
/// </remarks>
internal static class DomNodeEquality
{
    /// <summary>Whether <paramref name="left"/> equals <paramref name="right"/> under DOM §4.4.</summary>
    /// <remarks>
    /// <c>isEqualNode(null)</c> is <see langword="false"/>, which falls out of the null check rather than
    /// being a case: the IDL argument is <c>Node?</c> and a node is never equal to nothing.
    /// </remarks>
    internal static bool AreEqual(INode? left, INode? right)
    {
        if (left is null || right is null)
        {
            return false;
        }

        var pending = new Stack<(INode Left, INode Right)>();
        pending.Push((left, right));

        while (pending.Count != 0)
        {
            var (a, b) = pending.Pop();

            if (a.NodeType != b.NodeType || !SameData(a, b))
            {
                return false;
            }

            var children = a.ChildNodes;
            var others = b.ChildNodes;

            if (children.Length != others.Length)
            {
                return false;
            }

            for (var i = 0; i < children.Length; i++)
            {
                pending.Push((children[i], others[i]));
            }
        }

        return true;
    }

    /// <summary>
    /// The per-interface half of the algorithm: "the following are equal, switching on the interface A
    /// implements". Everything not named there — a document, a fragment, a shadow root — compares on its
    /// children alone.
    /// </summary>
    private static bool SameData(INode a, INode b) => a switch
    {
        IDocumentType doctype
            => b is IDocumentType other
               && string.Equals(doctype.Name, other.Name, StringComparison.Ordinal)
               && string.Equals(doctype.PublicIdentifier, other.PublicIdentifier, StringComparison.Ordinal)
               && string.Equals(doctype.SystemIdentifier, other.SystemIdentifier, StringComparison.Ordinal),

        IElement element => b is IElement other && SameElement(element, other),

        IAttr attribute => b is IAttr other && SameAttribute(attribute, other),

        IProcessingInstruction instruction
            => b is IProcessingInstruction other
               && string.Equals(instruction.Target, other.Target, StringComparison.Ordinal)
               && string.Equals(instruction.Data, other.Data, StringComparison.Ordinal),

        // Text, CDATASection and Comment, which the standard names one by one and which agree on `data`.
        // The node types were compared before this, so a text node is never reached with a comment.
        ICharacterData data => b is ICharacterData other && string.Equals(data.Data, other.Data, StringComparison.Ordinal),

        _ => true,
    };

    /// <summary>
    /// An element's namespace, namespace prefix, local name and attribute-list size, and then the set match
    /// the standard states separately: "each attribute in its attribute list has an attribute that equals an
    /// attribute in B's attribute list".
    /// </summary>
    /// <remarks>
    /// The set match is quadratic on purpose. An attribute list is a handful of entries — the elements with
    /// the most of them in a real page have a dozen — and an index would allocate a dictionary per element
    /// pair for a walk that already visits every node in both trees.
    /// </remarks>
    private static bool SameElement(IElement element, IElement other)
    {
        if (!SameName(element.NamespaceUri, element.LocalName, other.NamespaceUri, other.LocalName)
            || !string.Equals(Prefix(element.Prefix), Prefix(other.Prefix), StringComparison.Ordinal)
            || element.Attributes.Length != other.Attributes.Length)
        {
            return false;
        }

        foreach (var attribute in element.Attributes)
        {
            var matched = false;

            foreach (var candidate in other.Attributes)
            {
                if (SameAttribute(attribute, candidate))
                {
                    matched = true;
                    break;
                }
            }

            if (!matched)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// An attribute's namespace, local name and value — and <b>not</b> its prefix, which is the one place
    /// the standard's element rule and its attribute rule deliberately disagree.
    /// </summary>
    private static bool SameAttribute(IAttr attribute, IAttr other)
        => SameName(attribute.NamespaceUri, attribute.LocalName, other.NamespaceUri, other.LocalName)
           && string.Equals(attribute.Value, other.Value, StringComparison.Ordinal);

    private static bool SameName(string? namespaceUri, string localName, string? otherNamespace, string otherLocalName)
        => string.Equals(Namespace(namespaceUri), Namespace(otherNamespace), StringComparison.Ordinal)
           && string.Equals(localName, otherLocalName, StringComparison.Ordinal);

    // DOM has one spelling for "no namespace" and one for "no prefix", and AngleSharp reaches both of them
    // through the empty string as well as through null: setAttributeNS("", "x", "") stores an empty
    // namespace where createAttribute stores none. Comparing the raw values would make those two unequal.
    private static string? Namespace(string? value) => string.IsNullOrEmpty(value) ? null : value;

    private static string? Prefix(string? value) => string.IsNullOrEmpty(value) ? null : value;
}
