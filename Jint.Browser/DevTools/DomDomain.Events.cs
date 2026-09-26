using System.Xml.XPath;
using Jint.HtmlParser;
using Jint.Browser.Dom;
using Jint.Browser.Dom.Views;
using NativeNode = Jint.HtmlParser.Node;
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
/// <b>The records are native snapshots, at the microtask checkpoint.</b> <see cref="DomNodeTracker"/> parks them
/// as they arrive and delivers a batch on the engine's own queue, which is the lane
/// <c>Observers/MutationObserverLane</c> delivers a page's own <c>MutationObserver</c>s on — so a client and
/// a script see one document at the same moment.
/// </para>
/// </remarks>
internal sealed partial class DomDomain
{
    /// <summary>Turns one batch of native records into the events this attachment is owed.</summary>
    internal void Mutated(IReadOnlyList<MutationRecord> records)
    {
        if (!IsEnabled)
        {
            return;
        }

        foreach (var record in records)
        {
            switch (record.Kind)
            {
                case MutationRecordKind.ChildList:
                    ChildListMutated(record);
                    break;

                case MutationRecordKind.Attributes:
                    AttributeMutated(record);
                    break;

                case MutationRecordKind.CharacterData:
                    CharacterDataMutated(record);
                    break;

                default:
                    break;
            }
        }
    }

