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
/// <b>There are three gaps and three answers.</b> Five HTML element interfaces are nodes AngleSharp builds
/// and cannot name, so each is chosen by <em>local name</em> where everything else is chosen by CLR type;
/// <c>XMLDocument</c> has a CLR interface but no <c>[DomName]</c>, so it is declared here and chosen by that
/// interface; <c>StaticRange</c> is not AngleSharp's at all, so it is chosen by a CLR type of this package's
/// own that no node ever takes. The first is the harder one to see, and is this: AngleSharp models
/// <c>&lt;dl&gt;</c>, <c>&lt;dir&gt;</c>, <c>&lt;font&gt;</c>, <c>&lt;frame&gt;</c> and
/// <c>&lt;frameset&gt;</c> with internal sealed classes whose only public interface is
/// <c>IHtmlElement</c> — there is no <c>IHtmlDListElement</c>, no <c>IHtmlFrameElement</c> and no
/// <c>[DomName]</c> for any of the five WebIDL interfaces anywhere in the pinned assemblies — so
/// <c>DomTypeMap</c>, which keys on the CLR type, cannot tell any of them from a <c>&lt;div&gt;</c>. The
/// events bridge already makes the same test the same way (<c>EventHandlerContentAttributes.TargetFor</c>),
/// because a frameset carries the window-forwarded handler attributes a <c>&lt;body&gt;</c> does.
/// </para>
/// <para>
/// <b>An interface exists so that three things are true</b>: <c>el instanceof HTMLFontElement</c> holds,
/// <c>HTMLFontElement</c> is a name on the window, and <c>Object.prototype.toString</c> reports it.
/// <c>dom/events/Body-FrameSet-Event-Handlers.html</c> is what asks for all three of the frameset's, and it
/// asks first: it reaches for the name at file scope, so without it the whole document is a harness error
/// rather than a run with two failures. <c>dom/nodes/Node-cloneNode.html</c> asks for the other four, and
/// <c>html/dom/reflection-grouping.html</c> and <c>-obsolete.html</c> ask for their members.
/// </para>
/// <para>
/// <b>Their members are reflected attributes written by hand</b>, which is the one place in this package that
/// happens. <see cref="ReflectedAttribute"/> is the shared implementation and the descriptors normally come
/// from <c>overrides.json</c>'s <c>reflected</c> list — but that list is read against the interfaces the
/// generator can see, and these five are not among them by definition. So the descriptors are declared here,
/// next to the shapes that name them, and the algorithm is still the one every other reflected member takes.
/// The alternative — declaring the members on <c>HTMLElement</c>, the interface these elements do have —
/// would give <c>compact</c> and <c>noResize</c> to every element in the document, which is worse than not
/// having them.
/// </para>
/// <para>
/// <b><c>HTMLFrameElement</c>'s <c>contentDocument</c> and <c>contentWindow</c> are deliberately absent.</b>
/// A frame's document is reachable only where the page runtime loaded one, and it loads none for a
/// <c>&lt;frame&gt;</c>: <c>Runtime/Parsing/PageResourceLoader</c> dispatches a subresource request by the
/// element that made it and has an arm for <c>IHtmlInlineFrameElement</c> only, so a <c>&lt;frame src&gt;</c>
/// is refused like any other subresource this package does not fetch. AngleSharp's own
/// <c>HtmlFrameElementBase.ContentDocument</c> is on an internal class reachable through no public interface,
/// so there is nothing to answer from either. Two readonly members that could only ever answer <c>null</c>
/// would say a browsing context exists where none does; nothing in the corpus asks for them.
/// </para>
/// <para>
/// The indices continue <c>DomInterfaces</c>' own, which is what keeps <see cref="DomRealm"/>'s per-engine
/// arrays a dense array rather than a dictionary.
/// </para>
/// </remarks>
internal static class DomManualInterfaces
{
    /// <summary>
    /// <c>HTMLDListElement.compact</c> — the one member
    /// <a href="https://html.spec.whatwg.org/multipage/obsolete.html#HTMLDListElement-partial">HTML §16.3.3</a>
    /// adds to <a href="https://html.spec.whatwg.org/multipage/grouping-content.html#the-dl-element">§4.4.9</a>'s
    /// interface.
    /// </summary>
    private static readonly ReflectedAttribute[] _dListMembers =
    [
        ReflectedAttribute.Boolean("HTMLDListElement.compact", "compact"),
    ];

