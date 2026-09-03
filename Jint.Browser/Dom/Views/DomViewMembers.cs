using System.Runtime.CompilerServices;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Jint.Browser.Observers;
using Jint.Browser.Runtime;
using Jint.Native;
using Jint.Runtime;

namespace Jint.Browser.Dom.Views;

/// <summary>
/// The bodies of the members <c>overrides.json</c>'s <c>additions</c> list adds to a generated interface.
/// </summary>
/// <remarks>
/// <para>
/// Every one of them is a member the DOM standard puts on an interface the generator emits, and that the
/// generator could not project from AngleSharp: a filter parameter is a CLR delegate, a stringifier has no
/// <c>[DomName]</c>, and one member AngleSharp simply spells by its Shadow DOM v0 name. Adding them here
/// rather than hand-writing the whole interface keeps the prototype shaped and keeps the other two hundred
/// members generated.
/// </para>
/// <para>
/// They are static and take the brand-checked receiver, exactly like a generated body, so nothing about the
/// call site differs from the member beside it.
/// </para>
/// </remarks>
internal static class DomViewMembers
{
    /// <summary>
    /// The <c>NodeFilter</c> each traversal object was created with, so that <c>walker.filter</c> answers the
    /// value the page passed rather than the delegate it was converted into.
    /// </summary>
    /// <remarks>
    /// Keyed on the AngleSharp traversal object, which belongs to one engine and one document, so the stored
    /// value can never be read by an engine it does not belong to. A <see cref="ConditionalWeakTable{TKey,TValue}"/>
    /// rather than a field on the wrapper, because the wrapper is created lazily by the cache and the filter
    /// is known here, at creation.
    /// </remarks>
    private static readonly ConditionalWeakTable<object, JsValue> _filters = new();

    /// <summary>https://dom.spec.whatwg.org/#dom-document-createtreewalker.</summary>
    internal static JsValue CreateTreeWalker(DomRealm realm, IDocument document, JsValue[] arguments)
    {
        var root = DomBindings.Argument<INode>(arguments, 0, "Document.createTreeWalker");
        var settings = WhatToShow(arguments);
        var filter = arguments.At(2);

        // DomTreeWalker rather than document.CreateTreeWalker: AngleSharp's walker does not terminate, and
        // its own file says which loop and why this package owns the algorithm. It implements ITreeWalker,
        // so DomTypeMap gives it the generated TreeWalker shape like any other walker. The `root` argument
        // is not validated beyond being a node, which is DOM's own rule — createTreeWalker takes any node.
        var walker = new DomTreeWalker(root, settings, NodeFilters.From(realm, filter, "createTreeWalker"));
        _filters.AddOrUpdate(walker, filter.IsNullOrUndefined() ? JsValue.Null : filter);
        return realm.Wrap(walker);
    }

