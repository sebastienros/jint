using Jint.HtmlParser;

namespace Jint.Browser.Dom;

/// <summary>DOM §4.4's node-equality algorithm over native node and attribute identities.</summary>
internal static class DomNodeEquality
{
    internal static bool AreEqual(object? left, object? right)
    {
        if (left is Attr attribute) return right is Attr other && SameAttribute(attribute, other);
        if (left is not Node leftNode || right is not Node rightNode) return false;
        var pending = new Stack<(Node Left, Node Right)>();
        pending.Push((leftNode, rightNode));
        while (pending.TryPop(out var pair))
        {
            var (a, b) = pair;
            if (a.NodeType != b.NodeType || a.ChildCount != b.ChildCount || !SameData(a, b)) return false;
            var child = a.FirstChild;
            var other = b.FirstChild;
            while (child is not null)
            {
                pending.Push((child, other!));
                child = child.NextSibling;
                other = other!.NextSibling;
            }
        }
        return true;
    }

    private static bool SameData(Node a, Node b) => a switch
    {
        DocumentType value => b is DocumentType other && value.Name == other.Name &&
            value.PublicId == other.PublicId && value.SystemId == other.SystemId,
        Element value => b is Element other && SameElement(value, other),
        ProcessingInstruction value => b is ProcessingInstruction other && value.Target == other.Target && value.Data == other.Data,
        Text value => b is Text other && value.Data == other.Data,
        CDataSection value => b is CDataSection other && value.Data == other.Data,
        Comment value => b is Comment other && value.Data == other.Data,
        _ => true,
    };

    private static bool SameElement(Element value, Element other)
    {
        if (value.NamespaceUri != other.NamespaceUri || value.LocalName != other.LocalName ||
            value.Prefix != other.Prefix || value.AttributeCount != other.AttributeCount) return false;
        foreach (var attribute in value.Attributes)
        {
            var candidate = other.GetAttributeNodeNS(attribute.NamespaceUri, attribute.LocalName);
            if (candidate is null || !SameAttribute(attribute, candidate)) return false;
        }
        return true;
    }

    private static bool SameAttribute(Attr value, Attr other)
        => value.NamespaceUri == other.NamespaceUri && value.LocalName == other.LocalName && value.Value == other.Value;
}