    /// <summary>
    /// <c>HTMLDirectoryElement.compact</c>
    /// (<a href="https://html.spec.whatwg.org/multipage/obsolete.html#htmldirectoryelement">HTML §16.3.3</a>).
    /// </summary>
    private static readonly ReflectedAttribute[] _directoryMembers =
    [
        ReflectedAttribute.Boolean("HTMLDirectoryElement.compact", "compact"),
    ];

    /// <summary>
    /// <c>HTMLFontElement</c>'s three
    /// (<a href="https://html.spec.whatwg.org/multipage/obsolete.html#htmlfontelement">HTML §16.3.3</a>). Only
    /// <c>color</c> is <c>[LegacyNullToEmptyString]</c>, so <c>font.face = null</c> writes <c>"null"</c> where
    /// <c>font.color = null</c> writes the empty string.
    /// </summary>
    private static readonly ReflectedAttribute[] _fontMembers =
    [
        ReflectedAttribute.Text("HTMLFontElement.color", "color", legacyNullToEmptyString: true),
        ReflectedAttribute.Text("HTMLFontElement.face", "face"),
        ReflectedAttribute.Text("HTMLFontElement.size", "size"),
    ];

    /// <summary>
    /// <c>HTMLFrameElement</c>'s eight reflected members, in the IDL's own order
    /// (<a href="https://html.spec.whatwg.org/multipage/obsolete.html#htmlframeelement">HTML §16.3.3</a>).
    /// <c>src</c> and <c>longDesc</c> are <c>USVString</c>s whose content attribute contains a URL, so both
    /// answer the absolute URL the attribute resolves to; the two margins are
    /// <c>[LegacyNullToEmptyString]</c>, exactly as <c>HTMLIFrameElement</c>'s pair already is.
    /// </summary>
    private static readonly ReflectedAttribute[] _frameMembers =
    [
        ReflectedAttribute.Text("HTMLFrameElement.name", "name"),
        ReflectedAttribute.Text("HTMLFrameElement.scrolling", "scrolling"),
        ReflectedAttribute.Url("HTMLFrameElement.src", "src"),
        ReflectedAttribute.Text("HTMLFrameElement.frameBorder", "frameborder"),
        ReflectedAttribute.Url("HTMLFrameElement.longDesc", "longdesc"),
        ReflectedAttribute.Boolean("HTMLFrameElement.noResize", "noresize"),
        ReflectedAttribute.Text("HTMLFrameElement.marginHeight", "marginheight", legacyNullToEmptyString: true),
        ReflectedAttribute.Text("HTMLFrameElement.marginWidth", "marginwidth", legacyNullToEmptyString: true),
    ];

    /// <summary>
    /// <c>HTMLFrameSetElement</c>'s two
    /// (<a href="https://html.spec.whatwg.org/multipage/obsolete.html#htmlframesetelement">HTML §16.3.3</a>).
    /// Its <c>WindowEventHandlers</c> are on <c>HTMLElement</c> here along with <c>GlobalEventHandlers</c>, so
    /// the shape carries only these.
    /// </summary>
    private static readonly ReflectedAttribute[] _frameSetMembers =
    [
        ReflectedAttribute.Text("HTMLFrameSetElement.cols", "cols"),
        ReflectedAttribute.Text("HTMLFrameSetElement.rows", "rows"),
    ];

    /// <summary>https://html.spec.whatwg.org/multipage/obsolete.html#htmldirectoryelement.</summary>
    internal static readonly DomInterfaceDefinition HTMLDirectoryElement =
        ElementInterface("HTMLDirectoryElement", _directoryMembers);

