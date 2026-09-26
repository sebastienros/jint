using Jint.HtmlParser;
using Jint.Native;

namespace Jint.Browser.Dom;

/// <summary>DOM §4.2.6's variadic mutations over the native tree.</summary>
internal static class DomParentNodeMembers
{
    internal static JsValue Append(DomRealm realm, Node parent, JsValue[] arguments)
    {
        if (ConvertNodes(realm, parent, arguments, "append") is { } node)
        {
            parent.AppendChild(node);
        }
        return JsValue.Undefined;
    }

    internal static JsValue Prepend(DomRealm realm, Node parent, JsValue[] arguments)
    {
        if (ConvertNodes(realm, parent, arguments, "prepend") is { } node)
        {
            parent.InsertBefore(node, parent.FirstChild);
        }
        return JsValue.Undefined;
    }

    /// <summary>https://dom.spec.whatwg.org/#dom-parentnode-replacechildren</summary>
    internal static JsValue ReplaceChildren(DomRealm realm, Node parent, JsValue[] arguments)
    {
        // Native replace-all validates before removing children and publishes one mutation record.
        parent.ReplaceChildren(ConvertNodes(realm, parent, arguments, "replaceChildren"));
        return JsValue.Undefined;
    }

    internal static Node? ConvertNodes(DomRealm realm, Node parent, JsValue[] arguments, string operation)
    {
        var member = (parent switch
        {
            Document => "Document.",
            DocumentFragment => "DocumentFragment.",
            DocumentType => "DocumentType.",
            Text or CDataSection or Comment or ProcessingInstruction => "CharacterData.",
            _ => "Element.",
        }) + operation;
        var nodes = DomConvert.NodeOrTextRest(realm, parent, arguments, 0, member);
        if (nodes.Length == 0) return null;
        if (nodes.Length == 1) return nodes[0].Node ?? throw DomException.Hierarchy();
        var fragment = (parent as Document ?? parent.OwnerDocument!).CreateDocumentFragment();
        realm.CreationRealmOf(fragment);
        // Append in argument order. A later Attr refusal leaves the preceding nodes moved,
        // as DOM's convert-nodes algorithm requires. Do not preflight this union.
        foreach (var identity in nodes) fragment.AppendChild(identity.Node ?? throw DomException.Hierarchy());
        return fragment;
    }
}
