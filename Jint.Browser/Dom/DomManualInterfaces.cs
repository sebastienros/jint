using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Jint.Native;

namespace Jint.Browser.Dom;

/// <summary>
/// The interfaces this package declares itself, because AngleSharp has no <c>[DomName]</c> interface for them
/// and the generator can therefore never see one.
/// </summary>
/// <remarks>
/// <para>
/// <b>There are two, and they are here for opposite reasons.</b> <c>HTMLFrameSetElement</c> is a node
/// AngleSharp builds and cannot name, so it is chosen by <em>local name</em> where everything else is chosen
/// by CLR type; <c>StaticRange</c> is not AngleSharp's at all, so it is chosen by a CLR type of this
/// package's own that no node ever takes. The first is the harder one to see, and is this: AngleSharp models
/// <c>&lt;frameset&gt;</c> with the plain <c>IHtmlElement</c> — there is no <c>IHtmlFrameSetElement</c> and no
/// <c>[DomName("HTMLFrameSetElement")]</c> anywhere in the pinned assemblies — so <c>DomTypeMap</c>, which
/// keys on the CLR type, cannot tell a frameset from a <c>&lt;div&gt;</c>. The events bridge already makes the
/// same test the same way (<c>EventHandlerContentAttributes.TargetFor</c>), because a frameset carries the
/// window-forwarded handler attributes a <c>&lt;body&gt;</c> does.
/// </para>
/// <para>
/// <b>The shape is empty on purpose.</b> HTML gives <c>HTMLFrameSetElement</c> no members of its own beyond
/// <c>WindowEventHandlers</c>, which this package puts on <c>HTMLElement</c> along with
/// <c>GlobalEventHandlers</c>; the interface exists so that <c>frameset instanceof HTMLFrameSetElement</c>
/// holds, so that <c>HTMLFrameSetElement</c> is a name on the window, and so that
/// <c>Object.prototype.toString</c> reports it. <c>dom/events/Body-FrameSet-Event-Handlers.html</c> is what
/// asks for all three, and it asks first: it reaches for the name at file scope, so without it the whole
/// document is a harness error rather than a run with two failures.
/// </para>
/// <para>
/// The indices continue <c>DomInterfaces</c>' own, which is what keeps <see cref="DomRealm"/>'s per-engine
/// arrays a dense array rather than a dictionary.
/// </para>
/// </remarks>
internal static class DomManualInterfaces
{
    /// <summary>https://html.spec.whatwg.org/multipage/obsolete.html#htmlframesetelement.</summary>
    internal static readonly DomInterfaceDefinition HTMLFrameSetElement = new(
        "HTMLFrameSetElement",
        typeof(IHtmlElement),
        static () => new JsObjectShape.Builder()
            .PerRealmSlot("constructor", enumerable: false)
            .ToStringTag("HTMLFrameSetElement")
            .Build(),
        DomInterfaces.HTMLElement,
        rootsAtEventTarget: true,
        hasInterfaceObject: true,
        DomWrapperKind.Node)
    {
        Index = DomInterfaces.All.Length,
    };

    /// <summary>
    /// https://dom.spec.whatwg.org/#staticrange. AngleSharp has no <c>StaticRange</c> and no
    /// <c>AbstractRange</c> — the interface is four values a page hands over, so there is nothing for it to
    /// model — which is why this one is declared by CLR type where <c>HTMLFrameSetElement</c> is declared by
    /// local name: the type is <see cref="DomStaticRange.State"/>, ours, and no node ever takes it.
    /// <see cref="DomStaticRange"/> carries the algorithm and the shape.
    /// </summary>
    internal static readonly DomInterfaceDefinition StaticRange = new(
        "StaticRange",
        typeof(DomStaticRange.State),
        DomStaticRange.Shape,
        parent: null,
        rootsAtEventTarget: false,
        hasInterfaceObject: true,
        DomWrapperKind.Object,
        constructorLength: DomStaticRange.ConstructorLength)
    {
        Index = DomInterfaces.All.Length + 1,
    };

    /// <summary>Every manual interface, in index order.</summary>
    internal static readonly DomInterfaceDefinition[] All = [HTMLFrameSetElement, StaticRange];

    /// <summary>
    /// The interface a node takes when its CLR type does not decide it, or <see langword="null"/> when
    /// <c>DomTypeMap</c> is the answer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two nodes need an answer here rather than from the CLR type, and the second is HTML's rule for a
    /// custom element name.
    /// <a href="https://html.spec.whatwg.org/multipage/dom.html#htmlunknownelement">The element interface
    /// for a name in the HTML namespace</a> is <c>HTMLElement</c> when the name is a valid custom element
    /// name and <c>HTMLUnknownElement</c> otherwise — so an undefined <c>&lt;my-el&gt;</c> is an
    /// <c>HTMLElement</c>, which is what a page tests before anything is defined and what the element
    /// keeps until an upgrade swaps its wrapper's prototype for the constructor's.
    /// </para>
    /// <para>
    /// AngleSharp builds an <c>HtmlUnknownElement</c> for both, so the name is the only thing separating
    /// them. The test costs a character scan, and only for an element AngleSharp could not identify.
    /// </para>
    /// </remarks>
    internal static DomInterfaceDefinition? For(INode node)
    {
        if (node is IHtmlElement { LocalName: "frameset" })
        {
            return HTMLFrameSetElement;
        }

        if (node is IHtmlUnknownElement unknown && CustomElements.CustomElementNames.IsValid(unknown.LocalName))
        {
            return DomInterfaces.HTMLElement;
        }

        return null;
    }
}