    /// <summary>https://html.spec.whatwg.org/multipage/grouping-content.html#the-dl-element.</summary>
    internal static readonly DomInterfaceDefinition HTMLDListElement =
        ElementInterface("HTMLDListElement", _dListMembers);

    /// <summary>https://html.spec.whatwg.org/multipage/obsolete.html#htmlfontelement.</summary>
    internal static readonly DomInterfaceDefinition HTMLFontElement =
        ElementInterface("HTMLFontElement", _fontMembers);

    /// <summary>https://html.spec.whatwg.org/multipage/obsolete.html#htmlframeelement.</summary>
    internal static readonly DomInterfaceDefinition HTMLFrameElement =
        ElementInterface("HTMLFrameElement", _frameMembers);

    /// <summary>https://html.spec.whatwg.org/multipage/obsolete.html#htmlframesetelement.</summary>
    internal static readonly DomInterfaceDefinition HTMLFrameSetElement =
        ElementInterface("HTMLFrameSetElement", _frameSetMembers);

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
        DomWrapperKind.Node);

    /// <summary>
    /// https://dom.spec.whatwg.org/#staticrange. AngleSharp has no <c>StaticRange</c> and no
    /// <c>AbstractRange</c> — the interface is four values a page hands over, so there is nothing for it to
    /// model — which is why this one is declared by CLR type where the element interfaces are declared by
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
        constructorLength: DomStaticRange.ConstructorLength);

    /// <summary>Every manual interface, in index order.</summary>
    internal static readonly DomInterfaceDefinition[] All =
    [
        HTMLDirectoryElement,
        HTMLDListElement,
        HTMLFontElement,
        HTMLFrameElement,
        HTMLFrameSetElement,
        XMLDocument,
        StaticRange,
    ];

    /// <summary>
    /// Continues <c>DomInterfaces</c>' dense numbering, which is what <see cref="DomRealm"/> indexes its
    /// per-engine prototype and interface-object arrays by. It is done here rather than in each declaration
    /// so that adding a row cannot silently collide with one already numbered.
    /// </summary>
    static DomManualInterfaces()
    {
        for (var i = 0; i < All.Length; i++)
        {
            All[i].Index = DomInterfaces.All.Length + i;
        }
    }

    /// <summary>
    /// One of HTML's element interfaces that AngleSharp models with a plain <c>IHtmlElement</c>: an
    /// <c>HTMLElement</c> subclass whose shape is the <c>constructor</c> slot, the tag and its own reflected
    /// members.
    /// </summary>
    private static DomInterfaceDefinition ElementInterface(string name, ReflectedAttribute[] members)
        => new(
            name,
            typeof(IHtmlElement),
            () => Shape(name, members),
            DomInterfaces.HTMLElement,
            rootsAtEventTarget: true,
            hasInterfaceObject: true,
            DomWrapperKind.Node);

    /// <summary>Builds one such interface's prototype shape, on the first engine that asks for it.</summary>
    private static JsObjectShape Shape(string name, ReflectedAttribute[] members)
    {
        var builder = new JsObjectShape.Builder()
            .PerRealmSlot("constructor", enumerable: false)
            .ToStringTag(name);

        foreach (var member in members)
        {
            builder.Accessor(MemberNameOf(member), Reflected(member), ReflectedSetter(member));
        }

        return builder.Build();
    }

    /// <summary>
    /// The IDL attribute's own name, taken from the qualified one the descriptor already carries so that the
    /// property a page reads and the name a refusal prints can never disagree.
    /// </summary>
    private static string MemberNameOf(ReflectedAttribute attribute)
        => attribute.Member[(attribute.Member.LastIndexOf('.') + 1)..];

    /// <summary>
    /// A reflected attribute's getter, wrapped in the same failure guard and the same receiver check every
    /// generated member body is.
    /// </summary>
    /// <remarks>
    /// The receiver is bound as <see cref="IHtmlElement"/> and not as the element's own type, because there is
    /// no such type to bind: that absence is the whole reason these interfaces are declared by local name. A
    /// receiver of any other interface therefore reaches the descriptor, which is exactly what a
    /// <c>Function.prototype.call</c> onto a <c>&lt;div&gt;</c> does in a browser — the member reads that
    /// element's own content attribute rather than raising.
    /// </remarks>
    private static Func<JsValue, JsValue[], JsValue> Reflected(ReflectedAttribute attribute)
    {
        if (attribute.ReflectsUrl)
        {
            // HTML §2.6.1's URL reflection resolves the content attribute against the document base, and
            // inside a page runtime that is the runtime's current base rather than the parsed document's.
            // It is the one kind whose getter needs the realm, which is the same distinction `ModelBuilder`
            // makes for every generated `url` row and for no other.
            return DomFailures.Guard(attribute.Member, (thisObject, _) =>
            {
                var self = DomBindings.Bind<IHtmlElement>(thisObject, attribute.Member);
                return attribute.Get(self.Realm, self.Target);
            });
        }

        return DomFailures.Guard(attribute.Member, (thisObject, _) =>
        {
            var self = DomBindings.Bind<IHtmlElement>(thisObject, attribute.Member);
            return attribute.Get(self.Target);
        });
    }

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
    /// unannotated CLR interface; the other two are HTML's rules for the five elements above and for a custom
    /// element name.
    /// <a href="https://html.spec.whatwg.org/multipage/dom.html#htmlunknownelement">The element interface
    /// for a name in the HTML namespace</a> is <c>HTMLElement</c> when the name is a valid custom element
    /// name and <c>HTMLUnknownElement</c> otherwise — so an undefined <c>&lt;my-el&gt;</c> is an
    /// <c>HTMLElement</c>, which is what a page tests before anything is defined and what the element
    /// keeps until an upgrade swaps its wrapper's prototype for the constructor's.
    /// </para>
    /// <para>
    /// AngleSharp builds an <c>HtmlUnknownElement</c> for both, so the name is the only thing separating
    /// them. The test costs a character scan, and only for an element AngleSharp could not identify. The five
    /// local names cost one switch over a string already in hand, and none of them can be an unknown element:
    /// each is a name the HTML parser knows.
    /// </para>
    /// </remarks>
    internal static DomInterfaceDefinition? For(INode node)
    {
        if (node is IXmlDocument)
        {
            return XMLDocument;
        }

        if (node is IHtmlElement html && ByLocalName(html.LocalName) is { } declared)
        {
            return declared;
        }

        if (node is IHtmlUnknownElement unknown && CustomElements.CustomElementNames.IsValid(unknown.LocalName))
        {
            return DomInterfaces.HTMLElement;
        }

        return null;
    }

    /// <summary>
    /// The interface HTML gives an element of this local name, for the six AngleSharp cannot name — and
    /// <see langword="null"/> for every other element, which is nearly all of them.
    /// </summary>
    /// <remarks>
    /// <c>applet</c> is the one row here that names a <b>generated</b> interface rather than one declared
    /// above, and it is the opposite kind of gap: HTML <i>removed</i> <c>HTMLAppletElement</c>
    /// (https://html.spec.whatwg.org/multipage/obsolete.html#htmlappletelement), so the element takes the
    /// <c>HTMLUnknownElement</c> every unlisted HTML name takes. AngleSharp still builds an
    /// <c>HtmlAppletElement</c> implementing nothing narrower than <c>IHtmlElement</c>, so
    /// <see cref="DomTypeMap"/> would answer <c>HTMLElement</c> — which is why the local name has to decide
    /// it here too.
    /// </remarks>
    private static DomInterfaceDefinition? ByLocalName(string localName) => localName switch
    {
        "applet" => DomInterfaces.HTMLUnknownElement,
        "dir" => HTMLDirectoryElement,
        "dl" => HTMLDListElement,
        "font" => HTMLFontElement,
        "frame" => HTMLFrameElement,
        "frameset" => HTMLFrameSetElement,
        _ => null,
    };
}
