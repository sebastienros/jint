using System.Xml.XPath;
using AngleSharp.Dom;
using AngleSharp.XPath;
using Jint.Browser.Layout;
using Jint.Browser.Runtime;
using Jint.DevTools;
using Jint.DevTools.Domains;
using Jint.DevTools.Protocol.DOM;
using Jint.Native;
using ProtocolDom = Jint.DevTools.Protocol.DOM;

namespace Jint.Browser.DevTools;

/// <summary>
/// What the <c>DOM</c> domain says without being asked, and the node shape everything it says is built from.
/// </summary>
/// <remarks>
/// <para>
/// <b>Only for nodes this attachment has been sent.</b> That is Chrome's rule and it is what makes the
/// event stream finite: a client hears about a subtree it has asked for and about nothing else, so a page
/// rewriting a list the client never walked costs one <c>childNodeCountUpdated</c> at most and usually
/// nothing. <see cref="_sent"/> is the set of pushed nodes and <see cref="_childrenSent"/> the subset whose
/// children went with them — the second is what decides between an inserted-node event and a count update,
/// because a client that has not been sent the children cannot place a new one among them.
/// </para>
/// <para>
/// <b>The records are AngleSharp's, at the microtask checkpoint.</b> <see cref="DomNodeTracker"/> parks them
/// as they arrive and delivers a batch on the engine's own queue, which is the lane
/// <c>Observers/MutationObserverLane</c> delivers a page's own <c>MutationObserver</c>s on — so a client and
/// a script see one document at the same moment.
/// </para>
/// </remarks>
internal sealed partial class DomDomain
{
    /// <summary>Turns one batch of AngleSharp's records into the events this attachment is owed.</summary>
    internal void Mutated(IReadOnlyList<IMutationRecord> records)
    {
        if (!IsEnabled)
        {
            return;
        }

        foreach (var record in records)
        {
            switch (record.Type)
            {
                case "childList":
                    ChildListMutated(record);
                    break;

                case "attributes":
                    AttributeMutated(record);
                    break;

                case "characterData":
                    CharacterDataMutated(record);
                    break;

                default:
                    break;
            }
        }
    }

    private void ChildListMutated(IMutationRecord record)
    {
        var parentId = Tracker.KnownIdOf(record.Target);
        if (parentId == 0 || !_sent.Contains(parentId))
        {
            return;
        }

        if (!_childrenSent.Contains(parentId))
        {
            // The client holds the parent and not its children, so all it can act on is how many there are.
            EmitDetached(DOMEvents.ChildNodeCountUpdated(new ChildNodeCountUpdatedEvent
            {
                NodeId = parentId,
                ChildNodeCount = record.Target.ChildNodes.Length,
            }));

            return;
        }

        if (record.Removed is { } removed)
        {
            foreach (var node in removed)
            {
                var nodeId = Tracker.KnownIdOf(node);
                if (nodeId != 0 && _sent.Remove(nodeId))
                {
                    _childrenSent.Remove(nodeId);
                    EmitDetached(DOMEvents.ChildNodeRemoved(new ChildNodeRemovedEvent
                    {
                        ParentNodeId = parentId,
                        NodeId = nodeId,
                    }));
                }
            }
        }

        if (record.Added is not { } added)
        {
            return;
        }

        foreach (var node in added)
        {
            // The previous sibling is what places the node in a list the client already holds; a record's own
            // PreviousSibling is the one from before the batch, so it is read off the tree instead.
            var previous = node.PreviousSibling;

            EmitDetached(DOMEvents.ChildNodeInserted(new ChildNodeInsertedEvent
            {
                ParentNodeId = parentId,
                PreviousNodeId = previous is null ? 0 : Tracker.KnownIdOf(previous),
                Node = Describe(node, depth: 0, pushed: true),
            }));
        }
    }

    private void AttributeMutated(IMutationRecord record)
    {
        var nodeId = Tracker.KnownIdOf(record.Target);
        if (nodeId == 0 || !_sent.Contains(nodeId) || record.AttributeName is not { } name)
        {
            return;
        }

        if (record.Target is IElement element && element.GetAttribute(name) is { } value)
        {
            EmitDetached(DOMEvents.AttributeModified(new AttributeModifiedEvent
            {
                NodeId = nodeId,
                Name = name,
                Value = value,
            }));

            return;
        }

        EmitDetached(DOMEvents.AttributeRemoved(new AttributeRemovedEvent { NodeId = nodeId, Name = name }));
    }

    private void CharacterDataMutated(IMutationRecord record)
    {
        var nodeId = Tracker.KnownIdOf(record.Target);
        if (nodeId == 0 || !_sent.Contains(nodeId))
        {
            return;
        }

        EmitDetached(DOMEvents.CharacterDataModified(new CharacterDataModifiedEvent
        {
            NodeId = nodeId,
            CharacterData = record.Target.NodeValue ?? "",
        }));
    }

