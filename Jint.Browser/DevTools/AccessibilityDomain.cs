using System.Text.Json;
using AngleSharp.Dom;
using Jint.Browser.Accessibility;
using Jint.Browser.Runtime;
using Jint.DevTools;
using Jint.DevTools.Domains;
using Jint.DevTools.Protocol;
using Jint.DevTools.Protocol.Accessibility;
using Jint.DevTools.Session;
using Jint.Native;
using ProtocolAccessibility = Jint.DevTools.Protocol.Accessibility;

namespace Jint.Browser.DevTools;

/// <summary>
/// The <c>Accessibility</c> domain: the tree a screen reader would walk, over a document nobody rendered.
/// </summary>
/// <remarks>
/// <para>
/// <b>The tree is <c>Jint.Browser/Accessibility</c>'s and this only publishes it.</b> The roles are
/// HTML-AAM's mapping table, the names accname 1.2's algorithm and the hidden verdict the CSS cascade's, all
/// computed over AngleSharp's DOM with no engine and no layout — so what is here is the protocol vocabulary
/// on top: Chrome's <c>AXNode</c> shape, its <c>AXValue</c> types, and the <c>backendDOMNodeId</c> that ties
/// every node back to the one the <c>DOM</c> domain addresses.
/// </para>
/// <para>
/// <b>This is what an ARIA query is answered from.</b> Puppeteer's <c>page.accessibility.snapshot()</c> reads
/// <c>getFullAXTree</c>, its <c>aria/</c> selector engine sends <c>queryAXTree</c>, and Playwright's
/// <c>getByRole</c> is the same question — so a role or a name computed differently here is a selector that
/// finds nothing, which is why the computation has golden files of its own rather than only these commands.
/// </para>
/// <para>
/// <b>The tree is computed per request and never maintained</b>, which is why <c>loadComplete</c> and
/// <c>nodesUpdated</c> are not emitted: a client is told the truth at the moment it asks, and an event
/// stream would promise that the answer is being watched. It costs one walk of the document per command, and
/// the walk is linear.
/// </para>
/// <para>
/// <b>An <c>AXNodeId</c> is a document's.</b> It is stable while the document and the element live — two
/// calls on an unchanged document produce the same identifiers — and it is thrown away with the document,
/// exactly as a <c>nodeId</c> is. A client holding one from the document before is told the node was not
/// found.
/// </para>
/// <para>
/// Every command runs on the page loop, so the document is read on the one thread allowed to touch it.
/// </para>
/// <para>
/// See <see href="https://chromedevtools.github.io/devtools-protocol/tot/Accessibility/"/>.
/// </para>
/// </remarks>
internal sealed class AccessibilityDomain : AccessibilityDomainBase
{
    private static readonly JsonElement _true = Primitive(true);
    private static readonly JsonElement _false = Primitive(false);

    private readonly PageTarget _target;
    private readonly RemoteObjectMapper _objects;

    internal AccessibilityDomain(PageTarget target)
    {
        _target = target;
        _objects = new RemoteObjectMapper(target, this);
    }

    private DomNodeTracker Tracker => _target.Nodes;

    /// <inheritdoc/>
    protected override async ValueTask<EmptyResult> EnableAsync(EmptyParameters parameters, CommandContext context)
    {
        await MarkEnabledAsync(context).ConfigureAwait(false);
        return EmptyResult.Instance;
    }

    /// <inheritdoc/>
    protected override async ValueTask<EmptyResult> DisableAsync(EmptyParameters parameters, CommandContext context)
    {
        await MarkDisabledAsync(context).ConfigureAwait(false);
        return EmptyResult.Instance;
    }

    /// <summary>
    /// https://chromedevtools.github.io/devtools-protocol/tot/Accessibility/#method-getFullAXTree — the whole
    /// tree, flattened, ignored nodes included.
    /// </summary>
    /// <remarks>
    /// <c>depth</c> is honoured by not descending past it, and a node at the limit is reported with an
    /// <i>empty</i> <c>childIds</c> rather than one naming children this reply does not carry —
    /// <c>getChildAXNodes</c> is the way on. Naming them would be worse than saying nothing: a client builds
    /// its own tree by resolving every identifier it is given.
    /// </remarks>
    protected override ValueTask<GetFullAXTreeResponse> GetFullAXTreeAsync(GetFullAXTreeRequest parameters, CommandContext context)
    {
        RequireFrame(parameters.FrameId);

        var root = Tree();
        var nodes = new List<ProtocolAccessibility.AXNode>();
        Collect(root, nodes, Depth(parameters.Depth), root);

        return new ValueTask<GetFullAXTreeResponse>(new GetFullAXTreeResponse { Nodes = [.. nodes] });
    }

