using System.Globalization;
using AngleSharp.Dom;
using Jint.Browser.Events;
using Jint.Browser.Layout;
using Jint.Browser.Runtime;
using Jint.DevTools;
using Jint.DevTools.Domains;
using Jint.DevTools.Protocol;
using Jint.DevTools.Protocol.DOM;
using Jint.DevTools.Session;
using Jint.Native;
using ProtocolDom = Jint.DevTools.Protocol.DOM;

namespace Jint.Browser.DevTools;

/// <summary>
/// The <c>DOM</c> domain: the document a client walks, the nodes it addresses, and the boxes it clicks.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every command here runs on the page loop</b>, brought there by the target's mailbox, so the document
/// is read and written on the one thread allowed to touch it and nothing crosses back but strings.
/// </para>
/// <para>
/// <b>The identifiers are <see cref="DomNodeTracker"/>'s and the "sent" set is this attachment's.</b> Two
/// clients attached to one page address a node by the same <c>nodeId</c>; which of them hears about that
/// node changing is decided here, by whether it has been sent the node — Chrome's own rule, and the reason
/// a client that never called <c>getDocument</c> gets no mutation events at all rather than a flood about a
/// tree it has never seen.
/// </para>
/// <para>
/// <b>Every box comes from <see cref="FlatLayout"/></b>, the same model
/// <c>Element.getBoundingClientRect</c> and <c>Input.dispatchMouseEvent</c> use — so a client that reads a
/// box with <c>getBoxModel</c>, clicks its centre and asks <c>getNodeForLocation</c> what it hit is told one
/// consistent story. A node with no box is refused in Chrome's own words rather than answered with zeros,
/// because a client reads zeros as a real box at the origin.
/// </para>
/// <para>
/// See <see href="https://chromedevtools.github.io/devtools-protocol/tot/DOM/"/>.
/// </para>
/// </remarks>
internal sealed partial class DomDomain : DOMDomainBase, IDetachableDomain, ITargetObserver
{
    private readonly PageTarget _target;
    private readonly RemoteObjectMapper _objects;
    private readonly HashSet<int> _sent = [];
    private readonly HashSet<int> _childrenSent = [];
    private readonly Dictionary<string, int[]> _searches = new(StringComparer.Ordinal);

    private int _nextSearch;

    internal DomDomain(PageTarget target)
    {
        _target = target;
        _objects = new RemoteObjectMapper(target, this);
    }

    private DomNodeTracker Tracker => _target.Nodes;

    /// <inheritdoc/>
    protected override async ValueTask<EmptyResult> EnableAsync(ProtocolDom.EnableRequest parameters, CommandContext context)
    {
        // includeWhitespace is accepted and ignored: this domain reports every child node a document has,
        // which is a superset of either mode the parameter selects between.
        await MarkEnabledAsync(context).ConfigureAwait(false);

        if (Runtime() is { } runtime)
        {
            Tracker.Watch(runtime);
        }

        return EmptyResult.Instance;
    }

    /// <inheritdoc/>
    protected override async ValueTask<EmptyResult> DisableAsync(EmptyParameters parameters, CommandContext context)
    {
        await MarkDisabledAsync(context).ConfigureAwait(false);
        Forget();
        return EmptyResult.Instance;
    }

    /// <inheritdoc/>
    void IDetachableDomain.Detach() => Tracker.Remove(this);

    /// <inheritdoc/>
    /// <remarks>
    /// A navigation throws every <c>nodeId</c> away with the document that minted it, so the client is told
    /// to start again — which is exactly what <c>documentUpdated</c> means. The tracker's own tables are
    /// cleared by the target, once, rather than by each attachment.
    /// </remarks>
    void ITargetObserver.RuntimeReplaced(TargetRuntime runtime)
    {
        Forget();

        if (IsEnabled)
        {
            EmitDetached(DOMEvents.DocumentUpdated());
        }
    }

    /// <summary>
    /// https://chromedevtools.github.io/devtools-protocol/tot/DOM/#method-getDocument — the document, and as
    /// much of the tree below it as the client asked for.
    /// </summary>
    protected override ValueTask<GetDocumentResponse> GetDocumentAsync(GetDocumentRequest parameters, CommandContext context)
    {
        var document = Document();
        var root = Describe(document, Depth(parameters.Depth), pushed: true);

        return new ValueTask<GetDocumentResponse>(new GetDocumentResponse { Root = root });
    }

