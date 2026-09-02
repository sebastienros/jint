using AngleSharp.Dom;
using Jint.Browser.Dom;
using Jint.Browser.Runtime;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Browser.Layout;

/// <summary>
/// The CSSOM View members a page reads its boxes through, answered from <see cref="FlatLayout"/>.
/// </summary>
/// <remarks>
/// <para>
/// Every one of these is an <c>additions</c> entry in <c>tools/dom-bindings/overrides.json</c>: AngleSharp
/// has no layout and therefore declares none of them, so the standard's member list is the only place they
/// can come from. The bodies are here rather than in the table because the table holds decisions.
/// </para>
/// <para>
/// <b>A binding with no page answers zeros.</b> <c>DomBindings.Install</c> on a bare engine — which is what
/// the binding tests use — has no viewport, no scroll offset and no document loop, so every metric is zero
/// and every hit test is <see langword="null"/>. That is the same shape of answer a hidden element gets, and
/// it is the honest one: there is no window for a box to be measured against.
/// </para>
/// <para>
/// <b>Only the scrolling element scrolls.</b> <c>scrollTop</c> on <c>document.scrollingElement</c> is the
/// page's virtual scroll offset and writing it scrolls the page; on anything else it reads zero and a write
/// is ignored, because no element here has content larger than its own box. <c>scrollLeft</c> is zero
/// everywhere: every box is exactly as wide as the viewport.
/// </para>
/// </remarks>
internal static class LayoutMembers
{
    /// <summary>https://drafts.csswg.org/cssom-view/#dom-element-getboundingclientrect.</summary>
    internal static JsValue BoundingClientRect(DomRealm realm, IElement element)
        => DomRects.Of(realm.Engine, ClientBox(realm, element));

    /// <summary>
    /// https://drafts.csswg.org/cssom-view/#dom-element-getclientrects — one rectangle, or none at all.
    /// </summary>
    /// <remarks>
    /// An element with no box has no client rectangles, which is what CSSOM View says and what a page
    /// testing <c>el.getClientRects().length</c> for visibility is asking.
    /// </remarks>
    internal static JsValue ClientRects(DomRealm realm, IElement element)
    {
        return Layout(realm)?.ClientBoxOf(element) is { } box
            ? DomRects.List(realm, DomRects.Of(realm.Engine, box))
            : DomRects.List(realm);
    }

    /// <summary>https://drafts.csswg.org/cssom-view/#dom-element-clientwidth.</summary>
    internal static JsValue ClientWidth(DomRealm realm, IElement element)
        => JsNumber.Create(IsScrollingElement(element) ? Viewport(realm).Width : Round(ClientBox(realm, element).Width));

    /// <summary>https://drafts.csswg.org/cssom-view/#dom-element-clientheight.</summary>
    internal static JsValue ClientHeight(DomRealm realm, IElement element)
        => JsNumber.Create(IsScrollingElement(element) ? Viewport(realm).Height : Round(ClientBox(realm, element).Height));

    /// <summary>https://drafts.csswg.org/cssom-view/#dom-element-scrollwidth.</summary>
    /// <remarks>Never wider than the viewport: nothing here lays out horizontally.</remarks>
    internal static JsValue ScrollWidth(DomRealm realm, IElement element)
        => ClientWidth(realm, element);

    /// <summary>https://drafts.csswg.org/cssom-view/#dom-element-scrollheight.</summary>
    internal static JsValue ScrollHeight(DomRealm realm, IElement element)
    {
        if (!IsScrollingElement(element))
        {
            return JsNumber.Create(Round(ClientBox(realm, element).Height));
        }

        var layout = Layout(realm);
        return JsNumber.Create(layout is null ? 0 : Round(Math.Max(layout.ContentHeight, layout.ViewportHeight)));
    }

    /// <summary>https://drafts.csswg.org/cssom-view/#dom-element-scrolltop.</summary>
    internal static JsValue ScrollTop(DomRealm realm, IElement element)
        => JsNumber.Create(IsScrollingElement(element) && PageOf(realm) is { } page ? page.Layout.ScrollY : 0);