    /// <summary>
    /// https://chromedevtools.github.io/devtools-protocol/tot/Accessibility/#method-getRootAXNode — the
    /// document's own node and nothing under it.
    /// </summary>
    protected override ValueTask<GetRootAXNodeResponse> GetRootAXNodeAsync(GetRootAXNodeRequest parameters, CommandContext context)
    {
        RequireFrame(parameters.FrameId);

        var root = Tree();
        return new ValueTask<GetRootAXNodeResponse>(new GetRootAXNodeResponse { Node = Describe(root, root) });
    }

    /// <summary>
    /// https://chromedevtools.github.io/devtools-protocol/tot/Accessibility/#method-getChildAXNodes — one
    /// level below the node an identifier names.
    /// </summary>
    protected override ValueTask<GetChildAXNodesResponse> GetChildAXNodesAsync(GetChildAXNodesRequest parameters, CommandContext context)
    {
        RequireFrame(parameters.FrameId);

        var root = Tree();
        var node = Find(root, parameters.Id) ?? Throw.ServerError<AxNode>("Could not find node with given id");

        return new ValueTask<GetChildAXNodesResponse>(new GetChildAXNodesResponse
        {
            Nodes = [.. node.Children.Select(child => Describe(child, root))],
        });
    }

    /// <summary>
    /// https://chromedevtools.github.io/devtools-protocol/tot/Accessibility/#method-getPartialAXTree — the
    /// node a DOM identifier names, and what is around it.
    /// </summary>
    /// <remarks>
    /// <c>fetchRelatives</c> defaults to <see langword="true"/>, and what it fetches here is the node's
    /// ancestor chain and its own children — the two a front end needs to place a node in the tree and to
    /// expand it. Chrome additionally sends the node's siblings; leaving them out costs a client one more
    /// call and never a wrong answer.
    /// </remarks>
    protected override ValueTask<GetPartialAXTreeResponse> GetPartialAXTreeAsync(GetPartialAXTreeRequest parameters, CommandContext context)
    {
        var root = Tree();
        var node = For(root, Resolve(parameters.NodeId, parameters.BackendNodeId, parameters.ObjectId));
        var nodes = new List<ProtocolAccessibility.AXNode>();

        if (parameters.FetchRelatives != false)
        {
            Ancestors(node, nodes, root);
        }

        nodes.Add(Describe(node, root));

        if (parameters.FetchRelatives != false)
        {
            foreach (var child in node.Children)
            {
                nodes.Add(Describe(child, root));
            }
        }

        return new ValueTask<GetPartialAXTreeResponse>(new GetPartialAXTreeResponse { Nodes = [.. nodes] });
    }

    /// <summary>
    /// https://chromedevtools.github.io/devtools-protocol/tot/Accessibility/#method-getAXNodeAndAncestors —
    /// the chain from the root down to one node.
    /// </summary>
    protected override ValueTask<GetAXNodeAndAncestorsResponse> GetAXNodeAndAncestorsAsync(GetAXNodeAndAncestorsRequest parameters, CommandContext context)
    {
        var root = Tree();
        var node = For(root, Resolve(parameters.NodeId, parameters.BackendNodeId, parameters.ObjectId));
        var nodes = new List<ProtocolAccessibility.AXNode>();

        Ancestors(node, nodes, root);
        nodes.Add(Describe(node, root));

        return new ValueTask<GetAXNodeAndAncestorsResponse>(new GetAXNodeAndAncestorsResponse { Nodes = [.. nodes] });
    }

