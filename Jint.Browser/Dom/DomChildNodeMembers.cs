using Jint.HtmlParser;
using Jint.Native;

namespace Jint.Browser.Dom;

/// <summary>
/// <a href="https://dom.spec.whatwg.org/#interface-childnode">DOM §4.2.8</a>'s three mutation methods, in the
/// order the standard puts their steps in.
/// </summary>
/// <remarks>
/// <para>
/// <b>The order is the whole of it.</b> Each of <c>before</c>, <c>after</c> and <c>replaceWith</c> chooses a
/// <i>viable</i> sibling — the nearest one that is not itself among the arguments — <b>before</b> running
/// "convert nodes into a node", because that conversion moves every argument node into a fragment and can
/// therefore empty the very run of siblings the insertion point was going to be measured from. AngleSharp's
/// <c>IChildNode.Before</c>/<c>After</c>/<c>Replace</c> convert first and then ask the parent to insert
/// relative to the receiver, so <c>child.before('text', child)</c> — the argument list a page writes when it
/// is reordering around itself — reaches <c>InsertBefore</c> with a reference node the conversion has already
/// taken out of the parent, and raises <c>NotFoundError</c> where the standard has an answer.
/// <c>dom/nodes/ChildNode-before.html</c>, <c>-after.html</c> and <c>-replaceWith.html</c> are nine
/// assertions of exactly that; the divergence register keeps the upstream half.
/// </para>
/// <para>
/// The receiver is a native <c>Node</c> because DOM declares these on the <c>ChildNode</c> mixin, which
/// <c>Element</c>, <c>CharacterData</c> and <c>DocumentType</c> include; the hook is one method for all three
/// for the same reason the generated members are one mixin's.
/// </para>
/// </remarks>
internal static class DomChildNodeMembers
{
    /// <summary>https://dom.spec.whatwg.org/#dom-childnode-before</summary>
    internal static void Before(DomRealm realm, Node node, JsValue[] arguments)
    {
        var parent = node.ParentNode;
        if (parent is null)
        {
            return;
        }

        var viablePreviousSibling = Viable(node, arguments, forward: false);
        var inserted = ConvertNodes(realm, node, arguments, "before");

        // Step 4 is read after the conversion on purpose: the sibling that follows the viable one may be a
        // node the conversion just moved, and "the first child" may now be a different node entirely.
        var reference = viablePreviousSibling is null ? parent.FirstChild : viablePreviousSibling.NextSibling;

        if (inserted is not null)
        {
            parent.InsertBefore(inserted, reference);
        }
    }

    /// <summary>https://dom.spec.whatwg.org/#dom-childnode-after</summary>
    internal static void After(DomRealm realm, Node node, JsValue[] arguments)
    {
        var parent = node.ParentNode;
        if (parent is null)
        {
            return;
        }

        var viableNextSibling = Viable(node, arguments, forward: true);
        var inserted = ConvertNodes(realm, node, arguments, "after");

        if (inserted is not null)
        {
            parent.InsertBefore(inserted, viableNextSibling);
        }
    }

    /// <summary>https://dom.spec.whatwg.org/#dom-childnode-replacewith</summary>
    internal static void ReplaceWith(DomRealm realm, Node node, JsValue[] arguments)
    {
        var parent = node.ParentNode;
        if (parent is null)
        {
            return;
        }

        var viableNextSibling = Viable(node, arguments, forward: true);
        var inserted = ConvertNodes(realm, node, arguments, "replaceWith");

        if (inserted is null)
        {
            // An empty argument list converts to an empty fragment, and replacing with one removes the node.
            parent.RemoveChild(node);
            return;
        }

        // "If this's parent is parent, replace this with node within parent" — this may no longer be a child
        // of parent, because the conversion moves an argument that was the receiver itself into the fragment.
        if (ReferenceEquals(node.ParentNode, parent))
        {
            parent.ReplaceChild(inserted, node);
            return;
        }

        parent.InsertBefore(inserted, viableNextSibling);
    }

    /// <summary>
    /// The nearest preceding (or following) sibling of <paramref name="node"/> that is not itself one of the
    /// argument nodes, which is what the standard calls the viable previous (or next) sibling.
    /// </summary>
    private static Node? Viable(Node node, JsValue[] arguments, bool forward)
    {
        for (var sibling = forward ? node.NextSibling : node.PreviousSibling;
             sibling is not null;
             sibling = forward ? sibling.NextSibling : sibling.PreviousSibling)
        {
            if (!IsArgument(sibling, arguments))
            {
                return sibling;
            }
        }

        return null;
    }

    private static bool IsArgument(Node sibling, JsValue[] arguments)
    {
        foreach (var argument in arguments)
        {
            if (argument is IDomWrapper wrapper && ReferenceEquals(wrapper.DomTarget, sibling))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#converting-nodes-into-a-node — one node stays itself, several become a
    /// fragment, and a string becomes a text node in the receiver's node document. <c>null</c> means there
    /// was nothing to insert.
    /// </summary>
    private static Node? ConvertNodes(DomRealm realm, Node node, JsValue[] arguments, string operation)
        => DomParentNodeMembers.ConvertNodes(realm, node, arguments, operation);

}