    /// <summary>
    /// https://chromedevtools.github.io/devtools-protocol/tot/DOM/#method-requestChildNodes — pushes a
    /// node's children and answers nothing; the children arrive as <c>setChildNodes</c>.
    /// </summary>
    protected override ValueTask<EmptyResult> RequestChildNodesAsync(RequestChildNodesRequest parameters, CommandContext context)
    {
        var node = RequireNodeId(parameters.NodeId);
        SendChildren(node, Depth(parameters.Depth));
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <summary>
    /// https://chromedevtools.github.io/devtools-protocol/tot/DOM/#method-describeNode — what a node is,
    /// without pushing it.
    /// </summary>
    /// <remarks>
    /// <b>Describing is not sending.</b> Chrome answers <c>nodeId: 0</c> for a node the client has not been
    /// pushed, and every client copes with it — Puppeteer reads the <c>backendNodeId</c> and the
    /// <c>frameId</c> off this and nothing else. Minting an identifier here instead would grow a node table
    /// for a client that only ever describes.
    /// </remarks>
    protected override ValueTask<DescribeNodeResponse> DescribeNodeAsync(DescribeNodeRequest parameters, CommandContext context)
    {
        var node = Resolve(parameters.NodeId, parameters.BackendNodeId, parameters.ObjectId);
        var described = Describe(node, Depth(parameters.Depth), pushed: false);

        return new ValueTask<DescribeNodeResponse>(new DescribeNodeResponse { Node = described });
    }

    /// <summary>
    /// https://chromedevtools.github.io/devtools-protocol/tot/DOM/#method-requestNode — pushes the node a
    /// handle names and answers the identifier it is addressed by from now on.
    /// </summary>
    protected override ValueTask<RequestNodeResponse> RequestNodeAsync(RequestNodeRequest parameters, CommandContext context)
    {
        var node = NodeOf(_objects.Table.Resolve(parameters.ObjectId));
        var nodeId = Tracker.IdOf(node);

        // Chrome pushes the ancestor chain so that the front end can place the node in a tree it already
        // holds; a client that has never called getDocument simply gets the identifier.
        PushAncestors(node);
        _sent.Add(nodeId);

        return new ValueTask<RequestNodeResponse>(new RequestNodeResponse { NodeId = nodeId });
    }

    /// <summary>
    /// https://chromedevtools.github.io/devtools-protocol/tot/DOM/#method-resolveNode — the handle a client
    /// evaluates against, for a node it holds an identifier for.
    /// </summary>
    protected override ValueTask<ResolveNodeResponse> ResolveNodeAsync(ResolveNodeRequest parameters, CommandContext context)
    {
        var node = Resolve(parameters.NodeId, parameters.BackendNodeId, objectId: null);
        var value = Runtime(node).Dom.WrapNode(node);
        var request = RemoteObjectRequest.From(byValue: false, generatePreview: false, parameters.ObjectGroup);

        return new ValueTask<ResolveNodeResponse>(new ResolveNodeResponse { Object = _objects.Describe(value, request) });
    }

    /// <inheritdoc/>
    protected override ValueTask<QuerySelectorResponse> QuerySelectorAsync(QuerySelectorRequest parameters, CommandContext context)
    {
        var node = RequireNodeId(parameters.NodeId);
        var match = Query(node, parameters.Selector).FirstOrDefault();

        return new ValueTask<QuerySelectorResponse>(new QuerySelectorResponse { NodeId = match is null ? 0 : Push(match) });
    }

    /// <inheritdoc/>
    protected override ValueTask<QuerySelectorAllResponse> QuerySelectorAllAsync(QuerySelectorAllRequest parameters, CommandContext context)
    {
        var node = RequireNodeId(parameters.NodeId);
        var matches = Query(node, parameters.Selector).Select(Push).ToArray();

        return new ValueTask<QuerySelectorAllResponse>(new QuerySelectorAllResponse { NodeIds = matches });
    }

    /// <inheritdoc/>
    protected override ValueTask<GetOuterHTMLResponse> GetOuterHTMLAsync(GetOuterHTMLRequest parameters, CommandContext context)
    {
        var node = Resolve(parameters.NodeId, parameters.BackendNodeId, parameters.ObjectId);

        var markup = node switch
        {
            IElement element => element.OuterHtml,
            IDocument document => document.DocumentElement?.OuterHtml ?? "",
            _ => node.NodeValue ?? "",
        };

        return new ValueTask<GetOuterHTMLResponse>(new GetOuterHTMLResponse { OuterHTML = markup });
    }

    /// <summary>
    /// https://chromedevtools.github.io/devtools-protocol/tot/DOM/#method-setOuterHTML — replaces a node
    /// with the markup a client sends.
    /// </summary>
    /// <remarks>
    /// It goes through <c>DomHostHooks.SetOuterHtml</c>, which is the seam the parser driver owns, so a
    /// <c>&lt;script&gt;</c> in the markup is an element with text and does not run — HTML's own rule for
    /// markup parsed into a tree rather than by the parser, and the same answer a page's own
    /// <c>outerHTML = …</c> gets.
    /// </remarks>
    protected override ValueTask<EmptyResult> SetOuterHTMLAsync(SetOuterHTMLRequest parameters, CommandContext context)
    {
        var node = RequireNodeId(parameters.NodeId);
        if (node is not IElement element)
        {
            return Throw.ServerError<ValueTask<EmptyResult>>("Node is not an Element");
        }

        var runtime = Runtime(node);
        runtime.Dom.Hooks.SetOuterHtml(runtime.Dom, element, parameters.OuterHTML);
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <inheritdoc/>
    protected override ValueTask<GetAttributesResponse> GetAttributesAsync(GetAttributesRequest parameters, CommandContext context)
    {
        var element = RequireElement(parameters.NodeId);
        return new ValueTask<GetAttributesResponse>(new GetAttributesResponse { Attributes = Attributes(element) });
    }

    /// <inheritdoc/>
    protected override ValueTask<EmptyResult> SetAttributeValueAsync(SetAttributeValueRequest parameters, CommandContext context)
    {
        RequireElement(parameters.NodeId).SetAttribute(parameters.Name, parameters.Value);
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <summary>
    /// https://chromedevtools.github.io/devtools-protocol/tot/DOM/#method-setAttributesAsText — the front
    /// end's own attribute editor, which sends a fragment of markup rather than a name and a value.
    /// </summary>
    /// <remarks>
    /// The text is parsed by giving it to a throwaway element, which is how Chrome parses it too: an
    /// attribute's value may be quoted, unquoted or absent, and only the HTML parser knows all three. A
    /// <c>name</c> the client also sent names the attribute being <i>replaced</i>, which is removed first so
    /// that renaming one is one edit rather than two.
    /// </remarks>
    protected override ValueTask<EmptyResult> SetAttributesAsTextAsync(SetAttributesAsTextRequest parameters, CommandContext context)
    {
        var element = RequireElement(parameters.NodeId);

        if (parameters.Name is { Length: > 0 } replaced)
        {
            element.RemoveAttribute(replaced);
        }

        var holder = element.Owner?.CreateElement("div");
        if (holder is null)
        {
            return new ValueTask<EmptyResult>(EmptyResult.Instance);
        }

        holder.InnerHtml = "<span " + parameters.Text + "></span>";

        if (holder.FirstElementChild is { } parsed)
        {
            foreach (var attribute in parsed.Attributes)
            {
                element.SetAttribute(attribute.Name, attribute.Value);
            }
        }

        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <inheritdoc/>
    protected override ValueTask<EmptyResult> RemoveAttributeAsync(RemoveAttributeRequest parameters, CommandContext context)
    {
        RequireElement(parameters.NodeId).RemoveAttribute(parameters.Name);
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <inheritdoc/>
    protected override ValueTask<EmptyResult> RemoveNodeAsync(RemoveNodeRequest parameters, CommandContext context)
    {
        var node = RequireNodeId(parameters.NodeId);

        if (node.Parent is not { } parent)
        {
            return Throw.ServerError<ValueTask<EmptyResult>>("Cannot remove detached node");
        }

        parent.RemoveChild(node);
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <inheritdoc/>
    protected override ValueTask<EmptyResult> SetNodeValueAsync(SetNodeValueRequest parameters, CommandContext context)
    {
        var node = RequireNodeId(parameters.NodeId);
        node.NodeValue = parameters.Value;
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <summary>
    /// https://chromedevtools.github.io/devtools-protocol/tot/DOM/#method-focus — R2's focus controller,
    /// with the four events HTML's order asks for.
    /// </summary>
    protected override ValueTask<EmptyResult> FocusAsync(ProtocolDom.FocusRequest parameters, CommandContext context)
    {
        var node = Resolve(parameters.NodeId, parameters.BackendNodeId, parameters.ObjectId);
        if (node is not IElement element)
        {
            return Throw.ServerError<ValueTask<EmptyResult>>("Node is not an Element");
        }

        FocusController.Focus(Runtime(node).Dom, element);
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <summary>
    /// https://chromedevtools.github.io/devtools-protocol/tot/DOM/#method-getBoxModel — the flat box, four
    /// times over.
    /// </summary>
    /// <remarks>
    /// Content, padding, border and margin are the same quad because a flat box has no padding, border or
    /// margin to tell them apart; a client that measures the difference between two of them measures zero,
    /// which is the truth here.
    /// </remarks>
    protected override ValueTask<GetBoxModelResponse> GetBoxModelAsync(GetBoxModelRequest parameters, CommandContext context)
    {
        var node = Resolve(parameters.NodeId, parameters.BackendNodeId, parameters.ObjectId);
        var box = BoxOf(node) ?? Throw.ServerError<FlatBox>("Could not compute box model.");
        var quad = box.ToQuad();

        return new ValueTask<GetBoxModelResponse>(new GetBoxModelResponse
        {
            Model = new BoxModel
            {
                Content = quad,
                Padding = quad,
                Border = quad,
                Margin = quad,
                Width = (int) box.Width,
                Height = (int) box.Height,
            },
        });
    }

    /// <summary>
    /// https://chromedevtools.github.io/devtools-protocol/tot/DOM/#method-getContentQuads — one quad, which
    /// is Playwright's own way of finding a point to click.
    /// </summary>
    protected override ValueTask<GetContentQuadsResponse> GetContentQuadsAsync(GetContentQuadsRequest parameters, CommandContext context)
    {
        var node = Resolve(parameters.NodeId, parameters.BackendNodeId, parameters.ObjectId);
        var box = BoxOf(node) ?? Throw.ServerError<FlatBox>("Could not compute content quads.");

        return new ValueTask<GetContentQuadsResponse>(new GetContentQuadsResponse { Quads = [box.ToQuad()] });
    }

    /// <summary>
    /// https://chromedevtools.github.io/devtools-protocol/tot/DOM/#method-getNodeForLocation — the hit test,
    /// which is the same one a mouse event at that point goes through.
    /// </summary>
    protected override ValueTask<GetNodeForLocationResponse> GetNodeForLocationAsync(GetNodeForLocationRequest parameters, CommandContext context)
    {
        var runtime = Runtime() ?? Throw.ServerError<PageRuntime>("No node found at given location");

        if (runtime.Layout.Current().ElementFromPoint(parameters.X, parameters.Y) is not { } hit)
        {
            return Throw.ServerError<ValueTask<GetNodeForLocationResponse>>("No node found at given location");
        }

        // Chrome sends nodeId only for a node the client has already been pushed, and leaves it out otherwise.
        var known = Tracker.KnownIdOf(hit);

        return new ValueTask<GetNodeForLocationResponse>(new GetNodeForLocationResponse
        {
            BackendNodeId = Tracker.BackendIdOf(hit),
            FrameId = _target.FrameId,
            NodeId = known == 0 ? null : known,
        });
    }

    /// <summary>
    /// https://chromedevtools.github.io/devtools-protocol/tot/DOM/#method-scrollIntoViewIfNeeded — the
    /// virtual scroll, with the alignment a client's "if needed" means.
    /// </summary>
    /// <remarks>
    /// <c>rect</c> is accepted and ignored: it names a rectangle <i>inside</i> the node to reveal, and a flat
    /// box has no interior to align.
    /// </remarks>
    protected override ValueTask<EmptyResult> ScrollIntoViewIfNeededAsync(ScrollIntoViewIfNeededRequest parameters, CommandContext context)
    {
        var node = Resolve(parameters.NodeId, parameters.BackendNodeId, parameters.ObjectId);

        if (node is not IElement element || BoxOf(node) is null)
        {
            return Throw.ServerError<ValueTask<EmptyResult>>("Node does not have a layout object");
        }

        Runtime(node).Layout.ScrollIntoView(element, "nearest");
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <summary>
    /// https://chromedevtools.github.io/devtools-protocol/tot/DOM/#method-performSearch — a selector, then
    /// a text match.
    /// </summary>
    /// <remarks>
    /// <b>Chrome's three arms, in Chrome's order.</b> The query is tried as a selector first, so
    /// <c>performSearch("div")</c> finds elements rather than the word; then as an XPath expression, which is
    /// what a front end's search box sends for anything beginning <c>//</c> or <c>(</c>; then as a
    /// case-insensitive substring of a text node's data or of an attribute's value. A query that is not one
    /// of the three simply contributes nothing rather than failing the command — a search box is allowed to
    /// be typed into one character at a time.
    /// </remarks>
    protected override ValueTask<PerformSearchResponse> PerformSearchAsync(PerformSearchRequest parameters, CommandContext context)
    {
        var document = Document();
        var found = new List<INode>();
        var seen = new HashSet<INode>(ReferenceEqualityComparer.Instance);

        foreach (var element in Query(document, parameters.Query))
        {
            if (seen.Add(element))
            {
                found.Add(element);
            }
        }

        foreach (var node in XPathMatches(document, parameters.Query))
        {
            if (seen.Add(node))
            {
                found.Add(node);
            }
        }

        foreach (var node in TextMatches(document, parameters.Query))
        {
            if (seen.Add(node))
            {
                found.Add(node);
            }
        }

        var searchId = "search-" + (++_nextSearch).ToString(CultureInfo.InvariantCulture);
        _searches[searchId] = [.. found.Select(Tracker.IdOf)];

        return new ValueTask<PerformSearchResponse>(new PerformSearchResponse
        {
            SearchId = searchId,
            ResultCount = found.Count,
        });
    }

    /// <inheritdoc/>
    protected override ValueTask<GetSearchResultsResponse> GetSearchResultsAsync(GetSearchResultsRequest parameters, CommandContext context)
    {
        if (!_searches.TryGetValue(parameters.SearchId, out var results))
        {
            Throw.ServerError("No search session with given id found");
        }

        var from = Math.Clamp(parameters.FromIndex, 0, results!.Length);
        var to = Math.Clamp(parameters.ToIndex, from, results.Length);
        var slice = results[from..to];

        foreach (var nodeId in slice)
        {
            if (Tracker.ByNodeId(nodeId) is { } node)
            {
                PushAncestors(node);
                _sent.Add(nodeId);
            }
        }

        return new ValueTask<GetSearchResultsResponse>(new GetSearchResultsResponse { NodeIds = slice });
    }

    /// <inheritdoc/>
    protected override ValueTask<EmptyResult> DiscardSearchResultsAsync(DiscardSearchResultsRequest parameters, CommandContext context)
    {
        _searches.Remove(parameters.SearchId);
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <summary>
    /// https://chromedevtools.github.io/devtools-protocol/tot/DOM/#method-getFrameOwner — the
    /// <c>&lt;iframe&gt;</c> a frame lives in.
    /// </summary>
    /// <remarks>
    /// The page's own frame is owned by nothing, so the only identifier this command can be sent is the one
    /// it refuses. A child frame has a document and no realm (design doc §3), so it is never scripted and has
    /// no frame identifier of its own for a client to ask about.
    /// </remarks>
    protected override ValueTask<GetFrameOwnerResponse> GetFrameOwnerAsync(GetFrameOwnerRequest parameters, CommandContext context)
    {
        if (string.Equals(parameters.FrameId, _target.FrameId, StringComparison.Ordinal))
        {
            Throw.ServerError("Frame with the given id is the main frame and has no owner element");
        }

        // Chrome's own wording, which is what a client matches on to tell a frame that went away from a
        // wrong call.
        return Throw.ServerError<ValueTask<GetFrameOwnerResponse>>("Frame with the given id was not found.");
    }

    /// <summary>Answers success and remembers nothing, because nothing here can be undone.</summary>
    /// <remarks>
    /// The front end brackets an edit with it so that its own undo stack has a boundary; <c>DOM.undo</c> and
    /// <c>DOM.redo</c> are unimplemented, so the mark is a boundary in a stack that does not exist. It is
    /// answered rather than refused because the front end sends it around every attribute edit and reads a
    /// failure as the edit failing.
    /// </remarks>
    protected override ValueTask<EmptyResult> MarkUndoableStateAsync(EmptyParameters parameters, CommandContext context)
        => new(EmptyResult.Instance);
}