    /// <summary>
    /// The protocol's <c>Node</c> for <paramref name="node"/>, with <paramref name="depth"/> levels below it.
    /// </summary>
    /// <param name="node">The node to describe.</param>
    /// <param name="depth">How many levels of children to include; <c>-1</c> for the whole subtree.</param>
    /// <param name="pushed">
    /// Whether this counts as sending the node to the client. <c>describeNode</c> passes
    /// <see langword="false"/>, which is what makes it answer <c>nodeId: 0</c> for a node the client has
    /// never been given — Chrome's own behaviour.
    /// </param>
    private ProtocolDom.Node Describe(INode node, int depth, bool pushed)
    {
        var nodeId = pushed ? Push(node) : Tracker.KnownIdOf(node);

        ProtocolDom.Node[]? children = null;

        if (depth != 0 && node.ChildNodes.Length != 0)
        {
            var next = depth < 0 ? -1 : depth - 1;
            children = new ProtocolDom.Node[node.ChildNodes.Length];

            for (var i = 0; i < children.Length; i++)
            {
                children[i] = Describe(node.ChildNodes[i], next, pushed);
            }
        }

        if (depth != 0 && pushed)
        {
            // Marked even for a node with no children: the client has been given its child list either way,
            // and that is what decides between an inserted-node event and a count update when one arrives.
            _childrenSent.Add(nodeId);
        }

        var element = node as IElement;

        // A parent the client has never been sent has no identifier to name, so the member is absent rather
        // than zero: a client reading it unconditionally would place the node under the sentinel node.
        var parentId = node.Parent is { } parent ? Tracker.KnownIdOf(parent) : 0;

        return new ProtocolDom.Node
        {
            NodeId = nodeId,
            ParentId = parentId == 0 ? null : parentId,
            BackendNodeId = Tracker.BackendIdOf(node),
            NodeType = (int) node.NodeType,
            NodeName = node.NodeName,
            LocalName = element?.LocalName ?? "",
            NodeValue = node.NodeValue ?? "",
            ChildNodeCount = node.ChildNodes.Length,
            Children = children,
            Attributes = element is null ? null : Attributes(element),
            DocumentURL = node is IDocument document ? document.Url : null,
            BaseURL = node is IDocument baseDocument ? baseDocument.BaseUri : null,
            FrameId = node is IDocument ? _target.FrameId : null,

            // A shadow root is a real node here and is reported as one; contentDocument is deliberately
            // absent, because an <iframe>'s document has no realm and is never scripted (design doc §3), so a
            // client told about it would be told about a tree it can never evaluate in.
            ShadowRoots = element?.ShadowRoot is { } shadow ? [Describe(shadow, depth, pushed)] : null,
        };
    }

    /// <summary>Sends a node's children and marks them as the client's, which is <c>setChildNodes</c>.</summary>
    private void SendChildren(INode node, int depth)
    {
        var parentId = Push(node);
        var children = new ProtocolDom.Node[node.ChildNodes.Length];

        for (var i = 0; i < children.Length; i++)
        {
            children[i] = Describe(node.ChildNodes[i], depth < 0 ? -1 : depth - 1, pushed: true);
        }

        _childrenSent.Add(parentId);

        EmitDetached(DOMEvents.SetChildNodes(new SetChildNodesEvent { ParentId = parentId, Nodes = children }));
    }

    /// <summary>
    /// Sends every ancestor of <paramref name="node"/> the client has not been given, outermost first.
    /// </summary>
    /// <remarks>
    /// A client that asked about one node by handle has to be able to place it in the tree, and Chrome does
    /// that by pushing the chain above it as <c>setChildNodes</c>. A client that never enabled the domain
    /// hears none of it, which is what <see cref="DevToolsDomain.IsEnabled"/> gates.
    /// </remarks>
    private void PushAncestors(INode node)
    {
        if (!IsEnabled)
        {
            return;
        }

        var chain = new List<INode>();
        for (var parent = node.Parent; parent is not null; parent = parent.Parent)
        {
            chain.Add(parent);
        }

        chain.Reverse();

        foreach (var ancestor in chain)
        {
            if (!_childrenSent.Contains(Tracker.KnownIdOf(ancestor)))
            {
                SendChildren(ancestor, depth: 1);
            }
        }
    }

    /// <summary>Mints the node's identifier and records that this attachment now holds it.</summary>
    private int Push(INode node)
    {
        var nodeId = Tracker.IdOf(node);
        _sent.Add(nodeId);
        return nodeId;
    }

    /// <summary>Forgets every node this attachment was sent, which a navigation and a disable both do.</summary>
    private void Forget()
    {
        _sent.Clear();
        _childrenSent.Clear();
        _searches.Clear();
    }

    /// <summary>The flat viewport-relative box of a node, or <see langword="null"/> when it has none.</summary>
    private FlatBox? BoxOf(INode node)
        => node is IElement element && Runtime() is { } runtime
            ? runtime.Layout.Current().ClientBoxOf(element)
            : null;

    /// <summary>The page runtime of the document a node belongs to.</summary>
    private PageRuntime Runtime(INode node)
        => Runtime() ?? Throw.ServerError<PageRuntime>("Node with given id does not belong to the document");

    /// <summary>The page runtime this target is showing, or <see langword="null"/> before its first parse.</summary>
    private PageRuntime? Runtime() => PageRuntime.Find(_target.Runtime.Engine);