    /// <summary>The other half of <see cref="ScrollTop"/>: writing it scrolls the page.</summary>
    internal static JsValue SetScrollTop(DomRealm realm, IElement element, JsValue[] arguments)
    {
        if (IsScrollingElement(element) && PageOf(realm) is { } page)
        {
            page.Layout.ScrollTo(TypeConverter.ToNumber(arguments.At(0)));
        }

        return JsValue.Undefined;
    }

    /// <summary>https://drafts.csswg.org/cssom-view/#dom-element-scrollleft, which is always zero.</summary>
    /// <remarks>Every box is exactly as wide as the viewport, so nothing ever overflows sideways.</remarks>
    internal static JsValue ScrollLeft(DomRealm realm, IElement element) => JsNumber.PositiveZero;

    /// <summary>The other half of <see cref="ScrollLeft"/>, which changes nothing.</summary>
    internal static JsValue SetScrollLeft(DomRealm realm, IElement element, JsValue[] arguments) => JsValue.Undefined;

    /// <summary>
    /// https://drafts.csswg.org/cssom-view/#dom-element-clienttop and <c>clientLeft</c>, both always zero.
    /// </summary>
    /// <remarks>They are the widths of the top and left borders, and nothing here has a border.</remarks>
    internal static JsValue ClientEdge(DomRealm realm, IElement element) => JsNumber.PositiveZero;

    /// <summary>https://drafts.csswg.org/cssom-view/#dom-element-scrollintoview.</summary>
    /// <remarks>
    /// The argument is either a boolean — <see langword="true"/> aligning the element to the top of the
    /// window and <see langword="false"/> to the bottom, which is what the legacy signature means — or the
    /// options dictionary, whose <c>block</c> member this reads and whose <c>behavior</c> and <c>inline</c>
    /// members change nothing: there is no smooth scrolling to do and nothing to align horizontally.
    /// </remarks>
    internal static JsValue ScrollIntoView(DomRealm realm, IElement element, JsValue[] arguments)
    {
        PageOf(realm)?.Layout.ScrollIntoView(element, Block(arguments));
        return JsValue.Undefined;
    }

    /// <summary>https://drafts.csswg.org/cssom-view/#dom-htmlelement-offsetwidth.</summary>
    internal static JsValue OffsetWidth(DomRealm realm, IElement element)
        => JsNumber.Create(Round(ClientBox(realm, element).Width));

    /// <summary>https://drafts.csswg.org/cssom-view/#dom-htmlelement-offsetheight.</summary>
    internal static JsValue OffsetHeight(DomRealm realm, IElement element)
        => JsNumber.Create(Round(ClientBox(realm, element).Height));

    /// <summary>https://drafts.csswg.org/cssom-view/#dom-htmlelement-offsetleft, which is always zero.</summary>
    internal static JsValue OffsetLeft(DomRealm realm, IElement element) => JsNumber.PositiveZero;

    /// <summary>
    /// https://drafts.csswg.org/cssom-view/#dom-htmlelement-offsettop — the distance from the top of the
    /// offset parent, in document coordinates rather than viewport ones.
    /// </summary>
    internal static JsValue OffsetTop(DomRealm realm, IElement element)
    {
        if (Layout(realm) is not { } layout || layout.DocumentBoxOf(element) is not { } box)
        {
            return JsNumber.PositiveZero;
        }

        var parent = OffsetParentOf(element);
        var origin = parent is not null && layout.DocumentBoxOf(parent) is { } parentBox ? parentBox.Y : 0;
        return JsNumber.Create(Round(box.Y - origin));
    }

    /// <summary>
    /// https://drafts.csswg.org/cssom-view/#dom-htmlelement-offsetparent — the body, or nothing.
    /// </summary>
    /// <remarks>
    /// Nothing here is positioned, so HTML's walk up to the nearest positioned ancestor always ends at the
    /// body — which is exactly what a browser answers for an unpositioned element. The body itself, the
    /// document element and an element with no box answer <see langword="null"/>, as they do in Chrome.
    /// </remarks>
    internal static JsValue OffsetParent(DomRealm realm, IElement element)
    {
        if (Layout(realm)?.DocumentBoxOf(element) is null)
        {
            return JsValue.Null;
        }

        return OffsetParentOf(element) is { } parent ? realm.WrapNode(parent) : JsValue.Null;
    }

