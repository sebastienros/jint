using System.Runtime.CompilerServices;
using Jint.HtmlParser;

namespace Jint.Browser.Dom.Collections;

/// <summary>DOM §4.9.1's live view of one native element's attribute list.</summary>
internal sealed class DomNamedNodeMap(Element owner)
{
    private static readonly ConditionalWeakTable<Element, DomNamedNodeMap> Maps = new();
    internal static DomNamedNodeMap Of(Element owner) => Maps.GetValue(owner, static element => new(element));
    internal Element Owner => owner;
    internal int Length => owner.AttributeCount;
    internal Attr? Item(uint index) => owner.GetAttributeAt(index);
    internal Attr? GetNamedItem(string name) => owner.GetAttributeNode(name);
    internal Attr? GetNamedItemNS(string? namespaceUri, string localName) => owner.GetAttributeNodeNS(namespaceUri, localName);
    internal Attr? SetNamedItem(Attr attribute) => owner.SetAttributeNode(attribute);
    internal Attr RemoveNamedItem(string name)
        => owner.RemoveAttributeNode(owner.GetAttributeNode(name) ?? throw DomException.NotFound());
    internal Attr RemoveNamedItemNS(string? namespaceUri, string localName)
        => owner.RemoveAttributeNode(owner.GetAttributeNodeNS(namespaceUri, localName) ?? throw DomException.NotFound());

    internal IReadOnlyList<string> SupportedNames()
    {
        var names = new List<string>(Length);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var attribute in owner.Attributes)
        {
            var name = attribute.Name;
            if (Supports(name) && seen.Add(name)) names.Add(name);
        }
        return names;
    }

    internal bool HasSupportedName(string name)
    {
        if (!Supports(name)) return false;
        foreach (var attribute in owner.Attributes)
            if (attribute.Name == name) return true;
        return false;
    }

    private bool Supports(string name)
    {
        if (owner.NamespaceUri != Namespaces.Html || owner.OwnerDocument?.Kind != DocumentKind.Html) return true;
        foreach (var character in name)
            if (character is >= 'A' and <= 'Z') return false;
        return true;
    }
}
