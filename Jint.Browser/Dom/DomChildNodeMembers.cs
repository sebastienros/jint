using AngleSharp.Dom;
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
/// The receiver is an <c>INode</c> because DOM declares these on the <c>ChildNode</c> mixin, which
/// <c>Element</c>, <c>CharacterData</c> and <c>DocumentType</c> include; the hook is one method for all three
/// for the same reason the generated members are one mixin's.
/// </para>
/// </remarks>
internal static class DomChildNodeMembers
{
    /// <summary>https://dom.spec.whatwg.org/#dom-childnode-before</summary>
    internal static void Before(DomRealm realm, INode node, JsValue[] arguments)
    {
        var parent = node.Parent;
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
    internal static void After(DomRealm realm, INode node, JsValue[] arguments)
    {
        var parent = node.Parent;
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
    internal static void ReplaceWith(DomRealm realm, INode node, JsValue[] arguments)
    {
        var parent = node.Parent;
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
        if (ReferenceEquals(node.Parent, parent))
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
    private static INode? Viable(INode node, JsValue[] arguments, bool forward)
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

    private static bool IsArgument(INode sibling, JsValue[] arguments)
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
    private static INode? ConvertNodes(DomRealm realm, INode node, JsValue[] arguments, string operation)
    {
        if (arguments.Length == 0)
        {
            return null;
        }

        var nodes = DomConvert.NodeOrTextRest(realm, node, arguments, 0, Member(node, operation));

        if (nodes.Length == 1)
        {
            return nodes[0];
        }

        var fragment = (node as IDocument ?? node.Owner!).CreateDocumentFragment();
        foreach (var child in nodes)
        {
            fragment.AppendChild(child);
        }

        return fragment;
    }

    /// <summary>
    /// The name a conversion failure is reported under. Only <c>NodeOrTextRest</c> reads it, and only for the
    /// one case it cannot convert: a detached <c>DocumentType</c>, which has no node document to make a text
    /// node in — so the interface alone is enough to place it.
    /// </summary>
    private static string Member(INode node, string operation)
        => node switch
        {
            IElement => "Element.",
            IDocumentType => "DocumentType.",
            _ => "CharacterData.",
        } + operation;
}