    /// <summary>The document, or Chrome's own refusal when there is none yet.</summary>
    private IDocument Document()
        => Runtime()?.Document ?? Throw.ServerError<IDocument>("Document is not available");

    /// <summary>The node an identifier names, in Chrome's own wording when it names none.</summary>
    private INode Resolve(int? nodeId, int? backendNodeId, string? objectId)
    {
        if (nodeId is { } id)
        {
            return RequireNodeId(id);
        }

        if (backendNodeId is { } backendId)
        {
            return Tracker.ByBackendId(backendId) ?? Throw.ServerError<INode>("No node found for given backend id");
        }

        if (objectId is { Length: > 0 } handle)
        {
            return NodeOf(_objects.Table.Resolve(handle));
        }

        return Throw.ServerError<INode>("Either nodeId, backendNodeId or objectId must be specified");
    }

    /// <summary>The node a <c>nodeId</c> names, in Chrome's own wording when it names none.</summary>
    private INode RequireNodeId(int nodeId)
        => Tracker.ByNodeId(nodeId) ?? Throw.ServerError<INode>("Could not find node with given id");

    /// <summary>The element a <c>nodeId</c> names, refusing a node that is not one.</summary>
    private IElement RequireElement(int nodeId)
        => RequireNodeId(nodeId) as IElement ?? Throw.ServerError<IElement>("Node is not an Element");

    /// <summary>The node a handle wraps, refusing a handle that is not one.</summary>
    private static INode NodeOf(JsValue value)
        => value is Dom.DomNodeObject wrapper
            ? wrapper.Node
            : Throw.ServerError<INode>("Object id doesn't reference a Node");

    /// <summary>The flat name/value array the protocol reports an element's attributes as.</summary>
    private static string[] Attributes(IElement element)
    {
        var attributes = new List<string>(element.Attributes.Length * 2);

        foreach (var attribute in element.Attributes)
        {
            attributes.Add(attribute.Name);
            attributes.Add(attribute.Value);
        }

        return [.. attributes];
    }

    /// <summary>
    /// The elements a selector matches under <paramref name="node"/>, or none when it is not a selector.
    /// </summary>
    /// <remarks>
    /// A query that does not parse is not an error here: <c>performSearch</c> tries the same string as text
    /// afterwards, and <c>querySelector</c> is the one caller that would rather have been told — which it is,
    /// by getting no match, because AngleSharp raises a <c>DomException</c> that carries no protocol shape.
    /// </remarks>
    private static IEnumerable<IElement> Query(INode node, string selector)
    {
        if (node is not IParentNode parent || selector.Length == 0)
        {
            return [];
        }

        try
        {
            return parent.QuerySelectorAll(selector);
        }
        catch (DomException)
        {
            return [];
        }
    }

    /// <summary>
    /// The nodes an XPath expression selects, or none when <paramref name="query"/> is not one.
    /// </summary>
    /// <remarks>
    /// <c>System.Xml.XPath</c> over <c>AngleSharp.XPath</c>'s navigator, which is the same evaluator
    /// <c>document.evaluate</c> answers from — so a front end's search and a page's own XPath agree. A query
    /// that is not an expression raises, and raising is how this arm says "not mine": a search box is typed
    /// into one character at a time, and every prefix of <c>//div</c> would otherwise be an error.
    /// </remarks>
    private static List<INode> XPathMatches(IDocument document, string query)
    {
        if (query.Length == 0 || document.DocumentElement is null)
        {
            return [];
        }

        try
        {
            var navigator = new HtmlDocumentNavigator(document, document, ignoreNamespaces: true);

            if (navigator.Evaluate(query) is not XPathNodeIterator nodes)
            {
                return [];
            }

            var found = new List<INode>();

            while (nodes.MoveNext())
            {
                if (nodes.Current is HtmlDocumentNavigator { CurrentNode: { } node })
                {
                    found.Add(node);
                }
            }

            return found;
        }
        catch (Exception exception) when (exception is XPathException or ArgumentException)
        {
            return [];
        }
    }

    /// <summary>
    /// The nodes whose text or attribute values contain <paramref name="query"/>, case-insensitively.
    /// </summary>
    private static IEnumerable<INode> TextMatches(IDocument document, string query)
    {
        if (query.Length == 0)
        {
            yield break;
        }

        var stack = new Stack<INode>();
        stack.Push(document);

        while (stack.Count > 0)
        {
            var node = stack.Pop();

            for (var i = node.ChildNodes.Length - 1; i >= 0; i--)
            {
                stack.Push(node.ChildNodes[i]);
            }

            if (node.NodeType == NodeType.Text &&
                node.NodeValue?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
            {
                yield return node;
                continue;
            }

            if (node is not IElement element)
            {
                continue;
            }

            foreach (var attribute in element.Attributes)
            {
                if (attribute.Value.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    attribute.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    yield return element;
                    break;
                }
            }
        }
    }

    /// <summary>The depth a command asked for, which the protocol defaults to one.</summary>
    private static int Depth(int? depth) => depth ?? 1;
}