    /// <summary>https://drafts.csswg.org/cssom-view/#dom-document-elementfrompoint.</summary>
    internal static JsValue ElementFromPoint(DomRealm realm, JsValue[] arguments)
    {
        var hit = Layout(realm)?.ElementFromPoint(Coordinate(arguments, 0), Coordinate(arguments, 1));
        return hit is null ? JsValue.Null : realm.WrapNode(hit);
    }

    /// <summary>
    /// https://drafts.csswg.org/cssom-view/#dom-document-elementsfrompoint — the hit element and every
    /// rendered ancestor above it, innermost first.
    /// </summary>
    internal static JsValue ElementsFromPoint(DomRealm realm, JsValue[] arguments)
    {
        var hits = new List<INode>();

        if (Layout(realm) is { } layout &&
            layout.ElementFromPoint(Coordinate(arguments, 0), Coordinate(arguments, 1)) is { } hit)
        {
            for (IElement? element = hit; element is not null; element = element.ParentElement)
            {
                if (layout.OrdinalOf(element) >= 0)
                {
                    hits.Add(element);
                }
            }
        }

        return DomConvert.NodeSequence(realm, hits);
    }

    /// <summary>
    /// https://drafts.csswg.org/cssom-view/#dom-document-scrollingelement — the document element.
    /// </summary>
    /// <remarks>
    /// Standards mode always: AngleSharp's parser produces no quirks-mode document this package ever loads
    /// with a <c>&lt;body&gt;</c> as its scrolling element, and a page uses this member to find the one
    /// thing whose <c>scrollTop</c> moves the window.
    /// </remarks>
    internal static JsValue ScrollingElement(DomRealm realm, IDocument document)
        => realm.WrapNodeValue(document.DocumentElement);

    /// <summary>The element whose <c>scrollTop</c> is the page's own scroll offset.</summary>
    internal static bool IsScrollingElement(IElement element)
        => ReferenceEquals(element, element.Owner?.DocumentElement);

    /// <summary>The offset parent of <paramref name="element"/>, which is the body or nothing.</summary>
    private static IElement? OffsetParentOf(IElement element)
    {
        var body = element.Owner?.Body;
        return body is null || ReferenceEquals(element, body) || ReferenceEquals(element, element.Owner?.DocumentElement)
            ? null
            : body;
    }

    /// <summary>The element's viewport-relative box, or the empty one when it has none.</summary>
    private static FlatBox ClientBox(DomRealm realm, IElement element)
        => Layout(realm)?.ClientBoxOf(element) ?? FlatBox.Empty;

    /// <summary>The layout of the page this realm belongs to, or <see langword="null"/> when there is none.</summary>
    private static FlatLayout? Layout(DomRealm realm) => PageOf(realm)?.Layout.Current();

    private static PageRuntime? PageOf(DomRealm realm) => PageRuntime.Find(realm.Engine);

    private static Viewport Viewport(DomRealm realm) => PageOf(realm)?.Viewport ?? new Viewport(0, 0);

    /// <summary>
    /// CSSOM View declares every one of these <c>long</c>, so the box's <c>double</c> is rounded exactly as
    /// WebIDL's own conversion would round it.
    /// </summary>
    private static double Round(double value) => Math.Round(value, MidpointRounding.ToEven);

    private static double Coordinate(JsValue[] arguments, int index) => TypeConverter.ToNumber(arguments.At(index));

    private static string Block(JsValue[] arguments)
    {
        var argument = arguments.At(0);

        if (argument.IsBoolean())
        {
            return argument.AsBoolean() ? "start" : "end";
        }

        if (argument is ObjectInstance options && options.Get("block") is { } block && !block.IsUndefined())
        {
            return TypeConverter.ToString(block);
        }

        return "start";
    }
}
