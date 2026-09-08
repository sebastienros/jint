using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Xml.Dom;
using Jint.Native;

namespace Jint.Browser.Dom;

/// <summary>
/// The interfaces this package declares itself, because AngleSharp has no <c>[DomName]</c> interface for them
/// and the generator can therefore never see one.
/// </summary>
/// <remarks>
/// <para>
/// <b>There are three, and they answer three different gaps.</b> <c>HTMLFrameSetElement</c> is a node
/// AngleSharp builds and cannot name, so it is chosen by <em>local name</em> where everything else is chosen
/// by CLR type; <c>XMLDocument</c> has a CLR interface but no <c>[DomName]</c>, so it is declared here and
/// chosen by that interface; <c>StaticRange</c> is not AngleSharp's at all, so it is chosen by a CLR type of
/// this package's own that no node ever takes. The first is the harder one to see, and is this: AngleSharp models
/// <c>&lt;frameset&gt;</c> with the plain <c>IHtmlElement</c> — there is no <c>IHtmlFrameSetElement</c> and no
/// <c>[DomName("HTMLFrameSetElement")]</c> anywhere in the pinned assemblies — so <c>DomTypeMap</c>, which
/// keys on the CLR type, cannot tell a frameset from a <c>&lt;div&gt;</c>. The events bridge already makes the
/// same test the same way (<c>EventHandlerContentAttributes.TargetFor</c>), because a frameset carries the
/// window-forwarded handler attributes a <c>&lt;body&gt;</c> does.
/// </para>
/// <para>
/// <b>The interface exists so that three things are true</b>: <c>frameset instanceof HTMLFrameSetElement</c>
/// holds, <c>HTMLFrameSetElement</c> is a name on the window, and <c>Object.prototype.toString</c> reports it.
/// <c>dom/events/Body-FrameSet-Event-Handlers.html</c> is what asks for all three, and it asks first: it
/// reaches for the name at file scope, so without it the whole document is a harness error rather than a run
/// with two failures. Its <c>WindowEventHandlers</c> are on <c>HTMLElement</c> here along with
/// <c>GlobalEventHandlers</c>, so the shape carries only the two members HTML gives the interface itself —
/// <c>cols</c> and <c>rows</c>, which <c>html/dom/reflection-obsolete.html</c> asks for and which the shape
/// did not have while this file said HTML gave it none.
/// </para>
/// <para>
/// <b>Those two are reflected attributes written by hand</b>, which is the one place in this package that
/// happens. <see cref="ReflectedAttribute"/> is the shared implementation and the descriptors normally come
/// from <c>overrides.json</c>'s <c>reflected</c> list — but that list is read against the interfaces the
/// generator can see, and this one is not among them by definition. So the descriptor is declared here, next
/// to the shape that names it, and the algorithm is still the one every other reflected member takes.
/// </para>
/// <para>
/// The indices continue <c>DomInterfaces</c>' own, which is what keeps <see cref="DomRealm"/>'s per-engine
/// arrays a dense array rather than a dictionary.
/// </para>
/// </remarks>
internal static class DomManualInterfaces
{
    /// <summary><c>HTMLFrameSetElement.cols</c>, a reflected <c>DOMString</c> (HTML §16.3.3).</summary>
    private static readonly ReflectedAttribute _frameSetCols =
        ReflectedAttribute.Text("HTMLFrameSetElement.cols", "cols");

    /// <summary><c>HTMLFrameSetElement.rows</c>, a reflected <c>DOMString</c> (HTML §16.3.3).</summary>
    private static readonly ReflectedAttribute _frameSetRows =
        ReflectedAttribute.Text("HTMLFrameSetElement.rows", "rows");

    /// <summary>https://html.spec.whatwg.org/multipage/obsolete.html#htmlframesetelement.</summary>
    internal static readonly DomInterfaceDefinition HTMLFrameSetElement = new(
        "HTMLFrameSetElement",
        typeof(IHtmlElement),
        static () => new JsObjectShape.Builder()
            .PerRealmSlot("constructor", enumerable: false)
            .ToStringTag("HTMLFrameSetElement")
            .Accessor("cols", Reflected(_frameSetCols), ReflectedSetter(_frameSetCols))
            .Accessor("rows", Reflected(_frameSetRows), ReflectedSetter(_frameSetRows))
            .Build(),
        DomInterfaces.HTMLElement,
        rootsAtEventTarget: true,
        hasInterfaceObject: true,
        DomWrapperKind.Node)
    {
        Index = DomInterfaces.All.Length,
    };

    /// <summary>
    /// https://dom.spec.whatwg.org/#xmldocument. AngleSharp exposes <see cref="IXmlDocument"/> but gives it
    /// no <c>[DomName]</c>, so the generated interface table cannot see the WebIDL interface. The explicit
    /// wrapper selected by <c>new Document()</c> remains <c>Document</c>; every other XML document and its
    /// clones take this interface.
    /// </summary>
    internal static readonly DomInterfaceDefinition XMLDocument = new(
        "XMLDocument",
        typeof(IXmlDocument),
        static () => new JsObjectShape.Builder()
            .PerRealmSlot("constructor", enumerable: false)
            .ToStringTag("XMLDocument")
            .Build(),
        DomInterfaces.Document,
        rootsAtEventTarget: true,
        hasInterfaceObject: true,
        DomWrapperKind.Node)
    {
        Index = DomInterfaces.All.Length + 1,
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
        Index = DomInterfaces.All.Length + 2,
    };

    /// <summary>Every manual interface, in index order.</summary>
    internal static readonly DomInterfaceDefinition[] All = [HTMLFrameSetElement, XMLDocument, StaticRange];

    /// <summary>
    /// A reflected attribute's getter, wrapped in the same failure guard and the same receiver check every
    /// generated member body is.
    /// </summary>
    /// <remarks>
    /// The receiver is bound as <see cref="IHtmlElement"/> and not as a frameset type, because there is no
    /// frameset type to bind: that absence is the whole reason this interface is declared by local name. A
    /// receiver of any other interface therefore reaches the descriptor, which is exactly what a
    /// <c>Function.prototype.call</c> onto a <c>&lt;div&gt;</c> does in a browser — the member reads that
    /// element's own content attribute rather than raising.
    /// </remarks>
    private static Func<JsValue, JsValue[], JsValue> Reflected(ReflectedAttribute attribute)
        => DomFailures.Guard(attribute.Member, (thisObject, _) =>
        {
            var self = DomBindings.Bind<IHtmlElement>(thisObject, attribute.Member);
            return attribute.Get(self.Target);
        });

    /// <summary>The same member's setter.</summary>
    private static Func<JsValue, JsValue[], JsValue> ReflectedSetter(ReflectedAttribute attribute)
        => DomFailures.Guard(attribute.Member, (thisObject, arguments) =>
        {
            var self = DomBindings.Bind<IHtmlElement>(thisObject, attribute.Member);
            return attribute.Set(self.Realm, self.Target, arguments);
        });

    /// <summary>
    /// The interface a node takes when its CLR type does not decide it, or <see langword="null"/> when
    /// <c>DomTypeMap</c> is the answer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three node kinds need an answer here rather than from generated CLR metadata. XML documents have an
    /// unannotated CLR interface; the other two are HTML's rules for a frameset and a custom element name.
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
        if (node is IXmlDocument)
        {
            return XMLDocument;
        }

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