    private void ChildListMutated(MutationRecord record)
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
                ChildNodeCount = record.Target.ChildCount,
            }));

            return;
        }

        foreach (var node in record.RemovedNodes)
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

        foreach (var node in record.AddedNodes)
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

    private void AttributeMutated(MutationRecord record)
    {
        var nodeId = Tracker.KnownIdOf(record.Target);
        if (nodeId == 0 || !_sent.Contains(nodeId) || record.AttributeName is not { } name)
        {
            return;
        }

        var qualifiedName = record.AttributeQualifiedName
            ?? throw new InvalidOperationException("Attribute mutation record has no qualified-name snapshot.");
        var work = ReadWork();
        Attr? attribute = null;
        if (record.Target is Element element)
        {
            for (uint i = 0; i < (uint) element.AttributeCount; i++)
            {
                work.Step();
                var candidate = element.GetAttributeAt(i)!;
                if (!work.Equal(candidate.LocalName, name)
                    || (record.AttributeNamespace is null ? candidate.NamespaceUri is not null
                        : !work.Equal(candidate.NamespaceUri, record.AttributeNamespace))
                    || !work.Equal(candidate.Name, qualifiedName)) continue;
                attribute = candidate;
                break;
            }
        }
        work.Check();
        if (attribute is not null)
        {
            EmitDetached(DOMEvents.AttributeModified(new AttributeModifiedEvent
            {
                NodeId = nodeId,
                Name = qualifiedName,
                Value = attribute.Value,
            }));

            return;
        }

        EmitDetached(DOMEvents.AttributeRemoved(new AttributeRemovedEvent { NodeId = nodeId, Name = qualifiedName }));
    }

    private void CharacterDataMutated(MutationRecord record)
    {
        var nodeId = Tracker.KnownIdOf(record.Target);
        if (nodeId == 0 || !_sent.Contains(nodeId))
        {
            return;
        }

        EmitDetached(DOMEvents.CharacterDataModified(new CharacterDataModifiedEvent
        {
            NodeId = nodeId,
            CharacterData = DomNodeMembers.Value(record.Target) ?? "",
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
    private ProtocolDom.Node Describe(object node, int depth, bool pushed)
    {
        var work = ReadWork();
        var result = DescribeOne(node, depth, pushed, work);
        var pending = new Stack<(object Source, ProtocolDom.Node Target, int Depth)>();
        pending.Push((node, result, depth));
        while (pending.Count > 0)
        {
            work.Step();
            var (source, target, remaining) = pending.Pop();
            if (remaining != 0)
            {
                if (pushed) _childrenSent.Add(target.NodeId);
                var children = target.Children;
                var next = remaining < 0 ? -1 : remaining - 1;
                var index = 0;
                for (var child = (source as NativeNode)?.FirstChild; child is not null; child = child.NextSibling)
                {
                    work.Step();
                    var described = DescribeOne(child, next, pushed, work);
                    children![index++] = described;
                    pending.Push((child, described, next));
                }
            }
            if (source is Element { AttachedShadowRoot: { } shadow })
            {
                var described = DescribeOne(shadow, remaining, pushed, work);
                target.ShadowRoots![0] = described;
                pending.Push((shadow, described, remaining));
            }
        }
        work.Check();
        return result;
    }

    private ProtocolDom.Node DescribeOne(object node, int depth, bool pushed, DomReadWork work)
    {
        work.Step();
        var tree = node as NativeNode;
        var element = node as Element;
        var parentId = tree?.ParentNode is { } parent ? Tracker.KnownIdOf(parent) : 0;
        var document = node as Document;
        var dom = Runtime(node).Dom;
        return new ProtocolDom.Node
        {
            NodeId = pushed ? Push(node) : Tracker.KnownIdOf(node),
            ParentId = parentId == 0 ? null : parentId,
            BackendNodeId = Tracker.BackendIdOf(node),
            NodeType = node is Attr ? 2 : (int) tree!.NodeType,
            NodeName = DomNodeMembers.Name(node),
            LocalName = element?.LocalName ?? (node as Attr)?.LocalName ?? "",
            NodeValue = DomNodeMembers.Value(node) ?? "",
            ChildNodeCount = tree?.ChildCount ?? 0,
            Children = depth != 0 && tree is { ChildCount: > 0 } ? new ProtocolDom.Node[tree.ChildCount] : null,
            ShadowRoots = element?.AttachedShadowRoot is not null ? new ProtocolDom.Node[1] : null,
            Attributes = element is null ? null : Attributes(element, work),
            DocumentURL = document is null ? null : DomDocumentState.Of(document).Url,
            BaseURL = document is null ? null : DomDocumentState.BaseUri(document, () => dom.Engine.Constraints.Check(), dom.CancellationToken),
            FrameId = document is null ? null : _target.FrameId,
        };
    }

    /// <summary>Sends a node's children and marks them as the client's, which is <c>setChildNodes</c>.</summary>
    private void SendChildren(object node, int depth)
    {
        var work = ReadWork();
        var parentId = Push(node);
        var children = new ProtocolDom.Node[(node as NativeNode)?.ChildCount ?? 0];
        var i = 0;
        for (var child = (node as NativeNode)?.FirstChild; child is not null; child = child.NextSibling)
        {
            work.Step();
            children[i++] = Describe(child, depth < 0 ? -1 : depth - 1, pushed: true);
        }
        _childrenSent.Add(parentId);
        work.Check();
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
    private void PushAncestors(object node)
    {
        if (!IsEnabled)
        {
            return;
        }

        var work = ReadWork();
        var chain = new List<NativeNode>();
        for (var parent = (node as NativeNode)?.ParentNode; parent is not null; parent = parent.ParentNode)
        {
            work.Step();
            chain.Add(parent);
        }

        work.Check();
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
    private int Push(object node)
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
    private FlatBox? BoxOf(object node)
        => node is Element element && Runtime() is { } runtime
            ? runtime.Layout.Current().ClientBoxOf(element)
            : null;

    /// <summary>The page runtime of the document a node belongs to.</summary>
    private PageRuntime Runtime(object node)
        => Runtime() ?? Throw.ServerError<PageRuntime>("Node with given id does not belong to the document");

    /// <summary>The page runtime this target is showing, or <see langword="null"/> before its first parse.</summary>
    private PageRuntime? Runtime() => PageRuntime.Find(_target.Runtime.Engine);

    /// <summary>The document, or Chrome's own refusal when there is none yet.</summary>
    private Document Document()
        => Runtime()?.Document ?? Throw.ServerError<Document>("Document is not available");

    /// <summary>The node an identifier names, in Chrome's own wording when it names none.</summary>
    private object Resolve(int? nodeId, int? backendNodeId, string? objectId)
    {
        if (nodeId is { } id)
        {
            return RequireNodeId(id);
        }

        if (backendNodeId is { } backendId)
        {
            return Tracker.ByBackendId(backendId) ?? Throw.ServerError<object>("No node found for given backend id");
        }

        if (objectId is { Length: > 0 } handle)
        {
            return NodeOf(_objects.Table.Resolve(handle));
        }

        return Throw.ServerError<object>("Either nodeId, backendNodeId or objectId must be specified");
    }

    /// <summary>The node a <c>nodeId</c> names, in Chrome's own wording when it names none.</summary>
    private object RequireNodeId(int nodeId)
        => Tracker.ByNodeId(nodeId) ?? Throw.ServerError<object>("Could not find node with given id");

    /// <summary>The element a <c>nodeId</c> names, refusing a node that is not one.</summary>
    private Element RequireElement(int nodeId)
        => RequireNodeId(nodeId) as Element ?? Throw.ServerError<Element>("Node is not an Element");

    /// <summary>The node a handle wraps, refusing a handle that is not one.</summary>
    private static object NodeOf(JsValue value)
        => value is Dom.DomNodeObject wrapper
            ? wrapper.DomTarget
            : Throw.ServerError<object>("Object id doesn't reference a Node");

    /// <summary>The flat name/value array the protocol reports an element's attributes as.</summary>
    private Element? DocumentElement(Document document)
    {
        var work = ReadWork();
        for (var child = document.FirstChild; child is not null; child = child.NextSibling)
        {
            work.Step();
            if (child is not Element element) continue;
            work.Check();
            return element;
        }
        work.Check();
        return null;
    }

    private DomReadWork ReadWork()
    {
        var dom = Runtime()?.Dom ?? Throw.ServerError<DomRealm>("Document is not available");
        var work = new DomReadWork(dom.NativeReadCheckpoint, dom.CancellationToken);
        work.Check();
        return work;
    }

    private string[] Attributes(Element element)
    {
        var work = ReadWork();
        var result = Attributes(element, work);
        work.Check();
        return result;
    }

    private static string[] Attributes(Element element, DomReadWork work)
    {
        var attributes = new string[element.AttributeCount * 2];
        for (uint i = 0; i < (uint) element.AttributeCount; i++)
        {
            work.Step();
            var attribute = element.GetAttributeAt(i)!;
            attributes[i * 2] = attribute.Name;
            attributes[i * 2 + 1] = attribute.Value;
        }
        return attributes;
    }

    /// <summary>Selectors use the same bounded native matcher as script queries.</summary>
    private IReadOnlyList<Element> Query(object node, string selector)
    {
        if (node is not (Element or Document or DocumentFragment) || selector.Length == 0) return [];
        try
        {
            return DomSelectors.QuerySelectorAll(Runtime(node).Dom, (NativeNode) node, selector);
        }
        catch (DomException exception) when (exception.Name == "SyntaxError")
        {
            return [];
        }
    }

    /// <summary>XPath shares script's native navigator, including Attr reference identity.</summary>
    private List<object> XPathMatches(Document document, string query)
    {
        if (query.Length == 0 || DocumentElement(document) is null) return [];
        var dom = Runtime(document).Dom;
        var navigator = new BrowserXPathNavigator(dom, new DomNodeIdentity(document));
        try
        {
            var expression = NativeXPath.Compile(query, null, (_, _) => dom.Engine.Constraints.Check(), dom.CancellationToken);
            if (navigator.Evaluate(expression.ClonePrepared()) is not XPathNodeIterator nodes) return [];
            var found = new List<object>();
            while (true)
            {
                navigator.CheckRead();
                if (!nodes.MoveNext()) break;
                navigator.CheckRead();
                if (nodes.Current?.UnderlyingObject is NativeNode or Attr)
                {
                    navigator.ResultWork();
                    found.Add(nodes.Current.UnderlyingObject);
                }
            }
            navigator.PublishResult();
            return found;
        }
        catch (XPathException)
        {
            return [];
        }
    }

    /// <summary>The ordinary native light tree, with bounded ordinal case-insensitive comparisons.</summary>
    private List<object> TextMatches(Document document, string query)
    {
        var found = new List<object>();
        if (query.Length == 0) return found;
        var work = ReadWork();
        for (NativeNode? node = document; node is not null;)
        {
            work.Step();
            if (node is Text text && Contains(text.Data, query, work)) found.Add(node);
            else if (node is Element element)
            {
                for (uint i = 0; i < (uint) element.AttributeCount; i++)
                {
                    work.Step();
                    var attribute = element.GetAttributeAt(i)!;
                    if (!Contains(attribute.Value, query, work) && !Contains(attribute.Name, query, work)) continue;
                    found.Add(element);
                    break;
                }
            }
            if (node.FirstChild is { } child) { node = child; continue; }
            while (!ReferenceEquals(node, document) && node.NextSibling is null)
            {
                work.Step();
                node = node.ParentNode!;
            }
            node = ReferenceEquals(node, document) ? null : node.NextSibling;
        }
        work.Check();
        return found;
    }

    private static bool Contains(string text, string query, DomReadWork work)
    {
        for (var start = 0; start <= text.Length - query.Length; start++)
        {
            work.Step();
            var matched = true;
            for (var offset = 0; offset < query.Length;)
            {
                var count = Math.Min(64, query.Length - offset);
                // Keep surrogate pairs together so chunking preserves ordinal ignore-case semantics.
                if (offset + count < query.Length && (char.IsHighSurrogate(query[offset + count - 1])
                    || char.IsHighSurrogate(text[start + offset + count - 1]))) count++;
                for (var i = 0; i < count; i++) work.Step();
                if (!text.AsSpan(start + offset, count).Equals(query.AsSpan(offset, count), StringComparison.OrdinalIgnoreCase))
                {
                    matched = false;
                    break;
                }
                offset += count;
            }
            if (matched) return true;
        }
        return false;
    }

    /// <summary>The depth a command asked for, which the protocol defaults to one.</summary>
    private static int Depth(int? depth) => depth ?? 1;
}