    /// <summary>
    /// https://chromedevtools.github.io/devtools-protocol/tot/Accessibility/#method-queryAXTree — every node
    /// of a subtree whose computed name and role match.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is what an <c>aria/</c> selector is answered from, and it is matched Chrome's way: exact and
    /// case-sensitive, a missing criterion matching everything, and a subtree defaulting to the whole
    /// document when no DOM node is named. An ignored node never matches, because a client asking for a role
    /// is asking what an assistive technology would find.
    /// </para>
    /// <para>
    /// A name is compared after accname's own flattening, which is what the tree already stores — so
    /// <c>queryAXTree({ accessibleName: "Save changes" })</c> matches a button whose label is spread over
    /// three elements and several line breaks.
    /// </para>
    /// </remarks>
    protected override ValueTask<QueryAXTreeResponse> QueryAXTreeAsync(QueryAXTreeRequest parameters, CommandContext context)
    {
        var root = Tree();
        var subtree = parameters.NodeId is null && parameters.BackendNodeId is null && parameters.ObjectId is null
            ? root
            : For(root, Resolve(parameters.NodeId, parameters.BackendNodeId, parameters.ObjectId));

        var found = new List<ProtocolAccessibility.AXNode>();
        Match(subtree, parameters.AccessibleName, parameters.Role, found, root);

        return new ValueTask<QueryAXTreeResponse>(new QueryAXTreeResponse { Nodes = [.. found] });
    }

    private void Match(AxNode node, string? name, string? role, List<ProtocolAccessibility.AXNode> into, AxNode root)
    {
        var matches = !node.Ignored
            && (name is null || string.Equals(node.Name ?? "", name, StringComparison.Ordinal))
            && (role is null || string.Equals(node.Role, role, StringComparison.Ordinal));

        if (matches)
        {
            into.Add(Describe(node, root));
        }

        foreach (var child in node.Children)
        {
            Match(child, name, role, into, root);
        }
    }

    private void Ancestors(AxNode node, List<ProtocolAccessibility.AXNode> into, AxNode root)
    {
        if (node.Parent is not { } parent)
        {
            return;
        }

        Ancestors(parent, into, root);
        into.Add(Describe(parent, root));
    }

    private void Collect(AxNode node, List<ProtocolAccessibility.AXNode> into, int depth, AxNode root)
    {
        into.Add(Describe(node, root, withChildren: depth > 0));

        if (depth <= 0)
        {
            return;
        }

        foreach (var child in node.Children)
        {
            Collect(child, into, depth - 1, root);
        }
    }

    /// <summary>The accessibility tree of the document being shown, computed now.</summary>
    /// <remarks>
    /// <see cref="AccessibilityOptions.Full"/>, because the protocol's tree is the whole one: an ignored node
    /// is reported with <c>ignored: true</c> and its reasons rather than left out, which is what lets a front
    /// end explain why an element is invisible to a screen reader.
    /// </remarks>
    private AxNode Tree()
    {
        var document = PageRuntime.Find(_target.Runtime.Engine)?.Document
            ?? Throw.ServerError<IDocument>("Document is not available");

        return AccessibilityTree.Build(document, AccessibilityOptions.Full);
    }

    /// <summary>The node of the tree an identifier names, or <see langword="null"/>.</summary>
    private static AxNode? Find(AxNode node, string id)
    {
        if (string.Equals(node.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), id, StringComparison.Ordinal))
        {
            return node;
        }