    /// <summary>https://dom.spec.whatwg.org/#dom-document-createnodeiterator.</summary>
    internal static JsValue CreateNodeIterator(DomRealm realm, IDocument document, JsValue[] arguments)
    {
        var root = DomBindings.Argument<INode>(arguments, 0, "Document.createNodeIterator");
        var settings = WhatToShow(arguments);
        var filter = arguments.At(2);

        var iterator = document.CreateNodeIterator(root, settings, NodeFilters.From(realm, filter, "createNodeIterator")!);
        _filters.AddOrUpdate(iterator, filter.IsNullOrUndefined() ? JsValue.Null : filter);
        return realm.Wrap(iterator);
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-treewalker-filter and its <c>NodeIterator</c> twin: the value the page
    /// passed, or <see langword="null"/>.
    /// </summary>
    internal static JsValue Filter(object traversal)
        => _filters.TryGetValue(traversal, out var filter) ? filter : JsValue.Null;

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-range-stringifier — the text of every text node the range covers.
    /// </summary>
    /// <remarks>
    /// AngleSharp implements it as <c>Range.ToString()</c> and puts no <c>[DomName]</c> on it, so nothing in
    /// the metadata says it is the interface's stringifier.
    /// </remarks>
    internal static JsValue RangeToString(IRange range) => JsString.Create(range.ToString() ?? "");

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-node-isconnected — whether the node's shadow-including root is a
    /// document.
    /// </summary>
    /// <remarks>
    /// <b>AngleSharp's <c>INode</c> has no member for it at all</b>, so there is nothing to project and the
    /// walk is here. It is not a curiosity: every client library asks a node it holds whether it is still in
    /// the document before it clicks it — PuppeteerSharp's own message for a falsy answer is "Node is
    /// detached from document" — so an absent member makes every element handle look detached.
    /// </remarks>
    internal static JsValue IsConnected(INode node)
    {
        for (INode? current = node; current is not null;)
        {
            if (current is IDocument)
            {
                return JsBoolean.True;
            }

            // The shadow-including part: a node inside a shadow tree is connected when its host is, which is
            // what makes a component's own markup reachable to a client driving the page.
            current = current is IShadowRoot shadow ? shadow.Host : current.Parent;
        }

        return JsBoolean.False;
    }

    /// <summary>
    /// https://drafts.csswg.org/cssom-view/#dom-range-getboundingclientrect, at the only size a range can be
    /// told.
    /// </summary>
    /// <remarks>
    /// <b>Zeros, and still zeros with the flat box model in place.</b> That model gives every <i>element</i>
    /// a row; a range is a pair of positions inside the text of one, and nothing here measures text. A range
    /// covering half a paragraph has no honest rectangle, so it keeps the empty one.
    /// </remarks>
    internal static JsValue RangeRect(DomRealm realm) => Layout.DomRects.Zero(realm.Engine);

    /// <summary>
    /// https://drafts.csswg.org/cssom-view/#dom-range-getclientrects — empty, because a range with no layout
    /// covers no boxes.
    /// </summary>
    internal static JsValue RangeRects(DomRealm realm)
        => realm.PrincipalRealm.Intrinsics.Array.ConstructFast(System.Array.Empty<JsValue>());

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-slot-assignednodes.
    /// </summary>
    /// <remarks>
    /// AngleSharp names it <c>getDistributedNodes</c>, which is the Shadow DOM v0 spelling, and that is the
    /// name its <c>[DomName]</c> carries — so the generated interface has the old name and not the standard
    /// one. Both are present: the generated one because it is what the metadata says, this one because it is
    /// what a page calls.
    /// </remarks>
    internal static JsValue AssignedNodes(DomRealm realm, IHtmlSlotElement slot)
        => DomConvert.NodeSequence(realm, slot.GetDistributedNodes());

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-slot-assignedelements — the assigned nodes that are elements.
    /// </summary>
    internal static JsValue AssignedElements(DomRealm realm, IHtmlSlotElement slot)
        => DomConvert.NodeSequence(realm, slot.GetDistributedNodes().OfType<IElement>());

    /// <summary>https://w3c.github.io/selection-api/#dom-document-getselection.</summary>
    /// <remarks>
    /// The page's selection, whichever document the call was made on: a selection belongs to a document's
    /// browsing context, and a document a <c>DOMParser</c> produced has none. So
    /// <c>parsed.getSelection()</c> answers the page's rather than a second empty one, where a browser gives
    /// the parsed document its own. Nothing can select inside a parsed document, so the difference is what
    /// the object is rather than what it holds.
    /// </remarks>
    internal static JsValue GetSelection(DomRealm realm)
    {
        var runtime = PageRuntime.Find(realm.Engine);
        return runtime is null ? JsValue.Null : runtime.Views.Selection;
    }

    /// <summary>
    /// <c>whatToShow</c>, an <c>unsigned long</c> whose default is <c>0xFFFFFFFF</c>. It is read as a
    /// <see cref="uint"/> and widened, because <c>FilterSettings</c> is a 64-bit enum and a signed read of
    /// <c>SHOW_ALL</c> would be <c>-1</c>.
    /// </summary>
    private static FilterSettings WhatToShow(JsValue[] arguments)
        => (FilterSettings) DomConvert.OptionalUInt32(arguments, 1, uint.MaxValue);
}
