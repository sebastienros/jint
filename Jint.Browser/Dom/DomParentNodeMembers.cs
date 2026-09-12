using AngleSharp.Dom;
using Jint.Browser.Runtime;
using Jint.Native;
using Jint.Runtime;

namespace Jint.Browser.Dom;

/// <summary>The DOM §4.2.6 mutation methods AngleSharp does not expose.</summary>
internal static class DomParentNodeMembers
{
    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-parentnode-replacechildren — replace every child and queue one
    /// child-list mutation record for the replacement.
    /// </summary>
    internal static JsValue ReplaceChildren(DomRealm realm, INode parent, JsValue[] arguments)
    {
        var replacement = ConvertNodes(parent, arguments);
        Validate(parent, replacement);

        var removed = parent.ChildNodes.ToArray();
        var added = replacement switch
        {
            null => [],
            IDocumentFragment fragment => fragment.ChildNodes.ToArray(),
            _ => [replacement],
        };

        var lane = PageRuntime.Find(realm.Engine)?.MutationObservers;
        lane?.BeginReplaceAll(parent);

        try
        {
            while (parent.FirstChild is { } child)
            {
                parent.RemoveChild(child);
            }

            if (replacement is not null)
            {
                parent.AppendChild(replacement);
            }

            lane?.CompleteReplaceAll(parent, added, removed);
        }
        catch
        {
            lane?.CancelReplaceAll(parent);
            throw;
        }

        return JsValue.Undefined;
    }

    /// <summary>DOM's "convert nodes into a node" algorithm.</summary>
    private static INode? ConvertNodes(INode parent, JsValue[] arguments)
    {
        if (arguments.Length == 0)
        {
            return null;
        }

        var document = parent as IDocument ?? parent.Owner!;
        var nodes = new INode[arguments.Length];

        for (var i = 0; i < arguments.Length; i++)
        {
            nodes[i] = arguments[i] is IDomWrapper { DomTarget: INode node }
                ? node
                : document.CreateTextNode(TypeConverter.ToString(arguments[i]));
        }

        if (nodes.Length == 1)
        {
            return nodes[0];
        }

        var fragment = document.CreateDocumentFragment();
        foreach (var node in nodes)
        {
            fragment.AppendChild(node);
        }

        return fragment;
    }

    /// <summary>DOM's replace-all pre-insertion validity check, with the parent's old children excluded.</summary>
    private static void Validate(INode parent, INode? replacement)
    {
        if (replacement is null)
        {
            return;
        }

        for (INode? ancestor = parent; ancestor is not null; ancestor = ancestor.Parent ?? (ancestor as IShadowRoot)?.Host)
        {
            if (ReferenceEquals(ancestor, replacement))
            {
                Refuse();
            }
        }

        if (replacement.NodeType is not (NodeType.DocumentFragment or NodeType.DocumentType or NodeType.Element
            or NodeType.Text or NodeType.ProcessingInstruction or NodeType.Comment))
        {
            Refuse();
        }

        if (parent is not IDocument)
        {
            if (replacement.NodeType == NodeType.DocumentType)
            {
                Refuse();
            }

            return;
        }

        var elements = 0;
        var doctypes = 0;
        var sawElement = false;
        IEnumerable<INode> nodes = replacement is IDocumentFragment fragment
            ? fragment.ChildNodes
            : [replacement];

        foreach (var node in nodes)
        {
            switch (node.NodeType)
            {
                case NodeType.Element:
                    sawElement = true;
                    if (++elements > 1)
                    {
                        Refuse();
                    }

                    break;
                case NodeType.DocumentType:
                    if (sawElement || ++doctypes > 1)
                    {
                        Refuse();
                    }

                    break;
                case NodeType.Text:
                    Refuse();
                    break;
            }
        }
    }

    private static void Refuse() => throw new AngleSharp.Dom.DomException(DomError.HierarchyRequest);
}