        foreach (var child in node.Children)
        {
            if (Find(child, id) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>The accessibility node computed for one DOM node, in Chrome's wording when there is none.</summary>
    /// <remarks>
    /// A DOM node with no accessibility node of its own — a comment, an attribute, a node in a subtree the
    /// walk pruned — is refused rather than answered with the nearest one, because a client that asked about
    /// an element and was told about its parent would place it wrongly in its own tree.
    /// </remarks>
    private static AxNode For(AxNode root, INode target)
        => Locate(root, target) ?? Throw.ServerError<AxNode>("No node with given id found");

    private static AxNode? Locate(AxNode node, INode target)
    {
        if (ReferenceEquals(node.Node, target))
        {
            return node;
        }

        foreach (var child in node.Children)
        {
            if (Locate(child, target) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>The node an identifier names, in Chrome's own wording when it names none.</summary>
    private INode Resolve(int? nodeId, int? backendNodeId, string? objectId)
    {
        if (nodeId is { } id)
        {
            return Tracker.ByNodeId(id) ?? Throw.ServerError<INode>("Could not find node with given id");
        }

        if (backendNodeId is { } backendId)
        {
            return Tracker.ByBackendId(backendId) ?? Throw.ServerError<INode>("No node found for given backend id");
        }

        if (objectId is { Length: > 0 } handle)
        {
            return _objects.Table.Resolve(handle) is Dom.DomNodeObject wrapper
                ? wrapper.Node
                : Throw.ServerError<INode>("Object id doesn't reference a Node");
        }

        return Throw.ServerError<INode>("Either nodeId, backendNodeId or objectId must be specified");
    }

    /// <summary>Refuses a frame identifier that is not this page's, in Chrome's own wording.</summary>
    private void RequireFrame(string? frameId)
    {
        if (frameId is { Length: > 0 } && !string.Equals(frameId, _target.FrameId, StringComparison.Ordinal))
        {
            Throw.ServerError("Frame with the given id was not found.");
        }
    }

    /// <summary>How deep <c>getFullAXTree</c> descends; the protocol's own "omitted means all".</summary>
    private static int Depth(int? depth) => depth is { } value && value >= 0 ? value : int.MaxValue;

    /// <summary>One node in the protocol's shape, with the identifiers only this session can supply.</summary>
    private ProtocolAccessibility.AXNode Describe(AxNode node, AxNode root, bool withChildren = true)
    {
        var described = AccessibilityTree.ToProtocol(node);

        return new ProtocolAccessibility.AXNode
        {
            NodeId = described.NodeId,
            Ignored = described.Ignored,
            IgnoredReasons = Properties(described.IgnoredReasons),
            Role = Value(described.Role),
            Name = Value(described.Name),
            Description = Value(described.Description),
            Value = Value(described.Value),
            Properties = Properties(described.Properties),
            ParentId = described.ParentId,

            // Always an array, never absent, and never naming a node this reply does not carry. Chrome sends
            // one on every node it reports and a client walks it without asking whether it is there —
            // PuppeteerSharp's own tree builder dereferences it — so a leaf is an empty array and a node the
            // depth limit stopped at is an empty array too, with getChildAXNodes as the way on.
            ChildIds = withChildren ? described.ChildIds?.ToArray() ?? [] : [],

            // The tie back to the DOM domain: a client that walked the accessibility tree can turn any node
            // of it into a node it can describe, measure and click, and the identifier is the same one
            // DOM.describeNode reports. It is minted here, which is what makes it resolvable afterwards.
            BackendDOMNodeId = node.Node is { } dom ? Tracker.BackendIdOf(dom) : null,

            // Chrome puts the frame on the root of each document's tree and on nothing else; there is one
            // scripted frame per page here, and its identifier is the target's.
            FrameId = ReferenceEquals(node, root) ? _target.FrameId : null,
        };
    }

    private static ProtocolAccessibility.AXProperty[]? Properties(IReadOnlyList<AxProtocolProperty>? properties)
    {
        if (properties is not { Count: > 0 })
        {
            return null;
        }

        var mapped = new ProtocolAccessibility.AXProperty[properties.Count];
        for (var i = 0; i < properties.Count; i++)
        {
            mapped[i] = new ProtocolAccessibility.AXProperty
            {
                Name = properties[i].Name,
                Value = Value(properties[i].Value)!,
            };
        }

        return mapped;
    }

    /// <summary>
    /// One <c>AXValue</c>, whose <c>value</c> the protocol declares as <c>any</c> and a front end reads as
    /// its <c>type</c> says.
    /// </summary>
    /// <remarks>
    /// A boolean is written as a JSON boolean and a number as a JSON number, never as their text — which is
    /// what <c>Accessibility/AxProtocolNode</c>'s own converter already does for the snapshot path, and the
    /// reason this maps rather than duplicating the computation.
    /// </remarks>
    private static ProtocolAccessibility.AXValue? Value(AxProtocolValue? value)
    {
        if (value is null)
        {
            return null;
        }

        var inner = value.Value;

        return new ProtocolAccessibility.AXValue
        {
            Type = value.Type,
            Value = inner.Type switch
            {
                AxValueType.Boolean => (inner.Flag ?? false) ? _true : _false,
                AxValueType.Integer or AxValueType.Number => Primitive(inner.Numeric ?? 0),
                _ => inner.Text is { } text ? Primitive(text) : null,
            },
        };
    }

    private static JsonElement Primitive(bool value)
        => JsonSerializer.SerializeToElement(value, AxProtocolJsonContext.Default.Boolean);

    private static JsonElement Primitive(double value)
        => JsonSerializer.SerializeToElement(value, AxProtocolJsonContext.Default.Double);

    private static JsonElement Primitive(string value)
        => JsonSerializer.SerializeToElement(value, AxProtocolJsonContext.Default.String);
}
