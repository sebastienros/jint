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

    internal IReadOnlyList<string> SupportedNames() => ReadNames(null, default);
    internal IReadOnlyList<string> SupportedNames(DomRealm realm)
        => ReadNames(realm.NativeReadCheckpoint, realm.CancellationToken);

    private List<string> ReadNames(Action<int>? checkpoint, CancellationToken token)
    {
        var work = new DomReadWork(checkpoint, token);
        work.Check();
        var names = new List<string>(Length);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (uint index = 0; index < (uint) Length; index++)
        {
            work.Step();
            var name = Name(owner.GetAttributeAt(index)!, work);
            if (Supports(name, work) && seen.Add(name)) names.Add(name);
        }
        work.Check();
        return names;
    }

    internal bool HasSupportedName(string name) => HasSupportedName(name, new DomReadWork(null, default));
    internal bool HasSupportedName(DomRealm realm, string name)
        => HasSupportedName(name, new DomReadWork(realm.NativeReadCheckpoint, realm.CancellationToken));

    private bool HasSupportedName(string name, DomReadWork work)
    {
        work.Check();
        if (!Supports(name, work)) { work.Check(); return false; }
        for (uint index = 0; index < (uint) Length; index++)
        {
            work.Step();
            if (!work.Equal(Name(owner.GetAttributeAt(index)!, work), name)) continue;
            work.Check();
            return true;
        }
        work.Check();
        return false;
    }

    internal Attr? GetNamedItem(DomRealm realm, string name)
    {
        var work = new DomReadWork(realm.NativeReadCheckpoint, realm.CancellationToken);
        work.Check();
        if (owner.NamespaceUri == Namespaces.Html && owner.OwnerDocument?.Kind == DocumentKind.Html)
        {
            for (var firstUpper = 0; firstUpper < name.Length; firstUpper++)
            {
                work.Step();
                if (name[firstUpper] is not (>= 'A' and <= 'Z')) continue;
                work.Check();
                var buffer = name.ToCharArray();
                work.Check();
                for (var i = firstUpper; i < buffer.Length; i++)
                {
                    work.Step();
                    var character = buffer[i];
                    if (character is >= 'A' and <= 'Z') buffer[i] = (char) (character + ('a' - 'A'));
                }
                work.Check();
                name = new string(buffer);
                work.Check();
                break;
            }
        }
        for (uint index = 0; index < (uint) Length; index++)
        {
            work.Step();
            var attribute = owner.GetAttributeAt(index)!;
            if (!work.Equal(Name(attribute, work), name)) continue;
            work.Check();
            return attribute;
        }
        work.Check();
        return null;
    }

    private bool Supports(string name, DomReadWork work)
    {
        var lowercaseOnly = owner.NamespaceUri == Namespaces.Html && owner.OwnerDocument?.Kind == DocumentKind.Html;
        foreach (var character in name)
        {
            work.Step();
            if (lowercaseOnly && character is >= 'A' and <= 'Z') return false;
        }
        return true;
    }

    private static string Name(Attr attribute, DomReadWork work)
    {
        if (attribute.Prefix is null) return attribute.LocalName;
        work.Check();
        var name = attribute.Name;
        work.Check();
        return name;
    }
}
